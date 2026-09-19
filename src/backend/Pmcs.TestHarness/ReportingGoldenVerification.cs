using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private const string GoldenReportDateIso = "2099-12-30";
    private const string GoldenFormulaProbe = "=SUM(A1:A2) must remain text";
    private const string GoldenDraftMarker = "GOLDEN-DRAFT-V3-MUST-NOT-APPEAR";
    private const string GoldenCorrectionReason = "Replace the material evidence with the corrected official fact.";

    private static readonly Guid GoldenMeasurementItemId =
        Guid.Parse("74000000-0000-4000-8000-000000000001");
    private static readonly Guid GoldenV1ReportId =
        Guid.Parse("74000000-0000-4000-8000-000000000010");
    private static readonly Guid GoldenV2ReportId =
        Guid.Parse("74000000-0000-4000-8000-000000000020");
    private static readonly Guid GoldenV3ReportId =
        Guid.Parse("74000000-0000-4000-8000-000000000030");
    private static readonly Guid GoldenReplacementFactId =
        Guid.Parse("74000000-0000-4000-8000-000000000201");
    private static readonly Guid GoldenDraftFactId =
        Guid.Parse("74000000-0000-4000-8000-000000000301");
    private static readonly Guid GoldenBeforeRunId =
        Guid.Parse("75000000-0000-4000-8000-000000000001");
    private static readonly Guid GoldenBeforeTwinRunId =
        Guid.Parse("75000000-0000-4000-8000-000000000002");
    private static readonly Guid GoldenAfterRunId =
        Guid.Parse("75000000-0000-4000-8000-000000000003");
    private static readonly Guid GoldenAfterTwinRunId =
        Guid.Parse("75000000-0000-4000-8000-000000000004");

    private static readonly Guid GoldenMaterialFactId =
        Guid.Parse("74000000-0000-4000-8000-000000000104");

    private static readonly GoldenFactSeed[] GoldenV1Facts =
    [
        new(
            Guid.Parse("74000000-0000-4000-8000-000000000101"),
            "WorkProgress",
            "Golden concrete placement reached the certified quantity.",
            "Concrete",
            12.5m,
            "m3",
            null,
            null,
            "Medium",
            "WP-GOLD-01",
            GoldenMeasurementItemId),
        new(
            Guid.Parse("74000000-0000-4000-8000-000000000102"),
            "Labor",
            "Golden labor shift completed.",
            "Concrete crew",
            null,
            null,
            12,
            96m,
            null,
            "LAB-GOLD-01",
            null),
        new(
            Guid.Parse("74000000-0000-4000-8000-000000000103"),
            "Equipment",
            "Golden equipment shift completed.",
            "Concrete pump",
            null,
            null,
            2,
            16m,
            null,
            "EQ-GOLD-01",
            null),
        new(
            GoldenMaterialFactId,
            "Material",
            "Golden material receipt before correction.",
            "Cement",
            40m,
            "bag",
            null,
            null,
            null,
            "MAT-GOLD-OLD",
            null),
        new(
            Guid.Parse("74000000-0000-4000-8000-000000000105"),
            "Issue",
            "Golden critical issue requires management attention.",
            "Quality",
            null,
            null,
            null,
            null,
            "Critical",
            "ISS-GOLD-01",
            null),
        new(
            Guid.Parse("74000000-0000-4000-8000-000000000106"),
            "Stoppage",
            "Golden stoppage caused by the inspection hold point.",
            "Inspection",
            null,
            null,
            null,
            2.5m,
            "High",
            "STOP-GOLD-01",
            null),
        new(
            Guid.Parse("74000000-0000-4000-8000-000000000107"),
            "SiteCondition",
            "Golden site condition remained dry and accessible.",
            "Weather",
            null,
            null,
            null,
            null,
            "Low",
            "SITE-GOLD-01",
            null),
        new(
            Guid.Parse("74000000-0000-4000-8000-000000000108"),
            "Note",
            GoldenFormulaProbe,
            "Spreadsheet safety",
            null,
            null,
            null,
            null,
            null,
            "NOTE-GOLD-01",
            null)
    ];

    private static readonly string[] GoldenDataHeaders =
    [
        "نسخه", "وضعیت نسخه", "تاریخ شمسی", "تاریخ ISO", "محل", "نوع Fact", "شرح",
        "دسته", "مقدار", "واحد", "تعداد منبع", "ساعت", "اثر", "کد مرجع",
        "Measurement Item ID", "Fact ID", "Copied From Fact ID", "Report ID"
    ];

    private static readonly string[] GoldenWorkbookEntries =
    [
        "[Content_Types].xml",
        "_rels/.rels",
        "docProps/app.xml",
        "docProps/core.xml",
        "xl/workbook.xml",
        "xl/_rels/workbook.xml.rels",
        "xl/styles.xml",
        "xl/worksheets/sheet1.xml",
        "xl/worksheets/sheet2.xml"
    ];

    private static readonly string[] GoldenVariableMetadataKeys =
    [
        "Output Manifest SHA-256",
        "کد راستی‌آزمایی",
        "مسیر راستی‌آزمایی"
    ];

    private static readonly char[] GoldenPersianDigits =
        ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];

    private static async Task<int> VerifyReportingGoldenAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var siteSupervisor = Actor("site-supervisor");
        var technicalOffice = Actor("technical-office");
        var reportsPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/daily-reports";
        var reportingPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";

        var measurement = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/planning/measurement-items",
            new
            {
                clientGeneratedId = GoldenMeasurementItemId,
                code = "QA-GOLDEN-M3",
                title = "Golden concrete placement",
                unit = "m3",
                targetQuantity = 100m,
                notes = "Deterministic certified reporting Golden fixture."
            },
            "qa-rpt1-golden-measurement-create");
        RequireGolden(
            measurement.StatusCode == HttpStatusCode.Created &&
            HasGuid(measurement.Payload, "id", GoldenMeasurementItemId),
            $"Golden measurement setup failed with HTTP {(int)measurement.StatusCode}.");

        var v1Created = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            reportsPath,
            new
            {
                clientGeneratedId = GoldenV1ReportId,
                reportDate = GoldenReportDateIso,
                locationName = "Golden QA Root",
                narrative = "Golden v1 official daily report."
            },
            "qa-rpt1-golden-v1-create");
        RequireGolden(
            v1Created.StatusCode == HttpStatusCode.Created &&
            HasGuid(v1Created.Payload, "id", GoldenV1ReportId),
            $"Golden v1 creation failed with HTTP {(int)v1Created.StatusCode}.");

        var v1Revision = RequireRevision(v1Created.Payload, "Golden v1 creation");
        foreach (var fact in GoldenV1Facts)
        {
            var added = await SendAsync(
                client,
                key,
                siteSupervisor,
                HttpMethod.Post,
                $"{reportsPath}/{GoldenV1ReportId}/facts",
                new GoldenDailyFactRequest(
                    fact.Id,
                    fact.Kind,
                    fact.Description,
                    fact.Category,
                    fact.Quantity,
                    fact.Unit,
                    fact.ResourceCount,
                    fact.Hours,
                    fact.ImpactLevel,
                    fact.ReferenceCode,
                    v1Revision,
                    fact.MeasurementItemId,
                    PmcsTestDataSet.RootLocationId),
                $"qa-rpt1-golden-v1-fact-{fact.Id:N}");
            RequireGolden(
                added.StatusCode == HttpStatusCode.OK && ContainsId(added.Payload, "facts", fact.Id),
                $"Golden v1 fact '{fact.Id}' failed with HTTP {(int)added.StatusCode}.");
            v1Revision = RequireRevision(added.Payload, $"Golden v1 fact '{fact.Id}'");
        }

        var v1Submitted = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV1ReportId}/submit",
            new { baseRevision = v1Revision },
            "qa-rpt1-golden-v1-submit");
        RequireGolden(
            v1Submitted.StatusCode == HttpStatusCode.OK && HasString(v1Submitted.Payload, "status", "Submitted"),
            $"Golden v1 submission failed with HTTP {(int)v1Submitted.StatusCode}.");

        var v1Approved = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV1ReportId}/approve",
            new
            {
                baseRevision = RequireRevision(v1Submitted.Payload, "Golden v1 submission"),
                comment = "Golden v1 approved."
            },
            "qa-rpt1-golden-v1-approve");
        RequireGolden(
            v1Approved.StatusCode == HttpStatusCode.OK && HasString(v1Approved.Payload, "status", "Approved"),
            $"Golden v1 approval failed with HTTP {(int)v1Approved.StatusCode}.");
        var beforeCutoff = RequireInstant(v1Approved.Payload, "reviewedAt", "Golden v1 approval");
        var v1ApprovedRevision = RequireRevision(v1Approved.Payload, "Golden v1 approval");
        var v1Facts = ReadGoldenFacts(v1Approved.Payload, "Golden v1 approval");

        var v2Started = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV1ReportId}/corrections",
            new
            {
                clientGeneratedId = GoldenV2ReportId,
                baseRevision = v1ApprovedRevision,
                reason = GoldenCorrectionReason
            },
            "qa-rpt1-golden-v2-start");
        RequireGolden(
            v2Started.StatusCode == HttpStatusCode.Created &&
            HasGuid(v2Started.Payload, "id", GoldenV2ReportId),
            $"Golden v2 correction start failed with HTTP {(int)v2Started.StatusCode}.");
        var v2CopiedFacts = ReadGoldenFacts(v2Started.Payload, "Golden v2 correction start");
        var copiedMaterial = v2CopiedFacts.SingleOrDefault(fact => fact.CopiedFromFactId == GoldenMaterialFactId);
        RequireGolden(copiedMaterial is not null, "Golden v2 did not copy the source material fact.");
        var copiedMaterialId = copiedMaterial!.Id;

        var v2Removed = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV2ReportId}/facts/{copiedMaterialId}/remove",
            new { baseRevision = RequireRevision(v2Started.Payload, "Golden v2 correction start") },
            "qa-rpt1-golden-v2-remove-material");
        RequireGolden(
            v2Removed.StatusCode == HttpStatusCode.OK &&
            !ContainsId(v2Removed.Payload, "facts", copiedMaterialId),
            $"Golden v2 material removal failed with HTTP {(int)v2Removed.StatusCode}.");

        var v2Added = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV2ReportId}/facts",
            new GoldenDailyFactRequest(
                GoldenReplacementFactId,
                "Material",
                "Golden corrected official material receipt.",
                "Cement",
                45m,
                "bag",
                null,
                null,
                null,
                "MAT-GOLD-NEW",
                RequireRevision(v2Removed.Payload, "Golden v2 material removal"),
                null,
                PmcsTestDataSet.RootLocationId),
            "qa-rpt1-golden-v2-add-material");
        RequireGolden(
            v2Added.StatusCode == HttpStatusCode.OK &&
            ContainsId(v2Added.Payload, "facts", GoldenReplacementFactId),
            $"Golden v2 replacement fact failed with HTTP {(int)v2Added.StatusCode}.");

        var v2Submitted = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV2ReportId}/submit",
            new { baseRevision = RequireRevision(v2Added.Payload, "Golden v2 replacement fact") },
            "qa-rpt1-golden-v2-submit");
        RequireGolden(
            v2Submitted.StatusCode == HttpStatusCode.OK && HasString(v2Submitted.Payload, "status", "Submitted"),
            $"Golden v2 submission failed with HTTP {(int)v2Submitted.StatusCode}.");

        var v2Approved = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV2ReportId}/approve",
            new
            {
                baseRevision = RequireRevision(v2Submitted.Payload, "Golden v2 submission"),
                comment = "Golden v2 approved."
            },
            "qa-rpt1-golden-v2-approve");
        RequireGolden(
            v2Approved.StatusCode == HttpStatusCode.OK && HasString(v2Approved.Payload, "status", "Approved"),
            $"Golden v2 approval failed with HTTP {(int)v2Approved.StatusCode}.");
        var afterCutoff = RequireInstant(v2Approved.Payload, "reviewedAt", "Golden v2 approval");
        var v2ApprovedRevision = RequireRevision(v2Approved.Payload, "Golden v2 approval");
        var v2Facts = ReadGoldenFacts(v2Approved.Payload, "Golden v2 approval");

        var v3Started = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV2ReportId}/corrections",
            new
            {
                clientGeneratedId = GoldenV3ReportId,
                baseRevision = v2ApprovedRevision,
                reason = "Draft correction must remain outside certified output."
            },
            "qa-rpt1-golden-v3-start");
        RequireGolden(
            v3Started.StatusCode == HttpStatusCode.Created &&
            HasGuid(v3Started.Payload, "id", GoldenV3ReportId) &&
            HasString(v3Started.Payload, "status", "Draft"),
            $"Golden v3 draft start failed with HTTP {(int)v3Started.StatusCode}.");

        var v3Added = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{GoldenV3ReportId}/facts",
            new GoldenDailyFactRequest(
                GoldenDraftFactId,
                "Note",
                GoldenDraftMarker,
                "Draft exclusion",
                null,
                null,
                null,
                null,
                null,
                "DRAFT-GOLD-03",
                RequireRevision(v3Started.Payload, "Golden v3 draft start"),
                null,
                PmcsTestDataSet.RootLocationId),
            "qa-rpt1-golden-v3-add-marker");
        RequireGolden(
            v3Added.StatusCode == HttpStatusCode.OK &&
            HasString(v3Added.Payload, "status", "Draft") &&
            ContainsId(v3Added.Payload, "facts", GoldenDraftFactId),
            $"Golden v3 marker failed with HTTP {(int)v3Added.StatusCode}.");

        var v3Facts = ReadGoldenFacts(v3Added.Payload, "Golden v3 marker");
        var v1Kinds = v1Facts.Select(fact => fact.Kind).ToHashSet(StringComparer.Ordinal);
        var copiedSourceIds = v2Facts
            .Where(fact => fact.CopiedFromFactId.HasValue)
            .Select(fact => fact.CopiedFromFactId!.Value)
            .ToHashSet();
        Record(
            assertions,
            "reporting.golden.fixture.lineage-and-draft",
            v1Facts.Length == 8 &&
            v1Kinds.SetEquals(GoldenV1Facts.Select(fact => fact.Kind)) &&
            v2Facts.Length == 8 &&
            v2Facts.Count(fact => fact.CopiedFromFactId.HasValue) == 7 &&
            !copiedSourceIds.Contains(GoldenMaterialFactId) &&
            v2Facts.Any(fact => fact.Id == GoldenReplacementFactId && !fact.CopiedFromFactId.HasValue) &&
            v3Facts.Length == 9 &&
            v3Facts.Any(fact => fact.Id == GoldenDraftFactId && fact.Description == GoldenDraftMarker),
            $"v1={v1Facts.Length};v2={v2Facts.Length};v2Copied={copiedSourceIds.Count};v3={v3Facts.Length}");

        var beforeExpected = BuildExpectedRows(
            new GoldenExpectedVersion(
                1,
                "نسخه رسمی جاری",
                GoldenV1ReportId,
                GoldenReportDateIso,
                ReadOptionalString(v1Approved.Payload, "locationName"),
                v1Facts));
        var afterExpected = BuildExpectedRows(
            new GoldenExpectedVersion(
                1,
                "نسخه رسمی جایگزین‌شده",
                GoldenV1ReportId,
                GoldenReportDateIso,
                ReadOptionalString(v1Approved.Payload, "locationName"),
                v1Facts),
            new GoldenExpectedVersion(
                2,
                "نسخه رسمی جاری",
                GoldenV2ReportId,
                GoldenReportDateIso,
                ReadOptionalString(v2Approved.Payload, "locationName"),
                v2Facts));

        var before = await CreateGoldenRunAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            GoldenBeforeRunId,
            beforeCutoff,
            "qa-rpt1-golden-before",
            replay: true,
            repeatDownload: true,
            assertions);
        var beforeTwin = await CreateGoldenRunAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            GoldenBeforeTwinRunId,
            beforeCutoff,
            "qa-rpt1-golden-before-twin",
            replay: false,
            repeatDownload: false,
            assertions);
        var after = await CreateGoldenRunAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            GoldenAfterRunId,
            afterCutoff,
            "qa-rpt1-golden-after",
            replay: true,
            repeatDownload: true,
            assertions);
        var afterTwin = await CreateGoldenRunAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            GoldenAfterTwinRunId,
            afterCutoff,
            "qa-rpt1-golden-after-twin",
            replay: false,
            repeatDownload: false,
            assertions);

        Record(
            assertions,
            "reporting.golden.snapshot.cutoff-hashes",
            string.Equals(before.SnapshotHash, beforeTwin.SnapshotHash, StringComparison.Ordinal) &&
            string.Equals(after.SnapshotHash, afterTwin.SnapshotHash, StringComparison.Ordinal) &&
            !string.Equals(before.SnapshotHash, after.SnapshotHash, StringComparison.Ordinal),
            $"before={before.SnapshotHash};after={after.SnapshotHash}");

        Record(
            assertions,
            "reporting.golden.before.semantic-workbook",
            RowsEqual(before.Workbook.DataRows, beforeExpected) &&
            RowsEqual(beforeTwin.Workbook.DataRows, beforeExpected) &&
            string.Equals(before.Workbook.SemanticDigest, beforeTwin.Workbook.SemanticDigest, StringComparison.Ordinal) &&
            before.Workbook.DataRows.All(row => row[17] == GoldenV1ReportId.ToString()) &&
            before.Workbook.DataRows.All(row => row[1] == "نسخه رسمی جاری"),
            $"rows={before.Workbook.DataRows.Count};digest={before.Workbook.SemanticDigest}");

        var afterRows = after.Workbook.DataRows;
        Record(
            assertions,
            "reporting.golden.after.semantic-workbook",
            RowsEqual(afterRows, afterExpected) &&
            RowsEqual(afterTwin.Workbook.DataRows, afterExpected) &&
            string.Equals(after.Workbook.SemanticDigest, afterTwin.Workbook.SemanticDigest, StringComparison.Ordinal) &&
            !string.Equals(before.Workbook.SemanticDigest, after.Workbook.SemanticDigest, StringComparison.Ordinal) &&
            afterRows.Count(row => row[17] == GoldenV1ReportId.ToString()) == 8 &&
            afterRows.Count(row => row[17] == GoldenV2ReportId.ToString()) == 8 &&
            afterRows.All(row => row[17] != GoldenV3ReportId.ToString()) &&
            afterRows.All(row => row[15] != GoldenDraftFactId.ToString()) &&
            afterRows.All(row => !row[6].Contains(GoldenDraftMarker, StringComparison.Ordinal)),
            $"rows={afterRows.Count};digest={after.Workbook.SemanticDigest}");

        var expectedKindLabels = GoldenV1Facts
            .Select(fact => GoldenFactKind(fact.Kind))
            .ToHashSet(StringComparer.Ordinal);
        Record(
            assertions,
            "reporting.golden.xlsx.typed-safety-and-lineage",
            afterRows.Select(row => row[5]).ToHashSet(StringComparer.Ordinal).SetEquals(expectedKindLabels) &&
            afterRows.Any(row => row[12] == "بحرانی" && row[5] == "مسئله") &&
            afterRows.Any(row =>
                row[14] == GoldenMeasurementItemId.ToString() &&
                decimal.TryParse(row[8], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) &&
                quantity == 12.5m &&
                row[9] == "m3") &&
            afterRows.Any(row => row[6] == $"'{GoldenFormulaProbe}") &&
            afterRows.Count(row => row[16] != "—") == 7 &&
            afterRows.Any(row => row[15] == GoldenReplacementFactId.ToString() && row[16] == "—"),
            "eight kinds, critical impact, measurement quantity/unit, formula-safe text and correction lineage");

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-semantic-xlsx-golden-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            rootReportId = GoldenV1ReportId,
            beforeCutoff,
            afterCutoff,
            beforeSnapshotHash = before.SnapshotHash,
            afterSnapshotHash = after.SnapshotHash,
            beforeWorkbookSemanticDigest = before.Workbook.SemanticDigest,
            afterWorkbookSemanticDigest = after.Workbook.SemanticDigest,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task<GoldenRunEvidence> CreateGoldenRunAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string reportingPath,
        Guid runId,
        DateTimeOffset cutoff,
        string idempotencyKey,
        bool replay,
        bool repeatDownload,
        List<VerificationAssertion> assertions)
    {
        var request = CreateReportingRequest(
            runId,
            formats: GoldenXlsxFormat,
            includeRevisionChain: true,
            asOfUtc: cutoff) with
        {
            Parameters = new ReportingParameters(GoldenV1ReportId, true)
        };
        var created = await SendAsync(
            client,
            key,
            actor,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            idempotencyKey);
        RequireGolden(
            created.StatusCode == HttpStatusCode.Accepted && HasGuid(created.Payload, "id", runId),
            $"Golden run '{runId}' creation failed with HTTP {(int)created.StatusCode}.");

        if (replay)
        {
            var replayed = await SendAsync(
                client,
                key,
                actor,
                HttpMethod.Post,
                $"{reportingPath}/runs",
                request,
                idempotencyKey);
            Record(
                assertions,
                $"reporting.golden.{runId:N}.idempotent-replay",
                replayed.StatusCode == HttpStatusCode.Accepted && HasGuid(replayed.Payload, "id", runId),
                $"http={(int)replayed.StatusCode}");
        }

        var succeeded = await WaitForFinalRunAsync(client, key, actor, reportingPath, runId);
        string? snapshotHash = null;
        JsonElement output = default;
        var outputId = Guid.Empty;
        string? outputSha256 = null;
        string? verificationCode = null;
        RequireGolden(
            succeeded.StatusCode == HttpStatusCode.OK &&
            HasString(succeeded.Payload, "status", "Succeeded") &&
            HasString(succeeded.Payload, "pipelineStage", "Complete") &&
            HasString(succeeded.Payload, "dataStatus", "Available") &&
            TryReadString(succeeded.Payload, "snapshotHash", out snapshotHash) &&
            snapshotHash is not null && IsGoldenSha256(snapshotHash) &&
            TryFindOutput(succeeded.Payload, "Xlsx", out output) &&
            TryReadGuid(output, "id", out outputId) &&
            TryReadString(output, "sha256", out outputSha256) &&
            TryReadString(output, "verificationCode", out verificationCode),
            $"Golden run '{runId}' did not produce a valid XLSX contract.");

        var download = await DownloadReportingOutputAsync(
            client,
            key,
            actor,
            $"{reportingPath}/outputs/{outputId}/content");
        var actualSha256 = GoldenSha256(download.Bytes);
        RequireGolden(
            download.StatusCode == HttpStatusCode.OK &&
            string.Equals(
                download.ContentType,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actualSha256, outputSha256, StringComparison.Ordinal) &&
            string.Equals(download.ETag, $"\"sha256-{outputSha256}\"", StringComparison.Ordinal) &&
            download.NoStore && download.NoSniff && download.IsAttachment,
            $"Golden output '{outputId}' download integrity failed.");

        if (repeatDownload)
        {
            var repeated = await DownloadReportingOutputAsync(
                client,
                key,
                actor,
                $"{reportingPath}/outputs/{outputId}/content");
            Record(
                assertions,
                $"reporting.golden.{runId:N}.stored-bytes-deterministic",
                repeated.StatusCode == HttpStatusCode.OK &&
                download.Bytes.SequenceEqual(repeated.Bytes) &&
                string.Equals(actualSha256, GoldenSha256(repeated.Bytes), StringComparison.Ordinal),
                $"sha256={actualSha256};bytes={download.Bytes.Length}");
        }

        var workbook = ReadGoldenWorkbook(
            download.Bytes,
            outputId,
            verificationCode!,
            snapshotHash!,
            cutoff);
        Record(
            assertions,
            $"reporting.golden.{runId:N}.openxml-contract",
            workbook.ContractValid,
            workbook.Detail);
        return new GoldenRunEvidence(runId, snapshotHash!, outputSha256!, workbook);
    }

    private static GoldenWorkbookEvidence ReadGoldenWorkbook(
        byte[] bytes,
        Guid outputId,
        string verificationCode,
        string snapshotHash,
        DateTimeOffset cutoff)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var names = archive.Entries.Select(entry => entry.FullName).ToArray();
            var workbook = XDocument.Parse(ReadWorkbookEntry(archive, "xl/workbook.xml"));
            var metadata = XDocument.Parse(ReadWorkbookEntry(archive, "xl/worksheets/sheet1.xml"));
            var data = XDocument.Parse(ReadWorkbookEntry(archive, "xl/worksheets/sheet2.xml"));
            var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheetNames = workbook
                .Descendants(spreadsheet + "sheet")
                .Select(element => element.Attribute("name")?.Value ?? string.Empty)
                .ToArray();
            var metadataRows = ReadGoldenRows(metadata, 2);
            var dataRows = ReadGoldenRows(data, GoldenDataHeaders.Length);
            var metadataMap = metadataRows
                .Skip(1)
                .ToDictionary(row => row[0], row => row[1], StringComparer.Ordinal);
            var dataOnly = dataRows.Skip(1).Select(row => row.ToArray()).ToArray();
            var expectedFilter = $"A1:R{Math.Max(1, dataRows.Count)}";
            var contractValid =
                names.OrderBy(name => name, StringComparer.Ordinal)
                    .SequenceEqual(GoldenWorkbookEntries.OrderBy(name => name, StringComparer.Ordinal)) &&
                names.All(name => !name.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("externalLinks", StringComparison.OrdinalIgnoreCase)) &&
                sheetNames.SequenceEqual(GoldenSheetNames) &&
                metadataRows.Count == 17 &&
                metadataRows[0].SequenceEqual(GoldenMetadataHeaders) &&
                dataRows.Count >= 2 &&
                dataRows[0].SequenceEqual(GoldenDataHeaders) &&
                HasGoldenWorksheetView(metadata, spreadsheet) &&
                HasGoldenWorksheetView(data, spreadsheet) &&
                !metadata.Descendants(spreadsheet + "f").Any() &&
                !data.Descendants(spreadsheet + "f").Any() &&
                data.Descendants(spreadsheet + "autoFilter").SingleOrDefault()?.Attribute("ref")?.Value ==
                    expectedFilter &&
                metadataMap.TryGetValue("Snapshot SHA-256", out var workbookSnapshotHash) &&
                string.Equals(workbookSnapshotHash, snapshotHash, StringComparison.Ordinal) &&
                metadataMap.TryGetValue("Source Manifest SHA-256", out var sourceManifestHash) &&
                IsGoldenSha256(sourceManifestHash) &&
                metadataMap.TryGetValue("Output Manifest SHA-256", out var outputManifestHash) &&
                IsGoldenSha256(outputManifestHash) &&
                metadataMap.TryGetValue("کد راستی‌آزمایی", out var workbookVerificationCode) &&
                string.Equals(workbookVerificationCode, verificationCode, StringComparison.Ordinal) &&
                metadataMap.TryGetValue("مسیر راستی‌آزمایی", out var verificationPath) &&
                string.Equals(
                    verificationPath,
                    $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports/outputs/{outputId}/verify",
                    StringComparison.Ordinal) &&
                metadataMap.TryGetValue("Cutoff UTC", out var cutoffText) &&
                DateTimeOffset.TryParse(
                    cutoffText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var workbookCutoff) &&
                workbookCutoff == cutoff;
            var semanticMetadata = metadataMap
                .Where(item => !GoldenVariableMetadataKeys.Contains(item.Key, StringComparer.Ordinal))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new GoldenMetadataItem(item.Key, item.Value))
                .ToArray();
            var semanticJson = JsonSerializer.Serialize(
                new GoldenWorkbookProjection(semanticMetadata, dataOnly),
                JsonOptions);
            return new GoldenWorkbookEvidence(
                contractValid,
                contractValid ? "valid deterministic OpenXML contract" : "invalid deterministic OpenXML contract",
                GoldenSha256(Encoding.UTF8.GetBytes(semanticJson)),
                dataOnly,
                metadataMap);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or InvalidOperationException or ArgumentException or System.Xml.XmlException)
        {
            throw new InvalidOperationException(
                $"Golden workbook parsing failed: {exception.GetType().Name}: {exception.Message}",
                exception);
        }
    }

    private static bool HasGoldenWorksheetView(XDocument document, XNamespace spreadsheet)
    {
        var view = document.Descendants(spreadsheet + "sheetView").SingleOrDefault();
        var pane = view?.Element(spreadsheet + "pane");
        return view?.Attribute("rightToLeft")?.Value == "1" &&
            pane is not null &&
            pane.Attribute("state")?.Value == "frozen" &&
            pane.Attribute("ySplit")?.Value == "1" &&
            pane.Attribute("topLeftCell")?.Value == "A2";
    }

    private static List<string[]> ReadGoldenRows(XDocument document, int columnCount)
    {
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<string[]>();
        foreach (var row in document.Descendants(spreadsheet + "sheetData").Elements(spreadsheet + "row"))
        {
            var values = Enumerable.Repeat(string.Empty, columnCount).ToArray();
            foreach (var cell in row.Elements(spreadsheet + "c"))
            {
                var reference = cell.Attribute("r")?.Value
                    ?? throw new InvalidDataException("A workbook cell reference is missing.");
                var column = GoldenColumnIndex(reference);
                if (column < 1 || column > columnCount)
                {
                    throw new InvalidDataException($"Workbook cell '{reference}' is outside the expected table.");
                }
                values[column - 1] = cell.Attribute("t")?.Value == "inlineStr"
                    ? string.Concat(cell.Descendants(spreadsheet + "t").Select(element => element.Value))
                    : cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
            }
            rows.Add(values);
        }
        return rows;
    }

    private static int GoldenColumnIndex(string reference)
    {
        var column = 0;
        foreach (var character in reference)
        {
            if (character is < 'A' or > 'Z')
            {
                break;
            }
            column = checked(column * 26 + character - 'A' + 1);
        }
        return column;
    }

    private static string[][] BuildExpectedRows(params GoldenExpectedVersion[] versions) =>
        versions
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId)
            .SelectMany(version => version.Facts
                .OrderBy(fact => fact.CreatedAt)
                .ThenBy(fact => fact.Id)
                .Select(fact => new[]
                {
                    GoldenPersianNumber(version.VersionNumber),
                    version.State,
                    GoldenPersianDate(version.ReportDateIso),
                    version.ReportDateIso,
                    GoldenSafeSpreadsheetText(fact.LocationName ?? version.LocationName),
                    GoldenFactKind(fact.Kind),
                    GoldenSafeSpreadsheetText(fact.Description),
                    GoldenSafeSpreadsheetText(fact.Category),
                    GoldenDecimal(fact.Quantity),
                    GoldenSafeSpreadsheetText(fact.Unit),
                    fact.ResourceCount?.ToString(CultureInfo.InvariantCulture) ?? "—",
                    GoldenDecimal(fact.Hours),
                    GoldenImpact(fact.ImpactLevel),
                    GoldenSafeSpreadsheetText(fact.ReferenceCode),
                    fact.MeasurementItemId?.ToString() ?? "—",
                    fact.Id.ToString(),
                    fact.CopiedFromFactId?.ToString() ?? "—",
                    version.ReportId.ToString()
                }))
            .ToArray();

    private static GoldenFactSnapshot[] ReadGoldenFacts(JsonElement payload, string operation)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("facts", out var facts) ||
            facts.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"{operation} did not return a facts array.");
        }

        return facts.EnumerateArray().Select(fact => new GoldenFactSnapshot(
            RequireGuid(fact, "id", operation),
            ReadOptionalGuid(fact, "copiedFromFactId"),
            RequireString(fact, "kind", operation),
            RequireString(fact, "description", operation),
            ReadNullableString(fact, "category"),
            ReadNullableString(fact, "locationName"),
            ReadNullableDecimal(fact, "quantity"),
            ReadNullableString(fact, "unit"),
            ReadNullableInt32(fact, "resourceCount"),
            ReadNullableDecimal(fact, "hours"),
            ReadNullableString(fact, "impactLevel"),
            ReadNullableString(fact, "referenceCode"),
            ReadOptionalGuid(fact, "measurementItemId"),
            RequireInstant(fact, "createdAt", operation))).ToArray();
    }

    private static long RequireRevision(JsonElement payload, string operation) =>
        ReadInt64(payload, "revision")
        ?? throw new InvalidOperationException($"{operation} did not return a revision.");

    private static Guid RequireGuid(JsonElement payload, string property, string operation) =>
        TryReadGuid(payload, property, out var value)
            ? value
            : throw new InvalidOperationException($"{operation} did not return '{property}'.");

    private static string RequireString(JsonElement payload, string property, string operation) =>
        TryReadString(payload, property, out var value)
            ? value!
            : throw new InvalidOperationException($"{operation} did not return '{property}'.");

    private static DateTimeOffset RequireInstant(JsonElement payload, string property, string operation) =>
        TryReadString(payload, property, out var value) &&
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : throw new InvalidOperationException($"{operation} did not return a valid '{property}'.");

    private static Guid? ReadOptionalGuid(JsonElement payload, string property) =>
        TryReadGuid(payload, property, out var value) ? value : null;

    private static string? ReadNullableString(JsonElement payload, string property) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal? ReadNullableDecimal(JsonElement payload, string property) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var parsed)
            ? parsed
            : null;

    private static int? ReadNullableInt32(JsonElement payload, string property) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static bool RowsEqual(IReadOnlyList<string[]> actual, string[][] expected) =>
        actual.Count == expected.Length &&
        actual.Zip(expected).All(pair => pair.First.SequenceEqual(pair.Second));

    private static string GoldenPersianDate(string isoDate)
    {
        var date = DateOnly.ParseExact(isoDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .ToDateTime(TimeOnly.MinValue);
        var calendar = new PersianCalendar();
        return GoldenPersianNumber(
            $"{calendar.GetYear(date):0000}/{calendar.GetMonth(date):00}/{calendar.GetDayOfMonth(date):00}");
    }

    private static string GoldenPersianNumber(int value) =>
        GoldenPersianNumber(value.ToString(CultureInfo.InvariantCulture));

    private static string GoldenPersianNumber(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                span[index] = character is >= '0' and <= '9'
                    ? GoldenPersianDigits[character - '0']
                    : character;
            }
        });

    private static string GoldenSafeSpreadsheetText(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        return normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@'
            ? $"'{normalized}"
            : normalized;
    }

    private static string GoldenDecimal(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "—";

    private static string GoldenFactKind(string kind) => kind switch
    {
        "WorkProgress" => "پیشرفت کار",
        "Labor" => "نیروی انسانی",
        "Equipment" => "ماشین‌آلات",
        "Material" => "مصالح",
        "Issue" => "مسئله",
        "Stoppage" => "توقف",
        "SiteCondition" => "شرایط کارگاه",
        "Note" => "یادداشت",
        _ => throw new InvalidOperationException($"Unsupported Golden fact kind '{kind}'.")
    };

    private static string GoldenImpact(string? impact) => impact switch
    {
        "Low" => "کم",
        "Medium" => "متوسط",
        "High" => "زیاد",
        "Critical" => "بحرانی",
        null => "—",
        _ => throw new InvalidOperationException($"Unsupported Golden impact '{impact}'.")
    };

    private static string GoldenSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool IsGoldenSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireGolden(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static readonly string[] GoldenXlsxFormat = ["Xlsx"];
    private static readonly string[] GoldenSheetNames = ["Metadata", "Data"];
    private static readonly string[] GoldenMetadataHeaders = ["کلید", "مقدار"];

    private sealed record GoldenFactSeed(
        Guid Id,
        string Kind,
        string Description,
        string? Category,
        decimal? Quantity,
        string? Unit,
        int? ResourceCount,
        decimal? Hours,
        string? ImpactLevel,
        string? ReferenceCode,
        Guid? MeasurementItemId);

    private sealed record GoldenDailyFactRequest(
        Guid ClientGeneratedId,
        string Kind,
        string Description,
        string? Category,
        decimal? Quantity,
        string? Unit,
        int? ResourceCount,
        decimal? Hours,
        string? ImpactLevel,
        string? ReferenceCode,
        long BaseRevision,
        Guid? MeasurementItemId,
        Guid LocationId);

    private sealed record GoldenFactSnapshot(
        Guid Id,
        Guid? CopiedFromFactId,
        string Kind,
        string Description,
        string? Category,
        string? LocationName,
        decimal? Quantity,
        string? Unit,
        int? ResourceCount,
        decimal? Hours,
        string? ImpactLevel,
        string? ReferenceCode,
        Guid? MeasurementItemId,
        DateTimeOffset CreatedAt);

    private sealed record GoldenExpectedVersion(
        int VersionNumber,
        string State,
        Guid ReportId,
        string ReportDateIso,
        string? LocationName,
        IReadOnlyList<GoldenFactSnapshot> Facts);

    private sealed record GoldenRunEvidence(
        Guid RunId,
        string SnapshotHash,
        string OutputSha256,
        GoldenWorkbookEvidence Workbook);

    private sealed record GoldenWorkbookEvidence(
        bool ContractValid,
        string Detail,
        string SemanticDigest,
        IReadOnlyList<string[]> DataRows,
        IReadOnlyDictionary<string, string> Metadata);

    private sealed record GoldenMetadataItem(string Key, string Value);

    private sealed record GoldenWorkbookProjection(
        IReadOnlyList<GoldenMetadataItem> Metadata,
        IReadOnlyList<string[]> DataRows);
}
