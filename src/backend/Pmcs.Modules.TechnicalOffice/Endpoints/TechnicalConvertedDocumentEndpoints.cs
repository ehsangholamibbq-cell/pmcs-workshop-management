using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Endpoints;

internal static partial class TechnicalOfficeEndpoints
{
    private static async Task<IResult> ReadConvertedRevisionContentAsync(Guid projectId,
        Guid revisionId, HttpContext http, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectCollaborationMembership membership,
        ISharedDocumentDirectory documents, TechnicalOfficeDbContext db,
        IAuditTrail audit, IClock clock, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await AllowedAsync(actor.TenantId, actor.UserId, projectId,
                permissions, membership, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var revision = await db.DocumentRevisions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.Id == revisionId, cancellationToken);
        if (revision is null || !await db.Documents.AsNoTracking().AnyAsync(item =>
                item.Id == revision.DocumentId && item.TenantId == actor.TenantId &&
                item.ProjectId == projectId, cancellationToken))
            return Results.NotFound();
        if (!TryParseChatDocumentReference(revision.FileReference,
                out var messageId, out var documentId, out var version))
            return Results.NotFound();
        var content = await documents.ReadReleasedAsync(actor.TenantId, documentId,
            DocumentOwnerType.ProjectChat, messageId, cancellationToken);
        if (content is null || content.Document.ProjectId != projectId ||
            content.Document.VersionNumber != version ||
            !string.Equals(content.Document.Sha256, revision.Sha256, StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();
        if (content.Document.Classification == DocumentClassification.Restricted &&
            !await permissions.HasTenantPermissionAsync(actor.TenantId, actor.UserId,
                "documents.read", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await AllowedAsync(actor.TenantId, actor.UserId, projectId,
                permissions, membership, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        await audit.WriteAsync(new AuditEntry(actor.TenantId, projectId, actor.UserId,
            "TechnicalConvertedRevisionDownloaded", "TechnicalDocumentRevision",
            revisionId.ToString(), clock.UtcNow,
            new Dictionary<string, object?>
            {
                ["documentId"] = documentId,
                ["sha256"] = revision.Sha256,
                ["version"] = version
            }, http.TraceIdentifier), cancellationToken);
        http.Response.Headers.CacheControl = "no-store";
        return Results.File(content.Bytes, content.Document.ContentType,
            content.Document.OriginalFileName);
    }

    private static async Task<bool> AllowedAsync(Guid tenantId, Guid userId,
        Guid projectId, IProjectPermissionService permissions,
        IProjectCollaborationMembership membership, CancellationToken cancellationToken) =>
        await membership.IsActiveAsync(tenantId, projectId, userId, cancellationToken) &&
        await permissions.HasProjectPermissionAsync(tenantId, userId, projectId,
            "technical.read", cancellationToken) &&
        await permissions.HasProjectPermissionAsync(tenantId, userId, projectId,
            "documents.read", cancellationToken);

    private static bool TryParseChatDocumentReference(string value,
        out Guid messageId, out Guid documentId, out int version)
    {
        messageId = Guid.Empty;
        documentId = Guid.Empty;
        version = 0;
        var parts = value.Split(':');
        return parts.Length == 5 && parts[0] == "pmcs" &&
            parts[1] == "chat-document" &&
            Guid.TryParseExact(parts[2], "N", out messageId) &&
            Guid.TryParseExact(parts[3], "N", out documentId) &&
            parts[4].StartsWith('v') && int.TryParse(parts[4].AsSpan(1), out version) &&
            version > 0;
    }
}
