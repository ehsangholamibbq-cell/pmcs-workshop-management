using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Endpoints;

internal static partial class TechnicalOfficeEndpoints
{
    private static void MapRfiEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/rfis", CreateRfiAsync);
        group.MapPost("/rfis/{rfiId:guid}/internal-review", SubmitRfiForInternalReviewAsync);
        group.MapPost("/rfis/{rfiId:guid}/return", ReturnRfiAsync);
        group.MapPost("/rfis/{rfiId:guid}/issue", IssueRfiAsync);
        group.MapPost("/rfis/{rfiId:guid}/responses", RecordRfiResponseAsync);
        group.MapPost("/rfis/{rfiId:guid}/accept", AcceptRfiResponseAsync);
        group.MapPost("/rfis/{rfiId:guid}/clarification", RequestRfiClarificationAsync);
        group.MapPost("/rfis/{rfiId:guid}/close", CloseRfiAsync);
    }

    private static async Task<IResult> CreateRfiAsync(
        Guid projectId, CreateRfiRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects,
        ICommercialReferenceDirectory commercialDirectory, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "technical.rfis.create", cancellationToken);
        if (access is not null) return access;
        if (!await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var commercial = await commercialDirectory.ValidateAsync(
            actor.TenantId, projectId, request.ContractId, null, cancellationToken);
        if (!commercial.IsValid) return Results.UnprocessableEntity(new { code = commercial.ErrorCode });
        var revisionError = await ValidateRevisionReferencesAsync(
            db, actor.TenantId, projectId, request.RelatedRevisionIds, cancellationToken);
        if (revisionError is not null) return revisionError;
        if (!await CanReferenceRevisionsAsync(
                db, permissions, actor, projectId, request.RelatedRevisionIds, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.rfis.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = TechnicalRfi.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            request.Title, request.Question, request.RequestedFrom, request.Discipline, request.ContractId,
            request.LocationReference, request.WorkItemReference, request.WbsReference, request.SourceIssueId,
            request.RaisedDate, request.RequiredByDate, request.PotentialImpact, request.IsBlocking,
            request.ProposedSolution, request.EvidenceReferences, request.RelatedRevisionIds,
            actor.UserId, clock.UtcNow);
        db.Rfis.Add(item);
        var response = RfiResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalRfi", item.Id, "RfiCreated",
            RfiAudit(item), response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/technical-office/rfis/{item.Id}", response);
    }

    private static Task<IResult> SubmitRfiForInternalReviewAsync(
        Guid projectId, Guid rfiId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => TransitionRfiAsync(
            projectId, rfiId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.rfis.submit", "internal-review", (item, _) => item.SubmitForInternalReview(request.BaseRevision),
            "RfiInternalReviewRequested", cancellationToken);

    private static async Task<IResult> ReturnRfiAsync(
        Guid projectId, Guid rfiId, TechnicalReturnRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => await TransitionRfiAsync(
            projectId, rfiId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.rfis.review", "return",
            (item, at) => item.ReturnToDraft(request.BaseRevision, request.Reason, actor.UserId, at),
            "RfiReturned", cancellationToken);

    private static Task<IResult> IssueRfiAsync(
        Guid projectId, Guid rfiId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => TransitionRfiAsync(
            projectId, rfiId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.rfis.issue", "issue", (item, at) => item.Issue(request.BaseRevision, at),
            "RfiIssued", cancellationToken);

    private static async Task<IResult> RecordRfiResponseAsync(
        Guid projectId, Guid rfiId, RecordRfiResponseRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "technical.rfis.respond", cancellationToken);
        if (access is not null) return access;
        var revisionError = await ValidateRevisionReferencesAsync(
            db, actor.TenantId, projectId, request.ReferencedRevisionIds, cancellationToken);
        if (revisionError is not null) return revisionError;
        if (!await CanReferenceRevisionsAsync(
                db, permissions, actor, projectId, request.ReferencedRevisionIds, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.rfis.respond:{rfiId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindRfiAsync(db, actor.TenantId, projectId, rfiId, cancellationToken);
        if (item is null) return Results.NotFound();
        var referencedRevisionIds = item.RelatedRevisionIds
            .Concat(item.Responses.SelectMany(response => response.ReferencedRevisionIds))
            .Concat(request.ReferencedRevisionIds ?? [])
            .Distinct().ToArray();
        if (!await CanReferenceRevisionsAsync(
                db, permissions, actor, projectId, referencedRevisionIds, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (item.Revision != request.BaseRevision)
            return TechnicalOfficeEndpointSupport.RevisionConflict("technical.rfi.revision.conflict", item.Revision);
        item.RecordResponse(
            request.BaseRevision, request.ResponseText, request.RespondingParty, request.ResponseAt,
            request.Classification, request.ChangePotential, request.ReferencedRevisionIds, actor.UserId);
        var response = RfiResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalRfi", item.Id, "RfiResponseRecorded",
            RfiAudit(item), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> AcceptRfiResponseAsync(
        Guid projectId, Guid rfiId, TechnicalReviewRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => await TransitionRfiAsync(
            projectId, rfiId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.rfis.accept", "accept",
            (item, at) => item.AcceptResponse(request.BaseRevision, actor.UserId, at, request.Comment),
            "RfiResponseAccepted", cancellationToken);

    private static async Task<IResult> RequestRfiClarificationAsync(
        Guid projectId, Guid rfiId, TechnicalReturnRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => await TransitionRfiAsync(
            projectId, rfiId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.rfis.accept", "clarification",
            (item, at) => item.RequireClarification(request.BaseRevision, actor.UserId, at, request.Reason),
            "RfiClarificationRequested", cancellationToken);

    private static Task<IResult> CloseRfiAsync(
        Guid projectId, Guid rfiId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => TransitionRfiAsync(
            projectId, rfiId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.rfis.close", "close", (item, at) => item.Close(request.BaseRevision, at),
            "RfiClosed", cancellationToken);

    private static async Task<IResult> TransitionRfiAsync<TRequest>(
        Guid projectId, Guid rfiId, TRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, TechnicalOfficeDbContext db, IClock clock,
        ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, string permission,
        string operation, Action<TechnicalRfi, DateTimeOffset> transition, string eventType,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, permission, cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.rfis.{operation}:{rfiId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindRfiAsync(db, actor.TenantId, projectId, rfiId, cancellationToken);
        if (item is null) return Results.NotFound();
        var referencedRevisionIds = item.RelatedRevisionIds
            .Concat(item.Responses.SelectMany(response => response.ReferencedRevisionIds))
            .Distinct().ToArray();
        if (!await CanReferenceRevisionsAsync(
                db, permissions, actor, projectId, referencedRevisionIds, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        transition(item, clock.UtcNow);
        var response = RfiResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalRfi", item.Id, eventType,
            RfiAudit(item), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<TechnicalRfi?> FindRfiAsync(
        TechnicalOfficeDbContext db, Guid tenantId, Guid projectId, Guid id,
        CancellationToken cancellationToken) => db.Rfis.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, cancellationToken);

    private static async Task<IResult?> ValidateRevisionReferencesAsync(
        TechnicalOfficeDbContext db, Guid tenantId, Guid projectId, IReadOnlyCollection<Guid>? ids,
        CancellationToken cancellationToken)
    {
        var normalized = ids?.Distinct().ToArray() ?? [];
        if (normalized.Length == 0) return null;
        return await db.DocumentRevisions.CountAsync(item => item.TenantId == tenantId &&
            item.ProjectId == projectId && normalized.Contains(item.Id), cancellationToken) == normalized.Length
            ? null
            : Results.UnprocessableEntity(new { code = "technical.document_revision.reference.invalid" });
    }

    private static Dictionary<string, object?> RfiAudit(TechnicalRfi item) => new()
    {
        ["number"] = item.Number, ["title"] = item.Title, ["status"] = item.Status.ToString(),
        ["isBlocking"] = item.IsBlocking, ["potentialImpact"] = item.PotentialImpact.ToString(),
        ["responseCount"] = item.Responses.Count, ["contractId"] = item.ContractId,
        ["revision"] = item.Revision
    };
}
