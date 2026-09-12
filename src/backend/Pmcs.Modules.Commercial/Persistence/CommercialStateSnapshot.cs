using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Modules.Commercial.Persistence;

public sealed class CommercialStateSnapshot
{
    private CommercialStateSnapshot()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string CalculationVersion { get; private set; } = string.Empty;

    public DateOnly AsOfDate { get; private set; }

    public DateTimeOffset CalculatedAt { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public CommercialMetricState ContractState { get; private set; }

    public CommercialDataQualityStatus ContractDataQualityStatus { get; private set; }

    public CommercialMetricState ProcurementState { get; private set; }

    public CommercialDataQualityStatus ProcurementDataQualityStatus { get; private set; }

    public int ActivePartyCount { get; private set; }

    public int RegisteredContractCount { get; private set; }

    public int ActiveContractCount { get; private set; }

    public int ContractsWithoutCeilingCount { get; private set; }

    public int PendingContractApprovalCount { get; private set; }

    public int ExpiredActiveContractCount { get; private set; }

    public int ApprovedAmendmentCount { get; private set; }

    public decimal ApprovedAmendmentDelta { get; private set; }

    public decimal? ApprovedContractCeilingAmount { get; private set; }

    public int PurchaseRequestCount { get; private set; }

    public int PendingProcurementApprovalCount { get; private set; }

    public int ApprovedRequestsAwaitingOrderCount { get; private set; }

    public int OpenCommitmentCount { get; private set; }

    public int OverdueCommitmentCount { get; private set; }

    public decimal TotalCommittedAmount { get; private set; }

    public decimal OpenCommitmentAmount { get; private set; }

    public DateTimeOffset? SourceMaxChangedAt { get; private set; }

    public static CommercialStateSnapshot Create(Guid id, CommercialStateCalculation calculation) => new()
    {
        Id = id,
        TenantId = calculation.TenantId,
        ProjectId = calculation.ProjectId,
        CalculationVersion = calculation.CalculationVersion,
        AsOfDate = calculation.AsOfDate,
        CalculatedAt = calculation.CalculatedAt,
        CurrencyCode = calculation.CurrencyCode,
        ContractState = calculation.ContractState,
        ContractDataQualityStatus = calculation.ContractDataQualityStatus,
        ProcurementState = calculation.ProcurementState,
        ProcurementDataQualityStatus = calculation.ProcurementDataQualityStatus,
        ActivePartyCount = calculation.ActivePartyCount,
        RegisteredContractCount = calculation.RegisteredContractCount,
        ActiveContractCount = calculation.ActiveContractCount,
        ContractsWithoutCeilingCount = calculation.ContractsWithoutCeilingCount,
        PendingContractApprovalCount = calculation.PendingContractApprovalCount,
        ExpiredActiveContractCount = calculation.ExpiredActiveContractCount,
        ApprovedAmendmentCount = calculation.ApprovedAmendmentCount,
        ApprovedAmendmentDelta = calculation.ApprovedAmendmentDelta,
        ApprovedContractCeilingAmount = calculation.ApprovedContractCeilingAmount,
        PurchaseRequestCount = calculation.PurchaseRequestCount,
        PendingProcurementApprovalCount = calculation.PendingProcurementApprovalCount,
        ApprovedRequestsAwaitingOrderCount = calculation.ApprovedRequestsAwaitingOrderCount,
        OpenCommitmentCount = calculation.OpenCommitmentCount,
        OverdueCommitmentCount = calculation.OverdueCommitmentCount,
        TotalCommittedAmount = calculation.TotalCommittedAmount,
        OpenCommitmentAmount = calculation.OpenCommitmentAmount,
        SourceMaxChangedAt = calculation.SourceMaxChangedAt
    };
}
