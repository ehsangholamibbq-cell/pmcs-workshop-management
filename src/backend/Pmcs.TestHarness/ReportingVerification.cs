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
    private static readonly Guid ReportingSucceededRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000001");
    private static readonly Guid ReportingLicenseFailureRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000002");

    private static async Task<int> VerifyReportingAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var observer = Actor("observer");
        var basePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";

        var catalog = await SendAsync(client, key, observer, HttpMethod.Get, $"{basePath}/catalog");
        Record(
            assertions,
            "reporting.catalog.authorized",
            catalog.StatusCode == HttpStatusCode.OK &&
            ContainsDefinition(catalog.Payload, "daily-report-certified"),
            $"http={(int)catalog.StatusCode}");

        var xlsxRequest = CreateReportingRequest(
            ReportingSucceededRunId,
            formats: ["Xlsx"],
            includeRevisionChain: true);
        var observerCreate = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Post,
            $"{basePath}/runs",
            xlsxRequest with { ClientGeneratedId = Guid.Parse("71000000-0000-4000-8000-000000000099") },
            "qa-rpt1-observer-create-denied");
        Record(
            assertions,
            "reporting.observer.create.denied",
            observerCreate.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)observerCreate.StatusCode}");

        var created = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs",
            xlsxRequest,
            "qa-rpt1-xlsx-create");
        Record(
            assertions,
            "reporting.xlsx.create.accepted",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", ReportingSucceededRunId),
            $"http={(int)created.StatusCode}");

        var replay = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs",
            xlsxRequest,
            "qa-rpt1-xlsx-create");
        Record(
            assertions,
            "reporting.xlsx.create.idempotent-replay",
            replay.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(replay.Payload, "id", ReportingSucceededRunId),
            $"http={(int)replay.StatusCode}");

        var conflictingReplay = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs",
            xlsxRequest with
            {
                Parameters = xlsxRequest.Parameters with { IncludeRevisionChain = false }
            },
            "qa-rpt1-xlsx-create");
        Record(
            assertions,
            "reporting.xlsx.create.idempotency-conflict",
            conflictingReplay.StatusCode == HttpStatusCode.Conflict,
            $"http={(int)conflictingReplay.StatusCode}");

        var succeeded = await WaitForFinalRunAsync(
            client,
            key,
            technicalOffice,
            basePath,
            ReportingSucceededRunId);
        JsonElement xlsxOutput = default;
        var outputId = Guid.Empty;
        string? outputSha256 = null;
        string? verificationCode = null;
        var succeededContract = succeeded.StatusCode == HttpStatusCode.OK &&
            HasString(succeeded.Payload, "status", "Succeeded") &&
            HasString(succeeded.Payload, "pipelineStage", "Complete") &&
            HasString(succeeded.Payload, "dataStatus", "Available") &&
            TryFindOutput(succeeded.Payload, "Xlsx", out xlsxOutput) &&
            TryReadGuid(xlsxOutput, "id", out outputId) &&
            TryReadString(xlsxOutput, "sha256", out outputSha256) &&
            TryReadString(xlsxOutput, "verificationCode", out verificationCode);
        Record(
            assertions,
            "reporting.xlsx.worker.succeeded",
            succeededContract,
            $"http={(int)succeeded.StatusCode};status={ReadOptionalString(succeeded.Payload, "status")}");

        if (succeededContract)
        {
            var download = await DownloadReportingOutputAsync(
                client,
                key,
                technicalOffice,
                $"{basePath}/outputs/{outputId}/content");
            var actualSha256 = Convert.ToHexString(SHA256.HashData(download.Bytes)).ToLowerInvariant();
            var workbookValid = ValidateWorkbook(
                download.Bytes,
                outputId,
                verificationCode!,
                out var workbookDetail);
            Record(
                assertions,
                "reporting.xlsx.download.integrity",
                download.StatusCode == HttpStatusCode.OK &&
                string.Equals(
                    download.ContentType,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actualSha256, outputSha256, StringComparison.Ordinal) &&
                string.Equals(download.ETag, $"\"sha256-{outputSha256}\"", StringComparison.Ordinal) &&
                download.NoStore && download.NoSniff && download.IsAttachment && workbookValid,
                $"http={(int)download.StatusCode};workbook={workbookDetail}");

            var verified = await SendAsync(
                client,
                key,
                technicalOffice,
                HttpMethod.Get,
                $"{basePath}/outputs/{outputId}/verify");
            Record(
                assertions,
                "reporting.xlsx.verify.valid",
                verified.StatusCode == HttpStatusCode.OK &&
                HasString(verified.Payload, "status", "Valid") &&
                HasString(verified.Payload, "sha256", outputSha256!) &&
                HasString(verified.Payload, "verificationCode", verificationCode!),
                $"http={(int)verified.StatusCode}");

            var crossProject = await SendAsync(
                client,
                key,
                technicalOffice,
                HttpMethod.Get,
                $"/api/v1/projects/33333333-3333-4333-8333-333333333399/reports/outputs/{outputId}/verify");
            Record(
                assertions,
                "reporting.output.cross-project.denied",
                crossProject.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"http={(int)crossProject.StatusCode}");
        }

        var pdfRequest = CreateReportingRequest(
            ReportingLicenseFailureRunId,
            formats: ["Pdf"],
            includeRevisionChain: true);
        var pdfCreated = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs",
            pdfRequest,
            "qa-rpt1-pdf-license-create");
        Record(
            assertions,
            "reporting.pdf.unconfigured.create.accepted",
            pdfCreated.StatusCode == HttpStatusCode.Accepted,
            $"http={(int)pdfCreated.StatusCode}");

        var firstPdfFailure = await WaitForFinalRunAsync(
            client,
            key,
            technicalOffice,
            basePath,
            ReportingLicenseFailureRunId);
        Record(
            assertions,
            "reporting.pdf.license.fail-closed",
            HasString(firstPdfFailure.Payload, "status", "Failed") &&
            HasString(firstPdfFailure.Payload, "diagnosticCode", "reporting.renderer.license_unconfigured") &&
            ReadInt64(firstPdfFailure.Payload, "attemptCount") == 1,
            $"http={(int)firstPdfFailure.StatusCode};diagnostic={ReadOptionalString(firstPdfFailure.Payload, "diagnosticCode")}");

        var retry = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs/{ReportingLicenseFailureRunId}/retry",
            payload: null,
            idempotencyKey: "qa-rpt1-pdf-license-retry");
        Record(
            assertions,
            "reporting.pdf.retry.accepted",
            retry.StatusCode == HttpStatusCode.Accepted,
            $"http={(int)retry.StatusCode}");

        var secondPdfFailure = await WaitForFinalRunAsync(
            client,
            key,
            technicalOffice,
            basePath,
            ReportingLicenseFailureRunId);
        Record(
            assertions,
            "reporting.pdf.retry.bounded",
            HasString(secondPdfFailure.Payload, "status", "Failed") &&
            HasString(secondPdfFailure.Payload, "diagnosticCode", "reporting.renderer.license_unconfigured") &&
            ReadInt64(secondPdfFailure.Payload, "attemptCount") == 2,
            $"http={(int)secondPdfFailure.StatusCode};attempt={ReadInt64(secondPdfFailure.Payload, "attemptCount")}");

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-connected-api-storage-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            succeededRunId = ReportingSucceededRunId,
            licenseFailureRunId = ReportingLicenseFailureRunId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static ReportingRunRequest CreateReportingRequest(
        Guid clientGeneratedId,
        string[] formats,
        bool includeRevisionChain,
        DateTimeOffset? asOfUtc = null) => new(
        clientGeneratedId,
        "daily-report-certified",
        "1.0.0",
        asOfUtc,
        formats,
        new ReportingParameters(WorkflowReportId, includeRevisionChain));

    private static async Task<HarnessHttpResult> WaitForFinalRunAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string basePath,
        Guid runId)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            var result = await SendAsync(
                client,
                key,
                actor,
                HttpMethod.Get,
                $"{basePath}/runs/{runId}");
            if (result.StatusCode == HttpStatusCode.OK &&
                TryReadString(result.Payload, "status", out var status) &&
                status is "Succeeded" or "Failed" or "Cancelled")
            {
                return result;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new InvalidOperationException($"Reporting run '{runId}' did not reach a final state.");
    }

    private static async Task<ReportingBinaryResult> DownloadReportingOutputAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Pmcs-QA-Key", key);
        request.Headers.Add("X-Tenant-Id", PmcsTestDataSet.TenantId.ToString());
        request.Headers.Add("X-User-Id", actor.UserId.ToString());
        using var response = await client.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var noSniff = response.Headers.TryGetValues("X-Content-Type-Options", out var values) &&
            values.Any(value => string.Equals(value, "nosniff", StringComparison.OrdinalIgnoreCase));
        return new ReportingBinaryResult(
            response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            response.Headers.ETag?.ToString(),
            response.Headers.CacheControl?.NoStore == true,
            noSniff,
            string.Equals(
                response.Content.Headers.ContentDisposition?.DispositionType,
                "attachment",
                StringComparison.OrdinalIgnoreCase),
            bytes);
    }

    private static bool ValidateWorkbook(
        byte[] bytes,
        Guid outputId,
        string verificationCode,
        out string detail)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var names = archive.Entries.Select(entry => entry.FullName).ToArray();
            var metadataXml = ReadWorkbookEntry(archive, "xl/worksheets/sheet1.xml");
            var dataXml = ReadWorkbookEntry(archive, "xl/worksheets/sheet2.xml");
            var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var metadata = XDocument.Parse(metadataXml);
            var data = XDocument.Parse(dataXml);
            var valid = names.Length == 9 &&
                names.All(name => !name.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("externalLinks", StringComparison.OrdinalIgnoreCase)) &&
                metadata.Descendants(spreadsheet + "sheetView").Single()
                    .Attribute("rightToLeft")?.Value == "1" &&
                data.Descendants(spreadsheet + "sheetView").Single()
                    .Attribute("rightToLeft")?.Value == "1" &&
                !metadata.Descendants(spreadsheet + "f").Any() &&
                !data.Descendants(spreadsheet + "f").Any() &&
                metadataXml.Contains(verificationCode, StringComparison.Ordinal) &&
                metadataXml.Contains($"/outputs/{outputId}/verify", StringComparison.Ordinal);
            detail = valid ? "valid" : "invalid workbook contract";
            return valid;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or System.Xml.XmlException)
        {
            detail = exception.GetType().Name;
            return false;
        }
    }

    private static string ReadWorkbookEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name)
            ?? throw new InvalidDataException($"Workbook entry '{name}' is missing.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static bool ContainsDefinition(JsonElement payload, string code) =>
        payload.ValueKind == JsonValueKind.Array && payload.EnumerateArray().Any(item =>
            HasString(item, "code", code));

    private static bool TryFindOutput(JsonElement payload, string format, out JsonElement output)
    {
        output = default;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("outputs", out var outputs) ||
            outputs.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var candidate in outputs.EnumerateArray())
        {
            if (HasString(candidate, "format", format))
            {
                output = candidate.Clone();
                return true;
            }
        }
        return false;
    }

    private static bool TryReadGuid(JsonElement payload, string property, out Guid value)
    {
        value = Guid.Empty;
        return TryReadString(payload, property, out var text) && Guid.TryParse(text, out value);
    }

    private static string ReadOptionalString(JsonElement payload, string property) =>
        TryReadString(payload, property, out var value) ? value! : "<missing>";

    private sealed record ReportingRunRequest(
        Guid ClientGeneratedId,
        string DefinitionCode,
        string TemplateVersion,
        DateTimeOffset? AsOfUtc,
        string[] Formats,
        ReportingParameters Parameters);

    private sealed record ReportingParameters(
        Guid DailyReportId,
        bool IncludeRevisionChain);

    private sealed record ReportingBinaryResult(
        HttpStatusCode StatusCode,
        string? ContentType,
        string? ETag,
        bool NoStore,
        bool NoSniff,
        bool IsAttachment,
        byte[] Bytes);
}
