using Pmcs.Modules.TechnicalOffice.Domain;

namespace Pmcs.Modules.TechnicalOffice.Contracts;

public interface IProjectTechnicalOfficeReportingSource
{
    Task<ProjectTechnicalOfficeReportingResult> LoadAsync(
        Guid tenantId, Guid projectId, DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc, CancellationToken cancellationToken = default);
}

public static class ProjectTechnicalOfficeReportingContract
{
    public const string Version = "pmcs.technical-office.project-reporting/v1";
    public const string PolicyVersion = "pmcs.technical-office.reporting-policy/v1";
    public const string ManifestVersion = "pmcs.technical-office.reporting-manifest/v1";
    public const int MaximumDocuments = 20_000;
    public const int MaximumRevisions = 100_000;
    public const int MaximumTransmittals = 50_000;
    public const int MaximumRfis = 50_000;
    public const int MaximumSubmittals = 50_000;
    public const int MaximumEvents = 500_000;
    public const int MaximumLineageDepth = 100;
    public const int MaximumManifestBytes = 8_000_000;
    public const int MaximumReadSeconds = 30;
}

public enum TechnicalReportingClassification { Internal = 1, Confidential = 2, Restricted = 3 }
public enum TechnicalReportingCompleteness { Complete = 1, Incomplete = 2 }
public enum TechnicalReportingStatus { NotConfigured = 1, NoData = 2, InsufficientData = 3, Available = 4 }
public enum TechnicalReportingDueState { NotAssessable = 1, NotDue = 2, Overdue = 3, Acknowledged = 4 }
public enum TechnicalReportingSubmittalState
{
    Submitted = 1, UnderReview = 2, Approved = 3, ApprovedAsNoted = 4,
    ReviseAndResubmit = 5, Rejected = 6, ForInformation = 7, Closed = 8
}
public enum TechnicalReportingReason
{
    TechnicalSourceNotConfigured = 1, NoOfficialDocumentRevision = 2,
    NoIssuedTransmittal = 3, NoIssuedRfi = 4, NoSubmittedSubmittal = 5,
    HistoricalTransitionUnavailable = 6, SourceCoverageIncomplete = 7,
    OfficialRevisionLineageInvalid = 8, CrossProjectReference = 9,
    ClassificationUnknown = 10, DueDateUnavailable = 11,
    ReviewOutcomeUnavailable = 12
}

public enum TechnicalReportingEventType
{
    Submitted = 1, Approved = 2, Returned = 3, Issued = 4, Superseded = 5,
    Acknowledged = 6, InternalReview = 7, ReturnToDraft = 8,
    ResponseReceived = 9, ResponseAccepted = 10, ClarificationRequired = 11,
    Closed = 12, UnderReview = 13, Reviewed = 14
}

// These are already minimized application facts, never EF entities or endpoint DTOs.
// No file bytes/references, SHA of a file, recipient, respondent, question or review text crosses this boundary.
public sealed record TechnicalReportingEvent(
    long Sequence, TechnicalReportingEventType Type, DateTimeOffset AtUtc,
    RfiResponseClassification? ResponseClassification = null,
    bool ChangePotential = false,
    SubmittalReviewOutcome? ReviewOutcome = null);

public sealed record TechnicalReportingDocument(
    Guid Id, Guid TenantId, Guid ProjectId, string Number,
    TechnicalDocumentType Type, string Discipline, DateTimeOffset CreatedAtUtc,
    TechnicalReportingClassification Classification);

public sealed record TechnicalReportingRevision(
    Guid Id, Guid TenantId, Guid ProjectId, Guid DocumentId,
    string Code, DateOnly RevisionDate, DocumentRevisionPurpose Purpose,
    Guid? SupersedesRevisionId, Guid? IssuedThroughTransmittalId,
    DateTimeOffset CreatedAtUtc, IReadOnlyCollection<TechnicalReportingEvent> Events);

public sealed record TechnicalReportingTransmittal(
    Guid Id, Guid TenantId, Guid ProjectId, string Number,
    IReadOnlyCollection<Guid> RevisionIds, DateOnly? DueResponseDate,
    DateTimeOffset CreatedAtUtc, IReadOnlyCollection<TechnicalReportingEvent> Events);

public sealed record TechnicalReportingRfi(
    Guid Id, Guid TenantId, Guid ProjectId, string Number,
    DateOnly RaisedDate, DateOnly? RequiredByDate, bool IsBlocking,
    PotentialImpact PotentialImpact, IReadOnlyCollection<Guid> ReferencedRevisionIds,
    DateTimeOffset CreatedAtUtc, IReadOnlyCollection<TechnicalReportingEvent> Events);

public sealed record TechnicalReportingSubmittal(
    Guid Id, Guid TenantId, Guid ProjectId, string Number, TechnicalSubmittalType Type,
    string Discipline, DateOnly? ReviewDueDate, int ResubmissionNumber,
    Guid? SupersedesSubmittalId, IReadOnlyCollection<Guid> RevisionIds,
    DateTimeOffset CreatedAtUtc, IReadOnlyCollection<TechnicalReportingEvent> Events);

public sealed record ProjectTechnicalOfficeReportingProjection(
    string ContractVersion, Guid TenantId, Guid ProjectId,
    DateOnly CutoffLocalDate, DateTimeOffset SourceCutoffUtc,
    long ConfigurationVersion, DateTimeOffset ConfigurationEffectiveAtUtc, bool SourceEnabled,
    TechnicalReportingClassification Classification, bool RestrictedPublicationApproved,
    IReadOnlyCollection<TechnicalReportingDocument> Documents,
    IReadOnlyCollection<TechnicalReportingRevision> Revisions,
    IReadOnlyCollection<TechnicalReportingTransmittal> Transmittals,
    IReadOnlyCollection<TechnicalReportingRfi> Rfis,
    IReadOnlyCollection<TechnicalReportingSubmittal> Submittals,
    TechnicalReportingCompleteness DocumentCompleteness,
    TechnicalReportingCompleteness TransmittalCompleteness,
    TechnicalReportingCompleteness RfiCompleteness,
    TechnicalReportingCompleteness SubmittalCompleteness);

public sealed record TechnicalReportingDocumentRow(
    Guid DocumentId, string Number, TechnicalDocumentType Type, string Discipline,
    Guid RevisionId, string RevisionCode, DateOnly RevisionDate, DateTimeOffset IssuedAtUtc);
public sealed record TechnicalReportingTransmittalRow(
    Guid TransmittalId, string Number, DateTimeOffset IssuedAtUtc,
    int RevisionCount, DateTimeOffset? AcknowledgedAtUtc, TechnicalReportingDueState DueState);
public sealed record TechnicalReportingRfiRow(
    Guid RfiId, string Number, TechnicalReportingEventType State, DateTimeOffset IssuedAtUtc,
    int ResponseCount, RfiResponseClassification? LastResponseClassification,
    bool IsBlocking, bool? Overdue);
public sealed record TechnicalReportingSubmittalRow(
    Guid SubmittalId, string Number, TechnicalSubmittalType Type, string Discipline,
    TechnicalReportingSubmittalState State, SubmittalReviewOutcome? ReviewOutcome,
    int ResubmissionNumber, bool? ReviewOverdue);

public sealed record TechnicalReportingSection<T>(
    TechnicalReportingStatus Status, int? OfficialCount,
    IReadOnlyCollection<T> Rows, IReadOnlyCollection<TechnicalReportingReason> Reasons);

public sealed record TechnicalReportingManifestEntry(Guid Id, string FactSha256);
public sealed record TechnicalReportingManifestRegister(
    string Name, TechnicalReportingCompleteness Completeness,
    int SourceCount, int EventCount, Guid? FirstId, Guid? LastId,
    IReadOnlyCollection<TechnicalReportingManifestEntry> Entries);
public sealed record ProjectTechnicalOfficeSourceManifest(
    string ManifestVersion, string ContractVersion, string PolicyVersion,
    Guid TenantId, Guid ProjectId, DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc, long ConfigurationVersion,
    DateTimeOffset ConfigurationEffectiveAtUtc, bool SourceEnabled,
    TechnicalReportingClassification Classification, bool RestrictedPublicationApproved,
    DateTimeOffset WatermarkUtc,
    IReadOnlyCollection<TechnicalReportingManifestRegister> Collections);

public sealed record ProjectTechnicalOfficeReportingResult(
    string ContractVersion, string PolicyVersion, Guid TenantId, Guid ProjectId,
    DateOnly CutoffLocalDate, DateTimeOffset SourceCutoffUtc,
    TechnicalReportingClassification Classification, TechnicalReportingStatus DataStatus,
    IReadOnlyCollection<TechnicalReportingReason> Reasons,
    TechnicalReportingSection<TechnicalReportingDocumentRow> Documents,
    TechnicalReportingSection<TechnicalReportingTransmittalRow> Transmittals,
    TechnicalReportingSection<TechnicalReportingRfiRow> Rfis,
    TechnicalReportingSection<TechnicalReportingSubmittalRow> Submittals,
    ProjectTechnicalOfficeSourceManifest SourceManifest,
    string SourceManifestSha256, string SemanticSha256);
