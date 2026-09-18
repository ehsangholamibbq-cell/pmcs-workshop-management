using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid ReportingCancelledRunId =
        Guid.Parse("71000000-0000-4000-8000-000000000003");

    private static async Task<int> VerifyReportingCancellationAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var technicalOffice = Actor("technical-office");
        var basePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports";
        var request = CreateReportingRequest(
            ReportingCancelledRunId,
            formats: ["Xlsx"],
            includeRevisionChain: true);

        var created = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs",
            request,
            "qa-rpt1-cancel-create");
        Record(
            assertions,
            "reporting.cancel.queued-with-worker-disabled",
            created.StatusCode == HttpStatusCode.Accepted &&
            HasGuid(created.Payload, "id", ReportingCancelledRunId) &&
            HasString(created.Payload, "status", "Queued") &&
            HasString(created.Payload, "pipelineStage", "Queued"),
            $"http={(int)created.StatusCode};status={ReadOptionalString(created.Payload, "status")}");

        var cancelled = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs/{ReportingCancelledRunId}/cancel",
            payload: null,
            idempotencyKey: "qa-rpt1-cancel");
        Record(
            assertions,
            "reporting.cancel.accepted-before-rendering",
            cancelled.StatusCode == HttpStatusCode.OK &&
            HasString(cancelled.Payload, "status", "Cancelled") &&
            HasString(cancelled.Payload, "pipelineStage", "Cancelled"),
            $"http={(int)cancelled.StatusCode};status={ReadOptionalString(cancelled.Payload, "status")}");

        var replay = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs/{ReportingCancelledRunId}/cancel",
            payload: null,
            idempotencyKey: "qa-rpt1-cancel");
        Record(
            assertions,
            "reporting.cancel.idempotent-replay",
            replay.StatusCode == HttpStatusCode.OK &&
            HasString(replay.Payload, "status", "Cancelled"),
            $"http={(int)replay.StatusCode};status={ReadOptionalString(replay.Payload, "status")}");

        var secondCancel = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs/{ReportingCancelledRunId}/cancel",
            payload: null,
            idempotencyKey: "qa-rpt1-cancel-after-final");
        Record(
            assertions,
            "reporting.cancel.final-state-rejected",
            secondCancel.StatusCode == HttpStatusCode.Conflict &&
            HasString(secondCancel.Payload, "code", "reporting.run.already_final"),
            $"http={(int)secondCancel.StatusCode};code={ReadOptionalString(secondCancel.Payload, "code")}");

        var retry = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{basePath}/runs/{ReportingCancelledRunId}/retry",
            payload: null,
            idempotencyKey: "qa-rpt1-retry-cancelled");
        Record(
            assertions,
            "reporting.cancel.retry-rejected",
            retry.StatusCode == HttpStatusCode.Conflict &&
            HasString(retry.Payload, "code", "reporting.run.not_retryable"),
            $"http={(int)retry.StatusCode};code={ReadOptionalString(retry.Payload, "code")}");

        var current = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Get,
            $"{basePath}/runs/{ReportingCancelledRunId}");
        var noOutputs = current.Payload.ValueKind == JsonValueKind.Object &&
            current.Payload.TryGetProperty("outputs", out var outputs) &&
            outputs.ValueKind == JsonValueKind.Array &&
            outputs.GetArrayLength() == 0;
        Record(
            assertions,
            "reporting.cancel.remains-final-without-output",
            current.StatusCode == HttpStatusCode.OK &&
            HasString(current.Payload, "status", "Cancelled") &&
            ReadInt64(current.Payload, "attemptCount") == 0 &&
            noOutputs,
            $"http={(int)current.StatusCode};status={ReadOptionalString(current.Payload, "status")}");

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-connected-cancellation-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            cancelledRunId = ReportingCancelledRunId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
