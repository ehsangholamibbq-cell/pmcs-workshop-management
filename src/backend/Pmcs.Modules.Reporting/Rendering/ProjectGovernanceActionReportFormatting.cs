using Pmcs.Modules.ActionControl.Contracts;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectGovernanceActionReportFormatting
{
    public static string Status(GovernanceActionReportingStatus status) => status switch
    {
        GovernanceActionReportingStatus.NotConfigured => "بخش پیکربندی نشده",
        GovernanceActionReportingStatus.NoData => "بدون داده رسمی (صفر اثبات‌شده)",
        GovernanceActionReportingStatus.InsufficientData => "تاریخچه/پوشش ناکافی (شمارش نامعلوم)",
        GovernanceActionReportingStatus.Available => "داده رسمی موجود",
        _ => throw ProjectGovernanceActionReportRenderingContract.InvalidSnapshot("Unknown F09 status.")
    };

    public static string Count(int? count) => count.HasValue
        ? PersianReportFormatting.ToPersianDigits(count.Value.ToString(
            System.Globalization.CultureInfo.InvariantCulture))
        : "نامعلوم؛ صفر فرض نشود";

    public static string Reasons(IEnumerable<GovernanceActionReportingReason> reasons) =>
        string.Join("؛ ", reasons.Select(Reason));

    private static string Reason(GovernanceActionReportingReason reason) => reason switch
    {
        GovernanceActionReportingReason.GovernanceSourceNotConfigured => "منبع راهبری پیکربندی نشده",
        GovernanceActionReportingReason.NoOfficialIssue => "مسئله رسمی موجود نیست",
        GovernanceActionReportingReason.NoOfficialRisk => "ریسک رسمی موجود نیست",
        GovernanceActionReportingReason.NoSubmittedDecision => "درخواست تصمیم ارسال‌شده موجود نیست",
        GovernanceActionReportingReason.NoRaisedEscalation => "ارجاع رسمی ثبت نشده",
        GovernanceActionReportingReason.NoOfficialAction => "اقدام رسمی موجود نیست",
        GovernanceActionReportingReason.HistoricalTransitionUnavailable => "زمان تغییر وضعیت تاریخی موجود نیست",
        GovernanceActionReportingReason.SourceCoverageIncomplete => "پوشش منبع تاریخی ناقص است",
        GovernanceActionReportingReason.MatrixVersionUnavailable => "نسخه ماتریس ارزیابی نامعلوم است",
        GovernanceActionReportingReason.SlaRuleUnavailable => "نسخه قانون SLA نامعلوم است",
        GovernanceActionReportingReason.WorkingCalendarUnavailable => "تقویم کاری پروژه پیکربندی نشده",
        GovernanceActionReportingReason.NoAssessedRisk => "ریسک ارزیابی‌شده موجود نیست",
        _ => throw ProjectGovernanceActionReportRenderingContract.InvalidSnapshot("Unknown F09 reason.")
    };

    public static string Kind(GovernanceActionFactKind kind) => kind switch
    {
        GovernanceActionFactKind.Issue => "مسئله",
        GovernanceActionFactKind.Risk => "ریسک",
        GovernanceActionFactKind.DecisionRequest => "درخواست تصمیم",
        GovernanceActionFactKind.DecisionRecord => "تصمیم ثبت‌شده",
        GovernanceActionFactKind.Escalation => "ارجاع",
        GovernanceActionFactKind.Action => "اقدام",
        _ => throw ProjectGovernanceActionReportRenderingContract.InvalidSnapshot("Unknown F09 fact kind.")
    };
}
