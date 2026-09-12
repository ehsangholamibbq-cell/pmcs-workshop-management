using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Commercial.Contracts;

public interface ICommercialStateSource
{
    Task<CommercialStateRecord?> GetCurrentAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, CommercialStateRecord>> GetPortfolioAsync(
        Guid tenantId,
        IReadOnlyCollection<ProjectControlProfile> projects,
        CancellationToken cancellationToken = default);
}

public sealed record CommercialStateRecord(
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
    DateTimeOffset? SourceMaxChangedAt);
