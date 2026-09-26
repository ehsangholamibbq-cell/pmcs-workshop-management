using Pmcs.Modules.Documents.Domain;

namespace Pmcs.Modules.Documents.Contracts;

public sealed record GeneratedDocumentOrphanCandidate(
    Guid DocumentId,
    Guid TenantId,
    Guid ProjectId,
    Guid OwnerId,
    DocumentRetentionPolicy? RetentionPolicy,
    DateTimeOffset? RetainUntil,
    bool LegalHold,
    DateTimeOffset ReleasedAt,
    long Revision);

public sealed record GeneratedDocumentOrphanRemediationRequest(
    Guid DocumentId,
    Guid TenantId,
    Guid ProjectId,
    Guid OwnerId,
    Guid RunId,
    long ExpectedRevision,
    DateTimeOffset EvaluatedAt,
    string CorrelationId);

public enum GeneratedDocumentOrphanRemediationResult
{
    Remediated = 1,
    NotFound = 2,
    Changed = 3,
    InvalidState = 4,
    RetentionActive = 5,
    LegalHold = 6
}

/// <summary>
/// Narrow operational boundary for generated-report inventory and governed deletion. Candidate
/// metadata never exposes an object key. The calling owner must prove that its output record is
/// absent and serialize recovery before requesting remediation; this boundary rechecks document
/// identity, revision, retention and legal hold immediately before deletion.
/// </summary>
public interface IGeneratedDocumentOrphanRemediator
{
    Task<IReadOnlyList<GeneratedDocumentOrphanCandidate>> ListReportOutputCandidatesAsync(
        DateTimeOffset releasedBefore,
        Guid? afterDocumentId,
        int take,
        CancellationToken cancellationToken = default);

    Task<GeneratedDocumentOrphanRemediationResult> RemediateReportOutputAsync(
        GeneratedDocumentOrphanRemediationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class GeneratedDocumentOrphanRemediationException(
    string code,
    bool transient,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;

    public bool Transient { get; } = transient;
}
