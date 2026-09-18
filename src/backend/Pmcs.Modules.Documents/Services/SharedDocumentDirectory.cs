using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Documents.Persistence;

namespace Pmcs.Modules.Documents.Services;

internal sealed class SharedDocumentDirectory(DocumentsDbContext dbContext) : ISharedDocumentDirectory
{
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
                asset.ReleasedAt!.Value,
                asset.Revision))
            .ToListAsync(cancellationToken);
    }
}
