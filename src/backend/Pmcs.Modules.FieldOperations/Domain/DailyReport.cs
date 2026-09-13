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
        RootReportId = id;
        VersionNumber = 1;
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

    public Guid RootReportId { get; private set; }

    public int VersionNumber { get; private set; }

    public Guid? SupersedesReportId { get; private set; }

    public Guid? SupersededByReportId { get; private set; }

    public DateTimeOffset? SupersededAt { get; private set; }

    public string? CorrectionReason { get; private set; }

    public Guid? CorrectionInitiatedBy { get; private set; }

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

    public void RemoveFact(Guid factId, long baseRevision, DateTimeOffset removedAt)
    {
        EnsureEditable();
        EnsureRevision(baseRevision);
        var fact = _facts.SingleOrDefault(item => item.Id == factId)
            ?? throw new DomainRuleException("daily_fact.not_found", "The report fact was not found.");
        _facts.Remove(fact);
        LastModifiedAt = removedAt;
        AdvanceRevision();
    }

    public void ReviseDetails(
        long baseRevision,
        string? locationName,
        string? narrative,
        DateTimeOffset changedAt)
    {
        EnsureEditable();
        EnsureRevision(baseRevision);
        LocationName = NormalizeOptional(locationName, 200, "daily_report.location.too_long");
        Narrative = NormalizeOptional(narrative, 4_000, "daily_report.narrative.too_long");
        LastModifiedAt = changedAt;
        AdvanceRevision();
    }

    public DailyReport CreateCorrection(
        Guid correctionId,
        long baseRevision,
        string reason,
        Guid initiatedBy,
        DateTimeOffset initiatedAt)
    {
        EnsureRevision(baseRevision);
        if (Status != DailyReportStatus.Approved || SupersededByReportId.HasValue)
        {
            throw new DomainRuleException(
                "daily_report.correction.invalid_state",
                "Only the current approved report can start a correction.");
        }

        if (correctionId == Guid.Empty || initiatedBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "daily_report.correction.identity.required",
                "Correction and initiator ids are required.");
        }

        var normalizedReason = NormalizeRequired(reason, 1_000, "daily_report.correction.reason.invalid");
        var correction = new DailyReport(
            correctionId,
            TenantId,
            ProjectId,
            ReportDate,
            LocationName,
            Narrative,
            CreatedBy,
            initiatedAt)
        {
            RootReportId = RootReportId,
            VersionNumber = checked(VersionNumber + 1),
            SupersedesReportId = Id,
            CorrectionReason = normalizedReason,
            CorrectionInitiatedBy = initiatedBy
        };

        foreach (var fact in _facts)
        {
            correction._facts.Add(fact.CopyForCorrection(Guid.NewGuid(), correction.Id, initiatedBy, initiatedAt));
        }

        return correction;
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

    public void SupersedeWith(
        Guid replacementReportId,
        string reason,
        DateTimeOffset supersededAt)
    {
        if (Status != DailyReportStatus.Approved || replacementReportId == Guid.Empty ||
            SupersededByReportId.HasValue)
        {
            throw new DomainRuleException(
                "daily_report.supersede.invalid_state",
                "Only a current approved report can be superseded once.");
        }

        Status = DailyReportStatus.Superseded;
        SupersededByReportId = replacementReportId;
        SupersededAt = supersededAt;
        CorrectionReason = NormalizeRequired(reason, 1_000, "daily_report.correction.reason.invalid");
        LastModifiedAt = supersededAt;
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

    private static string NormalizeRequired(string value, int maxLength, string errorCode) =>
        NormalizeOptional(value, maxLength, errorCode)
        ?? throw new DomainRuleException(errorCode, "A non-empty value is required.");
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
