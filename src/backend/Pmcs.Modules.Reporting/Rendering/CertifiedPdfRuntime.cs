using System.Security.Cryptography;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class CertifiedPdfRuntime
{
    private static readonly object ConfigurationGate = new();
    private static string? configuredSignature;

    public static void EnsureConfigured(ReportingRendererOptions options)
    {
        var signature = string.Join(
            '|',
            options.PdfLicense,
            options.PdfRegularFontPath,
            options.PdfBoldFontPath,
            options.PdfRegularFontSha256,
            options.PdfBoldFontSha256,
            options.PdfRendererImageDigest);
        lock (ConfigurationGate)
        {
            if (configuredSignature is not null)
            {
                if (!string.Equals(configuredSignature, signature, StringComparison.Ordinal))
                {
                    throw new ReportRenderingException(
                        "reporting.renderer.configuration_conflict",
                        transient: false,
                        "PDF renderer configuration changed inside one process.");
                }
                return;
            }

            if (string.Equals(options.PdfLicense, "Unconfigured", StringComparison.OrdinalIgnoreCase))
            {
                throw new ReportRenderingException(
                    "reporting.renderer.license_unconfigured",
                    transient: false,
                    "A reviewed QuestPDF license mode must be configured before PDF rendering is enabled.");
            }
            if (!string.Equals(
                    options.PdfLicense,
                    CertifiedPdfRuntimeContract.LicenseDecision,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ReportRenderingException(
                    "reporting.renderer.license_unapproved",
                    transient: false,
                    "The configured QuestPDF license differs from the reviewed legal decision.");
            }

            QuestPDF.Settings.License = LicenseType.Community;
            if (!string.Equals(
                    options.PdfRendererImageDigest,
                    CertifiedPdfRuntimeContract.RuntimeImageDigest,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    options.PdfRegularFontSha256,
                    CertifiedPdfRuntimeContract.RegularFontSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    options.PdfBoldFontSha256,
                    CertifiedPdfRuntimeContract.BoldFontSha256,
                    StringComparison.Ordinal))
            {
                throw new ReportRenderingException(
                    "reporting.renderer.configuration_unpinned",
                    transient: false,
                    "The certified PDF image or font digest differs from the reviewed runtime contract.");
            }

            RegisterCertifiedFont(options.PdfRegularFontPath, options.PdfRegularFontSha256);
            RegisterCertifiedFont(options.PdfBoldFontPath, options.PdfBoldFontSha256);
            configuredSignature = signature;
        }
    }

    private static void RegisterCertifiedFont(string path, string expectedSha256)
    {
        if (!File.Exists(path))
        {
            throw new ReportRenderingException(
                "reporting.renderer.font_missing",
                transient: false,
                "A certified Persian PDF font file is not available.");
        }

        using var font = File.OpenRead(path);
        var actualSha256 = Convert.ToHexString(SHA256.HashData(font)).ToLowerInvariant();
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new ReportRenderingException(
                "reporting.renderer.font_integrity_failed",
                transient: false,
                "A certified Persian PDF font failed its SHA-256 integrity check.");
        }
        font.Position = 0;
        FontManager.RegisterFont(font);
    }
}
