using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;
using Pmcs.Modules.TechnicalOffice.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;
using Pmcs.Modules.TechnicalOffice.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectTechnicalOfficeReportRenderingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid RunId = Id(500);
    private static readonly DateTimeOffset Cutoff =
        new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void F07RendererPinsIdentityAndRejectsTamperingAndUnknownFormat()
    {
        var snapshot = Build(ProjectionWithPartialHistory());
        var parsed = ProjectTechnicalOfficeReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var request = Request(snapshot, ReportFormat.Pdf);
        var pdf = new ProjectTechnicalOfficeReportPdfRenderer(
            RendererOptions(), ReportingExecutionOptions.Default);
        var xlsx = new ProjectTechnicalOfficeReportXlsxRenderer(ReportingExecutionOptions.Default);
        var registry = new ProjectTechnicalOfficeReportRendererRegistry(
            new IProjectTechnicalOfficeReportRenderer[] { pdf, xlsx });

        Assert.Equal("1.0.0", ProjectTechnicalOfficeReportRuntimeContract.TemplateVersion);
        Assert.Equal("pmcs.reporting.project-technical-office.renderer/v1",
            ProjectTechnicalOfficeReportRuntimeContract.RendererContractVersion);
        Assert.Equal("pmcs.reporting.project-technical-office.layout/v1",
            ProjectTechnicalOfficeReportRuntimeContract.LayoutContractVersion);
        Assert.Equal("project-technical-office-PRJ-F07-1405-06-30.pdf", request.FileName);
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Same(pdf, registry.Require(ReportFormat.Pdf));
        Assert.Same(xlsx, registry.Require(ReportFormat.Xlsx));
        Assert.Equal("reporting.format.unsupported",
            Assert.Throws<ReportRenderingException>(() => registry.Require(ReportFormat.Csv)).Code);
        Assert.Equal("reporting.project_technical_office.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() =>
                ProjectTechnicalOfficeReportRenderSnapshot.Parse(snapshot.PayloadJson.Replace(
                    ProjectTechnicalOfficeReportRuntimeContract.SemanticContractId,
                    "PMCS-RPT1-F07-TAMPERED", StringComparison.Ordinal))).Code);
        Assert.Equal("reporting.project_technical_office.render_request.invalid",
            Assert.Throws<ReportRenderingException>(() =>
                ProjectTechnicalOfficeReportRenderModel.Create(
                    request with { SnapshotSha256 = new string('0', 64) })).Code);
        Assert.Equal("reporting.project_technical_office.render_request.invalid",
            Assert.Throws<ReportRenderingException>(() =>
                ProjectTechnicalOfficeReportRenderModel.Create(
                    request with { TemplateContentDigest = new string('0', 64) })).Code);
    }

    [Fact]
    public void F07XlsxGoldenPreservesUnknownCountsAndExplicitReasons()
    {
        var request = Request(Build(ProjectionWithPartialHistory()), ReportFormat.Xlsx);
        var renderer = new ProjectTechnicalOfficeReportXlsxRenderer(ReportingExecutionOptions.Default);
        var first = renderer.Render(request);
        var second = renderer.Render(request);
        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.True(first.Bytes.AsSpan().StartsWith("PK"u8));
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            first.ContentType);
        using var stream = new MemoryStream(first.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(13, archive.Entries.Count);
        var workbook = ReadEntry(archive, "xl/workbook.xml");
        foreach (var name in new[] { "Metadata", "Coverage", "Documents", "Transmittals", "RFI", "Submittals" })
            Assert.Contains(name, workbook, StringComparison.Ordinal);
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheets = Enumerable.Range(1, 6).Select(index =>
            ReadEntry(archive, $"xl/worksheets/sheet{index}.xml")).ToArray();
        foreach (var xml in sheets)
        {
            var document = XDocument.Parse(xml);
            Assert.Equal(spreadsheet + "worksheet", document.Root!.Name);
            Assert.Equal("1", document.Descendants(spreadsheet + "sheetView").Single()
                .Attribute("rightToLeft")?.Value);
            Assert.Equal("frozen", document.Descendants(spreadsheet + "pane").Single()
                .Attribute("state")?.Value);
            Assert.Empty(document.Descendants(spreadsheet + "f"));
        }
        Assert.Contains("نامعلوم؛ صفر فرض نشود", sheets[1], StringComparison.Ordinal);
        Assert.Contains("زمان تغییر وضعیت تاریخی موجود نیست", sheets[1], StringComparison.Ordinal);
        Assert.Contains("پوشش منبع تاریخی ناقص است", sheets[1], StringComparison.Ordinal);
        Assert.Contains("DOC-10", sheets[2], StringComparison.Ordinal);
        Assert.Contains("TRN-20", sheets[3], StringComparison.Ordinal);
        Assert.Single(XDocument.Parse(sheets[4]).Descendants(spreadsheet + "row"));
        Assert.Single(XDocument.Parse(sheets[5]).Descendants(spreadsheet + "row"));
        Assert.DoesNotContain("<f>", string.Concat(sheets), StringComparison.Ordinal);
        WriteQualificationArtifact("project-technical-office-golden.xlsx", first.Bytes);
        Assert.True(first.Sha256 == "a93140304603a0f9bffc0cc36058df27bb67df86413400fd8bbe675dc0856643",
            $"F07_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void F07PdfGoldenAndPagesAreDeterministicAndBounded()
    {
        var request = Request(Build(ProjectionWithPartialHistory()), ReportFormat.Pdf);
        var renderer = new ProjectTechnicalOfficeReportPdfRenderer(
            RendererOptions(), ReportingExecutionOptions.Default);
        var timer = Stopwatch.StartNew();
        var first = renderer.Render(request);
        timer.Stop();
        var cold = timer.Elapsed;
        timer.Restart();
        var second = renderer.Render(request);
        timer.Stop();
        var warm = timer.Elapsed;
        var images = renderer.RenderQualificationImages(request);
        var again = renderer.RenderQualificationImages(request);
        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.Equal("application/pdf", first.ContentType);
        Assert.True(first.Bytes.AsSpan().StartsWith("%PDF-"u8));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(first.Bytes[^32..]), StringComparison.Ordinal);
        Assert.InRange(first.Bytes.Length, 1,
            CertifiedPdfRuntimeContract.QualificationMaximumPdfBytes);
        Assert.True(cold.TotalMilliseconds <=
            CertifiedPdfRuntimeContract.QualificationColdRenderBudgetMilliseconds);
        Assert.True(warm.TotalMilliseconds <=
            CertifiedPdfRuntimeContract.QualificationWarmRenderBudgetMilliseconds);
        Assert.Equal(4, images.Count);
        Assert.Equal(images.Count, again.Count);
        foreach (var (page, index) in images.Select((page, index) => (page, index)))
        {
            Assert.True(page.AsSpan().StartsWith(
                new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }));
            Assert.True(page.SequenceEqual(again[index]));
            WriteQualificationArtifact($"project-technical-office-page-{index + 1}.png", page);
        }
        WriteQualificationArtifact("project-technical-office-golden.pdf", first.Bytes);
        var visualDigests = images.Select(page =>
            Convert.ToHexString(SHA256.HashData(page)).ToLowerInvariant()).ToArray();
        Assert.True(first.Sha256 == "50a20053f0e954ac57fb0450cf2157049ee6c1a2f29473b8bf454ee4612dc0fd" &&
            visualDigests.SequenceEqual(new[]
            {
                "c9f30dc4de3d83f3d8bf90d1dcdec7e726a018599e58e37b003381c1986773d0",
                "75a544ebeb4b21878807813cc32f5325799254cf22b96243cdabc3ffd387c4cd",
                "f96cd6c73245740afa2d22195eb2edba557182e3e87357f0a6f7c92aae068559",
                "4bd6dc818490b41333abf4eb5d6432eed669c7dde915b435873fc8ba752aa112"
            }),
            $"F07_PDF_GOLDEN_SHA256={first.Sha256}; F07_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
    }

    [Fact]
    public void F07CompleteEmptySourceHasProvenZeroWhileUnconfiguredAndPartialStayUnknown()
    {
        var complete = Request(Build(Projection()), ReportFormat.Xlsx);
        Assert.Equal(ReportDataStatus.NoData, complete.Snapshot.DataStatus);
        Assert.Equal(0, complete.Snapshot.Documents.OfficialCount);
        Assert.Equal(0, complete.Snapshot.Transmittals.OfficialCount);
        Assert.Equal(0, complete.Snapshot.Rfis.OfficialCount);
        Assert.Equal(0, complete.Snapshot.Submittals.OfficialCount);
        var artifact = new ProjectTechnicalOfficeReportXlsxRenderer(
            ReportingExecutionOptions.Default).Render(complete);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Contains("صفر اثبات‌شده", ReadEntry(archive, "xl/worksheets/sheet2.xml"),
            StringComparison.Ordinal);
        foreach (var index in Enumerable.Range(3, 4))
            Assert.Single(XDocument.Parse(ReadEntry(archive, $"xl/worksheets/sheet{index}.xml"))
                .Descendants((XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main" + "row"));
        var incomplete = Request(Build(ProjectionWithPartialHistory()), ReportFormat.Xlsx);
        Assert.Null(incomplete.Snapshot.Rfis.OfficialCount);
        Assert.Null(incomplete.Snapshot.Submittals.OfficialCount);
        var invalid = incomplete.Snapshot with
        {
            Rfis = incomplete.Snapshot.Rfis with { OfficialCount = 0 }
        };
        Assert.Equal("reporting.project_technical_office.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() =>
                ProjectTechnicalOfficeReportRenderSnapshot.Parse(CanonicalJson.Serialize(invalid))).Code);
    }

    [Fact]
    public void F07CompleteSyntheticHistoryRendersDistinctRfiAndSubmittalFactsWithoutFormulas()
    {
        var projection = ProjectionWithPartialHistory();
        var rfi = projection.Rfis.Single() with
        {
            Number = "=SUM(A1:A2)",
            Events = [Event(1, TechnicalReportingEventType.InternalReview, -5),
                Event(2, TechnicalReportingEventType.Issued, -4),
                Event(3, TechnicalReportingEventType.ResponseReceived, -3) with
                { ResponseClassification = RfiResponseClassification.ChangePotential,
                    ChangePotential = true },
                Event(4, TechnicalReportingEventType.ResponseAccepted, -2)]
        };
        var submittal = projection.Submittals.Single() with
        {
            Events = [Event(1, TechnicalReportingEventType.Submitted, -4),
                Event(2, TechnicalReportingEventType.UnderReview, -3),
                Event(3, TechnicalReportingEventType.Reviewed, -2) with
                { ReviewOutcome = SubmittalReviewOutcome.ForInformation }]
        };
        var request = Request(Build(projection with
        {
            Rfis = [rfi], Submittals = [submittal],
            RfiCompleteness = TechnicalReportingCompleteness.Complete,
            SubmittalCompleteness = TechnicalReportingCompleteness.Complete
        }), ReportFormat.Xlsx);
        Assert.Equal(ReportDataStatus.Available, request.Snapshot.DataStatus);
        Assert.Equal(1, request.Snapshot.Rfis.OfficialCount);
        Assert.Equal(1, request.Snapshot.Submittals.OfficialCount);
        var artifact = new ProjectTechnicalOfficeReportXlsxRenderer(
            ReportingExecutionOptions.Default).Render(request);
        using var stream = new MemoryStream(artifact.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var rfiSheet = ReadEntry(archive, "xl/worksheets/sheet5.xml");
        var submittalSheet = ReadEntry(archive, "xl/worksheets/sheet6.xml");
        Assert.Contains("'=SUM(A1:A2)", rfiSheet, StringComparison.Ordinal);
        Assert.Contains("ResponseAccepted", rfiSheet, StringComparison.Ordinal);
        Assert.Contains("ChangePotential", rfiSheet, StringComparison.Ordinal);
        Assert.Contains("ForInformation", submittalSheet, StringComparison.Ordinal);
        Assert.DoesNotContain("<f>", rfiSheet + submittalSheet, StringComparison.Ordinal);
    }

    [Fact]
    public void F07RejectsNonCanonicalRowsOversizedTextBudgetAndWrongFormat()
    {
        var snapshot = Build(ProjectionWithPartialHistory());
        var parsed = ProjectTechnicalOfficeReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var oversized = parsed with
        {
            Documents = parsed.Documents with
            {
                Rows = parsed.Documents.Rows.Select(item => item with
                    { Discipline = new string('X', 161) }).ToArray()
            }
        };
        Assert.Equal("reporting.project_technical_office.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() =>
                ProjectTechnicalOfficeReportRenderSnapshot.Parse(CanonicalJson.Serialize(oversized))).Code);
        var wrongOrder = parsed with
        {
            Documents = parsed.Documents with
            {
                Rows = new[] { parsed.Documents.Rows.Single() with { Number = "DOC-20" },
                    parsed.Documents.Rows.Single() }
            }
        };
        Assert.Throws<ReportRenderingException>(() =>
            ProjectTechnicalOfficeReportRenderSnapshot.Parse(CanonicalJson.Serialize(wrongOrder)));
        var bounded = ReportingExecutionOptions.Default with
        { MaximumPdfFacts = 1, MaximumXlsxRows = 1 };
        var pdf = new ProjectTechnicalOfficeReportPdfRenderer(RendererOptions(), bounded);
        var xlsx = new ProjectTechnicalOfficeReportXlsxRenderer(bounded);
        var pdfRequest = Request(snapshot, ReportFormat.Pdf);
        var xlsxRequest = Request(snapshot, ReportFormat.Xlsx);
        Assert.Equal("reporting.output.page_limit_exceeded",
            Assert.Throws<ReportRenderingException>(() => pdf.Render(pdfRequest)).Code);
        Assert.Equal("reporting.output.row_limit_exceeded",
            Assert.Throws<ReportRenderingException>(() => xlsx.Render(xlsxRequest)).Code);
        Assert.Equal("reporting.format.unsupported",
            Assert.Throws<ReportRenderingException>(() =>
                pdf.Render(pdfRequest with { Format = ReportFormat.Xlsx })).Code);
        Assert.Equal("reporting.format.unsupported",
            Assert.Throws<ReportRenderingException>(() =>
                xlsx.Render(xlsxRequest with { Format = ReportFormat.Pdf })).Code);
    }

    private static ReportSnapshot Build(ProjectTechnicalOfficeReportingProjection projection)
    {
        var source = ProjectTechnicalOfficeReportingCalculator.Calculate(projection);
        var profile = ProjectTechnicalOfficePinnedProjectProfile.Capture(Profile(), Cutoff.AddMinutes(1));
        return ProjectTechnicalOfficeReportSnapshotBuilder.Build(RunId, TenantId,
            profile, Cutoff, source, Cutoff.AddMinutes(1), Cutoff.AddMinutes(2));
    }

    private static ProjectTechnicalOfficeReportingProjection ProjectionWithPartialHistory() =>
        Projection(
            documents: [new TechnicalReportingDocument(Id(10), TenantId, ProjectId,
                "DOC-10", TechnicalDocumentType.Drawing, "CIVIL", Cutoff.AddDays(-10),
                TechnicalReportingClassification.Confidential)],
            revisions: [new TechnicalReportingRevision(Id(11), TenantId, ProjectId, Id(10),
                "A", new DateOnly(2026, 9, 18), DocumentRevisionPurpose.ForConstruction,
                null, Id(20), Cutoff.AddDays(-6),
                [Event(1, TechnicalReportingEventType.Submitted, -5),
                    Event(2, TechnicalReportingEventType.Approved, -4),
                    Event(3, TechnicalReportingEventType.Issued, -1)])],
            transmittals: [new TechnicalReportingTransmittal(Id(20), TenantId, ProjectId,
                "TRN-20", [Id(11)], null, Cutoff.AddDays(-2),
                [Event(1, TechnicalReportingEventType.Issued, -1)])],
            rfis: [new TechnicalReportingRfi(Id(30), TenantId, ProjectId,
                "RFI-30", new DateOnly(2026, 9, 17), null, true, PotentialImpact.Cost,
                [], Cutoff.AddDays(-6), [])],
            submittals: [new TechnicalReportingSubmittal(Id(40), TenantId, ProjectId,
                "SUB-40", TechnicalSubmittalType.ShopDrawing, "CIVIL", null, 0, null,
                [Id(11)], Cutoff.AddDays(-6), [])],
            rfiCoverage: TechnicalReportingCompleteness.Incomplete,
            submittalCoverage: TechnicalReportingCompleteness.Incomplete);

    private static ProjectTechnicalOfficeReportingProjection Projection(
        IReadOnlyCollection<TechnicalReportingDocument>? documents = null,
        IReadOnlyCollection<TechnicalReportingRevision>? revisions = null,
        IReadOnlyCollection<TechnicalReportingTransmittal>? transmittals = null,
        IReadOnlyCollection<TechnicalReportingRfi>? rfis = null,
        IReadOnlyCollection<TechnicalReportingSubmittal>? submittals = null,
        TechnicalReportingCompleteness rfiCoverage = TechnicalReportingCompleteness.Complete,
        TechnicalReportingCompleteness submittalCoverage = TechnicalReportingCompleteness.Complete) =>
        new(ProjectTechnicalOfficeReportingContract.Version, TenantId, ProjectId,
            DateOnly.FromDateTime(Cutoff.UtcDateTime), Cutoff, 1, Cutoff.AddDays(-30), true,
            TechnicalReportingClassification.Confidential, false,
            documents ?? [], revisions ?? [], transmittals ?? [], rfis ?? [], submittals ?? [],
            TechnicalReportingCompleteness.Complete, TechnicalReportingCompleteness.Complete,
            rfiCoverage, submittalCoverage);

    private static TechnicalReportingEvent Event(long sequence,
        TechnicalReportingEventType type, int days) => new(sequence, type, Cutoff.AddDays(days));

    private static ProjectControlProfile Profile() => new(
        ProjectId, TenantId, "PRJ-F07", "Test Project", "UTC", "IRR", 1,
        Cutoff.AddDays(-30), ProjectStatus.Active, ContractModel.NotConfigured,
        PlanningMode.None, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null), 1);

    private static ProjectTechnicalOfficeReportRenderRequest Request(
        ReportSnapshot snapshot, ReportFormat format)
    {
        var parsed = ProjectTechnicalOfficeReportRenderSnapshot.Parse(snapshot.PayloadJson);
        return new ProjectTechnicalOfficeReportRenderRequest(
            RunId, format == ReportFormat.Pdf ? Id(600) : Id(601), Id(602), Id(603),
            ProjectTechnicalOfficeReportRuntimeContract.DefinitionCode,
            ProjectTechnicalOfficeReportRuntimeContract.DefinitionVersion,
            ProjectTechnicalOfficeReportRuntimeContract.TemplateVersion,
            ProjectTechnicalOfficeReportRuntimeContract.TemplateContentDigest,
            ProjectTechnicalOfficeReportRuntimeContract.RendererContractVersion,
            ProjectTechnicalOfficeReportRuntimeContract.LayoutContractVersion,
            format, ReportArtifactIdentity.FileName(parsed, format),
            "RPT-F070-0000-0000-0000-0001",
            new string(format == ReportFormat.Pdf ? 'e' : 'f', 64),
            snapshot.Sha256, snapshot.SourceManifestSha256, snapshot.SourceCutoffUtc, parsed);
    }

    private static ReportingRendererOptions RendererOptions()
    {
        var fonts = Path.Combine(AppContext.BaseDirectory, "fonts");
        return new ReportingRendererOptions(CertifiedPdfRuntimeContract.LicenseDecision,
            Path.Combine(fonts, PmcsTypographyContract.PdfRegularFileName),
            Path.Combine(fonts, PmcsTypographyContract.PdfBoldFileName),
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

    private static void WriteQualificationArtifact(string fileName, byte[] bytes)
    {
        var output = Environment.GetEnvironmentVariable("PMCS_F07_RENDER_QUALIFICATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, fileName), bytes);
    }

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value.ToString("D12", CultureInfo.InvariantCulture)}");
}
