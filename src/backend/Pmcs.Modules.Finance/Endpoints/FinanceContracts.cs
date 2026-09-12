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
    string? CostCenterCode);

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
    string? CostCenterCode);

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
        item.Status,
        item.CreatedBy,
        item.CreatedAt,
        item.SubmittedAt,
        item.ReviewedBy,
        item.ReviewedAt,
        item.ReviewComment,
        item.Revision);
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
