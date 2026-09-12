using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Persistence;

namespace Pmcs.Modules.Intelligence.Services;

internal sealed partial class AdvisoryGenerationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AdvisoryGenerationWorker> logger) : BackgroundService
{
    private const int MaximumAttempts = 3;
    private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bool.TryParse(configuration["AdvisoryIntelligence:WorkerEnabled"], out var enabled) && !enabled)
        {
            LogDisabled(logger);
            return;
        }

        var interval = int.TryParse(configuration["AdvisoryIntelligence:PollSeconds"], out var seconds) && seconds >= 1
            ? TimeSpan.FromSeconds(seconds)
            : DefaultPollingInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(stoppingToken))
                {
                    // Drain ready requests before polling again.
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogLoopFailed(logger, exception);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        ClaimedRequest? claimed;
        await using (var claimScope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = claimScope.ServiceProvider.GetRequiredService<IntelligenceDbContext>();
            var clock = claimScope.ServiceProvider.GetRequiredService<IClock>();
            claimed = await ClaimNextAsync(dbContext, clock.UtcNow, cancellationToken);
        }

        if (claimed is null)
        {
            return false;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var assembler = scope.ServiceProvider.GetRequiredService<PermissionAwareContextAssembler>();
            var modelClient = scope.ServiceProvider.GetRequiredService<IAdvisoryModelClient>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var context = await assembler.AssembleAsync(
                claimed.TenantId, claimed.ProjectId, claimed.RequestedBy, cancellationToken);
            var result = await modelClient.GenerateAsync(claimed.Id, context, cancellationToken);
            var output = AdvisoryOutputValidator.NormalizeAndValidate(
                result.Output, context.AllowedEvidenceReferences);
            var generatedAt = clock.UtcNow;
            var insight = AdvisoryInsight.Create(
                Guid.NewGuid(),
                claimed.TenantId,
                claimed.ProjectId,
                claimed.Id,
                context.SnapshotId,
                claimed.RequestedBy,
                output,
                context.ContextHash,
                context.IncludesFinancialData,
                context.IncludesCommercialData,
                context.IncludesActionData,
                result.Provider,
                result.Model,
                result.ProviderResponseId,
                OpenAiResponsesClient.PromptVersion,
                OpenAiResponsesClient.PolicyVersion,
                generatedAt,
                generatedAt.AddHours(24));
            await PersistSuccessAsync(scope.ServiceProvider, claimed, insight, generatedAt, cancellationToken);
            LogGenerated(logger, claimed.Id, insight.Id, claimed.ProjectId);
        }
        catch (AdvisoryModelException exception)
        {
            await RecordFailureAsync(claimed, exception.Code, exception.IsTransient, cancellationToken);
            LogRequestFailed(logger, claimed.Id, exception.Code);
        }
        catch (AdvisoryContextException exception)
        {
            await RecordFailureAsync(claimed, exception.Code, transient: false, cancellationToken);
            LogRequestFailed(logger, claimed.Id, exception.Code);
        }
        catch (Pmcs.BuildingBlocks.Domain.DomainRuleException exception)
        {
            await RecordFailureAsync(claimed, exception.Code, transient: false, cancellationToken);
            LogRequestFailed(logger, claimed.Id, exception.Code);
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(claimed, "ai.processing.failed", transient: true, cancellationToken);
            LogUnexpectedRequestFailure(logger, exception, claimed.Id);
        }

        return true;
    }

    private static async Task<ClaimedRequest?> ClaimNextAsync(
        IntelligenceDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var select = new NpgsqlCommand(
            """
            select id, tenant_id, project_id, requested_by, attempts
            from intelligence.generation_requests
            where attempts < @maximum_attempts
              and (
                (status = 'Pending' and (next_attempt_at is null or next_attempt_at <= @now))
                or (status = 'Processing' and started_at < @lease_cutoff)
              )
            order by requested_at, id
            for update skip locked
            limit 1;
            """,
            connection,
            postgresTransaction);
        select.Parameters.AddWithValue("maximum_attempts", MaximumAttempts);
        select.Parameters.AddWithValue("now", now);
        select.Parameters.AddWithValue("lease_cutoff", now.Subtract(ProcessingLease));
        ClaimedRequest? claimed = null;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                claimed = new ClaimedRequest(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetGuid(3),
                    reader.GetInt32(4) + 1);
            }
        }

        if (claimed is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await using var update = new NpgsqlCommand(
            """
            update intelligence.generation_requests
            set status = 'Processing', started_at = @now, completed_at = null,
                next_attempt_at = null, attempts = attempts + 1, last_error_code = null,
                revision = revision + 1
            where id = @id;
            """,
            connection,
            postgresTransaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("id", claimed.Id);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private static async Task PersistSuccessAsync(
        IServiceProvider services,
        ClaimedRequest claimed,
        AdvisoryInsight insight,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<IntelligenceDbContext>();
        var request = await dbContext.GenerationRequests.SingleAsync(
            item => item.Id == claimed.Id && item.TenantId == claimed.TenantId,
            cancellationToken);
        request.Complete(insight.SnapshotId, insight.Id, completedAt);
        dbContext.AdvisoryInsights.Add(insight);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await InsertAuditAsync(connection, postgresTransaction, claimed, insight, completedAt, cancellationToken);
        await InsertOutboxAsync(connection, postgresTransaction, claimed, insight, completedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(
        ClaimedRequest claimed,
        string errorCode,
        bool transient,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var retry = transient && claimed.Attempts < MaximumAttempts;
        await using var command = new NpgsqlCommand(
            """
            update intelligence.generation_requests
            set status = @status,
                completed_at = @completed_at,
                next_attempt_at = @next_attempt_at,
                last_error_code = @error_code,
                revision = revision + 1
            where id = @id and status = 'Processing';
            """,
            connection);
        command.Parameters.AddWithValue("status", retry ? "Pending" : "Failed");
        command.Parameters.Add(new NpgsqlParameter("completed_at", NpgsqlDbType.TimestampTz)
        {
            Value = retry ? DBNull.Value : DateTimeOffset.UtcNow
        });
        command.Parameters.Add(new NpgsqlParameter("next_attempt_at", NpgsqlDbType.TimestampTz)
        {
            Value = retry ? DateTimeOffset.UtcNow.AddSeconds(30 * claimed.Attempts) : DBNull.Value
        });
        command.Parameters.AddWithValue("error_code", SafeCode(errorCode));
        command.Parameters.AddWithValue("id", claimed.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ClaimedRequest claimed,
        AdvisoryInsight insight,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into foundation.audit_events(
                event_id, tenant_id, project_id, actor_user_id, event_type,
                resource_type, resource_id, occurred_at, data, correlation_id)
            values (
                @event_id, @tenant_id, @project_id, @actor_user_id, 'AdvisoryInsightGenerated',
                'AdvisoryInsight', @resource_id, @occurred_at, @data, null);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("event_id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant_id", claimed.TenantId);
        command.Parameters.AddWithValue("project_id", claimed.ProjectId);
        command.Parameters.AddWithValue("actor_user_id", claimed.RequestedBy);
        command.Parameters.AddWithValue("resource_id", insight.Id.ToString());
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        command.Parameters.AddWithValue("data", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new
        {
            requestId = claimed.Id,
            insight.SnapshotId,
            insight.ContextHash,
            insight.Provider,
            insight.Model,
            insight.PromptVersion,
            insight.PolicyVersion,
            insight.IncludesFinancialData,
            insight.IncludesCommercialData,
            insight.IncludesActionData
        }));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ClaimedRequest claimed,
        AdvisoryInsight insight,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into foundation.outbox_messages(
                message_id, tenant_id, project_id, event_type, event_version,
                occurred_at, payload, correlation_id, published_at, attempts, last_error)
            values (
                @message_id, @tenant_id, @project_id, 'Intelligence.AdvisoryInsightGenerated', 1,
                @occurred_at, @payload, null, null, 0, null);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("message_id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant_id", claimed.TenantId);
        command.Parameters.AddWithValue("project_id", claimed.ProjectId);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new
        {
            requestId = claimed.Id,
            insightId = insight.Id,
            insight.SnapshotId,
            insight.ReviewStatus,
            insight.ExpiresAt
        }));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SafeCode(string code)
    {
        var safe = new string(code.Trim().Where(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-').ToArray());
        if (safe.Length == 0)
        {
            return "ai.processing.failed";
        }
        return safe.Length <= 120 ? safe : safe[..120];
    }

    private sealed record ClaimedRequest(
        Guid Id,
        Guid TenantId,
        Guid ProjectId,
        Guid RequestedBy,
        int Attempts);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Advisory intelligence worker is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Error, Message = "Advisory intelligence worker loop failed.")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Information, Message = "Advisory request {RequestId} generated insight {InsightId} for project {ProjectId}.")]
    private static partial void LogGenerated(ILogger logger, Guid requestId, Guid insightId, Guid projectId);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Warning, Message = "Advisory request {RequestId} failed with safe code {ErrorCode}.")]
    private static partial void LogRequestFailed(ILogger logger, Guid requestId, string errorCode);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Error, Message = "Advisory request {RequestId} failed unexpectedly.")]
    private static partial void LogUnexpectedRequestFailure(ILogger logger, Exception exception, Guid requestId);
}
