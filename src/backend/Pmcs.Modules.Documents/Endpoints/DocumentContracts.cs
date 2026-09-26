using Pmcs.Modules.Documents.Domain;

namespace Pmcs.Modules.Documents.Endpoints;

public sealed record CreateDocumentUploadSessionRequest(
    Guid ClientGeneratedId,
    Guid? ProjectId,
    DocumentOwnerType OwnerType,
    Guid OwnerId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DocumentClassification Classification,
    DocumentRetentionPolicy RetentionPolicy,
    DateTimeOffset? RetainUntil,
    bool LegalHold);

public sealed record ReleaseDocumentRequest(long BaseRevision);

public sealed record UpdateDocumentGovernanceRequest(
    long BaseRevision,
    DocumentClassification Classification,
    DocumentRetentionPolicy RetentionPolicy,
    DateTimeOffset? RetainUntil,
    bool LegalHold);

public sealed record DocumentUploadSessionResponse(
    DocumentAssetResponse Document,
    string? UploadMethod,
    string? UploadUrl,
    DateTimeOffset? ExpiresAt);

public sealed record DocumentAssetResponse(
    Guid Id,
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
    DateTimeOffset? RetainUntil,
    bool LegalHold,
    DocumentAssetStatus Status,
    DocumentScanVerdict ScanVerdict,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UploadedAt,
    DateTimeOffset? ReleasedAt,
    long Revision,
    string? ContentUrl)
{
    public static DocumentAssetResponse From(DocumentAsset asset) => new(
        asset.Id,
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
        asset.RetainUntil,
        asset.LegalHold,
        asset.Status,
        asset.ScanVerdict,
        asset.CreatedAt,
        asset.UploadedAt,
        asset.ReleasedAt,
        asset.Revision,
        asset.Status == DocumentAssetStatus.Released
            ? $"/api/v1/documents/{asset.Id}/content"
            : null);
}
