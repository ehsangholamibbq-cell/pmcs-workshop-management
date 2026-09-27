using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Rendering;

namespace Pmcs.Domain.Tests;

public sealed class ProjectGovernanceActionReportRenderingTests
{
    private static readonly Guid Tenant = Id(1);
    private static readonly Guid Project = Id(2);
    private static readonly Guid Run = Id(500);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 9, 27);
    private static readonly string[] SheetNames =
        ["Metadata", "Coverage", "Issue", "Risk", "Decision", "Escalation", "Action"];
    private static readonly string[] ExpectedVisualDigests =
    [
        "6cf11dd7ef26b414355e7619a26c934c8d1d25c38ec45c4edb1da28544bd56fb",
        "b1814cefff50d40d1e4ead503d5e68db12bce5e5e1663bb3d6b4212dca2eeab5",
        "4b9e1f3aa6403475cdb060c1e816d1bcad3189eb1a96ca4678d2b39127978ccf",
        "673d82016d65bc9220dcdd97b575e2cd6001dfc75afb4fc352e5c006c548c1bc",
        "161c61d90f75da2d8cb2b3c31a203c0b2876305b472c5331e8b8e056acb54f4b"
    ];

    [Fact]
    public void F09RendererPinsTemplateSnapshotAndFiveSections()
    {
        var snapshot = Snapshot();
        var parsed = ProjectGovernanceActionReportRenderSnapshot.Parse(snapshot.PayloadJson);
        var request = Request(snapshot, ReportFormat.Pdf);
        var registry = new ProjectGovernanceActionReportRendererRegistry(
            new IProjectGovernanceActionReportRenderer[]
            {
                new ProjectGovernanceActionReportPdfRenderer(Options(), ReportingExecutionOptions.Default),
                new ProjectGovernanceActionReportXlsxRenderer(ReportingExecutionOptions.Default)
            });
        Assert.Equal(snapshot.Sha256, CanonicalJson.Sha256(CanonicalJson.Serialize(parsed)));
        Assert.Equal("project-governance-action-PRJ-F09-1405-07-05.pdf", request.FileName);
        Assert.Equal(ReportFormat.Pdf, registry.Require(ReportFormat.Pdf).Format);
        Assert.Equal(ReportFormat.Xlsx, registry.Require(ReportFormat.Xlsx).Format);
        Assert.Equal("reporting.format.unsupported",
            Assert.Throws<ReportRenderingException>(() => registry.Require(ReportFormat.Csv)).Code);
        Assert.Equal("reporting.project_governance_action.render_request.invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectGovernanceActionReportRenderModel.Create(
                request with { SnapshotSha256 = new string('0', 64) })).Code);
        Assert.Equal("reporting.project_governance_action.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectGovernanceActionReportRenderSnapshot.Parse(
                snapshot.PayloadJson.Replace(ProjectGovernanceActionReportRuntimeContract.SemanticContractId,
                    "PMCS-RPT1-F09-TAMPERED", StringComparison.Ordinal))).Code);
    }

    [Fact]
    public void F09XlsxGoldenKeepsUnknownDecisionCountAndEscapesFormula()
    {
        var request = Request(Snapshot(), ReportFormat.Xlsx);
        var renderer = new ProjectGovernanceActionReportXlsxRenderer(ReportingExecutionOptions.Default);
        var first = renderer.Render(request);
        var second = renderer.Render(request);
        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.True(first.Bytes.AsSpan().StartsWith("PK"u8));
        using var stream = new MemoryStream(first.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(14, archive.Entries.Count);
        var workbook = Read(archive, "xl/workbook.xml");
        foreach (var name in SheetNames) Assert.Contains(name, workbook, StringComparison.Ordinal);
        var sheets = Enumerable.Range(1, 7).Select(index => Read(archive,
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
        Assert.Single(XDocument.Parse(sheets[4]).Descendants(spreadsheet + "row"));
        Assert.DoesNotContain("SourceFactId", string.Concat(sheets), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AssigneeDisplayName", string.Concat(sheets), StringComparison.OrdinalIgnoreCase);
        Assert.True(first.Sha256 == "7b41d33ea7db98fada6a041b9fd6bc5c265a8ab8400513fe643ca830d27efb1f",
            $"F09_XLSX_GOLDEN_SHA256={first.Sha256}");
    }

    [Fact]
    public void F09PdfGoldenRendersFiveIndependentSections()
    {
        var request = Request(Snapshot(), ReportFormat.Pdf);
        var renderer = new ProjectGovernanceActionReportPdfRenderer(Options(), ReportingExecutionOptions.Default);
        var first = renderer.Render(request);
        var second = renderer.Render(request);
        var images = renderer.RenderQualificationImages(request);
        var repeated = renderer.RenderQualificationImages(request);
        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.True(first.Bytes.AsSpan().StartsWith("%PDF-"u8));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(first.Bytes[^32..]), StringComparison.Ordinal);
        Assert.Equal(5, images.Count);
        Assert.Equal(images.Count, repeated.Count);
        Assert.All(images, image => Assert.True(image.AsSpan().StartsWith(
            new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A })));
        for (var index = 0; index < images.Count; index++)
            Assert.True(images[index].SequenceEqual(repeated[index]));
        var visualDigests = images.Select(page =>
            Convert.ToHexString(SHA256.HashData(page)).ToLowerInvariant()).ToArray();
        Assert.True(first.Sha256 == "85858ce8df5340841553bb53108515a49feb6637c4c5efb676f8f24f81dfdbb1" &&
                visualDigests.SequenceEqual(ExpectedVisualDigests),
            $"F09_PDF_GOLDEN_SHA256={first.Sha256}; F09_PDF_VISUAL_SHA256={string.Join(',', visualDigests)}");
    }

    [Fact]
    public void F09RendererRejectsFalseZeroUnknownStateAndActionDowngrade()
    {
        var parsed = ProjectGovernanceActionReportRenderSnapshot.Parse(Snapshot().PayloadJson);
        Assert.Null(parsed.Decisions.OfficialCount);
        Assert.Equal(2, parsed.Issues.OfficialCount);
        Assert.Equal("reporting.project_governance_action.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectGovernanceActionReportRenderSnapshot.Parse(
                CanonicalJson.Serialize(parsed with
                {
                    Decisions = parsed.Decisions with { OfficialCount = 0 }
                }))).Code);
        Assert.Equal("reporting.project_governance_action.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectGovernanceActionReportRenderSnapshot.Parse(
                CanonicalJson.Serialize(parsed with
                {
                    Issues = parsed.Issues with { Rows = parsed.Issues.Rows.Select(x =>
                        x with { State = "Unapproved" }).ToArray() }
                }))).Code);
        Assert.Equal("reporting.project_governance_action.snapshot.payload_invalid",
            Assert.Throws<ReportRenderingException>(() => ProjectGovernanceActionReportRenderSnapshot.Parse(
                CanonicalJson.Serialize(parsed with
                {
                    Actions = parsed.Actions with { Rows = parsed.Actions.Rows.Select(x =>
                        x with { Classification = GovernanceActionReportingClassification.Confidential }).ToArray() }
                }))).Code);
    }

    private static ReportSnapshot Snapshot()
    {
        var issue = new ProjectGovernanceActionReportSection(GovernanceActionReportingStatus.Available, 2,
            [new GovernanceActionReportingFact(Id(10), "=SUM(A1:A2)", GovernanceActionFactKind.Issue,
                "Open", Cutoff.AddDays(-2), Date, null, "High", null,
                GovernanceActionReportingClassification.Confidential),
             new GovernanceActionReportingFact(Id(11), "ISS-11", GovernanceActionFactKind.Issue,
                "Resolved", Cutoff.AddDays(-1), Date, null, "Medium", null,
                GovernanceActionReportingClassification.Confidential)], [], ReportClassification.Confidential);
        var empty = new ProjectGovernanceActionReportSection(GovernanceActionReportingStatus.NoData, 0,
            [], [GovernanceActionReportingReason.NoOfficialRisk], ReportClassification.Confidential);
        var decision = new ProjectGovernanceActionReportSection(
            GovernanceActionReportingStatus.InsufficientData, null, [],
            [GovernanceActionReportingReason.HistoricalTransitionUnavailable],
            ReportClassification.Confidential);
        var escalation = new ProjectGovernanceActionReportSection(GovernanceActionReportingStatus.NoData, 0,
            [], [GovernanceActionReportingReason.NoRaisedEscalation], ReportClassification.Confidential);
        var action = new ProjectGovernanceActionReportSection(GovernanceActionReportingStatus.Available, 1,
            [new GovernanceActionReportingFact(Id(30), Id(30).ToString("N"),
                GovernanceActionFactKind.Action, "Open", Cutoff.AddDays(-1), Date, null,
                "High", null, GovernanceActionReportingClassification.Restricted)], [],
            ReportClassification.Restricted);
        var payload = new ProjectGovernanceActionReportSemanticSnapshot(
            ProjectGovernanceActionReportRuntimeContract.SnapshotSchemaVersion,
            ProjectGovernanceActionReportRuntimeContract.SemanticContractId,
            ProjectGovernanceActionReportRuntimeContract.DefinitionCode,
            ProjectGovernanceActionReportRuntimeContract.DefinitionVersion,
            ProjectGovernanceActionReportingContract.PolicyVersion, ReportDataStatus.InsufficientData,
            [GovernanceActionReportingReason.NoOfficialRisk,
             GovernanceActionReportingReason.NoRaisedEscalation,
             GovernanceActionReportingReason.HistoricalTransitionUnavailable],
            new ProjectGovernanceActionReportParameters(),
            new ProjectGovernanceActionReportProjectIdentity(Project, Tenant, "PRJ-F09", "Test Project",
                "UTC", 1, 1, Cutoff.AddDays(-30), Cutoff.AddMinutes(1)),
            new ProjectGovernanceActionReportCutoffIdentity(Cutoff, Date),
            ReportClassification.Restricted, issue, empty, decision, escalation, action,
            CanonicalJson.Sha256("{}"), new string('a', 64));
        return ReportSnapshot.Create(Id(602), Run, Tenant, Project,
            ProjectGovernanceActionReportRuntimeContract.SnapshotSchemaVersion,
            payload.DataStatus, CanonicalJson.Serialize(payload), "{}",
            payload.Classification, Cutoff.AddMinutes(2), Cutoff);
    }

    private static ProjectGovernanceActionReportRenderRequest Request(
        ReportSnapshot snapshot, ReportFormat format)
    {
        var parsed = ProjectGovernanceActionReportRenderSnapshot.Parse(snapshot.PayloadJson);
        return new ProjectGovernanceActionReportRenderRequest(Run,
            format == ReportFormat.Pdf ? Id(600) : Id(601), snapshot.Id, Id(603),
            ProjectGovernanceActionReportRuntimeContract.DefinitionCode,
            ProjectGovernanceActionReportRuntimeContract.DefinitionVersion,
            ProjectGovernanceActionReportRuntimeContract.TemplateVersion,
            ProjectGovernanceActionReportRuntimeContract.TemplateContentDigest,
            ProjectGovernanceActionReportRuntimeContract.RendererContractVersion,
            ProjectGovernanceActionReportRuntimeContract.LayoutContractVersion,
            format, ReportArtifactIdentity.FileName(parsed, format), "RPT-F090-0000-0000-0000-0001",
            new string(format == ReportFormat.Pdf ? 'e' : 'f', 64),
            snapshot.Sha256, snapshot.SourceManifestSha256, snapshot.SourceCutoffUtc, parsed);
    }

    private static ReportingRendererOptions Options()
    {
        var fonts = Path.Combine(AppContext.BaseDirectory, "fonts");
        return new ReportingRendererOptions(CertifiedPdfRuntimeContract.LicenseDecision,
            Path.Combine(fonts, "DejaVuSans.ttf"), Path.Combine(fonts, "DejaVuSans-Bold.ttf"),
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
