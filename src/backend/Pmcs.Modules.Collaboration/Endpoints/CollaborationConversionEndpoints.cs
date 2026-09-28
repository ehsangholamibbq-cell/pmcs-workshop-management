using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Collaboration.Domain;
using Pmcs.Modules.Collaboration.Persistence;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Collaboration.Endpoints;

internal static partial class CollaborationEndpoints
{
    private static async Task<IResult> ConvertMessageAsync(Guid projectId, Guid messageId,
        ConvertProjectMessageRequest request, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, ISharedDocumentDirectory documents,
        IEnumerable<IProjectMessageConversionDestination> destinations,
        CollaborationDbContext db, IClock clock, IIdempotencyStore idempotency,
        ITransactionalSideEffectWriter effects, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.convert", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (!request.Confirmed || request.DestinationId == Guid.Empty ||
            request.BaseRevision < 1 || request.Details.ValueKind != JsonValueKind.Object ||
            request.DocumentIds is { Length: > 10 } ||
            request.DocumentIds?.Any(id => id == Guid.Empty) == true ||
            request.DocumentIds?.Distinct().Count() != request.DocumentIds?.Length)
            return Results.BadRequest(new { code = "collaboration.conversion.confirmation_or_payload.invalid" });
        var destination = destinations.SingleOrDefault(item => item.Supports(request.DestinationType ?? ""));
        if (destination is null)
            return Results.UnprocessableEntity(new { code = "collaboration.conversion.destination.unsupported" });
        var key = http.Request.Headers["Idempotency-Key"].ToString().Trim();
        IdempotencyKeyRules.Validate(key);
        var operation = $"collaboration.message.convert:{projectId:N}:{actor.UserId:N}";
        var requestHash = RequestHash.Create(JsonSerializer.Serialize(new
        {
            actor.TenantId, projectId, messageId, actor.UserId, request
        }, JsonOptions));
        var replay = await idempotency.FindAsync(actor.TenantId, key, operation,
            requestHash, cancellationToken);
        if (replay is not null)
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var locked = await db.Database.ExecuteSqlInterpolatedAsync($"""
            update collaboration.messages set revision = revision
            where tenant_id = {actor.TenantId} and project_id = {projectId} and id = {messageId}
            """, cancellationToken);
        if (locked != 1) return Results.NotFound();
        var message = await db.Messages.AsNoTracking().SingleAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.Id == messageId, cancellationToken);
        if (message.Revision != request.BaseRevision)
            return Results.Conflict(new { code = "collaboration.message.revision.conflict",
                currentRevision = message.Revision });
        if (message.DeletedAt.HasValue || message.RedactedAt.HasValue)
            return Results.Conflict(new { code = "collaboration.message.tombstone" });
        var existing = await db.Conversions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.DestinationType == request.DestinationType &&
            item.DestinationId == request.DestinationId, cancellationToken);
        if (existing is not null)
            return existing.RequestHash == requestHash && existing.MessageId == messageId
                ? Results.Ok(ToConversionResponse(existing))
                : Results.Conflict(new { code = "collaboration.conversion.destination_id.reused" });

        var ids = request.DocumentIds ?? [];
        var attached = await db.Attachments.AsNoTracking().Where(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.MessageId == messageId && ids.Contains(item.DocumentId))
            .ToArrayAsync(cancellationToken);
        if (attached.Length != ids.Length) return Results.NotFound();
        var released = await documents.FindReleasedAsync(actor.TenantId, ids, cancellationToken);
        if (released.Count != ids.Length || released.Any(document =>
            document.ProjectId != projectId || document.OwnerType != DocumentOwnerType.ProjectChat ||
            document.OwnerId != messageId || !attached.Any(item =>
                item.DocumentId == document.Id && item.DocumentSha256 == document.Sha256 &&
                item.DocumentVersion == document.VersionNumber)))
            return Results.NotFound();
        var references = released.OrderBy(item => item.Id).Select(item =>
            new ProjectMessageDocumentReference(item.Id, item.Sha256,
                item.VersionNumber, item.OriginalFileName,
                item.ContentType, item.SizeBytes)).ToArray();
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.convert", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var now = clock.UtcNow;
        ProjectMessageConversionResult result;
        try
        {
            result = await destination.ExecuteAsync(new ProjectMessageConversionCommand(
                actor.TenantId, projectId, actor.UserId, messageId, message.Revision,
                message.Body, request.DestinationType!, request.DestinationId,
                request.Details, references, now, http.TraceIdentifier,
                db.Database.GetDbConnection(), transaction.GetDbTransaction()), cancellationToken);
        }
        catch (ProjectMessageConversionException exception)
        {
            return Results.Json(new { code = exception.Code }, statusCode: exception.StatusCode);
        }
        if (result.DestinationId != request.DestinationId ||
            result.DestinationType != request.DestinationType)
            throw new InvalidOperationException("Destination command returned a different official identity.");
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.convert", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var lineage = ProjectMessageConversion.Create(message, result, references,
            requestHash, actor.UserId, now);
        db.Conversions.Add(lineage);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToConversionResponse(lineage);
        await effects.WriteAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, projectId, actor.UserId,
                    "ProjectMessageConverted", "ProjectMessage", messageId.ToString(), now,
                    new Dictionary<string, object?>
                    {
                        ["messageRevision"] = message.Revision,
                        ["destinationType"] = result.DestinationType,
                        ["destinationId"] = result.DestinationId,
                        ["documentIds"] = references.Select(item => item.Id).ToArray(),
                        ["documentHashes"] = references.Select(item => item.Sha256).ToArray()
                    }, http.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, projectId,
                    "collaboration.message.converted", 1, now,
                    JsonSerializer.Serialize(new { messageId, messageRevision = message.Revision,
                        result.DestinationType, result.DestinationId }, JsonOptions),
                    http.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, key, operation, requestHash,
                    StatusCodes.Status201Created, JsonSerializer.Serialize(response, JsonOptions),
                    now, now.AddDays(7))), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/collaboration/messages/{messageId}/conversions",
            response);
    }

    private static async Task<IResult> ListConversionsAsync(Guid projectId, Guid messageId,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.convert", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (!await db.Messages.AsNoTracking().AnyAsync(item => item.TenantId == actor.TenantId &&
            item.ProjectId == projectId && item.Id == messageId, cancellationToken))
            return Results.NotFound();
        var rows = await db.Conversions.AsNoTracking().Where(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.MessageId == messageId).OrderBy(item => item.ConfirmedAt)
            .Take(100).ToArrayAsync(cancellationToken);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.convert", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.Ok(rows.Select(ToConversionResponse).ToArray());
    }

    private static ProjectMessageConversionResponse ToConversionResponse(ProjectMessageConversion item) =>
        new(item.Id, item.MessageId, item.MessageRevision, item.DestinationType,
            item.DestinationId, item.DestinationReference,
            JsonSerializer.Deserialize<ProjectMessageDocumentReference[]>(item.DocumentReferencesJson) ?? [],
            item.ConfirmedBy, item.ConfirmedAt);
}

internal sealed record ConvertProjectMessageRequest(Guid DestinationId,
    string? DestinationType, long BaseRevision, bool Confirmed,
    JsonElement Details, Guid[]? DocumentIds);

internal sealed record ProjectMessageConversionResponse(Guid Id, Guid MessageId,
    long MessageRevision, string DestinationType, Guid DestinationId,
    string DestinationReference, IReadOnlyList<ProjectMessageDocumentReference> Documents,
    Guid ConfirmedBy, DateTimeOffset ConfirmedAt);
