using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class ContractAmendment : AggregateRoot
{
    private ContractAmendment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid ContractId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public ContractAmendmentType Type { get; private set; }

    public decimal? AmountDelta { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public int? ExtensionDays { get; private set; }

    public string? Notes { get; private set; }

    public ContractAmendmentStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public static ContractAmendment Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid contractId,
        string number,
        string title,
        ContractAmendmentType type,
        decimal? amountDelta,
        string currencyCode,
        int? extensionDays,
        string? notes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        CommercialRules.Identity(id, tenantId, projectId, contractId, createdBy);
        ValidateChange(type, amountDelta, extensionDays);
        return new ContractAmendment
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            ContractId = contractId,
            Number = CommercialRules.Required(number, 80, "commercial.amendment.number.invalid"),
            Title = CommercialRules.Required(title, 240, "commercial.amendment.title.invalid"),
            Type = type,
            AmountDelta = amountDelta,
            CurrencyCode = CommercialRules.Currency(currencyCode),
            ExtensionDays = extensionDays,
            Notes = CommercialRules.Optional(notes, 2_000, "commercial.amendment.notes.too_long"),
            Status = ContractAmendmentStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ChangedAt = createdAt
        };
    }

    public void Amend(
        long baseRevision,
        string number,
        string title,
        ContractAmendmentType type,
        decimal? amountDelta,
        string currencyCode,
        int? extensionDays,
        string? notes,
        DateTimeOffset changedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        ValidateChange(type, amountDelta, extensionDays);
        Number = CommercialRules.Required(number, 80, "commercial.amendment.number.invalid");
        Title = CommercialRules.Required(title, 240, "commercial.amendment.title.invalid");
        Type = type;
        AmountDelta = amountDelta;
        CurrencyCode = CommercialRules.Currency(currencyCode);
        ExtensionDays = extensionDays;
        Notes = CommercialRules.Optional(notes, 2_000, "commercial.amendment.notes.too_long");
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        Status = ContractAmendmentStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        ChangedAt = submittedAt;
        AdvanceRevision();
    }

    public void Approve(long baseRevision, Guid reviewedBy, DateTimeOffset reviewedAt, string? comment)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = ContractAmendmentStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "commercial.amendment.review_comment.too_long");
        ChangedAt = reviewedAt;
        AdvanceRevision();
    }

    public void Return(long baseRevision, Guid reviewedBy, DateTimeOffset reviewedAt, string? comment)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = ContractAmendmentStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "commercial.amendment.review_comment.too_long");
        ChangedAt = reviewedAt;
        AdvanceRevision();
    }

    private static void ValidateChange(ContractAmendmentType type, decimal? amountDelta, int? extensionDays)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("commercial.amendment.type.invalid", "Amendment type is invalid.");
        }

        if (amountDelta.HasValue && decimal.Round(amountDelta.Value, 2, MidpointRounding.AwayFromZero) != amountDelta)
        {
            throw new DomainRuleException("commercial.amendment.amount.invalid", "Amount delta may have at most two decimal places.");
        }

        if (extensionDays is < 0)
        {
            throw new DomainRuleException("commercial.amendment.extension.invalid", "Extension days cannot be negative.");
        }

        if (type == ContractAmendmentType.ValueChange && amountDelta is null or 0)
        {
            throw new DomainRuleException("commercial.amendment.amount.required", "A value amendment requires a non-zero amount delta.");
        }

        if (type == ContractAmendmentType.TimeExtension && extensionDays is null or 0)
        {
            throw new DomainRuleException("commercial.amendment.extension.required", "A time extension requires extension days.");
        }

        if (type == ContractAmendmentType.Mixed && (amountDelta is null or 0 || extensionDays is null or 0))
        {
            throw new DomainRuleException("commercial.amendment.mixed.required", "A mixed amendment requires value and time changes.");
        }
    }

    private void EnsureRevision(long baseRevision) =>
        CommercialRules.Revision(Revision, baseRevision, "commercial.amendment.revision.conflict");

    private void EnsureEditable()
    {
        if (Status is not ContractAmendmentStatus.Draft and not ContractAmendmentStatus.Returned)
        {
            throw new DomainRuleException("commercial.amendment.edit.invalid_state", "Only a draft or returned amendment can be edited.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != ContractAmendmentStatus.Submitted)
        {
            throw new DomainRuleException("commercial.amendment.review.invalid_state", "Only a submitted amendment can be reviewed.");
        }
    }
}

public enum ContractAmendmentType
{
    ScopeChange = 1,
    ValueChange = 2,
    TimeExtension = 3,
    Mixed = 4
}

public enum ContractAmendmentStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4
}
