using System.Globalization;
using System.Text;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class PersianReportFormatting
{
    private static readonly PersianCalendar Calendar = new();
    private static readonly char[] PersianDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];

    public static string ToPersianDigits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                span[index] = character is >= '0' and <= '9'
                    ? PersianDigits[character - '0']
                    : character;
            }
        });
    }

    public static string FormatDate(DateOnly value, bool persianDigits = true)
    {
        var date = value.ToDateTime(TimeOnly.MinValue);
        var formatted = $"{Calendar.GetYear(date):0000}/{Calendar.GetMonth(date):00}/{Calendar.GetDayOfMonth(date):00}";
        return persianDigits ? ToPersianDigits(formatted) : formatted;
    }

    public static string FormatInstant(DateTimeOffset utcValue, string timeZoneId)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw InvalidTimeZone(exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw InvalidTimeZone(exception);
        }

        var local = TimeZoneInfo.ConvertTime(utcValue.ToUniversalTime(), timeZone);
        var date = local.DateTime;
        var formatted = $"{Calendar.GetYear(date):0000}/{Calendar.GetMonth(date):00}/{Calendar.GetDayOfMonth(date):00} " +
            $"{date.Hour:00}:{date.Minute:00}";
        return ToPersianDigits(formatted);
    }

    public static string FormatDecimal(decimal? value) => value.HasValue
        ? ToPersianDigits(value.Value.ToString("0.############################", CultureInfo.InvariantCulture))
        : "—";

    public static string DataStatus(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "داده رسمی موجود",
        ReportDataStatus.NoData => "داده رسمی وجود ندارد",
        ReportDataStatus.InsufficientData => "داده رسمی ناکافی است",
        ReportDataStatus.NotConfigured => "منبع داده پیکربندی نشده است",
        _ => "وضعیت نامشخص"
    };

    public static string VersionState(DailyReportReportingVersionState state) => state switch
    {
        DailyReportReportingVersionState.Approved => "نسخه رسمی جاری",
        DailyReportReportingVersionState.Superseded => "نسخه رسمی جایگزین‌شده",
        _ => "نامشخص"
    };

    public static string FactKind(DailyReportReportingFactKind kind) => kind switch
    {
        DailyReportReportingFactKind.WorkProgress => "پیشرفت کار",
        DailyReportReportingFactKind.Labor => "نیروی انسانی",
        DailyReportReportingFactKind.Equipment => "ماشین‌آلات",
        DailyReportReportingFactKind.Material => "مصالح",
        DailyReportReportingFactKind.Issue => "مسئله",
        DailyReportReportingFactKind.Stoppage => "توقف",
        DailyReportReportingFactKind.SiteCondition => "شرایط کارگاه",
        DailyReportReportingFactKind.Note => "یادداشت",
        _ => "نامشخص"
    };

    public static string Impact(DailyReportReportingImpactLevel? impact) => impact switch
    {
        DailyReportReportingImpactLevel.Low => "کم",
        DailyReportReportingImpactLevel.Medium => "متوسط",
        DailyReportReportingImpactLevel.High => "زیاد",
        DailyReportReportingImpactLevel.Critical => "بحرانی",
        _ => "—"
    };

    public static string PeriodKind(ProjectReportPeriodKind kind) => kind switch
    {
        ProjectReportPeriodKind.Weekly => "هفتگی",
        ProjectReportPeriodKind.Monthly => "ماهانه",
        _ => "نامشخص"
    };

    public static string ReasonCode(ProjectPeriodicReportReasonCode reason) => reason switch
    {
        ProjectPeriodicReportReasonCode.ReportingCadenceMissing => "تناوب گزارش‌دهی پیکربندی نشده است",
        ProjectPeriodicReportReasonCode.DailyWorkflowMissing => "گردش‌کار گزارش روزانه پیکربندی نشده است",
        ProjectPeriodicReportReasonCode.DailyCutoffMissing => "زمان برش روزانه پیکربندی نشده است",
        ProjectPeriodicReportReasonCode.WorkingCalendarMissing => "تقویم کاری معتبر پیکربندی نشده است",
        ProjectPeriodicReportReasonCode.PeriodOpenAtCutoff => "دوره در زمان برش هنوز بسته نشده است",
        ProjectPeriodicReportReasonCode.ExpectedSlotMissing => "یک یا چند نوبت مورد انتظار پوشش داده نشده است",
        ProjectPeriodicReportReasonCode.OfficialVersionMissing => "نسخه رسمی یکی از گزارش‌ها موجود نیست",
        ProjectPeriodicReportReasonCode.OfficialReportEmpty => "یکی از گزارش‌های رسمی Fact ساختاریافته ندارد",
        _ => "علت نامشخص"
    };

    public static string ReasonCode(ExecutiveProjectStateReportReasonCode reason) => reason switch
    {
        ExecutiveProjectStateReportReasonCode.ProjectStateReportingNotConfigured =>
            "منبع رسمی وضعیت پروژه برای گزارش‌دهی پیکربندی نشده است",
        ExecutiveProjectStateReportReasonCode.OfficialSnapshotMissing =>
            "Snapshot رسمی واجد شرایط وجود ندارد",
        ExecutiveProjectStateReportReasonCode.OfficialSnapshotNoData =>
            "Snapshot رسمی فاقد داده قابل ارزیابی است",
        ExecutiveProjectStateReportReasonCode.OfficialSnapshotInsufficient =>
            "Snapshot رسمی برای ارزیابی عملیاتی ناکافی است",
        ExecutiveProjectStateReportReasonCode.CoverageInsufficient =>
            "پوشش گزارش‌های رسمی ناکافی است",
        ExecutiveProjectStateReportReasonCode.FreshnessStale =>
            "داده رسمی در زمان برش کهنه است",
        ExecutiveProjectStateReportReasonCode.ConfidenceLow =>
            "اعتمادپذیری داده رسمی پایین است",
        ExecutiveProjectStateReportReasonCode.ProjectConfigurationRevisionOutdated =>
            "نسخه پیکربندی پروژه از Snapshot رسمی جدیدتر است",
        ExecutiveProjectStateReportReasonCode.ApprovedSourceChangedAfterSnapshot =>
            "منبع رسمی پس از Snapshot تغییر کرده است",
        _ => "علت نامشخص"
    };

    public static string ReasonCode(ProjectProgressReportReasonCode reason) => reason switch
    {
        ProjectProgressReportReasonCode.ProgressReportingNotConfigured =>
            "گزارش‌دهی پیشرفت پیکربندی نشده است",
        ProjectProgressReportReasonCode.PlanningModeNone =>
            "حالت برنامه‌ریزی پروژه غیرفعال است",
        ProjectProgressReportReasonCode.OfficialBaselineMissing =>
            "Baseline رسمی واجد شرایط وجود ندارد",
        ProjectProgressReportReasonCode.PlanningModeBaselineMismatch =>
            "نوع Baseline با حالت برنامه‌ریزی مؤثر سازگار نیست",
        ProjectProgressReportReasonCode.OfficialActualMissing =>
            "شواهد رسمی پیشرفت وجود ندارد",
        ProjectProgressReportReasonCode.OfficialActualIncomplete =>
            "شواهد رسمی پیشرفت برای همه ردیف‌های وزن‌دار کامل نیست",
        ProjectProgressReportReasonCode.ScheduleNotConfiguredForMeasurementWeights =>
            "Baseline وزنی فاقد زمان‌بندی رسمی است",
        ProjectProgressReportReasonCode.CalendarDaysFallback =>
            "محاسبه برنامه بر مبنای روز تقویمی انجام شده است",
        ProjectProgressReportReasonCode.ApprovedProgressOutsideBaseline =>
            "شواهد رسمی خارج از Baseline بدون تخصیص باقی مانده است",
        _ => "علت نامشخص"
    };

    public static string Classification(ReportClassification classification) => classification switch
    {
        ReportClassification.Internal => "داخلی",
        ReportClassification.Confidential => "محرمانه",
        ReportClassification.Restricted => "محدود",
        _ => "نامشخص"
    };

    public static string PlanningMode(PlanningMode mode) => mode switch
    {
        Pmcs.Modules.Projects.Domain.PlanningMode.None => "بدون برنامه‌ریزی",
        Pmcs.Modules.Projects.Domain.PlanningMode.SimpleWorkList => "فهرست کار ساده",
        Pmcs.Modules.Projects.Domain.PlanningMode.Milestones => "مایلستون",
        Pmcs.Modules.Projects.Domain.PlanningMode.WbsBaseline => "Baseline ساختار شکست کار",
        Pmcs.Modules.Projects.Domain.PlanningMode.ExternalSchedule => "برنامه زمان‌بندی خارجی",
        _ => "نامشخص"
    };

    public static string BaselineKind(PlanningBaselineKind kind) => kind switch
    {
        PlanningBaselineKind.MeasurementWeights => "وزن اقلام اندازه‌گیری",
        PlanningBaselineKind.MilestonePlan => "برنامه مایلستون",
        PlanningBaselineKind.WbsBaseline => "Baseline ساختار شکست کار",
        PlanningBaselineKind.ExternalSchedule => "برنامه زمان‌بندی خارجی",
        _ => "نامشخص"
    };

    public static string EntryKind(PlanningEntryKind kind) => kind switch
    {
        PlanningEntryKind.Summary => "خلاصه",
        PlanningEntryKind.Activity => "فعالیت",
        PlanningEntryKind.Milestone => "مایلستون",
        PlanningEntryKind.MeasurementItem => "قلم اندازه‌گیری",
        _ => "نامشخص"
    };

    public static string MeasurementMethod(ProgressMeasurementMethod method) => method switch
    {
        ProgressMeasurementMethod.None => "بدون روش اندازه‌گیری",
        ProgressMeasurementMethod.QuantityBased => "مقدارمحور",
        ProgressMeasurementMethod.ManualPercent => "درصد رسمی دستی",
        _ => "نامشخص"
    };

    public static string MetricStatus(ProjectProgressMetricStatus status) => status switch
    {
        ProjectProgressMetricStatus.NotConfigured => "پیکربندی نشده",
        ProjectProgressMetricStatus.NoData => "بدون داده",
        ProjectProgressMetricStatus.InsufficientData => "داده ناکافی",
        ProjectProgressMetricStatus.Available => "موجود",
        _ => "نامشخص"
    };

    public static string CalendarBasis(ProjectProgressCalendarBasis? basis) => basis switch
    {
        ProjectProgressCalendarBasis.CalendarDays => "روزهای تقویمی",
        ProjectProgressCalendarBasis.WorkingDays => "روزهای کاری پین‌شده",
        _ => "—"
    };

    public static string CalendarState(ProjectProgressCalendarState state) => state switch
    {
        ProjectProgressCalendarState.NotConfigured => "پیکربندی نشده",
        ProjectProgressCalendarState.WorkingWeek => "هفته کاری پین‌شده",
        _ => "نامشخص"
    };

    public static string SamplingKind(ProjectProgressCurveSamplingKind kind) => kind switch
    {
        ProjectProgressCurveSamplingKind.NotConfigured => "پیکربندی نشده",
        ProjectProgressCurveSamplingKind.DailyInclusiveV1 => "روزانه و شامل ابتدا/انتها",
        ProjectProgressCurveSamplingKind.Uniform365PlusCutoffV1 => "شبکه یکنواخت ۳۶۵ نقطه به‌علاوه برش",
        _ => "نامشخص"
    };

    public static string UnitState(ProjectPeriodicReportUnitState state) => state switch
    {
        ProjectPeriodicReportUnitState.SourceUnit => "واحد منبع",
        ProjectPeriodicReportUnitState.UnitMissing => "واحد ثبت نشده",
        _ => "نامشخص"
    };

    public static string Classification(DailyReportReportingClassification classification) => classification switch
    {
        DailyReportReportingClassification.Internal => "داخلی",
        DailyReportReportingClassification.Confidential => "محرمانه",
        DailyReportReportingClassification.Restricted => "محدود",
        _ => "نامشخص"
    };

    public static string Classification(ProjectStateReportingClassification classification) => classification switch
    {
        ProjectStateReportingClassification.Internal => "داخلی",
        ProjectStateReportingClassification.Confidential => "محرمانه",
        ProjectStateReportingClassification.Restricted => "محدود",
        _ => "نامشخص"
    };

    public static string SourceState(ProjectStateReportingSourceState state) => state switch
    {
        ProjectStateReportingSourceState.NotConfigured => "پیکربندی نشده",
        ProjectStateReportingSourceState.Configured => "پیکربندی شده",
        _ => "نامشخص"
    };

    public static string AssessmentScope(ProjectAssessmentScope scope) => scope switch
    {
        ProjectAssessmentScope.ApprovedDailyOperations => "عملیات روزانه رسمی تأییدشده",
        _ => "نامشخص"
    };

    public static string OperationalStatus(ProjectOperationalStatus status) => status switch
    {
        ProjectOperationalStatus.NoData => "بدون داده",
        ProjectOperationalStatus.InsufficientData => "داده ناکافی",
        ProjectOperationalStatus.Stable => "پایدار در محدوده عملیات روزانه رسمی",
        ProjectOperationalStatus.Watch => "نیازمند پایش",
        ProjectOperationalStatus.AtRisk => "در معرض ریسک عملیاتی",
        ProjectOperationalStatus.Critical => "بحرانی عملیاتی",
        _ => "نامشخص"
    };

    public static string CoverageStatus(DataCoverageStatus status) => status switch
    {
        DataCoverageStatus.NoData => "بدون داده",
        DataCoverageStatus.Insufficient => "ناکافی",
        DataCoverageStatus.Sufficient => "کافی",
        _ => "نامشخص"
    };

    public static string FreshnessStatus(DataFreshnessStatus status) => status switch
    {
        DataFreshnessStatus.NoData => "بدون داده",
        DataFreshnessStatus.Current => "به‌روز",
        DataFreshnessStatus.Aging => "در حال کهنه‌شدن",
        DataFreshnessStatus.Stale => "کهنه",
        _ => "نامشخص"
    };

    public static string ConfidenceStatus(DataConfidenceStatus status) => status switch
    {
        DataConfidenceStatus.NoData => "بدون داده",
        DataConfidenceStatus.Low => "پایین",
        DataConfidenceStatus.Adequate => "کافی",
        _ => "نامشخص"
    };

    public static string CoverageBasis(ProjectCoverageBasis basis) => basis switch
    {
        ProjectCoverageBasis.SevenCalendarDays => "هفت روز تقویمی",
        ProjectCoverageBasis.FallbackSevenCalendarDays => "جایگزین هفت روز تقویمی",
        ProjectCoverageBasis.ConfiguredWorkingDays => "روزهای کاری پیکربندی‌شده",
        _ => "نامشخص"
    };

    public static string AttentionKind(ProjectAttentionKind kind) => kind switch
    {
        ProjectAttentionKind.Issue => "مسئله",
        ProjectAttentionKind.Stoppage => "توقف",
        _ => "نامشخص"
    };

    public static string ObservedImpact(ProjectObservedImpact? impact) => impact switch
    {
        ProjectObservedImpact.Low => "کم",
        ProjectObservedImpact.Medium => "متوسط",
        ProjectObservedImpact.High => "زیاد",
        ProjectObservedImpact.Critical => "بحرانی",
        _ => "ارزیابی نشده"
    };

    public static string AttentionPriority(ProjectAttentionPriority priority) => priority switch
    {
        ProjectAttentionPriority.Unassessed => "ارزیابی نشده",
        ProjectAttentionPriority.Low => "کم",
        ProjectAttentionPriority.Medium => "متوسط",
        ProjectAttentionPriority.High => "زیاد",
        ProjectAttentionPriority.Critical => "بحرانی",
        _ => "نامشخص"
    };

    public static string AttentionAgeBand(ProjectAttentionAgeBand ageBand) => ageBand switch
    {
        ProjectAttentionAgeBand.New => "جدید",
        ProjectAttentionAgeBand.Aging => "در حال ماندگاری",
        ProjectAttentionAgeBand.Overdue => "معوق",
        _ => "نامشخص"
    };

    public static string AttentionStatus(ProjectAttentionStatus status) => status switch
    {
        ProjectAttentionStatus.NeedsTriage => "نیازمند بررسی",
        _ => "نامشخص"
    };

    public static string FeatureState(ProjectFeatureState state) => state switch
    {
        ProjectFeatureState.NotConfigured => "پیکربندی نشده",
        ProjectFeatureState.NotEnabled => "فعال نشده",
        ProjectFeatureState.SetupRequired => "نیازمند راه‌اندازی",
        ProjectFeatureState.Active => "فعال",
        ProjectFeatureState.Suspended => "تعلیق‌شده",
        _ => "نامشخص"
    };

    public static string YesNoUnknown(bool? value) => value.HasValue ? YesNo(value.Value) : "—";

    public static string YesNo(bool value) => value ? "بله" : "خیر";

    public static string SafeText(string? value, int maximumLength = 4_000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        var builder = new StringBuilder(Math.Min(value.Length, maximumLength));
        foreach (var character in value.Trim())
        {
            if (builder.Length >= maximumLength)
            {
                break;
            }

            if (!char.IsControl(character) || character is '\n' or '\r' or '\t')
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? "—" : builder.ToString();
    }

    public static string SafeSpreadsheetText(string? value, int maximumLength = 32_000)
    {
        var normalized = SafeText(value, maximumLength);
        return normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@'
            ? $"'{normalized}"
            : normalized;
    }

    private static ReportRenderingException InvalidTimeZone(Exception exception) => new(
        "reporting.project.time_zone.invalid",
        transient: false,
        "The project time zone cannot be used for certified rendering.",
        exception);
}
