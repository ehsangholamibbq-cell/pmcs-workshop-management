using Pmcs.Modules.TechnicalOffice.Domain;

namespace Pmcs.Modules.TechnicalOffice.Endpoints;

public sealed record CreateTechnicalDocumentRequest(
    Guid? ClientGeneratedId,
    string Title,
    TechnicalDocumentType Type,
    string Discipline,
    string? Originator,
    Guid? ContractId,
    string? LocationReference,
    string? WorkItemReference,
    string? WbsReference,
    string? Confidentiality);

public sealed record CreateDocumentRevisionRequest(
    Guid? ClientGeneratedId,
    string RevisionCode,
    DateOnly RevisionDate,
    DocumentRevisionPurpose Purpose,
    string FileName,
    string FileReference,
    string Sha256,
    Guid? SupersedesRevisionId);

public sealed record TechnicalTransitionRequest(long BaseRevision);
public sealed record TechnicalReviewRequest(long BaseRevision, string? Comment);
public sealed record TechnicalReturnRequest(long BaseRevision, string Reason);

public sealed record CreateTransmittalRequest(
    Guid? ClientGeneratedId,
    string Sender,
    IReadOnlyCollection<string> Recipients,
    IReadOnlyCollection<Guid> RevisionIds,
    string Purpose,
    string DeliveryChannel,
    DateOnly? DueResponseDate);

public sealed record AcknowledgeTransmittalRequest(long BaseRevision, string Reference);

public sealed record CreateRfiRequest(
    Guid? ClientGeneratedId,
    string Title,
    string Question,
    string RequestedFrom,
    string Discipline,
    Guid? ContractId,
    string? LocationReference,
    string? WorkItemReference,
    string? WbsReference,
    Guid? SourceIssueId,
    DateOnly RaisedDate,
    DateOnly? RequiredByDate,
    PotentialImpact PotentialImpact,
    bool IsBlocking,
    string? ProposedSolution,
    IReadOnlyCollection<string>? EvidenceReferences,
    IReadOnlyCollection<Guid>? RelatedRevisionIds);

public sealed record RecordRfiResponseRequest(
    long BaseRevision,
    string ResponseText,
    string RespondingParty,
    DateTimeOffset ResponseAt,
    RfiResponseClassification Classification,
    bool ChangePotential,
    IReadOnlyCollection<Guid>? ReferencedRevisionIds);

public sealed record CreateSubmittalRequest(
    Guid? ClientGeneratedId,
    string Title,
    TechnicalSubmittalType Type,
    string Discipline,
    string Submitter,
    string Reviewer,
    Guid? ContractId,
    Guid? CommitmentId,
    string? LocationReference,
    string? WorkItemReference,
    string? WbsReference,
    DateOnly? RequiredByDate,
    DateOnly? PlannedSubmissionDate,
    DateOnly? ReviewDueDate,
    int ResubmissionNumber,
    Guid? SupersedesSubmittalId,
    IReadOnlyCollection<Guid> RevisionIds,
    string? RequiredDeliverableReference);

public sealed record ReviewSubmittalRequest(
    long BaseRevision,
    SubmittalReviewOutcome Outcome,
    string? Comment);

public sealed record TechnicalOfficeStateResponse(
    IReadOnlyCollection<TechnicalDocumentResponse> Documents,
    IReadOnlyCollection<DocumentRevisionResponse> DocumentRevisions,
    IReadOnlyCollection<TransmittalResponse> Transmittals,
    IReadOnlyCollection<RfiResponse> Rfis,
    IReadOnlyCollection<SubmittalResponse> Submittals,
    int BlockingRfiCount,
    int OverdueRfiCount,
    int OverdueSubmittalCount,
    int SupersededRevisionCount);

public sealed record TechnicalDocumentResponse(
    Guid Id,
    string Number,
    string Title,
    TechnicalDocumentType Type,
    string Discipline,
    string? Originator,
    Guid? ContractId,
    string? LocationReference,
    string? WorkItemReference,
    string? WbsReference,
    string? Confidentiality,
    Guid? CurrentOfficialRevisionId,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    long Revision)
{
    public static TechnicalDocumentResponse From(TechnicalDocument item) => new(
        item.Id, item.Number, item.Title, item.Type, item.Discipline, item.Originator,
        item.ContractId, item.LocationReference, item.WorkItemReference, item.WbsReference,
        item.Confidentiality, item.CurrentOfficialRevisionId, item.CreatedBy, item.CreatedAt, item.Revision);
}

public sealed record DocumentRevisionResponse(
    Guid Id,
    Guid DocumentId,
    string RevisionCode,
    DateOnly RevisionDate,
    DocumentRevisionPurpose Purpose,
    string FileName,
    string FileReference,
    string Sha256,
    Guid? SupersedesRevisionId,
    DocumentRevisionStatus Status,
    Guid PreparedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    Guid? IssuedThroughTransmittalId,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? SupersededAt,
    long Revision)
{
    public static DocumentRevisionResponse From(TechnicalDocumentRevision item) => new(
        item.Id, item.DocumentId, item.RevisionCode, item.RevisionDate, item.Purpose,
        item.FileName, item.FileReference, item.Sha256, item.SupersedesRevisionId, item.Status,
        item.PreparedBy, item.CreatedAt, item.SubmittedAt, item.ReviewedBy, item.ReviewedAt,
        item.ReviewComment, item.IssuedThroughTransmittalId, item.IssuedAt, item.SupersededAt, item.Revision);
}

public sealed record TransmittalResponse(
    Guid Id,
    string Number,
    string Sender,
    IReadOnlyCollection<string> Recipients,
    IReadOnlyCollection<Guid> RevisionIds,
    string Purpose,
    string DeliveryChannel,
    DateOnly? DueResponseDate,
    TransmittalStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IssuedAt,
    Guid? IssuedBy,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgmentReference,
    long Revision)
{
    public static TransmittalResponse From(TechnicalTransmittal item) => new(
        item.Id, item.Number, item.Sender, item.Recipients, item.RevisionIds, item.Purpose,
        item.DeliveryChannel, item.DueResponseDate, item.Status, item.CreatedBy, item.CreatedAt,
        item.IssuedAt, item.IssuedBy, item.AcknowledgedAt, item.AcknowledgmentReference, item.Revision);
}

public sealed record RfiResponse(
    Guid Id,
    string Number,
    string Title,
    string Question,
    string RequestedFrom,
    string Discipline,
    Guid? ContractId,
    string? LocationReference,
    string? WorkItemReference,
    string? WbsReference,
    Guid? SourceIssueId,
    DateOnly RaisedDate,
    DateOnly? RequiredByDate,
    PotentialImpact PotentialImpact,
    bool IsBlocking,
    string? ProposedSolution,
    IReadOnlyCollection<string> EvidenceReferences,
    IReadOnlyCollection<Guid> RelatedRevisionIds,
    IReadOnlyCollection<RfiResponseRecord> Responses,
    RfiStatus Status,
    Guid RaisedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ClosedAt,
    long Revision)
{
    public static RfiResponse From(TechnicalRfi item) => new(
        item.Id, item.Number, item.Title, item.Question, item.RequestedFrom, item.Discipline,
        item.ContractId, item.LocationReference, item.WorkItemReference, item.WbsReference,
        item.SourceIssueId, item.RaisedDate, item.RequiredByDate, item.PotentialImpact,
        item.IsBlocking, item.ProposedSolution, item.EvidenceReferences, item.RelatedRevisionIds,
        item.Responses, item.Status, item.RaisedBy, item.CreatedAt, item.SubmittedAt, item.ClosedAt, item.Revision);
}

public sealed record SubmittalResponse(
    Guid Id,
    string Number,
    string Title,
    TechnicalSubmittalType Type,
    string Discipline,
    string Submitter,
    string Reviewer,
    Guid? ContractId,
    Guid? CommitmentId,
    string? LocationReference,
    string? WorkItemReference,
    string? WbsReference,
    DateOnly? RequiredByDate,
    DateOnly? PlannedSubmissionDate,
    DateOnly? ReviewDueDate,
    int ResubmissionNumber,
    Guid? SupersedesSubmittalId,
    IReadOnlyCollection<Guid> RevisionIds,
    string? RequiredDeliverableReference,
    SubmittalStatus Status,
    SubmittalReviewOutcome? ReviewOutcome,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    DateTimeOffset? ClosedAt,
    long Revision)
{
    public static SubmittalResponse From(TechnicalSubmittal item) => new(
        item.Id, item.Number, item.Title, item.Type, item.Discipline, item.Submitter, item.Reviewer,
        item.ContractId, item.CommitmentId, item.LocationReference, item.WorkItemReference,
        item.WbsReference, item.RequiredByDate, item.PlannedSubmissionDate, item.ReviewDueDate,
        item.ResubmissionNumber, item.SupersedesSubmittalId, item.RevisionIds,
        item.RequiredDeliverableReference, item.Status, item.ReviewOutcome, item.CreatedBy,
        item.CreatedAt, item.SubmittedAt, item.ReviewedBy, item.ReviewedAt, item.ReviewComment,
        item.ClosedAt, item.Revision);
}
