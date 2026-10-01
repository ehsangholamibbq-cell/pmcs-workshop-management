using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using UglyToad.PdfPig;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid PortfolioSummaryRunId =
        Guid.Parse("7f000000-0000-4000-8000-000000000001");
    private const string PortfolioSummaryDefinitionCode = "portfolio-summary-certified";

    private static async Task<int> VerifyReportingPortfolioSummaryAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var actor = Actor("qa-super-admin");
        var assertions = new List<VerificationAssertion>();
        const string path = "/api/v1/portfolio/reports";
        var request = new PortfolioSummaryRunRequest(PortfolioSummaryRunId,
            PortfolioSummaryDefinitionCode, "1.0.0", null, ["Pdf", "Xlsx"], new { });

        var catalog = await SendAsync(client, key, actor, HttpMethod.Get, $"{path}/catalog");
        var definition = FindCatalogDefinition(catalog.Payload, PortfolioSummaryDefinitionCode);
        Record(assertions, "reporting.portfolio.catalog.tenant-scope",
            catalog.StatusCode == HttpStatusCode.OK && definition.HasValue &&
            HasString(definition.Value, "scope", "Portfolio") &&
            HasString(definition.Value, "classification", "Confidential"),
            $"http={(int)catalog.StatusCode};found={definition.HasValue}");

        var created = await SendAsync(client, key, actor, HttpMethod.Post,
            $"{path}/runs", request, "qa-rpt1-portfolio-summary-create");
        Record(assertions, "reporting.portfolio.create.accepted",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", PortfolioSummaryRunId),
            $"http={(int)created.StatusCode};code={ReadOptionalString(created.Payload, "code")}");
        if (created.StatusCode == HttpStatusCode.Accepted)
        {
            var replay = await SendAsync(client, key, actor, HttpMethod.Post,
                $"{path}/runs", request, "qa-rpt1-portfolio-summary-create");
            Record(assertions, "reporting.portfolio.create.idempotent",
                replay.StatusCode == HttpStatusCode.Accepted &&
                HasGuid(replay.Payload, "id", PortfolioSummaryRunId),
                $"http={(int)replay.StatusCode}");

            var completed = await SendAsync(client, key, actor, HttpMethod.Get,
                $"{path}/runs/{PortfolioSummaryRunId}");
            for (var attempt = 0; attempt < 60 &&
                 !HasString(completed.Payload, "status", "Succeeded") &&
                 !HasString(completed.Payload, "status", "Failed") &&
                 !HasString(completed.Payload, "status", "Cancelled"); attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
                completed = await SendAsync(client, key, actor, HttpMethod.Get,
                    $"{path}/runs/{PortfolioSummaryRunId}");
            }
            var outputs = completed.Payload.ValueKind == JsonValueKind.Object &&
                completed.Payload.TryGetProperty("outputs", out var array) &&
                array.ValueKind == JsonValueKind.Array
                    ? array.GetArrayLength() : 0;
            Record(assertions, "reporting.portfolio.worker.tenant-artifacts",
                completed.StatusCode == HttpStatusCode.OK &&
                HasString(completed.Payload, "status", "Succeeded") &&
                HasString(completed.Payload, "pipelineStage", "Complete") &&
                outputs == 2 &&
                TryFindOutput(completed.Payload, "Pdf", out var pdf) &&
                HasString(pdf, "contentType", "application/pdf") &&
                TryFindOutput(completed.Payload, "Xlsx", out var xlsx) &&
                HasString(xlsx, "contentType",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
                $"http={(int)completed.StatusCode};" +
                $"status={ReadOptionalString(completed.Payload, "status")};" +
                $"stage={ReadOptionalString(completed.Payload, "pipelineStage")};" +
                $"attempts={ReadInt64(completed.Payload, "attemptCount")};" +
                $"diagnostic={ReadOptionalString(completed.Payload, "diagnosticCode")};" +
                $"outputs={outputs}");
            if (HasString(completed.Payload, "status", "Succeeded"))
            {
                await VerifyPortfolioOutputAsync(client, key, actor, path,
                    completed.Payload, "Pdf", assertions);
                await VerifyPortfolioOutputAsync(client, key, actor, path,
                    completed.Payload, "Xlsx", assertions);
            }
        }

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-portfolio-summary-worker-connected-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            runId = PortfolioSummaryRunId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task VerifyPortfolioOutputAsync(HttpClient client,
        string key, PmcsTestActor actor, string path, JsonElement run,
        string format, List<VerificationAssertion> assertions)
    {
        var outputId = Guid.Empty;
        var validMetadata = TryFindOutput(run, format, out var output) &&
            TryReadGuid(output, "id", out outputId) &&
            TryReadString(output, "sha256", out var expectedHash) &&
            TryReadString(output, "verificationCode", out var verificationCode) &&
            TryReadString(output, "fileName", out var fileName) &&
            expectedHash is { Length: 64 } &&
            !string.IsNullOrWhiteSpace(verificationCode) &&
            fileName!.StartsWith("portfolio-summary-", StringComparison.Ordinal);
        Record(assertions, $"reporting.portfolio.{format}.metadata",
            validMetadata, $"outputId={outputId}");
        if (!validMetadata) return;

        var expectedSha256 = output.GetProperty("sha256").GetString()!;
        var code = output.GetProperty("verificationCode").GetString()!;
        var result = await DownloadReportingOutputAsync(client, key, actor,
            $"{path}/outputs/{outputId}/content");
        var sha = Convert.ToHexString(SHA256.HashData(result.Bytes)).ToLowerInvariant();
        var payloadValid = format == "Pdf"
            ? ValidPortfolioPdf(result.Bytes, code)
            : ValidPortfolioWorkbook(result.Bytes, code);
        Record(assertions, $"reporting.portfolio.{format}.download.integrity",
            result.StatusCode == HttpStatusCode.OK &&
            result.ContentType == (format == "Pdf" ? "application/pdf" :
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") &&
            result.NoStore && result.NoSniff && result.IsAttachment &&
            result.ETag == $"\"sha256-{expectedSha256}\"" &&
            sha == expectedSha256 && payloadValid,
            $"http={(int)result.StatusCode};sha256={sha};bytes={result.Bytes.Length}");

        var verified = await SendAsync(client, key, actor, HttpMethod.Get,
            $"{path}/outputs/{outputId}/verify");
        Record(assertions, $"reporting.portfolio.{format}.verify.valid",
            verified.StatusCode == HttpStatusCode.OK &&
            HasString(verified.Payload, "status", "Valid") &&
            HasString(verified.Payload, "definitionCode", PortfolioSummaryDefinitionCode) &&
            HasString(verified.Payload, "sha256", expectedSha256) &&
            HasString(verified.Payload, "verificationCode", code),
            $"http={(int)verified.StatusCode}");

        var projectRoute = await SendAsync(client, key, actor, HttpMethod.Get,
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports/outputs/{outputId}/verify");
        Record(assertions, $"reporting.portfolio.{format}.project-route-isolated",
            projectRoute.StatusCode == HttpStatusCode.NotFound,
            $"http={(int)projectRoute.StatusCode}");
    }

    private static bool ValidPortfolioPdf(byte[] bytes, string code)
    {
        if (!bytes.AsSpan().StartsWith("%PDF-"u8)) return false;
        using var document = PdfDocument.Open(bytes);
        return document.NumberOfPages >= 3 &&
            string.Join('\n', document.GetPages().Select(page => page.Text))
                .Contains(code, StringComparison.Ordinal);
    }

    private static bool ValidPortfolioWorkbook(byte[] bytes, string code)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes, writable: false),
                ZipArchiveMode.Read);
            return archive.GetEntry("xl/workbook.xml") is not null &&
                archive.Entries.Count(entry => entry.FullName.StartsWith(
                    "xl/worksheets/sheet", StringComparison.Ordinal)) == 6 &&
                archive.Entries.Any(entry =>
                {
                    if (!entry.FullName.StartsWith("xl/worksheets/sheet",
                            StringComparison.Ordinal)) return false;
                    using var reader = new StreamReader(entry.Open());
                    return reader.ReadToEnd().Contains(code, StringComparison.Ordinal);
                });
        }
        catch (InvalidDataException) { return false; }
    }

    private sealed record PortfolioSummaryRunRequest(Guid ClientGeneratedId,
        string DefinitionCode, string TemplateVersion, DateTimeOffset? AsOfUtc,
        string[] Formats, object Parameters);
}
