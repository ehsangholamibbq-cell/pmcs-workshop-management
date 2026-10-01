using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid ReportingPoisonRunId =
        Guid.Parse("72000000-0000-4000-8000-000000000001");

    private static readonly Guid[] ReportingHealthyCapacityRunIds = Enumerable.Range(101, 20)
        .Select(value => Guid.Parse($"72000000-0000-4000-8000-{value:000000000000}"))
        .ToArray();

    private static async Task<int> PrepareReportingCapacityAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var basePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";
        var fixtures = new[] { ReportingPoisonRunId }.Concat(ReportingHealthyCapacityRunIds);

        foreach (var runId in fixtures)
        {
            var created = await SendAsync(
                client,
                key,
                technicalOffice,
                HttpMethod.Post,
                $"{basePath}/runs",
                CreateReportingRequest(runId, formats: ["Xlsx"], includeRevisionChain: true),
                $"qa-rpt1-capacity-{runId:N}");
            Record(
                assertions,
                $"reporting.capacity.{runId:N}.queued",
                created.StatusCode == HttpStatusCode.Accepted &&
                HasGuid(created.Payload, "id", runId) &&
                HasString(created.Payload, "status", "Queued"),
                $"http={(int)created.StatusCode};status={ReadOptionalString(created.Payload, "status")}");
        }

        return WriteReportingCapacityResult(
            "rpt1-capacity-fixture-preparation",
            assertions,
            healthyP95Seconds: null);
    }

    private static async Task<int> VerifyReportingCapacityAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var basePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";

        var poison = await WaitForFinalRunAsync(
            client,
            key,
            technicalOffice,
            basePath,
            ReportingPoisonRunId);
        Record(
            assertions,
            "reporting.capacity.poison-isolated-after-bounded-retries",
            poison.StatusCode == HttpStatusCode.OK &&
            HasString(poison.Payload, "status", "Failed") &&
            HasString(poison.Payload, "pipelineStage", "Failed") &&
            HasString(poison.Payload, "diagnosticCode", "reporting.qa.transient_injected") &&
            ReadInt64(poison.Payload, "attemptCount") == 3 &&
            HasNoOutputs(poison.Payload),
            $"http={(int)poison.StatusCode};status={ReadOptionalString(poison.Payload, "status")};" +
            $"attempt={ReadInt64(poison.Payload, "attemptCount")};" +
            $"diagnostic={ReadOptionalString(poison.Payload, "diagnosticCode")}");

        var durations = new List<double>(ReportingHealthyCapacityRunIds.Length);
        foreach (var runId in ReportingHealthyCapacityRunIds)
        {
            var result = await WaitForFinalRunAsync(client, key, technicalOffice, basePath, runId);
            var duration = TryReadInstant(result.Payload, "createdAt", out var createdAt) &&
                TryReadInstant(result.Payload, "completedAt", out var completedAt)
                    ? (completedAt - createdAt).TotalSeconds
                    : double.PositiveInfinity;
            durations.Add(duration);
            Record(
                assertions,
                $"reporting.capacity.{runId:N}.completed-once",
                result.StatusCode == HttpStatusCode.OK &&
                HasString(result.Payload, "status", "Succeeded") &&
                HasString(result.Payload, "pipelineStage", "Complete") &&
                ReadInt64(result.Payload, "attemptCount") == 1 &&
                HasSingleOutput(result.Payload),
                $"http={(int)result.StatusCode};status={ReadOptionalString(result.Payload, "status")};" +
                $"attempt={ReadInt64(result.Payload, "attemptCount")};durationSeconds={duration:F3}");
        }

        durations.Sort();
        var p95Index = Math.Max(0, (int)Math.Ceiling(durations.Count * 0.95) - 1);
        var p95Seconds = durations[p95Index];
        Record(
            assertions,
            "reporting.capacity.healthy-p95-under-30-seconds",
            durations.Count == 20 && p95Seconds < 30,
            $"runs={durations.Count};p95Seconds={p95Seconds:F3};budgetSeconds=30");

        return WriteReportingCapacityResult(
            "rpt1-worker-capacity-verification",
            assertions,
            p95Seconds);
    }

    private static int WriteReportingCapacityResult(
        string stage,
        IReadOnlyCollection<VerificationAssertion> assertions,
        double? healthyP95Seconds)
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
            poisonRunId = ReportingPoisonRunId,
            healthyRunIds = ReportingHealthyCapacityRunIds,
            healthyP95Seconds,
            healthyP95BudgetSeconds = 30,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static bool TryReadInstant(JsonElement payload, string propertyName, out DateTimeOffset value)
    {
        value = default;
        return payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(property.GetString(), out value);
    }

    private static bool HasNoOutputs(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("outputs", out var outputs) &&
        outputs.ValueKind == JsonValueKind.Array &&
        outputs.GetArrayLength() == 0;
}
