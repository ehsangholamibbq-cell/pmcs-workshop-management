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
    private static void MapDocumentEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/documents", CreateDocumentAsync);
        group.MapPost("/documents/{documentId:guid}/revisions", CreateDocumentRevisionAsync);
        group.MapPost("/document-revisions/{revisionId:guid}/submit", SubmitDocumentRevisionAsync);
        group.MapPost("/document-revisions/{revisionId:guid}/approve", ApproveDocumentRevisionAsync);
        group.MapPost("/document-revisions/{revisionId:guid}/return", ReturnDocumentRevisionAsync);
        group.MapPost("/transmittals", CreateTransmittalAsync);
        group.MapPost("/transmittals/{transmittalId:guid}/issue", IssueTransmittalAsync);
        group.MapPost("/transmittals/{transmittalId:guid}/acknowledge", AcknowledgeTransmittalAsync);
    }

    private static async Task<IResult> CreateDocumentAsync(
        Guid projectId, CreateTechnicalDocumentRequest request, HttpContext httpContext,
        ICurrentActor actor, IProjectPermissionService permissionService, IProjectDirectory projectDirectory,
        ICommercialReferenceDirectory commercialDirectory, TechnicalOfficeDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffectWriter, IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissionService, projectId, "technical.documents.create", cancellationToken);
        if (access is not null) return access;
        if (!string.IsNullOrWhiteSpace(request.Confidentiality) &&
            !await CanAsync(permissionService, actor, projectId, "technical.confidential.manage", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await projectDirectory.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var reference = await commercialDirectory.ValidateAsync(
            actor.TenantId, projectId, request.ContractId, null, cancellationToken);
        if (!reference.IsValid) return Results.UnprocessableEntity(new { code = reference.ErrorCode });

        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            httpContext, actor, idempotencyStore, $"technical.documents.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;

        var id = request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid());
        var item = TechnicalDocument.Create(
            id, actor.TenantId, projectId, request.Title, request.Type, request.Discipline,
            request.Originator, request.ContractId, request.LocationReference, request.WorkItemReference,
            request.WbsReference, request.Confidentiality, actor.UserId, clock.UtcNow);
        dbContext.Documents.Add(item);
        var response = TechnicalDocumentResponse.From(item);
        await PersistAsync(dbContext, httpContext, actor, projectId, "TechnicalDocument", item.Id,
            "DocumentRegistered", DocumentAudit(item), response, command!, StatusCodes.Status201Created,
            sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/technical-office/documents/{item.Id}", response);
    }

    private static async Task<IResult> CreateDocumentRevisionAsync(
        Guid projectId, Guid documentId, CreateDocumentRevisionRequest request, HttpContext httpContext,
        ICurrentActor actor, IProjectPermissionService permissionService, TechnicalOfficeDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffectWriter, IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissionService, projectId, "technical.documents.create-revision", cancellationToken);
        if (access is not null) return access;
        var document = await dbContext.Documents.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.ProjectId == projectId && item.Id == documentId,
            cancellationToken);
        if (document is null) return Results.NotFound();
        if (!string.IsNullOrWhiteSpace(document.Confidentiality) &&
            !await CanAsync(permissionService, actor, projectId, "technical.confidential.manage", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        TechnicalDocumentRevision? superseded = null;
        if (request.SupersedesRevisionId.HasValue)
        {
            superseded = await FindRevisionAsync(dbContext, actor.TenantId, projectId, request.SupersedesRevisionId.Value, cancellationToken);
            if (superseded is null || superseded.DocumentId != documentId ||
                superseded.Status is DocumentRevisionStatus.Draft or DocumentRevisionStatus.Submitted)
                return Results.UnprocessableEntity(new { code = "technical.revision.supersedes.invalid" });
        }
        var normalizedCode = string.IsNullOrWhiteSpace(request.RevisionCode)
            ? string.Empty
            : request.RevisionCode.Trim().ToUpperInvariant();
        if (await dbContext.DocumentRevisions.AnyAsync(item => item.TenantId == actor.TenantId &&
                item.ProjectId == projectId && item.DocumentId == documentId && item.RevisionCode == normalizedCode,
                cancellationToken))
            return Results.Conflict(new { code = "technical.revision.code.duplicate" });

        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            httpContext, actor, idempotencyStore, $"technical.document-revisions.create:{documentId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = TechnicalDocumentRevision.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId, documentId,
            request.RevisionCode, request.RevisionDate, request.Purpose, request.FileName, request.FileReference,
            request.Sha256, request.SupersedesRevisionId, actor.UserId, clock.UtcNow);
        dbContext.DocumentRevisions.Add(item);
        var response = DocumentRevisionResponse.From(item);
        await PersistAsync(dbContext, httpContext, actor, projectId, "TechnicalDocumentRevision", item.Id,
            "DocumentRevisionCreated", RevisionAudit(item), response, command!, StatusCodes.Status201Created,
            sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/technical-office/document-revisions/{item.Id}", response);
    }

    private static Task<IResult> SubmitDocumentRevisionAsync(
        Guid projectId, Guid revisionId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) =>
        TransitionRevisionAsync(projectId, revisionId, request, context, actor, permissions, db, clock,
            effects, idempotency, "technical.documents.submit", "submit", cancellationToken);

    private static async Task<IResult> ApproveDocumentRevisionAsync(
        Guid projectId, Guid revisionId, TechnicalReviewRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) =>
        await ReviewRevisionAsync(projectId, revisionId, request.BaseRevision, request.Comment, false,
            context, actor, permissions, db, clock, effects, idempotency, cancellationToken);

    private static async Task<IResult> ReturnDocumentRevisionAsync(
        Guid projectId, Guid revisionId, TechnicalReturnRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) =>
        await ReviewRevisionAsync(projectId, revisionId, request.BaseRevision, request.Reason, true,
            context, actor, permissions, db, clock, effects, idempotency, cancellationToken);

    private static async Task<IResult> TransitionRevisionAsync(
        Guid projectId, Guid revisionId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        string permission, string transition, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, permission, cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.document-revisions.{transition}:{revisionId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindRevisionAsync(db, actor.TenantId, projectId, revisionId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (!await CanReferenceRevisionsAsync(db, permissions, actor, projectId, [item.Id], cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (item.Revision != request.BaseRevision)
            return TechnicalOfficeEndpointSupport.RevisionConflict("technical.revision.revision.conflict", item.Revision);
        item.Submit(request.BaseRevision, clock.UtcNow);
        var response = DocumentRevisionResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalDocumentRevision", item.Id,
            "DocumentRevisionSubmitted", RevisionAudit(item), response, command!, StatusCodes.Status200OK,
            effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ReviewRevisionAsync(
        Guid projectId, Guid revisionId, long baseRevision, string? comment, bool returned,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions,
        TechnicalOfficeDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "technical.documents.review", cancellationToken);
        if (access is not null) return access;
        var requestBody = new { baseRevision, comment };
        var operation = returned ? "return" : "approve";
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.document-revisions.{operation}:{revisionId:N}", requestBody, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindRevisionAsync(db, actor.TenantId, projectId, revisionId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (!await CanReferenceRevisionsAsync(db, permissions, actor, projectId, [item.Id], cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (item.Revision != baseRevision)
            return TechnicalOfficeEndpointSupport.RevisionConflict("technical.revision.revision.conflict", item.Revision);
        if (returned) item.Return(baseRevision, actor.UserId, clock.UtcNow, comment ?? string.Empty);
        else item.Approve(baseRevision, actor.UserId, clock.UtcNow, comment);
        var response = DocumentRevisionResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalDocumentRevision", item.Id,
            returned ? "DocumentRevisionReturned" : "DocumentRevisionApproved", RevisionAudit(item), response,
            command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateTransmittalAsync(
        Guid projectId, CreateTransmittalRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "technical.transmittals.create", cancellationToken);
        if (access is not null) return access;
        if (!await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var ids = request.RevisionIds?.Distinct().ToArray() ?? [];
        if (ids.Length == 0 || await db.DocumentRevisions.CountAsync(item => item.TenantId == actor.TenantId &&
                item.ProjectId == projectId && ids.Contains(item.Id), cancellationToken) != ids.Length)
            return Results.UnprocessableEntity(new { code = "technical.transmittal.revisions.invalid" });
        if (!await CanReferenceRevisionsAsync(db, permissions, actor, projectId, ids, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.transmittals.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = TechnicalTransmittal.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            request.Sender, request.Recipients, ids, request.Purpose, request.DeliveryChannel,
            request.DueResponseDate, actor.UserId, clock.UtcNow);
        db.Transmittals.Add(item);
        var response = TransmittalResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalTransmittal", item.Id,
            "TransmittalCreated", TransmittalAudit(item), response, command!, StatusCodes.Status201Created,
            effects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/technical-office/transmittals/{item.Id}", response);
    }

    private static async Task<IResult> IssueTransmittalAsync(
        Guid projectId, Guid transmittalId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "technical.transmittals.issue", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.transmittals.issue:{transmittalId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await db.Transmittals.SingleOrDefaultAsync(candidate => candidate.TenantId == actor.TenantId &&
            candidate.ProjectId == projectId && candidate.Id == transmittalId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (!await CanReferenceRevisionsAsync(db, permissions, actor, projectId, item.RevisionIds, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (item.Revision != request.BaseRevision)
            return TechnicalOfficeEndpointSupport.RevisionConflict("technical.transmittal.revision.conflict", item.Revision);
        var ids = item.RevisionIds.ToArray();
        if (!await CanReferenceRevisionsAsync(db, permissions, actor, projectId, ids, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var revisions = await db.DocumentRevisions.Where(candidate => candidate.TenantId == actor.TenantId &&
            candidate.ProjectId == projectId && ids.Contains(candidate.Id)).ToListAsync(cancellationToken);
        if (revisions.Count != ids.Length || revisions.Any(candidate => candidate.Status != DocumentRevisionStatus.Approved))
            return Results.UnprocessableEntity(new { code = "technical.transmittal.revisions.not_approved" });
        var documentIds = revisions.Select(candidate => candidate.DocumentId).Distinct().ToArray();
        if (documentIds.Length != revisions.Count)
            return Results.UnprocessableEntity(new { code = "technical.transmittal.multiple_revisions_same_document" });
        var documents = await db.Documents.Where(candidate => candidate.TenantId == actor.TenantId &&
            candidate.ProjectId == projectId && documentIds.Contains(candidate.Id)).ToListAsync(cancellationToken);
        if (documents.Count != documentIds.Length)
            return Results.UnprocessableEntity(new { code = "technical.transmittal.documents.invalid" });

        var previousIds = documents.Where(candidate => candidate.CurrentOfficialRevisionId.HasValue)
            .Select(candidate => candidate.CurrentOfficialRevisionId!.Value).ToArray();
        List<TechnicalDocumentRevision> previous = previousIds.Length == 0 ? [] : await db.DocumentRevisions.Where(candidate =>
            candidate.TenantId == actor.TenantId && candidate.ProjectId == projectId && previousIds.Contains(candidate.Id))
            .ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        item.Issue(request.BaseRevision, actor.UserId, now);
        foreach (var revision in revisions)
        {
            var document = documents.Single(candidate => candidate.Id == revision.DocumentId);
            var old = previous.SingleOrDefault(candidate => candidate.Id == document.CurrentOfficialRevisionId);
            if (old is not null && old.Id != revision.Id) old.Supersede(now);
            revision.Issue(item.Id, now);
            document.MakeCurrent(revision.Id);
        }
        var response = TransmittalResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalTransmittal", item.Id,
            "TransmittalIssued", TransmittalAudit(item), response, command!, StatusCodes.Status200OK,
            effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> AcknowledgeTransmittalAsync(
        Guid projectId, Guid transmittalId, AcknowledgeTransmittalRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "technical.transmittals.acknowledge", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.transmittals.acknowledge:{transmittalId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await db.Transmittals.SingleOrDefaultAsync(candidate => candidate.TenantId == actor.TenantId &&
            candidate.ProjectId == projectId && candidate.Id == transmittalId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (item.Revision != request.BaseRevision)
            return TechnicalOfficeEndpointSupport.RevisionConflict("technical.transmittal.revision.conflict", item.Revision);
        item.Acknowledge(request.BaseRevision, actor.UserId, clock.UtcNow, request.Reference);
        var response = TransmittalResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalTransmittal", item.Id,
            "TransmittalAcknowledged", TransmittalAudit(item), response, command!, StatusCodes.Status200OK,
            effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<TechnicalDocumentRevision?> FindRevisionAsync(
        TechnicalOfficeDbContext db, Guid tenantId, Guid projectId, Guid revisionId,
        CancellationToken cancellationToken) => db.DocumentRevisions.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.ProjectId == projectId && item.Id == revisionId, cancellationToken);

    private static async Task<IResult?> RequireAsync(
        ICurrentActor actor, IProjectPermissionService permissions, Guid projectId, string permission,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        return await CanAsync(permissions, actor, projectId, permission, cancellationToken)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static Task PersistAsync<TResponse>(
        TechnicalOfficeDbContext db, HttpContext context, ICurrentActor actor, Guid projectId,
        string entityType, Guid entityId, string eventType, IReadOnlyDictionary<string, object?> audit,
        TResponse response, TechnicalCommand command, int statusCode, ITransactionalSideEffectWriter effects,
        IClock clock, CancellationToken cancellationToken) =>
        TechnicalOfficeEndpointSupport.PersistAsync(db, context, actor, projectId, entityType, entityId,
            eventType, audit, response, command, statusCode, effects, clock, cancellationToken);

    private static Dictionary<string, object?> DocumentAudit(TechnicalDocument item) => new()
    {
        ["number"] = item.Number, ["title"] = item.Title, ["type"] = item.Type.ToString(),
        ["discipline"] = item.Discipline, ["contractId"] = item.ContractId,
        ["currentOfficialRevisionId"] = item.CurrentOfficialRevisionId, ["revision"] = item.Revision
    };

    private static Dictionary<string, object?> RevisionAudit(TechnicalDocumentRevision item) => new()
    {
        ["documentId"] = item.DocumentId, ["revisionCode"] = item.RevisionCode,
        ["purpose"] = item.Purpose.ToString(), ["status"] = item.Status.ToString(),
        ["sha256"] = item.Sha256, ["supersedesRevisionId"] = item.SupersedesRevisionId,
        ["transmittalId"] = item.IssuedThroughTransmittalId, ["revision"] = item.Revision
    };

    private static Dictionary<string, object?> TransmittalAudit(TechnicalTransmittal item) => new()
    {
        ["number"] = item.Number, ["status"] = item.Status.ToString(),
        ["revisionCount"] = item.RevisionIds.Count, ["deliveryChannel"] = item.DeliveryChannel,
        ["revision"] = item.Revision
    };
}
