using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Collaboration.Domain;
using Pmcs.Modules.Collaboration.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Collaboration.Endpoints;

internal static partial class CollaborationEndpoints
{
    private static Task<IResult> EditMessageAsync(Guid projectId, Guid messageId,
        ProjectMessageMutationRequest request, HttpContext http, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IProjectCollaborationMembership membership,
        IProjectPermissionService permissions, IProjectDirectory projects,
        CollaborationDbContext db, IClock clock, IIdempotencyStore idempotency,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken) =>
        MutateMessageAsync(projectId, messageId, "Edited", request, http, runtime,
            actor, membership, permissions, projects, db, clock, idempotency,
            sideEffects, cancellationToken);

    private static Task<IResult> DeleteMessageAsync(Guid projectId, Guid messageId,
        ProjectMessageMutationRequest request, HttpContext http, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IProjectCollaborationMembership membership,
        IProjectPermissionService permissions, IProjectDirectory projects,
        CollaborationDbContext db, IClock clock, IIdempotencyStore idempotency,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken) =>
        MutateMessageAsync(projectId, messageId, "Deleted", request, http, runtime,
            actor, membership, permissions, projects, db, clock, idempotency,
            sideEffects, cancellationToken);

    private static Task<IResult> RedactMessageAsync(Guid projectId, Guid messageId,
        ProjectMessageMutationRequest request, HttpContext http, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IProjectCollaborationMembership membership,
        IProjectPermissionService permissions, IProjectDirectory projects,
        CollaborationDbContext db, IClock clock, IIdempotencyStore idempotency,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken) =>
        MutateMessageAsync(projectId, messageId, "Redacted", request, http, runtime,
            actor, membership, permissions, projects, db, clock, idempotency,
            sideEffects, cancellationToken);

    private static Task<IResult> SetLegalHoldAsync(Guid projectId, Guid messageId,
        ProjectMessageMutationRequest request, HttpContext http, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IProjectCollaborationMembership membership,
        IProjectPermissionService permissions, IProjectDirectory projects,
        CollaborationDbContext db, IClock clock, IIdempotencyStore idempotency,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken) =>
        MutateMessageAsync(projectId, messageId, "LegalHold", request, http, runtime,
            actor, membership, permissions, projects, db, clock, idempotency,
            sideEffects, cancellationToken);

    private static async Task<IResult> MutateMessageAsync(Guid projectId, Guid messageId,
        string action, ProjectMessageMutationRequest request, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        IIdempotencyStore idempotency, ITransactionalSideEffectWriter sideEffects,
        CancellationToken cancellationToken)
    {
        var permission = action is "Edited" or "Deleted"
            ? "collaboration.edit-own" : "collaboration.moderate";
        var gate = await GateAsync(projectId, permission, runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (request.BaseRevision < 1 || action == "LegalHold" && !request.Enabled.HasValue)
            return Results.BadRequest(new { code = "collaboration.message.mutation.invalid" });
        if ((action is "Redacted" or "LegalHold") &&
            (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500 ||
             request.Reason!.Any(character => char.IsControl(character) && character != '\n')))
            return Results.BadRequest(new { code = "collaboration.moderation.reason.invalid" });
        var key = http.Request.Headers["Idempotency-Key"].ToString().Trim();
        IdempotencyKeyRules.Validate(key);
        var operation = $"collaboration.message.{action}:{projectId:N}:{actor.UserId:N}";
        var requestHash = RequestHash.Create(JsonSerializer.Serialize(new
        {
            actor.TenantId, projectId, messageId, actor.UserId, action, request
        }, JsonOptions));
        var replay = await idempotency.FindAsync(actor.TenantId, key,
            operation, requestHash, cancellationToken);
        if (replay is not null)
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // A no-op row update obtains the PostgreSQL row lock before the revision check.
        var locked = await db.Database.ExecuteSqlInterpolatedAsync($"""
            update collaboration.messages set revision = revision
            where tenant_id = {actor.TenantId} and project_id = {projectId} and id = {messageId}
            """, cancellationToken);
        if (locked != 1) return Results.NotFound();
        var message = await db.Messages.SingleAsync(item => item.Id == messageId &&
            item.TenantId == actor.TenantId && item.ProjectId == projectId,
            cancellationToken);
        if (permission == "collaboration.edit-own" && message.AuthorUserId != actor.UserId)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, permission, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (message.Revision != request.BaseRevision)
            return Results.Conflict(new { code = "collaboration.message.revision.conflict",
                currentRevision = message.Revision });
        if (action == "Deleted" && message.LegalHold)
            return Results.Conflict(new { code = "collaboration.message.legal_hold" });
        if ((message.DeletedAt.HasValue || message.RedactedAt.HasValue) && action != "LegalHold")
            return Results.Conflict(new { code = "collaboration.message.tombstone" });

        var now = clock.UtcNow;
        ProjectMessageRevision? history = null;
        ProjectModerationRecord? moderation = null;
        switch (action)
        {
            case "Edited":
                history = message.Edit(request.BaseRevision, request.Body, actor.UserId, now);
                break;
            case "Deleted":
                history = message.Tombstone(request.BaseRevision, actor.UserId, now);
                break;
            case "Redacted":
                history = message.Redact(request.BaseRevision, actor.UserId, now);
                moderation = ProjectModerationRecord.Create(message, action,
                    request.Reason, actor.UserId, now);
                break;
            case "LegalHold":
                message.SetLegalHold(request.BaseRevision, request.Enabled!.Value, actor.UserId);
                moderation = ProjectModerationRecord.Create(message,
                    request.Enabled.Value ? "HoldApplied" : "HoldRemoved",
                    request.Reason, actor.UserId, now);
                break;
        }
        if (history is not null) db.Revisions.Add(history);
        if (moderation is not null) db.ModerationRecords.Add(moderation);
        await db.SaveChangesAsync(cancellationToken);
        var response = ProjectMessageResponse.From(message);
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);
        await sideEffects.WriteAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, projectId, actor.UserId,
                    $"ProjectMessage{action}", "ProjectMessage", messageId.ToString(), now,
                    new Dictionary<string, object?>
                    {
                        ["revision"] = message.Revision,
                        ["legalHold"] = message.LegalHold,
                        ["reason"] = moderation?.Reason
                    }, http.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, projectId,
                    $"collaboration.message.{action.ToLowerInvariant()}", 1, now,
                    JsonSerializer.Serialize(new { messageId, projectId,
                        revision = message.Revision, action }, JsonOptions),
                    http.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, key, operation, requestHash,
                    StatusCodes.Status200OK, responseJson, now, now.AddDays(7))), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetMessageHistoryAsync(Guid projectId,
        Guid messageId, CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        var message = await db.Messages.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == messageId && item.TenantId == actor.TenantId &&
            item.ProjectId == projectId, cancellationToken);
        if (message is null) return Results.NotFound();
        var moderator = await permissions.HasProjectPermissionAsync(actor.TenantId,
            actor.UserId, projectId, "collaboration.moderate", cancellationToken);
        if (!moderator && message.AuthorUserId != actor.UserId)
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var revisions = await db.Revisions.AsNoTracking().Where(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.MessageId == messageId).OrderBy(item => item.FromRevision)
            .ToArrayAsync(cancellationToken);
        ProjectModerationRecord[] moderation = moderator ? await db.ModerationRecords.AsNoTracking().Where(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.MessageId == messageId).OrderBy(item => item.OccurredAt)
            .ToArrayAsync(cancellationToken) : [];
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.read", cancellationToken) ||
            moderator && !await permissions.HasProjectPermissionAsync(actor.TenantId,
                actor.UserId, projectId, "collaboration.moderate", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.Ok(new { messageId, currentRevision = message.Revision,
            revisions = revisions.Select(item => new { item.FromRevision,
                item.Body, item.Action, item.ActorUserId, item.OccurredAt }),
            moderation = moderation.Select(item => new { item.MessageRevision,
                item.Action, item.Reason, item.ActorUserId, item.OccurredAt }) });
    }
}

internal sealed record ProjectMessageMutationRequest(long BaseRevision, string? Body = null,
    string? Reason = null, bool? Enabled = null);
