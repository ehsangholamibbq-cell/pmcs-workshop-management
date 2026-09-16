using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Finance.Domain;

public sealed class PettyCashRequest : AggregateRoot
{
    private PettyCashRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string Custodian { get; private set; } = string.Empty;
    public DateOnly RequestDate { get; private set; }
    public DateOnly ReconciliationDueDate { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public decimal? ReconciledExpenseAmount { get; private set; }
    public decimal? ReturnedAmount { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public Guid? LocationId { get; private set; }
    public string? LocationCode { get; private set; }
    public string? CostCenterCode { get; private set; }
    public string? WbsReference { get; private set; }
    public Guid? AdvanceRecordId { get; private set; }
    public Guid? ExpenseRecordId { get; private set; }
    public Guid? ReturnRecordId { get; private set; }
    public PettyCashRequestStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }
    public DateTimeOffset? AdvancedAt { get; private set; }
    public DateTimeOffset? ReconciliationSubmittedAt { get; private set; }
    public DateTimeOffset? ReconciledAt { get; private set; }

    public static PettyCashRequest Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string number,
        string purpose,
        string custodian,
        DateOnly requestDate,
        DateOnly reconciliationDueDate,
        decimal requestedAmount,
        string currencyCode,
        Guid? locationId,
        string? locationCode,
        string? costCenterCode,
        string? wbsReference,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        FinanceRules.Identity("finance.petty_cash.identity.required", id, tenantId, projectId, createdBy);
        if (requestDate == default || reconciliationDueDate == default || reconciliationDueDate < requestDate)
        {
            throw new DomainRuleException("finance.petty_cash.dates.invalid", "Reconciliation due date must be on or after the request date.");
        }

        if (locationId == Guid.Empty)
        {
            throw new DomainRuleException("finance.petty_cash.location.invalid", "Optional location id cannot be empty.");
        }

        return new PettyCashRequest
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Number = FinanceRules.Required(number, 80, "finance.petty_cash.number.invalid"),
            Purpose = FinanceRules.Required(purpose, 1_000, "finance.petty_cash.purpose.invalid"),
            Custodian = FinanceRules.Required(custodian, 200, "finance.petty_cash.custodian.invalid"),
            RequestDate = requestDate,
            ReconciliationDueDate = reconciliationDueDate,
            RequestedAmount = FinanceRules.PositiveMoney(requestedAmount, "finance.petty_cash.amount.invalid"),
            CurrencyCode = FinanceRules.Currency(currencyCode, "finance.petty_cash.currency.invalid"),
            LocationId = locationId,
            LocationCode = FinanceRules.Optional(locationCode, 80, "finance.petty_cash.location_code.too_long"),
            CostCenterCode = FinanceRules.Optional(costCenterCode, 120, "finance.petty_cash.cost_center.too_long"),
            WbsReference = FinanceRules.Optional(wbsReference, 240, "finance.petty_cash.wbs.too_long"),
            Status = PettyCashRequestStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.petty_cash.revision.conflict");
        if (Status is not PettyCashRequestStatus.Draft and not PettyCashRequestStatus.Returned)
        {
            throw new DomainRuleException("finance.petty_cash.submit.invalid_state", "Only a draft or returned request can be submitted.");
        }

        Status = PettyCashRequestStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Amend(
        long baseRevision,
        string number,
        string purpose,
        string custodian,
        DateOnly requestDate,
        DateOnly reconciliationDueDate,
        decimal requestedAmount,
        string currencyCode,
        Guid? locationId,
        string? locationCode,
        string? costCenterCode,
        string? wbsReference)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.petty_cash.revision.conflict");
        if (Status is not PettyCashRequestStatus.Draft and not PettyCashRequestStatus.Returned)
        {
            throw new DomainRuleException("finance.petty_cash.amend.invalid_state", "Only a draft or returned request can be amended.");
        }

        if (requestDate == default || reconciliationDueDate == default || reconciliationDueDate < requestDate)
        {
            throw new DomainRuleException("finance.petty_cash.dates.invalid", "Reconciliation due date must be on or after the request date.");
        }

        if (locationId == Guid.Empty)
        {
            throw new DomainRuleException("finance.petty_cash.location.invalid", "Optional location id cannot be empty.");
        }

        Number = FinanceRules.Required(number, 80, "finance.petty_cash.number.invalid");
        Purpose = FinanceRules.Required(purpose, 1_000, "finance.petty_cash.purpose.invalid");
        Custodian = FinanceRules.Required(custodian, 200, "finance.petty_cash.custodian.invalid");
        RequestDate = requestDate;
        ReconciliationDueDate = reconciliationDueDate;
        RequestedAmount = FinanceRules.PositiveMoney(requestedAmount, "finance.petty_cash.amount.invalid");
        CurrencyCode = FinanceRules.Currency(currencyCode, "finance.petty_cash.currency.invalid");
        LocationId = locationId;
        LocationCode = FinanceRules.Optional(locationCode, 80, "finance.petty_cash.location_code.too_long");
        CostCenterCode = FinanceRules.Optional(costCenterCode, 120, "finance.petty_cash.cost_center.too_long");
        WbsReference = FinanceRules.Optional(wbsReference, 240, "finance.petty_cash.wbs.too_long");
        AdvanceRevision();
    }

    public void Approve(long baseRevision, decimal approvedAmount, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.petty_cash.revision.conflict");
        if (Status != PettyCashRequestStatus.Submitted)
        {
            throw new DomainRuleException("finance.petty_cash.approve.invalid_state", "Only a submitted request can be approved.");
        }

        FinanceRules.Identity("finance.petty_cash.reviewer.required", reviewedBy);
        var normalized = FinanceRules.PositiveMoney(approvedAmount, "finance.petty_cash.approved_amount.invalid");
        if (normalized > RequestedAmount)
        {
            throw new DomainRuleException("finance.petty_cash.approved_amount.exceeds_requested", "Approved amount cannot exceed requested amount.");
        }

        ApprovedAmount = normalized;
        Status = PettyCashRequestStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = FinanceRules.Optional(comment, 1_000, "finance.petty_cash.review_comment.too_long");
        AdvanceRevision();
    }

    public void ReturnForCorrection(long baseRevision, string reason, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.petty_cash.revision.conflict");
        if (Status != PettyCashRequestStatus.Submitted)
        {
            throw new DomainRuleException("finance.petty_cash.return.invalid_state", "Only a submitted request can be returned.");
        }

        FinanceRules.Identity("finance.petty_cash.reviewer.required", reviewedBy);
        Status = PettyCashRequestStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = FinanceRules.Required(reason, 1_000, "finance.petty_cash.return_reason.invalid");
        AdvanceRevision();
    }

    public void RecordAdvance(long baseRevision, Guid advanceRecordId, DateTimeOffset advancedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.petty_cash.revision.conflict");
        if (Status != PettyCashRequestStatus.Approved)
        {
            throw new DomainRuleException("finance.petty_cash.advance.invalid_state", "Only an approved request can receive an advance.");
        }

        FinanceRules.Identity("finance.petty_cash.advance_record.required", advanceRecordId);
        AdvanceRecordId = advanceRecordId;
        AdvancedAt = advancedAt;
        Status = PettyCashRequestStatus.Advanced;
        AdvanceRevision();
    }

    public void SubmitReconciliation(
        long baseRevision,
        decimal expenseAmount,
        decimal returnedAmount,
        Guid expenseRecordId,
        Guid? returnRecordId,
        DateTimeOffset submittedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.petty_cash.revision.conflict");
        if (Status != PettyCashRequestStatus.Advanced || !ApprovedAmount.HasValue)
        {
            throw new DomainRuleException("finance.petty_cash.reconcile.invalid_state", "Only an advanced request can be reconciled.");
        }

        var expense = FinanceRules.NonNegativeMoney(expenseAmount, "finance.petty_cash.expense.invalid");
        var returned = FinanceRules.NonNegativeMoney(returnedAmount, "finance.petty_cash.returned.invalid");
        if (expense + returned != ApprovedAmount.Value)
        {
            throw new DomainRuleException("finance.petty_cash.reconcile.unbalanced", "Expense plus returned cash must equal the approved advance.");
        }

        if (expense > 0)
        {
            FinanceRules.Identity("finance.petty_cash.expense_record.required", expenseRecordId);
        }
        else if (expenseRecordId != Guid.Empty)
        {
            throw new DomainRuleException("finance.petty_cash.expense_record.unexpected", "Expense record must be empty when expense amount is zero.");
        }

        if ((returned > 0) != returnRecordId.HasValue || returnRecordId == Guid.Empty)
        {
            throw new DomainRuleException("finance.petty_cash.return_record.mismatch", "A return record is required only when returned cash is positive.");
        }

        ReconciledExpenseAmount = expense;
        ReturnedAmount = returned;
        ExpenseRecordId = expense == 0 ? null : expenseRecordId;
        ReturnRecordId = returnRecordId;
        ReconciliationSubmittedAt = submittedAt;
        Status = PettyCashRequestStatus.ReconciliationSubmitted;
        AdvanceRevision();
    }

    public void ApproveReconciliation(long baseRevision, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.petty_cash.revision.conflict");
        if (Status != PettyCashRequestStatus.ReconciliationSubmitted)
        {
            throw new DomainRuleException("finance.petty_cash.reconciliation_review.invalid_state", "Only a submitted reconciliation can be approved.");
        }

        FinanceRules.Identity("finance.petty_cash.reviewer.required", reviewedBy);
        Status = PettyCashRequestStatus.Reconciled;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = FinanceRules.Optional(comment, 1_000, "finance.petty_cash.review_comment.too_long");
        ReconciledAt = reviewedAt;
        AdvanceRevision();
    }
}

public enum PettyCashRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Advanced = 5,
    ReconciliationSubmitted = 6,
    Reconciled = 7
}
