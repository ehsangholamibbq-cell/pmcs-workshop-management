using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.QualitySafety.Domain;

namespace Pmcs.Domain.Tests;

public sealed class QualitySafetyWorkflowTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DisabledCapabilitiesStayIndependentAndAreNotReadySignals()
    {
        var configuration = QualitySafetyConfiguration.Create(Guid.NewGuid(), TenantId, ProjectId,
            QualityOperatingMode.FullV1, HseOperatingMode.Disabled, UserId, null, Guid.NewGuid(), null,
            true, true, true, UserId, Now);

        Assert.True(configuration.QualityReady);
        Assert.True(configuration.HseReady);
        Assert.Equal(HseOperatingMode.Disabled, configuration.HseMode);
        Assert.Throws<DomainRuleException>(() => QualitySafetyConfiguration.Create(Guid.NewGuid(), TenantId,
            ProjectId, QualityOperatingMode.Disabled, HseOperatingMode.Disabled, UserId, null,
            Guid.NewGuid(), null, false, false, false, UserId, Now));
    }

    [Fact]
    public void MatrixVersionIsImmutableAndDefinitionMustBeJson()
    {
        var matrix = RiskMatrixVersion.Create(Guid.NewGuid(), TenantId, ProjectId, ControlArea.Hse,
            2, "ماتریس ریسک ایمنی", "{\"likelihood\":[1,2],\"impact\":[1,2]}", Now, UserId, Now);
        Assert.Equal(2, matrix.Version);
        Assert.Throws<DomainRuleException>(() => RiskMatrixVersion.Create(Guid.NewGuid(), TenantId, ProjectId,
            ControlArea.Hse, 3, "خراب", "not-json", Now, UserId, Now));
    }

    [Fact]
    public void IntakeIsNotAFormalIncidentBeforeTriageAndConversion()
    {
        var intake = IncidentIntake();
        Assert.Equal(IntakeStatus.Captured, intake.Status);
        Assert.Equal(IntakeConversionType.None, intake.ConversionType);
        Assert.Null(intake.ConvertedRecordId);

        intake.BeginTriage(1, UserId, Now.AddMinutes(1));
        intake.Convert(2, IntakeConversionType.Incident, Guid.NewGuid(), "نیازمند بررسی رسمی", UserId, Now.AddMinutes(2));
        Assert.Equal(IntakeStatus.Converted, intake.Status);
        Assert.Equal(IntakeConversionType.Incident, intake.ConversionType);
    }

    [Fact]
    public void IntakeCannotCrossQualityAndHseBoundaries()
    {
        var intake = IncidentIntake();
        intake.BeginTriage(1, UserId, Now);
        Assert.Throws<DomainRuleException>(() => intake.Convert(2, IntakeConversionType.NonConformance,
            Guid.NewGuid(), "تبدیل اشتباه", UserId, Now));
    }

    [Fact]
    public void IncidentIntakeCannotUseGeneralClassification()
    {
        Assert.Throws<DomainRuleException>(() => QualitySafetyIntake.Capture(Guid.NewGuid(), TenantId,
            ProjectId, IntakeKind.IncidentIntake, Now, "کارگاه", "لغزش کارگر", InitialSeverity.High,
            "توقف کار", ["evidence:photo"], DataClassification.GeneralProject, UserId, Now));
    }

    [Fact]
    public void InspectionReadinessAndResultAreSeparateFacts()
    {
        var inspection = Inspection();
        inspection.RecordReadiness(1, InspectionReadiness.Ready, null);

        Assert.Equal(InspectionStatus.ReadinessRecorded, inspection.Status);
        Assert.Null(inspection.Result);
        inspection.RecordResult(2, InspectionResult.PassWithObservation, "یک مشاهده جزئی",
            ["evidence:inspection-sheet"], UserId, Now.AddHours(1));
        Assert.Equal(InspectionResult.PassWithObservation, inspection.Result);
        Assert.Equal(InspectionStatus.ResultRecorded, inspection.Status);
    }

    [Fact]
    public void NotReadyInspectionCannotPretendToPass()
    {
        var inspection = Inspection();
        inspection.RecordReadiness(1, InspectionReadiness.NotReady, "سطح آماده نیست");
        Assert.Throws<DomainRuleException>(() => inspection.RecordResult(2, InspectionResult.Pass, null,
            ["evidence:sheet"], UserId, Now));
    }

    [Fact]
    public void NcrConcessionRequiresApprover()
    {
        var ncr = Ncr();
        ncr.Transition(1, NcrStatus.Issued, NcrDisposition.NotDecided, null, null,
            RootCauseStatus.NotStarted, null, null, null, null, Now);
        ncr.Transition(2, NcrStatus.Containment, NcrDisposition.NotDecided, null, null,
            RootCauseStatus.NotStarted, null, null, null, null, Now);
        ncr.Transition(3, NcrStatus.InvestigationDisposition, NcrDisposition.NotDecided, null, null,
            RootCauseStatus.UnderReview, "فرضیه اولیه", null, null, null, Now);
        Assert.Throws<DomainRuleException>(() => ncr.Transition(4, NcrStatus.ActionImplementation,
            NcrDisposition.UseAsIsWithConcession, "پذیرش مشروط", null, RootCauseStatus.Confirmed,
            "انحراف ساخت", null, null, null, Now));
    }

    [Fact]
    public void NcrCannotCloseWithoutVerificationEvidenceOrAuditedWaiver()
    {
        var ncr = NcrReadyForVerification();
        Assert.Throws<DomainRuleException>(() => ncr.Transition(6, NcrStatus.Closed, NcrDisposition.Rework,
            "بازکاری", null, RootCauseStatus.Confirmed, "روش اجرا", [], null, null, Now));
        ncr.Transition(6, NcrStatus.Closed, NcrDisposition.Rework, "بازکاری", null,
            RootCauseStatus.Confirmed, "روش اجرا", ["evidence:reinspection"], null, null, Now);
        Assert.Equal(NcrStatus.Closed, ncr.Status);
    }

    [Fact]
    public void RectifiedDefectIsNotVerifiedOrClosed()
    {
        var defect = DefectRecord.Create(Guid.NewGuid(), TenantId, ProjectId, null,
            "ترک سطح", "طبقه دوم", UserId, Now);
        defect.Assign(1, UserId, DateOnly.FromDateTime(Now.AddDays(2).Date));
        defect.Transition(2, DefectStatus.Rectified, ["evidence:rectification"], Now);
        Assert.Equal(DefectStatus.Rectified, defect.Status);
        Assert.Throws<DomainRuleException>(() => defect.Transition(3, DefectStatus.Closed, null, Now));
    }

    [Fact]
    public void IncidentFinalSeverityIsSeparateAndRequiredAtFinalReview()
    {
        var incident = Incident();
        incident.Transition(1, IncidentStatus.Triage, null, RootCauseStatus.NotStarted, null, null, Now);
        incident.Transition(2, IncidentStatus.Contained, null, RootCauseStatus.NotStarted, null, null, Now);
        incident.Transition(3, IncidentStatus.UnderInvestigation, null, RootCauseStatus.UnderReview, "در حال بررسی", null, Now);
        incident.Transition(4, IncidentStatus.ActionsOpen, null, RootCauseStatus.Confirmed, "حفاظ ناکافی", null, Now);
        Assert.Throws<DomainRuleException>(() => incident.Transition(5, IncidentStatus.FinalReview, null,
            RootCauseStatus.Confirmed, "حفاظ ناکافی", null, Now));
        incident.Transition(5, IncidentStatus.FinalReview, InitialSeverity.Medium,
            RootCauseStatus.Confirmed, "حفاظ ناکافی", null, Now);
        Assert.Equal(InitialSeverity.High, incident.PreliminarySeverity);
        Assert.Equal(InitialSeverity.Medium, incident.FinalSeverity);
    }

    [Fact]
    public void CompletedCorrectiveActionIsNotVerifiedOrClosed()
    {
        var action = Action();
        action.Transition(1, CorrectiveActionStatus.InProgress, null, UserId, Now);
        action.Transition(2, CorrectiveActionStatus.Completed, ["evidence:completion"], UserId, Now);
        Assert.Equal(CorrectiveActionStatus.Completed, action.Status);
        Assert.Null(action.VerifiedAt);
        Assert.Throws<DomainRuleException>(() => action.Transition(3, CorrectiveActionStatus.Closed, null, UserId, Now));
    }

    [Fact]
    public void CorrectiveActionVerificationRequiresIndependentEvidence()
    {
        var action = Action();
        action.Transition(1, CorrectiveActionStatus.InProgress, null, UserId, Now);
        action.Transition(2, CorrectiveActionStatus.Completed, ["evidence:completion"], UserId, Now);
        action.Transition(3, CorrectiveActionStatus.ReadyForVerification, null, UserId, Now);
        Assert.Throws<DomainRuleException>(() => action.Transition(4, CorrectiveActionStatus.Verified, [], UserId, Now));
        action.Transition(4, CorrectiveActionStatus.Verified, ["evidence:verification"], UserId, Now);
        action.Transition(5, CorrectiveActionStatus.Closed, null, UserId, Now);
        Assert.Equal(CorrectiveActionStatus.Closed, action.Status);
    }

    [Fact]
    public void DueDateExtensionNeedsLaterDateReasonAndApprover()
    {
        var action = Action();
        Assert.Throws<DomainRuleException>(() => action.Extend(1, new DateOnly(2026, 9, 12), "", UserId));
        action.Extend(1, new DateOnly(2026, 9, 20), "تأخیر تأمین قطعه", UserId);
        Assert.Equal(new DateOnly(2026, 9, 20), action.ExtendedDueDate);
    }

    [Fact]
    public void PermitCannotActivateOutsideApprovedValidityWindow()
    {
        var permit = PermitToWork.Create(Guid.NewGuid(), TenantId, ProjectId, "جوشکاری", "موتورخانه",
            Now.AddHours(1), Now.AddHours(3), ["حریق"], ["کپسول و ناظر"], UserId, Now);
        permit.Transition(1, PermitStatus.Submitted, UserId, Now);
        permit.Transition(2, PermitStatus.Approved, UserId, Now);
        Assert.Throws<DomainRuleException>(() => permit.Transition(3, PermitStatus.Active, UserId, Now));
        permit.Transition(3, PermitStatus.Active, UserId, Now.AddHours(1));
        Assert.Equal(PermitStatus.Active, permit.Status);
    }

    [Fact]
    public void ToolboxTalkNeedsAttendanceAndEvidence()
    {
        Assert.Throws<DomainRuleException>(() => ToolboxTalk.Record(Guid.NewGuid(), TenantId, ProjectId,
            "کار در ارتفاع", Now, "کارگاه", [], ["evidence:photo"], UserId, Now));
        var talk = ToolboxTalk.Record(Guid.NewGuid(), TenantId, ProjectId, "کار در ارتفاع", Now,
            "کارگاه", ["اکیپ نما"], ["evidence:attendance"], UserId, Now);
        Assert.StartsWith("TBT-14050620-", talk.Number);
    }

    [Fact]
    public void InspectionPlanAndChecklistAreImmutableVersionedDefinitions()
    {
        var plan = InspectionTestPlanVersion.Create(Guid.NewGuid(), TenantId, ProjectId, "CONCRETE", 2,
            "برنامه بتن", ["پیش از بتن", "حین بتن"], "نقشه و مشخصات مصوب", "بازرس کیفیت",
            InspectionPointType.Hold, Now, UserId, Now);
        var checklist = ChecklistTemplateVersion.Create(Guid.NewGuid(), TenantId, ProjectId, "CONCRETE", 4,
            "چک‌لیست بتن", ["آرماتور", "قالب", "تمیزی"], Now, UserId, Now);
        Assert.Equal(2, plan.Version);
        Assert.Equal(4, checklist.Version);
        Assert.Equal(InspectionPointType.Hold, plan.PointType);
    }

    [Fact]
    public void QualityTestRequiresEvidenceAndKeepsResultExplicit()
    {
        Assert.Throws<DomainRuleException>(() => QualityTestRecord.Record(Guid.NewGuid(), TenantId, ProjectId,
            null, "مقاومت بتن", Now, "نمونه ۱۲", "حداقل ۳۵", QualityTestResult.Fail,
            "مقاومت ۳۱", [], UserId, Now));
        var record = QualityTestRecord.Record(Guid.NewGuid(), TenantId, ProjectId, null, "مقاومت بتن",
            Now, "نمونه ۱۲", "حداقل ۳۵", QualityTestResult.Fail, "مقاومت ۳۱",
            ["evidence:lab-report"], UserId, Now);
        Assert.Equal(QualityTestResult.Fail, record.Result);
    }

    [Fact]
    public void IncidentRateBasisRejectsMissingOrOverPreciseHours()
    {
        Assert.Throws<DomainRuleException>(() => ExposureHoursRecord.Record(Guid.NewGuid(), TenantId,
            ProjectId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), 0m,
            "کارکرد هفتگی", ["evidence:timesheet"], UserId, Now));
        Assert.Throws<DomainRuleException>(() => ExposureHoursRecord.Record(Guid.NewGuid(), TenantId,
            ProjectId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), 120.123m,
            "کارکرد هفتگی", ["evidence:timesheet"], UserId, Now));
        var exposure = ExposureHoursRecord.Record(Guid.NewGuid(), TenantId, ProjectId,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7), 120.25m,
            "کارکرد هفتگی", ["evidence:timesheet"], UserId, Now);
        Assert.Equal(120.25m, exposure.Hours);
    }

    [Fact]
    public void CompetencyRecordRequiresRestrictedClassificationAndEvidence()
    {
        Assert.Throws<DomainRuleException>(() => HseCompetencyRecord.Record(Guid.NewGuid(), TenantId,
            ProjectId, "نیروی ۱۲", new DateOnly(2026, 9, 1), null, ["کار در ارتفاع"],
            ["evidence:certificate"], DataClassification.GeneralProject, UserId, Now));
        var record = HseCompetencyRecord.Record(Guid.NewGuid(), TenantId, ProjectId, "نیروی ۱۲",
            new DateOnly(2026, 9, 1), new DateOnly(2027, 9, 1), ["کار در ارتفاع"],
            ["evidence:certificate"], DataClassification.ConfidentialHse, UserId, Now);
        Assert.Equal(new DateOnly(2027, 9, 1), record.ValidUntil);
    }

    private static QualitySafetyIntake IncidentIntake() => QualitySafetyIntake.Capture(Guid.NewGuid(), TenantId,
        ProjectId, IntakeKind.IncidentIntake, Now, "کارگاه", "لغزش بدون آسیب قطعی", InitialSeverity.High,
        "محصورسازی محل", ["evidence:photo"], DataClassification.ConfidentialHse, UserId, Now);
    private static InspectionRecord Inspection() => InspectionRecord.Request(Guid.NewGuid(), TenantId, ProjectId,
        null, Guid.NewGuid(), "بازرسی پیش از بتن", "سقف دوم", "تطابق با نقشه مصوب", "CHK-CONCRETE", 3,
        Now.AddHours(1), UserId, Now);
    private static NonConformanceRecord Ncr() => NonConformanceRecord.Create(Guid.NewGuid(), TenantId, ProjectId,
        null, null, null, null, null, null, null, "انحراف بتن", "مقاومت ۳۵ مگاپاسکال",
        "نتیجه آزمایش کمتر است", UserId, Now);
    private static NonConformanceRecord NcrReadyForVerification()
    {
        var ncr = Ncr();
        ncr.Transition(1, NcrStatus.Issued, NcrDisposition.NotDecided, null, null, RootCauseStatus.NotStarted, null, null, null, null, Now);
        ncr.Transition(2, NcrStatus.Containment, NcrDisposition.NotDecided, null, null, RootCauseStatus.NotStarted, null, null, null, null, Now);
        ncr.Transition(3, NcrStatus.InvestigationDisposition, NcrDisposition.NotDecided, null, null, RootCauseStatus.UnderReview, "فرضیه", null, null, null, Now);
        ncr.Transition(4, NcrStatus.ActionImplementation, NcrDisposition.Rework, "بازکاری", null, RootCauseStatus.Confirmed, "روش اجرا", null, null, null, Now);
        ncr.Transition(5, NcrStatus.ReinspectionVerification, NcrDisposition.Rework, "بازکاری", null, RootCauseStatus.Confirmed, "روش اجرا", null, null, null, Now);
        return ncr;
    }
    private static SafetyIncident Incident() => SafetyIncident.Report(Guid.NewGuid(), TenantId, ProjectId, null,
        Now, "کارگاه", "لغزش کارگر روی سطح خیس", InitialSeverity.High, Guid.NewGuid(),
        DataClassification.ConfidentialHse, UserId, Now);
    private static CorrectiveAction Action() => CorrectiveAction.Create(Guid.NewGuid(), TenantId, ProjectId,
        ControlArea.Quality, "NCR", Guid.NewGuid(), CorrectiveActionKind.Corrective, "اصلاح روش اجرا",
        UserId, "پیمانکار جزء", new DateOnly(2026, 9, 15), "بازرسی مجدد قابل قبول", UserId, Now);
}
