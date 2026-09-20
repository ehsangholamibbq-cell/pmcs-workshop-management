using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Services;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Planning.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectProgressReportRenderingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid RunId = Id(3);
    private static readonly Guid BaselineId = Id(10);
    private static readonly Guid ActivityEntryId = Id(20);
    private static readonly Guid MilestoneEntryId = Id(21);
    private static readonly Guid MeasurementId = Id(30);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 20);
    private static readonly DateTimeOffset Cutoff =
        new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RendererContractPinsIdentityParsesCanonicalSnapshotAndRejectsTampering()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ProjectProgressReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var request = CreateRequest(snapshot, ReportFormat.Pdf);

        Assert.Equal(
            "pmcs.reporting.project-progress.renderer/v1",
            ProjectProgressReportRuntimeContract.RendererContractVersion);
        Assert.Equal(
            "pmcs.reporting.project-progress.layout/v1",
            ProjectProgressReportRuntimeContract.LayoutContractVersion);
        Assert.Equal("1.0.0", ProjectProgressReportRuntimeContract.TemplateVersion);
        Assert.Equal(
            "3f19d880a7790854fcc0d79d4822c5653cb6bb888294eadf8eaeeee8b5857816",
            ProjectProgressReportRuntimeContract.TemplateContentDigest);
        Assert.Equal(ProjectProgressReportRuntimeContract.DefinitionCode, parsed.DefinitionCode);
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal("project-progress-PRJ-F04-1405-06-29.pdf", request.FileName);

        var tamperedPayload = snapshot.PayloadJson.Replace(
            ProjectProgressReportRuntimeContract.DefinitionCode,
            "project-progress-tampered",
            StringComparison.Ordinal);
        var payloadError = Assert.Throws<ReportRenderingException>(() =>
            ProjectProgressReportRenderSnapshot.Parse(tamperedPayload));
        var hashError = Assert.Throws<ReportRenderingException>(() =>
            ProjectProgressReportRenderModel.Create(
                request with { SnapshotSha256 = new string('0', 64) }));

        Assert.Equal("reporting.project_progress.snapshot.payload_invalid", payloadError.Code);
        Assert.Equal("reporting.project_progress.render_request.invalid", hashError.Code);
        Assert.False(payloadError.Transient);
        Assert.False(hashError.Transient);
    }

    [Fact]
    public void CertifiedProjectProgressXlsxIsDeterministicGoldenRtlFormulaFreeAndSemantic()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Xlsx);
        var renderer = new ProjectProgressReportXlsxRenderer(ReportingExecutionOptions.Default);

        var first = renderer.Render(request);
        var second = renderer.Render(request);

        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", first.ContentType);
        Assert.True(first.Bytes.AsSpan().StartsWith("PK"u8));

        using var stream = new MemoryStream(first.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(
            [
                "[Content_Types].xml",
                "_rels/.rels",
                "docProps/app.xml",
                "docProps/core.xml",
                "xl/workbook.xml",
                "xl/_rels/workbook.xml.rels",
                "xl/styles.xml",
                "xl/worksheets/sheet1.xml",
                "xl/worksheets/sheet2.xml",
                "xl/worksheets/sheet3.xml",
                "xl/worksheets/sheet4.xml",
                "xl/worksheets/sheet5.xml",
                "xl/worksheets/sheet6.xml",
                "xl/worksheets/sheet7.xml",
                "xl/worksheets/sheet8.xml"
            ],
            archive.Entries.Select(item => item.FullName).ToArray());

        var workbookXml = ReadEntry(archive, "xl/workbook.xml");
        foreach (var sheetName in new[]
        {
            "Metadata", "Summary", "Configuration", "Baseline",
            "Entries", "Milestones", "S-Curve", "Lineage"
        })
        {
            Assert.Contains(sheetName, workbookXml, StringComparison.Ordinal);
        }

        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var worksheetXml = Enumerable.Range(1, 8)
            .Select(index => ReadEntry(archive, $"xl/worksheets/sheet{index}.xml"))
            .ToArray();
        foreach (var xml in worksheetXml)
        {
            var document = XDocument.Parse(xml);
            Assert.Equal(spreadsheet + "worksheet", document.Root!.Name);
            Assert.Equal("1", document.Descendants(spreadsheet + "sheetView").Single()
                .Attribute("rightToLeft")?.Value);
            Assert.Equal("frozen", document.Descendants(spreadsheet + "pane").Single()
                .Attribute("state")?.Value);
            Assert.Empty(document.Descendants(spreadsheet + "f"));
        }

        Assert.Contains("'=SUM(A1:A2) باید متن بماند", worksheetXml[4], StringComparison.Ordinal);
        Assert.Contains("Actual - Planned", worksheetXml[7], StringComparison.Ordinal);
        Assert.Contains(
            XDocument.Parse(worksheetXml[1]).Descendants(spreadsheet + "v"),
            value => decimal.TryParse(
                value.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed == 32m);
        Assert.Contains("—", worksheetXml[6], StringComparison.Ordinal);
        var allWorksheets = string.Concat(worksheetXml);
        Assert.DoesNotContain("Forecast", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Earned Value", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">SPI<", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">CPI<", allWorksheets, StringComparison.OrdinalIgnoreCase);

        WriteQualificationArtifacts("project-progress-golden.xlsx", first.Bytes);
        Assert.True(
            string.Equals(
                new string('0', 64),
                first.Sha256,
                StringComparison.Ordinal),
            $"F04_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void CertifiedProjectProgressPdfIsDeterministicVisuallyPinnedAndWithinPerformanceBudget()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Pdf);
        var renderer = new ProjectProgressReportPdfRenderer(
            RendererOptions(),
            ReportingExecutionOptions.Default);

        var timer = Stopwatch.StartNew();
        var first = renderer.Render(request);
        timer.Stop();
        var coldRender = timer.Elapsed;
        timer.Restart();
        var second = renderer.Render(request);
        timer.Stop();
        var warmRender = timer.Elapsed;
        var firstImages = renderer.RenderQualificationImages(request);
        var secondImages = renderer.RenderQualificationImages(request);

        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal("application/pdf", first.ContentType);
        Assert.True(first.Bytes.AsSpan().StartsWith("%PDF-"u8));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(first.Bytes[^32..]), StringComparison.Ordinal);
        Assert.InRange(first.Bytes.Length, 1, CertifiedPdfRuntimeContract.QualificationMaximumPdfBytes);
        Assert.True(
            coldRender.TotalMilliseconds <= CertifiedPdfRuntimeContract.QualificationColdRenderBudgetMilliseconds,
            $"Cold Project Progress PDF render took {coldRender.TotalMilliseconds:F1} ms.");
        Assert.True(
            warmRender.TotalMilliseconds <= CertifiedPdfRuntimeContract.QualificationWarmRenderBudgetMilliseconds,
            $"Warm Project Progress PDF render took {warmRender.TotalMilliseconds:F1} ms.");
        Assert.True(firstImages.Count >= 2);
        Assert.Equal(firstImages.Count, secondImages.Count);
        for (var index = 0; index < firstImages.Count; index++)
        {
            Assert.True(firstImages[index].AsSpan().StartsWith(
                new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }));
            Assert.True(firstImages[index].SequenceEqual(secondImages[index]));
        }

        var visualDigests = firstImages
            .Select(image => Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant())
            .ToArray();
        WriteQualificationArtifacts("project-progress-golden.pdf", first.Bytes);
        for (var index = 0; index < firstImages.Count; index++)
        {
            WriteQualificationArtifacts(
                $"project-progress-golden-page-{index + 1}.png",
                firstImages[index]);
        }

        var expectedVisualDigests = new[]
        {
            "556d3b6a56d59e0c4ac8e8fcd526fca405fe9ba066ae5823b2c9c7482ea4b69b",
            "8fbb9b69d9322522e8a55f041e16c8785716dc7554660bf2eacdf9030fe5e850"
        };
        Assert.True(
            string.Equals(
                "bdc9c3a99c1dc5a0da57f9431d7bc7f04830fbbfbeb578b24c7234df708785ef",
                first.Sha256,
                StringComparison.Ordinal) &&
                expectedVisualDigests.SequenceEqual(visualDigests),
            $"F04_PDF_GOLDEN_SHA256={first.Sha256}; " +
            $"F04_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
    }

    [Fact]
    public void NoDataWorkbookKeepsSemanticSheetsHeaderOnlyAndDoesNotFabricateZeroMetrics()
    {
        var result = ProjectProgressReportingCalculator.Calculate(ProjectProgressReportingSelector.Select(
            Projection(PlanningMode.WbsBaseline, [], Evidence())));
        var snapshot = Build(result);
        var request = CreateRequest(snapshot, ReportFormat.Xlsx);

        var artifact = new ProjectProgressReportXlsxRenderer(ReportingExecutionOptions.Default)
            .Render(request);

        Assert.Equal(ReportDataStatus.NoData, request.Snapshot.DataStatus);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var sheetNumber in new[] { 2, 4, 5, 6, 7 })
        {
            var document = XDocument.Parse(ReadEntry(
                archive,
                $"xl/worksheets/sheet{sheetNumber}.xml"));
            Assert.Single(document.Descendants(spreadsheet + "row"));
        }
        var allWorksheets = string.Concat(Enumerable.Range(1, 8)
            .Select(index => ReadEntry(archive, $"xl/worksheets/sheet{index}.xml")));
        Assert.Contains("داده رسمی وجود ندارد", allWorksheets, StringComparison.Ordinal);
        Assert.Contains("Baseline رسمی واجد شرایط وجود ندارد", allWorksheets, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererRejectsOversizedTextAndFutureActualInsteadOfSilentlyRepairingThem()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ProjectProgressReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var oversized = parsed with
        {
            Entries = parsed.Entries.Select((entry, index) => index == 0
                ? entry with
                {
                    Title = new string('ا', ProjectProgressReportRenderingContract.MaximumEntryTitleLength + 1)
                }
                : entry).ToArray()
        };
        var futureActual = parsed with
        {
            Curve = parsed.Curve.Select(point => point.PointDate > parsed.Cutoff.CutoffLocalDate
                ? point with { ActualPercent = 1m }
                : point).ToArray()
        };

        var textError = Assert.Throws<ReportRenderingException>(() =>
            ProjectProgressReportRenderSnapshot.Parse(CanonicalJson.Serialize(oversized)));
        var futureError = Assert.Throws<ReportRenderingException>(() =>
            ProjectProgressReportRenderSnapshot.Parse(CanonicalJson.Serialize(futureActual)));

        Assert.Equal("reporting.project_progress.snapshot.payload_invalid", textError.Code);
        Assert.Equal("reporting.project_progress.snapshot.payload_invalid", futureError.Code);
        Assert.False(textError.Transient);
        Assert.False(futureError.Transient);
    }

    [Fact]
    public void RenderersFailClosedOnRowBudgetsAndWrongFormats()
    {
        var snapshot = BuildAvailableSnapshot();
        var bounded = ReportingExecutionOptions.Default with
        {
            MaximumPdfFacts = 1,
            MaximumXlsxRows = 1
        };
        var pdf = new ProjectProgressReportPdfRenderer(RendererOptions(), bounded);
        var xlsx = new ProjectProgressReportXlsxRenderer(bounded);
        var pdfRequest = CreateRequest(snapshot, ReportFormat.Pdf);
        var xlsxRequest = CreateRequest(snapshot, ReportFormat.Xlsx);

        var pdfLimit = Assert.Throws<ReportRenderingException>(() => pdf.Render(pdfRequest));
        var xlsxLimit = Assert.Throws<ReportRenderingException>(() => xlsx.Render(xlsxRequest));
        var wrongPdfFormat = Assert.Throws<ReportRenderingException>(() =>
            pdf.Render(pdfRequest with { Format = ReportFormat.Xlsx }));
        var wrongXlsxFormat = Assert.Throws<ReportRenderingException>(() =>
            xlsx.Render(xlsxRequest with { Format = ReportFormat.Pdf }));

        Assert.Equal("reporting.output.page_limit_exceeded", pdfLimit.Code);
        Assert.Equal("reporting.output.row_limit_exceeded", xlsxLimit.Code);
        Assert.Equal("reporting.format.unsupported", wrongPdfFormat.Code);
        Assert.Equal("reporting.format.unsupported", wrongXlsxFormat.Code);
        Assert.All(
            new[] { pdfLimit, xlsxLimit, wrongPdfFormat, wrongXlsxFormat },
            exception => Assert.False(exception.Transient));
    }

    private static ReportSnapshot BuildAvailableSnapshot()
    {
        var result = ProjectProgressReportingCalculator.Calculate(ProjectProgressReportingSelector.Select(
            Projection(
                PlanningMode.WbsBaseline,
                [ScheduledBaseline()],
                Evidence(
                    Fact(Id(100), MeasurementId, 80m),
                    Fact(Id(101), Id(999), 10m)),
                [MilestoneUpdate()],
                ProjectProgressReportingClassification.Restricted)));
        return Build(result);
    }

    private static ProjectProgressReportingProjection Projection(
        PlanningMode mode,
        IReadOnlyCollection<ProjectProgressBaselineVersion> baselines,
        ProgressEvidenceReportingProjection evidence,
        IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion>? milestones = null,
        ProjectProgressReportingClassification classification = ProjectProgressReportingClassification.Internal) => new(
        ProjectProgressReportingContract.Version,
        TenantId,
        ProjectId,
        CutoffLocalDate,
        Cutoff,
        [new ProjectProgressConfigurationVersion(
            7,
            12,
            ProgressReportingEnabled: true,
            mode,
            ProjectProgressCalendarState.NotConfigured,
            null,
            7,
            Cutoff.AddDays(-100),
            null,
            classification)],
        baselines,
        milestones ?? [],
        evidence);

    private static ProjectProgressBaselineVersion ScheduledBaseline()
    {
        var approvedAt = Cutoff.AddDays(-20);
        return new ProjectProgressBaselineVersion(
            BaselineId,
            "BL-F04-01",
            "Baseline رسمی کنترل پروژه",
            PlanningBaselineKind.WbsBaseline,
            3,
            approvedAt,
            null,
            "Primavera P6",
            "BL-2026-A",
            ProjectProgressReportingClassification.Restricted,
            [
                new ProjectProgressBaselineEntry(
                    ActivityEntryId,
                    null,
                    "ACT-01",
                    "=SUM(A1:A2) باید متن بماند",
                    PlanningEntryKind.Activity,
                    ProgressMeasurementMethod.QuantityBased,
                    MeasurementId,
                    CutoffLocalDate.AddDays(-2),
                    CutoffLocalDate.AddDays(2),
                    60m,
                    1,
                    new ProjectProgressPinnedMeasurementTarget(
                        MeasurementId,
                        "M-01",
                        "بتن‌ریزی سازه",
                        "m3",
                        100m,
                        1,
                        approvedAt)),
                new ProjectProgressBaselineEntry(
                    MilestoneEntryId,
                    null,
                    "MS-01",
                    "تحویل سازه",
                    PlanningEntryKind.Milestone,
                    ProgressMeasurementMethod.ManualPercent,
                    null,
                    CutoffLocalDate.AddDays(1),
                    CutoffLocalDate.AddDays(1),
                    40m,
                    2,
                    null)
            ]);
    }

    private static ProjectProgressMilestoneUpdateVersion MilestoneUpdate() => new(
        Id(300),
        BaselineId,
        MilestoneEntryId,
        CutoffLocalDate.AddDays(-1),
        50m,
        2,
        Cutoff.AddDays(-1),
        null,
        ProjectProgressReportingClassification.Restricted);

    private static ProgressEvidenceReportingProjection Evidence(
        params ProgressEvidenceReportingFact[] facts)
    {
        if (facts.Length == 0)
        {
            return ProgressEvidenceReportingSelector.Select(
                TenantId,
                ProjectId,
                CutoffLocalDate,
                Cutoff,
                []);
        }

        return ProgressEvidenceReportingSelector.Select(
            TenantId,
            ProjectId,
            CutoffLocalDate,
            Cutoff,
            [new ProgressEvidenceReportingVersion(
                Id(90),
                Id(90),
                1,
                CutoffLocalDate.AddDays(-1),
                Cutoff.AddDays(-2),
                null,
                facts)],
            ProgressEvidenceReportingClassification.Restricted);
    }

    private static ProgressEvidenceReportingFact Fact(
        Guid id,
        Guid? measurementId,
        decimal? quantity) => new(
        id,
        measurementId,
        quantity,
        "m3",
        Cutoff.AddDays(-3));

    private static ReportSnapshot Build(ProjectProgressReportingResult result) =>
        ProjectProgressReportSnapshotBuilder.Build(
            RunId,
            TenantId,
            Profile(),
            Cutoff,
            result,
            Cutoff.AddMinutes(1),
            Cutoff.AddMinutes(2));

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-F04",
        "پروژه نمونه پیشرفت",
        "Asia/Tehran",
        "IRR",
        12,
        Cutoff.AddDays(-1),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.WbsBaseline,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.SetupRequired,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 62),
        ConfigurationVersion: 7);

    private static ProjectProgressReportRenderRequest CreateRequest(
        ReportSnapshot snapshot,
        ReportFormat format)
    {
        var parsed = ProjectProgressReportRenderSnapshot.Parse(snapshot.PayloadJson);
        return new ProjectProgressReportRenderRequest(
            RunId,
            format == ReportFormat.Pdf ? Id(400) : Id(401),
            Id(402),
            Id(403),
            ProjectProgressReportRuntimeContract.DefinitionCode,
            ProjectProgressReportRuntimeContract.DefinitionVersion,
            ProjectProgressReportRuntimeContract.TemplateVersion,
            ProjectProgressReportRuntimeContract.TemplateContentDigest,
            ProjectProgressReportRuntimeContract.RendererContractVersion,
            ProjectProgressReportRuntimeContract.LayoutContractVersion,
            format,
            ReportArtifactIdentity.FileName(parsed, format),
            "RPT-F040-0000-0000-0000-0001",
            new string(format == ReportFormat.Pdf ? 'e' : 'f', 64),
            snapshot.Sha256,
            snapshot.SourceManifestSha256,
            snapshot.SourceCutoffUtc,
            parsed);
    }

    private static ReportingRendererOptions RendererOptions()
    {
        var fonts = Path.Combine(AppContext.BaseDirectory, "fonts");
        return new ReportingRendererOptions(
            CertifiedPdfRuntimeContract.LicenseDecision,
            Path.Combine(fonts, "DejaVuSans.ttf"),
            Path.Combine(fonts, "DejaVuSans-Bold.ttf"),
            CertifiedPdfRuntimeContract.RegularFontSha256,
            CertifiedPdfRuntimeContract.BoldFontSha256,
            CertifiedPdfRuntimeContract.RuntimeImageDigest);
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"Missing {name}.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteQualificationArtifacts(string fileName, byte[] bytes)
    {
        var output = Environment.GetEnvironmentVariable("PMCS_F04_RENDER_QUALIFICATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, fileName), bytes);
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}
