using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid PortfolioLicenseRunId =
        Guid.Parse("7f000000-0000-4000-8000-000000000002");
    private static readonly Guid PortfolioCancelledRunId =
        Guid.Parse("7f000000-0000-4000-8000-000000000003");
    private const string PortfolioPath = "/api/v1/portfolio/reports";

    private static PortfolioSummaryRunRequest LifecycleRequest(Guid runId) => new(
        runId, PortfolioSummaryDefinitionCode, "1.0.0", null, ["Pdf"], new { });

    private static async Task<int> PrepareReportingPortfolioRetryAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var actor = Actor("qa-super-admin");
        var assertions = new List<VerificationAssertion>();
        var created = await SendAsync(client, key, actor, HttpMethod.Post,
            $"{PortfolioPath}/runs", LifecycleRequest(PortfolioLicenseRunId),
            "qa-rpt1-portfolio-license-create");
        Record(assertions, "reporting.portfolio.license.create",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", PortfolioLicenseRunId),
            $"http={(int)created.StatusCode}");
        if (created.StatusCode == HttpStatusCode.Accepted)
        {
            var failed = await WaitForFinalRunAsync(client, key, actor,
                PortfolioPath, PortfolioLicenseRunId);
            Record(assertions, "reporting.portfolio.license.unconfigured-fails",
                failed.StatusCode == HttpStatusCode.OK &&
                HasString(failed.Payload, "status", "Failed") &&
                HasString(failed.Payload, "diagnosticCode",
                    "reporting.renderer.license_unconfigured") &&
                ReadInt64(failed.Payload, "attemptCount") == 1,
                $"http={(int)failed.StatusCode};" +
                $"status={ReadOptionalString(failed.Payload, "status")};" +
                $"diagnostic={ReadOptionalString(failed.Payload, "diagnosticCode")}");
        }
        return PrintPortfolioAssertions("portfolio-license-failure", assertions,
            PortfolioLicenseRunId);
    }

    private static async Task<int> VerifyReportingPortfolioRetryAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var actor = Actor("qa-super-admin");
        var assertions = new List<VerificationAssertion>();
        var retried = await SendAsync(client, key, actor, HttpMethod.Post,
            $"{PortfolioPath}/runs/{PortfolioLicenseRunId}/retry", new { },
            "qa-rpt1-portfolio-license-retry");
        Record(assertions, "reporting.portfolio.retry.accepted",
            retried.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(retried.Payload, "id", PortfolioLicenseRunId),
            $"http={(int)retried.StatusCode};code={ReadOptionalString(retried.Payload, "code")}");
        if (retried.StatusCode == HttpStatusCode.Accepted)
        {
            var replay = await SendAsync(client, key, actor, HttpMethod.Post,
                $"{PortfolioPath}/runs/{PortfolioLicenseRunId}/retry", new { },
                "qa-rpt1-portfolio-license-retry");
            Record(assertions, "reporting.portfolio.retry.idempotent",
                replay.StatusCode == HttpStatusCode.Accepted &&
                HasGuid(replay.Payload, "id", PortfolioLicenseRunId),
                $"http={(int)replay.StatusCode}");
            var succeeded = await WaitForFinalRunAsync(client, key, actor,
                PortfolioPath, PortfolioLicenseRunId);
            Record(assertions, "reporting.portfolio.retry.snapshot-preserved",
                succeeded.StatusCode == HttpStatusCode.OK &&
                HasString(succeeded.Payload, "status", "Succeeded") &&
                ReadInt64(succeeded.Payload, "attemptCount") == 2 &&
                TryFindOutput(succeeded.Payload, "Pdf", out _),
                $"http={(int)succeeded.StatusCode};" +
                $"status={ReadOptionalString(succeeded.Payload, "status")};" +
                $"diagnostic={ReadOptionalString(succeeded.Payload, "diagnosticCode")}");
        }
        return PrintPortfolioAssertions("portfolio-explicit-retry", assertions,
            PortfolioLicenseRunId);
    }

    private static async Task<int> VerifyReportingPortfolioCancellationAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var actor = Actor("qa-super-admin");
        var assertions = new List<VerificationAssertion>();
        var created = await SendAsync(client, key, actor, HttpMethod.Post,
            $"{PortfolioPath}/runs", LifecycleRequest(PortfolioCancelledRunId),
            "qa-rpt1-portfolio-cancel-create");
        Record(assertions, "reporting.portfolio.cancel.queued",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasString(created.Payload, "status", "Queued"),
            $"http={(int)created.StatusCode}");
        if (created.StatusCode == HttpStatusCode.Accepted)
        {
            var cancelled = await SendAsync(client, key, actor, HttpMethod.Post,
                $"{PortfolioPath}/runs/{PortfolioCancelledRunId}/cancel", new { },
                "qa-rpt1-portfolio-cancel");
            Record(assertions, "reporting.portfolio.cancel.final",
                cancelled.StatusCode == HttpStatusCode.OK &&
                HasString(cancelled.Payload, "status", "Cancelled") &&
                HasString(cancelled.Payload, "pipelineStage", "Cancelled"),
                $"http={(int)cancelled.StatusCode};" +
                $"status={ReadOptionalString(cancelled.Payload, "status")}");
            var replay = await SendAsync(client, key, actor, HttpMethod.Post,
                $"{PortfolioPath}/runs/{PortfolioCancelledRunId}/cancel", new { },
                "qa-rpt1-portfolio-cancel");
            Record(assertions, "reporting.portfolio.cancel.idempotent",
                replay.StatusCode == HttpStatusCode.OK &&
                HasString(replay.Payload, "status", "Cancelled"),
                $"http={(int)replay.StatusCode}");
            var retry = await SendAsync(client, key, actor, HttpMethod.Post,
                $"{PortfolioPath}/runs/{PortfolioCancelledRunId}/retry", new { },
                "qa-rpt1-portfolio-cancel-retry-denied");
            Record(assertions, "reporting.portfolio.cancel.not-retryable",
                retry.StatusCode == HttpStatusCode.Conflict &&
                HasString(retry.Payload, "code", "reporting.run.not_retryable"),
                $"http={(int)retry.StatusCode}");
        }
        return PrintPortfolioAssertions("portfolio-cancellation", assertions,
            PortfolioCancelledRunId);
    }

    private static int PrintPortfolioAssertions(string stage,
        List<VerificationAssertion> assertions, Guid runId)
    {
        var failed = assertions.Count(item => !item.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage, passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed, failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId, runId,
            capturedAt = DateTimeOffset.UtcNow, assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
