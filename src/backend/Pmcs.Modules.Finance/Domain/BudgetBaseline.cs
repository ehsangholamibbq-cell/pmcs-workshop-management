using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Finance.Domain;

public sealed class BudgetBaseline : AggregateRoot
{
    private BudgetBaseline()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public BudgetBaselineStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public static BudgetBaseline Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string title,
        decimal amount,
        string currencyCode,
        string? notes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("budget.baseline.identity.required", "Baseline, tenant, project and creator ids are required.");
        }

        if (amount <= 0 || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            throw new DomainRuleException("budget.baseline.amount.invalid", "Budget amount must be positive and have at most two decimal places.");
        }

        var currency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length != 3 || currency.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new DomainRuleException("budget.baseline.currency.invalid", "Currency must be a three-letter ISO-style code.");
        }

        return new BudgetBaseline
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Title = Required(title, 200, "budget.baseline.title.invalid"),
            Amount = amount,
            CurrencyCode = currency,
            Notes = Optional(notes, 2_000, "budget.baseline.notes.too_long"),
            Status = BudgetBaselineStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureRevision(baseRevision);
        if (Status is not BudgetBaselineStatus.Draft and not BudgetBaselineStatus.Returned)
        {
            throw new DomainRuleException("budget.baseline.submit.invalid_state", "Only a draft or returned baseline can be submitted.");
        }

        Status = BudgetBaselineStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Amend(
        long baseRevision,
        string title,
        decimal amount,
        string currencyCode,
        string? notes)
    {
        EnsureRevision(baseRevision);
        if (Status is not BudgetBaselineStatus.Draft and not BudgetBaselineStatus.Returned)
        {
            throw new DomainRuleException("budget.baseline.amend.invalid_state", "Only a draft or returned baseline can be amended.");
        }

        if (amount <= 0 || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
        {
            throw new DomainRuleException("budget.baseline.amount.invalid", "Budget amount must be positive and have at most two decimal places.");
        }

        var currency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length != 3 || currency.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new DomainRuleException("budget.baseline.currency.invalid", "Currency must be a three-letter ISO-style code.");
        }

        Title = Required(title, 200, "budget.baseline.title.invalid");
        Amount = amount;
        CurrencyCode = currency;
        Notes = Optional(notes, 2_000, "budget.baseline.notes.too_long");
        AdvanceRevision();
    }

    public void Approve(long baseRevision, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = BudgetBaselineStatus.Approved;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        ReviewComment = Optional(comment, 1_000, "budget.baseline.review_comment.too_long");
        AdvanceRevision();
    }

    public void ReturnForCorrection(long baseRevision, string reason, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = BudgetBaselineStatus.Returned;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        ReviewComment = Required(reason, 1_000, "budget.baseline.return_reason.invalid");
        AdvanceRevision();
    }

    public void Supersede(Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        if (Status != BudgetBaselineStatus.Approved)
        {
            throw new DomainRuleException("budget.baseline.supersede.invalid_state", "Only an approved baseline can be superseded.");
        }

        Status = BudgetBaselineStatus.Superseded;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        AdvanceRevision();
    }

    private void EnsureSubmitted()
    {
        if (Status != BudgetBaselineStatus.Submitted)
        {
            throw new DomainRuleException("budget.baseline.review.invalid_state", "Only a submitted baseline can be reviewed.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException("budget.baseline.revision.conflict", "The baseline changed after it was loaded.");
        }
    }

    private static Guid RequiredActor(Guid actorId) => actorId != Guid.Empty
        ? actorId
        : throw new DomainRuleException("budget.baseline.reviewer.required", "Reviewer is required.");

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
}

public enum BudgetBaselineStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Superseded = 5
}
