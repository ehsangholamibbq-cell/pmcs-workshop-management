using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.FieldOperations.Domain;

public sealed class DailyReport : AggregateRoot
{
    private readonly List<DailyReportFact> _facts = [];

    private DailyReport()
    {
    }

    private DailyReport(
        Guid id,
        Guid tenantId,
        Guid projectId,
        DateOnly reportDate,
        string? locationName,
        string? narrative,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        ReportDate = reportDate;
        LocationName = NormalizeOptional(locationName, 200, "daily_report.location.too_long");
        Narrative = NormalizeOptional(narrative, 4_000, "daily_report.narrative.too_long");
        Status = DailyReportStatus.Draft;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public DateOnly ReportDate { get; private set; }

    public string? LocationName { get; private set; }

    public string? Narrative { get; private set; }

    public DailyReportStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public IReadOnlyCollection<DailyReportFact> Facts => _facts.AsReadOnly();

    public static DailyReport Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        DateOnly reportDate,
        string? locationName,
        string? narrative,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("daily_report.identity.required", "Report, tenant, project and creator ids are required.");
        }

        return new DailyReport(id, tenantId, projectId, reportDate, locationName, narrative, createdBy, createdAt);
    }

    public DailyReportFact AddFact(
        Guid factId,
        DailyFactInput input,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        EnsureEditable();

        if (factId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("daily_fact.identity.required", "Fact and creator ids are required.");
        }

        var fact = DailyReportFact.Create(
            factId,
            Id,
            input,
            createdBy,
            createdAt);

        _facts.Add(fact);
        LastModifiedAt = createdAt;
        AdvanceRevision();
        return fact;
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureEditable();
        EnsureRevision(baseRevision);

        if (_facts.Count == 0)
        {
            throw new DomainRuleException("daily_report.fact.required", "At least one observed fact is required before submission.");
        }

        Status = DailyReportStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        LastModifiedAt = submittedAt;
        AdvanceRevision();
    }

    public void ReturnForCorrection(
        long baseRevision,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt)
    {
        EnsureAwaitingReview();
        EnsureRevision(baseRevision);

        if (reviewedBy == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1_000)
        {
            throw new DomainRuleException(
                "daily_report.return.reason.invalid",
                "Reviewer and a correction reason of at most 1000 characters are required.");
        }

        Status = DailyReportStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = reason.Trim();
        LastModifiedAt = reviewedAt;
        AdvanceRevision();
    }

    public void Approve(
        long baseRevision,
        string? comment,
        Guid reviewedBy,
        DateTimeOffset reviewedAt)
    {
        EnsureAwaitingReview();
        EnsureRevision(baseRevision);

        if (reviewedBy == Guid.Empty)
        {
            throw new DomainRuleException("daily_report.reviewer.required", "Reviewer is required.");
        }

        Status = DailyReportStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = NormalizeOptional(comment, 1_000, "daily_report.review_comment.too_long");
        LastModifiedAt = reviewedAt;
        AdvanceRevision();
    }

    private void EnsureEditable()
    {
        if (Status is not DailyReportStatus.Draft and not DailyReportStatus.Returned)
        {
            throw new DomainRuleException("daily_report.edit.invalid_state", "Only a draft or returned report can be edited.");
        }
    }

    private void EnsureAwaitingReview()
    {
        if (Status != DailyReportStatus.Submitted)
        {
            throw new DomainRuleException("daily_report.review.invalid_state", "Only a submitted report can be reviewed.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException("daily_report.revision.conflict", "The report changed after it was loaded.");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainRuleException(errorCode, $"Value must be at most {maxLength} characters.");
        }

        return normalized;
    }
}

public enum DailyReportStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Rejected = 5,
    Superseded = 6
}
