using Pmcs.Modules.TechnicalOffice.Contracts;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectTechnicalOfficeReportFormatting
{
    public static string Status(TechnicalReportingStatus status) => status switch
    {
        TechnicalReportingStatus.NotConfigured => "منبع دفتر فنی پیکربندی نشده",
        TechnicalReportingStatus.NoData => "بدون داده رسمی (صفر اثبات‌شده)",
        TechnicalReportingStatus.InsufficientData => "داده تاریخی ناکافی (شمارش رسمی نامعلوم)",
        TechnicalReportingStatus.Available => "داده رسمی موجود",
        _ => throw ProjectTechnicalOfficeReportRenderingContract.InvalidSnapshot("Unknown F07 status.")
    };

    public static string Count(int? count) => count.HasValue
        ? PersianReportFormatting.ToPersianDigits(count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
        : "نامعلوم؛ صفر فرض نشود";

    public static string Reasons(IEnumerable<TechnicalReportingReason> reasons) =>
        string.Join("؛ ", reasons.Select(Reason));

    public static string Reason(TechnicalReportingReason reason) => reason switch
    {
        TechnicalReportingReason.TechnicalSourceNotConfigured => "منبع دفتر فنی پیکربندی نشده",
        TechnicalReportingReason.NoOfficialDocumentRevision => "بازنگری سند با ابلاغ رسمی وجود ندارد",
        TechnicalReportingReason.NoIssuedTransmittal => "ترنسمیتال صادرشده وجود ندارد",
        TechnicalReportingReason.NoIssuedRfi => "RFI صادرشده وجود ندارد",
        TechnicalReportingReason.NoSubmittedSubmittal => "سابمیتال ثبت‌شده وجود ندارد",
        TechnicalReportingReason.HistoricalTransitionUnavailable => "زمان تغییر وضعیت تاریخی موجود نیست",
        TechnicalReportingReason.SourceCoverageIncomplete => "پوشش منبع تاریخی ناقص است",
        TechnicalReportingReason.OfficialRevisionLineageInvalid => "زنجیره بازنگری رسمی نامعتبر است",
        TechnicalReportingReason.CrossProjectReference => "ارجاع بیرون از پروژه",
        TechnicalReportingReason.ClassificationUnknown => "طبقه‌بندی نامعلوم است",
        TechnicalReportingReason.DueDateUnavailable => "موعد ارزیابی موجود نیست",
        TechnicalReportingReason.ReviewOutcomeUnavailable => "نتیجه بررسی موجود نیست",
        _ => throw ProjectTechnicalOfficeReportRenderingContract.InvalidSnapshot("Unknown F07 reason.")
    };

    public static string Due(TechnicalReportingDueState state) => state switch
    {
        TechnicalReportingDueState.NotAssessable => "غیرقابل ارزیابی",
        TechnicalReportingDueState.NotDue => "در موعد",
        TechnicalReportingDueState.Overdue => "معوق",
        TechnicalReportingDueState.Acknowledged => "دریافت تأیید شده",
        _ => throw ProjectTechnicalOfficeReportRenderingContract.InvalidSnapshot("Unknown F07 due state.")
    };

    public static string YesNo(bool? value) => value switch
    {
        true => "بله",
        false => "خیر",
        null => "غیرقابل ارزیابی"
    };
}
