using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class PortfolioSummaryReportFormatting
{
    public static string Status(PortfolioDimensionStatus status) => status switch
    {
        PortfolioDimensionStatus.NotAuthorized => "بدون مجوز",
        PortfolioDimensionStatus.NotConfigured => "پیکربندی نشده",
        PortfolioDimensionStatus.NotEnabled => "فعال نشده",
        PortfolioDimensionStatus.SetupRequired => "نیازمند راه‌اندازی",
        PortfolioDimensionStatus.Suspended => "تعلیق شده",
        PortfolioDimensionStatus.NoData => "بدون داده رسمی",
        PortfolioDimensionStatus.InsufficientData => "داده ناکافی؛ صفر فرض نشود",
        PortfolioDimensionStatus.Available => "داده رسمی موجود",
        _ => throw new ReportRenderingException("reporting.portfolio.snapshot.payload_invalid",
            transient: false, "Unknown F10 dimension status.")
    };

    public static string Name(PortfolioProjectSelection project) =>
        project.ConfigurationProvenAtCutoff ?
            PersianReportFormatting.SafeText(project.Name, 180) : "پیکربندی تاریخی نامعلوم";

    public static string Code(PortfolioProjectSelection project) =>
        project.ConfigurationProvenAtCutoff ?
            PersianReportFormatting.SafeText(project.Code, 100) : "—";

    public static string Amount(decimal? value) => value.HasValue
        ? PersianReportFormatting.FormatDecimal(value)
        : "نامعلوم؛ صفر فرض نشود";

    public static string Reason(string? value) =>
        value is null ? "—" : PersianReportFormatting.SafeText(value, 180);
}
