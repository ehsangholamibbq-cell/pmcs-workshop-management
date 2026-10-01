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
    private static async Task<IResult> GetAttachmentUploadAsync(Guid projectId, Guid messageId,
        Guid documentId, HttpContext http, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IProjectCollaborationMembership membership,
        IProjectPermissionService permissions, IProjectDirectory projects,
        ISharedDocumentDirectory documents, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.upload", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        var authored = await db.Messages.AsNoTracking().AnyAsync(item =>
            item.Id == messageId && item.TenantId == actor.TenantId &&
            item.ProjectId == projectId && item.AuthorUserId == actor.UserId &&
            item.DeletedAt == null && item.RedactedAt == null,
            cancellationToken);
        if (!authored) return Results.NotFound();
        var upload = await documents.FindProjectChatUploadAsync(actor.TenantId,
            projectId, messageId, documentId, actor.UserId, cancellationToken);
        if (upload is null) return Results.NotFound();
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.upload", cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "documents.upload", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await db.Messages.AsNoTracking().AnyAsync(item =>
            item.Id == messageId && item.TenantId == actor.TenantId &&
            item.ProjectId == projectId && item.AuthorUserId == actor.UserId &&
            item.DeletedAt == null && item.RedactedAt == null, cancellationToken))
            return Results.NotFound();
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(upload);
    }

    private static async Task<IResult> AttachDocumentAsync(Guid projectId, Guid messageId,
        Guid documentId, HttpContext http, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IProjectCollaborationMembership membership,
        IProjectPermissionService permissions, IProjectDirectory projects,
        ISharedDocumentDirectory documents, CollaborationDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.upload", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        var message = await db.Messages.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == messageId && item.TenantId == actor.TenantId &&
            item.ProjectId == projectId && item.AuthorUserId == actor.UserId &&
            item.DeletedAt == null && item.RedactedAt == null,
            cancellationToken);
        if (message is null) return Results.NotFound();
        var released = (await documents.FindReleasedAsync(actor.TenantId, [documentId],
            cancellationToken)).SingleOrDefault();
        if (released is null || released.ProjectId != projectId ||
            released.OwnerType != DocumentOwnerType.ProjectChat || released.OwnerId != messageId)
            return Results.NotFound(new { code = "collaboration.attachment.not_released" });
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.upload", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var attached = ProjectMessageAttachment.Create(actor.TenantId, projectId,
            messageId, documentId, released.Sha256, released.VersionNumber,
            actor.UserId, clock.UtcNow);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize attachment association with edit/delete/moderation of the owner.
        var locked = await db.Database.ExecuteSqlInterpolatedAsync($"""
            update collaboration.messages set revision = revision
            where tenant_id = {actor.TenantId} and project_id = {projectId} and
                id = {messageId} and author_user_id = {actor.UserId} and
                deleted_at is null and redacted_at is null
            """, cancellationToken);
        if (locked != 1) return Results.NotFound();
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into collaboration.message_attachments(id, tenant_id, project_id,
                message_id, document_id, document_sha256, document_version,
                attached_by, attached_at)
            values ({attached.Id}, {actor.TenantId}, {projectId}, {messageId},
                {documentId}, {attached.DocumentSha256}, {attached.DocumentVersion},
                {actor.UserId}, {attached.AttachedAt})
            on conflict (tenant_id, project_id, document_id) do nothing
            """, cancellationToken);
        if (inserted == 0 && !await db.Attachments.AsNoTracking().AnyAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.MessageId == messageId && item.DocumentId == documentId,
            cancellationToken))
            return Results.Conflict(new { code = "collaboration.attachment.already_owned" });
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.upload", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (inserted == 1)
            await sideEffects.WriteEventAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
                new TransactionalEventBatch(
                    new AuditEntry(actor.TenantId, projectId, actor.UserId,
                        "ProjectMessageDocumentAttached", "ProjectMessage", messageId.ToString(),
                        attached.AttachedAt, new Dictionary<string, object?>
                        {
                            ["documentId"] = documentId, ["sha256"] = released.Sha256,
                            ["documentVersion"] = released.VersionNumber
                        }, http.TraceIdentifier),
                    new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, projectId,
                        "collaboration.message.document-attached", 1, attached.AttachedAt,
                        JsonSerializer.Serialize(new { projectId, messageId, documentId }, JsonOptions),
                        http.TraceIdentifier)), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(ToAttachmentResponse(messageId, released));
    }

    private static async Task<IResult> ListAttachmentsAsync(Guid projectId, Guid messageId,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, ISharedDocumentDirectory documents,
        CollaborationDbContext db, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (!await MessageExistsAsync(db, actor, projectId, messageId, cancellationToken))
            return Results.NotFound();
        var attached = await db.Attachments.AsNoTracking().Where(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.MessageId == messageId).OrderBy(item => item.AttachedAt).Take(50)
            .ToArrayAsync(cancellationToken);
        var released = await documents.FindReleasedAsync(actor.TenantId,
            attached.Select(item => item.DocumentId).ToArray(), cancellationToken);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.read", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.Ok(released.Where(document =>
            document.ProjectId == projectId && document.OwnerType == DocumentOwnerType.ProjectChat &&
            document.OwnerId == messageId && attached.Any(item =>
                item.DocumentId == document.Id && item.DocumentSha256 == document.Sha256 &&
                item.DocumentVersion == document.VersionNumber))
            .Select(document => ToAttachmentResponse(messageId, document)).ToArray());
    }

    private static async Task<IResult> DownloadAttachmentAsync(Guid projectId,
        Guid messageId, Guid documentId, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, ISharedDocumentDirectory documents,
        CollaborationDbContext db, IAuditTrail audit, IClock clock,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (!await MessageExistsAsync(db, actor, projectId, messageId, cancellationToken))
            return Results.NotFound();
        var attachment = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.MessageId == messageId && item.DocumentId == documentId,
            cancellationToken);
        if (attachment is null) return Results.NotFound();
        var content = await documents.ReadReleasedAsync(actor.TenantId, documentId,
            DocumentOwnerType.ProjectChat, messageId, cancellationToken);
        if (content is null || content.Document.ProjectId != projectId ||
            content.Document.Sha256 != attachment.DocumentSha256 ||
            content.Document.VersionNumber != attachment.DocumentVersion)
            return Results.NotFound();
        var allowed = content.Document.Classification == DocumentClassification.Restricted
            ? await permissions.HasTenantPermissionAsync(actor.TenantId, actor.UserId,
                "documents.read", cancellationToken)
            : await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "documents.read", cancellationToken);
        if (!allowed || !await membership.IsActiveAsync(actor.TenantId, projectId,
                actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.read", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await MessageExistsAsync(db, actor, projectId, messageId, cancellationToken))
            return Results.NotFound();
        await audit.WriteAsync(new AuditEntry(actor.TenantId, projectId, actor.UserId,
            "ProjectMessageDocumentDownloaded", "ProjectMessage", messageId.ToString(),
            clock.UtcNow, new Dictionary<string, object?>
            {
                ["documentId"] = documentId, ["sha256"] = attachment.DocumentSha256
            }, http.TraceIdentifier), cancellationToken);
        http.Response.Headers.CacheControl = "no-store";
        return Results.File(content.Bytes, content.Document.ContentType,
            content.Document.OriginalFileName);
    }

    private static ProjectAttachmentResponse ToAttachmentResponse(Guid messageId,
        ReleasedDocumentReference document) => new(messageId, document.Id,
            document.OriginalFileName, document.ContentType, document.SizeBytes,
            document.Sha256, document.Classification, document.RetentionPolicy,
            document.LegalHold, document.ReleasedAt, document.VersionNumber,
            $"/api/v1/projects/{document.ProjectId}/collaboration/messages/{messageId}/attachments/{document.Id}/content");
}

internal sealed record ProjectAttachmentResponse(Guid MessageId, Guid DocumentId,
    string OriginalFileName, string ContentType, long SizeBytes, string Sha256,
    DocumentClassification Classification, DocumentRetentionPolicy RetentionPolicy,
    bool LegalHold, DateTimeOffset ReleasedAt, int VersionNumber, string ContentUrl);
