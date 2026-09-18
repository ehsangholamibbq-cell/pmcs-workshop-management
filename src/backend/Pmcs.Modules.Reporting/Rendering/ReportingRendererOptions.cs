using Microsoft.Extensions.Configuration;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed record ReportingRendererOptions(
    string PdfLicense,
    string PdfRegularFontPath,
    string PdfBoldFontPath)
{
    public static ReportingRendererOptions Create(IConfiguration configuration) => new(
        configuration["ReportingCenter:PdfLicense"]?.Trim() ?? "Unconfigured",
        configuration["ReportingCenter:PdfRegularFontPath"]?.Trim()
            ?? "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        configuration["ReportingCenter:PdfBoldFontPath"]?.Trim()
            ?? "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf");
}
