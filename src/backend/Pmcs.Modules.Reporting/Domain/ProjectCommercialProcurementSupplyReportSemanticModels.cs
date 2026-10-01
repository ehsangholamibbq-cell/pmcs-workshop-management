using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ProjectCommercialProcurementSupplyReportSemanticSnapshot(
    string SchemaVersion,
    string SemanticContractId,
    string DefinitionCode,
    string DefinitionVersion,
    string PolicyVersion,
    ReportDataStatus DataStatus,
    IReadOnlyCollection<ProjectCommercialReportingReasonCode> ReasonCodes,
    ProjectCommercialProcurementSupplyReportParameters Parameters,
    ProjectCommercialProcurementSupplyReportProjectIdentity Project,
    ProjectCommercialProcurementSupplyReportCutoffIdentity Cutoff,
    ReportClassification Classification,
    ProjectCommercialProcurementSupplyReportConfiguration? Configuration,
    ProjectCommercialReportingSectionStatus ContractStatus,
    ProjectCommercialContractSummary? ContractSummary,
    IReadOnlyCollection<ProjectCommercialProcurementSupplyReportContractRow> ContractRegister,
    IReadOnlyCollection<ProjectCommercialProcurementSupplyReportAmendmentRow> ApprovedAmendments,
    ProjectCommercialReportingSectionStatus ProcurementStatus,
    ProjectCommercialProcurementSummary? ProcurementSummary,
    IReadOnlyCollection<ProjectCommercialProcurementSupplyReportOrderRow> PurchaseOrders,
    ProjectCommercialReportingSectionStatus SupplyStatus,
    IReadOnlyCollection<ProjectCommercialSupplySummary> SupplySummaries,
    IReadOnlyCollection<ProjectCommercialProcurementSupplyReportSupplierPerformance> SupplierPerformance,
    ProjectCommercialSourceCounts SourceCounts,
    DateTimeOffset? SourceMaxChangedAt,
    string SourceManifestSha256);

internal sealed record ProjectCommercialProcurementSupplyReportProjectIdentity(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    string CapturedBaseCurrencyCode,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt,
    DateTimeOffset ProfileCapturedAtUtc);

internal sealed record ProjectCommercialProcurementSupplyReportCutoffIdentity(
    DateTimeOffset SourceCutoffUtc,
    DateOnly CutoffLocalDate);

internal sealed record ProjectCommercialProcurementSupplyReportConfiguration(
    long ConfigurationVersion,
    long ProjectRevision,
    ContractModel ContractModel,
    ProjectFeatureState ContractState,
    ProjectFeatureState ProcurementState,
    string TimeZone,
    string BaseCurrencyCode,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc);

internal sealed record ProjectCommercialProcurementSupplyReportContractRow(
    string Number,
    string Title,
    string PartyCode,
    string PartyName,
    PartyType PartyType,
    ProjectContractType Type,
    ProjectCommercialContractState State,
    DateOnly? StartDate,
    DateOnly? OriginalEndDate,
    DateOnly? EffectiveEndDate,
    decimal? OriginalApprovedAmount,
    decimal ApprovedAmountDelta,
    decimal? EffectiveApprovedAmount,
    int ApprovedAmendmentCount,
    int ApprovedExtensionDays);

internal sealed record ProjectCommercialProcurementSupplyReportAmendmentRow(
    string ContractNumber,
    string Number,
    string Title,
    ContractAmendmentType Type,
    decimal? AmountDelta,
    int? ExtensionDays,
    DateTimeOffset ApprovedAt);

internal sealed record ProjectCommercialProcurementSupplyReportOrderRow(
    string Number,
    string Title,
    string PurchaseRequestNumber,
    string? ContractNumber,
    string PartyCode,
    string PartyName,
    PartyType PartyType,
    decimal Amount,
    DateOnly? DeliveryDueDate,
    ProjectCommercialPurchaseOrderState State,
    string? ItemCode,
    string? ItemName,
    SupplyItemKind? ItemKind,
    decimal? OrderedQuantity,
    string? UnitCode,
    decimal? OrderedBaseQuantity,
    string? BaseUnit,
    int? ConversionVersion,
    decimal? DeliveredBaseQuantity,
    decimal? AcceptedBaseQuantity,
    decimal? RejectedBaseQuantity,
    decimal? QuarantinedBaseQuantity,
    decimal? RemainingOrderedQuantity,
    decimal? AcceptedExcessQuantity,
    decimal? FulfillmentPercent,
    DateOnly? CompletionDate,
    ProjectCommercialDeliveryStatus DeliveryStatus,
    int GoodsReceiptCount,
    int PendingInspectionCount,
    int ServiceAcceptanceCount,
    int RejectedOrQuarantinedEvidenceCount);

internal sealed record ProjectCommercialProcurementSupplyReportSupplierPerformance(
    string PartyCode,
    string PartyName,
    PartyType PartyType,
    int IssuedOrderCount,
    int OpenOrderCount,
    int ClosedOrderCount,
    int CancelledOrderCount,
    int AssessableCompletedOrderCount,
    int OnTimeFulfilledCount,
    int LateFulfilledCount,
    int OverdueOpenCount,
    int ClosedShortCount,
    int NotAssessableCount,
    int GoodsReceiptCount,
    int PendingInspectionCount,
    int ReceiptWithRejectedOrQuarantinedCount,
    int ServiceAcceptanceCount,
    int ServiceAcceptanceWithRejectedCount,
    decimal? OnTimeFulfillmentRate);
