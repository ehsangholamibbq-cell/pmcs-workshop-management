using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Finance.Domain;

public sealed class FinancialRecord : AggregateRoot
{
    private FinancialRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public FinancialRecordType Type { get; private set; }

    public DateOnly TransactionDate { get; private set; }

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? Counterparty { get; private set; }

    public string? DocumentNumber { get; private set; }

    public string? ContractReference { get; private set; }

    public Guid? ContractId { get; private set; }

    public Guid? CommitmentId { get; private set; }

    public string? CostCenterCode { get; private set; }

    public Guid? PartyId { get; private set; }

    public Guid? LocationId { get; private set; }

    public string? LocationCode { get; private set; }

    public string? WbsReference { get; private set; }

    public FinancialRecordStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public static FinancialRecord Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        FinancialRecordType type,
        DateOnly transactionDate,
        decimal amount,
        string currencyCode,
        string description,
        string? counterparty,
        string? documentNumber,
        string? contractReference,
        Guid? contractId,
        Guid? commitmentId,
        string? costCenterCode,
        Guid createdBy,
        DateTimeOffset createdAt,
        Guid? partyId = null,
        Guid? locationId = null,
        string? locationCode = null,
        string? wbsReference = null)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("finance.record.identity.required", "Record, tenant, project and creator ids are required.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("finance.record.type.invalid", "Financial record type is invalid.");
        }

        if (transactionDate == default)
        {
            throw new DomainRuleException("finance.record.date.required", "Transaction date is required.");
        }

        if (amount <= 0 || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            throw new DomainRuleException("finance.record.amount.invalid", "Amount must be positive and have at most two decimal places.");
        }

        ValidateOptionalIdentity(partyId, "finance.record.party.invalid");
        ValidateOptionalIdentity(locationId, "finance.record.location.invalid");

        return new FinancialRecord
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Type = type,
            TransactionDate = transactionDate,
            Amount = amount,
            CurrencyCode = NormalizeCurrency(currencyCode),
            Description = Required(description, 1_000, "finance.record.description.invalid"),
            Counterparty = Optional(counterparty, 200, "finance.record.counterparty.too_long"),
            DocumentNumber = Optional(documentNumber, 120, "finance.record.document_number.too_long"),
            ContractReference = Optional(contractReference, 120, "finance.record.contract_reference.too_long"),
            ContractId = contractId,
            CommitmentId = commitmentId,
            CostCenterCode = Optional(costCenterCode, 120, "finance.record.cost_center.too_long"),
            PartyId = partyId,
            LocationId = locationId,
            LocationCode = Optional(locationCode, 80, "finance.record.location_code.too_long"),
            WbsReference = Optional(wbsReference, 240, "finance.record.wbs.too_long"),
            Status = FinancialRecordStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        Status = FinancialRecordStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Amend(
        long baseRevision,
        FinancialRecordType type,
        DateOnly transactionDate,
        decimal amount,
        string currencyCode,
        string description,
        string? counterparty,
        string? documentNumber,
        string? contractReference,
        Guid? contractId,
        Guid? commitmentId,
        string? costCenterCode,
        Guid? partyId = null,
        Guid? locationId = null,
        string? locationCode = null,
        string? wbsReference = null)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("finance.record.type.invalid", "Financial record type is invalid.");
        }

        if (transactionDate == default)
        {
            throw new DomainRuleException("finance.record.date.required", "Transaction date is required.");
        }

        if (amount <= 0 || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            throw new DomainRuleException("finance.record.amount.invalid", "Amount must be positive and have at most two decimal places.");
        }

        ValidateOptionalIdentity(partyId, "finance.record.party.invalid");
        ValidateOptionalIdentity(locationId, "finance.record.location.invalid");

        Type = type;
        TransactionDate = transactionDate;
        Amount = amount;
        CurrencyCode = NormalizeCurrency(currencyCode);
        Description = Required(description, 1_000, "finance.record.description.invalid");
        Counterparty = Optional(counterparty, 200, "finance.record.counterparty.too_long");
        DocumentNumber = Optional(documentNumber, 120, "finance.record.document_number.too_long");
        ContractReference = Optional(contractReference, 120, "finance.record.contract_reference.too_long");
        ContractId = contractId;
        CommitmentId = commitmentId;
        CostCenterCode = Optional(costCenterCode, 120, "finance.record.cost_center.too_long");
        PartyId = partyId;
        LocationId = locationId;
        LocationCode = Optional(locationCode, 80, "finance.record.location_code.too_long");
        WbsReference = Optional(wbsReference, 240, "finance.record.wbs.too_long");
        AdvanceRevision();
    }

    public void Post(long baseRevision, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        if (reviewedBy == Guid.Empty)
        {
            throw new DomainRuleException("finance.record.reviewer.required", "Reviewer is required.");
        }

        Status = FinancialRecordStatus.Posted;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = Optional(comment, 1_000, "finance.record.review_comment.too_long");
        AdvanceRevision();
    }

    public void ReturnForCorrection(long baseRevision, string reason, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        if (reviewedBy == Guid.Empty)
        {
            throw new DomainRuleException("finance.record.reviewer.required", "Reviewer is required.");
        }

        Status = FinancialRecordStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = Required(reason, 1_000, "finance.record.return_reason.invalid");
        AdvanceRevision();
    }

    private void EnsureEditable()
    {
        if (Status is not FinancialRecordStatus.Draft and not FinancialRecordStatus.Returned)
        {
            throw new DomainRuleException("finance.record.submit.invalid_state", "Only a draft or returned record can be submitted.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != FinancialRecordStatus.Submitted)
        {
            throw new DomainRuleException("finance.record.review.invalid_state", "Only a submitted record can be reviewed.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException("finance.record.revision.conflict", "The financial record changed after it was loaded.");
        }
    }

    private static string NormalizeCurrency(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new DomainRuleException("finance.record.currency.invalid", "Currency must be a three-letter ISO-style code.");
        }

        return normalized;
    }

    private static string Required(string value, int maximumLength, string errorCode) =>
        Optional(value, maximumLength, errorCode) ??
        throw new DomainRuleException(errorCode, "A value is required.");

    private static string? Optional(string? value, int maximumLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(errorCode, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    private static void ValidateOptionalIdentity(Guid? value, string errorCode)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleException(errorCode, "Optional identity cannot be empty.");
        }
    }
}

public enum FinancialRecordType
{
    Receipt = 1,
    Payment = 2,
    PettyCashFunding = 3,
    PettyCashExpense = 4
}

public enum FinancialRecordStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Posted = 4
}
