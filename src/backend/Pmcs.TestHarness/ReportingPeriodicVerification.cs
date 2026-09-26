using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using UglyToad.PdfPig;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private const string PeriodicDefinitionCode = "project-periodic-certified";
    private static readonly string[] PeriodicFormats = ["Pdf", "Xlsx"];
    private static readonly string[] PeriodicXlsxFormat = ["Xlsx"];
    private static readonly Guid PeriodicRunId =
        Guid.Parse("77000000-0000-4000-8000-000000000001");

    private static async Task<int> VerifyReportingPeriodicAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var observer = Actor("observer");
        var reportingPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";
        var periodStart = LastClosedWeekStart(DateTimeOffset.UtcNow);
        var request = new ProjectPeriodicReportingRunRequest(
            PeriodicRunId,
            PeriodicDefinitionCode,
            "1.0.0",
            null,
            PeriodicFormats,
            new ProjectPeriodicReportingParameters(
                "Weekly",
                periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

        var catalog = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"{reportingPath}/catalog");
        var periodicDefinition = FindCatalogDefinition(catalog.Payload, PeriodicDefinitionCode);
        Record(
            assertions,
            "reporting.periodic.catalog.published",
            catalog.StatusCode == HttpStatusCode.OK &&
            periodicDefinition.HasValue &&
            HasString(periodicDefinition.Value, "parameterSchemaVersion", "pmcs.reporting.project-periodic.parameters/v1") &&
            HasString(periodicDefinition.Value, "templateVersion", "1.0.0") &&
            PeriodicContainsString(periodicDefinition.Value, "supportedFormats", "Pdf") &&
            PeriodicContainsString(periodicDefinition.Value, "supportedFormats", "Xlsx") &&
            PeriodicContainsString(periodicDefinition.Value, "dataStatuses", "NotConfigured"),
            $"http={(int)catalog.StatusCode};found={periodicDefinition.HasValue}");

        var malformed = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            new
            {
                clientGeneratedId = Guid.Parse("77000000-0000-4000-8000-000000000098"),
                definitionCode = PeriodicDefinitionCode,
                templateVersion = "1.0.0",
                formats = PeriodicXlsxFormat,
                parameters = new
                {
                    periodKind = "Weekly",
                    periodStartLocalDate = periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    unexpected = true
                }
            },
            "qa-rpt1-periodic-malformed");
        Record(
            assertions,
            "reporting.periodic.parameters.strict",
            malformed.StatusCode == HttpStatusCode.BadRequest &&
            HasString(malformed.Payload, "code", "reporting.parameters.invalid"),
            $"http={(int)malformed.StatusCode};code={ReadOptionalString(malformed.Payload, "code")}");

        var observerCreate = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with { ClientGeneratedId = Guid.Parse("77000000-0000-4000-8000-000000000099") },
            "qa-rpt1-periodic-observer-denied");
        Record(
            assertions,
            "reporting.periodic.observer.create.denied",
            observerCreate.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)observerCreate.StatusCode}");

        var created = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-periodic-create");
        Record(
            assertions,
            "reporting.periodic.create.accepted",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", PeriodicRunId) &&
            HasString(created.Payload, "definitionCode", PeriodicDefinitionCode),
            $"http={(int)created.StatusCode}");

        var replay = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-periodic-create");
        Record(
            assertions,
            "reporting.periodic.create.idempotent-replay",
            replay.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(replay.Payload, "id", PeriodicRunId),
            $"http={(int)replay.StatusCode}");

        var conflict = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request with { Formats = PeriodicXlsxFormat },
            "qa-rpt1-periodic-create");
        Record(
            assertions,
            "reporting.periodic.create.idempotency-conflict",
            conflict.StatusCode == HttpStatusCode.Conflict,
            $"http={(int)conflict.StatusCode}");

        var succeeded = await WaitForFinalRunAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            PeriodicRunId);
        var outputs = succeeded.Payload.ValueKind == JsonValueKind.Object &&
            succeeded.Payload.TryGetProperty("outputs", out var outputArray) &&
            outputArray.ValueKind == JsonValueKind.Array
                ? outputArray.GetArrayLength()
                : 0;
        Record(
            assertions,
            "reporting.periodic.worker.succeeded",
            succeeded.StatusCode == HttpStatusCode.OK &&
            HasString(succeeded.Payload, "status", "Succeeded") &&
            HasString(succeeded.Payload, "pipelineStage", "Complete") &&
            HasString(succeeded.Payload, "dataStatus", "NotConfigured") &&
            outputs == 2,
            $"http={(int)succeeded.StatusCode};status={ReadOptionalString(succeeded.Payload, "status")};outputs={outputs}");

        await VerifyPeriodicOutputAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            succeeded.Payload,
            "Pdf",
            assertions);
        await VerifyPeriodicOutputAsync(
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
            stage = "rpt1-project-periodic-connected-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            runId = PeriodicRunId,
            periodKind = "Weekly",
            periodStartLocalDate = periodStart,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task VerifyPeriodicOutputAsync(
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
            fileName.StartsWith("project-weekly-DEMO-01-", StringComparison.Ordinal);
        Record(
            assertions,
            $"reporting.periodic.{format.ToLowerInvariant()}.metadata",
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
            ? ValidatePeriodicPdf(download.Bytes, verificationCode!)
            : ValidatePeriodicWorkbook(download.Bytes, verificationCode!);
        var expectedContentType = format == "Pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        Record(
            assertions,
            $"reporting.periodic.{format.ToLowerInvariant()}.download.integrity",
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
            $"reporting.periodic.{format.ToLowerInvariant()}.verify.valid",
            verified.StatusCode == HttpStatusCode.OK &&
            HasString(verified.Payload, "status", "Valid") &&
            HasString(verified.Payload, "definitionCode", PeriodicDefinitionCode) &&
            HasString(verified.Payload, "sha256", expectedSha256!) &&
            HasString(verified.Payload, "verificationCode", verificationCode!),
            $"http={(int)verified.StatusCode}");
    }

    private static bool ValidatePeriodicPdf(byte[] bytes, string verificationCode)
    {
        if (bytes.Length == 0 || !bytes.AsSpan().StartsWith("%PDF-"u8))
        {
            return false;
        }

        using var document = PdfDocument.Open(bytes);
        return document.NumberOfPages >= 1 &&
            string.Join('\n', document.GetPages().Select(page => page.Text))
                .Contains(verificationCode, StringComparison.Ordinal);
    }

    private static bool ValidatePeriodicWorkbook(byte[] bytes, string verificationCode)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return archive.GetEntry("[Content_Types].xml") is not null &&
                archive.GetEntry("xl/workbook.xml") is not null &&
                archive.Entries.Count(entry => entry.FullName.StartsWith(
                    "xl/worksheets/sheet",
                    StringComparison.Ordinal)) >= 8 &&
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

    private static JsonElement? FindCatalogDefinition(JsonElement payload, string code)
    {
        if (payload.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in payload.EnumerateArray())
        {
            if (HasString(candidate, "code", code))
            {
                return candidate.Clone();
            }
        }
        return null;
    }

    private static bool PeriodicContainsString(JsonElement payload, string property, string expected) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(property, out var values) &&
        values.ValueKind == JsonValueKind.Array &&
        values.EnumerateArray().Any(value =>
            value.ValueKind == JsonValueKind.String &&
            string.Equals(value.GetString(), expected, StringComparison.Ordinal));

    private static DateOnly LastClosedWeekStart(DateTimeOffset nowUtc)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, timeZone).Date);
        var daysSinceSaturday = ((int)localDate.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return localDate.AddDays(-daysSinceSaturday - 7);
    }

    private sealed record ProjectPeriodicReportingRunRequest(
        Guid ClientGeneratedId,
        string DefinitionCode,
        string TemplateVersion,
        DateTimeOffset? AsOfUtc,
        string[] Formats,
        ProjectPeriodicReportingParameters Parameters);

    private sealed record ProjectPeriodicReportingParameters(
        string PeriodKind,
        string PeriodStartLocalDate);
}
