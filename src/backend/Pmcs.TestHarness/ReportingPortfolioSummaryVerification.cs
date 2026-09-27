using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

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

    private sealed record PortfolioSummaryRunRequest(Guid ClientGeneratedId,
        string DefinitionCode, string TemplateVersion, DateTimeOffset? AsOfUtc,
        string[] Formats, object Parameters);
}
