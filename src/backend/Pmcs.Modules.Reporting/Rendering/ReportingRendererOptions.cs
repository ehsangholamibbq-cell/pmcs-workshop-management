using Microsoft.Extensions.Configuration;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed record ReportingRendererOptions(
    string PdfLicense,
    string PdfRegularFontPath,
    string PdfBoldFontPath,
    string PdfRegularFontSha256,
    string PdfBoldFontSha256,
    string PdfRendererImageDigest)
{
    public static ReportingRendererOptions Create(IConfiguration configuration) => new(
        configuration["ReportingCenter:PdfLicense"]?.Trim() ?? "Unconfigured",
        configuration["ReportingCenter:PdfRegularFontPath"]?.Trim()
            ?? "/app/fonts/DejaVuSans.ttf",
        configuration["ReportingCenter:PdfBoldFontPath"]?.Trim()
            ?? "/app/fonts/DejaVuSans-Bold.ttf",
        configuration["ReportingCenter:PdfRegularFontSha256"]?.Trim().ToLowerInvariant()
            ?? CertifiedPdfRuntimeContract.RegularFontSha256,
        configuration["ReportingCenter:PdfBoldFontSha256"]?.Trim().ToLowerInvariant()
            ?? CertifiedPdfRuntimeContract.BoldFontSha256,
        configuration["ReportingCenter:PdfRendererImageDigest"]?.Trim().ToLowerInvariant()
            ?? CertifiedPdfRuntimeContract.RuntimeImageDigest);
}
