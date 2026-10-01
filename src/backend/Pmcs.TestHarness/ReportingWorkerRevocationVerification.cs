using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid ReportingWorkerRevocationRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000009");

    private static async Task<int> PrepareReportingWorkerRevocationAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var basePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";

        var created = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs",
            CreateReportingRequest(
                ReportingWorkerRevocationRunId,
                formats: ["Xlsx"],
                includeRevisionChain: true),
            "qa-rpt1-worker-revocation");
        Record(
            assertions,
            "reporting.worker-revocation.queued",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", ReportingWorkerRevocationRunId) &&
            HasString(created.Payload, "status", "Queued") &&
            HasString(created.Payload, "pipelineStage", "Queued"),
            $"http={(int)created.StatusCode};status={ReadOptionalString(created.Payload, "status")}");

        return WriteReportingWorkerRevocationResult(
            "rpt1-worker-revocation-fixture-preparation",
            assertions);
    }

    private static async Task<int> VerifyReportingWorkerRevocationAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var path = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports/runs/" +
            ReportingWorkerRevocationRunId;

        var result = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Get,
            path);
        var hasNoOutputs = result.Payload.ValueKind == JsonValueKind.Object &&
            result.Payload.TryGetProperty("outputs", out var outputs) &&
            outputs.ValueKind == JsonValueKind.Array &&
            outputs.GetArrayLength() == 0;
        Record(
            assertions,
            "reporting.worker-revocation.failed-before-storage",
            result.StatusCode == HttpStatusCode.OK &&
            HasGuid(result.Payload, "id", ReportingWorkerRevocationRunId) &&
            HasString(result.Payload, "status", "Failed") &&
            HasString(result.Payload, "pipelineStage", "Failed") &&
            HasString(result.Payload, "diagnosticCode", "reporting.permission.revoked") &&
            ReadInt64(result.Payload, "attemptCount") == 1 &&
            hasNoOutputs,
            $"http={(int)result.StatusCode};status={ReadOptionalString(result.Payload, "status")};" +
            $"diagnostic={ReadOptionalString(result.Payload, "diagnosticCode")};" +
            $"attempt={ReadInt64(result.Payload, "attemptCount")}");

        return WriteReportingWorkerRevocationResult(
            "rpt1-worker-revocation-verification",
            assertions);
    }

    private static int WriteReportingWorkerRevocationResult(
        string stage,
        IReadOnlyCollection<VerificationAssertion> assertions)
    {
        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage,
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            runId = ReportingWorkerRevocationRunId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
