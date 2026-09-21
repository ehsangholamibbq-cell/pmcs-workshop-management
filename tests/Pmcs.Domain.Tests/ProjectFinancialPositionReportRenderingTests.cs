using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Finance.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectFinancialPositionReportRenderingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid RunId = Id(3);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 20);
    private static readonly DateTimeOffset Cutoff =
        new(2026, 9, 20, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void RendererContractPinsIdentityParsesCanonicalSnapshotAndRejectsTampering()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ProjectFinancialPositionReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var request = CreateRequest(snapshot, ReportFormat.Pdf);
        var pdf = new ProjectFinancialPositionReportPdfRenderer(
            RendererOptions(),
            ReportingExecutionOptions.Default);
        var xlsx = new ProjectFinancialPositionReportXlsxRenderer(
            ReportingExecutionOptions.Default);
        var registry = new ProjectFinancialPositionReportRendererRegistry(
            new IProjectFinancialPositionReportRenderer[] { pdf, xlsx });

        Assert.Equal(
            "pmcs.reporting.project-financial-position.renderer/v1",
            ProjectFinancialPositionReportRuntimeContract.RendererContractVersion);
        Assert.Equal(
            "pmcs.reporting.project-financial-position.layout/v1",
            ProjectFinancialPositionReportRuntimeContract.LayoutContractVersion);
        Assert.Equal("1.0.0", ProjectFinancialPositionReportRuntimeContract.TemplateVersion);
        Assert.Equal(
            "e6ad4cbf2559d825d70b1579e687e7f9ce15020afaf697692e263f18480f18e4",
            ProjectFinancialPositionReportRuntimeContract.TemplateContentDigest);
        Assert.Equal(ProjectFinancialPositionReportRuntimeContract.DefinitionCode, parsed.DefinitionCode);
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal(
            "project-financial-position-PRJ-F05-1405-06-29.pdf",
            request.FileName);
        Assert.Same(pdf, registry.Require(ReportFormat.Pdf));
        Assert.Same(xlsx, registry.Require(ReportFormat.Xlsx));

        var tamperedPayload = snapshot.PayloadJson.Replace(
            ProjectFinancialPositionReportRuntimeContract.SemanticContractId,
            "PMCS-RPT1-F05-TAMPERED",
            StringComparison.Ordinal);
        var payloadError = Assert.Throws<ReportRenderingException>(() =>
            ProjectFinancialPositionReportRenderSnapshot.Parse(tamperedPayload));
        var hashError = Assert.Throws<ReportRenderingException>(() =>
            ProjectFinancialPositionReportRenderModel.Create(
                request with { SnapshotSha256 = new string('0', 64) }));
        var registryError = Assert.Throws<ReportRenderingException>(() =>
            registry.Require(ReportFormat.Csv));

        Assert.Equal("reporting.project_financial_position.snapshot.payload_invalid", payloadError.Code);
        Assert.Equal("reporting.project_financial_position.render_request.invalid", hashError.Code);
        Assert.Equal("reporting.format.unsupported", registryError.Code);
        Assert.All(
            new[] { payloadError, hashError, registryError },
            exception => Assert.False(exception.Transient));
    }

    [Fact]
    public void CertifiedFinancialPositionXlsxIsDeterministicGoldenRtlFormulaFreeAndSemantic()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Xlsx);
        var renderer = new ProjectFinancialPositionReportXlsxRenderer(
            ReportingExecutionOptions.Default);

        var first = renderer.Render(request);
        var second = renderer.Render(request);

        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            first.ContentType);
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
            "Metadata", "Cash", "Budget", "Summaries", "Aging",
            "Open Obligations", "Source Counts", "Lineage"
        })
        {
            Assert.Contains(sheetName, workbookXml, StringComparison.Ordinal);
        }

        var spreadsheet =
            (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
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

        Assert.Contains("'=SUM(A1:A2)", worksheetXml[5], StringComparison.Ordinal);
        Assert.Contains("پرداختنی", worksheetXml[4], StringComparison.Ordinal);
        Assert.Contains("دریافتنی", worksheetXml[4], StringComparison.Ordinal);
        Assert.Contains("بالاتر از صد", worksheetXml[7], StringComparison.Ordinal);
        Assert.Contains(
            XDocument.Parse(worksheetXml[2]).Descendants(spreadsheet + "v"),
            value => decimal.TryParse(
                value.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed == 120m);
        var allWorksheets = string.Concat(worksheetXml);
        Assert.DoesNotContain("Forecast", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Earned Value", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Management Fee", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">CPI<", allWorksheets, StringComparison.OrdinalIgnoreCase);

        WriteQualificationArtifacts("project-financial-position-golden.xlsx", first.Bytes);
        Assert.True(
            string.Equals(
                "cadb7f0dc5670f401df879f04efdd930cf799213194e7cdf43c0d5d5e75a6222",
                first.Sha256,
                StringComparison.Ordinal),
            $"F05_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void CertifiedFinancialPositionPdfIsDeterministicVisuallyPinnedAndWithinPerformanceBudget()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Pdf);
        var renderer = new ProjectFinancialPositionReportPdfRenderer(
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
            coldRender.TotalMilliseconds <=
                CertifiedPdfRuntimeContract.QualificationColdRenderBudgetMilliseconds,
            $"Cold Project Financial Position PDF render took {coldRender.TotalMilliseconds:F1} ms.");
        Assert.True(
            warmRender.TotalMilliseconds <=
                CertifiedPdfRuntimeContract.QualificationWarmRenderBudgetMilliseconds,
            $"Warm Project Financial Position PDF render took {warmRender.TotalMilliseconds:F1} ms.");
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
        WriteQualificationArtifacts("project-financial-position-golden.pdf", first.Bytes);
        for (var index = 0; index < firstImages.Count; index++)
        {
            WriteQualificationArtifacts(
                $"project-financial-position-golden-page-{index + 1}.png",
                firstImages[index]);
        }

        var expectedVisualDigests = new[]
        {
            "44afd18ca0babb473b69911bf83d775dec57519c34c0237e471bebc9bdd439b7",
            "f8eb576d5e0cdfd267d80013d2fce3c8cb9f45ad18b54d0a37632a3b10358cbb"
        };
        Assert.True(
            string.Equals(
                "25293911fd4eec21e9b2e2f62de9239d6f5d5bed8b4842a32c8d1483fc987d09",
                first.Sha256,
                StringComparison.Ordinal) &&
                expectedVisualDigests.SequenceEqual(visualDigests),
            $"F05_PDF_GOLDEN_SHA256={first.Sha256}; " +
            $"F05_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
    }

    [Fact]
    public void NoDataWorkbookKeepsFinancialSheetsHeaderOnlyAndDoesNotFabricateZeroMetrics()
    {
        var result = Calculate(Projection());
        var snapshot = Build(result);
        var request = CreateRequest(snapshot, ReportFormat.Xlsx);

        var artifact = new ProjectFinancialPositionReportXlsxRenderer(
            ReportingExecutionOptions.Default).Render(request);

        Assert.Equal(ReportDataStatus.NoData, request.Snapshot.DataStatus);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var spreadsheet =
            (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var sheetNumber in new[] { 2, 3, 4, 5, 6 })
        {
            var document = XDocument.Parse(ReadEntry(
                archive,
                $"xl/worksheets/sheet{sheetNumber}.xml"));
            Assert.Single(document.Descendants(spreadsheet + "row"));
        }
        var allWorksheets = string.Concat(Enumerable.Range(1, 8)
            .Select(index => ReadEntry(archive, $"xl/worksheets/sheet{index}.xml")));
        Assert.Contains("داده رسمی وجود ندارد", allWorksheets, StringComparison.Ordinal);
        Assert.Contains("Budget Baseline رسمی وجود ندارد", allWorksheets, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererRejectsOversizedTextAndNonCanonicalRowsInsteadOfSilentlyRepairingThem()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ProjectFinancialPositionReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var oversized = parsed with
        {
            OpenObligations = parsed.OpenObligations.Select((item, index) => index == 0
                ? item with
                {
                    CounterpartySnapshot = new string(
                        'ش',
                        ProjectFinancialPositionReportRenderingContract.MaximumCounterpartyLength + 1)
                }
                : item).ToArray()
        };
        var nonCanonical = parsed with
        {
            OpenObligations = parsed.OpenObligations.Reverse().ToArray()
        };

        var textError = Assert.Throws<ReportRenderingException>(() =>
            ProjectFinancialPositionReportRenderSnapshot.Parse(CanonicalJson.Serialize(oversized)));
        var orderError = Assert.Throws<ReportRenderingException>(() =>
            ProjectFinancialPositionReportRenderSnapshot.Parse(CanonicalJson.Serialize(nonCanonical)));

        Assert.Equal("reporting.project_financial_position.snapshot.payload_invalid", textError.Code);
        Assert.Equal("reporting.project_financial_position.snapshot.payload_invalid", orderError.Code);
        Assert.False(textError.Transient);
        Assert.False(orderError.Transient);
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
        var pdf = new ProjectFinancialPositionReportPdfRenderer(RendererOptions(), bounded);
        var xlsx = new ProjectFinancialPositionReportXlsxRenderer(bounded);
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
        var payment = Record(11, FinancialRecordType.Payment, 260m);
        var payable = Obligation(
            20,
            FinancialObligationType.Payable,
            500m,
            CutoffLocalDate.AddDays(-10),
            "=SUM(A1:A2)",
            "پیمانکار نمونه");
        var result = Calculate(Projection(
            records:
            [
                Record(10, FinancialRecordType.Receipt, 1_000m),
                payment,
                Record(12, FinancialRecordType.PettyCashFunding, 100m),
                Record(13, FinancialRecordType.PettyCashExpense, 40m)
            ],
            obligations:
            [
                payable,
                Obligation(
                    21,
                    FinancialObligationType.Payable,
                    800m,
                    CutoffLocalDate.AddDays(5),
                    "AP-002",
                    "تأمین‌کننده ب"),
                Obligation(
                    22,
                    FinancialObligationType.Receivable,
                    700m,
                    CutoffLocalDate.AddDays(-45),
                    "AR-001",
                    "کارفرمای نمونه"),
                Obligation(
                    23,
                    FinancialObligationType.Receivable,
                    900m,
                    CutoffLocalDate.AddDays(-80),
                    "AR-002",
                    null)
            ],
            settlements:
            [
                Settlement(30, payable.ObligationId, payment.RecordId, 100m)
            ],
            budgets:
            [
                Budget(40, 250m, Cutoff.AddDays(-20))
            ],
            sourceClassification: ProjectFinancialPositionReportingClassification.Restricted));
        return Build(result);
    }

    private static ProjectFinancialPositionReportingResult Calculate(
        ProjectFinancialPositionReportingProjection projection) =>
        ProjectFinancialPositionReportingCalculator.Calculate(
            ProjectFinancialPositionReportingSelector.Select(projection));

    private static ProjectFinancialPositionReportingProjection Projection(
        IReadOnlyCollection<ProjectFinancialRecordVersion>? records = null,
        IReadOnlyCollection<ProjectFinancialObligationVersion>? obligations = null,
        IReadOnlyCollection<ProjectFinancialSettlementVersion>? settlements = null,
        IReadOnlyCollection<ProjectBudgetBaselineVersion>? budgets = null,
        ProjectFinancialPositionReportingClassification sourceClassification =
            ProjectFinancialPositionReportingClassification.Confidential) => new(
        ProjectFinancialPositionReportingContract.Version,
        TenantId,
        ProjectId,
        CutoffLocalDate,
        Cutoff,
        [new ProjectFinancialConfigurationVersion(
            7,
            12,
            ProjectFeatureState.Active,
            ProjectFeatureState.Active,
            "IRR",
            Cutoff.AddDays(-100),
            null,
            ProjectFinancialPositionReportingClassification.Confidential)],
        records ?? [],
        obligations ?? [],
        settlements ?? [],
        budgets ?? [],
        ProjectFinancialSourceCompleteness.Complete,
        ProjectFinancialSourceCompleteness.Complete,
        ProjectFinancialSourceCompleteness.Complete,
        sourceClassification);

    private static ProjectFinancialRecordVersion Record(
        int id,
        FinancialRecordType type,
        decimal amount) => new(
        Id(id),
        TenantId,
        ProjectId,
        3,
        type,
        FinancialRecordStatus.Posted,
        CutoffLocalDate.AddDays(-2),
        amount,
        "IRR",
        Cutoff.AddDays(-10),
        Cutoff.AddDays(-2),
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ProjectFinancialObligationVersion Obligation(
        int id,
        FinancialObligationType type,
        decimal amount,
        DateOnly dueDate,
        string number,
        string? counterparty) => new(
        Id(id),
        TenantId,
        ProjectId,
        4,
        type,
        FinancialObligationStatus.Approved,
        number,
        counterparty,
        CutoffLocalDate.AddDays(-365),
        dueDate,
        amount,
        "IRR",
        Cutoff.AddDays(-40),
        Cutoff.AddDays(-20),
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ProjectFinancialSettlementVersion Settlement(
        int id,
        Guid obligationId,
        Guid recordId,
        decimal amount) => new(
        Id(id),
        TenantId,
        ProjectId,
        obligationId,
        recordId,
        amount,
        Cutoff.AddDays(-1),
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ProjectBudgetBaselineVersion Budget(
        int id,
        decimal amount,
        DateTimeOffset approvedAt) => new(
        Id(id),
        TenantId,
        ProjectId,
        3,
        BudgetBaselineStatus.Approved,
        amount,
        "IRR",
        approvedAt.AddDays(-2),
        approvedAt,
        null,
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ReportSnapshot Build(ProjectFinancialPositionReportingResult source) =>
        ProjectFinancialPositionReportSnapshotBuilder.Build(
            RunId,
            TenantId,
            ProjectFinancialPositionPinnedProjectProfile.Capture(
                Profile(),
                Cutoff.AddMinutes(1)),
            Cutoff,
            source,
            Cutoff.AddMinutes(1),
            Cutoff.AddMinutes(2));

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-F05",
        "پروژه نمونه وضعیت مالی",
        "Asia/Tehran",
        "IRR",
        12,
        Cutoff.AddDays(-100),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.SimpleWorkList,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 62),
        ConfigurationVersion: 7);

    private static ProjectFinancialPositionReportRenderRequest CreateRequest(
        ReportSnapshot snapshot,
        ReportFormat format)
    {
        var parsed = ProjectFinancialPositionReportRenderSnapshot.Parse(snapshot.PayloadJson);
        return new ProjectFinancialPositionReportRenderRequest(
            RunId,
            format == ReportFormat.Pdf ? Id(400) : Id(401),
            Id(402),
            Id(403),
            ProjectFinancialPositionReportRuntimeContract.DefinitionCode,
            ProjectFinancialPositionReportRuntimeContract.DefinitionVersion,
            ProjectFinancialPositionReportRuntimeContract.TemplateVersion,
            ProjectFinancialPositionReportRuntimeContract.TemplateContentDigest,
            ProjectFinancialPositionReportRuntimeContract.RendererContractVersion,
            ProjectFinancialPositionReportRuntimeContract.LayoutContractVersion,
            format,
            ReportArtifactIdentity.FileName(parsed, format),
            "RPT-F050-0000-0000-0000-0001",
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
        var entry = archive.GetEntry(name) ??
            throw new InvalidOperationException($"Missing {name}.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteQualificationArtifacts(string fileName, byte[] bytes)
    {
        var output = Environment.GetEnvironmentVariable(
            "PMCS_F05_RENDER_QUALIFICATION_OUTPUT");
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
