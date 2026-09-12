using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ActionControl.Endpoints;

internal static partial class ActionControlEndpoints
{
    private static void MapEscalationEndpoints(RouteGroupBuilder governance)
    {
        governance.MapPost("/alerts/evaluate", EvaluateGovernanceAsync);
        governance.MapPost("/escalations/{escalationId:guid}/acknowledge", AcknowledgeEscalationAsync);
    }

    private static async Task<IResult> EvaluateGovernanceAsync(Guid projectId, HttpContext httpContext,
        ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projectDirectory,
        IProjectLeadershipDirectory leadership, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "governance.escalate", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var request = new { ProjectId = projectId };
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.alerts.evaluate", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        var rules = await db.SlaRules.AsNoTracking().Where(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId).ToListAsync(cancellationToken);
        var risks = await db.Risks.Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId).ToListAsync(cancellationToken);
        var issues = await db.Issues.Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId).ToListAsync(cancellationToken);
        var requests = await db.DecisionRequests.Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId).ToListAsync(cancellationToken);
        var alerts = BuildAlerts(clock.UtcNow, project, rules, risks, issues, requests);
        var managers = await leadership.ListProjectManagersAsync(actor.TenantId, [projectId], cancellationToken);
        var manager = managers.FirstOrDefault();
        var existing = await db.Escalations.Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId &&
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
            var rule = rules.FirstOrDefault(x => x.EntityType == alert.EntityType &&
                ((alert.EntityType == SlaEntityType.Issue && issues.FirstOrDefault(y => y.Id == alert.EntityId)?.SlaRuleVersionId == x.Id) ||
                 (alert.EntityType == SlaEntityType.Risk && risks.FirstOrDefault(y => y.Id == alert.EntityId)?.SlaRuleVersionId == x.Id) ||
                 (alert.EntityType == SlaEntityType.DecisionRequest && requests.FirstOrDefault(y => y.Id == alert.EntityId)?.SlaRuleVersionId == x.Id)));
            var fallback = ResolveFallbackRecipient(alert.EntityType, alert.EntityId, risks, issues, requests, manager);
            var recipientId = rule?.EscalationRecipientUserId ?? fallback.UserId;
            var recipientName = rule?.EscalationRecipientDisplayName ?? fallback.DisplayName;
            if (!recipientId.HasValue || string.IsNullOrWhiteSpace(recipientName)) continue;
            RaiseOrRefresh(db, existing, alert.EntityType, alert.EntityId, alert.EntityNumber, alert.Title,
                reason, 1, recipientId.Value, recipientName, alert.Confidentiality, actor.TenantId, projectId,
                clock.UtcNow, ref raised, ref refreshed);
        }

        foreach (var risk in risks.Where(x => x.Status is (RiskStatus.Active or RiskStatus.Monitoring) &&
                     (x.ResidualRating ?? x.InherentRating) == RiskRatingBand.Critical))
        {
            var rule = rules.Where(x => x.EntityType == SlaEntityType.Risk && x.EffectiveFrom <= clock.UtcNow)
                .OrderByDescending(x => x.Version).FirstOrDefault();
            var recipientId = rule?.EscalationRecipientUserId ?? manager?.UserId ?? risk.OwnerUserId;
            var recipientName = rule?.EscalationRecipientDisplayName ?? manager?.DisplayName ?? risk.OwnerDisplayName;
            if (string.IsNullOrWhiteSpace(recipientName)) continue;
            RaiseOrRefresh(db, existing, SlaEntityType.Risk, risk.Id, risk.Number, risk.UncertainEvent,
                EscalationReason.CriticalSeverity, 1, recipientId, recipientName,
                risk.Confidentiality, actor.TenantId, projectId, clock.UtcNow, ref raised, ref refreshed);
        }

        var changed = db.ChangeTracker.Entries<EscalationThread>().Select(x => x.Entity)
            .DistinctBy(x => x.Id).Select(EscalationResponse.From).ToArray();
        var response = new EvaluateGovernanceResponse(raised, refreshed, 0, changed);
        await PersistAsync(db, httpContext, actor, projectId, projectId, "GovernanceEvaluation",
            "GovernanceAlertsEvaluated", new Dictionary<string, object?> { ["raised"] = raised,
                ["refreshed"] = refreshed, ["candidateCount"] = alerts.Length }, response, replay,
            StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    internal static void RaiseOrRefresh(ActionControlDbContext db, List<EscalationThread> existing,
        SlaEntityType entityType, Guid entityId, string number, string title, EscalationReason reason,
        int level, Guid recipientId, string recipientName, RecordConfidentiality confidentiality,
        Guid tenantId, Guid projectId, DateTimeOffset at, ref int raised, ref int refreshed)
    {
        var key = EscalationThread.Key(entityType, entityId, reason, level);
        var thread = existing.SingleOrDefault(x => x.ThreadKey == key);
        if (thread is null)
        {
            thread = EscalationThread.Raise(Guid.NewGuid(), tenantId, projectId, entityType,
                entityId, number, title, reason, level, recipientId, recipientName, confidentiality, at);
            db.Escalations.Add(thread);
            existing.Add(thread);
            raised++;
        }
        else if (thread.LastRaisedAt <= at.AddHours(-1))
        {
            thread.Touch(at);
            refreshed++;
        }
    }

    internal static (Guid? UserId, string? DisplayName) ResolveFallbackRecipient(
        SlaEntityType entityType, Guid entityId, IReadOnlyCollection<ProjectRisk> risks,
        IReadOnlyCollection<ManagementIssue> issues, IReadOnlyCollection<DecisionRequest> requests,
        ProjectLeaderRecord? manager)
    {
        if (manager is not null) return (manager.UserId, manager.DisplayName);
        return entityType switch
        {
            SlaEntityType.Risk => risks.Where(x => x.Id == entityId)
                .Select(x => ((Guid?)x.OwnerUserId, (string?)x.OwnerDisplayName)).FirstOrDefault(),
            SlaEntityType.Issue => issues.Where(x => x.Id == entityId)
                .Select(x => ((Guid?)x.OwnerUserId, (string?)x.OwnerDisplayName)).FirstOrDefault(),
            SlaEntityType.DecisionRequest => requests.Where(x => x.Id == entityId)
                .Select(x => ((Guid?)x.AuthorityUserId, (string?)x.AuthorityDisplayName)).FirstOrDefault(),
            _ => (null, null)
        };
    }

    private static async Task<IResult> AcknowledgeEscalationAsync(Guid projectId, Guid escalationId,
        AcknowledgeEscalationRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "escalations.acknowledge", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.escalation.acknowledge", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var item = await db.Escalations.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == escalationId, cancellationToken);
        if (item is null || !await CanSeeAsync(item.Confidentiality, permissions, actor, projectId, cancellationToken))
            return Results.NotFound();
        if (item.RecipientUserId != actor.UserId && !await HasPermissionAsync(permissions, actor,
                projectId, "escalations.manage", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        item.Acknowledge(request.BaseRevision, actor.UserId, request.Note, clock.UtcNow);
        var response = EscalationResponse.From(item);
        await PersistAsync(db, httpContext, actor, projectId, item.Id, "EscalationThread", "EscalationAcknowledged",
            new Dictionary<string, object?> { ["entityType"] = item.EntityType.ToString(),
                ["entityId"] = item.EntityId, ["reason"] = item.Reason.ToString(),
                ["acknowledgedBy"] = actor.UserId, ["revision"] = item.Revision }, response, replay,
            StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }
}
