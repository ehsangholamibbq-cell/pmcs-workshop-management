using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid ReportingConcurrencyLockedRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000004");
    private static readonly Guid ReportingConcurrencySkippedRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000005");
    private static readonly Guid ReportingCrashBeforeStorageRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000006");
    private static readonly Guid ReportingCrashAfterStorageRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000007");
    private static readonly Guid ReportingStaleLeaseRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000008");

    private static readonly ReportingRecoveryRun[] ReportingRecoveryRuns =
    [
        new(ReportingConcurrencyLockedRunId, "concurrency-locked", 1),
        new(ReportingConcurrencySkippedRunId, "concurrency-skipped", 1),
        new(ReportingCrashBeforeStorageRunId, "crash-before-storage", 2),
        new(ReportingCrashAfterStorageRunId, "crash-after-storage", 2),
        new(ReportingStaleLeaseRunId, "stale-lease", 2)
    ];

    private static async Task<int> PrepareReportingRecoveryAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var basePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";

        foreach (var fixture in ReportingRecoveryRuns)
        {
            var created = await SendAsync(
                client,
                key,
                technicalOffice,
                HttpMethod.Post,
                $"{basePath}/runs",
                CreateReportingRequest(
                    fixture.RunId,
                    formats: ["Xlsx"],
                    includeRevisionChain: true),
                $"qa-rpt1-recovery-{fixture.Code}");
            Record(
                assertions,
                $"reporting.recovery.{fixture.Code}.queued",
                created.StatusCode == HttpStatusCode.Accepted &&
                HasGuid(created.Payload, "id", fixture.RunId) &&
                HasString(created.Payload, "status", "Queued") &&
                HasString(created.Payload, "pipelineStage", "Queued"),
                $"http={(int)created.StatusCode};status={ReadOptionalString(created.Payload, "status")}");
        }

        return WriteReportingRecoveryResult("rpt1-recovery-fixture-preparation", assertions);
    }

    private static async Task<int> VerifyReportingRecoveryAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var basePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";

        foreach (var fixture in ReportingRecoveryRuns)
        {
            var result = await SendAsync(
                client,
                key,
                technicalOffice,
                HttpMethod.Get,
                $"{basePath}/runs/{fixture.RunId}");
            Record(
                assertions,
                $"reporting.recovery.{fixture.Code}.completed-once",
                result.StatusCode == HttpStatusCode.OK &&
                HasGuid(result.Payload, "id", fixture.RunId) &&
                HasString(result.Payload, "status", "Succeeded") &&
                HasString(result.Payload, "pipelineStage", "Complete") &&
                ReadInt64(result.Payload, "attemptCount") == fixture.ExpectedAttempts &&
                HasSingleOutput(result.Payload),
                $"http={(int)result.StatusCode};status={ReadOptionalString(result.Payload, "status")};" +
                $"attempt={ReadInt64(result.Payload, "attemptCount")}");
        }

        return WriteReportingRecoveryResult("rpt1-worker-recovery-verification", assertions);
    }

    private static int WriteReportingRecoveryResult(
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
            runIds = ReportingRecoveryRuns.Select(item => item.RunId).ToArray(),
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static bool HasSingleOutput(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("outputs", out var outputs) &&
        outputs.ValueKind == JsonValueKind.Array &&
        outputs.GetArrayLength() == 1;

    private sealed record ReportingRecoveryRun(Guid RunId, string Code, long ExpectedAttempts);
}
