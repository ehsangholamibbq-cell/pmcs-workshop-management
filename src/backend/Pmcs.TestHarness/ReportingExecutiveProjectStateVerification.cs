using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using UglyToad.PdfPig;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private const string ExecutiveProjectStateDefinitionCode =
        "executive-project-state-certified";
    private static readonly string[] ExecutiveProjectStateFormats = ["Pdf", "Xlsx"];
    private static readonly string[] ExecutiveProjectStateXlsxFormat = ["Xlsx"];
    private static readonly Guid ExecutiveProjectStateRunId =
        Guid.Parse("78000000-0000-4000-8000-000000000001");

    private static async Task<int> VerifyReportingExecutiveProjectStateAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var financeManager = Actor("finance-manager");
        var observer = Actor("observer");
        var reportingPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";
        var request = new ExecutiveProjectStateReportingRunRequest(
            ExecutiveProjectStateRunId,
            ExecutiveProjectStateDefinitionCode,
            "1.0.0",
            null,
            ExecutiveProjectStateFormats,
            new { });

        var catalog = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Get,
            $"{reportingPath}/catalog");
        var definition = FindCatalogDefinition(
            catalog.Payload,
            ExecutiveProjectStateDefinitionCode);
        Record(
            assertions,
            "reporting.executive-state.catalog.permission-isolated",
            catalog.StatusCode == HttpStatusCode.OK &&
            definition.HasValue &&
            HasString(
                definition.Value,
                "parameterSchemaVersion",
                "pmcs.reporting.executive-project-state.parameters/v1") &&
            HasString(definition.Value, "templateVersion", "1.0.0") &&
            PeriodicContainsString(definition.Value, "supportedFormats", "Pdf") &&
            PeriodicContainsString(definition.Value, "supportedFormats", "Xlsx") &&
            PeriodicContainsString(definition.Value, "requiredSourcePermissions", "project-state.read") &&
            PeriodicContainsString(definition.Value, "dataStatuses", "NotConfigured") &&
            !FindCatalogDefinition(catalog.Payload, "daily-report-certified").HasValue &&
            !FindCatalogDefinition(catalog.Payload, "project-periodic-certified").HasValue,
            $"http={(int)catalog.StatusCode};found={definition.HasValue}");

        var malformed = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with
            {
                ClientGeneratedId = Guid.Parse("78000000-0000-4000-8000-000000000098"),
                Formats = ExecutiveProjectStateXlsxFormat,
                Parameters = new { snapshotId = Guid.NewGuid() }
            },
            "qa-rpt1-executive-state-malformed");
        Record(
            assertions,
            "reporting.executive-state.parameters.strict-empty-object",
            malformed.StatusCode == HttpStatusCode.BadRequest &&
            HasString(malformed.Payload, "code", "reporting.parameters.invalid"),
            $"http={(int)malformed.StatusCode};code={ReadOptionalString(malformed.Payload, "code")}");

        var observerCreate = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with
            {
                ClientGeneratedId = Guid.Parse("78000000-0000-4000-8000-000000000099")
            },
            "qa-rpt1-executive-state-observer-denied");
        Record(
            assertions,
            "reporting.executive-state.observer.create.denied",
            observerCreate.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)observerCreate.StatusCode}");

        var created = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-executive-state-create");
        Record(
            assertions,
            "reporting.executive-state.create.accepted",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", ExecutiveProjectStateRunId) &&
            HasString(created.Payload, "definitionCode", ExecutiveProjectStateDefinitionCode),
            $"http={(int)created.StatusCode}");

        var replay = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-executive-state-create");
        Record(
            assertions,
            "reporting.executive-state.create.idempotent-replay",
            replay.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(replay.Payload, "id", ExecutiveProjectStateRunId),
            $"http={(int)replay.StatusCode}");

        var conflict = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with { Formats = ExecutiveProjectStateXlsxFormat },
            "qa-rpt1-executive-state-create");
        Record(
            assertions,
            "reporting.executive-state.create.idempotency-conflict",
            conflict.StatusCode == HttpStatusCode.Conflict,
            $"http={(int)conflict.StatusCode}");

        var succeeded = await WaitForFinalRunAsync(
            client,
            key,
            financeManager,
            reportingPath,
            ExecutiveProjectStateRunId);
        var outputs = succeeded.Payload.ValueKind == JsonValueKind.Object &&
            succeeded.Payload.TryGetProperty("outputs", out var outputArray) &&
            outputArray.ValueKind == JsonValueKind.Array
                ? outputArray.GetArrayLength()
                : 0;
        Record(
            assertions,
            "reporting.executive-state.worker.succeeded",
            succeeded.StatusCode == HttpStatusCode.OK &&
            HasString(succeeded.Payload, "status", "Succeeded") &&
            HasString(succeeded.Payload, "pipelineStage", "Complete") &&
            HasString(succeeded.Payload, "dataStatus", "NoData") &&
            outputs == 2,
            $"http={(int)succeeded.StatusCode};status={ReadOptionalString(succeeded.Payload, "status")};outputs={outputs}");

        var visibleRuns = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Get,
            $"{reportingPath}/runs?limit=100");
        Record(
            assertions,
            "reporting.executive-state.runs.permission-filtered",
            visibleRuns.StatusCode == HttpStatusCode.OK &&
            visibleRuns.Payload.ValueKind == JsonValueKind.Array &&
            visibleRuns.Payload.EnumerateArray().Any(item =>
                HasGuid(item, "id", ExecutiveProjectStateRunId)) &&
            visibleRuns.Payload.EnumerateArray().All(item =>
                HasString(item, "definitionCode", ExecutiveProjectStateDefinitionCode)),
            $"http={(int)visibleRuns.StatusCode}");

        await VerifyExecutiveProjectStateOutputAsync(
            client,
            key,
            financeManager,
            reportingPath,
            succeeded.Payload,
            "Pdf",
            assertions);
        await VerifyExecutiveProjectStateOutputAsync(
            client,
            key,
            financeManager,
            reportingPath,
            succeeded.Payload,
            "Xlsx",
            assertions);

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-executive-project-state-connected-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            runId = ExecutiveProjectStateRunId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task VerifyExecutiveProjectStateOutputAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string reportingPath,
        JsonElement run,
        string format,
        List<VerificationAssertion> assertions)
    {
        JsonElement output = default;
        var outputId = Guid.Empty;
        string? expectedSha256 = null;
        string? verificationCode = null;
        string? fileName = null;
        var metadataValid = TryFindOutput(run, format, out output) &&
            TryReadGuid(output, "id", out outputId) &&
            TryReadString(output, "sha256", out expectedSha256) &&
            TryReadString(output, "verificationCode", out verificationCode) &&
            TryReadString(output, "fileName", out fileName) &&
            fileName is not null &&
            fileName.StartsWith("executive-project-state-DEMO-01-", StringComparison.Ordinal);
        Record(
            assertions,
            $"reporting.executive-state.{format.ToLowerInvariant()}.metadata",
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
            ? ValidateExecutiveProjectStatePdf(download.Bytes, verificationCode!)
            : ValidateExecutiveProjectStateWorkbook(download.Bytes, verificationCode!);
        var expectedContentType = format == "Pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        Record(
            assertions,
            $"reporting.executive-state.{format.ToLowerInvariant()}.download.integrity",
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
            $"reporting.executive-state.{format.ToLowerInvariant()}.verify.valid",
            verified.StatusCode == HttpStatusCode.OK &&
            HasString(verified.Payload, "status", "Valid") &&
            HasString(verified.Payload, "definitionCode", ExecutiveProjectStateDefinitionCode) &&
            HasString(verified.Payload, "sha256", expectedSha256!) &&
            HasString(verified.Payload, "verificationCode", verificationCode!),
            $"http={(int)verified.StatusCode}");
    }

    private static bool ValidateExecutiveProjectStatePdf(byte[] bytes, string verificationCode)
    {
        if (bytes.Length == 0 || !bytes.AsSpan().StartsWith("%PDF-"u8))
        {
            return false;
        }

        using var document = PdfDocument.Open(bytes);
        return document.NumberOfPages is >= 1 and <= 2 &&
            string.Join('\n', document.GetPages().Select(page => page.Text))
                .Contains(verificationCode, StringComparison.Ordinal);
    }

    private static bool ValidateExecutiveProjectStateWorkbook(byte[] bytes, string verificationCode)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return archive.GetEntry("[Content_Types].xml") is not null &&
                archive.GetEntry("xl/workbook.xml") is not null &&
                archive.Entries.Count(entry => entry.FullName.StartsWith(
                    "xl/worksheets/sheet",
                    StringComparison.Ordinal)) == 8 &&
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

    private sealed record ExecutiveProjectStateReportingRunRequest(
        Guid ClientGeneratedId,
        string DefinitionCode,
        string TemplateVersion,
        DateTimeOffset? AsOfUtc,
        string[] Formats,
        object Parameters);
}
