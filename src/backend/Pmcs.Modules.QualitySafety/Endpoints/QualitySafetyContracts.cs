using Pmcs.Modules.QualitySafety.Domain;

namespace Pmcs.Modules.QualitySafety.Endpoints;

public sealed record ConfigureQualitySafetyRequest(long? BaseRevision, QualityOperatingMode QualityMode,
    HseOperatingMode HseMode, Guid? QualityOwnerUserId, Guid? HseOwnerUserId,
    Guid? QualityMatrixVersionId, Guid? HseMatrixVersionId, bool WorkflowAndSlaDefined,
    bool TemplatesDefined, bool EvidenceAndClosureRulesDefined);
public sealed record CreateRiskMatrixRequest(Guid ClientGeneratedId, ControlArea Area, int Version,
    string Title, string DefinitionJson, DateTimeOffset EffectiveFrom);
public sealed record CaptureIntakeRequest(Guid ClientGeneratedId, IntakeKind Kind, DateTimeOffset ObservedAt,
    string Location, string Facts, InitialSeverity InitialSeverity, string? ImmediateAction,
    IReadOnlyCollection<string>? EvidenceReferences, DataClassification Classification);
public sealed record TriageIntakeRequest(long BaseRevision);
public sealed record ResolveIntakeRequest(long BaseRevision, bool RetainAsGeneralIssue, string Note);
public sealed record ConvertIntakeRequest(long BaseRevision, IntakeConversionType ConversionType,
    Guid ClientGeneratedRecordId, string Note, string? Title, string? Requirement);
public sealed record RequestInspectionRequest(Guid ClientGeneratedId, Guid? SourceIntakeId,
    Guid? InspectionTestPlanVersionId, string InspectionType,
    string Location, string AcceptanceCriteria, string? ChecklistTemplateReference,
    int? ChecklistTemplateVersion, DateTimeOffset RequestedFor);
public sealed record RecordInspectionReadinessRequest(long BaseRevision, InspectionReadiness Readiness, string? Note);
public sealed record RecordInspectionResultRequest(long BaseRevision, InspectionResult Result, string? Note,
    IReadOnlyCollection<string>? EvidenceReferences);
public sealed record CreateNcrRequest(Guid ClientGeneratedId, Guid? SourceIntakeId, Guid? InspectionId,
    Guid? GoodsReceiptId, Guid? PurchaseOrderId, Guid? VendorPartyId, Guid? SupplyItemId,
    string? LotReference, string Title, string Requirement, string NonConformity);
public sealed record TransitionNcrRequest(long BaseRevision, NcrStatus TargetStatus, NcrDisposition Disposition,
    string? DispositionNote, Guid? ConcessionApprovedBy, RootCauseStatus RootCauseStatus, string? RootCause,
    IReadOnlyCollection<string>? ClosureEvidence, string? ClosureWaiverReason);
public sealed record CreateDefectRequest(Guid ClientGeneratedId, Guid? SourceIntakeId, string Title, string Location);
public sealed record AssignDefectRequest(long BaseRevision, Guid AssigneeUserId, DateOnly DueDate);
public sealed record TransitionDefectRequest(long BaseRevision, DefectStatus TargetStatus,
    IReadOnlyCollection<string>? EvidenceReferences);
public sealed record ReportIncidentRequest(Guid ClientGeneratedId, Guid? SourceIntakeId, DateTimeOffset OccurredAt,
    string Location, string Facts, InitialSeverity PreliminarySeverity, Guid MatrixVersionId,
    DataClassification Classification);
public sealed record TransitionIncidentRequest(long BaseRevision, IncidentStatus TargetStatus,
    InitialSeverity? FinalSeverity, RootCauseStatus RootCauseStatus, string? RootCause,
    IReadOnlyCollection<string>? ClosureEvidence);
public sealed record CreateCorrectiveActionRequest(Guid ClientGeneratedId, ControlArea SourceArea,
    string SourceRecordType, Guid SourceRecordId, CorrectiveActionKind Kind, string Title,
    Guid OwnerUserId, string ResponsibleParty, DateOnly DueDate, string SuccessCriteria);
public sealed record TransitionCorrectiveActionRequest(long BaseRevision, CorrectiveActionStatus TargetStatus,
    IReadOnlyCollection<string>? EvidenceReferences);
public sealed record ExtendCorrectiveActionRequest(long BaseRevision, DateOnly ExtendedDueDate, string Reason);
public sealed record CreatePermitRequest(Guid ClientGeneratedId, string WorkDescription, string Location,
    DateTimeOffset ValidFrom, DateTimeOffset ValidTo, IReadOnlyCollection<string> Hazards,
    IReadOnlyCollection<string> Controls);
public sealed record TransitionPermitRequest(long BaseRevision, PermitStatus TargetStatus);
public sealed record RecordToolboxTalkRequest(Guid ClientGeneratedId, string Topic, DateTimeOffset HeldAt,
    string Location, IReadOnlyCollection<string> Attendees, IReadOnlyCollection<string> EvidenceReferences);
public sealed record CreateInspectionTestPlanRequest(Guid ClientGeneratedId, string Code, int Version,
    string Title, IReadOnlyCollection<string> Stages, string AcceptanceCriteria, string InspectorRole,
    InspectionPointType PointType, DateTimeOffset EffectiveFrom);
public sealed record CreateChecklistTemplateRequest(Guid ClientGeneratedId, string Code, int Version,
    string Title, IReadOnlyCollection<string> Items, DateTimeOffset EffectiveFrom);
public sealed record RecordQualityTestRequest(Guid ClientGeneratedId, Guid? InspectionId, string TestType,
    DateTimeOffset TestedAt, string SampleReference, string AcceptanceCriteria, QualityTestResult Result,
    string ResultDetails, IReadOnlyCollection<string> EvidenceReferences);
public sealed record RecordCompetencyRequest(Guid ClientGeneratedId, string PersonReference,
    DateOnly InductionDate, DateOnly? ValidUntil, IReadOnlyCollection<string> Competencies,
    IReadOnlyCollection<string> EvidenceReferences, DataClassification Classification);
public sealed record RecordExposureHoursRequest(Guid ClientGeneratedId, DateOnly PeriodStart,
    DateOnly PeriodEnd, decimal Hours, string SourceReference, IReadOnlyCollection<string> EvidenceReferences);

public sealed record ConfigurationResponse(Guid Id, QualityOperatingMode QualityMode, HseOperatingMode HseMode,
    Guid? QualityOwnerUserId, Guid? HseOwnerUserId, Guid? QualityMatrixVersionId, Guid? HseMatrixVersionId,
    bool WorkflowAndSlaDefined, bool TemplatesDefined, bool EvidenceAndClosureRulesDefined,
    bool QualityReady, bool HseReady, DateTimeOffset ChangedAt, long Revision)
{
    internal static ConfigurationResponse From(QualitySafetyConfiguration x) => new(x.Id, x.QualityMode, x.HseMode,
        x.QualityOwnerUserId, x.HseOwnerUserId, x.QualityMatrixVersionId, x.HseMatrixVersionId,
        x.WorkflowAndSlaDefined, x.TemplatesDefined, x.EvidenceAndClosureRulesDefined,
        x.QualityReady, x.HseReady, x.ChangedAt, x.Revision);
}
public sealed record RiskMatrixResponse(Guid Id, ControlArea Area, int Version, string Title,
    string DefinitionJson, DateTimeOffset EffectiveFrom, DateTimeOffset CreatedAt, long Revision)
{ internal static RiskMatrixResponse From(RiskMatrixVersion x) => new(x.Id, x.Area, x.Version, x.Title, x.DefinitionJson, x.EffectiveFrom, x.CreatedAt, x.Revision); }
public sealed record IntakeResponse(Guid Id, string Number, IntakeKind Kind, DateTimeOffset ObservedAt, string Location,
    string Facts, InitialSeverity InitialSeverity, string? ImmediateAction, IReadOnlyCollection<string> EvidenceReferences,
    DataClassification Classification, IntakeStatus Status, IntakeConversionType ConversionType,
    Guid? ConvertedRecordId, string? TriageNote, DateTimeOffset CreatedAt, long Revision)
{ internal static IntakeResponse From(QualitySafetyIntake x) => new(x.Id, x.Number, x.Kind, x.ObservedAt, x.Location, x.Facts, x.InitialSeverity, x.ImmediateAction, x.EvidenceReferences, x.Classification, x.Status, x.ConversionType, x.ConvertedRecordId, x.TriageNote, x.CreatedAt, x.Revision); }
public sealed record InspectionResponse(Guid Id, string Number, Guid? SourceIntakeId,
    Guid? InspectionTestPlanVersionId, string InspectionType,
    string Location, string AcceptanceCriteria, string? ChecklistTemplateReference, int? ChecklistTemplateVersion,
    DateTimeOffset RequestedFor, InspectionReadiness Readiness, string? ReadinessNote,
    InspectionStatus Status, InspectionResult? Result, string? ResultNote,
    IReadOnlyCollection<string> EvidenceReferences, DateTimeOffset? InspectedAt, long Revision)
{ internal static InspectionResponse From(InspectionRecord x) => new(x.Id, x.Number, x.SourceIntakeId, x.InspectionTestPlanVersionId, x.InspectionType, x.Location, x.AcceptanceCriteria, x.ChecklistTemplateReference, x.ChecklistTemplateVersion, x.RequestedFor, x.Readiness, x.ReadinessNote, x.Status, x.Result, x.ResultNote, x.EvidenceReferences, x.InspectedAt, x.Revision); }
public sealed record NcrResponse(Guid Id, string Number, Guid? SourceIntakeId, Guid? InspectionId,
    Guid? GoodsReceiptId, Guid? PurchaseOrderId, Guid? VendorPartyId, Guid? SupplyItemId, string? LotReference,
    string Title, string Requirement, string NonConformity, NcrStatus Status, NcrDisposition Disposition,
    RootCauseStatus RootCauseStatus, string? RootCause, IReadOnlyCollection<string> ClosureEvidence,
    DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt, long Revision)
{ internal static NcrResponse From(NonConformanceRecord x) => new(x.Id, x.Number, x.SourceIntakeId, x.InspectionId, x.GoodsReceiptId, x.PurchaseOrderId, x.VendorPartyId, x.SupplyItemId, x.LotReference, x.Title, x.Requirement, x.NonConformity, x.Status, x.Disposition, x.RootCauseStatus, x.RootCause, x.ClosureEvidence, x.CreatedAt, x.ClosedAt, x.Revision); }
public sealed record DefectResponse(Guid Id, string Number, Guid? SourceIntakeId, string Title, string Location,
    Guid? AssigneeUserId, DateOnly? DueDate, DefectStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt, long Revision)
{ internal static DefectResponse From(DefectRecord x) => new(x.Id, x.Number, x.SourceIntakeId, x.Title, x.Location, x.AssigneeUserId, x.DueDate, x.Status, x.CreatedAt, x.ClosedAt, x.Revision); }
public sealed record IncidentResponse(Guid Id, string Number, Guid? SourceIntakeId, DateTimeOffset OccurredAt,
    string Location, string Facts, InitialSeverity PreliminarySeverity, InitialSeverity? FinalSeverity,
    Guid MatrixVersionId, DataClassification Classification, IncidentStatus Status,
    RootCauseStatus RootCauseStatus, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt, long Revision)
{ internal static IncidentResponse From(SafetyIncident x) => new(x.Id, x.Number, x.SourceIntakeId, x.OccurredAt, x.Location, x.Facts, x.PreliminarySeverity, x.FinalSeverity, x.MatrixVersionId, x.Classification, x.Status, x.RootCauseStatus, x.CreatedAt, x.ClosedAt, x.Revision); }
public sealed record CorrectiveActionResponse(Guid Id, string Number, ControlArea SourceArea, string SourceRecordType,
    Guid SourceRecordId, CorrectiveActionKind Kind, string Title, Guid OwnerUserId, string ResponsibleParty,
    DateOnly DueDate, DateOnly? ExtendedDueDate, string SuccessCriteria, CorrectiveActionStatus Status,
    DateTimeOffset? VerifiedAt, DateTimeOffset? ClosedAt, long Revision)
{ internal static CorrectiveActionResponse From(CorrectiveAction x) => new(x.Id, x.Number, x.SourceArea, x.SourceRecordType, x.SourceRecordId, x.Kind, x.Title, x.OwnerUserId, x.ResponsibleParty, x.DueDate, x.ExtendedDueDate, x.SuccessCriteria, x.Status, x.VerifiedAt, x.ClosedAt, x.Revision); }
public sealed record PermitResponse(Guid Id, string Number, string WorkDescription, string Location,
    DateTimeOffset ValidFrom, DateTimeOffset ValidTo, PermitStatus Status, DateTimeOffset? ApprovedAt,
    DateTimeOffset? ClosedAt, long Revision)
{ internal static PermitResponse From(PermitToWork x) => new(x.Id, x.Number, x.WorkDescription, x.Location, x.ValidFrom, x.ValidTo, x.Status, x.ApprovedAt, x.ClosedAt, x.Revision); }
public sealed record ToolboxTalkResponse(Guid Id, string Number, string Topic, DateTimeOffset HeldAt,
    string Location, DateTimeOffset CreatedAt, long Revision)
{ internal static ToolboxTalkResponse From(ToolboxTalk x) => new(x.Id, x.Number, x.Topic, x.HeldAt, x.Location, x.CreatedAt, x.Revision); }
public sealed record InspectionTestPlanResponse(Guid Id, string Code, int Version, string Title,
    string AcceptanceCriteria, string InspectorRole, InspectionPointType PointType,
    DateTimeOffset EffectiveFrom, long Revision)
{ internal static InspectionTestPlanResponse From(InspectionTestPlanVersion x) => new(x.Id, x.Code, x.Version, x.Title, x.AcceptanceCriteria, x.InspectorRole, x.PointType, x.EffectiveFrom, x.Revision); }
public sealed record ChecklistTemplateResponse(Guid Id, string Code, int Version, string Title,
    DateTimeOffset EffectiveFrom, long Revision)
{ internal static ChecklistTemplateResponse From(ChecklistTemplateVersion x) => new(x.Id, x.Code, x.Version, x.Title, x.EffectiveFrom, x.Revision); }
public sealed record QualityTestResponse(Guid Id, string Number, Guid? InspectionId, string TestType,
    DateTimeOffset TestedAt, string SampleReference, QualityTestResult Result, string ResultDetails,
    DateTimeOffset CreatedAt, long Revision)
{ internal static QualityTestResponse From(QualityTestRecord x) => new(x.Id, x.Number, x.InspectionId, x.TestType, x.TestedAt, x.SampleReference, x.Result, x.ResultDetails, x.CreatedAt, x.Revision); }
public sealed record CompetencyResponse(Guid Id, string PersonReference, DateOnly InductionDate,
    DateOnly? ValidUntil, DataClassification Classification, DateTimeOffset CreatedAt, long Revision)
{ internal static CompetencyResponse From(HseCompetencyRecord x) => new(x.Id, x.PersonReference, x.InductionDate, x.ValidUntil, x.Classification, x.CreatedAt, x.Revision); }
public sealed record ExposureHoursResponse(Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd,
    decimal Hours, string SourceReference, DateTimeOffset ApprovedAt, long Revision)
{ internal static ExposureHoursResponse From(ExposureHoursRecord x) => new(x.Id, x.PeriodStart, x.PeriodEnd, x.Hours, x.SourceReference, x.ApprovedAt, x.Revision); }

public sealed record QualitySafetyStateResponse(
    string QualityState, string HseState, ConfigurationResponse? Configuration,
    IReadOnlyCollection<RiskMatrixResponse> Matrices, IReadOnlyCollection<IntakeResponse> Intakes,
    IReadOnlyCollection<InspectionResponse> Inspections, IReadOnlyCollection<NcrResponse> NonConformances,
    IReadOnlyCollection<DefectResponse> Defects, IReadOnlyCollection<IncidentResponse> Incidents,
    IReadOnlyCollection<CorrectiveActionResponse> CorrectiveActions, IReadOnlyCollection<PermitResponse> Permits,
    IReadOnlyCollection<ToolboxTalkResponse> ToolboxTalks,
    IReadOnlyCollection<InspectionTestPlanResponse> InspectionTestPlans,
    IReadOnlyCollection<ChecklistTemplateResponse> ChecklistTemplates,
    IReadOnlyCollection<QualityTestResponse> TestRecords, IReadOnlyCollection<CompetencyResponse> CompetencyRecords,
    IReadOnlyCollection<ExposureHoursResponse> ExposureHours, int OpenQualityCount,
    int? OpenHseCount, int OverdueCorrectiveActionCount,
    bool IncidentRatesAvailable, decimal? ReportedIncidentFrequencyPerTwoHundredThousandHours,
    string? IncidentRateUnavailableReason);
