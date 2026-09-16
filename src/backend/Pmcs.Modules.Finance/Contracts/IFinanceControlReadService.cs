using Pmcs.Modules.Finance.Domain;

namespace Pmcs.Modules.Finance.Contracts;

public interface IFinanceControlReadService
{
    Task<FinanceControlStateRecord?> GetCurrentAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public sealed record FinanceControlStateRecord(
    FinancialStateRecord FinancialState,
    FinanceControlCalculation Control);

public interface IFinanceVerificationService
{
    Task<FinanceVerificationRecord?> VerifyAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public sealed record FinanceVerificationRecord(
    Guid ProjectId,
    string VerificationVersion,
    DateTimeOffset VerifiedAt,
    DateOnly AsOfDate,
    bool SnapshotMatchesIndependentCalculation,
    IReadOnlyCollection<string> CalculationMismatches,
    int InspectedFinancialRecordCount,
    int InspectedObligationCount,
    int InspectedPettyCashRequestCount,
    int InspectedManagementFeePolicyCount,
    int FinanceAuditEventCount,
    int RecordsWithoutAuditCount,
    int AuditEventsWithoutCorrelationCount,
    int InvalidLocationLinkCount,
    int InvalidCommercialLinkCount,
    bool Passed);
