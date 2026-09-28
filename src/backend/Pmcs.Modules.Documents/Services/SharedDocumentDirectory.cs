using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Documents.Persistence;
using Pmcs.Modules.Documents.Storage;

namespace Pmcs.Modules.Documents.Services;

internal sealed class SharedDocumentDirectory(
    DocumentsDbContext dbContext,
    IDocumentObjectStorage objectStorage) : ISharedDocumentDirectory
{
    public Task<ProjectChatUploadReference?> FindProjectChatUploadAsync(
        Guid tenantId,
        Guid projectId,
        Guid messageId,
        Guid documentId,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || messageId == Guid.Empty ||
            documentId == Guid.Empty || createdBy == Guid.Empty)
            return Task.FromResult<ProjectChatUploadReference?>(null);

        return dbContext.Assets.AsNoTracking()
            .Where(asset => asset.TenantId == tenantId && asset.ProjectId == projectId &&
                asset.OwnerType == DocumentOwnerType.ProjectChat && asset.OwnerId == messageId &&
                asset.Id == documentId && asset.CreatedBy == createdBy &&
                asset.Status != DocumentAssetStatus.Deleted)
            .Select(asset => new ProjectChatUploadReference(
                asset.Id, asset.OwnerId, asset.OriginalFileName, asset.ContentType,
                asset.SizeBytes, asset.Sha256, asset.Status, asset.VersionNumber,
                asset.ReleasedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReleasedDocumentReference>> FindReleasedAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || documentIds.Count == 0)
        {
            return [];
        }

        var distinctIds = documentIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return [];
        }

        return await dbContext.Assets.AsNoTracking()
            .Where(asset => asset.TenantId == tenantId &&
                distinctIds.Contains(asset.Id) &&
                asset.Status == DocumentAssetStatus.Released &&
                asset.ReleasedAt.HasValue)
            .OrderBy(asset => asset.Id)
            .Select(asset => new ReleasedDocumentReference(
                asset.Id,
                asset.TenantId,
                asset.ProjectId,
                asset.OwnerType,
                asset.OwnerId,
                asset.VersionNumber,
                asset.OriginalFileName,
                asset.ContentType,
                asset.SizeBytes,
                asset.Sha256,
                asset.Classification,
                asset.RetentionPolicy,
                asset.LegalHold,
                asset.ReleasedAt!.Value,
                asset.Revision))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleasedDocumentContent?> ReadReleasedAsync(
        Guid tenantId,
        Guid documentId,
        DocumentOwnerType expectedOwnerType,
        Guid expectedOwnerId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || documentId == Guid.Empty || expectedOwnerId == Guid.Empty ||
            !Enum.IsDefined(expectedOwnerType))
        {
            return null;
        }

        var asset = await dbContext.Assets.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.TenantId == tenantId &&
            candidate.Id == documentId &&
            candidate.OwnerType == expectedOwnerType &&
            candidate.OwnerId == expectedOwnerId &&
            candidate.Status == DocumentAssetStatus.Released &&
            candidate.ReleasedAt.HasValue,
            cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var content = await objectStorage.ReadAsync(asset.ObjectKey, cancellationToken);
        if (content is null)
        {
            return null;
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(content.Bytes)).ToLowerInvariant();
        if (content.Bytes.LongLength != asset.SizeBytes ||
            !string.Equals(actualHash, asset.Sha256, StringComparison.Ordinal) ||
            !string.Equals(content.ContentType, asset.ContentType, StringComparison.OrdinalIgnoreCase) ||
            !DocumentContentPolicy.MatchesSignature(asset.ContentType, content.Bytes))
        {
            throw new InvalidOperationException("Released document content failed integrity verification.");
        }

        var reference = new ReleasedDocumentReference(
            asset.Id,
            asset.TenantId,
            asset.ProjectId,
            asset.OwnerType,
            asset.OwnerId,
            asset.VersionNumber,
            asset.OriginalFileName,
            asset.ContentType,
            asset.SizeBytes,
            asset.Sha256,
            asset.Classification,
            asset.RetentionPolicy,
            asset.LegalHold,
            asset.ReleasedAt!.Value,
            asset.Revision);
        return new ReleasedDocumentContent(reference, content.Bytes);
    }
}
