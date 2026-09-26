using Pmcs.Modules.Documents.Domain;

namespace Pmcs.Modules.Documents.Contracts;

public sealed record GeneratedDocumentPublishRequest(
    Guid DocumentId,
    Guid TenantId,
    Guid ProjectId,
    Guid OwnerId,
    string FileName,
    string ContentType,
    byte[] Bytes,
    string Sha256,
    DocumentClassification Classification,
    DocumentRetentionPolicy RetentionPolicy,
    DateTimeOffset? RetainUntil,
    bool LegalHold,
    DateTimeOffset CreatedAt,
    string CorrelationId);

public sealed record GeneratedDocumentReference(
    Guid DocumentId,
    Guid TenantId,
    Guid ProjectId,
    Guid OwnerId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DocumentClassification Classification,
    DocumentRetentionPolicy RetentionPolicy,
    DateTimeOffset ReleasedAt,
    long Revision);

/// <summary>
/// Narrow, server-side publication boundary for immutable report artifacts. This contract never
/// exposes an object key or a generic upload session to the calling module.
/// </summary>
public interface IGeneratedDocumentPublisher
{
    Task<GeneratedDocumentReference> PublishReportOutputAsync(
        GeneratedDocumentPublishRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class GeneratedDocumentPublishException(
    string code,
    bool transient,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;

    public bool Transient { get; } = transient;
}
