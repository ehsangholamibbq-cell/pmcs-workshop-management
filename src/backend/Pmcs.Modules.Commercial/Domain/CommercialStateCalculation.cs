using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Commercial.Domain;

public static class CommercialStateCalculator
{
    public const string CalculationVersion = "commercial-state-v1";

    public static CommercialStateCalculation Calculate(
        ProjectControlProfile project,
        IReadOnlyCollection<CommercialContractFact> contracts,
        IReadOnlyCollection<CommercialAmendmentFact> amendments,
        IReadOnlyCollection<CommercialRequestFact> requests,
        IReadOnlyCollection<CommercialOrderFact> orders,
        int activePartyCount,
        DateOnly asOfDate,
        DateTimeOffset calculatedAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(amendments);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(orders);

        EnsureCurrency(project.BaseCurrencyCode, contracts.Select(item => item.CurrencyCode));
        EnsureCurrency(project.BaseCurrencyCode, amendments.Select(item => item.CurrencyCode));
        EnsureCurrency(project.BaseCurrencyCode, requests.Select(item => item.CurrencyCode));
        EnsureCurrency(project.BaseCurrencyCode, orders.Select(item => item.CurrencyCode));

        var approvedAmendments = amendments.Where(item => item.Status == ContractAmendmentStatus.Approved).ToArray();
        var activeContracts = contracts.Where(item => item.Status == ProjectContractStatus.Active).ToArray();
        var knownContracts = contracts
            .Where(item => item.Status is ProjectContractStatus.Active or ProjectContractStatus.Closed)
            .Where(item => item.OriginalApprovedAmount.HasValue)
            .ToArray();
        var approvedDelta = approvedAmendments.Sum(item => item.AmountDelta ?? 0m);
        var approvedCeilings = knownContracts.Select(contract =>
            contract.OriginalApprovedAmount!.Value + approvedAmendments
                .Where(amendment => amendment.ContractId == contract.Id)
                .Sum(amendment => amendment.AmountDelta ?? 0m)).ToArray();
        if (approvedCeilings.Any(amount => amount < 0))
        {
            throw new InvalidOperationException("Approved amendments cannot reduce a known contract ceiling below zero.");
        }
        var pendingContractApprovals = contracts.Count(item => item.Status == ProjectContractStatus.Submitted) +
            amendments.Count(item => item.Status == ContractAmendmentStatus.Submitted);
        var expiredContracts = activeContracts.Count(item => item.EndDate.HasValue && item.EndDate.Value < asOfDate);

        var issuedOrders = orders.Where(item => item.Status == PurchaseOrderStatus.Issued).ToArray();
        var committedOrders = orders.Where(item => item.Status is PurchaseOrderStatus.Issued or PurchaseOrderStatus.Closed).ToArray();
        var overdueOrders = issuedOrders.Count(item => item.DeliveryDueDate.HasValue && item.DeliveryDueDate.Value < asOfDate);
        var pendingProcurementApprovals = requests.Count(item => item.Status == PurchaseRequestStatus.Submitted);
        var awaitingOrder = requests.Count(item => item.Status == PurchaseRequestStatus.Approved);

        return new CommercialStateCalculation(
            project.TenantId,
            project.Id,
            CalculationVersion,
            asOfDate,
            calculatedAt,
            project.BaseCurrencyCode,
            MapState(project.Contract, contracts.Count > 0 || amendments.Count > 0),
            contracts.Count == 0 && amendments.Count == 0
                ? CommercialDataQualityStatus.NoData
                : expiredContracts > 0
                    ? CommercialDataQualityStatus.NeedsAttention
                    : CommercialDataQualityStatus.Adequate,
            MapState(project.Procurement, requests.Count > 0 || orders.Count > 0),
            requests.Count == 0 && orders.Count == 0
                ? CommercialDataQualityStatus.NoData
                : overdueOrders > 0
                    ? CommercialDataQualityStatus.NeedsAttention
                    : CommercialDataQualityStatus.Adequate,
            activePartyCount,
            contracts.Count,
            activeContracts.Length,
            contracts.Count(item => item.OriginalApprovedAmount is null),
            pendingContractApprovals,
            expiredContracts,
            approvedAmendments.Length,
            approvedDelta,
            approvedCeilings.Length > 0 ? approvedCeilings.Sum() : null,
            requests.Count,
            pendingProcurementApprovals,
            awaitingOrder,
            issuedOrders.Length,
            overdueOrders,
            committedOrders.Sum(item => item.Amount),
            issuedOrders.Sum(item => item.Amount),
            MaxTimestamp(
                contracts.Select(item => item.ChangedAt)
                    .Concat(amendments.Select(item => item.ChangedAt))
                    .Concat(requests.Select(item => item.ChangedAt))
                    .Concat(orders.Select(item => item.ChangedAt))));
    }

    private static CommercialMetricState MapState(ProjectFeatureState state, bool hasData) => state switch
    {
        ProjectFeatureState.NotConfigured or ProjectFeatureState.NotEnabled => CommercialMetricState.NotConfigured,
        ProjectFeatureState.SetupRequired => CommercialMetricState.SetupRequired,
        ProjectFeatureState.Active => hasData ? CommercialMetricState.Available : CommercialMetricState.NoData,
        ProjectFeatureState.Suspended => CommercialMetricState.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported project feature state.")
    };

    private static void EnsureCurrency(string baseCurrency, IEnumerable<string> currencies)
    {
        if (currencies.Any(currency => !string.Equals(currency, baseCurrency, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Commercial state cannot aggregate currencies outside the project base currency.");
        }
    }

    private static DateTimeOffset? MaxTimestamp(IEnumerable<DateTimeOffset> values)
    {
        var available = values.ToArray();
        return available.Length == 0 ? null : available.Max();
    }
}

public sealed record CommercialContractFact(
    Guid Id,
    ProjectContractStatus Status,
    decimal? OriginalApprovedAmount,
    string CurrencyCode,
    DateOnly? EndDate,
    DateTimeOffset ChangedAt);

public sealed record CommercialAmendmentFact(
    Guid Id,
    Guid ContractId,
    ContractAmendmentStatus Status,
    decimal? AmountDelta,
    string CurrencyCode,
    DateTimeOffset ChangedAt);

public sealed record CommercialRequestFact(
    Guid Id,
    PurchaseRequestStatus Status,
    string CurrencyCode,
    DateTimeOffset ChangedAt);

public sealed record CommercialOrderFact(
    Guid Id,
    PurchaseOrderStatus Status,
    decimal Amount,
    string CurrencyCode,
    DateOnly? DeliveryDueDate,
    DateTimeOffset ChangedAt);

public sealed record CommercialStateCalculation(
    Guid TenantId,
    Guid ProjectId,
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

public enum CommercialMetricState
{
    NotConfigured = 1,
    SetupRequired = 2,
    NoData = 3,
    Available = 4,
    Suspended = 5
}

public enum CommercialDataQualityStatus
{
    NoData = 1,
    Adequate = 2,
    NeedsAttention = 3
}
