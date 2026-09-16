using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Finance.Domain;

public sealed class FinancialObligation : AggregateRoot
{
    private FinancialObligation()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public FinancialObligationType Type { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateOnly IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public decimal SettledAmount { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public Guid? PartyId { get; private set; }
    public string? Counterparty { get; private set; }
    public Guid? ContractId { get; private set; }
    public Guid? CommitmentId { get; private set; }
    public string? CostCenterCode { get; private set; }
    public string? WbsReference { get; private set; }
    public Guid? LocationId { get; private set; }
    public string? LocationCode { get; private set; }
    public FinancialObligationStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }

    public decimal OutstandingAmount => Amount - SettledAmount;

    public static FinancialObligation Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        FinancialObligationType type,
        string number,
        string description,
        DateOnly issueDate,
        DateOnly dueDate,
        decimal amount,
        string currencyCode,
        Guid? partyId,
        string? counterparty,
        Guid? contractId,
        Guid? commitmentId,
        string? costCenterCode,
        string? wbsReference,
        Guid? locationId,
        string? locationCode,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        FinanceRules.Identity("finance.obligation.identity.required", id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("finance.obligation.type.invalid", "Obligation type is invalid.");
        }

        ValidateDates(issueDate, dueDate);
        ValidateOptionalIdentity(partyId, "finance.obligation.party.invalid");
        ValidateOptionalIdentity(contractId, "finance.obligation.contract.invalid");
        ValidateOptionalIdentity(commitmentId, "finance.obligation.commitment.invalid");
        ValidateOptionalIdentity(locationId, "finance.obligation.location.invalid");

        return new FinancialObligation
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Type = type,
            Number = FinanceRules.Required(number, 80, "finance.obligation.number.invalid"),
            Description = FinanceRules.Required(description, 1_000, "finance.obligation.description.invalid"),
            IssueDate = issueDate,
            DueDate = dueDate,
            Amount = FinanceRules.PositiveMoney(amount, "finance.obligation.amount.invalid"),
            CurrencyCode = FinanceRules.Currency(currencyCode, "finance.obligation.currency.invalid"),
            PartyId = partyId,
            Counterparty = FinanceRules.Optional(counterparty, 200, "finance.obligation.counterparty.too_long"),
            ContractId = contractId,
            CommitmentId = commitmentId,
            CostCenterCode = FinanceRules.Optional(costCenterCode, 120, "finance.obligation.cost_center.too_long"),
            WbsReference = FinanceRules.Optional(wbsReference, 240, "finance.obligation.wbs.too_long"),
            LocationId = locationId,
            LocationCode = FinanceRules.Optional(locationCode, 80, "finance.obligation.location_code.too_long"),
            Status = FinancialObligationStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.obligation.revision.conflict");
        if (Status is not FinancialObligationStatus.Draft and not FinancialObligationStatus.Returned)
        {
            throw new DomainRuleException("finance.obligation.submit.invalid_state", "Only a draft or returned obligation can be submitted.");
        }

        Status = FinancialObligationStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Amend(
        long baseRevision,
        FinancialObligationType type,
        string number,
        string description,
        DateOnly issueDate,
        DateOnly dueDate,
        decimal amount,
        string currencyCode,
        Guid? partyId,
        string? counterparty,
        Guid? contractId,
        Guid? commitmentId,
        string? costCenterCode,
        string? wbsReference,
        Guid? locationId,
        string? locationCode)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.obligation.revision.conflict");
        if (Status is not FinancialObligationStatus.Draft and not FinancialObligationStatus.Returned)
        {
            throw new DomainRuleException("finance.obligation.amend.invalid_state", "Only a draft or returned obligation can be amended.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("finance.obligation.type.invalid", "Obligation type is invalid.");
        }

        ValidateDates(issueDate, dueDate);
        ValidateOptionalIdentity(partyId, "finance.obligation.party.invalid");
        ValidateOptionalIdentity(contractId, "finance.obligation.contract.invalid");
        ValidateOptionalIdentity(commitmentId, "finance.obligation.commitment.invalid");
        ValidateOptionalIdentity(locationId, "finance.obligation.location.invalid");
        Type = type;
        Number = FinanceRules.Required(number, 80, "finance.obligation.number.invalid");
        Description = FinanceRules.Required(description, 1_000, "finance.obligation.description.invalid");
        IssueDate = issueDate;
        DueDate = dueDate;
        Amount = FinanceRules.PositiveMoney(amount, "finance.obligation.amount.invalid");
        CurrencyCode = FinanceRules.Currency(currencyCode, "finance.obligation.currency.invalid");
        PartyId = partyId;
        Counterparty = FinanceRules.Optional(counterparty, 200, "finance.obligation.counterparty.too_long");
        ContractId = contractId;
        CommitmentId = commitmentId;
        CostCenterCode = FinanceRules.Optional(costCenterCode, 120, "finance.obligation.cost_center.too_long");
        WbsReference = FinanceRules.Optional(wbsReference, 240, "finance.obligation.wbs.too_long");
        LocationId = locationId;
        LocationCode = FinanceRules.Optional(locationCode, 80, "finance.obligation.location_code.too_long");
        AdvanceRevision();
    }

    public void Approve(long baseRevision, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.obligation.revision.conflict");
        EnsureSubmitted();
        FinanceRules.Identity("finance.obligation.reviewer.required", reviewedBy);
        Status = FinancialObligationStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = FinanceRules.Optional(comment, 1_000, "finance.obligation.review_comment.too_long");
        AdvanceRevision();
    }

    public void ReturnForCorrection(long baseRevision, string reason, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.obligation.revision.conflict");
        EnsureSubmitted();
        FinanceRules.Identity("finance.obligation.reviewer.required", reviewedBy);
        Status = FinancialObligationStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = FinanceRules.Required(reason, 1_000, "finance.obligation.return_reason.invalid");
        AdvanceRevision();
    }

    public void ApplySettlement(long baseRevision, decimal amount, DateTimeOffset settledAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.obligation.revision.conflict");
        if (Status is not FinancialObligationStatus.Approved and not FinancialObligationStatus.PartiallySettled)
        {
            throw new DomainRuleException("finance.obligation.settle.invalid_state", "Only an approved open obligation can be settled.");
        }

        var normalized = FinanceRules.PositiveMoney(amount, "finance.obligation.settlement.amount.invalid");
        if (normalized > OutstandingAmount)
        {
            throw new DomainRuleException("finance.obligation.settlement.exceeds_outstanding", "Settlement cannot exceed the outstanding amount.");
        }

        SettledAmount += normalized;
        SettledAt = settledAt;
        Status = SettledAmount == Amount
            ? FinancialObligationStatus.Settled
            : FinancialObligationStatus.PartiallySettled;
        AdvanceRevision();
    }

    private void EnsureSubmitted()
    {
        if (Status != FinancialObligationStatus.Submitted)
        {
            throw new DomainRuleException("finance.obligation.review.invalid_state", "Only a submitted obligation can be reviewed.");
        }
    }

    private static void ValidateDates(DateOnly issueDate, DateOnly dueDate)
    {
        if (issueDate == default || dueDate == default || dueDate < issueDate)
        {
            throw new DomainRuleException("finance.obligation.dates.invalid", "Due date must be on or after the issue date.");
        }
    }

    private static void ValidateOptionalIdentity(Guid? value, string code)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleException(code, "Optional identity cannot be empty.");
        }
    }
}

public enum FinancialObligationType
{
    Payable = 1,
    Receivable = 2
}

public enum FinancialObligationStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    PartiallySettled = 5,
    Settled = 6
}

public sealed class FinancialSettlement
{
    private FinancialSettlement()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ObligationId { get; private set; }
    public Guid FinancialRecordId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset SettledAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    public static FinancialSettlement Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid obligationId,
        Guid financialRecordId,
        decimal amount,
        DateTimeOffset settledAt,
        Guid createdBy)
    {
        FinanceRules.Identity(
            "finance.settlement.identity.required",
            id,
            tenantId,
            projectId,
            obligationId,
            financialRecordId,
            createdBy);
        return new FinancialSettlement
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            ObligationId = obligationId,
            FinancialRecordId = financialRecordId,
            Amount = FinanceRules.PositiveMoney(amount, "finance.settlement.amount.invalid"),
            SettledAt = settledAt,
            CreatedBy = createdBy
        };
    }
}
