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
    private static void MapDecisionEndpoints(RouteGroupBuilder governance)
    {
        governance.MapPost("/decision-requests", CreateDecisionRequestAsync);
        governance.MapPost("/decision-requests/{requestId:guid}/submit", SubmitDecisionRequestAsync);
        governance.MapPost("/decision-requests/{requestId:guid}/begin", BeginDecisionAsync);
        governance.MapPost("/decision-requests/{requestId:guid}/implement", ImplementDecisionAsync);
        governance.MapPost("/decision-requests/{requestId:guid}/request-information", RequestDecisionInformationAsync);
        governance.MapPost("/decision-requests/{requestId:guid}/decisions", RecordDecisionAsync);
        governance.MapPost("/decisions/{decisionId:guid}/supersede", SupersedeDecisionAsync);
        governance.MapPost("/decisions/{decisionId:guid}/effect-review", ReviewDecisionEffectAsync);
    }

    private static async Task<IResult> CreateDecisionRequestAsync(Guid projectId, CreateDecisionRequest request,
        HttpContext httpContext, ICurrentActor actor, IProjectPermissionService permissions,
        IProjectDirectory projectDirectory, IProjectAssigneeDirectory assignees, ActionControlDbContext db,
        IClock clock, ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "decision-requests.create", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (IsSensitive(request.Confidentiality) && !await HasPermissionAsync(permissions, actor, projectId,
                "governance.sensitive.write", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.decision-request.create", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        if (request.RequiredBy < ResolveLocalDate(clock.UtcNow, project.TimeZone))
            throw new DomainRuleException("governance.decision_request.required_by.in_past", "Required decision date cannot be in the past.");
        var authority = await assignees.FindAssignableAsync(actor.TenantId, projectId, request.AuthorityUserId, cancellationToken);
        if (authority is null) return Results.UnprocessableEntity(new { code = "governance.decision_request.authority.not_assignable" });
        if (!await permissions.HasProjectPermissionAsync(actor.TenantId, authority.UserId, projectId,
                "decisions.decide", cancellationToken))
            return Results.UnprocessableEntity(new { code = "governance.decision_request.authority.not_authorized" });
        var sla = await ResolveSlaAsync(db, actor.TenantId, projectId, SlaEntityType.DecisionRequest,
            null, clock.UtcNow, project, cancellationToken);
        var item = DecisionRequest.Create(request.ClientGeneratedId, actor.TenantId, projectId,
            request.Question, request.WhyNow, request.RequiredBy, authority.UserId, authority.DisplayName,
            request.KnownFacts, request.Assumptions, request.Predictions, request.Options,
            request.Recommendation, request.Constraints, request.EvidenceReferences, request.Confidentiality,
            request.SourceModule, request.SourceEntityType, request.SourceEntityId, request.SourceRevision,
            request.SourceSnapshot, sla.DueAt, sla.RuleId, actor.UserId, clock.UtcNow);
        db.DecisionRequests.Add(item);
        var response = DecisionRequestResponse.From(item);
        await PersistAsync(db, httpContext, actor, projectId, item.Id, "DecisionRequest", "DecisionRequestCreated",
            DecisionAudit(item), response, replay, StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/decision-requests/{item.Id}", response);
    }

    private static Task<IResult> SubmitDecisionRequestAsync(Guid projectId, Guid requestId,
        DecisionRequestStateRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => ChangeDecisionRequestAsync(projectId, requestId, request,
            "decision-requests.submit", "governance.decision-request.submit", "DecisionRequestSubmitted",
            (item, at) => item.Submit(request.BaseRevision, actor.UserId, at), httpContext, actor,
            permissions, db, clock, sideEffects, idempotency, cancellationToken);

    private static Task<IResult> BeginDecisionAsync(Guid projectId, Guid requestId,
        DecisionRequestStateRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => ChangeDecisionRequestAsync(projectId, requestId, request,
            "decisions.decide", "governance.decision-request.begin", "DecisionStarted",
            (item, at) => item.BeginDecision(request.BaseRevision, actor.UserId, at), httpContext, actor,
            permissions, db, clock, sideEffects, idempotency, cancellationToken, true);

    private static Task<IResult> RequestDecisionInformationAsync(Guid projectId, Guid requestId,
        DecisionRequestStateRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => ChangeDecisionRequestAsync(projectId, requestId, request,
            "decisions.decide", "governance.decision-request.information", "DecisionInformationRequested",
            (item, at) => item.RequestMoreInformation(request.BaseRevision, request.InformationRequest ?? string.Empty,
                actor.UserId, at), httpContext, actor, permissions, db, clock, sideEffects, idempotency,
            cancellationToken, true);

    private static Task<IResult> ImplementDecisionAsync(Guid projectId, Guid requestId,
        DecisionRequestStateRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => ChangeDecisionRequestAsync(projectId, requestId, request,
            "decisions.implement", "governance.decision-request.implement", "DecisionImplementationStarted",
            (item, at) => item.MarkImplementing(request.BaseRevision, actor.UserId, at), httpContext, actor,
            permissions, db, clock, sideEffects, idempotency, cancellationToken);

    private static async Task<IResult> ChangeDecisionRequestAsync<TRequest>(Guid projectId, Guid requestId,
        TRequest request, string permission, string operation, string eventType,
        Action<DecisionRequest, DateTimeOffset> change, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken, bool requireAuthority = false)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, permission, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, operation, request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var item = await db.DecisionRequests.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == requestId, cancellationToken);
        if (item is null || !await CanSeeAsync(item.Confidentiality, permissions, actor, projectId, cancellationToken))
            return Results.NotFound();
        if (requireAuthority && item.AuthorityUserId != actor.UserId && !await HasPermissionAsync(permissions,
                actor, projectId, "decisions.override-authority", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        change(item, clock.UtcNow);
        var response = DecisionRequestResponse.From(item);
        await PersistAsync(db, httpContext, actor, projectId, item.Id, "DecisionRequest", eventType,
            DecisionAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> RecordDecisionAsync(Guid projectId, Guid requestId,
        RecordDecisionRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectAssigneeDirectory assignees, ActionControlDbContext db,
        IClock clock, ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "decisions.decide", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.decision.record", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var item = await db.DecisionRequests.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == requestId, cancellationToken);
        if (item is null || !await CanSeeAsync(item.Confidentiality, permissions, actor, projectId, cancellationToken))
            return Results.NotFound();
        if (item.AuthorityUserId != actor.UserId && !await HasPermissionAsync(permissions, actor,
                projectId, "decisions.override-authority", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var authority = await assignees.FindAssignableAsync(actor.TenantId, projectId, actor.UserId, cancellationToken);
        if (authority is null) return Results.UnprocessableEntity(new { code = "governance.decision.authority.not_assignable" });
        var decision = DecisionRecord.Record(request.ClientGeneratedId, item, request.SelectedOption,
            request.Rationale, request.Conditions, request.Channel, request.DecidedAt, request.EffectiveDate,
            actor.UserId, authority.DisplayName, null, clock.UtcNow);
        item.LinkDecision(request.BaseRevision, decision.Id, actor.UserId, clock.UtcNow);
        db.Decisions.Add(decision);
        await CloseEscalationsAsync(db, actor.TenantId, projectId, SlaEntityType.DecisionRequest,
            item.Id, clock.UtcNow, cancellationToken);
        var response = new { request = DecisionRequestResponse.From(item), decision = DecisionResponse.From(decision) };
        await PersistAsync(db, httpContext, actor, projectId, decision.Id, "DecisionRecord", "DecisionRecorded",
            new Dictionary<string, object?> { ["decisionRequestId"] = item.Id, ["requestNumber"] = item.Number,
                ["decisionNumber"] = decision.Number, ["channel"] = decision.Channel.ToString(),
                ["requestRevision"] = item.Revision, ["decisionRevision"] = decision.Revision }, response, replay,
            StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/decisions/{decision.Id}", response);
    }

    private static async Task<IResult> SupersedeDecisionAsync(Guid projectId, Guid decisionId,
        SupersedeDecisionRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectAssigneeDirectory assignees, ActionControlDbContext db,
        IClock clock, ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "decisions.decide", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.decision.supersede", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var original = await db.Decisions.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == decisionId, cancellationToken);
        if (original is null) return Results.NotFound();
        var decisionRequest = await db.DecisionRequests.SingleAsync(x => x.Id == original.DecisionRequestId &&
            x.TenantId == actor.TenantId && x.ProjectId == projectId, cancellationToken);
        if (!await CanSeeAsync(decisionRequest.Confidentiality, permissions, actor, projectId, cancellationToken))
            return Results.NotFound();
        if (decisionRequest.AuthorityUserId != actor.UserId && !await HasPermissionAsync(permissions, actor,
                projectId, "decisions.override-authority", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var authority = await assignees.FindAssignableAsync(actor.TenantId, projectId, actor.UserId, cancellationToken);
        if (authority is null) return Results.UnprocessableEntity(new { code = "governance.decision.authority.not_assignable" });
        var replacement = DecisionRecord.Record(request.ClientGeneratedId, decisionRequest,
            request.SelectedOption, request.Rationale, request.Conditions, request.Channel, request.DecidedAt,
            request.EffectiveDate, actor.UserId, authority.DisplayName, original.Id, clock.UtcNow);
        original.Supersede(request.DecisionBaseRevision, replacement.Id);
        decisionRequest.LinkReplacementDecision(request.RequestBaseRevision, original.Id, replacement.Id,
            actor.UserId, clock.UtcNow);
        db.Decisions.Add(replacement);
        var response = new { superseded = DecisionResponse.From(original), current = DecisionResponse.From(replacement),
            request = DecisionRequestResponse.From(decisionRequest) };
        await PersistAsync(db, httpContext, actor, projectId, replacement.Id, "DecisionRecord", "DecisionSuperseded",
            new Dictionary<string, object?> { ["supersededDecisionId"] = original.Id,
                ["replacementDecisionId"] = replacement.Id, ["decisionRequestId"] = decisionRequest.Id },
            response, replay, StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/governance/decisions/{replacement.Id}", response);
    }

    private static async Task<IResult> ReviewDecisionEffectAsync(Guid projectId, Guid decisionId,
        ReviewDecisionEffectRequest request, HttpContext httpContext, ICurrentActor actor,
        IProjectPermissionService permissions, ActionControlDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await HasPermissionAsync(permissions, actor, projectId, "decisions.review-effect", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await GetReplayAsync(httpContext, actor, idempotency, "governance.decision.effect-review", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var decision = await db.Decisions.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId &&
            x.ProjectId == projectId && x.Id == decisionId, cancellationToken);
        if (decision is null) return Results.NotFound();
        var decisionRequest = await db.DecisionRequests.SingleAsync(x => x.Id == decision.DecisionRequestId &&
            x.TenantId == actor.TenantId && x.ProjectId == projectId, cancellationToken);
        if (!await CanSeeAsync(decisionRequest.Confidentiality, permissions, actor, projectId, cancellationToken))
            return Results.NotFound();
        if (decisionRequest.DecisionRecordId != decision.Id)
            return Results.Conflict(new { code = "governance.decision.not_current" });
        decision.ReviewEffect(request.DecisionBaseRevision, request.Review, request.EvidenceReferences);
        decisionRequest.MarkEffectReviewed(request.RequestBaseRevision, actor.UserId, clock.UtcNow);
        var response = new { request = DecisionRequestResponse.From(decisionRequest), decision = DecisionResponse.From(decision) };
        await PersistAsync(db, httpContext, actor, projectId, decision.Id, "DecisionRecord", "DecisionEffectReviewed",
            new Dictionary<string, object?> { ["decisionRequestId"] = decisionRequest.Id,
                ["decisionRevision"] = decision.Revision, ["requestRevision"] = decisionRequest.Revision },
            response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Dictionary<string, object?> DecisionAudit(DecisionRequest item) => new()
    {
        ["number"] = item.Number, ["status"] = item.Status.ToString(),
        ["authorityUserId"] = item.AuthorityUserId, ["requiredBy"] = item.RequiredBy,
        ["decisionRecordId"] = item.DecisionRecordId, ["revision"] = item.Revision
    };
}
