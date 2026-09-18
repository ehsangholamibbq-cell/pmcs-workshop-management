using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using Pmcs.Modules.QualityAssurance;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly Guid WorkflowReportId =
        Guid.Parse("70000000-0000-4000-8000-000000000001");

    private static readonly Guid WorkflowFactId =
        Guid.Parse("70000000-0000-4000-8000-000000000002");

    private static readonly PermissionExpectation[] PermissionExpectations =
    [
        new("qa-super-admin", "identity.users.manage", true),
        new("site-supervisor", "field.daily-reports.capture", true),
        new("site-supervisor", "field.daily-reports.review", false),
        new("observer", "project-state.read", true),
        new("observer", "field.daily-reports.capture", false),
        new("technical-office", "field.daily-reports.review", true),
        new("technical-office", "finance.records.review", false),
        new("finance-operator", "finance.records.capture", true),
        new("finance-operator", "finance.records.review", false),
        new("finance-operator", "finance.verification.read", false),
        new("finance-manager", "finance.records.review", true),
        new("finance-manager", "finance.verification.read", true),
        new("contract-administrator", "contracts.review", true),
        new("contract-administrator", "supply.inventory.adjust", false),
        new("procurement-operator", "procurement.requests.capture", true),
        new("procurement-operator", "procurement.orders.issue", false),
        new("procurement-manager", "procurement.orders.issue", true),
        new("procurement-manager", "finance.records.review", false),
        new("quality-controller", "quality.actions.verify", true),
        new("quality-controller", "hse.incidents.investigate", false),
        new("hse-officer", "hse.incidents.investigate", true),
        new("hse-officer", "quality.actions.verify", false),
        new("project-controller", "actions.manage", true),
        new("project-controller", "field.daily-reports.review", false)
    ];

    public static async Task<int> Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault()?.ToLowerInvariant() switch
            {
                "guard" => GuardDatabase(),
                "manifest" => WriteManifest(),
                "probe" => await ProbeAsync(),
                "verify" => await VerifyAsync(),
                "verify-files" => await VerifyFilesAsync(),
                "verify-reporting" => await VerifyReportingAsync(),
                "verify-sync" => await VerifySyncAsync(),
                "verify-exploratory" => await VerifyExploratoryAsync(),
                _ => WriteUsage()
            };
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int GuardDatabase()
    {
        var connectionString = ReadRequiredEnvironment("PMCS_QA_CONNECTION_STRING");
        Console.WriteLine(QaDatabaseSafety.RequireIsolatedDatabase(connectionString));
        return 0;
    }

    private static int WriteManifest()
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            rootLocationId = PmcsTestDataSet.RootLocationId,
            actors = PmcsTestDataSet.Actors
        }, JsonOptions));
        return 0;
    }

    private static async Task<int> ProbeAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var result = await SendAsync(
            client,
            key,
            PmcsTestDataSet.QaSuperAdministrator,
            HttpMethod.Get,
            "/api/qa/v1/diagnostics");

        if (result.StatusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException(
                $"QA diagnostics returned HTTP {(int)result.StatusCode}.");
        }

        if (!TryReadString(result.Payload, "overallHealth", out var overallHealth) ||
            !string.Equals(overallHealth, "Healthy", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("QA diagnostics did not report a healthy system.");
        }

        Console.WriteLine(JsonSerializer.Serialize(result.Payload, JsonOptions));
        return 0;
    }

    private static async Task<int> VerifyAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();

        await VerifyPermissionsAsync(client, key, assertions);
        await VerifyDailyReportWorkflowAsync(client, key, assertions);

        var failed = assertions.Count(assertion => !assertion.Passed);
        var report = new
        {
            contractVersion = 1,
            stage = "permission-workflow-api-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            workflowReportId = WorkflowReportId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        };
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task VerifyPermissionsAsync(
        HttpClient client,
        string key,
        List<VerificationAssertion> assertions)
    {
        foreach (var expectation in PermissionExpectations)
        {
            var actor = Actor(expectation.ActorCode);
            var path =
                $"/api/qa/v1/permissions/preview?projectId={PmcsTestDataSet.ProjectId}" +
                $"&userId={actor.UserId}&operation={Uri.EscapeDataString(expectation.Operation)}";
            var result = await SendAsync(
                client,
                key,
                PmcsTestDataSet.QaSuperAdministrator,
                HttpMethod.Get,
                path);

            var decisionValid = TryReadDecision(
                result.Payload,
                expectation.Operation,
                out var actualAllowed);
            var contractValid = result.StatusCode == HttpStatusCode.OK &&
                TryReadString(result.Payload, "accountStatus", out var accountStatus) &&
                string.Equals(accountStatus, "Active", StringComparison.Ordinal) &&
                TryReadString(result.Payload, "projectRole", out var projectRole) &&
                string.Equals(projectRole, actor.ProjectRole, StringComparison.Ordinal) &&
                TryReadString(result.Payload, "policyVersion", out var policyVersion) &&
                string.Equals(policyVersion, "pmcs-rbac-v1", StringComparison.Ordinal) &&
                decisionValid;
            var passed = contractValid && actualAllowed == expectation.Allowed;
            Record(
                assertions,
                $"permission.{expectation.ActorCode}.{expectation.Operation}",
                passed,
                contractValid
                    ? $"expected={expectation.Allowed};actual={actualAllowed}"
                    : $"invalid preview contract;http={(int)result.StatusCode}");
        }
    }

    private static async Task VerifyDailyReportWorkflowAsync(
        HttpClient client,
        string key,
        List<VerificationAssertion> assertions)
    {
        var observer = Actor("observer");
        var siteSupervisor = Actor("site-supervisor");
        var technicalOffice = Actor("technical-office");
        var reportsPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/daily-reports";

        var observerCreate = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Post,
            reportsPath,
            new
            {
                clientGeneratedId = Guid.Parse("70000000-0000-4000-8000-000000000099"),
                reportDate = "2099-12-28",
                locationName = "Denied QA probe",
                narrative = "Observer must not create a report."
            },
            "qa-v2-observer-create-denied");
        Record(
            assertions,
            "workflow.observer.create.denied",
            observerCreate.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)observerCreate.StatusCode}");

        var created = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            reportsPath,
            new
            {
                clientGeneratedId = WorkflowReportId,
                reportDate = "2099-12-29",
                locationName = "QA Root",
                narrative = "Deterministic permission and workflow verification."
            },
            "qa-v2-daily-report-create");
        var createdRevision = ReadInt64(created.Payload, "revision");
        Record(
            assertions,
            "workflow.site-supervisor.create",
            created.StatusCode == HttpStatusCode.Created &&
            HasGuid(created.Payload, "id", WorkflowReportId) &&
            HasString(created.Payload, "status", "Draft") &&
            createdRevision == 1,
            $"http={(int)created.StatusCode};revision={createdRevision}");

        var siteApproval = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{WorkflowReportId}/approve",
            new { baseRevision = createdRevision, comment = "Must be denied." },
            "qa-v2-site-approve-denied");
        Record(
            assertions,
            "workflow.site-supervisor.approve.denied",
            siteApproval.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)siteApproval.StatusCode}");

        var factAdded = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{WorkflowReportId}/facts",
            new
            {
                clientGeneratedId = WorkflowFactId,
                kind = "Note",
                description = "QA deterministic field observation",
                baseRevision = createdRevision,
                locationId = PmcsTestDataSet.RootLocationId
            },
            "qa-v2-daily-report-fact");
        var factRevision = ReadInt64(factAdded.Payload, "revision");
        Record(
            assertions,
            "workflow.site-supervisor.add-fact",
            factAdded.StatusCode == HttpStatusCode.OK &&
            factRevision == 2 &&
            ContainsId(factAdded.Payload, "facts", WorkflowFactId),
            $"http={(int)factAdded.StatusCode};revision={factRevision}");

        var submitted = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{reportsPath}/{WorkflowReportId}/submit",
            new { baseRevision = factRevision },
            "qa-v2-daily-report-submit");
        var submittedRevision = ReadInt64(submitted.Payload, "revision");
        Record(
            assertions,
            "workflow.site-supervisor.submit",
            submitted.StatusCode == HttpStatusCode.OK &&
            HasString(submitted.Payload, "status", "Submitted") &&
            submittedRevision == 3,
            $"http={(int)submitted.StatusCode};revision={submittedRevision}");

        var inbox = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Get,
            $"{reportsPath}/inbox");
        Record(
            assertions,
            "workflow.technical-office.inbox",
            inbox.StatusCode == HttpStatusCode.OK && ContainsId(inbox.Payload, null, WorkflowReportId),
            $"http={(int)inbox.StatusCode}");

        var approved = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Post,
            $"{reportsPath}/{WorkflowReportId}/approve",
            new { baseRevision = submittedRevision, comment = "QA workflow approved." },
            "qa-v2-daily-report-approve");
        var approvedRevision = ReadInt64(approved.Payload, "revision");
        Record(
            assertions,
            "workflow.technical-office.approve",
            approved.StatusCode == HttpStatusCode.OK &&
            HasString(approved.Payload, "status", "Approved") &&
            HasGuid(approved.Payload, "reviewedBy", technicalOffice.UserId) &&
            approvedRevision == 4,
            $"http={(int)approved.StatusCode};revision={approvedRevision}");

        var observerRead = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"{reportsPath}/{WorkflowReportId}");
        Record(
            assertions,
            "workflow.observer.read-approved",
            observerRead.StatusCode == HttpStatusCode.OK &&
            HasString(observerRead.Payload, "status", "Approved") &&
            HasGuid(observerRead.Payload, "id", WorkflowReportId),
            $"http={(int)observerRead.StatusCode}");
    }

    private static HttpClient CreateClient()
    {
        var baseUrl = ReadRequiredEnvironment("PMCS_QA_BASE_URL");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("PMCS_QA_BASE_URL must be an absolute HTTP or HTTPS URL.");
        }

        return new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) };
    }

    private static async Task<HarnessHttpResult> SendAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        HttpMethod method,
        string path,
        object? payload = null,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Pmcs-QA-Key", key);
        request.Headers.Add("X-Tenant-Id", PmcsTestDataSet.TenantId.ToString());
        request.Headers.Add("X-User-Id", actor.UserId.ToString());
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return new HarnessHttpResult(response.StatusCode, default);
        }

        using var document = JsonDocument.Parse(body);
        return new HarnessHttpResult(response.StatusCode, document.RootElement.Clone());
    }

    private static PmcsTestActor Actor(string code) =>
        PmcsTestDataSet.Actors.Single(actor => string.Equals(actor.Code, code, StringComparison.Ordinal));

    private static bool TryReadDecision(JsonElement payload, string operation, out bool allowed)
    {
        allowed = false;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("decisions", out var decisions) ||
            decisions.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var decision in decisions.EnumerateArray())
        {
            if (TryReadString(decision, "operation", out var value) &&
                string.Equals(value, operation, StringComparison.OrdinalIgnoreCase) &&
                decision.TryGetProperty("allowed", out var decisionAllowed) &&
                decisionAllowed.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                allowed = decisionAllowed.GetBoolean();
                return true;
            }
        }

        return false;
    }

    private static bool ContainsId(JsonElement payload, string? arrayProperty, Guid expected)
    {
        var array = payload;
        if (arrayProperty is not null &&
            (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty(arrayProperty, out array)))
        {
            return false;
        }
        if (array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return array.EnumerateArray().Any(item => HasGuid(item, "id", expected));
    }

    private static bool HasGuid(JsonElement payload, string property, Guid expected) =>
        TryReadString(payload, property, out var value) &&
        Guid.TryParse(value, out var actual) &&
        actual == expected;

    private static bool HasString(JsonElement payload, string property, string expected) =>
        TryReadString(payload, property, out var value) &&
        string.Equals(value, expected, StringComparison.Ordinal);

    private static bool TryReadString(JsonElement payload, string property, out string? value)
    {
        value = null;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(property, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return value is not null;
    }

    private static long? ReadInt64(JsonElement payload, string property) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(property, out var value) &&
        value.TryGetInt64(out var parsed)
            ? parsed
            : null;

    private static void Record(
        List<VerificationAssertion> assertions,
        string name,
        bool passed,
        string detail) =>
        assertions.Add(new VerificationAssertion(name, passed, detail));

    private static int WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: Pmcs.TestHarness <guard|manifest|probe|verify|verify-files|verify-reporting|verify-sync|verify-exploratory>");
        return 2;
    }

    private static string ReadRequiredEnvironment(string key)
    {
        var value = Environment.GetEnvironmentVariable(key)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} is required.")
            : value;
    }

    private sealed record PermissionExpectation(string ActorCode, string Operation, bool Allowed);

    private sealed record VerificationAssertion(string Name, bool Passed, string Detail);

    private sealed record HarnessHttpResult(HttpStatusCode StatusCode, JsonElement Payload);
}
