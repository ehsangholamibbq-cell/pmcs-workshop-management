using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Modules.Commercial.Endpoints;

public sealed record CreatePartyRequest(
    Guid? ClientGeneratedId,
    string Code,
    string LegalName,
    PartyType Type,
    string? NationalId,
    string? ContactName,
    string? Phone);

public sealed record AmendPartyRequest(
    long BaseRevision,
    string LegalName,
    PartyType Type,
    string? NationalId,
    string? ContactName,
    string? Phone);

public sealed record SetPartyStatusRequest(long BaseRevision, PartyStatus Status);

public sealed record PartyResponse(
    Guid Id,
    Guid ProjectId,
    string Code,
    string LegalName,
    PartyType Type,
    string? NationalId,
    string? ContactName,
    string? Phone,
    PartyStatus Status,
    DateTimeOffset ChangedAt,
    long Revision)
{
    internal static PartyResponse From(Party item) => new(
        item.Id,
        item.ProjectId,
        item.Code,
        item.LegalName,
        item.Type,
        item.NationalId,
        item.ContactName,
        item.Phone,
        item.Status,
        item.ChangedAt,
        item.Revision);
}

public sealed record CreateProjectContractRequest(
    Guid? ClientGeneratedId,
    Guid PartyId,
    string Number,
    string Title,
    ProjectContractType Type,
    decimal? OriginalApprovedAmount,
    string? CurrencyCode,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Notes);

public sealed record AmendProjectContractRequest(
    long BaseRevision,
    Guid PartyId,
    string Number,
    string Title,
    ProjectContractType Type,
    decimal? OriginalApprovedAmount,
    string? CurrencyCode,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Notes);

public sealed record CommercialTransitionRequest(long BaseRevision, string? Comment);

public sealed record ProjectContractResponse(
    Guid Id,
    Guid ProjectId,
    Guid PartyId,
    string Number,
    string Title,
    ProjectContractType Type,
    decimal? OriginalApprovedAmount,
    string CurrencyCode,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Notes,
    ProjectContractStatus Status,
    DateTimeOffset ChangedAt,
    string? ReviewComment,
    long Revision)
{
    internal static ProjectContractResponse From(ProjectContract item) => new(
        item.Id,
        item.ProjectId,
        item.PartyId,
        item.Number,
        item.Title,
        item.Type,
        item.OriginalApprovedAmount,
        item.CurrencyCode,
        item.StartDate,
        item.EndDate,
        item.Notes,
        item.Status,
        item.ChangedAt,
        item.ReviewComment,
        item.Revision);
}

public sealed record CreateContractAmendmentRequest(
    Guid? ClientGeneratedId,
    Guid ContractId,
    string Number,
    string Title,
    ContractAmendmentType Type,
    decimal? AmountDelta,
    string? CurrencyCode,
    int? ExtensionDays,
    string? Notes);

public sealed record AmendContractAmendmentRequest(
    long BaseRevision,
    string Number,
    string Title,
    ContractAmendmentType Type,
    decimal? AmountDelta,
    string? CurrencyCode,
    int? ExtensionDays,
    string? Notes);

public sealed record ContractAmendmentResponse(
    Guid Id,
    Guid ProjectId,
    Guid ContractId,
    string Number,
    string Title,
    ContractAmendmentType Type,
    decimal? AmountDelta,
    string CurrencyCode,
    int? ExtensionDays,
    string? Notes,
    ContractAmendmentStatus Status,
    DateTimeOffset ChangedAt,
    string? ReviewComment,
    long Revision)
{
    internal static ContractAmendmentResponse From(ContractAmendment item) => new(
        item.Id,
        item.ProjectId,
        item.ContractId,
        item.Number,
        item.Title,
        item.Type,
        item.AmountDelta,
        item.CurrencyCode,
        item.ExtensionDays,
        item.Notes,
        item.Status,
        item.ChangedAt,
        item.ReviewComment,
        item.Revision);
}

public sealed record CreatePurchaseRequestRequest(
    Guid? ClientGeneratedId,
    string Number,
    string Title,
    string Description,
    decimal? EstimatedAmount,
    string? CurrencyCode,
    DateOnly? NeededByDate,
    Guid? SupplyItemId = null,
    decimal? RequestedQuantity = null,
    string? UnitCode = null,
    string? DeliveryLocation = null,
    string? WorkItemReference = null,
    string? WbsReference = null,
    string? BudgetLineReference = null,
    BudgetCheckStatus BudgetCheckStatus = BudgetCheckStatus.NotConfigured,
    ProcurementCriticality Criticality = ProcurementCriticality.Normal);

public sealed record AmendPurchaseRequestRequest(
    long BaseRevision,
    string Number,
    string Title,
    string Description,
    decimal? EstimatedAmount,
    string? CurrencyCode,
    DateOnly? NeededByDate,
    Guid? SupplyItemId = null,
    decimal? RequestedQuantity = null,
    string? UnitCode = null,
    string? DeliveryLocation = null,
    string? WorkItemReference = null,
    string? WbsReference = null,
    string? BudgetLineReference = null,
    BudgetCheckStatus BudgetCheckStatus = BudgetCheckStatus.NotConfigured,
    ProcurementCriticality Criticality = ProcurementCriticality.Normal);

public sealed record PurchaseRequestResponse(
    Guid Id,
    Guid ProjectId,
    string Number,
    string Title,
    string Description,
    decimal? EstimatedAmount,
    string CurrencyCode,
    DateOnly? NeededByDate,
    Guid? SupplyItemId,
    decimal? RequestedQuantity,
    string? UnitCode,
    string? DeliveryLocation,
    string? WorkItemReference,
    string? WbsReference,
    string? BudgetLineReference,
    BudgetCheckStatus BudgetCheckStatus,
    ProcurementCriticality Criticality,
    PurchaseRequestStatus Status,
    DateTimeOffset ChangedAt,
    string? ReviewComment,
    long Revision)
{
    internal static PurchaseRequestResponse From(PurchaseRequest item) => new(
        item.Id,
        item.ProjectId,
        item.Number,
        item.Title,
        item.Description,
        item.EstimatedAmount,
        item.CurrencyCode,
        item.NeededByDate,
        item.SupplyItemId,
        item.RequestedQuantity,
        item.UnitCode,
        item.DeliveryLocation,
        item.WorkItemReference,
        item.WbsReference,
        item.BudgetLineReference,
        item.BudgetCheckStatus,
        item.Criticality,
        item.Status,
        item.ChangedAt,
        item.ReviewComment,
        item.Revision);
}

public sealed record IssuePurchaseOrderRequest(
    Guid? ClientGeneratedId,
    Guid PurchaseRequestId,
    long PurchaseRequestRevision,
    Guid PartyId,
    Guid? ContractId,
    string Number,
    string Title,
    decimal Amount,
    string? CurrencyCode,
    DateOnly? DeliveryDueDate);

public sealed record PurchaseOrderResponse(
    Guid Id,
    Guid ProjectId,
    Guid PurchaseRequestId,
    Guid PartyId,
    Guid? ContractId,
    string Number,
    string Title,
    decimal Amount,
    string CurrencyCode,
    DateOnly? DeliveryDueDate,
    Guid? SupplyItemId,
    decimal? OrderedQuantity,
    string? UnitCode,
    string? DeliveryLocation,
    PurchaseOrderStatus Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset ChangedAt,
    string? ClosureComment,
    long Revision)
{
    internal static PurchaseOrderResponse From(PurchaseOrder item) => new(
        item.Id,
        item.ProjectId,
        item.PurchaseRequestId,
        item.PartyId,
        item.ContractId,
        item.Number,
        item.Title,
        item.Amount,
        item.CurrencyCode,
        item.DeliveryDueDate,
        item.SupplyItemId,
        item.OrderedQuantity,
        item.UnitCode,
        item.DeliveryLocation,
        item.Status,
        item.IssuedAt,
        item.ChangedAt,
        item.ClosureComment,
        item.Revision);
}

public sealed record CommercialStateResponse(
    Guid? SnapshotId,
    string CalculationVersion,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    string CurrencyCode,
    CommercialMetricState ContractState,
    CommercialDataQualityStatus ContractDataQualityStatus,
    CommercialMetricState ProcurementState,
    CommercialDataQualityStatus ProcurementDataQualityStatus,
    int ActivePartyCount,
    int RegisteredContractCount,
    int ActiveContractCount,
    int ContractsWithoutCeilingCount,
    int PendingContractApprovalCount,
    int ExpiredActiveContractCount,
    int ApprovedAmendmentCount,
    decimal ApprovedAmendmentDelta,
    decimal? ApprovedContractCeilingAmount,
    int PurchaseRequestCount,
    int PendingProcurementApprovalCount,
    int ApprovedRequestsAwaitingOrderCount,
    int OpenCommitmentCount,
    int OverdueCommitmentCount,
    decimal TotalCommittedAmount,
    decimal OpenCommitmentAmount,
    DateTimeOffset? SourceMaxChangedAt)
{
    public static CommercialStateResponse From(CommercialStateRecord item) => new(
        item.SnapshotId,
        item.CalculationVersion,
        item.AsOfDate,
        item.CalculatedAt,
        item.CurrencyCode,
        item.ContractState,
        item.ContractDataQualityStatus,
        item.ProcurementState,
        item.ProcurementDataQualityStatus,
        item.ActivePartyCount,
        item.RegisteredContractCount,
        item.ActiveContractCount,
        item.ContractsWithoutCeilingCount,
        item.PendingContractApprovalCount,
        item.ExpiredActiveContractCount,
        item.ApprovedAmendmentCount,
        item.ApprovedAmendmentDelta,
        item.ApprovedContractCeilingAmount,
        item.PurchaseRequestCount,
        item.PendingProcurementApprovalCount,
        item.ApprovedRequestsAwaitingOrderCount,
        item.OpenCommitmentCount,
        item.OverdueCommitmentCount,
        item.TotalCommittedAmount,
        item.OpenCommitmentAmount,
        item.SourceMaxChangedAt);
}

public sealed record PortfolioCommercialStateResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    CommercialStateResponse State);
