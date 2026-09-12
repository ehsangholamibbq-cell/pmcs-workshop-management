using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ActionControl.Endpoints;

internal static partial class ActionControlEndpoints
{
    private static void MapGovernanceEndpoints(IEndpointRouteBuilder endpoints)
    {
        var governance = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/governance").WithTags("Project Governance");
        governance.MapGet("/", GetGovernanceStateAsync);
        governance.MapPost("/settings/risk-matrices", CreateRiskMatrixAsync);
        governance.MapPost("/settings/sla-rules", CreateSlaRuleAsync);
        MapIssueRiskEndpoints(governance);
        MapDecisionEndpoints(governance);
        MapEscalationEndpoints(governance);
    }

    private static async Task<IResult> GetGovernanceStateAsync(
        Guid projectId, ICurrentActor actor, IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory, IProjectAssigneeDirectory assigneeDirectory,
        ActionControlDbContext dbContext, IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissionService, actor, projectId, "governance.read", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });

        var assignable = await assigneeDirectory.ListAssignableAsync(actor.TenantId, projectId, cancellationToken);
        var people = assignable.Select(person =>
            new GovernancePersonResponse(person.UserId, person.DisplayName, person.CanDecide)).ToArray();

        var includeSensitive = await HasPermissionAsync(permissionService, actor, projectId,
            "governance.sensitive.read", cancellationToken);
        var matrices = await dbContext.RiskMatrices.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.Version).Take(20).ToListAsync(cancellationToken);
        var rules = await dbContext.SlaRules.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.Version).Take(100).ToListAsync(cancellationToken);
        var risksQuery = dbContext.Risks.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId);
        var issuesQuery = dbContext.Issues.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId);
        var requestsQuery = dbContext.DecisionRequests.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId);
        var escalationsQuery = dbContext.Escalations.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId);
        if (!includeSensitive)
        {
            risksQuery = risksQuery.Where(x => x.Confidentiality == RecordConfidentiality.GeneralProject);
            issuesQuery = issuesQuery.Where(x => x.Confidentiality == RecordConfidentiality.GeneralProject);
            requestsQuery = requestsQuery.Where(x => x.Confidentiality == RecordConfidentiality.GeneralProject);
            escalationsQuery = escalationsQuery.Where(x => x.Confidentiality == RecordConfidentiality.GeneralProject);
        }
        var risks = await risksQuery.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var issues = await issuesQuery.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var requests = await requestsQuery.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var requestIds = requests.Select(x => x.Id).ToArray();
        var decisions = await dbContext.Decisions.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && requestIds.Contains(x.DecisionRequestId))
            .OrderByDescending(x => x.RecordedAt).Take(200).ToListAsync(cancellationToken);
        var escalations = await escalationsQuery.OrderByDescending(x => x.LastRaisedAt).Take(200).ToListAsync(cancellationToken);
        var alerts = BuildAlerts(clock.UtcNow, project, rules, risks, issues, requests);
        var terminalIssues = new[] { IssueStatus.Closed, IssueStatus.NotAnIssue, IssueStatus.Void };
        var terminalRisks = new[] { RiskStatus.Materialized, RiskStatus.Expired, RiskStatus.Closed };
        var terminalRequests = new[] { DecisionRequestStatus.Decided, DecisionRequestStatus.Implementing,
            DecisionRequestStatus.EffectReviewed, DecisionRequestStatus.Closed, DecisionRequestStatus.Withdrawn };
        var counts = new GovernanceCountsResponse(
            issues.Count(x => !terminalIssues.Contains(x.Status)),
            risks.Count(x => !terminalRisks.Contains(x.Status)),
            risks.Count(x => !terminalRisks.Contains(x.Status) && (x.ResidualRating ?? x.InherentRating) == RiskRatingBand.Critical),
            requests.Count(x => !terminalRequests.Contains(x.Status)),
            escalations.Count(x => x.Status == EscalationStatus.Open));
        var outlook = new GovernanceOutlookResponse(alerts.Length == 0 ? "InsufficientData" : "Available",
            alerts.Length, alerts.Count(x => x.DueAt >= clock.UtcNow && x.DueAt <= clock.UtcNow.AddDays(7)),
            alerts.Count(x => x.DueAt < clock.UtcNow), clock.UtcNow);
        var activeRules = rules.Where(x => x.EffectiveFrom <= clock.UtcNow).ToArray();
        var requiredRuleTypes = new[] { SlaEntityType.Issue, SlaEntityType.Risk, SlaEntityType.DecisionRequest };
        var setupState = !matrices.Any(x => x.EffectiveFrom <= clock.UtcNow) ||
            requiredRuleTypes.Any(type => activeRules.All(x => x.EntityType != type))
            ? "SetupRequired" : "Configured";
        return Results.Ok(new GovernanceStateResponse(setupState, includeSensitive,
            people,
            matrices.Select(x => new RiskMatrixResponse(x.Id, x.Version, x.Title, x.FormulaVersion,
                x.LowMaximum, x.ModerateMaximum, x.HighMaximum, x.EffectiveFrom)).ToArray(),
            rules.Select(ToResponse).ToArray(), risks.Select(RiskResponse.From).ToArray(),
            issues.Select(IssueResponse.From).ToArray(), requests.Select(DecisionRequestResponse.From).ToArray(),
            decisions.Select(DecisionResponse.From).ToArray(), escalations.Select(EscalationResponse.From).ToArray(),
            alerts, counts, outlook));
    }

    private static async Task<IResult> CreateRiskMatrixAsync(
        Guid projectId, CreateRiskMatrixRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissionService, IProjectDirectory projectDirectory,
        ActionControlDbContext dbContext, IClock clock, ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissionService, actor, projectId, "governance.configure", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotencyStore, "governance.risk-matrix.create", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        if (!await projectDirectory.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var version = (await dbContext.RiskMatrices.Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;
        var item = GovernanceRiskMatrixVersion.Create(request.ClientGeneratedId, actor.TenantId, projectId,
            version, request.Title, request.LowMaximum, request.ModerateMaximum, request.HighMaximum,
            request.EffectiveFrom, actor.UserId, clock.UtcNow);
        dbContext.RiskMatrices.Add(item);
        var response = new RiskMatrixResponse(item.Id, item.Version, item.Title, item.FormulaVersion,
            item.LowMaximum, item.ModerateMaximum, item.HighMaximum, item.EffectiveFrom);
        await PersistAsync(dbContext, httpContext, actor, projectId, item.Id, "RiskMatrixVersion",
            "RiskMatrixVersionCreated", new Dictionary<string, object?> { ["version"] = item.Version,
                ["formulaVersion"] = item.FormulaVersion }, response, replay, StatusCodes.Status201Created,
            sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/settings/risk-matrices/{item.Id}", response);
    }

    private static async Task<IResult> CreateSlaRuleAsync(
        Guid projectId, CreateSlaRuleRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissionService, IProjectDirectory projectDirectory,
        IProjectAssigneeDirectory assigneeDirectory, ActionControlDbContext dbContext, IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter, IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissionService, actor, projectId, "governance.configure", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotencyStore, "governance.sla-rule.create", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        if (!await projectDirectory.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var recipient = await assigneeDirectory.FindAssignableAsync(actor.TenantId, projectId,
            request.EscalationRecipientUserId, cancellationToken);
        if (recipient is null) return Results.UnprocessableEntity(new { code = "governance.sla.recipient.not_assignable" });
        var query = dbContext.SlaRules.Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId &&
            x.EntityType == request.EntityType && x.Severity == request.Severity);
        var version = (await query.MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0) + 1;
        var item = SlaRuleVersion.Create(request.ClientGeneratedId, actor.TenantId, projectId, version,
            request.Title, request.EntityType, request.Severity, request.Duration, request.DurationUnit,
            request.WarningLeadMinutes, request.EscalationDelayMinutes, recipient.UserId, recipient.DisplayName,
            request.EffectiveFrom, actor.UserId, clock.UtcNow);
        dbContext.SlaRules.Add(item);
        var response = ToResponse(item);
        await PersistAsync(dbContext, httpContext, actor, projectId, item.Id, "SlaRuleVersion",
            "SlaRuleVersionCreated", new Dictionary<string, object?> { ["version"] = item.Version,
                ["entityType"] = item.EntityType.ToString(), ["durationUnit"] = item.DurationUnit.ToString() },
            response, replay, StatusCodes.Status201Created, sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/settings/sla-rules/{item.Id}", response);
    }

    private static SlaRuleResponse ToResponse(SlaRuleVersion x) => new(x.Id, x.Version, x.Title,
        x.EntityType, x.Severity, x.Duration, x.DurationUnit, x.WarningLeadMinutes,
        x.EscalationDelayMinutes, x.EscalationRecipientUserId, x.EscalationRecipientDisplayName, x.EffectiveFrom);

    private static async Task<(DateTimeOffset? DueAt, Guid? RuleId)> ResolveSlaAsync(
        ActionControlDbContext dbContext, Guid tenantId, Guid projectId, SlaEntityType entityType,
        GovernanceSeverity? severity, DateTimeOffset createdAt, ProjectControlProfile project,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.SlaRules.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.EntityType == entityType &&
                x.EffectiveFrom <= createdAt && (x.Severity == severity || x.Severity == null))
            .OrderByDescending(x => x.Severity == severity).ThenByDescending(x => x.Version)
            .Take(2).ToListAsync(cancellationToken);
        var rule = candidates.FirstOrDefault();
        var deadline = rule is null ? null : GovernanceDeadlineCalculator.Calculate(createdAt, rule, project);
        return (deadline?.DueAt, deadline?.RuleVersionId);
    }

    private static bool IsSensitive(RecordConfidentiality confidentiality) =>
        confidentiality != RecordConfidentiality.GeneralProject;

    private static async Task<bool> CanSeeAsync(RecordConfidentiality confidentiality, IProjectPermissionService permissions,
        ICurrentActor actor, Guid projectId, CancellationToken cancellationToken) =>
        !IsSensitive(confidentiality) || await HasPermissionAsync(permissions, actor, projectId,
            "governance.sensitive.read", cancellationToken);

    internal static GovernanceDeadlineAlertResponse[] BuildAlerts(DateTimeOffset now,
        ProjectControlProfile project, IReadOnlyCollection<SlaRuleVersion> rules, IReadOnlyCollection<ProjectRisk> risks,
        IReadOnlyCollection<ManagementIssue> issues, IReadOnlyCollection<DecisionRequest> requests)
    {
        var alerts = new List<GovernanceDeadlineAlertResponse>();
        foreach (var item in issues.Where(x => x.Status is not (IssueStatus.Closed or IssueStatus.NotAnIssue or IssueStatus.Void)))
        {
            AddDateAlert(alerts, now, project, SlaEntityType.Issue, item.Id, item.Number, item.Title,
                "TargetResolution", item.TargetResolutionDate, item.Confidentiality);
            AddSlaAlert(alerts, now, rules, SlaEntityType.Issue, item.Id, item.Number, item.Title,
                item.SlaDueAt, item.SlaRuleVersionId, item.Confidentiality);
        }
        foreach (var item in risks.Where(x => x.Status is not (RiskStatus.Closed or RiskStatus.Expired or RiskStatus.Materialized)))
        {
            if (item.ReviewDate.HasValue)
                AddDateAlert(alerts, now, project, SlaEntityType.Risk, item.Id, item.Number, item.UncertainEvent,
                    "RiskReview", item.ReviewDate.Value, item.Confidentiality);
            AddSlaAlert(alerts, now, rules, SlaEntityType.Risk, item.Id, item.Number, item.UncertainEvent,
                item.SlaDueAt, item.SlaRuleVersionId, item.Confidentiality);
        }
        foreach (var item in requests.Where(x => x.Status is not (DecisionRequestStatus.Decided or DecisionRequestStatus.Implementing or
                     DecisionRequestStatus.EffectReviewed or DecisionRequestStatus.Closed or DecisionRequestStatus.Withdrawn)))
        {
            AddDateAlert(alerts, now, project, SlaEntityType.DecisionRequest, item.Id, item.Number, item.Question,
                "RequiredDecision", item.RequiredBy, item.Confidentiality);
            AddSlaAlert(alerts, now, rules, SlaEntityType.DecisionRequest, item.Id, item.Number, item.Question,
                item.SlaDueAt, item.SlaRuleVersionId, item.Confidentiality);
        }
        return alerts.OrderBy(x => x.DueAt).ToArray();
    }

    private static void AddSlaAlert(List<GovernanceDeadlineAlertResponse> alerts, DateTimeOffset now,
        IReadOnlyCollection<SlaRuleVersion> rules, SlaEntityType type, Guid id, string number, string title,
        DateTimeOffset? dueAt, Guid? ruleId, RecordConfidentiality confidentiality)
    {
        if (!dueAt.HasValue || !ruleId.HasValue) return;
        var rule = rules.SingleOrDefault(x => x.Id == ruleId.Value);
        if (rule is null) return;
        alerts.Add(new GovernanceDeadlineAlertResponse(type, id, number, title, "ServiceLevel", dueAt.Value,
            GovernanceDeadlineCalculator.Signal(now, dueAt.Value, rule.WarningLeadMinutes, rule.EscalationDelayMinutes), confidentiality));
    }

    private static void AddDateAlert(List<GovernanceDeadlineAlertResponse> alerts, DateTimeOffset now,
        ProjectControlProfile project, SlaEntityType type, Guid id, string number, string title,
        string kind, DateOnly date, RecordConfidentiality confidentiality)
    {
        var dueAt = ResolveProjectDateEnd(date, project.TimeZone);
        var signal = now >= dueAt ? DeadlineSignal.Overdue : now >= dueAt.AddDays(-2) ? DeadlineSignal.DueSoon : DeadlineSignal.OnTrack;
        alerts.Add(new GovernanceDeadlineAlertResponse(type, id, number, title, kind, dueAt, signal, confidentiality));
    }

    private static DateTimeOffset ResolveProjectDateEnd(DateOnly date, string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var local = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Unspecified);
            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
        }
        catch (TimeZoneNotFoundException exception) { throw new DomainRuleException("project.time_zone.unavailable", exception.Message); }
        catch (InvalidTimeZoneException exception) { throw new DomainRuleException("project.time_zone.invalid", exception.Message); }
    }
}
