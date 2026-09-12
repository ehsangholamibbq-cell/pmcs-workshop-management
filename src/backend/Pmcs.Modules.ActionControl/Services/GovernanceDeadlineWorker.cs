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
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Endpoints;
using Pmcs.Modules.ActionControl.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ActionControl.Services;

internal sealed partial class GovernanceDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<GovernanceDeadlineWorker> logger) : BackgroundService
{
    internal static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bool.TryParse(configuration["GovernanceAlerts:Enabled"], out var enabled) && !enabled)
        {
            LogDisabled(logger);
            return;
        }
        var interval = int.TryParse(configuration["GovernanceAlerts:PollSeconds"], out var seconds) && seconds >= 10
            ? TimeSpan.FromSeconds(seconds) : DefaultInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await EvaluateAllAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogCycleFailed(logger, exception); }
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task EvaluateAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<GovernanceScope> scopes;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ActionControlDbContext>();
            var issues = await db.Issues.AsNoTracking()
                .Where(x => x.Status != IssueStatus.Closed && x.Status != IssueStatus.NotAnIssue && x.Status != IssueStatus.Void)
                .Select(x => new GovernanceScope(x.TenantId, x.ProjectId)).Distinct().ToListAsync(cancellationToken);
            var risks = await db.Risks.AsNoTracking()
                .Where(x => x.Status != RiskStatus.Closed && x.Status != RiskStatus.Expired && x.Status != RiskStatus.Materialized)
                .Select(x => new GovernanceScope(x.TenantId, x.ProjectId)).Distinct().ToListAsync(cancellationToken);
            var requests = await db.DecisionRequests.AsNoTracking()
                .Where(x => x.Status != DecisionRequestStatus.Decided && x.Status != DecisionRequestStatus.Implementing &&
                    x.Status != DecisionRequestStatus.EffectReviewed && x.Status != DecisionRequestStatus.Closed &&
                    x.Status != DecisionRequestStatus.Withdrawn)
                .Select(x => new GovernanceScope(x.TenantId, x.ProjectId)).Distinct().ToListAsync(cancellationToken);
            scopes = issues.Concat(risks).Concat(requests).Distinct().ToArray();
        }

        foreach (var item in scopes)
        {
            try { await EvaluateProjectAsync(item, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { LogProjectFailed(logger, exception, item.TenantId, item.ProjectId); }
        }
    }

    private async Task EvaluateProjectAsync(GovernanceScope scope, CancellationToken cancellationToken)
    {
        await using var serviceScope = scopeFactory.CreateAsyncScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<ActionControlDbContext>();
        var projects = serviceScope.ServiceProvider.GetRequiredService<IProjectDirectory>();
        var leadership = serviceScope.ServiceProvider.GetRequiredService<IProjectLeadershipDirectory>();
        var clock = serviceScope.ServiceProvider.GetRequiredService<IClock>();
        var project = await projects.FindProfileAsync(scope.TenantId, scope.ProjectId, cancellationToken);
        if (project is null) return;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var rules = await db.SlaRules.AsNoTracking().Where(x => x.TenantId == scope.TenantId &&
            x.ProjectId == scope.ProjectId).ToListAsync(cancellationToken);
        var risks = await db.Risks.Where(x => x.TenantId == scope.TenantId && x.ProjectId == scope.ProjectId)
            .ToListAsync(cancellationToken);
        var issues = await db.Issues.Where(x => x.TenantId == scope.TenantId && x.ProjectId == scope.ProjectId)
            .ToListAsync(cancellationToken);
        var requests = await db.DecisionRequests.Where(x => x.TenantId == scope.TenantId && x.ProjectId == scope.ProjectId)
            .ToListAsync(cancellationToken);
        var alerts = ActionControlEndpoints.BuildAlerts(clock.UtcNow, project, rules, risks, issues, requests);
        var managers = await leadership.ListProjectManagersAsync(scope.TenantId, [scope.ProjectId], cancellationToken);
        var manager = managers.FirstOrDefault();
        var existing = await db.Escalations.Where(x => x.TenantId == scope.TenantId && x.ProjectId == scope.ProjectId &&
            x.Status != EscalationStatus.ClosedBySourceResolution).ToListAsync(cancellationToken);
        var raised = 0;
        var refreshed = 0;

        foreach (var alert in alerts.Where(x => x.Signal is DeadlineSignal.Overdue or DeadlineSignal.EscalationDue))
        {
            var reason = alert.DeadlineKind switch
            {
                "RiskReview" => EscalationReason.ReviewOverdue,
                "RequiredDecision" => EscalationReason.DecisionOverdue,
                _ => EscalationReason.Overdue
            };
            var rule = FindPinnedRule(alert, rules, risks, issues, requests);
            var fallback = ActionControlEndpoints.ResolveFallbackRecipient(
                alert.EntityType, alert.EntityId, risks, issues, requests, manager);
            var recipientId = rule?.EscalationRecipientUserId ?? fallback.UserId;
            var recipientName = rule?.EscalationRecipientDisplayName ?? fallback.DisplayName;
            if (!recipientId.HasValue || string.IsNullOrWhiteSpace(recipientName)) continue;
            ActionControlEndpoints.RaiseOrRefresh(db, existing, alert.EntityType, alert.EntityId,
                alert.EntityNumber, alert.Title, reason, 1, recipientId.Value, recipientName,
                alert.Confidentiality, scope.TenantId, scope.ProjectId, clock.UtcNow, ref raised, ref refreshed);
        }

        foreach (var risk in risks.Where(x => x.Status is (RiskStatus.Active or RiskStatus.Monitoring) &&
                     (x.ResidualRating ?? x.InherentRating) == RiskRatingBand.Critical))
        {
            var rule = rules.Where(x => x.EntityType == SlaEntityType.Risk && x.EffectiveFrom <= clock.UtcNow)
                .OrderByDescending(x => x.Version).FirstOrDefault();
            var recipientId = rule?.EscalationRecipientUserId ?? manager?.UserId ?? risk.OwnerUserId;
            var recipientName = rule?.EscalationRecipientDisplayName ?? manager?.DisplayName ?? risk.OwnerDisplayName;
            if (string.IsNullOrWhiteSpace(recipientName)) continue;
            ActionControlEndpoints.RaiseOrRefresh(db, existing, SlaEntityType.Risk, risk.Id, risk.Number,
                risk.UncertainEvent, EscalationReason.CriticalSeverity, 1, recipientId, recipientName,
                risk.Confidentiality, scope.TenantId, scope.ProjectId, clock.UtcNow, ref raised, ref refreshed);
        }

        if (raised + refreshed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        await db.SaveChangesAsync(cancellationToken);
        await InsertSideEffectsAsync(db, transaction, scope, raised, refreshed, clock.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogProjectEvaluated(logger, scope.TenantId, scope.ProjectId, raised, refreshed);
    }

    private static SlaRuleVersion? FindPinnedRule(GovernanceDeadlineAlertResponse alert,
        IReadOnlyCollection<SlaRuleVersion> rules, IReadOnlyCollection<ProjectRisk> risks,
        IReadOnlyCollection<ManagementIssue> issues, IReadOnlyCollection<DecisionRequest> requests)
    {
        var ruleId = alert.EntityType switch
        {
            SlaEntityType.Issue => issues.FirstOrDefault(x => x.Id == alert.EntityId)?.SlaRuleVersionId,
            SlaEntityType.Risk => risks.FirstOrDefault(x => x.Id == alert.EntityId)?.SlaRuleVersionId,
            SlaEntityType.DecisionRequest => requests.FirstOrDefault(x => x.Id == alert.EntityId)?.SlaRuleVersionId,
            _ => null
        };
        return ruleId.HasValue ? rules.FirstOrDefault(x => x.Id == ruleId.Value) : null;
    }

    private static async Task InsertSideEffectsAsync(ActionControlDbContext db, IDbContextTransaction transaction,
        GovernanceScope scope, int raised, int refreshed, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        var payload = JsonSerializer.Serialize(new { scope.ProjectId, Raised = raised, Refreshed = refreshed, EvaluatedAt = at });
        await using var audit = new NpgsqlCommand(
            """
            insert into foundation.audit_events(event_id, tenant_id, project_id, actor_user_id, event_type,
                resource_type, resource_id, occurred_at, data, correlation_id)
            values (@event_id, @tenant_id, @project_id, @actor_user_id, 'GovernanceAlertsAutoEvaluated',
                'GovernanceEvaluation', @resource_id, @occurred_at, @data, null);
            """, connection, postgresTransaction);
        audit.Parameters.AddWithValue("event_id", Guid.NewGuid());
        audit.Parameters.AddWithValue("tenant_id", scope.TenantId);
        audit.Parameters.AddWithValue("project_id", scope.ProjectId);
        audit.Parameters.AddWithValue("actor_user_id", SystemActorId);
        audit.Parameters.AddWithValue("resource_id", scope.ProjectId.ToString());
        audit.Parameters.AddWithValue("occurred_at", at);
        audit.Parameters.AddWithValue("data", NpgsqlDbType.Jsonb, payload);
        await audit.ExecuteNonQueryAsync(cancellationToken);

        await using var outbox = new NpgsqlCommand(
            """
            insert into foundation.outbox_messages(message_id, tenant_id, project_id, event_type,
                event_version, occurred_at, payload, correlation_id, published_at, attempts, last_error)
            values (@message_id, @tenant_id, @project_id, 'ActionControl.GovernanceAlertsAutoEvaluated',
                1, @occurred_at, @payload, null, null, 0, null);
            """, connection, postgresTransaction);
        outbox.Parameters.AddWithValue("message_id", Guid.NewGuid());
        outbox.Parameters.AddWithValue("tenant_id", scope.TenantId);
        outbox.Parameters.AddWithValue("project_id", scope.ProjectId);
        outbox.Parameters.AddWithValue("occurred_at", at);
        outbox.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, payload);
        await outbox.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record GovernanceScope(Guid TenantId, Guid ProjectId);

    [LoggerMessage(EventId = 7101, Level = LogLevel.Information, Message = "Automatic governance deadline evaluation is disabled.")]
    private static partial void LogDisabled(ILogger logger);
    [LoggerMessage(EventId = 7102, Level = LogLevel.Error, Message = "Governance deadline evaluation cycle failed.")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);
    [LoggerMessage(EventId = 7103, Level = LogLevel.Error, Message = "Governance deadline evaluation failed for tenant {TenantId}, project {ProjectId}.")]
    private static partial void LogProjectFailed(ILogger logger, Exception exception, Guid tenantId, Guid projectId);
    [LoggerMessage(EventId = 7104, Level = LogLevel.Information, Message = "Governance deadlines evaluated for tenant {TenantId}, project {ProjectId}; raised {Raised}, refreshed {Refreshed}.")]
    private static partial void LogProjectEvaluated(ILogger logger, Guid tenantId, Guid projectId, int raised, int refreshed);
}
