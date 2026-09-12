using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.TechnicalOffice.Domain;

public sealed class TechnicalSubmittal : AggregateRoot
{
    private TechnicalSubmittal() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public TechnicalSubmittalType Type { get; private set; }
    public string Discipline { get; private set; } = string.Empty;
    public string Submitter { get; private set; } = string.Empty;
    public string Reviewer { get; private set; } = string.Empty;
    public Guid? ContractId { get; private set; }
    public Guid? CommitmentId { get; private set; }
    public string? LocationReference { get; private set; }
    public string? WorkItemReference { get; private set; }
    public string? WbsReference { get; private set; }
    public DateOnly? RequiredByDate { get; private set; }
    public DateOnly? PlannedSubmissionDate { get; private set; }
    public DateOnly? ReviewDueDate { get; private set; }
    public int ResubmissionNumber { get; private set; }
    public Guid? SupersedesSubmittalId { get; private set; }
    public string RevisionIdsJson { get; private set; } = "[]";
    public string? RequiredDeliverableReference { get; private set; }
    public SubmittalStatus Status { get; private set; }
    public SubmittalReviewOutcome? ReviewOutcome { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<Guid> RevisionIds => TechnicalOfficeRules.DeserializeIds(RevisionIdsJson);

    public static TechnicalSubmittal Create(
        Guid id, Guid tenantId, Guid projectId, string title, TechnicalSubmittalType type,
        string discipline, string submitter, string reviewer, Guid? contractId, Guid? commitmentId,
        string? locationReference, string? workItemReference, string? wbsReference,
        DateOnly? requiredByDate, DateOnly? plannedSubmissionDate, DateOnly? reviewDueDate,
        int resubmissionNumber, Guid? supersedesSubmittalId, IReadOnlyCollection<Guid> revisionIds,
        string? requiredDeliverableReference, Guid createdBy, DateTimeOffset createdAt)
    {
        TechnicalOfficeRules.Identity(id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(type) || resubmissionNumber < 0)
        {
            throw new DomainRuleException("technical.submittal.type_or_revision.invalid", "Submittal type or resubmission number is invalid.");
        }
        if (resubmissionNumber == 0 != !supersedesSubmittalId.HasValue)
        {
            throw new DomainRuleException("technical.submittal.supersedes.invalid", "Resubmissions must reference the preceding package.");
        }
        var normalizedRevisions = TechnicalOfficeRules.SerializeIds(revisionIds, 100, "technical.submittal.revisions.invalid");
        if (TechnicalOfficeRules.DeserializeIds(normalizedRevisions).Count == 0)
        {
            throw new DomainRuleException("technical.submittal.revisions.required", "At least one document revision is required.");
        }

        return new TechnicalSubmittal
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Number = TechnicalOfficeRules.OfficialNumber("SUB", createdAt, id),
            Title = TechnicalOfficeRules.Required(title, 240, "technical.submittal.title.invalid"),
            Type = type,
            Discipline = TechnicalOfficeRules.Required(discipline, 120, "technical.submittal.discipline.invalid"),
            Submitter = TechnicalOfficeRules.Required(submitter, 240, "technical.submittal.submitter.invalid"),
            Reviewer = TechnicalOfficeRules.Required(reviewer, 240, "technical.submittal.reviewer.invalid"),
            ContractId = contractId,
            CommitmentId = commitmentId,
            LocationReference = TechnicalOfficeRules.Optional(locationReference, 240, "technical.submittal.location.too_long"),
            WorkItemReference = TechnicalOfficeRules.Optional(workItemReference, 240, "technical.submittal.work_item.too_long"),
            WbsReference = TechnicalOfficeRules.Optional(wbsReference, 240, "technical.submittal.wbs.too_long"),
            RequiredByDate = requiredByDate,
            PlannedSubmissionDate = plannedSubmissionDate,
            ReviewDueDate = reviewDueDate,
            ResubmissionNumber = resubmissionNumber,
            SupersedesSubmittalId = supersedesSubmittalId,
            RevisionIdsJson = normalizedRevisions,
            RequiredDeliverableReference = TechnicalOfficeRules.Optional(requiredDeliverableReference, 500, "technical.submittal.deliverable.too_long"),
            Status = SubmittalStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Submit(long baseRevision, DateTimeOffset at)
    {
        EnsureRevision(baseRevision);
        if (Status != SubmittalStatus.Draft)
        {
            throw new DomainRuleException("technical.submittal.submit.invalid_state", "Only a draft submittal can be submitted.");
        }
        Status = SubmittalStatus.Submitted;
        SubmittedAt = at;
        AdvanceRevision();
    }

    public void BeginReview(long baseRevision)
    {
        EnsureRevision(baseRevision);
        if (Status != SubmittalStatus.Submitted)
        {
            throw new DomainRuleException("technical.submittal.review.invalid_state", "Only a submitted package can enter review.");
        }
        Status = SubmittalStatus.UnderReview;
        AdvanceRevision();
    }

    public void RecordReview(
        long baseRevision, SubmittalReviewOutcome outcome, Guid reviewer, DateTimeOffset at, string? comment)
    {
        EnsureRevision(baseRevision);
        TechnicalOfficeRules.Identity(reviewer);
        if (Status != SubmittalStatus.UnderReview || !Enum.IsDefined(outcome))
        {
            throw new DomainRuleException("technical.submittal.review.invalid_state", "Only a valid review can complete an active review.");
        }
        if (outcome is SubmittalReviewOutcome.Rejected or SubmittalReviewOutcome.ReviseAndResubmit &&
            string.IsNullOrWhiteSpace(comment))
        {
            throw new DomainRuleException("technical.submittal.review.reason.required", "Rejected or revised packages require a reason.");
        }

        ReviewOutcome = outcome;
        Status = outcome switch
        {
            SubmittalReviewOutcome.Approved => SubmittalStatus.Approved,
            SubmittalReviewOutcome.ApprovedAsNoted => SubmittalStatus.ApprovedAsNoted,
            SubmittalReviewOutcome.ReviseAndResubmit => SubmittalStatus.ReviseAndResubmit,
            SubmittalReviewOutcome.Rejected => SubmittalStatus.Rejected,
            SubmittalReviewOutcome.ForInformation => SubmittalStatus.ApprovedAsNoted,
            _ => throw new DomainRuleException("technical.submittal.review.outcome.invalid", "Review outcome is invalid.")
        };
        ReviewedBy = reviewer;
        ReviewedAt = at;
        ReviewComment = TechnicalOfficeRules.Optional(comment, 2_000, "technical.review.comment.too_long");
        AdvanceRevision();
    }

    public void Close(long baseRevision, DateTimeOffset at)
    {
        EnsureRevision(baseRevision);
        if (Status is not SubmittalStatus.Approved and not SubmittalStatus.ApprovedAsNoted and not SubmittalStatus.Rejected)
        {
            throw new DomainRuleException("technical.submittal.close.invalid_state", "Only a completed review can be closed.");
        }
        Status = SubmittalStatus.Closed;
        ClosedAt = at;
        AdvanceRevision();
    }

    private void EnsureRevision(long supplied) =>
        TechnicalOfficeRules.Revision(Revision, supplied, "technical.submittal.revision.conflict");
}
