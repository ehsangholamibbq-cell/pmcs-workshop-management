using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using UglyToad.PdfPig;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private const string ProjectQualityHseDefinitionCode =
        "project-quality-hse-certified";
    private static readonly string[] ProjectQualityHseFormats = ["Pdf", "Xlsx"];
    private static readonly string[] ProjectQualityHseXlsxFormat = ["Xlsx"];
    private static readonly Guid ProjectQualityHseRunId =
        Guid.Parse("7d000000-0000-4000-8000-000000000001");

    private static async Task<int> VerifyReportingProjectQualityHseAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var authorized = Actor("qa-super-admin");
        var qualityController = Actor("quality-controller");
        var hseOfficer = Actor("hse-officer");
        var financeManager = Actor("finance-manager");
        var projectController = Actor("project-controller");
        var reportingPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";
        var request = new ProjectQualityHseReportingRunRequest(
            ProjectQualityHseRunId,
            ProjectQualityHseDefinitionCode,
            "1.0.0",
            null,
            ProjectQualityHseFormats,
            new { });

        var catalog = await SendAsync(
            client,
            key,
            authorized,
            HttpMethod.Get,
            $"{reportingPath}/catalog");
        var definition = FindCatalogDefinition(
            catalog.Payload,
            ProjectQualityHseDefinitionCode);
        Record(
            assertions,
            "reporting.project-quality-hse.catalog.definition-aware",
            catalog.StatusCode == HttpStatusCode.OK &&
            definition.HasValue &&
            HasString(definition.Value, "classification", "Confidential") &&
            HasString(
                definition.Value,
                "parameterSchemaVersion",
                "pmcs.reporting.project-quality-hse.parameters/v1") &&
            HasString(definition.Value, "templateVersion", "1.0.0") &&
            PeriodicContainsString(definition.Value, "supportedFormats", "Pdf") &&
            PeriodicContainsString(definition.Value, "supportedFormats", "Xlsx") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "quality.read") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "hse.read") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "hse.confidential.read") &&
            PeriodicContainsString(definition.Value, "dataStatuses", "NotConfigured"),
            $"http={(int)catalog.StatusCode};found={definition.HasValue}");

        var isolatedCatalog = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Get,
            $"{reportingPath}/catalog");
        Record(
            assertions,
            "reporting.project-quality-hse.catalog.permission-isolated",
            isolatedCatalog.StatusCode == HttpStatusCode.OK &&
            !FindCatalogDefinition(
                isolatedCatalog.Payload,
                ProjectQualityHseDefinitionCode).HasValue &&
            FindCatalogDefinition(
                isolatedCatalog.Payload,
                "project-financial-position-certified").HasValue,
            $"http={(int)isolatedCatalog.StatusCode}");

        var partiallyPermittedCatalog = await SendAsync(
            client, key, projectController, HttpMethod.Get, $"{reportingPath}/catalog");
        Record(
            assertions,
            "reporting.project-quality-hse.catalog.confidential-permission-required",
            partiallyPermittedCatalog.StatusCode == HttpStatusCode.OK &&
            !FindCatalogDefinition(partiallyPermittedCatalog.Payload,
                ProjectQualityHseDefinitionCode).HasValue &&
            FindCatalogDefinition(partiallyPermittedCatalog.Payload,
                "project-progress-certified").HasValue,
            $"http={(int)partiallyPermittedCatalog.StatusCode}");

        var qualityRoleCatalog = await SendAsync(
            client, key, qualityController, HttpMethod.Get, $"{reportingPath}/catalog");
        Record(
            assertions,
            "reporting.project-quality-hse.catalog.quality-role-needs-hse-reads",
            qualityRoleCatalog.StatusCode == HttpStatusCode.OK &&
            !FindCatalogDefinition(qualityRoleCatalog.Payload,
                ProjectQualityHseDefinitionCode).HasValue,
            $"http={(int)qualityRoleCatalog.StatusCode}");

        var hseRoleCatalog = await SendAsync(
            client, key, hseOfficer, HttpMethod.Get, $"{reportingPath}/catalog");
        Record(
            assertions,
            "reporting.project-quality-hse.catalog.hse-role-needs-quality-read",
            hseRoleCatalog.StatusCode == HttpStatusCode.OK &&
            !FindCatalogDefinition(hseRoleCatalog.Payload,
                ProjectQualityHseDefinitionCode).HasValue,
            $"http={(int)hseRoleCatalog.StatusCode}");

        var malformed = await SendAsync(
            client,
            key,
            authorized,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with
            {
                ClientGeneratedId = Guid.Parse("7d000000-0000-4000-8000-000000000098"),
                Formats = ProjectQualityHseXlsxFormat,
                Parameters = new { contractId = Guid.NewGuid() }
            },
            "qa-rpt1-project-quality-hse-malformed");
        Record(
            assertions,
            "reporting.project-quality-hse.parameters.strict-empty-object",
            malformed.StatusCode == HttpStatusCode.BadRequest &&
            HasString(malformed.Payload, "code", "reporting.parameters.invalid"),
            $"http={(int)malformed.StatusCode};code={ReadOptionalString(malformed.Payload, "code")}");

        var denied = await SendAsync(
            client,
            key,
            projectController,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with
            {
                ClientGeneratedId = Guid.Parse("7d000000-0000-4000-8000-000000000099")
            },
            "qa-rpt1-project-quality-hse-source-denied");
        Record(
            assertions,
            "reporting.project-quality-hse.create.requires-all-source-permissions",
            denied.StatusCode == HttpStatusCode.Forbidden &&
            HasString(denied.Payload, "code", "reporting.source_permission.denied"),
            $"http={(int)denied.StatusCode};code={ReadOptionalString(denied.Payload, "code")}");

        foreach (var (actor, suffix, runId) in new[]
        {
            (qualityController, "quality-only", Guid.Parse("7d000000-0000-4000-8000-000000000096")),
            (hseOfficer, "hse-only", Guid.Parse("7d000000-0000-4000-8000-000000000097"))
        })
        {
            var partial = await SendAsync(
                client, key, actor, HttpMethod.Post, $"{reportingPath}/runs",
                request with { ClientGeneratedId = runId },
                $"qa-rpt1-project-quality-hse-{suffix}-denied");
            Record(assertions, $"reporting.project-quality-hse.create.{suffix}-denied",
                partial.StatusCode == HttpStatusCode.Forbidden &&
                HasString(partial.Payload, "code", "reporting.source_permission.denied"),
                $"http={(int)partial.StatusCode};code={ReadOptionalString(partial.Payload, "code")}");
        }

        var created = await SendAsync(
            client,
            key,
            authorized,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-project-quality-hse-create");
        Record(
            assertions,
            "reporting.project-quality-hse.create.accepted",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", ProjectQualityHseRunId) &&
            HasString(
                created.Payload,
                "definitionCode",
                ProjectQualityHseDefinitionCode),
            $"http={(int)created.StatusCode}");

        var replay = await SendAsync(
            client,
            key,
            authorized,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-project-quality-hse-create");
        Record(
            assertions,
            "reporting.project-quality-hse.create.idempotent-replay",
            replay.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(replay.Payload, "id", ProjectQualityHseRunId),
            $"http={(int)replay.StatusCode}");

        var conflict = await SendAsync(
            client,
            key,
            authorized,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with { Formats = ProjectQualityHseXlsxFormat },
            "qa-rpt1-project-quality-hse-create");
        Record(
            assertions,
            "reporting.project-quality-hse.create.idempotency-conflict",
            conflict.StatusCode == HttpStatusCode.Conflict,
            $"http={(int)conflict.StatusCode}");

        var succeeded = await WaitForFinalRunAsync(
            client,
            key,
            authorized,
            reportingPath,
            ProjectQualityHseRunId);
        var outputs = succeeded.Payload.ValueKind == JsonValueKind.Object &&
            succeeded.Payload.TryGetProperty("outputs", out var outputArray) &&
            outputArray.ValueKind == JsonValueKind.Array
                ? outputArray.GetArrayLength()
                : 0;
        Record(
            assertions,
            "reporting.project-quality-hse.worker.succeeded",
            succeeded.StatusCode == HttpStatusCode.OK &&
            HasString(succeeded.Payload, "status", "Succeeded") &&
            HasString(succeeded.Payload, "pipelineStage", "Complete") &&
            (HasString(succeeded.Payload, "dataStatus", "NotConfigured") ||
                HasString(succeeded.Payload, "dataStatus", "InsufficientData")) &&
            outputs == 2,
            $"http={(int)succeeded.StatusCode};" +
            $"status={ReadOptionalString(succeeded.Payload, "status")};" +
            $"stage={ReadOptionalString(succeeded.Payload, "pipelineStage")};" +
            $"dataStatus={ReadOptionalString(succeeded.Payload, "dataStatus")};" +
            $"diagnostic={ReadOptionalString(succeeded.Payload, "diagnosticCode")};" +
            $"outputs={outputs}");

        var visibleRuns = await SendAsync(
            client,
            key,
            authorized,
            HttpMethod.Get,
            $"{reportingPath}/runs?definitionCode={ProjectQualityHseDefinitionCode}&limit=100");
        Record(
            assertions,
            "reporting.project-quality-hse.runs.permission-filtered",
            visibleRuns.StatusCode == HttpStatusCode.OK &&
            visibleRuns.Payload.ValueKind == JsonValueKind.Array &&
            visibleRuns.Payload.EnumerateArray().Any(item =>
                HasGuid(item, "id", ProjectQualityHseRunId)) &&
            visibleRuns.Payload.EnumerateArray().All(item =>
                HasString(
                    item,
                    "definitionCode",
                    ProjectQualityHseDefinitionCode)),
            $"http={(int)visibleRuns.StatusCode}");

        await VerifyProjectQualityHseOutputAsync(
            client,
            key,
            authorized,
            reportingPath,
            succeeded.Payload,
            "Pdf",
            assertions);
        await VerifyProjectQualityHseOutputAsync(
            client,
            key,
            authorized,
            reportingPath,
            succeeded.Payload,
            "Xlsx",
            assertions);

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-project-quality-hse-connected-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            runId = ProjectQualityHseRunId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task VerifyProjectQualityHseOutputAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string reportingPath,
        JsonElement run,
        string format,
        List<VerificationAssertion> assertions)
    {
        var outputId = Guid.Empty;
        string? expectedSha256 = null;
        string? verificationCode = null;
        string? fileName = null;
        var metadataValid = TryFindOutput(run, format, out var output) &&
            TryReadGuid(output, "id", out outputId) &&
            TryReadString(output, "sha256", out expectedSha256) &&
            TryReadString(output, "verificationCode", out verificationCode) &&
            TryReadString(output, "fileName", out fileName) &&
            fileName is not null &&
            fileName.StartsWith(
                "project-quality-hse-DEMO-01-",
                StringComparison.Ordinal);
        Record(
            assertions,
            $"reporting.project-quality-hse.{format.ToLowerInvariant()}.metadata",
            metadataValid,
            $"outputId={outputId};fileName={fileName ?? "<missing>"}");
        if (!metadataValid)
        {
            return;
        }

        var download = await DownloadReportingOutputAsync(
            client,
            key,
            actor,
            $"{reportingPath}/outputs/{outputId}/content");
        var actualSha256 = Convert.ToHexString(SHA256.HashData(download.Bytes)).ToLowerInvariant();
        var payloadValid = format == "Pdf"
            ? ValidateProjectQualityHsePdf(download.Bytes, verificationCode!)
            : ValidateProjectQualityHseWorkbook(download.Bytes, verificationCode!);
        var expectedContentType = format == "Pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        Record(
            assertions,
            $"reporting.project-quality-hse.{format.ToLowerInvariant()}.download.integrity",
            download.StatusCode == HttpStatusCode.OK &&
            string.Equals(download.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal) &&
            string.Equals(download.ETag, $"\"sha256-{expectedSha256}\"", StringComparison.Ordinal) &&
            download.NoStore && download.NoSniff && download.IsAttachment && payloadValid,
            $"http={(int)download.StatusCode};sha256={actualSha256};bytes={download.Bytes.Length}");

        var verified = await SendAsync(
            client,
            key,
            actor,
            HttpMethod.Get,
            $"{reportingPath}/outputs/{outputId}/verify");
        Record(
            assertions,
            $"reporting.project-quality-hse.{format.ToLowerInvariant()}.verify.valid",
            verified.StatusCode == HttpStatusCode.OK &&
            HasString(verified.Payload, "status", "Valid") &&
            HasString(
                verified.Payload,
                "definitionCode",
                ProjectQualityHseDefinitionCode) &&
            HasString(verified.Payload, "sha256", expectedSha256!) &&
            HasString(verified.Payload, "verificationCode", verificationCode!),
            $"http={(int)verified.StatusCode}");
    }

    private static bool ValidateProjectQualityHsePdf(
        byte[] bytes,
        string verificationCode)
    {
        if (bytes.Length == 0 || !bytes.AsSpan().StartsWith("%PDF-"u8))
        {
            return false;
        }

        using var document = PdfDocument.Open(bytes);
        return document.NumberOfPages == 2 &&
            string.Join('\n', document.GetPages().Select(page => page.Text))
                .Contains(verificationCode, StringComparison.Ordinal);
    }

    private static bool ValidateProjectQualityHseWorkbook(
        byte[] bytes,
        string verificationCode)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return archive.GetEntry("[Content_Types].xml") is not null &&
                archive.GetEntry("xl/workbook.xml") is not null &&
                archive.Entries.Count(entry => entry.FullName.StartsWith(
                    "xl/worksheets/sheet",
                    StringComparison.Ordinal)) == 4 &&
                archive.Entries.Any(entry =>
                {
                    if (!entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal))
                    {
                        return false;
                    }
                    using var reader = new StreamReader(entry.Open());
                    return reader.ReadToEnd().Contains(verificationCode, StringComparison.Ordinal);
                });
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private sealed record ProjectQualityHseReportingRunRequest(
        Guid ClientGeneratedId,
        string DefinitionCode,
        string TemplateVersion,
        DateTimeOffset? AsOfUtc,
        string[] Formats,
        object Parameters);
}
