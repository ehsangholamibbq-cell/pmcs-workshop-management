using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;

namespace Pmcs.Domain.Tests;

public sealed class ProjectQualityHseReportRenderingTests
{
    private static readonly Guid Tenant = Id(1);
    private static readonly Guid Project = Id(2);
    private static readonly Guid Run = Id(500);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] SheetNames = ["Metadata", "Coverage", "Quality", "HSE"];
    private static readonly string[] ExpectedVisualDigests =
        ["54e9b27c34442fe5257d506e4fb9d8013d335569976d0f39932452ff1a01edec",
            "fd8ed2eb21a3264ab08f853d9301ceeec6379d8b5a2d7d2beb963554d1b28a36"];

    [Fact]
    public void F08RendererPinsTemplateSnapshotAndWholeClassification()
    {
        var snapshot = Snapshot();
        var parsed = ProjectQualityHseReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var request = Request(snapshot, ReportFormat.Pdf);
        var registry = new ProjectQualityHseReportRendererRegistry(
            new IProjectQualityHseReportRenderer[]
            {
                new ProjectQualityHseReportPdfRenderer(Options(), ReportingExecutionOptions.Default),
                new ProjectQualityHseReportXlsxRenderer(ReportingExecutionOptions.Default)
            });
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal("project-quality-hse-PRJ-F08-1405-07-05.pdf", request.FileName);
        Assert.Equal(ReportFormat.Pdf, registry.Require(ReportFormat.Pdf).Format);
        Assert.Equal(ReportFormat.Xlsx, registry.Require(ReportFormat.Xlsx).Format);
        Assert.Equal("reporting.format.unsupported",
            Assert.Throws<ReportRenderingException>(() => registry.Require(ReportFormat.Csv)).Code);
        Assert.Equal("reporting.project_quality_hse.render_request.invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectQualityHseReportRenderModel.Create(
                request with { SnapshotSha256 = new string('0', 64) })).Code);
        Assert.Equal("reporting.project_quality_hse.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectQualityHseReportRenderSnapshot.Parse(
                snapshot.PayloadJson.Replace(ProjectQualityHseReportRuntimeContract.SemanticContractId,
                    "PMCS-RPT1-F08-TAMPERED", StringComparison.Ordinal))).Code);
    }

    [Fact]
    public void F08XlsxGoldenKeepsIndependentUnknownCountAndNoFormula()
    {
        var request = Request(Snapshot(), ReportFormat.Xlsx);
        var renderer = new ProjectQualityHseReportXlsxRenderer(ReportingExecutionOptions.Default);
        var first = renderer.Render(request);
        var second = renderer.Render(request);
        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.True(first.Bytes.AsSpan().StartsWith("PK"u8));
        using var stream = new MemoryStream(first.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(11, archive.Entries.Count);
        var workbook = Read(archive, "xl/workbook.xml");
        foreach (var name in SheetNames)
            Assert.Contains(name, workbook, StringComparison.Ordinal);
        var sheets = Enumerable.Range(1, 4).Select(index => Read(archive,
            $"xl/worksheets/sheet{index}.xml")).ToArray();
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var xml in sheets)
        {
            var document = XDocument.Parse(xml);
            Assert.Equal("1", document.Descendants(spreadsheet + "sheetView").Single()
                .Attribute("rightToLeft")?.Value);
            Assert.Equal("frozen", document.Descendants(spreadsheet + "pane").Single()
                .Attribute("state")?.Value);
            Assert.Empty(document.Descendants(spreadsheet + "f"));
        }
        Assert.Contains("نامعلوم؛ صفر فرض نشود", sheets[1], StringComparison.Ordinal);
        Assert.Contains("زمان تغییر وضعیت تاریخی موجود نیست", sheets[1], StringComparison.Ordinal);
        Assert.Contains("'=SUM(A1:A2)", sheets[2], StringComparison.Ordinal);
        Assert.Contains("PassWithObservation", sheets[2], StringComparison.Ordinal);
        Assert.Single(XDocument.Parse(sheets[3]).Descendants(spreadsheet + "row"));
        Assert.DoesNotContain("PersonReference", string.Concat(sheets), StringComparison.Ordinal);
        Ms48FontGoldenArtifacts.Save("project-quality-hse-golden.xlsx", first.Bytes);
        Assert.True(first.Sha256 == "8a22452579b105b2927184caa97ddc4c64c99b91b59d440961c4e0c733fcc626",
            $"F08_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void F08PdfGoldenRendersTwoIndependentSections()
    {
        var request = Request(Snapshot(), ReportFormat.Pdf);
        var renderer = new ProjectQualityHseReportPdfRenderer(Options(), ReportingExecutionOptions.Default);
        var first = renderer.Render(request);
        var second = renderer.Render(request);
        var images = renderer.RenderQualificationImages(request);
        var repeated = renderer.RenderQualificationImages(request);
        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.True(first.Bytes.AsSpan().StartsWith("%PDF-"u8));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(first.Bytes[^32..]), StringComparison.Ordinal);
        Assert.Equal(2, images.Count);
        Assert.Equal(images.Count, repeated.Count);
        Assert.All(images, image => Assert.True(image.AsSpan().StartsWith(
            new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A })));
        for (var index = 0; index < images.Count; index++)
            Assert.True(images[index].SequenceEqual(repeated[index]));
        var visualDigests = images.Select(page =>
            Convert.ToHexString(SHA256.HashData(page)).ToLowerInvariant()).ToArray();
        Ms48FontGoldenArtifacts.Save("project-quality-hse-golden.pdf", first.Bytes);
        for (var index = 0; index < images.Count; index++)
            Ms48FontGoldenArtifacts.Save($"project-quality-hse-page-{index + 1}.png", images[index]);
        Assert.True(first.Sha256 == "b7e4ed87273b5270c124cdb54d76cfee7e50492630c3d5425090c30958f54213" &&
                visualDigests.SequenceEqual(ExpectedVisualDigests),
            $"F08_PDF_GOLDEN_SHA256={first.Sha256}; F08_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
    }

    [Fact]
    public void F08RendererRejectsFalseZeroAndUnapprovedRows()
    {
        var parsed = ProjectQualityHseReportRenderSnapshot.Parse(Snapshot().PayloadJson);
        Assert.Null(parsed.Hse.OfficialCount);
        Assert.Equal(2, parsed.Quality.OfficialCount);
        Assert.Equal("reporting.project_quality_hse.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectQualityHseReportRenderSnapshot.Parse(
                CanonicalJson.Serialize(parsed with
                {
                    Hse = parsed.Hse with { OfficialCount = 0 }
                }))).Code);
        Assert.Equal("reporting.project_quality_hse.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectQualityHseReportRenderSnapshot.Parse(
                CanonicalJson.Serialize(parsed with
                {
                    Quality = parsed.Quality with
                    {
                        Rows = parsed.Quality.Rows.Select(x => x with { State = "PersonReference" }).ToArray()
                    }
                }))).Code);
    }

    [Fact]
    public void F08ApprovedExposureDoesNotInventIncidentRateAndRestrictedSectionStaysRestricted()
    {
        var parsed = ProjectQualityHseReportRenderSnapshot.Parse(Snapshot().PayloadJson);
        var hse = new ProjectQualityHseReportSection(QualityHseReportingStatus.Available, 1,
            [new QualityHseReportingFact(Id(30), "EXH-00000030", QualityHseFactKind.ExposureHours,
                "Approved", Cutoff.AddHours(-1), 120.5m)],
            [QualityHseReportingReason.ExposureBasisIncomplete], ReportClassification.Restricted);
        var payload = parsed with
        {
            Hse = hse, DataStatus = ReportDataStatus.Available,
            Reasons = [QualityHseReportingReason.ExposureBasisIncomplete],
            Classification = ReportClassification.Restricted
        };
        var report = ReportSnapshot.Create(Id(604), Run, Tenant, Project,
            ProjectQualityHseReportRuntimeContract.SnapshotSchemaVersion,
            payload.DataStatus, CanonicalJson.Serialize(payload), "{}",
            payload.Classification, Cutoff.AddMinutes(2), Cutoff);
        var artifact = new ProjectQualityHseReportXlsxRenderer(
            ReportingExecutionOptions.Default).Render(Request(report, ReportFormat.Xlsx));
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Contains("120.5", Read(archive, "xl/worksheets/sheet4.xml"), StringComparison.Ordinal);
        Assert.Contains("مبنای محاسبه نرخ حادثه کامل نیست", Read(archive, "xl/worksheets/sheet2.xml"),
            StringComparison.Ordinal);
        Assert.DoesNotContain("incidentRate", Read(archive, "xl/worksheets/sheet1.xml"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ReportClassification.Restricted, report.Classification);
    }

    private static ReportSnapshot Snapshot()
    {
        var quality = new ProjectQualityHseReportSection(QualityHseReportingStatus.Available, 2,
            [new QualityHseReportingFact(Id(10), "=SUM(A1:A2)", QualityHseFactKind.QualityTest,
                "Inconclusive", Cutoff.AddDays(-2)),
             new QualityHseReportingFact(Id(11), "IR-11", QualityHseFactKind.InspectionResult,
                "PassWithObservation", Cutoff.AddDays(-1))], [], ReportClassification.Confidential);
        var hse = new ProjectQualityHseReportSection(QualityHseReportingStatus.InsufficientData,
            null, [], [QualityHseReportingReason.HistoricalTransitionUnavailable],
            ReportClassification.Confidential);
        var payload = new ProjectQualityHseReportSemanticSnapshot(
            ProjectQualityHseReportRuntimeContract.SnapshotSchemaVersion,
            ProjectQualityHseReportRuntimeContract.SemanticContractId,
            ProjectQualityHseReportRuntimeContract.DefinitionCode,
            ProjectQualityHseReportRuntimeContract.DefinitionVersion,
            ProjectQualityHseReportingContract.PolicyVersion, ReportDataStatus.InsufficientData,
            [QualityHseReportingReason.HistoricalTransitionUnavailable],
            new ProjectQualityHseReportParameters(),
            new ProjectQualityHseReportProjectIdentity(Project, Tenant, "PRJ-F08", "Test Project", "UTC",
                1, 1, Cutoff.AddDays(-30), Cutoff.AddMinutes(1),
                ProjectFeatureState.Active, ProjectFeatureState.Active),
            new ProjectQualityHseReportCutoffIdentity(Cutoff, new DateOnly(2026, 9, 27)),
            ReportClassification.Confidential, quality, hse,
            CanonicalJson.Sha256("{}"), new string('a', 64));
        return ReportSnapshot.Create(Id(602), Run, Tenant, Project,
            ProjectQualityHseReportRuntimeContract.SnapshotSchemaVersion,
            payload.DataStatus, CanonicalJson.Serialize(payload), "{}",
            payload.Classification, Cutoff.AddMinutes(2), Cutoff);
    }

    private static ProjectQualityHseReportRenderRequest Request(ReportSnapshot snapshot, ReportFormat format)
    {
        var parsed = ProjectQualityHseReportRenderSnapshot.Parse(snapshot.PayloadJson);
        return new ProjectQualityHseReportRenderRequest(Run, format == ReportFormat.Pdf ? Id(600) : Id(601),
            snapshot.Id, Id(603), ProjectQualityHseReportRuntimeContract.DefinitionCode,
            ProjectQualityHseReportRuntimeContract.DefinitionVersion,
            ProjectQualityHseReportRuntimeContract.TemplateVersion,
            ProjectQualityHseReportRuntimeContract.TemplateContentDigest,
            ProjectQualityHseReportRuntimeContract.RendererContractVersion,
            ProjectQualityHseReportRuntimeContract.LayoutContractVersion,
            format, ReportArtifactIdentity.FileName(parsed, format), "RPT-F080-0000-0000-0000-0001",
            new string(format == ReportFormat.Pdf ? 'e' : 'f', 64),
            snapshot.Sha256, snapshot.SourceManifestSha256, snapshot.SourceCutoffUtc, parsed);
    }

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
