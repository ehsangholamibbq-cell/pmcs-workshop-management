using Pmcs.Modules.ActionControl.Domain;

namespace Pmcs.Modules.ActionControl.Endpoints;

public sealed record CreateRiskMatrixRequest(Guid ClientGeneratedId, string Title, int LowMaximum,
    int ModerateMaximum, int HighMaximum, DateTimeOffset EffectiveFrom);

public sealed record CreateSlaRuleRequest(Guid ClientGeneratedId, string Title, SlaEntityType EntityType,
    GovernanceSeverity? Severity, int Duration, SlaDurationUnit DurationUnit, int WarningLeadMinutes,
    int EscalationDelayMinutes, Guid EscalationRecipientUserId, DateTimeOffset EffectiveFrom);

public sealed record ProposeRiskRequest(Guid ClientGeneratedId, RiskType Type, string Cause,
    string UncertainEvent, string ImpactStatement, string Category, Guid OwnerUserId,
    RecordConfidentiality Confidentiality, string SourceModule, string SourceEntityType,
    Guid? SourceEntityId, long? SourceRevision, string SourceSnapshot, IReadOnlyCollection<string>? EvidenceReferences);

public sealed record AssessRiskRequest(long BaseRevision, Guid MatrixVersionId, ProbabilityBand Probability,
    ImpactBand TimeImpact, ImpactBand CostImpact, ImpactBand QualityImpact, ImpactBand SafetyImpact,
    ImpactBand ContractImpact, ImpactBand OperationsImpact, RiskResponseStrategy ResponseStrategy,
    string ResponsePlan, string EarlyWarningIndicator, DateOnly ReviewDate);

public sealed record ReviewRiskRequest(long BaseRevision, ProbabilityBand ResidualProbability,
    ImpactBand ResidualImpact, string ResponsePlan, DateOnly NextReviewDate);

public sealed record RiskStateRequest(long BaseRevision);
public sealed record ReopenRiskRequest(long BaseRevision, string Reason);
public sealed record CloseRiskRequest(long BaseRevision, bool Expired, string Reason, IReadOnlyCollection<string>? EvidenceReferences);

public sealed record MaterializeRiskRequest(long BaseRevision, Guid IssueId, string Title, string ObservedFact,
    string Category, GovernanceSeverity Severity, GovernanceUrgency Urgency, Guid OwnerUserId,
    DateOnly TargetResolutionDate, IReadOnlyCollection<string>? EvidenceReferences);

public sealed record CreateIssueRequest(Guid ClientGeneratedId, string Title, string ObservedFact,
    string Category, GovernanceSeverity Severity, GovernanceUrgency Urgency, Guid OwnerUserId,
    DateOnly TargetResolutionDate, string SourceModule, string SourceEntityType, Guid? SourceEntityId,
    long? SourceRevision, string SourceSnapshot, IReadOnlyCollection<string>? EvidenceReferences,
    RecordConfidentiality Confidentiality);

public sealed record TransitionIssueRequest(long BaseRevision, IssueStatus TargetStatus,
    string? ResolutionNote, IReadOnlyCollection<string>? ClosureEvidenceReferences);

public sealed record CreateDecisionRequest(Guid ClientGeneratedId, string Question, string WhyNow,
    DateOnly RequiredBy, Guid AuthorityUserId, IReadOnlyCollection<string>? KnownFacts,
    IReadOnlyCollection<string>? Assumptions, IReadOnlyCollection<string>? Predictions,
    IReadOnlyCollection<string>? Options, string? Recommendation, IReadOnlyCollection<string>? Constraints,
    IReadOnlyCollection<string>? EvidenceReferences, RecordConfidentiality Confidentiality,
    string SourceModule, string SourceEntityType, Guid? SourceEntityId, long? SourceRevision, string SourceSnapshot);

public sealed record DecisionRequestStateRequest(long BaseRevision, string? InformationRequest);

public sealed record RecordDecisionRequest(Guid ClientGeneratedId, long BaseRevision, string SelectedOption,
    string Rationale, IReadOnlyCollection<string>? Conditions, DecisionChannel Channel,
    DateTimeOffset DecidedAt, DateOnly? EffectiveDate);

public sealed record SupersedeDecisionRequest(Guid ClientGeneratedId, long DecisionBaseRevision,
    long RequestBaseRevision, string SelectedOption, string Rationale, IReadOnlyCollection<string>? Conditions,
    DecisionChannel Channel, DateTimeOffset DecidedAt, DateOnly? EffectiveDate);

public sealed record ReviewDecisionEffectRequest(long DecisionBaseRevision, long RequestBaseRevision,
    string Review, IReadOnlyCollection<string>? EvidenceReferences);

public sealed record AcknowledgeEscalationRequest(long BaseRevision, string? Note);

public sealed record RiskMatrixResponse(Guid Id, int Version, string Title, string FormulaVersion,
    int LowMaximum, int ModerateMaximum, int HighMaximum, DateTimeOffset EffectiveFrom);

public sealed record SlaRuleResponse(Guid Id, int Version, string Title, SlaEntityType EntityType,
    GovernanceSeverity? Severity, int Duration, SlaDurationUnit DurationUnit, int WarningLeadMinutes,
    int EscalationDelayMinutes, Guid EscalationRecipientUserId, string EscalationRecipientDisplayName,
    DateTimeOffset EffectiveFrom);

public sealed record RiskResponse(Guid Id, string Number, RiskType Type, string Cause, string UncertainEvent,
    string ImpactStatement, string Category, Guid OwnerUserId, string OwnerDisplayName,
    RecordConfidentiality Confidentiality, RiskStatus Status, ProbabilityBand? Probability,
    int? InherentScore, RiskRatingBand? InherentRating, int? ResidualScore, RiskRatingBand? ResidualRating,
    Guid? MatrixVersionId, int? MatrixVersion, RiskResponseStrategy? ResponseStrategy, string? ResponsePlan,
    string? EarlyWarningIndicator, DateOnly? ReviewDate, string SourceModule, string SourceEntityType,
    Guid? SourceEntityId, long? SourceRevision, string SourceSnapshot, IReadOnlyCollection<string> EvidenceReferences,
    Guid? MaterializedIssueId, DateTimeOffset? SlaDueAt, Guid? SlaRuleVersionId,
    string? ClosureReason, IReadOnlyCollection<string> ClosureEvidence, DateTimeOffset CreatedAt, long Revision)
{
    internal static RiskResponse From(ProjectRisk item) => new(item.Id, item.Number, item.Type, item.Cause,
        item.UncertainEvent, item.ImpactStatement, item.Category, item.OwnerUserId, item.OwnerDisplayName,
        item.Confidentiality, item.Status, item.Probability, item.InherentScore, item.InherentRating,
        item.ResidualScore, item.ResidualRating, item.MatrixVersionId, item.MatrixVersion,
        item.ResponseStrategy, item.ResponsePlan, item.EarlyWarningIndicator, item.ReviewDate,
        item.SourceModule, item.SourceEntityType, item.SourceEntityId, item.SourceRevision, item.SourceSnapshot,
        item.EvidenceReferences, item.MaterializedIssueId, item.SlaDueAt, item.SlaRuleVersionId,
        item.ClosureReason, item.ClosureEvidence, item.CreatedAt, item.Revision);
}

public sealed record IssueResponse(Guid Id, string Number, string Title, string ObservedFact, string Category,
    GovernanceSeverity Severity, GovernanceUrgency Urgency, Guid OwnerUserId, string OwnerDisplayName,
    DateOnly TargetResolutionDate, RecordConfidentiality Confidentiality, IssueStatus Status,
    string SourceModule, string SourceEntityType, Guid? SourceEntityId, long? SourceRevision,
    string SourceSnapshot, Guid? MaterializedFromRiskId, IReadOnlyCollection<string> EvidenceReferences,
    DateTimeOffset? SlaDueAt, Guid? SlaRuleVersionId, string? ResolutionNote,
    IReadOnlyCollection<string> ClosureEvidence, Guid? ResolvedBy, DateTimeOffset? ResolvedAt,
    Guid? ClosedBy, DateTimeOffset? ClosedAt, DateTimeOffset CreatedAt, long Revision)
{
    internal static IssueResponse From(ManagementIssue item) => new(item.Id, item.Number, item.Title,
        item.ObservedFact, item.Category, item.Severity, item.Urgency, item.OwnerUserId,
        item.OwnerDisplayName, item.TargetResolutionDate, item.Confidentiality, item.Status,
        item.SourceModule, item.SourceEntityType, item.SourceEntityId, item.SourceRevision, item.SourceSnapshot,
        item.MaterializedFromRiskId, item.EvidenceReferences, item.SlaDueAt, item.SlaRuleVersionId,
        item.ResolutionNote, item.ClosureEvidence, item.ResolvedBy, item.ResolvedAt,
        item.ClosedBy, item.ClosedAt, item.CreatedAt, item.Revision);
}

public sealed record DecisionRequestResponse(Guid Id, string Number, string Question, string WhyNow,
    DateOnly RequiredBy, Guid AuthorityUserId, string AuthorityDisplayName,
    IReadOnlyCollection<string> KnownFacts, IReadOnlyCollection<string> Assumptions,
    IReadOnlyCollection<string> Predictions, IReadOnlyCollection<string> Options, string? Recommendation,
    IReadOnlyCollection<string> Constraints, IReadOnlyCollection<string> EvidenceReferences,
    RecordConfidentiality Confidentiality, DecisionRequestStatus Status, string? InformationRequest,
    Guid? DecisionRecordId, DateTimeOffset? SlaDueAt, Guid? SlaRuleVersionId, DateTimeOffset CreatedAt, long Revision)
{
    internal static DecisionRequestResponse From(DecisionRequest item) => new(item.Id, item.Number,
        item.Question, item.WhyNow, item.RequiredBy, item.AuthorityUserId, item.AuthorityDisplayName,
        item.KnownFacts, item.Assumptions, item.Predictions, item.Options, item.Recommendation,
        item.Constraints, item.EvidenceReferences, item.Confidentiality, item.Status,
        item.InformationRequest, item.DecisionRecordId, item.SlaDueAt, item.SlaRuleVersionId,
        item.CreatedAt, item.Revision);
}

public sealed record DecisionResponse(Guid Id, Guid DecisionRequestId, string Number, string SelectedOption,
    string Rationale, IReadOnlyCollection<string> Conditions, DecisionChannel Channel,
    DateTimeOffset DecidedAt, DateOnly? EffectiveDate, Guid DecidedBy, string DecidedByDisplayName,
    Guid? SupersedesDecisionId, Guid? SupersededByDecisionId, string? EffectReview,
    IReadOnlyCollection<string> EffectEvidence, DecisionRecordStatus Status, DateTimeOffset RecordedAt, long Revision)
{
    internal static DecisionResponse From(DecisionRecord item) => new(item.Id, item.DecisionRequestId,
        item.Number, item.SelectedOption, item.Rationale, item.Conditions, item.Channel, item.DecidedAt,
        item.EffectiveDate, item.DecidedBy, item.DecidedByDisplayName, item.SupersedesDecisionId,
        item.SupersededByDecisionId, item.EffectReview, item.EffectEvidence, item.Status, item.RecordedAt, item.Revision);
}

public sealed record EscalationResponse(Guid Id, string ThreadKey, SlaEntityType EntityType, Guid EntityId,
    string EntityNumber, string EntityTitle, EscalationReason Reason, int Level,
    Guid RecipientUserId, string RecipientDisplayName, RecordConfidentiality Confidentiality,
    EscalationStatus Status, int OccurrenceCount, DateTimeOffset FirstRaisedAt,
    DateTimeOffset LastRaisedAt, Guid? AcknowledgedBy, DateTimeOffset? AcknowledgedAt,
    string? AcknowledgementNote, long Revision)
{
    internal static EscalationResponse From(EscalationThread item) => new(item.Id, item.ThreadKey,
        item.EntityType, item.EntityId, item.EntityNumber, item.EntityTitle, item.Reason, item.Level,
        item.RecipientUserId, item.RecipientDisplayName, item.Confidentiality, item.Status,
        item.OccurrenceCount, item.FirstRaisedAt, item.LastRaisedAt, item.AcknowledgedBy,
        item.AcknowledgedAt, item.AcknowledgementNote, item.Revision);
}

public sealed record GovernanceDeadlineAlertResponse(SlaEntityType EntityType, Guid EntityId,
    string EntityNumber, string Title, string DeadlineKind, DateTimeOffset DueAt, DeadlineSignal Signal,
    RecordConfidentiality Confidentiality);

public sealed record GovernanceCountsResponse(int OpenIssues, int ActiveRisks, int CriticalRisks,
    int PendingDecisions, int UnacknowledgedEscalations);

public sealed record GovernanceOutlookResponse(string State, int TrackedDeadlines,
    int DueWithinSevenDays, int Overdue, DateTimeOffset CalculatedAt);

public sealed record GovernancePersonResponse(Guid UserId, string DisplayName, bool CanDecide);

public sealed record GovernanceStateResponse(string SetupState, bool SensitiveRecordsIncluded,
    IReadOnlyCollection<GovernancePersonResponse> AssignablePeople,
    IReadOnlyCollection<RiskMatrixResponse> RiskMatrices, IReadOnlyCollection<SlaRuleResponse> SlaRules,
    IReadOnlyCollection<RiskResponse> Risks, IReadOnlyCollection<IssueResponse> Issues,
    IReadOnlyCollection<DecisionRequestResponse> DecisionRequests, IReadOnlyCollection<DecisionResponse> Decisions,
    IReadOnlyCollection<EscalationResponse> Escalations, IReadOnlyCollection<GovernanceDeadlineAlertResponse> Alerts,
    GovernanceCountsResponse Counts, GovernanceOutlookResponse Outlook);

public sealed record EvaluateGovernanceResponse(int Raised, int Refreshed, int Closed,
    IReadOnlyCollection<EscalationResponse> Escalations);
