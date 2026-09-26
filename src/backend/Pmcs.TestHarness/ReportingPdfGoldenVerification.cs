using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using UglyToad.PdfPig;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private const int GoldenPdfMaximumBytes = 5 * 1024 * 1024;
    private static readonly Guid GoldenPdfRunId =
        Guid.Parse("76000000-0000-4000-8000-000000000001");

    private static async Task<int> VerifyReportingPdfGoldenAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var reportingPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";
        var request = CreateReportingRequest(
            GoldenPdfRunId,
            formats: ["Pdf"],
            includeRevisionChain: true) with
        {
            Parameters = new ReportingParameters(GoldenV1ReportId, true)
        };

        var timer = Stopwatch.StartNew();
        var created = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-pdf-golden-create");
        Record(
            assertions,
            "reporting.pdf.golden.create.accepted",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", GoldenPdfRunId),
            $"http={(int)created.StatusCode}");

        var replay = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportingPath}/runs",
            request,
            "qa-rpt1-pdf-golden-create");
        Record(
            assertions,
            "reporting.pdf.golden.idempotent-replay",
            replay.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(replay.Payload, "id", GoldenPdfRunId),
            $"http={(int)replay.StatusCode}");

        var succeeded = await WaitForFinalRunAsync(
            client,
            key,
            technicalOffice,
            reportingPath,
            GoldenPdfRunId);
        timer.Stop();
        JsonElement output = default;
        var outputId = Guid.Empty;
        string? outputSha256 = null;
        string? verificationCode = null;
        var succeededContract = succeeded.StatusCode == HttpStatusCode.OK &&
            HasString(succeeded.Payload, "status", "Succeeded") &&
            HasString(succeeded.Payload, "pipelineStage", "Complete") &&
            HasString(succeeded.Payload, "dataStatus", "Available") &&
            TryFindOutput(succeeded.Payload, "Pdf", out output) &&
            TryReadGuid(output, "id", out outputId) &&
            TryReadString(output, "sha256", out outputSha256) &&
            TryReadString(output, "verificationCode", out verificationCode);
        Record(
            assertions,
            "reporting.pdf.golden.worker.succeeded",
            succeededContract,
            $"http={(int)succeeded.StatusCode};elapsedMs={timer.Elapsed.TotalMilliseconds:F1}");

        var pageCount = 0;
        var extractedText = string.Empty;
        var downloadedBytes = 0;
        if (succeededContract)
        {
            var download = await DownloadReportingOutputAsync(
                client,
                key,
                technicalOffice,
                $"{reportingPath}/outputs/{outputId}/content");
            downloadedBytes = download.Bytes.Length;
            var actualSha256 = Convert.ToHexString(SHA256.HashData(download.Bytes)).ToLowerInvariant();
            Record(
                assertions,
                "reporting.pdf.golden.download.integrity",
                download.StatusCode == HttpStatusCode.OK &&
                string.Equals(download.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(actualSha256, outputSha256, StringComparison.Ordinal) &&
                string.Equals(download.ETag, $"\"sha256-{outputSha256}\"", StringComparison.Ordinal) &&
                download.NoStore && download.NoSniff && download.IsAttachment &&
                download.Bytes.AsSpan().StartsWith("%PDF-"u8) &&
                download.Bytes.Length is > 0 and <= GoldenPdfMaximumBytes,
                $"http={(int)download.StatusCode};sha256={actualSha256};bytes={download.Bytes.Length}");

            var repeated = await DownloadReportingOutputAsync(
                client,
                key,
                technicalOffice,
                $"{reportingPath}/outputs/{outputId}/content");
            Record(
                assertions,
                "reporting.pdf.golden.stored-bytes-deterministic",
                repeated.StatusCode == HttpStatusCode.OK &&
                download.Bytes.SequenceEqual(repeated.Bytes) &&
                string.Equals(
                    actualSha256,
                    Convert.ToHexString(SHA256.HashData(repeated.Bytes)).ToLowerInvariant(),
                    StringComparison.Ordinal),
                $"sha256={actualSha256};bytes={download.Bytes.Length}");

            using var document = PdfDocument.Open(download.Bytes);
            var pages = document.GetPages().ToArray();
            pageCount = pages.Length;
            extractedText = string.Join('\n', pages.Select(page => page.Text));
            Record(
                assertions,
                "reporting.pdf.golden.structure-and-text",
                pageCount >= 1 &&
                extractedText.Contains("PMCS", StringComparison.Ordinal) &&
                extractedText.Contains("DEMO-01", StringComparison.Ordinal) &&
                extractedText.Contains(verificationCode!, StringComparison.Ordinal) &&
                extractedText.Any(character => character is >= '\u0600' and <= '\u06ff'),
                $"pages={pageCount};textLength={extractedText.Length}");
            Record(
                assertions,
                "reporting.pdf.golden.draft-excluded",
                !extractedText.Contains(GoldenDraftMarker, StringComparison.Ordinal),
                $"draftMarkerPresent={extractedText.Contains(GoldenDraftMarker, StringComparison.Ordinal)}");
            Record(
                assertions,
                "reporting.pdf.golden.end-to-end-budget",
                timer.Elapsed <= TimeSpan.FromSeconds(15),
                $"elapsedMs={timer.Elapsed.TotalMilliseconds:F1};budgetMs=15000");
        }

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-pdf-golden-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            rootReportId = GoldenV1ReportId,
            runId = GoldenPdfRunId,
            pageCount,
            outputBytes = downloadedBytes,
            elapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
