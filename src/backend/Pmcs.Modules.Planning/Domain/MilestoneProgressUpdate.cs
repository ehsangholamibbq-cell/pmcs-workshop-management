using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Planning.Domain;

public sealed class MilestoneProgressUpdate : AggregateRoot
{
    private MilestoneProgressUpdate()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid BaselineId { get; private set; }

    public Guid BaselineEntryId { get; private set; }

    public DateOnly StatusDate { get; private set; }

    public decimal ProgressPercent { get; private set; }

    public string EvidenceReference { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public MilestoneProgressStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public static MilestoneProgressUpdate Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid baselineId,
        Guid baselineEntryId,
        DateOnly statusDate,
        decimal progressPercent,
        string evidenceReference,
        string? note,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || baselineId == Guid.Empty ||
            baselineEntryId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "planning.milestone_update.identity.required",
                "Milestone update, baseline, entry, tenant, project and creator ids are required.");
        }

        return new MilestoneProgressUpdate
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            BaselineId = baselineId,
            BaselineEntryId = baselineEntryId,
            StatusDate = statusDate,
            ProgressPercent = ValidatePercent(progressPercent),
            EvidenceReference = Required(evidenceReference, 500, "planning.milestone_update.evidence.required"),
            Note = Optional(note, 2_000, "planning.milestone_update.note.too_long"),
            Status = MilestoneProgressStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Amend(
        long baseRevision,
        DateOnly statusDate,
        decimal progressPercent,
        string evidenceReference,
        string? note)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        StatusDate = statusDate;
        ProgressPercent = ValidatePercent(progressPercent);
        EvidenceReference = Required(evidenceReference, 500, "planning.milestone_update.evidence.required");
        Note = Optional(note, 2_000, "planning.milestone_update.note.too_long");
        AdvanceRevision();
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        Status = MilestoneProgressStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Approve(long baseRevision, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = MilestoneProgressStatus.Approved;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        ReviewComment = Optional(comment, 1_000, "planning.milestone_update.review_comment.too_long");
        AdvanceRevision();
    }

    public void ReturnForCorrection(long baseRevision, string reason, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = MilestoneProgressStatus.Returned;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        ReviewComment = Required(reason, 1_000, "planning.milestone_update.return_reason.invalid");
        AdvanceRevision();
    }

    public void Supersede(Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        if (Status != MilestoneProgressStatus.Approved)
        {
            throw new DomainRuleException(
                "planning.milestone_update.supersede.invalid_state",
                "Only an approved milestone update can be superseded.");
        }

        Status = MilestoneProgressStatus.Superseded;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        AdvanceRevision();
    }

    private static decimal ValidatePercent(decimal value)
    {
        if (value is < 0 or > 100 || decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
        {
            throw new DomainRuleException(
                "planning.milestone_update.percent.invalid",
                "Milestone progress must be between zero and 100 with at most two decimal places.");
        }

        return value;
    }

    private void EnsureEditable()
    {
        if (Status is not MilestoneProgressStatus.Draft and not MilestoneProgressStatus.Returned)
        {
            throw new DomainRuleException(
                "planning.milestone_update.amend.invalid_state",
                "Only a draft or returned milestone update can be changed.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != MilestoneProgressStatus.Submitted)
        {
            throw new DomainRuleException(
                "planning.milestone_update.review.invalid_state",
                "Only a submitted milestone update can be reviewed.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException(
                "planning.milestone_update.revision.conflict",
                "The milestone update changed after it was loaded.");
        }
    }

    private static Guid RequiredActor(Guid actorId) => actorId != Guid.Empty
        ? actorId
        : throw new DomainRuleException("planning.milestone_update.reviewer.required", "Reviewer is required.");

    private static string Required(string value, int maximumLength, string errorCode) =>
        Optional(value, maximumLength, errorCode) ?? throw new DomainRuleException(errorCode, "A value is required.");

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

public enum MilestoneProgressStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Superseded = 5
}
