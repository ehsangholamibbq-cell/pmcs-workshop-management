using Pmcs.Modules.Documents.Domain;

namespace Pmcs.Modules.Documents.Contracts;

public sealed record ReleasedDocumentReference(
    Guid Id,
    Guid TenantId,
    Guid? ProjectId,
    DocumentOwnerType OwnerType,
    Guid OwnerId,
    int VersionNumber,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DocumentClassification Classification,
    DateTimeOffset ReleasedAt,
    long Revision);

public sealed record ReleasedDocumentContent(
    ReleasedDocumentReference Document,
    byte[] Bytes);

public interface ISharedDocumentDirectory
{
    Task<IReadOnlyList<ReleasedDocumentReference>> FindReleasedAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default);

    Task<ReleasedDocumentContent?> ReadReleasedAsync(
        Guid tenantId,
        Guid documentId,
        DocumentOwnerType expectedOwnerType,
        Guid expectedOwnerId,
        CancellationToken cancellationToken = default);
}
