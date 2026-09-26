using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectPeriodicReportRenderingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid RunId = Id(3);
    private static readonly DateOnly WeeklyStart = new(2026, 9, 19);
    private static readonly DateOnly WeeklyEnd = new(2026, 9, 26);
    private static readonly DateTimeOffset ClosedCutoff =
        new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RendererContractPinsIdentityParsesCanonicalSnapshotAndRejectsTampering()
    {
        var snapshot = BuildAvailableSnapshot();
        var parsed = ProjectPeriodicReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var request = CreateRequest(snapshot, ReportFormat.Pdf);

        Assert.Equal(
            "pmcs.reporting.project-periodic.renderer/v1",
            ProjectPeriodicReportRuntimeContract.RendererContractVersion);
        Assert.Equal(
            "pmcs.reporting.project-periodic.layout/v1",
            ProjectPeriodicReportRuntimeContract.LayoutContractVersion);
        Assert.Equal("1.0.0", ProjectPeriodicReportRuntimeContract.TemplateVersion);
        Assert.Equal(ProjectPeriodicReportRuntimeContract.DefinitionCode, parsed.DefinitionCode);
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal(
            "project-weekly-PRJ-F02-1405-06-28-1405-07-03.pdf",
            request.FileName);

        var tamperedPayload = snapshot.PayloadJson.Replace(
            ProjectPeriodicReportRuntimeContract.DefinitionCode,
            "project-periodic-tampered",
            StringComparison.Ordinal);
        var payloadError = Assert.Throws<ReportRenderingException>(() =>
            ProjectPeriodicReportRenderSnapshot.Parse(tamperedPayload));
        var hashError = Assert.Throws<ReportRenderingException>(() =>
            ProjectPeriodicReportRenderModel.Create(request with { SnapshotSha256 = new string('0', 64) }));

        Assert.Equal("reporting.periodic.snapshot.payload_invalid", payloadError.Code);
        Assert.Equal("reporting.periodic.render_request.invalid", hashError.Code);
        Assert.False(payloadError.Transient);
        Assert.False(hashError.Transient);
    }

    [Fact]
    public void CertifiedPeriodicXlsxIsDeterministicGoldenRtlFormulaFreeAndUnitSafe()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Xlsx);
        var renderer = new ProjectPeriodicReportXlsxRenderer(ReportingExecutionOptions.Default);

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

        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var workbookXml = ReadEntry(archive, "xl/workbook.xml");
        Assert.Contains("Metadata", workbookXml, StringComparison.Ordinal);
        Assert.Contains("Coverage", workbookXml, StringComparison.Ordinal);
        Assert.Contains("Reports", workbookXml, StringComparison.Ordinal);
        Assert.Contains("Facts", workbookXml, StringComparison.Ordinal);
        Assert.Contains("Fact Counts", workbookXml, StringComparison.Ordinal);
        Assert.Contains("Quantities", workbookXml, StringComparison.Ordinal);
        Assert.Contains("Resources", workbookXml, StringComparison.Ordinal);
        Assert.Contains("High Impact", workbookXml, StringComparison.Ordinal);

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

        Assert.Contains("'=SUM(A1:A2)", worksheetXml[3], StringComparison.Ordinal);
        Assert.Contains(">m3<", worksheetXml[5], StringComparison.Ordinal);
        Assert.Contains(">M3<", worksheetXml[5], StringComparison.Ordinal);
        Assert.Contains("واحد ثبت نشده", worksheetXml[5], StringComparison.Ordinal);
        Assert.DoesNotContain("grandTotal", string.Concat(worksheetXml), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("جمع کل", string.Concat(worksheetXml), StringComparison.Ordinal);

        var qualificationOutput = Environment.GetEnvironmentVariable("PMCS_F02_RENDER_QUALIFICATION_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qualificationOutput))
        {
            Directory.CreateDirectory(qualificationOutput);
            File.WriteAllBytes(Path.Combine(qualificationOutput, "project-periodic-weekly-golden.xlsx"), first.Bytes);
        }
        Assert.Equal(
            "83fd80eedaa1024e84eb253bec76591379fe2f088be12c5b322573d63eb1909d",
            first.Sha256);
    }

    [Fact]
    public void CertifiedPeriodicPdfIsDeterministicVisuallyPinnedAndWithinPerformanceBudget()
    {
        var request = CreateRequest(BuildAvailableSnapshot(), ReportFormat.Pdf);
        var renderer = new ProjectPeriodicReportPdfRenderer(
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
            $"Cold project-periodic PDF render took {coldRender.TotalMilliseconds:F1} ms.");
        Assert.True(
            warmRender.TotalMilliseconds <= CertifiedPdfRuntimeContract.QualificationWarmRenderBudgetMilliseconds,
            $"Warm project-periodic PDF render took {warmRender.TotalMilliseconds:F1} ms.");
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
        var qualificationOutput = Environment.GetEnvironmentVariable("PMCS_F02_RENDER_QUALIFICATION_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qualificationOutput))
        {
            Directory.CreateDirectory(qualificationOutput);
            File.WriteAllBytes(Path.Combine(qualificationOutput, "project-periodic-weekly-golden.pdf"), first.Bytes);
            for (var index = 0; index < firstImages.Count; index++)
            {
                File.WriteAllBytes(
                    Path.Combine(qualificationOutput, $"project-periodic-weekly-golden-page-{index + 1}.png"),
                    firstImages[index]);
            }
        }
        Assert.Equal(
            "52ec4e80c34e682f6994ef7a674b161b748a772e34b4e04ec12e27e94c98f989",
            first.Sha256);
        Assert.Equal(
            [
                "058a3da3045408a1d87dc9e5c942cd38ffdf7da1921b6594e6ee88a0aa22b396",
                "d61a1090d07d5f21a5d57c15b3abb197a341332b124ba3e8a98461996a42b770"
            ],
            visualDigests);
    }

    [Fact]
    public void NoDataWorkbookKeepsSemanticSheetsHeaderOnlyAndDoesNotFabricateZeroMetrics()
    {
        var snapshot = BuildSnapshot([]);
        var request = CreateRequest(snapshot, ReportFormat.Xlsx);
        var renderer = new ProjectPeriodicReportXlsxRenderer(ReportingExecutionOptions.Default);

        var artifact = renderer.Render(request);

        Assert.Equal(ReportDataStatus.NoData, request.Snapshot.DataStatus);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var sheetNumber in Enumerable.Range(3, 6))
        {
            var xml = ReadEntry(archive, $"xl/worksheets/sheet{sheetNumber}.xml");
            var document = XDocument.Parse(xml);
            Assert.Single(document.Descendants(spreadsheet + "row"));
        }
        var allWorksheets = string.Concat(Enumerable.Range(1, 8)
            .Select(index => ReadEntry(archive, $"xl/worksheets/sheet{index}.xml")));
        Assert.DoesNotContain("<v>0</v>", allWorksheets, StringComparison.Ordinal);
        Assert.Contains("داده رسمی وجود ندارد", allWorksheets, StringComparison.Ordinal);
    }

    [Fact]
    public void MonthlyWorkbookUsesCanonicalPersianMonthBoundariesAndIdentity()
    {
        var periodStart = new DateOnly(2026, 8, 23);
        var periodEnd = new DateOnly(2026, 9, 23);
        var source = new DailyReportReportingPeriod(
            DailyReportPeriodReportingContract.Version,
            TenantId,
            ProjectId,
            periodStart,
            periodEnd,
            ClosedCutoff,
            []);
        var snapshot = ProjectPeriodicReportSnapshotBuilder.Build(
            RunId,
            TenantId,
            Project(),
            ClosedCutoff,
            new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Monthly, periodStart),
            source,
            ClosedCutoff.AddMinutes(1),
            ClosedCutoff.AddMinutes(2));
        var request = CreateRequest(snapshot, ReportFormat.Xlsx);

        var artifact = new ProjectPeriodicReportXlsxRenderer(ReportingExecutionOptions.Default)
            .Render(request);

        Assert.Equal(
            "project-monthly-PRJ-F02-1405-06-01-1405-06-31.xlsx",
            artifact.FileName);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var metadata = ReadEntry(archive, "xl/worksheets/sheet1.xml");
        Assert.Contains("گزارش ماهانه رسمی پروژه", metadata, StringComparison.Ordinal);
        Assert.Contains("۱۴۰۵/۰۶/۰۱", metadata, StringComparison.Ordinal);
        Assert.Contains("۱۴۰۵/۰۶/۳۱", metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void NotConfiguredWorkbookCarriesExplicitReasonsWithoutFabricatedMetrics()
    {
        var project = Project() with
        {
            ReportingFrequency = ReportingFrequency.NotConfigured,
            DailyReportWorkflow = DailyReportWorkflow.NotConfigured,
            DailyCutoffLocalTime = null,
            Calendar = new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null)
        };
        var snapshot = BuildSnapshot([], project);
        var request = CreateRequest(snapshot, ReportFormat.Xlsx);

        var artifact = new ProjectPeriodicReportXlsxRenderer(ReportingExecutionOptions.Default)
            .Render(request);

        Assert.Equal(ReportDataStatus.NotConfigured, request.Snapshot.DataStatus);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var metadata = ReadEntry(archive, "xl/worksheets/sheet1.xml");
        Assert.Contains("منبع داده پیکربندی نشده است", metadata, StringComparison.Ordinal);
        Assert.Contains("تناوب گزارش‌دهی پیکربندی نشده است", metadata, StringComparison.Ordinal);
        Assert.Contains("گردش‌کار گزارش روزانه پیکربندی نشده است", metadata, StringComparison.Ordinal);
        Assert.Contains("زمان برش روزانه پیکربندی نشده است", metadata, StringComparison.Ordinal);
        Assert.DoesNotContain("<v>0</v>", metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void PeriodicRenderersRejectWrongFormatsAndBoundFactRowsFailClosed()
    {
        var snapshot = BuildAvailableSnapshot();
        var pdfRequest = CreateRequest(snapshot, ReportFormat.Pdf);
        var xlsxRequest = CreateRequest(snapshot, ReportFormat.Xlsx);
        var bounded = ReportingExecutionOptions.Default with
        {
            MaximumPdfFacts = 1,
            MaximumXlsxRows = 1
        };
        var pdf = new ProjectPeriodicReportPdfRenderer(RendererOptions(), bounded);
        var xlsx = new ProjectPeriodicReportXlsxRenderer(bounded);

        var pdfLimit = Assert.Throws<ReportRenderingException>(() => pdf.Render(pdfRequest));
        var xlsxLimit = Assert.Throws<ReportRenderingException>(() => xlsx.Render(xlsxRequest));
        var wrongPdfFormat = Assert.Throws<ReportRenderingException>(() => pdf.Render(xlsxRequest));
        var wrongXlsxFormat = Assert.Throws<ReportRenderingException>(() => xlsx.Render(pdfRequest));

        Assert.Equal("reporting.output.page_limit_exceeded", pdfLimit.Code);
        Assert.Equal("reporting.output.row_limit_exceeded", xlsxLimit.Code);
        Assert.Equal("reporting.format.unsupported", wrongPdfFormat.Code);
        Assert.Equal("reporting.format.unsupported", wrongXlsxFormat.Code);
        Assert.All(
            new[] { pdfLimit, xlsxLimit, wrongPdfFormat, wrongXlsxFormat },
            exception => Assert.False(exception.Transient));
    }

    private static ReportSnapshot BuildAvailableSnapshot() => BuildSnapshot(
    [
        Root(
            100,
            WeeklyStart,
            DailyReportReportingClassification.Internal,
            Fact(1001, DailyReportReportingFactKind.WorkProgress, "بتن‌ریزی فونداسیون", quantity: 10m, unit: "m3"),
            Fact(1002, DailyReportReportingFactKind.Note, "=SUM(A1:A2) باید صرفاً متن باقی بماند")),
        Root(
            200,
            WeeklyStart.AddDays(1),
            DailyReportReportingClassification.Internal,
            Fact(2001, DailyReportReportingFactKind.WorkProgress, "اجرای دیوار برشی", quantity: 5m, unit: "M3"),
            Fact(2002, DailyReportReportingFactKind.Labor, "اکیپ قالب‌بندی", resourceCount: 4, hours: 32m)),
        Root(
            300,
            WeeklyStart.AddDays(2),
            DailyReportReportingClassification.Confidential,
            Fact(3001, DailyReportReportingFactKind.WorkProgress, "عملیات بدون واحد منبع", quantity: 2m)),
        Root(
            400,
            WeeklyStart.AddDays(3),
            DailyReportReportingClassification.Internal,
            Fact(4001, DailyReportReportingFactKind.Labor, "اکیپ آرماتوربندی", resourceCount: 4, hours: 28m)),
        Root(
            500,
            WeeklyStart.AddDays(4),
            DailyReportReportingClassification.Internal,
            Fact(5001, DailyReportReportingFactKind.Equipment, "جرثقیل کارگاهی", resourceCount: 2, hours: 5m),
            Fact(5002, DailyReportReportingFactKind.Material, "میلگرد تحویلی", quantity: 100m, unit: "kg")),
        Root(
            600,
            WeeklyStart.AddDays(5),
            DailyReportReportingClassification.Restricted,
            Fact(
                6001,
                DailyReportReportingFactKind.Issue,
                "تأخیر در تأیید نقشه اجرایی",
                impactLevel: DailyReportReportingImpactLevel.High)),
        CorrectedRoot()
    ]);

    private static ReportSnapshot BuildSnapshot(
        IReadOnlyCollection<DailyReportReportingRoot> roots,
        ProjectControlProfile? project = null)
    {
        var source = new DailyReportReportingPeriod(
            DailyReportPeriodReportingContract.Version,
            TenantId,
            ProjectId,
            WeeklyStart,
            WeeklyEnd,
            ClosedCutoff,
            roots);
        return ProjectPeriodicReportSnapshotBuilder.Build(
            RunId,
            TenantId,
            project ?? Project(),
            ClosedCutoff,
            new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Weekly, WeeklyStart),
            source,
            ClosedCutoff.AddMinutes(1),
            ClosedCutoff.AddMinutes(2));
    }

    private static ProjectPeriodicReportRenderRequest CreateRequest(
        ReportSnapshot snapshot,
        ReportFormat format)
    {
        var parsed = ProjectPeriodicReportRenderSnapshot.Parse(snapshot.PayloadJson);
        return new ProjectPeriodicReportRenderRequest(
            RunId,
            format == ReportFormat.Pdf ? Id(10) : Id(11),
            Id(12),
            Id(13),
            ProjectPeriodicReportRuntimeContract.DefinitionCode,
            ProjectPeriodicReportRuntimeContract.DefinitionVersion,
            ProjectPeriodicReportRuntimeContract.TemplateVersion,
            new string('b', 64),
            ProjectPeriodicReportRuntimeContract.RendererContractVersion,
            ProjectPeriodicReportRuntimeContract.LayoutContractVersion,
            format,
            ReportArtifactIdentity.FileName(parsed, format),
            "RPT-F020-0000-0000-0000-0001",
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

    private static ProjectControlProfile Project() => new(
        ProjectId,
        TenantId,
        "PRJ-F02",
        "پروژه گزارش دوره‌ای",
        "Asia/Tehran",
        "IRR",
        11,
        ClosedCutoff.AddDays(-10),
        ProjectStatus.Active,
        ContractModel.GeneralContracting,
        PlanningMode.SimpleWorkList,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 127),
        4,
        new TimeOnly(18, 0),
        ReportingFrequency.Daily,
        DailyReportWorkflow.OneStepApproval);

    private static DailyReportReportingRoot Root(
        int sequence,
        DateOnly reportDate,
        DailyReportReportingClassification classification,
        params DailyReportReportingFact[] facts)
    {
        var reportId = Id(sequence + 1);
        var version = Version(
            reportId,
            Id(sequence),
            reportDate,
            1,
            facts,
            ClosedCutoff.AddDays(-1));
        return new DailyReportReportingRoot(
            Id(sequence),
            reportDate,
            reportId,
            classification,
            [version]);
    }

    private static DailyReportReportingRoot CorrectedRoot()
    {
        var rootId = Id(700);
        var originalReportId = Id(701);
        var correctedReportId = Id(703);
        var correctedAt = ClosedCutoff.AddHours(-4);
        var originalFact = Fact(
            7001,
            DailyReportReportingFactKind.Stoppage,
            "توقف ناشی از محدودیت دسترسی",
            impactLevel: DailyReportReportingImpactLevel.High);
        var original = Version(
            originalReportId,
            rootId,
            WeeklyStart.AddDays(6),
            1,
            [originalFact],
            ClosedCutoff.AddDays(-1)) with
        {
            State = DailyReportReportingVersionState.Superseded,
            SupersededByReportId = correctedReportId,
            SupersededAt = correctedAt,
            LastModifiedAt = correctedAt,
            Revision = 6
        };
        var corrected = Version(
            correctedReportId,
            rootId,
            WeeklyStart.AddDays(6),
            2,
            [
                Fact(
                    7003,
                    DailyReportReportingFactKind.Stoppage,
                    "توقف بحرانی اصلاح‌شده با lineage رسمی",
                    impactLevel: DailyReportReportingImpactLevel.Critical,
                    copiedFromFactId: originalFact.FactId),
                Fact(7004, DailyReportReportingFactKind.SiteCondition, "شرایط کارگاه پس از رفع محدودیت")
            ],
            correctedAt,
            originalReportId);
        return new DailyReportReportingRoot(
            rootId,
            WeeklyStart.AddDays(6),
            correctedReportId,
            DailyReportReportingClassification.Restricted,
            [original, corrected]);
    }

    private static DailyReportReportingVersion Version(
        Guid reportId,
        Guid rootId,
        DateOnly reportDate,
        int versionNumber,
        IReadOnlyCollection<DailyReportReportingFact> facts,
        DateTimeOffset approvedAt,
        Guid? supersedesReportId = null) => new(
            reportId,
            rootId,
            versionNumber,
            supersedesReportId,
            null,
            null,
            reportDate,
            "زون آزمون",
            versionNumber == 1 ? "روایت رسمی روزانه" : "روایت اصلاحی رسمی",
            DailyReportReportingVersionState.Approved,
            Id(9001),
            approvedAt.AddHours(-2),
            Id(9002),
            approvedAt,
            approvedAt,
            supersedesReportId.HasValue ? "اصلاح شواهد رسمی" : null,
            supersedesReportId.HasValue ? Id(9003) : null,
            versionNumber + 3,
            facts);

    private static DailyReportReportingFact Fact(
        int sequence,
        DailyReportReportingFactKind kind,
        string description,
        decimal? quantity = null,
        string? unit = null,
        int? resourceCount = null,
        decimal? hours = null,
        DailyReportReportingImpactLevel? impactLevel = null,
        Guid? copiedFromFactId = null) => new(
            Id(sequence),
            copiedFromFactId,
            kind,
            description,
            kind.ToString(),
            Id(8001),
            "زون آزمون",
            quantity,
            unit,
            resourceCount,
            hours,
            impactLevel,
            $"F02-{sequence}",
            kind == DailyReportReportingFactKind.WorkProgress ? Id(8002) : null,
            Id(9001),
            new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero));

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name)
            ?? throw new InvalidOperationException($"Workbook entry '{name}' is missing.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Guid Id(int sequence) =>
        Guid.Parse($"00000000-0000-4000-8000-{sequence:000000000000}");
}
