using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class ProjectContract : AggregateRoot
{
    private ProjectContract()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid PartyId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public ProjectContractType Type { get; private set; }

    public decimal? OriginalApprovedAmount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public string? Notes { get; private set; }

    public ProjectContractStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public static ProjectContract Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid partyId,
        string number,
        string title,
        ProjectContractType type,
        decimal? originalApprovedAmount,
        string currencyCode,
        DateOnly? startDate,
        DateOnly? endDate,
        string? notes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        CommercialRules.Identity(id, tenantId, projectId, partyId, createdBy);
        ValidateTypeAndDates(type, startDate, endDate);
        return new ProjectContract
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            PartyId = partyId,
            Number = CommercialRules.Required(number, 80, "commercial.contract.number.invalid"),
            Title = CommercialRules.Required(title, 240, "commercial.contract.title.invalid"),
            Type = type,
            OriginalApprovedAmount = CommercialRules.OptionalPositiveAmount(
                originalApprovedAmount,
                "commercial.contract.amount.invalid"),
            CurrencyCode = CommercialRules.Currency(currencyCode),
            StartDate = startDate,
            EndDate = endDate,
            Notes = CommercialRules.Optional(notes, 2_000, "commercial.contract.notes.too_long"),
            Status = ProjectContractStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ChangedAt = createdAt
        };
    }

    public void Amend(
        long baseRevision,
        Guid partyId,
        string number,
        string title,
        ProjectContractType type,
        decimal? originalApprovedAmount,
        string currencyCode,
        DateOnly? startDate,
        DateOnly? endDate,
        string? notes,
        DateTimeOffset changedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        CommercialRules.Identity(partyId);
        ValidateTypeAndDates(type, startDate, endDate);
        PartyId = partyId;
        Number = CommercialRules.Required(number, 80, "commercial.contract.number.invalid");
        Title = CommercialRules.Required(title, 240, "commercial.contract.title.invalid");
        Type = type;
        OriginalApprovedAmount = CommercialRules.OptionalPositiveAmount(originalApprovedAmount, "commercial.contract.amount.invalid");
        CurrencyCode = CommercialRules.Currency(currencyCode);
        StartDate = startDate;
        EndDate = endDate;
        Notes = CommercialRules.Optional(notes, 2_000, "commercial.contract.notes.too_long");
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        Status = ProjectContractStatus.Submitted;
        SubmittedAt = submittedAt;
        ChangedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Activate(long baseRevision, Guid reviewedBy, DateTimeOffset reviewedAt, string? comment)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        CommercialRules.Identity(reviewedBy);
        Status = ProjectContractStatus.Active;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "commercial.contract.review_comment.too_long");
        ChangedAt = reviewedAt;
        AdvanceRevision();
    }

    public void Return(long baseRevision, Guid reviewedBy, DateTimeOffset reviewedAt, string? comment)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        CommercialRules.Identity(reviewedBy);
        Status = ProjectContractStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "commercial.contract.review_comment.too_long");
        ChangedAt = reviewedAt;
        AdvanceRevision();
    }

    public void Close(long baseRevision, Guid reviewedBy, DateTimeOffset closedAt, string? comment)
    {
        EnsureRevision(baseRevision);
        if (Status is not ProjectContractStatus.Active and not ProjectContractStatus.Suspended)
        {
            throw new DomainRuleException("commercial.contract.close.invalid_state", "Only an active or suspended contract can be closed.");
        }

        Status = ProjectContractStatus.Closed;
        ReviewedBy = reviewedBy;
        ReviewedAt = closedAt;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "commercial.contract.review_comment.too_long");
        ChangedAt = closedAt;
        AdvanceRevision();
    }

    private static void ValidateTypeAndDates(ProjectContractType type, DateOnly? startDate, DateOnly? endDate)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("commercial.contract.type.invalid", "Contract type is invalid.");
        }

        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
        {
            throw new DomainRuleException("commercial.contract.dates.invalid", "Contract end date cannot precede start date.");
        }
    }

    private void EnsureRevision(long baseRevision) =>
        CommercialRules.Revision(Revision, baseRevision, "commercial.contract.revision.conflict");

    private void EnsureEditable()
    {
        if (Status is not ProjectContractStatus.Draft and not ProjectContractStatus.Returned)
        {
            throw new DomainRuleException("commercial.contract.edit.invalid_state", "Only a draft or returned contract can be edited.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != ProjectContractStatus.Submitted)
        {
            throw new DomainRuleException("commercial.contract.review.invalid_state", "Only a submitted contract can be reviewed.");
        }
    }
}

public enum ProjectContractType
{
    MainContract = 1,
    Subcontract = 2,
    Supply = 3,
    ProfessionalService = 4,
    Labor = 5,
    Other = 6
}

public enum ProjectContractStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Active = 4,
    Suspended = 5,
    Closed = 6,
    Terminated = 7
}
