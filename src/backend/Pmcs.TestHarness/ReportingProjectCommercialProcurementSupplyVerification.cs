using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using UglyToad.PdfPig;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private const string ProjectCommercialProcurementSupplyDefinitionCode =
        "project-commercial-procurement-supply-certified";
    private static readonly string[] ProjectCommercialProcurementSupplyFormats = ["Pdf", "Xlsx"];
    private static readonly string[] ProjectCommercialProcurementSupplyXlsxFormat = ["Xlsx"];
    private static readonly Guid ProjectCommercialProcurementSupplyRunId =
        Guid.Parse("7b000000-0000-4000-8000-000000000001");

    private static async Task<int> VerifyReportingProjectCommercialProcurementSupplyAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var financeManager = Actor("finance-manager");
        var reportingPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";
        var request = new ProjectCommercialProcurementSupplyReportingRunRequest(
            ProjectCommercialProcurementSupplyRunId,
            ProjectCommercialProcurementSupplyDefinitionCode,
            "1.0.0",
            null,
            ProjectCommercialProcurementSupplyFormats,
            new { });

        var catalog = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Get,
            $"{reportingPath}/catalog");
        var definition = FindCatalogDefinition(
            catalog.Payload,
            ProjectCommercialProcurementSupplyDefinitionCode);
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.catalog.definition-aware",
            catalog.StatusCode == HttpStatusCode.OK &&
            definition.HasValue &&
            HasString(definition.Value, "classification", "Confidential") &&
            HasString(
                definition.Value,
                "parameterSchemaVersion",
                "pmcs.reporting.project-commercial-procurement-supply.parameters/v1") &&
            HasString(definition.Value, "templateVersion", "1.0.0") &&
            PeriodicContainsString(definition.Value, "supportedFormats", "Pdf") &&
            PeriodicContainsString(definition.Value, "supportedFormats", "Xlsx") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "commercial-state.read") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "commercial.parties.read") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "contracts.read") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "procurement.requests.read") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "procurement.orders.read") &&
            PeriodicContainsString(
                definition.Value,
                "requiredSourcePermissions",
                "supply.read") &&
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
            "reporting.project-commercial-procurement-supply.catalog.permission-isolated",
            isolatedCatalog.StatusCode == HttpStatusCode.OK &&
            !FindCatalogDefinition(
                isolatedCatalog.Payload,
                ProjectCommercialProcurementSupplyDefinitionCode).HasValue &&
            FindCatalogDefinition(
                isolatedCatalog.Payload,
                "project-financial-position-certified").HasValue,
            $"http={(int)isolatedCatalog.StatusCode}");

        var malformed = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with
            {
                ClientGeneratedId = Guid.Parse("7b000000-0000-4000-8000-000000000098"),
                Formats = ProjectCommercialProcurementSupplyXlsxFormat,
                Parameters = new { contractId = Guid.NewGuid() }
            },
            "qa-rpt1-project-commercial-procurement-supply-malformed");
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.parameters.strict-empty-object",
            malformed.StatusCode == HttpStatusCode.BadRequest &&
            HasString(malformed.Payload, "code", "reporting.parameters.invalid"),
            $"http={(int)malformed.StatusCode};code={ReadOptionalString(malformed.Payload, "code")}");

        var denied = await SendAsync(
            client,
            key,
            financeManager,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with
            {
                ClientGeneratedId = Guid.Parse("7b000000-0000-4000-8000-000000000099")
            },
            "qa-rpt1-project-commercial-procurement-supply-source-denied");
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.create.requires-all-source-permissions",
            denied.StatusCode == HttpStatusCode.Forbidden &&
            HasString(denied.Payload, "code", "reporting.source_permission.denied"),
            $"http={(int)denied.StatusCode};code={ReadOptionalString(denied.Payload, "code")}");

        var created = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-project-commercial-procurement-supply-create");
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.create.accepted",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", ProjectCommercialProcurementSupplyRunId) &&
            HasString(
                created.Payload,
                "definitionCode",
                ProjectCommercialProcurementSupplyDefinitionCode),
            $"http={(int)created.StatusCode}");

        var replay = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-project-commercial-procurement-supply-create");
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.create.idempotent-replay",
            replay.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(replay.Payload, "id", ProjectCommercialProcurementSupplyRunId),
            $"http={(int)replay.StatusCode}");

        var conflict = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with { Formats = ProjectCommercialProcurementSupplyXlsxFormat },
            "qa-rpt1-project-commercial-procurement-supply-create");
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.create.idempotency-conflict",
            conflict.StatusCode == HttpStatusCode.Conflict,
            $"http={(int)conflict.StatusCode}");

        var succeeded = await WaitForFinalRunAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            ProjectCommercialProcurementSupplyRunId);
        var outputs = succeeded.Payload.ValueKind == JsonValueKind.Object &&
            succeeded.Payload.TryGetProperty("outputs", out var outputArray) &&
            outputArray.ValueKind == JsonValueKind.Array
                ? outputArray.GetArrayLength()
                : 0;
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.worker.succeeded",
            succeeded.StatusCode == HttpStatusCode.OK &&
            HasString(succeeded.Payload, "status", "Succeeded") &&
            HasString(succeeded.Payload, "pipelineStage", "Complete") &&
            HasString(succeeded.Payload, "dataStatus", "NoData") &&
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
            technicalOffice,
            HttpMethod.Get,
            $"{reportingPath}/runs?definitionCode={ProjectCommercialProcurementSupplyDefinitionCode}&limit=100");
        Record(
            assertions,
            "reporting.project-commercial-procurement-supply.runs.permission-filtered",
            visibleRuns.StatusCode == HttpStatusCode.OK &&
            visibleRuns.Payload.ValueKind == JsonValueKind.Array &&
            visibleRuns.Payload.EnumerateArray().Any(item =>
                HasGuid(item, "id", ProjectCommercialProcurementSupplyRunId)) &&
            visibleRuns.Payload.EnumerateArray().All(item =>
                HasString(
                    item,
                    "definitionCode",
                    ProjectCommercialProcurementSupplyDefinitionCode)),
            $"http={(int)visibleRuns.StatusCode}");

        await VerifyProjectCommercialProcurementSupplyOutputAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            succeeded.Payload,
            "Pdf",
            assertions);
        await VerifyProjectCommercialProcurementSupplyOutputAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            succeeded.Payload,
            "Xlsx",
            assertions);

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-project-commercial-procurement-supply-connected-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            runId = ProjectCommercialProcurementSupplyRunId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task VerifyProjectCommercialProcurementSupplyOutputAsync(
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
                "project-commercial-procurement-supply-DEMO-01-",
                StringComparison.Ordinal);
        Record(
            assertions,
            $"reporting.project-commercial-procurement-supply.{format.ToLowerInvariant()}.metadata",
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
            ? ValidateProjectCommercialProcurementSupplyPdf(download.Bytes, verificationCode!)
            : ValidateProjectCommercialProcurementSupplyWorkbook(download.Bytes, verificationCode!);
        var expectedContentType = format == "Pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        Record(
            assertions,
            $"reporting.project-commercial-procurement-supply.{format.ToLowerInvariant()}.download.integrity",
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
            $"reporting.project-commercial-procurement-supply.{format.ToLowerInvariant()}.verify.valid",
            verified.StatusCode == HttpStatusCode.OK &&
            HasString(verified.Payload, "status", "Valid") &&
            HasString(
                verified.Payload,
                "definitionCode",
                ProjectCommercialProcurementSupplyDefinitionCode) &&
            HasString(verified.Payload, "sha256", expectedSha256!) &&
            HasString(verified.Payload, "verificationCode", verificationCode!),
            $"http={(int)verified.StatusCode}");
    }

    private static bool ValidateProjectCommercialProcurementSupplyPdf(
        byte[] bytes,
        string verificationCode)
    {
        if (bytes.Length == 0 || !bytes.AsSpan().StartsWith("%PDF-"u8))
        {
            return false;
        }

        using var document = PdfDocument.Open(bytes);
        return document.NumberOfPages == 3 &&
            string.Join('\n', document.GetPages().Select(page => page.Text))
                .Contains(verificationCode, StringComparison.Ordinal);
    }

    private static bool ValidateProjectCommercialProcurementSupplyWorkbook(
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
                    StringComparison.Ordinal)) == 10 &&
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

    private sealed record ProjectCommercialProcurementSupplyReportingRunRequest(
        Guid ClientGeneratedId,
        string DefinitionCode,
        string TemplateVersion,
        DateTimeOffset? AsOfUtc,
        string[] Formats,
        object Parameters);
}
