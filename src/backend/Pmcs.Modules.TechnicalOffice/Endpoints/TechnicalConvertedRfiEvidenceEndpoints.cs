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
    private static async Task<IResult> ReadConvertedRfiEvidenceAsync(Guid projectId,
        Guid rfiId, Guid documentId, HttpContext http, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectCollaborationMembership membership,
        ISharedDocumentDirectory documents, TechnicalOfficeDbContext db,
        IAuditTrail audit, IClock clock, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await AllowedAsync(actor.TenantId, actor.UserId, projectId,
                permissions, membership, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var rfi = await db.Rfis.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.Id == rfiId, cancellationToken);
        if (rfi is null) return Results.NotFound();
        var references = rfi.EvidenceReferences.Select(TryReadEvidence).Where(item =>
            item.HasValue && item.Value.DocumentId == documentId).ToArray();
        if (references.Length != 1) return Results.NotFound();
        var reference = references[0]!.Value;
        var content = await documents.ReadReleasedAsync(actor.TenantId, documentId,
            DocumentOwnerType.ProjectChat, reference.MessageId, cancellationToken);
        if (content is null || content.Document.ProjectId != projectId ||
            content.Document.VersionNumber != reference.Version ||
            !string.Equals(content.Document.Sha256, reference.Sha256,
                StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();
        if (content.Document.Classification == DocumentClassification.Restricted &&
            !await permissions.HasTenantPermissionAsync(actor.TenantId, actor.UserId,
                "documents.read", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await AllowedAsync(actor.TenantId, actor.UserId, projectId,
                permissions, membership, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        await audit.WriteAsync(new AuditEntry(actor.TenantId, projectId, actor.UserId,
            "TechnicalConvertedRfiEvidenceDownloaded", "TechnicalRfi", rfiId.ToString(),
            clock.UtcNow, new Dictionary<string, object?>
            {
                ["documentId"] = documentId,
                ["sha256"] = reference.Sha256,
                ["version"] = reference.Version
            }, http.TraceIdentifier), cancellationToken);
        http.Response.Headers.CacheControl = "no-store";
        return Results.File(content.Bytes, content.Document.ContentType,
            content.Document.OriginalFileName);
    }

    private static (Guid MessageId, Guid DocumentId, int Version, string Sha256)? TryReadEvidence(
        string evidence)
    {
        var parts = evidence.Split(':');
        if (parts.Length != 7 || parts[0] != "pmcs" || parts[1] != "chat-document" ||
            parts[5] != "sha256" || !Guid.TryParseExact(parts[2], "N", out var messageId) ||
            !Guid.TryParseExact(parts[3], "N", out var documentId) ||
            !parts[4].StartsWith('v') || !int.TryParse(parts[4].AsSpan(1), out var version) ||
            version < 1 || parts[6].Length != 64 ||
            !parts[6].All(Uri.IsHexDigit))
            return null;
        return (messageId, documentId, version, parts[6]);
    }
}
