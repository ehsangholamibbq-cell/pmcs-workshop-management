using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectCommercialProcurementSupplyReportRenderingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid PartyId = Id(3);
    private static readonly Guid ItemId = Id(4);
    private static readonly Guid RunId = Id(500);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 21);
    private static readonly DateTimeOffset Cutoff =
        new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void RendererContractPinsIdentityParsesCanonicalSnapshotAndRejectsTampering()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ProjectCommercialProcurementSupplyReportRenderSnapshot.Parse(
            snapshot.PayloadJson);
        var request = CreateRequest(snapshot, ReportFormat.Pdf);
        var pdf = new ProjectCommercialProcurementSupplyReportPdfRenderer(
            RendererOptions(),
            ReportingExecutionOptions.Default);
        var xlsx = new ProjectCommercialProcurementSupplyReportXlsxRenderer(
            ReportingExecutionOptions.Default);
        var registry = new ProjectCommercialProcurementSupplyReportRendererRegistry(
            new IProjectCommercialProcurementSupplyReportRenderer[] { pdf, xlsx });

        Assert.Equal(
            "pmcs.reporting.project-commercial-procurement-supply.renderer/v1",
            ProjectCommercialProcurementSupplyReportRuntimeContract.RendererContractVersion);
        Assert.Equal(
            "pmcs.reporting.project-commercial-procurement-supply.layout/v1",
            ProjectCommercialProcurementSupplyReportRuntimeContract.LayoutContractVersion);
        Assert.Equal("1.0.0", ProjectCommercialProcurementSupplyReportRuntimeContract.TemplateVersion);
        Assert.Equal(
            "e5966e5910ff9d875151b3e0c5891fb2c36027e8608771d34ee0a5d67120efb5",
            ProjectCommercialProcurementSupplyReportRuntimeContract.TemplateContentDigest);
        Assert.Equal(
            ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionCode,
            parsed.DefinitionCode);
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal(
            "project-commercial-procurement-supply-PRJ-F06-1405-06-30.pdf",
            request.FileName);
        Assert.Same(pdf, registry.Require(ReportFormat.Pdf));
        Assert.Same(xlsx, registry.Require(ReportFormat.Xlsx));

        var tamperedPayload = snapshot.PayloadJson.Replace(
            ProjectCommercialProcurementSupplyReportRuntimeContract.SemanticContractId,
            "PMCS-RPT1-F06-TAMPERED",
            StringComparison.Ordinal);
        var payloadError = Assert.Throws<ReportRenderingException>(() =>
            ProjectCommercialProcurementSupplyReportRenderSnapshot.Parse(tamperedPayload));
        var hashError = Assert.Throws<ReportRenderingException>(() =>
            ProjectCommercialProcurementSupplyReportRenderModel.Create(
                request with { SnapshotSha256 = new string('0', 64) }));
        var registryError = Assert.Throws<ReportRenderingException>(() =>
            registry.Require(ReportFormat.Csv));

        Assert.Equal(
            "reporting.project_commercial_procurement_supply.snapshot.payload_invalid",
            payloadError.Code);
        Assert.Equal(
            "reporting.project_commercial_procurement_supply.render_request.invalid",
            hashError.Code);
        Assert.Equal("reporting.format.unsupported", registryError.Code);
        Assert.All(
            new[] { payloadError, hashError, registryError },
            exception => Assert.False(exception.Transient));
    }

    [Fact]
    public void CertifiedCommercialProcurementSupplyXlsxIsDeterministicGoldenRtlFormulaFreeAndSemantic()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Xlsx);
        var renderer = new ProjectCommercialProcurementSupplyReportXlsxRenderer(
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
                "xl/worksheets/sheet8.xml",
                "xl/worksheets/sheet9.xml",
                "xl/worksheets/sheet10.xml"
            ],
            archive.Entries.Select(item => item.FullName).ToArray());

        var workbookXml = ReadEntry(archive, "xl/workbook.xml");
        foreach (var sheetName in new[]
        {
            "Metadata", "Contract Summary", "Contracts", "Amendments", "Procurement",
            "Purchase Orders", "Supply Summary", "Suppliers", "Source Counts", "Lineage"
        })
        {
            Assert.Contains(sheetName, workbookXml, StringComparison.Ordinal);
        }

        var spreadsheet =
            (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var worksheetXml = Enumerable.Range(1, 10)
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

        Assert.Contains("'=SUM(A1:A2)", worksheetXml[2], StringComparison.Ordinal);
        Assert.Contains("تکمیل به‌موقع", worksheetXml[5], StringComparison.Ordinal);
        Assert.Contains("فقط شمارنده و نرخ", worksheetXml[9], StringComparison.Ordinal);
        Assert.Contains(
            XDocument.Parse(worksheetXml[1]).Descendants(spreadsheet + "v"),
            value => decimal.TryParse(
                value.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed == 1_600m);
        var allWorksheets = string.Concat(worksheetXml);
        Assert.DoesNotContain("Invoice", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Payment", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Forecast", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ranking", allWorksheets, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Score<", allWorksheets, StringComparison.OrdinalIgnoreCase);

        WriteQualificationArtifacts(
            "project-commercial-procurement-supply-golden.xlsx",
            first.Bytes);
        Assert.True(
            string.Equals("F06_XLSX_PENDING", first.Sha256, StringComparison.Ordinal),
            $"F06_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void CertifiedCommercialProcurementSupplyPdfIsDeterministicVisuallyPinnedAndWithinPerformanceBudget()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Pdf);
        var renderer = new ProjectCommercialProcurementSupplyReportPdfRenderer(
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
        Assert.Contains(
            "%%EOF",
            Encoding.ASCII.GetString(first.Bytes[^32..]),
            StringComparison.Ordinal);
        Assert.InRange(
            first.Bytes.Length,
            1,
            CertifiedPdfRuntimeContract.QualificationMaximumPdfBytes);
        Assert.True(
            coldRender.TotalMilliseconds <=
                CertifiedPdfRuntimeContract.QualificationColdRenderBudgetMilliseconds,
            $"Cold F06 PDF render took {coldRender.TotalMilliseconds:F1} ms.");
        Assert.True(
            warmRender.TotalMilliseconds <=
                CertifiedPdfRuntimeContract.QualificationWarmRenderBudgetMilliseconds,
            $"Warm F06 PDF render took {warmRender.TotalMilliseconds:F1} ms.");
        Assert.Equal(3, firstImages.Count);
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
        WriteQualificationArtifacts(
            "project-commercial-procurement-supply-golden.pdf",
            first.Bytes);
        for (var index = 0; index < firstImages.Count; index++)
        {
            WriteQualificationArtifacts(
                $"project-commercial-procurement-supply-golden-page-{index + 1}.png",
                firstImages[index]);
        }

        var expectedVisualDigests = new[] { "F06_VISUAL_PENDING" };
        Assert.True(
            string.Equals("F06_PDF_PENDING", first.Sha256, StringComparison.Ordinal) &&
                expectedVisualDigests.SequenceEqual(visualDigests),
            $"F06_PDF_GOLDEN_SHA256={first.Sha256}; " +
            $"F06_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
    }

    [Fact]
    public void NoDataWorkbookKeepsCommercialSheetsHeaderOnlyAndDoesNotFabricateZeroMetrics()
    {
        var snapshot = Build(Calculate(Projection()));
        var request = CreateRequest(snapshot, ReportFormat.Xlsx);

        var artifact = new ProjectCommercialProcurementSupplyReportXlsxRenderer(
            ReportingExecutionOptions.Default).Render(request);

        Assert.Equal(ReportDataStatus.NoData, request.Snapshot.DataStatus);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var spreadsheet =
            (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var sheetNumber in Enumerable.Range(2, 7))
        {
            var document = XDocument.Parse(ReadEntry(
                archive,
                $"xl/worksheets/sheet{sheetNumber}.xml"));
            Assert.Single(document.Descendants(spreadsheet + "row"));
        }
        var allWorksheets = string.Concat(Enumerable.Range(1, 10)
            .Select(index => ReadEntry(archive, $"xl/worksheets/sheet{index}.xml")));
        Assert.Contains("بدون داده رسمی", allWorksheets, StringComparison.Ordinal);
        Assert.Contains("قرارداد رسمی وجود ندارد", allWorksheets, StringComparison.Ordinal);
        Assert.Contains("خرید رسمی وجود ندارد", allWorksheets, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererRejectsOversizedTextAndNonCanonicalRowsInsteadOfSilentlyRepairingThem()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ProjectCommercialProcurementSupplyReportRenderSnapshot.Parse(
            snapshot.PayloadJson);
        var oversized = parsed with
        {
            ContractRegister = parsed.ContractRegister.Select((item, index) => index == 0
                ? item with
                {
                    Title = new string(
                        'ش',
                        ProjectCommercialProcurementSupplyReportRenderingContract.MaximumTitleLength + 1)
                }
                : item).ToArray()
        };
        var nonCanonical = parsed with
        {
            ContractRegister = parsed.ContractRegister.Reverse().ToArray()
        };

        var textError = Assert.Throws<ReportRenderingException>(() =>
            ProjectCommercialProcurementSupplyReportRenderSnapshot.Parse(
                CanonicalJson.Serialize(oversized)));
        var orderError = Assert.Throws<ReportRenderingException>(() =>
            ProjectCommercialProcurementSupplyReportRenderSnapshot.Parse(
                CanonicalJson.Serialize(nonCanonical)));

        Assert.Equal(
            "reporting.project_commercial_procurement_supply.snapshot.payload_invalid",
            textError.Code);
        Assert.Equal(
            "reporting.project_commercial_procurement_supply.snapshot.payload_invalid",
            orderError.Code);
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
        var pdf = new ProjectCommercialProcurementSupplyReportPdfRenderer(
            RendererOptions(),
            bounded);
        var xlsx = new ProjectCommercialProcurementSupplyReportXlsxRenderer(bounded);
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
        var request = PurchaseRequest(20);
        var order = PurchaseOrder(30, request.PurchaseRequestId, Id(10)) with
        {
            Lifecycle =
            [
                OrderEvent(1, ProjectCommercialPurchaseOrderEventType.Issued, -8),
                OrderEvent(2, ProjectCommercialPurchaseOrderEventType.Closed, -1)
            ]
        };
        var result = Calculate(Projection(
            parties: [Party()],
            items: [Item()],
            contracts:
            [
                Contract(10, 1_000m, "=SUM(A1:A2)"),
                Contract(11, 500m, "قرارداد تأمین دوم")
            ],
            amendments: [Amendment(12)],
            requests: [request],
            orders: [order],
            receipts: [Receipt(40)]));
        return Build(result);
    }

    private static ProjectCommercialProcurementSupplyReportingResult Calculate(
        ProjectCommercialProcurementSupplyReportingProjection projection) =>
        ProjectCommercialProcurementSupplyReportingCalculator.Calculate(
            ProjectCommercialProcurementSupplyReportingSelector.Select(projection));

    private static ProjectCommercialProcurementSupplyReportingProjection Projection(
        IReadOnlyCollection<ProjectCommercialPartySnapshotVersion>? parties = null,
        IReadOnlyCollection<ProjectCommercialItemSnapshotVersion>? items = null,
        IReadOnlyCollection<ProjectCommercialContractVersion>? contracts = null,
        IReadOnlyCollection<ProjectCommercialAmendmentVersion>? amendments = null,
        IReadOnlyCollection<ProjectCommercialPurchaseRequestVersion>? requests = null,
        IReadOnlyCollection<ProjectCommercialPurchaseOrderVersion>? orders = null,
        IReadOnlyCollection<ProjectCommercialGoodsReceiptVersion>? receipts = null) => new(
        ProjectCommercialProcurementSupplyReportingContract.Version,
        TenantId,
        ProjectId,
        CutoffLocalDate,
        Cutoff,
        [Configuration()],
        parties ?? [],
        items ?? [],
        contracts ?? [],
        amendments ?? [],
        requests ?? [],
        orders ?? [],
        receipts ?? [],
        [],
        ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialConfigurationVersion Configuration() => new(
        7,
        12,
        ContractModel.ConstructionManagement,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        "Etc/UTC",
        "IRR",
        Cutoff.AddDays(-100),
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialPartySnapshotVersion Party() => new(
        PartyId,
        TenantId,
        ProjectId,
        1,
        "SUP-003",
        "تأمین‌کننده نمونه",
        PartyType.Supplier,
        Cutoff.AddDays(-100),
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialItemSnapshotVersion Item() => new(
        ItemId,
        TenantId,
        ProjectId,
        1,
        "ITM-004",
        "پمپ نمونه",
        SupplyItemKind.Material,
        "EA",
        Cutoff.AddDays(-100),
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialContractVersion Contract(
        int id,
        decimal amount,
        string title) => new(
        Id(id),
        TenantId,
        ProjectId,
        3,
        PartyId,
        $"CTR-{id:000}",
        title,
        ProjectContractType.Supply,
        amount,
        "IRR",
        CutoffLocalDate.AddDays(-100),
        CutoffLocalDate.AddDays(100),
        Cutoff.AddDays(-30),
        [
            ContractEvent(1, ProjectCommercialContractEventType.Submitted, -20),
            ContractEvent(2, ProjectCommercialContractEventType.Activated, -19)
        ],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialAmendmentVersion Amendment(int id) => new(
        Id(id),
        TenantId,
        ProjectId,
        2,
        Id(10),
        $"AMD-{id:000}",
        "اصلاحیه مبلغ مصوب",
        ContractAmendmentType.ValueChange,
        100m,
        "IRR",
        null,
        Cutoff.AddDays(-10),
        [
            AmendmentEvent(1, ProjectCommercialAmendmentEventType.Submitted, -2),
            AmendmentEvent(2, ProjectCommercialAmendmentEventType.Approved, -1)
        ],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialPurchaseRequestVersion PurchaseRequest(int id) => new(
        Id(id),
        TenantId,
        ProjectId,
        4,
        $"REQ-{id:000}",
        "درخواست خرید پمپ",
        100m,
        "IRR",
        ItemId,
        Cutoff.AddDays(-12),
        [
            RequestEvent(1, ProjectCommercialPurchaseRequestEventType.Submitted, -10),
            RequestEvent(2, ProjectCommercialPurchaseRequestEventType.Approved, -9),
            RequestEvent(3, ProjectCommercialPurchaseRequestEventType.Ordered, -8)
        ],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialPurchaseOrderVersion PurchaseOrder(
        int id,
        Guid requestId,
        Guid contractId) => new(
        Id(id),
        TenantId,
        ProjectId,
        2,
        requestId,
        PartyId,
        contractId,
        $"PO-{id:000}",
        "سفارش خرید پمپ",
        100m,
        "IRR",
        CutoffLocalDate.AddDays(-1),
        ItemId,
        10m,
        "EA",
        10m,
        "EA",
        1,
        [OrderEvent(1, ProjectCommercialPurchaseOrderEventType.Issued, -8)],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialGoodsReceiptVersion Receipt(int id) => new(
        Id(id),
        TenantId,
        ProjectId,
        2,
        $"REC-{id:000}",
        Id(30),
        PartyId,
        ItemId,
        Cutoff.AddDays(-2).AddHours(-1),
        Cutoff.AddDays(-2),
        10m,
        "EA",
        1,
        GoodsReceiptStatus.Accepted,
        Cutoff.AddDays(-2).AddHours(1),
        10m,
        0m,
        0m,
        null,
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialContractLifecycleEvent ContractEvent(
        long sequence,
        ProjectCommercialContractEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ProjectCommercialAmendmentLifecycleEvent AmendmentEvent(
        long sequence,
        ProjectCommercialAmendmentEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ProjectCommercialPurchaseRequestLifecycleEvent RequestEvent(
        long sequence,
        ProjectCommercialPurchaseRequestEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ProjectCommercialPurchaseOrderLifecycleEvent OrderEvent(
        long sequence,
        ProjectCommercialPurchaseOrderEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ReportSnapshot Build(
        ProjectCommercialProcurementSupplyReportingResult source) =>
        ProjectCommercialProcurementSupplyReportSnapshotBuilder.Build(
            RunId,
            TenantId,
            ProjectCommercialProcurementSupplyPinnedProjectProfile.Capture(
                Profile(),
                Cutoff.AddMinutes(1)),
            Cutoff,
            source,
            Cutoff.AddMinutes(1),
            Cutoff.AddMinutes(2));

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-F06",
        "پروژه نمونه قرارداد و تأمین",
        "Etc/UTC",
        "IRR",
        12,
        Cutoff.AddDays(-100),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.SimpleWorkList,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 62),
        ConfigurationVersion: 7);

    private static ProjectCommercialProcurementSupplyReportRenderRequest CreateRequest(
        ReportSnapshot snapshot,
        ReportFormat format)
    {
        var parsed = ProjectCommercialProcurementSupplyReportRenderSnapshot.Parse(
            snapshot.PayloadJson);
        return new ProjectCommercialProcurementSupplyReportRenderRequest(
            RunId,
            format == ReportFormat.Pdf ? Id(600) : Id(601),
            Id(602),
            Id(603),
            ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionCode,
            ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionVersion,
            ProjectCommercialProcurementSupplyReportRuntimeContract.TemplateVersion,
            ProjectCommercialProcurementSupplyReportRuntimeContract.TemplateContentDigest,
            ProjectCommercialProcurementSupplyReportRuntimeContract.RendererContractVersion,
            ProjectCommercialProcurementSupplyReportRuntimeContract.LayoutContractVersion,
            format,
            ReportArtifactIdentity.FileName(parsed, format),
            "RPT-F060-0000-0000-0000-0001",
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
            "PMCS_F06_RENDER_QUALIFICATION_OUTPUT");
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
