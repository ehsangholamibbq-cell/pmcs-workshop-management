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
    DocumentRetentionPolicy RetentionPolicy,
    bool LegalHold,
    DateTimeOffset ReleasedAt,
    long Revision);

public sealed record ReleasedDocumentContent(
    ReleasedDocumentReference Document,
    byte[] Bytes);

public sealed record ProjectChatUploadReference(
    Guid Id,
    Guid MessageId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DocumentAssetStatus Status,
    int VersionNumber,
    DateTimeOffset? ReleasedAt);

public interface ISharedDocumentDirectory
{
    Task<ProjectChatUploadReference?> FindProjectChatUploadAsync(
        Guid tenantId,
        Guid projectId,
        Guid messageId,
        Guid documentId,
        Guid createdBy,
        CancellationToken cancellationToken = default);

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
