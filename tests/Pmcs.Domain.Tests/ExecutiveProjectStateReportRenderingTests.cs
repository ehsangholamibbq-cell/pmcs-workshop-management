using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.ProjectIntelligence.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ExecutiveProjectStateReportRenderingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid RunId = Id(3);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 20);
    private static readonly DateTimeOffset Cutoff =
        new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RendererContractPinsIdentityParsesCanonicalSnapshotAndRejectsTampering()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ExecutiveProjectStateReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var request = CreateRequest(snapshot, ReportFormat.Pdf);

        Assert.Equal(
            "pmcs.reporting.executive-project-state.renderer/v1",
            ExecutiveProjectStateReportRuntimeContract.RendererContractVersion);
        Assert.Equal(
            "pmcs.reporting.executive-project-state.layout/v1",
            ExecutiveProjectStateReportRuntimeContract.LayoutContractVersion);
        Assert.Equal("1.0.0", ExecutiveProjectStateReportRuntimeContract.TemplateVersion);
        Assert.Equal(
            "4bf4f1f5de92eda854ab16702fc87aaebae951eea17ef023569cc338a5ce7d7a",
            ExecutiveProjectStateReportRuntimeContract.TemplateContentDigest);
        Assert.Equal(ExecutiveProjectStateReportRuntimeContract.DefinitionCode, parsed.DefinitionCode);
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal(
            "executive-project-state-PRJ-F03-1405-06-29.pdf",
            request.FileName);

        var tamperedPayload = snapshot.PayloadJson.Replace(
            ExecutiveProjectStateReportRuntimeContract.DefinitionCode,
            "executive-project-state-tampered",
            StringComparison.Ordinal);
        var payloadError = Assert.Throws<ReportRenderingException>(() =>
            ExecutiveProjectStateReportRenderSnapshot.Parse(tamperedPayload));
        var hashError = Assert.Throws<ReportRenderingException>(() =>
            ExecutiveProjectStateReportRenderModel.Create(
                request with { SnapshotSha256 = new string('0', 64) }));

        Assert.Equal("reporting.executive_state.snapshot.payload_invalid", payloadError.Code);
        Assert.Equal("reporting.executive_state.render_request.invalid", hashError.Code);
        Assert.False(payloadError.Transient);
        Assert.False(hashError.Transient);
    }

    [Fact]
    public void CertifiedExecutiveStateXlsxIsDeterministicGoldenRtlFormulaFreeAndSemantic()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Xlsx);
        var renderer = new ExecutiveProjectStateReportXlsxRenderer(ReportingExecutionOptions.Default);

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
            "Metadata", "State", "Coverage", "Fact Counts",
            "Feature States", "Attention", "Trend", "Lineage"
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

        Assert.Contains("'=SUM(A1:A2) باید متن بماند", worksheetXml[5], StringComparison.Ordinal);
        Assert.Contains("ارزیابی نشده", worksheetXml[5], StringComparison.Ordinal);
        Assert.Contains("محدود/جزئی", worksheetXml[1], StringComparison.Ordinal);
        Assert.Contains("فقط وضعیت پیکربندی", worksheetXml[4], StringComparison.Ordinal);
        Assert.DoesNotContain("Composite", string.Concat(worksheetXml), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("امتیاز سلامت", string.Concat(worksheetXml), StringComparison.Ordinal);

        WriteQualificationArtifacts("executive-project-state-golden.xlsx", first.Bytes);
        Assert.True(
            string.Equals(
                "e19809b6c3ffa5ff3443babe683c9f286c3b928986d176f1d515166f336cf5a3",
                first.Sha256,
                StringComparison.Ordinal),
            $"F03_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void CertifiedExecutiveStatePdfIsDeterministicVisuallyPinnedAndWithinPerformanceBudget()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Pdf);
        var renderer = new ExecutiveProjectStateReportPdfRenderer(
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
            $"Cold Executive Project State PDF render took {coldRender.TotalMilliseconds:F1} ms.");
        Assert.True(
            warmRender.TotalMilliseconds <= CertifiedPdfRuntimeContract.QualificationWarmRenderBudgetMilliseconds,
            $"Warm Executive Project State PDF render took {warmRender.TotalMilliseconds:F1} ms.");
        Assert.NotEmpty(firstImages);
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
        WriteQualificationArtifacts("executive-project-state-golden.pdf", first.Bytes);
        for (var index = 0; index < firstImages.Count; index++)
        {
            WriteQualificationArtifacts(
                $"executive-project-state-golden-page-{index + 1}.png",
                firstImages[index]);
        }

        Assert.True(
            string.Equals(
                "d765dfc98873fbc07e28b7320524fd156cfa5acc80c6f4da2b2d42941c4e09d1",
                first.Sha256,
                StringComparison.Ordinal),
            $"F03_PDF_GOLDEN_SHA256={first.Sha256}; " +
            $"F03_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
        Assert.True(
            new[]
            {
                "6d18d03ff3e9100ffe0e12c5da05a6f1976c3d7c1d2ba7f27b36656dcc5ff0ba",
                "252a6dd6c8562242a37e4466dfb4d0a0155831abb39c30e2751309eb3acfa205"
            }.SequenceEqual(visualDigests),
            $"F03_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
    }

    [Fact]
    public void NoDataWorkbookKeepsSemanticSheetsHeaderOnlyAndDoesNotFabricateZeroMetrics()
    {
        var snapshot = Build(ProjectStateReportingSelector.Select(
            TenantId,
            ProjectId,
            CutoffLocalDate,
            Cutoff,
            ProjectStateReportingSourceState.Configured,
            ProjectStateReportingClassification.Internal,
            null,
            []));
        var request = CreateRequest(snapshot, ReportFormat.Xlsx);

        var artifact = new ExecutiveProjectStateReportXlsxRenderer(ReportingExecutionOptions.Default)
            .Render(request);

        Assert.Equal(ReportDataStatus.NoData, request.Snapshot.DataStatus);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var sheetNumber in Enumerable.Range(2, 7))
        {
            var document = XDocument.Parse(ReadEntry(
                archive,
                $"xl/worksheets/sheet{sheetNumber}.xml"));
            Assert.Single(document.Descendants(spreadsheet + "row"));
        }
        var allWorksheets = string.Concat(Enumerable.Range(1, 8)
            .Select(index => ReadEntry(archive, $"xl/worksheets/sheet{index}.xml")));
        Assert.DoesNotContain("<v>0</v>", allWorksheets, StringComparison.Ordinal);
        Assert.Contains("داده رسمی وجود ندارد", allWorksheets, StringComparison.Ordinal);
        Assert.Contains("Snapshot رسمی واجد شرایط وجود ندارد", allWorksheets, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererRejectsOversizedAttentionInsteadOfSilentlyTruncatingIt()
    {
        var oversized = Attention(
            41,
            ProjectAttentionKind.Issue,
            ProjectAttentionPriority.Unassessed,
            null,
            2,
            new string('ا', ExecutiveProjectStateReportRenderingContract.MaximumAttentionDescriptionLength + 1));
        var snapshot = Snapshot(30, attentionItems: [oversized]);
        var report = Build(Select([snapshot]));

        var exception = Assert.Throws<ReportRenderingException>(() =>
            ExecutiveProjectStateReportRenderSnapshot.Parse(report.PayloadJson));

        Assert.Equal("reporting.executive_state.snapshot.payload_invalid", exception.Code);
        Assert.False(exception.Transient);
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
        var pdf = new ExecutiveProjectStateReportPdfRenderer(RendererOptions(), bounded);
        var xlsx = new ExecutiveProjectStateReportXlsxRenderer(bounded);
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
        var attention = new[]
        {
            Attention(41, ProjectAttentionKind.Issue, ProjectAttentionPriority.Unassessed, null, 2,
                "=SUM(A1:A2) باید متن بماند"),
            Attention(31, ProjectAttentionKind.Issue, ProjectAttentionPriority.High, ProjectObservedImpact.High, 1),
            Attention(21, ProjectAttentionKind.Stoppage, ProjectAttentionPriority.Critical, ProjectObservedImpact.Critical, 3),
            Attention(11, ProjectAttentionKind.Issue, ProjectAttentionPriority.Medium, ProjectObservedImpact.Medium, 4)
        };
        var selected = Snapshot(30, attentionItems: attention) with
        {
            Classification = ProjectStateReportingClassification.Restricted,
            OperationalStatus = ProjectOperationalStatus.Watch
        };
        var previous = Snapshot(
            20,
            CutoffLocalDate.AddDays(-1),
            Cutoff.AddDays(-1).AddHours(-1)) with
        {
            CoveragePercent = 85.71m,
            ApprovedReportDays = 6,
            OperationalStatus = ProjectOperationalStatus.Watch,
            FreshnessStatus = DataFreshnessStatus.Aging
        };
        var oldest = Snapshot(
            10,
            CutoffLocalDate.AddDays(-2),
            Cutoff.AddDays(-2).AddHours(-1)) with
        {
            CoveragePercent = 71.43m,
            ApprovedReportDays = 5,
            OperationalStatus = ProjectOperationalStatus.AtRisk,
            FreshnessStatus = DataFreshnessStatus.Aging
        };
        return Build(Select(
            [selected, oldest, previous],
            ProjectStateReportingClassification.Restricted));
    }

    private static ReportSnapshot Build(ProjectStateReportingSelection selection) =>
        ExecutiveProjectStateReportSnapshotBuilder.Build(
            RunId,
            TenantId,
            Profile(),
            Cutoff,
            selection,
            Cutoff.AddMinutes(1),
            Cutoff.AddMinutes(2));

    private static ProjectStateReportingSelection Select(
        ProjectStateReportingSnapshot[] snapshots,
        ProjectStateReportingClassification classification = ProjectStateReportingClassification.Internal) =>
        ProjectStateReportingSelector.Select(
            TenantId,
            ProjectId,
            CutoffLocalDate,
            Cutoff,
            ProjectStateReportingSourceState.Configured,
            classification,
            snapshots.Length == 0 ? null : Cutoff.AddHours(-2),
            snapshots);

    private static ProjectStateReportingSnapshot Snapshot(
        int id,
        DateOnly? asOfDate = null,
        DateTimeOffset? calculatedAt = null,
        IReadOnlyCollection<ProjectStateReportingAttentionItem>? attentionItems = null)
    {
        var date = asOfDate ?? CutoffLocalDate;
        var calculated = calculatedAt ?? Cutoff.AddHours(-1);
        var attention = attentionItems ?? [];
        return new ProjectStateReportingSnapshot(
            Id(id),
            TenantId,
            ProjectId,
            "PRJ-F03",
            "پروژه نمونه مدیریت",
            ProjectStateReportingClassification.Internal,
            ProjectStateCalculator.CalculationVersion,
            7,
            date,
            calculated,
            date.AddDays(-6),
            date,
            ProjectAssessmentScope.ApprovedDailyOperations,
            IsPartial: true,
            ProjectOperationalStatus.Stable,
            DataCoverageStatus.Sufficient,
            DataFreshnessStatus.Current,
            DataConfidenceStatus.Adequate,
            ProjectCoverageBasis.ConfiguredWorkingDays,
            100m,
            7,
            7,
            date,
            12,
            3,
            2,
            2,
            1,
            attention.Count(item => item.Kind == ProjectAttentionKind.Issue),
            attention.Count(item => item.Kind == ProjectAttentionKind.Stoppage),
            attention.Count(item => item.Priority == ProjectAttentionPriority.High),
            attention.Count(item => item.Priority == ProjectAttentionPriority.Critical),
            attention.Count == 0 ? null : attention.Max(item => item.AgeDays),
            ProjectFeatureState.Active,
            ProjectFeatureState.Active,
            ProjectFeatureState.SetupRequired,
            ProjectFeatureState.NotEnabled,
            ProjectFeatureState.NotConfigured,
            calculated.AddMinutes(-1),
            attention);
    }

    private static ProjectStateReportingAttentionItem Attention(
        int id,
        ProjectAttentionKind kind,
        ProjectAttentionPriority priority,
        ProjectObservedImpact? impact,
        int ageDays,
        string? description = null) => new(
        Id(1000 + id),
        Id(id),
        CutoffLocalDate.AddDays(-ageDays),
        kind,
        description ?? $"مورد توجه رسمی {id}",
        id % 2 == 0 ? "اجرایی" : null,
        Id(2000 + id),
        "طبقه اول",
        impact,
        priority,
        ageDays,
        ageDays switch
        {
            <= 1 => ProjectAttentionAgeBand.New,
            <= 3 => ProjectAttentionAgeBand.Aging,
            _ => ProjectAttentionAgeBand.Overdue
        },
        ProjectAttentionStatus.NeedsTriage,
        id % 2 == 0 ? $"REF-{id}" : null);

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-F03",
        "پروژه نمونه مدیریت",
        "Asia/Tehran",
        "IRR",
        7,
        Cutoff.AddDays(-1),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.None,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.SetupRequired,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 127),
        ConfigurationVersion: 3);

    private static ExecutiveProjectStateReportRenderRequest CreateRequest(
        ReportSnapshot snapshot,
        ReportFormat format)
    {
        var parsed = ExecutiveProjectStateReportRenderSnapshot.Parse(snapshot.PayloadJson);
        return new ExecutiveProjectStateReportRenderRequest(
            RunId,
            format == ReportFormat.Pdf ? Id(10) : Id(11),
            Id(12),
            Id(13),
            ExecutiveProjectStateReportRuntimeContract.DefinitionCode,
            ExecutiveProjectStateReportRuntimeContract.DefinitionVersion,
            ExecutiveProjectStateReportRuntimeContract.TemplateVersion,
            ExecutiveProjectStateReportRuntimeContract.TemplateContentDigest,
            ExecutiveProjectStateReportRuntimeContract.RendererContractVersion,
            ExecutiveProjectStateReportRuntimeContract.LayoutContractVersion,
            format,
            ReportArtifactIdentity.FileName(parsed, format),
            "RPT-F030-0000-0000-0000-0001",
            new string(format == ReportFormat.Pdf ? 'c' : 'd', 64),
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
        var output = Environment.GetEnvironmentVariable("PMCS_F03_RENDER_QUALIFICATION_OUTPUT");
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
