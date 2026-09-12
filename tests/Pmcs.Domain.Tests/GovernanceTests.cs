using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class GovernanceTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RiskRatingUsesMaximumImpactAndPinsMatrixVersion()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var risk = Risk(tenantId, projectId);
        var matrix = GovernanceRiskMatrixVersion.Create(Guid.NewGuid(), tenantId, projectId, 3,
            "ماتریس مصوب", 5, 10, 15, At, Guid.NewGuid(), At);

        risk.Assess(1, matrix, ProbabilityBand.Likely, ImpactBand.Minor, ImpactBand.Major,
            ImpactBand.Moderate, ImpactBand.Negligible, ImpactBand.Minor, ImpactBand.Moderate,
            RiskResponseStrategy.Mitigate, "اقدام کاهش ریسک", "عبور تأخیر از سه روز",
            new DateOnly(2026, 9, 20), Guid.NewGuid(), At.AddHours(1));

        Assert.Equal(16, risk.InherentScore);
        Assert.Equal(RiskRatingBand.Critical, risk.InherentRating);
        Assert.Equal(matrix.Id, risk.MatrixVersionId);
        Assert.Equal(matrix.Version, risk.MatrixVersion);
    }

    [Fact]
    public void MaterializedRiskKeepsHistoryAndLinksNewIssue()
    {
        var risk = AssessedRisk();
        risk.Activate(2, Guid.NewGuid(), At.AddHours(2));
        var issueId = Guid.NewGuid();

        risk.Materialize(3, issueId, Guid.NewGuid(), At.AddHours(3));

        Assert.Equal(RiskStatus.Materialized, risk.Status);
        Assert.Equal(issueId, risk.MaterializedIssueId);
        Assert.Equal("عدم دریافت نقشه", risk.UncertainEvent);
    }

    [Fact]
    public void IssueCannotCloseWithoutResolutionAndVerificationEvidence()
    {
        var issue = Issue();

        var exception = Assert.Throws<DomainRuleException>(() => issue.Transition(
            1, IssueStatus.Closed, "رفع شد", ["evidence:1"], Guid.NewGuid(), At.AddHours(1)));

        Assert.Equal("governance.issue.transition.invalid", exception.Code);
    }

    [Fact]
    public void IssueResolutionAndVerifiedClosureAreSeparateTransitions()
    {
        var issue = Issue();
        var actor = Guid.NewGuid();
        issue.Transition(1, IssueStatus.UnderAssessment, null, null, actor, At.AddHours(1));
        issue.Transition(2, IssueStatus.ResponseInProgress, null, null, actor, At.AddHours(2));
        issue.Transition(3, IssueStatus.Resolved, "مانع رفع و خروجی کنترل شد", null, actor, At.AddHours(3));
        var verifier = Guid.NewGuid();
        issue.Transition(4, IssueStatus.Closed, "تأیید نهایی", ["evidence:closure"], verifier, At.AddHours(4));

        Assert.Equal(IssueStatus.Closed, issue.Status);
        Assert.Single(issue.ClosureEvidence);
        Assert.Equal(actor, issue.ResolvedBy);
        Assert.Equal(verifier, issue.ClosedBy);
        Assert.Equal(5, issue.Revision);
    }

    [Fact]
    public void ResolverCannotVerifyAndCloseTheSameIssue()
    {
        var issue = Issue();
        var actor = Guid.NewGuid();
        issue.Transition(1, IssueStatus.UnderAssessment, null, null, actor, At.AddHours(1));
        issue.Transition(2, IssueStatus.ResponseInProgress, null, null, actor, At.AddHours(2));
        issue.Transition(3, IssueStatus.Resolved, "مانع رفع شد", null, actor, At.AddHours(3));

        var exception = Assert.Throws<DomainRuleException>(() => issue.Transition(
            4, IssueStatus.Closed, "تأیید نهایی", ["evidence:closure"], actor, At.AddHours(4)));

        Assert.Equal("governance.issue.independent_verifier.required", exception.Code);
        Assert.Equal(IssueStatus.Resolved, issue.Status);
        Assert.Null(issue.ClosedBy);
    }

    [Fact]
    public void DecisionRequestNeedsTwoOptionsBeforeSubmission()
    {
        var request = Decision(["گزینه نخست"]);

        var exception = Assert.Throws<DomainRuleException>(() =>
            request.Submit(1, Guid.NewGuid(), At.AddHours(1)));

        Assert.Equal("governance.decision_request.options.insufficient", exception.Code);
    }

    [Fact]
    public void FormalDecisionMustSelectSubmittedOption()
    {
        var request = Decision(["اجرای راهکار الف", "اجرای راهکار ب"]);
        request.Submit(1, Guid.NewGuid(), At.AddHours(1));

        var exception = Assert.Throws<DomainRuleException>(() => DecisionRecord.Record(Guid.NewGuid(),
            request, "راهکار ثبت‌نشده", "توجیه تصمیم", null, DecisionChannel.InSystem, At,
            null, Guid.NewGuid(), "مدیر پروژه", null, At.AddHours(2)));

        Assert.Equal("governance.decision.option.not_offered", exception.Code);
    }

    [Fact]
    public void SupersedingDecisionCreatesChainWithoutRewritingOriginal()
    {
        var request = Decision(["اجرای راهکار الف", "اجرای راهکار ب"]);
        request.Submit(1, Guid.NewGuid(), At.AddHours(1));
        var original = DecisionRecord.Record(Guid.NewGuid(), request, "اجرای راهکار الف", "توجیه نخست",
            null, DecisionChannel.InSystem, At, null, Guid.NewGuid(), "مدیر پروژه", null, At.AddHours(2));
        request.LinkDecision(2, original.Id, Guid.NewGuid(), At.AddHours(2));
        var replacement = DecisionRecord.Record(Guid.NewGuid(), request, "اجرای راهکار ب", "اطلاعات جدید",
            ["کنترل هفتگی"], DecisionChannel.Meeting, At.AddHours(3), null, Guid.NewGuid(),
            "مدیر پروژه", original.Id, At.AddHours(4));
        original.Supersede(1, replacement.Id);
        request.LinkReplacementDecision(3, original.Id, replacement.Id, Guid.NewGuid(), At.AddHours(4));

        Assert.Equal(DecisionRecordStatus.Superseded, original.Status);
        Assert.Equal(replacement.Id, original.SupersededByDecisionId);
        Assert.Equal(original.Id, replacement.SupersedesDecisionId);
        Assert.Equal(replacement.Id, request.DecisionRecordId);
        Assert.Equal("اجرای راهکار الف", original.SelectedOption);
    }

    [Fact]
    public void WorkingDayDeadlineUsesProjectCalendarAndSkipsWeekend()
    {
        var rule = Sla(SlaDurationUnit.ProjectWorkingDays, 2);
        var project = Project(0b0111110);

        var deadline = GovernanceDeadlineCalculator.Calculate(
            new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero), rule, project);

        Assert.NotNull(deadline);
        Assert.Equal(new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero), deadline!.DueAt);
    }

    [Fact]
    public void WorkingDayDeadlineIsUnavailableWithoutConfiguredCalendar()
    {
        var deadline = GovernanceDeadlineCalculator.Calculate(At,
            Sla(SlaDurationUnit.ProjectWorkingDays, 2), Project(null));

        Assert.Null(deadline);
    }

    [Fact]
    public void SeveritySpecificSlaIsOnlyValidForIssues()
    {
        var exception = Assert.Throws<DomainRuleException>(() => SlaRuleVersion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "قاعده نامعتبر",
            SlaEntityType.Risk, GovernanceSeverity.High, 24, SlaDurationUnit.ElapsedHours,
            60, 120, Guid.NewGuid(), "مدیر پروژه", At, Guid.NewGuid(), At));

        Assert.Equal("governance.sla.rule.invalid", exception.Code);
    }

    [Fact]
    public void AcknowledgementDoesNotCloseEscalationOrMutateSource()
    {
        var thread = EscalationThread.Raise(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SlaEntityType.Issue, Guid.NewGuid(), "ISS-01", "مانع تجهیز", EscalationReason.Overdue,
            1, Guid.NewGuid(), "مدیر پروژه", RecordConfidentiality.GeneralProject, At);

        thread.Acknowledge(1, Guid.NewGuid(), "پیگیری شد", At.AddHours(1));

        Assert.Equal(EscalationStatus.Acknowledged, thread.Status);
        Assert.NotEqual(EscalationStatus.ClosedBySourceResolution, thread.Status);
    }

    private static ProjectRisk Risk(Guid? tenantId = null, Guid? projectId = null) => ProjectRisk.Propose(
        Guid.NewGuid(), tenantId ?? Guid.NewGuid(), projectId ?? Guid.NewGuid(), RiskType.Threat,
        "تأخیر در بازبینی", "عدم دریافت نقشه", "توقف جبهه کاری", "فنی", Guid.NewGuid(),
        "مسئول دفتر فنی", RecordConfidentiality.GeneralProject, "TechnicalOffice", "DocumentRevision",
        Guid.NewGuid(), 2, "نسخه تأییدشده نقشه دریافت نشده", ["evidence:source"], null, null,
        Guid.NewGuid(), At);

    private static ProjectRisk AssessedRisk()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var risk = Risk(tenantId, projectId);
        var matrix = GovernanceRiskMatrixVersion.Create(Guid.NewGuid(), tenantId, projectId, 1,
            "ماتریس", 5, 10, 15, At, Guid.NewGuid(), At);
        risk.Assess(1, matrix, ProbabilityBand.Possible, ImpactBand.Minor, ImpactBand.Major,
            ImpactBand.Minor, ImpactBand.Minor, ImpactBand.Moderate, ImpactBand.Moderate,
            RiskResponseStrategy.Mitigate, "پیگیری نقشه", "عدم پاسخ تا سررسید",
            new DateOnly(2026, 9, 20), Guid.NewGuid(), At.AddHours(1));
        return risk;
    }

    private static ManagementIssue Issue() => ManagementIssue.Create(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "مانع تجهیز", "تجهیز وارد کارگاه نشده", "تدارکات",
        GovernanceSeverity.High, GovernanceUrgency.Soon, Guid.NewGuid(), "مسئول تدارکات",
        new DateOnly(2026, 9, 20), "Supply", "PurchaseOrder", Guid.NewGuid(), 1,
        "سفارش تأییدشده ولی تحویل نشده", null, ["evidence:source"],
        RecordConfidentiality.GeneralProject, null, null, Guid.NewGuid(), At);

    private static DecisionRequest Decision(IReadOnlyCollection<string> options) => DecisionRequest.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "کدام راهکار اجرا شود؟", "توقف کار نزدیک است",
        new DateOnly(2026, 9, 15), Guid.NewGuid(), "مدیر پروژه", ["نقشه تأیید نشده"],
        ["پاسخ تا فردا می‌رسد"], ["احتمال توقف جبهه"], options, null, ["هزینه محدود"],
        ["evidence:source"], RecordConfidentiality.GeneralProject, "TechnicalOffice", "Rfi",
        Guid.NewGuid(), 1, "درخواست اطلاعات باز", null, null, Guid.NewGuid(), At);

    private static SlaRuleVersion Sla(SlaDurationUnit unit, int duration) => SlaRuleVersion.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "قاعده پاسخ", SlaEntityType.Issue,
        GovernanceSeverity.High, duration, unit, 60, 120, Guid.NewGuid(), "مدیر پروژه", At,
        Guid.NewGuid(), At);

    private static ProjectControlProfile Project(int? workingDaysMask) => new(Guid.NewGuid(), Guid.NewGuid(),
        "P-01", "پروژه نمونه", "UTC", "IRR", 1, null, ProjectStatus.Active,
        ContractModel.NotConfigured, PlanningMode.None, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        new ProjectCalendarProfile(workingDaysMask.HasValue
            ? ProjectCalendarConfigurationState.Configured : ProjectCalendarConfigurationState.NotConfigured,
            workingDaysMask));
}
