using Pmcs.Modules.QualitySafety.Contracts;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectQualityHseReportFormatting
{
    public static string Status(QualityHseReportingStatus status) => status switch
    {
        QualityHseReportingStatus.NotConfigured => "بخش پیکربندی نشده",
        QualityHseReportingStatus.NoData => "بدون داده رسمی (صفر اثبات‌شده)",
        QualityHseReportingStatus.InsufficientData => "تاریخچه/پوشش ناکافی (شمارش نامعلوم)",
        QualityHseReportingStatus.Available => "داده رسمی موجود",
        _ => throw ProjectQualityHseReportRenderingContract.InvalidSnapshot("Unknown F08 status.")
    };

    public static string Count(int? count) => count.HasValue
        ? PersianReportFormatting.ToPersianDigits(count.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
        : "نامعلوم؛ صفر فرض نشود";

    public static string Reasons(IEnumerable<QualityHseReportingReason> reasons) =>
        string.Join("؛ ", reasons.Select(Reason));

    private static string Reason(QualityHseReportingReason reason) => reason switch
    {
        QualityHseReportingReason.QualityNotConfigured => "کنترل کیفیت پیکربندی نشده",
        QualityHseReportingReason.HseNotConfigured => "HSE پیکربندی نشده",
        QualityHseReportingReason.NoOfficialQualityFact => "رخداد رسمی کیفیت موجود نیست",
        QualityHseReportingReason.NoOfficialHseFact => "رخداد رسمی HSE موجود نیست",
        QualityHseReportingReason.HistoricalTransitionUnavailable => "زمان تغییر وضعیت تاریخی موجود نیست",
        QualityHseReportingReason.SourceCoverageIncomplete => "پوشش منبع تاریخی ناقص است",
        QualityHseReportingReason.ConfigurationHistoryUnavailable => "تاریخچه پیکربندی قابل اثبات نیست",
        QualityHseReportingReason.ExposureBasisIncomplete => "مبنای محاسبه نرخ حادثه کامل نیست؛ نرخ منتشر نمی‌شود",
        QualityHseReportingReason.RestrictedPublicationUnavailable => "مجوز انتشار محدود فراهم نیست",
        _ => throw ProjectQualityHseReportRenderingContract.InvalidSnapshot("Unknown F08 reason.")
    };

    public static string Kind(QualityHseFactKind kind) => kind switch
    {
        QualityHseFactKind.InspectionRequested => "درخواست بازرسی",
        QualityHseFactKind.InspectionResult => "نتیجه بازرسی",
        QualityHseFactKind.QualityTest => "آزمون کیفیت",
        QualityHseFactKind.ToolboxTalk => "جلسه ایمنی",
        QualityHseFactKind.ExposureHours => "ساعت مواجهه تأییدشده",
        _ => throw ProjectQualityHseReportRenderingContract.InvalidSnapshot("Unknown F08 fact kind.")
    };
}
