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
    private static void MapIssueRiskEndpoints(RouteGroupBuilder governance)
    {
        governance.MapPost("/issues", CreateIssueAsync);
        governance.MapPost("/issues/{issueId:guid}/transition", TransitionIssueAsync);
        governance.MapPost("/risks", ProposeRiskAsync);
        governance.MapPost("/risks/{riskId:guid}/assess", AssessRiskAsync);
        governance.MapPost("/risks/{riskId:guid}/activate", ActivateRiskAsync);
        governance.MapPost("/risks/{riskId:guid}/review", ReviewRiskAsync);
        governance.MapPost("/risks/{riskId:guid}/materialize", MaterializeRiskAsync);
        governance.MapPost("/risks/{riskId:guid}/close", CloseRiskAsync);
        governance.MapPost("/risks/{riskId:guid}/reopen", ReopenRiskAsync);
    }

    private static async Task<IResult> CreateIssueAsync(Guid projectId, CreateIssueRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        IProjectDirectory projectDirectory, IProjectAssigneeDirectory assignees, ActionControlDbContext db,
        IClock clock, ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "issues.create", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (IsSensitive(request.Confidentiality) && !await HasPermissionAsync(permissions, actor, projectId,
                "governance.sensitive.write", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.issue.create", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        if (request.TargetResolutionDate < ResolveLocalDate(clock.UtcNow, project.TimeZone))
            throw new DomainRuleException("governance.issue.target.in_past", "Target resolution date cannot be in the past.");
        var owner = await assignees.FindAssignableAsync(actor.TenantId, projectId, request.OwnerUserId, cancellationToken);
        if (owner is null) return Results.UnprocessableEntity(new { code = "governance.owner.not_assignable" });
        var sla = await ResolveSlaAsync(db, actor.TenantId, projectId, SlaEntityType.Issue,
            request.Severity, clock.UtcNow, project, cancellationToken);
        var item = ManagementIssue.Create(request.ClientGeneratedId, actor.TenantId, projectId,
            request.Title, request.ObservedFact, request.Category, request.Severity, request.Urgency,
            owner.UserId, owner.DisplayName, request.TargetResolutionDate, request.SourceModule,
            request.SourceEntityType, request.SourceEntityId, request.SourceRevision, request.SourceSnapshot,
            null, request.EvidenceReferences, request.Confidentiality, sla.DueAt, sla.RuleId,
            actor.UserId, clock.UtcNow);
        db.Issues.Add(item);
        var response = IssueResponse.From(item);
        await PersistAsync(db, httpContext, actor, projectId, item.Id, "ManagementIssue", "IssueCreated",
            IssueAudit(item), response, replay, StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/issues/{item.Id}", response);
    }

    private static async Task<IResult> TransitionIssueAsync(Guid projectId, Guid issueId,
        TransitionIssueRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "issues.manage", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.issue.transition", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var item = await db.Issues.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == issueId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (!await CanSeeAsync(item.Confidentiality, permissions, actor, projectId, cancellationToken))
            return Results.NotFound();
        if (request.TargetStatus == IssueStatus.Closed && !await HasPermissionAsync(permissions, actor,
                projectId, "issues.verify-close", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        item.Transition(request.BaseRevision, request.TargetStatus, request.ResolutionNote,
            request.ClosureEvidenceReferences, actor.UserId, clock.UtcNow);
        if (item.Status is IssueStatus.Closed or IssueStatus.NotAnIssue or IssueStatus.Void)
            await CloseEscalationsAsync(db, actor.TenantId, projectId, SlaEntityType.Issue, item.Id, clock.UtcNow, cancellationToken);
        var response = IssueResponse.From(item);
        await PersistAsync(db, httpContext, actor, projectId, item.Id, "ManagementIssue", "IssueTransitioned",
            IssueAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ProposeRiskAsync(Guid projectId, ProposeRiskRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        IProjectDirectory projectDirectory, IProjectAssigneeDirectory assignees, ActionControlDbContext db,
        IClock clock, ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "risks.create", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (IsSensitive(request.Confidentiality) && !await HasPermissionAsync(permissions, actor, projectId,
                "governance.sensitive.write", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.risk.propose", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        var owner = await assignees.FindAssignableAsync(actor.TenantId, projectId, request.OwnerUserId, cancellationToken);
        if (owner is null) return Results.UnprocessableEntity(new { code = "governance.owner.not_assignable" });
        var sla = await ResolveSlaAsync(db, actor.TenantId, projectId, SlaEntityType.Risk, null,
            clock.UtcNow, project, cancellationToken);
        var item = ProjectRisk.Propose(request.ClientGeneratedId, actor.TenantId, projectId, request.Type,
            request.Cause, request.UncertainEvent, request.ImpactStatement, request.Category, owner.UserId,
            owner.DisplayName, request.Confidentiality, request.SourceModule, request.SourceEntityType,
            request.SourceEntityId, request.SourceRevision, request.SourceSnapshot, request.EvidenceReferences,
            sla.DueAt, sla.RuleId, actor.UserId, clock.UtcNow);
        db.Risks.Add(item);
        var response = RiskResponse.From(item);
        await PersistAsync(db, httpContext, actor, projectId, item.Id, "ProjectRisk", "RiskProposed",
            RiskAudit(item), response, replay, StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/risks/{item.Id}", response);
    }

    private static async Task<IResult> AssessRiskAsync(Guid projectId, Guid riskId, AssessRiskRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        IProjectDirectory projectDirectory, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "risks.assess", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.risk.assess", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var tuple = await FindRiskAndProjectAsync(db, projectDirectory, actor, projectId, riskId, cancellationToken);
        if (tuple.Risk is null || tuple.Project is null) return Results.NotFound();
        if (!await CanSeeAsync(tuple.Risk.Confidentiality, permissions, actor, projectId, cancellationToken)) return Results.NotFound();
        if (request.ReviewDate < ResolveLocalDate(clock.UtcNow, tuple.Project.TimeZone))
            throw new DomainRuleException("governance.risk.review_date.in_past", "Risk review date cannot be in the past.");
        var matrix = await db.RiskMatrices.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == request.MatrixVersionId && x.EffectiveFrom <= clock.UtcNow,
            cancellationToken);
        if (matrix is null) return Results.UnprocessableEntity(new { code = "governance.risk_matrix.not_found" });
        tuple.Risk.Assess(request.BaseRevision, matrix, request.Probability, request.TimeImpact,
            request.CostImpact, request.QualityImpact, request.SafetyImpact, request.ContractImpact,
            request.OperationsImpact, request.ResponseStrategy, request.ResponsePlan,
            request.EarlyWarningIndicator, request.ReviewDate, actor.UserId, clock.UtcNow);
        return await PersistRiskAsync(tuple.Risk, "RiskAssessed", request, replay, httpContext, actor,
            projectId, db, sideEffects, clock, cancellationToken);
    }

    private static async Task<IResult> ActivateRiskAsync(Guid projectId, Guid riskId, RiskStateRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        ActionControlDbContext db, IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await PrepareRiskCommandAsync(projectId, riskId, request, "risks.manage",
            "governance.risk.activate", httpContext, actor, permissions, db, idempotency, cancellationToken);
        if (access.Result is not null) return access.Result;
        access.Risk!.Activate(request.BaseRevision, actor.UserId, clock.UtcNow);
        return await PersistRiskAsync(access.Risk, "RiskActivated", request, access.Replay, httpContext,
            actor, projectId, db, sideEffects, clock, cancellationToken);
    }

    private static async Task<IResult> ReviewRiskAsync(Guid projectId, Guid riskId, ReviewRiskRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        IProjectDirectory projectDirectory, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "risks.review", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.risk.review", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var tuple = await FindRiskAndProjectAsync(db, projectDirectory, actor, projectId, riskId, cancellationToken);
        if (tuple.Risk is null || tuple.Project is null) return Results.NotFound();
        if (!await CanSeeAsync(tuple.Risk.Confidentiality, permissions, actor, projectId, cancellationToken)) return Results.NotFound();
        if (request.NextReviewDate < ResolveLocalDate(clock.UtcNow, tuple.Project.TimeZone))
            throw new DomainRuleException("governance.risk.review_date.in_past", "Risk review date cannot be in the past.");
        var matrix = await db.RiskMatrices.SingleAsync(x => x.Id == tuple.Risk.MatrixVersionId &&
            x.TenantId == actor.TenantId && x.ProjectId == projectId, cancellationToken);
        tuple.Risk.Review(request.BaseRevision, matrix, request.ResidualProbability, request.ResidualImpact,
            request.ResponsePlan, request.NextReviewDate, actor.UserId, clock.UtcNow);
        return await PersistRiskAsync(tuple.Risk, "RiskReviewed", request, replay, httpContext,
            actor, projectId, db, sideEffects, clock, cancellationToken);
    }

    private static async Task<IResult> MaterializeRiskAsync(Guid projectId, Guid riskId,
        MaterializeRiskRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projectDirectory,
        IProjectAssigneeDirectory assignees, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "risks.manage", cancellationToken) ||
            !await HasPermissionAsync(permissions, actor, projectId, "issues.create", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.risk.materialize", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var tuple = await FindRiskAndProjectAsync(db, projectDirectory, actor, projectId, riskId, cancellationToken);
        if (tuple.Risk is null || tuple.Project is null) return Results.NotFound();
        if (!await CanSeeAsync(tuple.Risk.Confidentiality, permissions, actor, projectId, cancellationToken)) return Results.NotFound();
        if (request.TargetResolutionDate < ResolveLocalDate(clock.UtcNow, tuple.Project.TimeZone))
            throw new DomainRuleException("governance.issue.target.in_past", "Target resolution date cannot be in the past.");
        var owner = await assignees.FindAssignableAsync(actor.TenantId, projectId, request.OwnerUserId, cancellationToken);
        if (owner is null) return Results.UnprocessableEntity(new { code = "governance.owner.not_assignable" });
        var sla = await ResolveSlaAsync(db, actor.TenantId, projectId, SlaEntityType.Issue,
            request.Severity, clock.UtcNow, tuple.Project, cancellationToken);
        var issue = ManagementIssue.Create(request.IssueId, actor.TenantId, projectId, request.Title,
            request.ObservedFact, request.Category, request.Severity, request.Urgency, owner.UserId,
            owner.DisplayName, request.TargetResolutionDate, "ActionControl", "ProjectRisk", tuple.Risk.Id,
            tuple.Risk.Revision, $"تحقق ریسک {tuple.Risk.Number}: {tuple.Risk.UncertainEvent}", tuple.Risk.Id,
            request.EvidenceReferences, tuple.Risk.Confidentiality, sla.DueAt, sla.RuleId, actor.UserId, clock.UtcNow);
        tuple.Risk.Materialize(request.BaseRevision, issue.Id, actor.UserId, clock.UtcNow);
        db.Issues.Add(issue);
        await CloseEscalationsAsync(db, actor.TenantId, projectId, SlaEntityType.Risk, tuple.Risk.Id, clock.UtcNow, cancellationToken);
        var response = new { risk = RiskResponse.From(tuple.Risk), issue = IssueResponse.From(issue) };
        await PersistAsync(db, httpContext, actor, projectId, tuple.Risk.Id, "ProjectRisk", "RiskMaterialized",
            new Dictionary<string, object?> { ["riskNumber"] = tuple.Risk.Number, ["issueId"] = issue.Id,
                ["issueNumber"] = issue.Number, ["revision"] = tuple.Risk.Revision }, response, replay,
            StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/issues/{issue.Id}", response);
    }

    private static async Task<IResult> CloseRiskAsync(Guid projectId, Guid riskId, CloseRiskRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        ActionControlDbContext db, IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await PrepareRiskCommandAsync(projectId, riskId, request, "risks.manage",
            "governance.risk.close", httpContext, actor, permissions, db, idempotency, cancellationToken);
        if (access.Result is not null) return access.Result;
        access.Risk!.Close(request.BaseRevision, request.Expired, request.Reason,
            request.EvidenceReferences, actor.UserId, clock.UtcNow);
        await CloseEscalationsAsync(db, actor.TenantId, projectId, SlaEntityType.Risk, riskId, clock.UtcNow, cancellationToken);
        return await PersistRiskAsync(access.Risk, "RiskClosed", request, access.Replay, httpContext,
            actor, projectId, db, sideEffects, clock, cancellationToken);
    }

    private static async Task<IResult> ReopenRiskAsync(Guid projectId, Guid riskId, ReopenRiskRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        ActionControlDbContext db, IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await PrepareRiskCommandAsync(projectId, riskId, request, "risks.manage",
            "governance.risk.reopen", httpContext, actor, permissions, db, idempotency, cancellationToken);
        if (access.Result is not null) return access.Result;
        access.Risk!.Reopen(request.BaseRevision, request.Reason, actor.UserId, clock.UtcNow);
        return await PersistRiskAsync(access.Risk, "RiskReopened", request, access.Replay, httpContext,
            actor, projectId, db, sideEffects, clock, cancellationToken);
    }

    private static async Task<(ProjectRisk? Risk, ProjectControlProfile? Project)> FindRiskAndProjectAsync(
        ActionControlDbContext db, IProjectDirectory projects, ICurrentActor actor, Guid projectId,
        Guid riskId, CancellationToken cancellationToken)
    {
        var risk = await db.Risks.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == riskId, cancellationToken);
        var project = risk is null ? null : await projects.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        return (risk, project);
    }

    private static async Task<(ProjectRisk? Risk, (string Key, string Hash, string Operation, IResult? Result) Replay, IResult? Result)>
        PrepareRiskCommandAsync<TRequest>(Guid projectId, Guid riskId, TRequest request, string permission,
            string operation, HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
            ActionControlDbContext db, IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var empty = (string.Empty, string.Empty, operation, (IResult?)null);
        if (!actor.IsAuthenticated) return (null, empty, Results.Unauthorized());
        if (!await HasPermissionAsync(permissions, actor, projectId, permission, cancellationToken))
            return (null, empty, Results.StatusCode(StatusCodes.Status403Forbidden));
        var replay = await GetReplayAsync(httpContext, actor, idempotency, operation, request, cancellationToken);
        if (replay.Result is not null) return (null, replay, replay.Result);
        var risk = await db.Risks.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == riskId, cancellationToken);
        if (risk is null) return (null, replay, Results.NotFound());
        if (!await CanSeeAsync(risk.Confidentiality, permissions, actor, projectId, cancellationToken))
            return (null, replay, Results.NotFound());
        return (risk, replay, null);
    }

    private static async Task<IResult> PersistRiskAsync<TRequest>(ProjectRisk item, string eventType,
        TRequest request, (string Key, string Hash, string Operation, IResult? Result) replay,
        HttpContext httpContext, ICurrentActor actor, Guid projectId, ActionControlDbContext db,
        ITransactionalSideEffectWriter sideEffects, IClock clock, CancellationToken cancellationToken)
    {
        _ = request;
        var response = RiskResponse.From(item);
        await PersistAsync(db, httpContext, actor, projectId, item.Id, "ProjectRisk", eventType,
            RiskAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Dictionary<string, object?> RiskAudit(ProjectRisk item) => new()
    {
        ["number"] = item.Number, ["status"] = item.Status.ToString(), ["ownerUserId"] = item.OwnerUserId,
        ["matrixVersion"] = item.MatrixVersion, ["rating"] = (item.ResidualRating ?? item.InherentRating)?.ToString(),
        ["materializedIssueId"] = item.MaterializedIssueId, ["revision"] = item.Revision
    };

    private static Dictionary<string, object?> IssueAudit(ManagementIssue item) => new()
    {
        ["number"] = item.Number, ["status"] = item.Status.ToString(), ["severity"] = item.Severity.ToString(),
        ["ownerUserId"] = item.OwnerUserId, ["materializedFromRiskId"] = item.MaterializedFromRiskId,
        ["revision"] = item.Revision
    };

    private static async Task CloseEscalationsAsync(ActionControlDbContext db, Guid tenantId, Guid projectId,
        SlaEntityType type, Guid entityId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var threads = await db.Escalations.Where(x => x.TenantId == tenantId && x.ProjectId == projectId &&
            x.EntityType == type && x.EntityId == entityId && x.Status != EscalationStatus.ClosedBySourceResolution)
            .ToListAsync(cancellationToken);
        foreach (var thread in threads) thread.CloseFromSource(at);
    }
}
