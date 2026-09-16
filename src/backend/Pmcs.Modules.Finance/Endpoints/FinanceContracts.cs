using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;

namespace Pmcs.Modules.Finance.Endpoints;

public sealed record CreateFinancialRecordRequest(
    Guid? ClientGeneratedId,
    FinancialRecordType Type,
    DateOnly TransactionDate,
    decimal Amount,
    string? CurrencyCode,
    string Description,
    string? Counterparty,
    string? DocumentNumber,
    string? ContractReference,
    Guid? ContractId,
    Guid? CommitmentId,
    string? CostCenterCode,
    Guid? PartyId = null,
    Guid? LocationId = null,
    string? WbsReference = null);

public sealed record SubmitFinanceItemRequest(long BaseRevision);

public sealed record AmendFinancialRecordRequest(
    long BaseRevision,
    FinancialRecordType Type,
    DateOnly TransactionDate,
    decimal Amount,
    string? CurrencyCode,
    string Description,
    string? Counterparty,
    string? DocumentNumber,
    string? ContractReference,
    Guid? ContractId,
    Guid? CommitmentId,
    string? CostCenterCode,
    Guid? PartyId = null,
    Guid? LocationId = null,
    string? WbsReference = null);

public sealed record ReviewFinanceItemRequest(long BaseRevision, string? Comment);

public sealed record FinancialRecordResponse(
    Guid Id,
    Guid ProjectId,
    FinancialRecordType Type,
    DateOnly TransactionDate,
    decimal Amount,
    string CurrencyCode,
    string Description,
    string? Counterparty,
    string? DocumentNumber,
    string? ContractReference,
    Guid? ContractId,
    Guid? CommitmentId,
    string? CostCenterCode,
    Guid? PartyId,
    Guid? LocationId,
    string? LocationCode,
    string? WbsReference,
    FinancialRecordStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    long Revision)
{
    internal static FinancialRecordResponse From(FinancialRecord item) => new(
        item.Id,
        item.ProjectId,
        item.Type,
        item.TransactionDate,
        item.Amount,
        item.CurrencyCode,
        item.Description,
        item.Counterparty,
        item.DocumentNumber,
        item.ContractReference,
        item.ContractId,
        item.CommitmentId,
        item.CostCenterCode,
        item.PartyId,
        item.LocationId,
        item.LocationCode,
        item.WbsReference,
        item.Status,
        item.CreatedBy,
        item.CreatedAt,
        item.SubmittedAt,
        item.ReviewedBy,
        item.ReviewedAt,
        item.ReviewComment,
        item.Revision);
}

public sealed record CreateFinancialObligationRequest(
    Guid? ClientGeneratedId,
    FinancialObligationType Type,
    string Number,
    string Description,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Amount,
    string? CurrencyCode,
    Guid? PartyId,
    string? Counterparty,
    Guid? ContractId,
    Guid? CommitmentId,
    string? CostCenterCode,
    string? WbsReference,
    Guid? LocationId);

public sealed record AmendFinancialObligationRequest(
    long BaseRevision,
    FinancialObligationType Type,
    string Number,
    string Description,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Amount,
    string? CurrencyCode,
    Guid? PartyId,
    string? Counterparty,
    Guid? ContractId,
    Guid? CommitmentId,
    string? CostCenterCode,
    string? WbsReference,
    Guid? LocationId);

public sealed record FinancialObligationResponse(
    Guid Id,
    Guid ProjectId,
    FinancialObligationType Type,
    string Number,
    string Description,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Amount,
    decimal SettledAmount,
    decimal OutstandingAmount,
    string CurrencyCode,
    Guid? PartyId,
    string? Counterparty,
    Guid? ContractId,
    Guid? CommitmentId,
    string? CostCenterCode,
    string? WbsReference,
    Guid? LocationId,
    string? LocationCode,
    FinancialObligationStatus Status,
    string? ReviewComment,
    long Revision)
{
    internal static FinancialObligationResponse From(FinancialObligation item) => new(
        item.Id,
        item.ProjectId,
        item.Type,
        item.Number,
        item.Description,
        item.IssueDate,
        item.DueDate,
        item.Amount,
        item.SettledAmount,
        item.OutstandingAmount,
        item.CurrencyCode,
        item.PartyId,
        item.Counterparty,
        item.ContractId,
        item.CommitmentId,
        item.CostCenterCode,
        item.WbsReference,
        item.LocationId,
        item.LocationCode,
        item.Status,
        item.ReviewComment,
        item.Revision);
}

public sealed record SettleFinancialObligationRequest(
    long BaseRevision,
    Guid FinancialRecordId,
    decimal Amount);

public sealed record CreatePettyCashRequest(
    Guid? ClientGeneratedId,
    string Number,
    string Purpose,
    string Custodian,
    DateOnly RequestDate,
    DateOnly ReconciliationDueDate,
    decimal RequestedAmount,
    string? CurrencyCode,
    Guid? LocationId,
    string? CostCenterCode,
    string? WbsReference);

public sealed record AmendPettyCashRequest(
    long BaseRevision,
    string Number,
    string Purpose,
    string Custodian,
    DateOnly RequestDate,
    DateOnly ReconciliationDueDate,
    decimal RequestedAmount,
    string? CurrencyCode,
    Guid? LocationId,
    string? CostCenterCode,
    string? WbsReference);

public sealed record ApprovePettyCashRequest(long BaseRevision, decimal ApprovedAmount, string? Comment);

public sealed record RecordPettyCashAdvanceRequest(long BaseRevision, Guid AdvanceRecordId);

public sealed record SubmitPettyCashReconciliationRequest(
    long BaseRevision,
    decimal ExpenseAmount,
    decimal ReturnedAmount,
    Guid ExpenseRecordId,
    Guid? ReturnRecordId);

public sealed record PettyCashRequestResponse(
    Guid Id,
    Guid ProjectId,
    string Number,
    string Purpose,
    string Custodian,
    DateOnly RequestDate,
    DateOnly ReconciliationDueDate,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    decimal? ReconciledExpenseAmount,
    decimal? ReturnedAmount,
    string CurrencyCode,
    Guid? LocationId,
    string? LocationCode,
    string? CostCenterCode,
    string? WbsReference,
    Guid? AdvanceRecordId,
    Guid? ExpenseRecordId,
    Guid? ReturnRecordId,
    PettyCashRequestStatus Status,
    string? ReviewComment,
    long Revision)
{
    internal static PettyCashRequestResponse From(PettyCashRequest item) => new(
        item.Id,
        item.ProjectId,
        item.Number,
        item.Purpose,
        item.Custodian,
        item.RequestDate,
        item.ReconciliationDueDate,
        item.RequestedAmount,
        item.ApprovedAmount,
        item.ReconciledExpenseAmount,
        item.ReturnedAmount,
        item.CurrencyCode,
        item.LocationId,
        item.LocationCode,
        item.CostCenterCode,
        item.WbsReference,
        item.AdvanceRecordId,
        item.ExpenseRecordId,
        item.ReturnRecordId,
        item.Status,
        item.ReviewComment,
        item.Revision);
}

public sealed record CreateManagementFeePolicyRequest(
    Guid? ClientGeneratedId,
    string Title,
    decimal RatePercent,
    ManagementFeeBase CalculationBase,
    DateOnly EffectiveFrom,
    string? Notes);

public sealed record AmendManagementFeePolicyRequest(
    long BaseRevision,
    string Title,
    decimal RatePercent,
    ManagementFeeBase CalculationBase,
    DateOnly EffectiveFrom,
    string? Notes);

public sealed record ManagementFeePolicyResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    decimal RatePercent,
    ManagementFeeBase CalculationBase,
    DateOnly EffectiveFrom,
    string? Notes,
    ManagementFeePolicyStatus Status,
    string? ReviewComment,
    long Revision)
{
    internal static ManagementFeePolicyResponse From(ManagementFeePolicy item) => new(
        item.Id,
        item.ProjectId,
        item.Title,
        item.RatePercent,
        item.CalculationBase,
        item.EffectiveFrom,
        item.Notes,
        item.Status,
        item.ReviewComment,
        item.Revision);
}

public sealed record FinanceControlStateResponse(
    FinancialStateResponse FinancialState,
    FinanceControlCalculation Control)
{
    public static FinanceControlStateResponse From(FinanceControlStateRecord state) => new(
        FinancialStateResponse.From(state.FinancialState),
        state.Control);
}

public sealed record CreateBudgetBaselineRequest(
    Guid? ClientGeneratedId,
    string Title,
    decimal Amount,
    string? CurrencyCode,
    string? Notes);

public sealed record AmendBudgetBaselineRequest(
    long BaseRevision,
    string Title,
    decimal Amount,
    string? CurrencyCode,
    string? Notes);

public sealed record BudgetBaselineResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    decimal Amount,
    string CurrencyCode,
    string? Notes,
    BudgetBaselineStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    long Revision)
{
    internal static BudgetBaselineResponse From(BudgetBaseline item) => new(
        item.Id,
        item.ProjectId,
        item.Title,
        item.Amount,
        item.CurrencyCode,
        item.Notes,
        item.Status,
        item.CreatedBy,
        item.CreatedAt,
        item.SubmittedAt,
        item.ReviewedBy,
        item.ReviewedAt,
        item.ReviewComment,
        item.Revision);
}

public sealed record FinancialStateResponse(
    Guid? SnapshotId,
    string CalculationVersion,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    string CurrencyCode,
    FinancialStateStatus Status,
    FinancialDataQualityStatus DataQualityStatus,
    BudgetComparisonState BudgetComparisonState,
    int PostedRecordCount,
    decimal TotalReceipts,
    decimal DirectPayments,
    decimal PettyCashFunding,
    decimal PettyCashExpenses,
    decimal ExternalNetCash,
    decimal RecognizedSpend,
    decimal PettyCashBalance,
    decimal? ApprovedBudgetAmount,
    decimal? BudgetRemainingAmount,
    decimal? BudgetConsumedPercent,
    DateTimeOffset? SourceMaxChangedAt)
{
    public static FinancialStateResponse From(FinancialStateRecord state) => new(
        state.SnapshotId,
        state.CalculationVersion,
        state.AsOfDate,
        state.CalculatedAt,
        state.CurrencyCode,
        state.Status,
        state.DataQualityStatus,
        state.BudgetComparisonState,
        state.PostedRecordCount,
        state.TotalReceipts,
        state.DirectPayments,
        state.PettyCashFunding,
        state.PettyCashExpenses,
        state.ExternalNetCash,
        state.RecognizedSpend,
        state.PettyCashBalance,
        state.ApprovedBudgetAmount,
        state.BudgetRemainingAmount,
        state.BudgetConsumedPercent,
        state.SourceMaxChangedAt);
}
