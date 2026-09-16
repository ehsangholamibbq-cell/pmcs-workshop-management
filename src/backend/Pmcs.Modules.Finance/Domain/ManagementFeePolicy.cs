using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Finance.Domain;

public sealed class ManagementFeePolicy : AggregateRoot
{
    private ManagementFeePolicy()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal RatePercent { get; private set; }
    public ManagementFeeBase CalculationBase { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public string? Notes { get; private set; }
    public ManagementFeePolicyStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }

    public static ManagementFeePolicy Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string title,
        decimal ratePercent,
        ManagementFeeBase calculationBase,
        DateOnly effectiveFrom,
        string? notes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        FinanceRules.Identity("finance.management_fee.identity.required", id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(calculationBase))
        {
            throw new DomainRuleException("finance.management_fee.base.invalid", "Management fee calculation base is invalid.");
        }

        if (effectiveFrom == default)
        {
            throw new DomainRuleException("finance.management_fee.effective_date.required", "Effective date is required.");
        }

        return new ManagementFeePolicy
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Title = FinanceRules.Required(title, 200, "finance.management_fee.title.invalid"),
            RatePercent = ValidateRate(ratePercent),
            CalculationBase = calculationBase,
            EffectiveFrom = effectiveFrom,
            Notes = FinanceRules.Optional(notes, 2_000, "finance.management_fee.notes.too_long"),
            Status = ManagementFeePolicyStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.management_fee.revision.conflict");
        if (Status is not ManagementFeePolicyStatus.Draft and not ManagementFeePolicyStatus.Returned)
        {
            throw new DomainRuleException("finance.management_fee.submit.invalid_state", "Only a draft or returned policy can be submitted.");
        }

        Status = ManagementFeePolicyStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Amend(
        long baseRevision,
        string title,
        decimal ratePercent,
        ManagementFeeBase calculationBase,
        DateOnly effectiveFrom,
        string? notes)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.management_fee.revision.conflict");
        if (Status is not ManagementFeePolicyStatus.Draft and not ManagementFeePolicyStatus.Returned)
        {
            throw new DomainRuleException("finance.management_fee.amend.invalid_state", "Only a draft or returned policy can be amended.");
        }

        if (!Enum.IsDefined(calculationBase))
        {
            throw new DomainRuleException("finance.management_fee.base.invalid", "Management fee calculation base is invalid.");
        }

        if (effectiveFrom == default)
        {
            throw new DomainRuleException("finance.management_fee.effective_date.required", "Effective date is required.");
        }

        Title = FinanceRules.Required(title, 200, "finance.management_fee.title.invalid");
        RatePercent = ValidateRate(ratePercent);
        CalculationBase = calculationBase;
        EffectiveFrom = effectiveFrom;
        Notes = FinanceRules.Optional(notes, 2_000, "finance.management_fee.notes.too_long");
        AdvanceRevision();
    }

    public void Approve(long baseRevision, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.management_fee.revision.conflict");
        if (Status != ManagementFeePolicyStatus.Submitted)
        {
            throw new DomainRuleException("finance.management_fee.approve.invalid_state", "Only a submitted policy can be approved.");
        }

        FinanceRules.Identity("finance.management_fee.reviewer.required", reviewedBy);
        Status = ManagementFeePolicyStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = FinanceRules.Optional(comment, 1_000, "finance.management_fee.review_comment.too_long");
        AdvanceRevision();
    }

    public void ReturnForCorrection(long baseRevision, string reason, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        FinanceRules.Revision(Revision, baseRevision, "finance.management_fee.revision.conflict");
        if (Status != ManagementFeePolicyStatus.Submitted)
        {
            throw new DomainRuleException("finance.management_fee.return.invalid_state", "Only a submitted policy can be returned.");
        }

        FinanceRules.Identity("finance.management_fee.reviewer.required", reviewedBy);
        Status = ManagementFeePolicyStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = FinanceRules.Required(reason, 1_000, "finance.management_fee.return_reason.invalid");
        AdvanceRevision();
    }

    public void Supersede(Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        if (Status != ManagementFeePolicyStatus.Approved)
        {
            throw new DomainRuleException("finance.management_fee.supersede.invalid_state", "Only an approved policy can be superseded.");
        }

        FinanceRules.Identity("finance.management_fee.reviewer.required", reviewedBy);
        Status = ManagementFeePolicyStatus.Superseded;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        AdvanceRevision();
    }

    private static decimal ValidateRate(decimal rate)
    {
        if (rate <= 0 || rate > 100 || decimal.Round(rate, 4, MidpointRounding.AwayFromZero) != rate)
        {
            throw new DomainRuleException("finance.management_fee.rate.invalid", "Rate must be greater than zero, at most 100 and have at most four decimal places.");
        }

        return rate;
    }
}

public enum ManagementFeeBase
{
    RecognizedSpend = 1
}

public enum ManagementFeePolicyStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Superseded = 5
}
