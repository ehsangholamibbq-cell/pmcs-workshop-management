using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class PortfolioSummaryReportRenderingTests
{
    private static readonly Guid Tenant = Id(1);
    private static readonly Guid Run = Id(500);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly LocalDate = new(2026, 9, 27);
    private static readonly string[] ExpectedVisualDigests =
    [
        "60d4d57b2878597981f0af3434924d48e85a7d4928c8c33330f73c95f4f80ac8",
        "4e8b245135302a8bd39a4eeb5aee405b1952f2a76b7c2ea7afd519e83cf1e37a",
        "d16d42796838c05ae94892c3672f009dc743b2ce774ce4d936fa29e5ba71c3d2"
    ];

    [Fact]
    public void F10RenderContractReplaysImmutableSnapshotAndRejectsTamper()
    {
        var snapshot = Snapshot();
        var parsed = PortfolioSummaryReportRenderingContract.Parse(
            snapshot.PayloadJson, snapshot.SourceManifestJson);
        var request = Request(snapshot, ReportFormat.Pdf);
        var registry = new PortfolioSummaryReportRendererRegistry(
            new IPortfolioSummaryReportRenderer[]
            {
                new PortfolioSummaryReportPdfRenderer(Options(), ReportingExecutionOptions.Default),
                new PortfolioSummaryReportXlsxRenderer(ReportingExecutionOptions.Default)
            });
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal("portfolio-summary-2026-09-27.pdf", request.FileName);
        Assert.Equal(ReportFormat.Pdf, registry.Require(ReportFormat.Pdf).Format);
        Assert.Equal(ReportFormat.Xlsx, registry.Require(ReportFormat.Xlsx).Format);
        Assert.Equal("reporting.format.unsupported",
            Assert.Throws<ReportRenderingException>(() => registry.Require(ReportFormat.Csv)).Code);
        Assert.Equal("reporting.portfolio.render_request.invalid",
            Assert.Throws<ReportRenderingException>(() => PortfolioSummaryReportRenderModel.Create(
                request with { SnapshotSha256 = new string('0', 64) })).Code);
        Assert.Equal("reporting.portfolio.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => PortfolioSummaryReportRenderingContract.Parse(
                snapshot.PayloadJson.Replace("\"authorizedProjectCount\":3",
                    "\"authorizedProjectCount\":4", StringComparison.Ordinal),
                snapshot.SourceManifestJson)).Code);
        Assert.Equal("reporting.portfolio.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => PortfolioSummaryReportRenderingContract.Parse(
                snapshot.PayloadJson, snapshot.SourceManifestJson.Replace(
                    PortfolioSummaryReportRuntimeContract.SourceManifestVersion,
                    "pmcs.reporting.portfolio-summary.source-manifest/tampered",
                    StringComparison.Ordinal))).Code);
    }

    [Fact]
    public void F10XlsxGoldenKeepsCurrencyGroupsAndUnknownSeparate()
    {
        var request = Request(Snapshot(), ReportFormat.Xlsx);
        var renderer = new PortfolioSummaryReportXlsxRenderer(ReportingExecutionOptions.Default);
        var first = renderer.Render(request);
        var replay = renderer.Render(request);
        Assert.True(first.Bytes.SequenceEqual(replay.Bytes));
        Assert.True(first.Bytes.AsSpan().StartsWith("PK"u8));
        using var stream = new MemoryStream(first.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(13, archive.Entries.Count);
        var workbook = Read(archive, "xl/workbook.xml");
        foreach (var sheet in new[] { "Metadata", "Coverage", "Currencies", "Projects", "Finance", "Commercial" })
            Assert.Contains(sheet, workbook, StringComparison.Ordinal);
        var sheets = Enumerable.Range(1, 6).Select(index => Read(archive,
            $"xl/worksheets/sheet{index}.xml")).ToArray();
        var ns = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var xml in sheets)
        {
            var document = XDocument.Parse(xml);
            Assert.Equal("1", document.Descendants(ns + "sheetView").Single()
                .Attribute("rightToLeft")?.Value);
            Assert.Equal("frozen", document.Descendants(ns + "pane").Single()
                .Attribute("state")?.Value);
            Assert.Empty(document.Descendants(ns + "f"));
        }
        Assert.Contains("IRR", sheets[2], StringComparison.Ordinal);
        Assert.Contains("USD", sheets[2], StringComparison.Ordinal);
        Assert.Contains("نامعلوم؛ صفر فرض نشود", sheets[2], StringComparison.Ordinal);
        Assert.Contains("داده ناکافی؛ صفر فرض نشود", sheets[4], StringComparison.Ordinal);
        Assert.Contains("بدون مجوز", sheets[5], StringComparison.Ordinal);
        Assert.DoesNotContain("TotalPortfolio", string.Concat(sheets), StringComparison.OrdinalIgnoreCase);
        Ms48FontGoldenArtifacts.Save("portfolio-summary-golden.xlsx", first.Bytes);
        Assert.True(first.Sha256 == "c0b23f66a0044e187a8d176099571dbd8eca7eda2b88c5d5d084630a97a620e5",
            $"F10_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void F10PdfGoldenPreservesRtlLocalDateAndDimensionGaps()
    {
        var request = Request(Snapshot(), ReportFormat.Pdf);
        var renderer = new PortfolioSummaryReportPdfRenderer(Options(), ReportingExecutionOptions.Default);
        var first = renderer.Render(request);
        var replay = renderer.Render(request);
        var images = renderer.RenderQualificationImages(request);
        var repeated = renderer.RenderQualificationImages(request);
        Assert.True(first.Bytes.SequenceEqual(replay.Bytes));
        Assert.True(first.Bytes.AsSpan().StartsWith("%PDF-"u8));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(first.Bytes[^32..]), StringComparison.Ordinal);
        Assert.Equal(3, images.Count);
        Assert.All(images, image => Assert.True(image.AsSpan().StartsWith(
            new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A })));
        for (var index = 0; index < images.Count; index++)
            Assert.True(images[index].SequenceEqual(repeated[index]));
        var digests = images.Select(image => Convert.ToHexString(SHA256.HashData(image))
            .ToLowerInvariant()).ToArray();
        Ms48FontGoldenArtifacts.Save("portfolio-summary-golden.pdf", first.Bytes);
        for (var index = 0; index < images.Count; index++)
            Ms48FontGoldenArtifacts.Save($"portfolio-summary-page-{index + 1}.png", images[index]);
        Assert.True(first.Sha256 == "0407ff05a5760327dd064ce81c54036bb401f37377502b18147e15a7e91da356" &&
                digests.SequenceEqual(ExpectedVisualDigests),
            $"F10_PDF_GOLDEN_SHA256={first.Sha256}; F10_PDF_VISUAL_SHA256={string.Join(',', digests)}");
    }

    private static ReportSnapshot Snapshot()
    {
        var first = Project(Id(10), "IRR", 10m);
        var gap = Project(Id(11), "IRR", 20m) with
        {
            Financial = new PortfolioFinancialDimension(PortfolioDimensionStatus.InsufficientData,
                "IRR", null, null, null, "financial.history_unavailable",
                ReportClassification.Restricted),
            Classification = ReportClassification.Restricted
        };
        var dollar = Project(Id(12), "USD", 30m);
        var built = PortfolioSummaryReportSnapshotBuilder.Build(Run,
            new PortfolioSummarySelection(Tenant, Id(501), Cutoff, [dollar, gap, first]),
            Cutoff.AddMinutes(1));
        return ReportSnapshot.CreatePortfolio(Id(502), Run, Tenant, built.SchemaVersion,
            built.DataStatus, built.PayloadJson, built.SourceManifestJson,
            built.Classification, Cutoff.AddMinutes(1), Cutoff);
    }

    private static PortfolioSummaryReportRenderRequest Request(ReportSnapshot snapshot, ReportFormat format)
    {
        var parsed = PortfolioSummaryReportRenderingContract.Parse(
            snapshot.PayloadJson, snapshot.SourceManifestJson);
        return new PortfolioSummaryReportRenderRequest(Run,
            format == ReportFormat.Pdf ? Id(600) : Id(601), snapshot.Id, Id(603),
            PortfolioSummaryReportRuntimeContract.DefinitionCode,
            PortfolioSummaryReportRuntimeContract.DefinitionVersion,
            PortfolioSummaryReportRuntimeContract.TemplateVersion,
            PortfolioSummaryReportRuntimeContract.TemplateContentDigest,
            PortfolioSummaryReportRuntimeContract.RendererContractVersion,
            PortfolioSummaryReportRuntimeContract.LayoutContractVersion,
            format, ReportArtifactIdentity.FileName(parsed, format), "RPT-F100-0000-0000-0000-0001",
            new string(format == ReportFormat.Pdf ? 'e' : 'f', 64),
            snapshot.Sha256, snapshot.SourceManifestSha256, snapshot.SourceManifestJson,
            snapshot.SourceCutoffUtc, snapshot.Classification, parsed);
    }

    private static PortfolioProjectSelection Project(Guid id, string currency, decimal spend) => new(
        id, id.ToString("N"), "Project", ProjectStatus.Active,
        LocalDate, "UTC", currency, 1, Cutoff.AddDays(-1), true, "policy-v1",
        PortfolioDimensionStatus.Available, ProjectOperationalStatus.Stable,
        DataCoverageStatus.Sufficient, DataFreshnessStatus.Current,
        DataConfidenceStatus.Adequate, false, Id(50), Cutoff.AddHours(-1),
        new string('a', 64), null,
        new PortfolioFinancialDimension(PortfolioDimensionStatus.Available, currency,
            spend, spend - 1, new string('b', 64), null, ReportClassification.Confidential),
        new PortfolioCommercialDimension(PortfolioDimensionStatus.NotAuthorized,
            null, null, null, null, null, ReportClassification.Internal),
        ReportClassification.Confidential);

    private static ReportingRendererOptions Options()
    {
        var fonts = Path.Combine(AppContext.BaseDirectory, "fonts");
        return new ReportingRendererOptions(CertifiedPdfRuntimeContract.LicenseDecision,
            Path.Combine(fonts, PmcsTypographyContract.PdfRegularFileName), Path.Combine(fonts, PmcsTypographyContract.PdfBoldFileName),
            CertifiedPdfRuntimeContract.RegularFontSha256, CertifiedPdfRuntimeContract.BoldFontSha256,
            CertifiedPdfRuntimeContract.RuntimeImageDigest);
    }

    private static string Read(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Missing {name}.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
