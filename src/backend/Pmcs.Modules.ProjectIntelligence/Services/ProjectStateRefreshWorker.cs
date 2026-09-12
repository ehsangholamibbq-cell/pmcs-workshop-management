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
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.ProjectIntelligence.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Services;

internal sealed partial class ProjectStateRefreshWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ProjectStateRefreshWorker> logger) : BackgroundService
{
    internal const string SourceEventType = "FieldOperations.DailyReportApproved";
    private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bool.TryParse(configuration["ProjectStateRefresh:Enabled"], out var enabled) && !enabled)
        {
            LogDisabled(logger);
            return;
        }

        var interval = int.TryParse(configuration["ProjectStateRefresh:PollSeconds"], out var seconds) && seconds >= 1
            ? TimeSpan.FromSeconds(seconds)
            : DefaultPollingInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(stoppingToken))
                {
                    // Drain available approval events before waiting for the next polling interval.
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogRefreshFailed(logger, exception);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProjectIntelligenceDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var message = await ClaimNextAsync(dbContext, transaction, cancellationToken);
        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        try
        {
            var projectDirectory = scope.ServiceProvider.GetRequiredService<IProjectDirectory>();
            var approvedFactSource = scope.ServiceProvider.GetRequiredService<IApprovedDailyFactSource>();
            var project = await projectDirectory.FindProfileAsync(
                message.TenantId, message.ProjectId, cancellationToken)
                ?? throw new InvalidOperationException($"Project {message.ProjectId} from approval event was not found.");
            var asOfDate = ResolveLocalDate(clock.UtcNow, project.TimeZone);
            var source = await approvedFactSource.LoadAsync(
                message.TenantId,
                message.ProjectId,
                asOfDate.AddDays(-(ProjectStateCalculator.AttentionWindowDays - 1)),
                asOfDate,
                cancellationToken);
            var calculation = ProjectStateCalculator.Calculate(project, source, asOfDate, clock.UtcNow);
            var snapshot = ProjectStateSnapshot.Create(Guid.NewGuid(), calculation);
            dbContext.ProjectStateSnapshots.Add(snapshot);
            await dbContext.SaveChangesAsync(cancellationToken);
            await InsertAuditAsync(dbContext, transaction, message, snapshot, clock.UtcNow, cancellationToken);
            await MarkPublishedAsync(dbContext, transaction, message.MessageId, clock.UtcNow, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            LogRefreshed(logger, message.ProjectId, snapshot.Id, message.MessageId);
            return true;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            await RecordFailureAsync(message.MessageId, exception.Message, cancellationToken);
            LogEventFailed(logger, exception, message.MessageId);
            return false;
        }
    }

    private static async Task<ApprovalOutboxMessage?> ClaimNextAsync(
        ProjectIntelligenceDbContext dbContext,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var command = new NpgsqlCommand(
            """
            select message_id, tenant_id, project_id, payload
            from foundation.outbox_messages
            where published_at is null
              and event_type = @event_type
              and project_id is not null
              and attempts < 10
            order by occurred_at, message_id
            for update skip locked
            limit 1;
            """,
            connection,
            postgresTransaction);
        command.Parameters.AddWithValue("event_type", SourceEventType);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var payload = reader.GetString(3);
        using var document = JsonDocument.Parse(payload);
        return new ApprovalOutboxMessage(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            document.RootElement.Clone());
    }

    private static async Task MarkPublishedAsync(
        ProjectIntelligenceDbContext dbContext,
        IDbContextTransaction transaction,
        Guid messageId,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var command = new NpgsqlCommand(
            """
            update foundation.outbox_messages
            set published_at = @published_at, attempts = attempts + 1, last_error = null
            where message_id = @message_id;
            """,
            connection,
            postgresTransaction);
        command.Parameters.AddWithValue("published_at", publishedAt);
        command.Parameters.AddWithValue("message_id", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        ProjectIntelligenceDbContext dbContext,
        IDbContextTransaction transaction,
        ApprovalOutboxMessage message,
        ProjectStateSnapshot snapshot,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var command = new NpgsqlCommand(
            """
            insert into foundation.audit_events(
                event_id, tenant_id, project_id, actor_user_id, event_type,
                resource_type, resource_id, occurred_at, data, correlation_id)
            values (
                @event_id, @tenant_id, @project_id, @actor_user_id, @event_type,
                @resource_type, @resource_id, @occurred_at, @data, null);
            """,
            connection,
            postgresTransaction);
        command.Parameters.AddWithValue("event_id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant_id", message.TenantId);
        command.Parameters.AddWithValue("project_id", message.ProjectId);
        command.Parameters.AddWithValue("actor_user_id", ResolveTriggerActor(message.Payload));
        command.Parameters.AddWithValue("event_type", "ProjectStateAutoCalculated");
        command.Parameters.AddWithValue("resource_type", "ProjectStateSnapshot");
        command.Parameters.AddWithValue("resource_id", snapshot.Id.ToString());
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        command.Parameters.AddWithValue("data", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new
        {
            sourceMessageId = message.MessageId,
            snapshot.CalculationVersion,
            snapshot.AsOfDate,
            snapshot.OperationalStatus,
            snapshot.SourceMaxChangedAt
        }));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            update foundation.outbox_messages
            set attempts = attempts + 1, last_error = @last_error
            where message_id = @message_id;
            """,
            connection);
        command.Parameters.AddWithValue("last_error", error.Length <= 2_000 ? error : error[..2_000]);
        command.Parameters.AddWithValue("message_id", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateOnly ResolveLocalDate(DateTimeOffset now, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
    }

    private static Guid ResolveTriggerActor(JsonElement payload)
    {
        if (payload.TryGetProperty("reviewedBy", out var reviewedBy) &&
            reviewedBy.ValueKind == JsonValueKind.String &&
            Guid.TryParse(reviewedBy.GetString(), out var actorId))
        {
            return actorId;
        }

        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    private sealed record ApprovalOutboxMessage(
        Guid MessageId,
        Guid TenantId,
        Guid ProjectId,
        JsonElement Payload);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Automatic Project State refresh is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Error, Message = "Automatic Project State refresh loop failed.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Information,
        Message = "Project {ProjectId} refreshed into snapshot {SnapshotId} from approval event {MessageId}.")]
    private static partial void LogRefreshed(ILogger logger, Guid projectId, Guid snapshotId, Guid messageId);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Error, Message = "Approval event {MessageId} could not refresh Project State.")]
    private static partial void LogEventFailed(ILogger logger, Exception exception, Guid messageId);
}
