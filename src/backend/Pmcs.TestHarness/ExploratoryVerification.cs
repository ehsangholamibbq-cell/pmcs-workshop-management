using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid ExploratoryOtherProjectId =
        Guid.Parse("33333333-3333-4333-8333-333333333397");

    private static readonly Guid ExploratoryUnknownTenantId =
        Guid.Parse("11111111-1111-4111-8111-111111111199");

    private static readonly Guid ExploratoryUnknownUserId =
        Guid.Parse("22222222-2222-4222-8222-222222222299");

    private static readonly ExploratoryReadSurface[] ExploratoryReadSurfaces =
    [
        new(
            "projects.profile",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}",
            ["projects.read"]),
        new(
            "field.daily-reports",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/daily-reports",
            ["field.daily-reports.read"]),
        new(
            "planning.progress",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/planning/progress",
            ["planning.progress.read"]),
        new(
            "technical-office.state",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/technical-office/state",
            ["technical.read"]),
        new(
            "actions.list",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/actions/",
            ["actions.read"]),
        new(
            "governance.state",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/governance/",
            ["governance.read"]),
        new(
            "finance.records",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/finance/records",
            ["finance.records.read"]),
        new(
            "commercial.contracts",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/commercial/contracts",
            ["contracts.read"]),
        new(
            "commercial.procurement-requests",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/commercial/purchase-requests",
            ["procurement.requests.read"]),
        new(
            "quality-safety.state",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/quality-safety/state",
            ["quality.read", "hse.read"]),
        new(
            "project.command-center",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/command-center",
            ["project-state.read"]),
        new(
            "evidence.list",
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/evidence/",
            ["evidence.read"])
    ];

    private static async Task<int> VerifyExploratoryAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();

        await VerifyExploratoryAuthenticationAsync(client, key, assertions);
        await VerifyExploratoryPersonaMatrixAsync(client, key, assertions);

        var failed = assertions.Count(assertion => !assertion.Passed);
        var report = new
        {
            contractVersion = 1,
            stage = "agent-exploratory-verification",
            mode = "deterministic-persona-explorer",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            actorCount = PmcsTestDataSet.Actors.Count,
            readSurfaceCount = ExploratoryReadSurfaces.Length,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        };
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task VerifyExploratoryAuthenticationAsync(
        HttpClient client,
        string key,
        List<VerificationAssertion> assertions)
    {
        var administrator = PmcsTestDataSet.QaSuperAdministrator;
        var observer = Actor("observer");
        const string suppliedCorrelationId = "qa-exploratory-known-correlation";

        var validHeaders = QaHeaders(key, PmcsTestDataSet.TenantId, administrator.UserId);
        validHeaders["X-Correlation-Id"] = [suppliedCorrelationId];
        var valid = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            validHeaders);
        Record(
            assertions,
            "exploratory.authentication.valid-status-contract",
            valid.StatusCode == HttpStatusCode.OK &&
            string.Equals(valid.CorrelationId, suppliedCorrelationId, StringComparison.Ordinal) &&
            string.Equals(valid.CacheControl, "no-store", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(valid.ContentTypeOptions, "nosniff", StringComparison.OrdinalIgnoreCase) &&
            !valid.Body.Contains(key, StringComparison.Ordinal) &&
            HasQaStatusContract(valid.Body, administrator.UserId),
            $"http={(int)valid.StatusCode};correlation={valid.CorrelationId}");

        var noKeyHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Tenant-Id"] = [PmcsTestDataSet.TenantId.ToString()],
            ["X-User-Id"] = [administrator.UserId.ToString()]
        };
        var noKey = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            noKeyHeaders);
        Record(
            assertions,
            "exploratory.authentication.key-required",
            noKey.StatusCode == HttpStatusCode.Unauthorized &&
            !string.IsNullOrWhiteSpace(noKey.WwwAuthenticate),
            $"http={(int)noKey.StatusCode};challenge={noKey.WwwAuthenticate}");

        var wrongKeyValue = $"wrong-{key}";
        var wrongKey = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            QaHeaders(wrongKeyValue, PmcsTestDataSet.TenantId, administrator.UserId));
        Record(
            assertions,
            "exploratory.authentication.wrong-key-denied-without-secret",
            wrongKey.StatusCode == HttpStatusCode.Unauthorized &&
            !wrongKey.Body.Contains(key, StringComparison.Ordinal) &&
            !wrongKey.Body.Contains(wrongKeyValue, StringComparison.Ordinal),
            $"http={(int)wrongKey.StatusCode}");

        var changedKeyValue = key[..^1] + (key[^1] == 'x' ? 'y' : 'x');
        var changedKey = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            QaHeaders(changedKeyValue, PmcsTestDataSet.TenantId, administrator.UserId));
        Record(
            assertions,
            "exploratory.authentication.one-byte-key-change-denied",
            changedKey.StatusCode == HttpStatusCode.Unauthorized,
            $"http={(int)changedKey.StatusCode}");

        var duplicateKeyHeaders = QaHeaders(key, PmcsTestDataSet.TenantId, administrator.UserId);
        duplicateKeyHeaders["X-Pmcs-QA-Key"] = [key, key];
        var duplicateKey = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            duplicateKeyHeaders);
        Record(
            assertions,
            "exploratory.authentication.duplicate-key-denied",
            duplicateKey.StatusCode == HttpStatusCode.Unauthorized,
            $"http={(int)duplicateKey.StatusCode}");

        var mixedCredentialsHeaders = QaHeaders(key, PmcsTestDataSet.TenantId, administrator.UserId);
        mixedCredentialsHeaders["Authorization"] = ["Bearer qa-must-not-mix"];
        var mixedCredentials = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            mixedCredentialsHeaders);
        Record(
            assertions,
            "exploratory.authentication.mixed-credentials-denied",
            mixedCredentials.StatusCode == HttpStatusCode.Unauthorized,
            $"http={(int)mixedCredentials.StatusCode}");

        var unknownTenant = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            QaHeaders(key, ExploratoryUnknownTenantId, administrator.UserId));
        Record(
            assertions,
            "exploratory.authentication.cross-tenant-actor-denied",
            unknownTenant.StatusCode == HttpStatusCode.Forbidden &&
            HasProblemCode(unknownTenant.Body, "authentication.access.denied"),
            $"http={(int)unknownTenant.StatusCode}");

        var unknownUser = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            QaHeaders(key, PmcsTestDataSet.TenantId, ExploratoryUnknownUserId));
        Record(
            assertions,
            "exploratory.authentication.unknown-user-denied",
            unknownUser.StatusCode == HttpStatusCode.Forbidden &&
            HasProblemCode(unknownUser.Body, "authentication.access.denied"),
            $"http={(int)unknownUser.StatusCode}");

        var roleInjectionHeaders = QaHeaders(key, PmcsTestDataSet.TenantId, observer.UserId);
        roleInjectionHeaders["X-Role"] = ["TenantAdministrator"];
        roleInjectionHeaders["X-Project-Role"] = ["ProjectManager"];
        var roleInjection = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            roleInjectionHeaders);
        Record(
            assertions,
            "exploratory.authentication.role-header-cannot-escalate",
            roleInjection.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)roleInjection.StatusCode}");

        var crossProject = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"/api/v1/projects/{ExploratoryOtherProjectId}");
        Record(
            assertions,
            "exploratory.scope.unrelated-project-denied",
            crossProject.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)crossProject.StatusCode}");

        var missingPreviewParameters = await SendAsync(
            client,
            key,
            administrator,
            HttpMethod.Get,
            "/api/qa/v1/permissions/preview");
        Record(
            assertions,
            "exploratory.gateway.missing-required-query-rejected",
            missingPreviewParameters.StatusCode == HttpStatusCode.BadRequest,
            $"http={(int)missingPreviewParameters.StatusCode}");

        var wrongMethod = await SendAsync(
            client,
            key,
            administrator,
            HttpMethod.Post,
            "/api/qa/v1/status");
        Record(
            assertions,
            "exploratory.gateway.read-only-method-boundary",
            wrongMethod.StatusCode == HttpStatusCode.MethodNotAllowed,
            $"http={(int)wrongMethod.StatusCode}");

        foreach (var destructivePath in new[] { "/api/qa/v1/reset", "/api/qa/v1/seed" })
        {
            var destructive = await SendAsync(
                client,
                key,
                administrator,
                HttpMethod.Post,
                destructivePath);
            Record(
                assertions,
                $"exploratory.gateway.destructive-route-absent.{destructivePath.Split('/').Last()}",
                destructive.StatusCode == HttpStatusCode.NotFound,
                $"http={(int)destructive.StatusCode}");
        }

        const string unknownOperation = "qa.exploratory.unknown-operation";
        var unknownPermission = await SendAsync(
            client,
            key,
            administrator,
            HttpMethod.Get,
            $"/api/qa/v1/permissions/preview?projectId={PmcsTestDataSet.ProjectId}" +
            $"&userId={observer.UserId}&operation={unknownOperation}");
        Record(
            assertions,
            "exploratory.permission.unknown-operation-default-deny",
            unknownPermission.StatusCode == HttpStatusCode.OK &&
            TryReadDecision(unknownPermission.Payload, unknownOperation, out var unknownAllowed) &&
            !unknownAllowed,
            $"http={(int)unknownPermission.StatusCode}");

        const string simulatedOperation = "finance.records.read";
        var simulatedRole = await SendAsync(
            client,
            key,
            administrator,
            HttpMethod.Get,
            $"/api/qa/v1/permissions/preview?projectId={PmcsTestDataSet.ProjectId}" +
            $"&userId={observer.UserId}&operation={simulatedOperation}" +
            "&proposedProjectRoleCode=ProjectManager");
        var observerFinance = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/finance/records");
        Record(
            assertions,
            "exploratory.permission.preview-simulation-does-not-persist",
            simulatedRole.StatusCode == HttpStatusCode.OK &&
            TryReadDecision(simulatedRole.Payload, simulatedOperation, out var simulatedAllowed) &&
            simulatedAllowed &&
            observerFinance.StatusCode == HttpStatusCode.Forbidden,
            $"previewHttp={(int)simulatedRole.StatusCode};actualHttp={(int)observerFinance.StatusCode}");

        var invalidCorrelationValue = new string('x', 80);
        var invalidCorrelationHeaders = QaHeaders(key, PmcsTestDataSet.TenantId, administrator.UserId);
        invalidCorrelationHeaders["X-Correlation-Id"] = [invalidCorrelationValue];
        var invalidCorrelation = await SendExploratoryAsync(
            client,
            HttpMethod.Get,
            "/api/qa/v1/status",
            invalidCorrelationHeaders);
        Record(
            assertions,
            "exploratory.telemetry.invalid-correlation-replaced",
            invalidCorrelation.StatusCode == HttpStatusCode.OK &&
            !string.IsNullOrWhiteSpace(invalidCorrelation.CorrelationId) &&
            invalidCorrelation.CorrelationId.Length <= 64 &&
            !string.Equals(invalidCorrelation.CorrelationId, invalidCorrelationValue, StringComparison.Ordinal),
            $"http={(int)invalidCorrelation.StatusCode};correlation={invalidCorrelation.CorrelationId}");
    }

    private static async Task VerifyExploratoryPersonaMatrixAsync(
        HttpClient client,
        string key,
        List<VerificationAssertion> assertions)
    {
        var requestedOperations = ExploratoryReadSurfaces
            .SelectMany(surface => surface.Operations)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var actor in PmcsTestDataSet.Actors)
        {
            var query = string.Concat(requestedOperations.Select(operation =>
                $"&operation={Uri.EscapeDataString(operation)}"));
            var preview = await SendAsync(
                client,
                key,
                PmcsTestDataSet.QaSuperAdministrator,
                HttpMethod.Get,
                $"/api/qa/v1/permissions/preview?projectId={PmcsTestDataSet.ProjectId}" +
                $"&userId={actor.UserId}{query}");

            var decisions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var operation in requestedOperations)
            {
                if (TryReadDecision(preview.Payload, operation, out var allowed))
                {
                    decisions[operation] = allowed;
                }
            }

            var previewValid = preview.StatusCode == HttpStatusCode.OK &&
                decisions.Count == requestedOperations.Length &&
                HasString(preview.Payload, "policyVersion", "pmcs-rbac-v1") &&
                HasString(preview.Payload, "accountStatus", "Active") &&
                HasString(preview.Payload, "projectRole", actor.ProjectRole);
            Record(
                assertions,
                $"exploratory.persona.{actor.Code}.preview-contract",
                previewValid,
                $"http={(int)preview.StatusCode};decisions={decisions.Count}/{requestedOperations.Length}");

            foreach (var surface in ExploratoryReadSurfaces)
            {
                var expectedAllowed = surface.Operations.Any(operation =>
                    decisions.TryGetValue(operation, out var allowed) && allowed);
                var actual = await SendAsync(
                    client,
                    key,
                    actor,
                    HttpMethod.Get,
                    surface.Path);
                var expectedStatus = expectedAllowed
                    ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden;
                Record(
                    assertions,
                    $"exploratory.persona.{actor.Code}.{surface.Code}",
                    previewValid && actual.StatusCode == expectedStatus,
                    $"expectedHttp={(int)expectedStatus};actualHttp={(int)actual.StatusCode}");
            }
        }
    }

    private static Dictionary<string, string[]> QaHeaders(
        string key,
        Guid tenantId,
        Guid userId) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["X-Pmcs-QA-Key"] = [key],
        ["X-Tenant-Id"] = [tenantId.ToString()],
        ["X-User-Id"] = [userId.ToString()]
    };

    private static async Task<ExploratoryHttpResult> SendExploratoryAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string[]> headers)
    {
        using var request = new HttpRequestMessage(method, path);
        foreach (var (name, values) in headers)
        {
            if (!request.Headers.TryAddWithoutValidation(name, values))
            {
                throw new InvalidOperationException($"Unable to add exploratory request header '{name}'.");
            }
        }

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return new ExploratoryHttpResult(
            response.StatusCode,
            body,
            ReadResponseHeader(response, "X-Correlation-Id"),
            ReadResponseHeader(response, "Cache-Control"),
            ReadResponseHeader(response, "X-Content-Type-Options"),
            ReadResponseHeader(response, "WWW-Authenticate"));
    }

    private static string? ReadResponseHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values) ||
            response.Content.Headers.TryGetValues(name, out values))
        {
            return string.Join(",", values);
        }

        return null;
    }

    private static bool HasQaStatusContract(string body, Guid expectedUserId)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("contractVersion", out var contractVersion) &&
            contractVersion.TryGetInt32(out var parsedContractVersion) &&
            parsedContractVersion == 1 &&
            root.TryGetProperty("enabled", out var enabled) &&
            enabled.ValueKind == JsonValueKind.True &&
            root.TryGetProperty("environment", out var environment) &&
            string.Equals(environment.GetString(), "Development", StringComparison.Ordinal) &&
            root.TryGetProperty("database", out var database) &&
            database.GetString() is { } databaseName &&
            databaseName.StartsWith("pmcs_qa_", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("actor", out var actor) &&
            actor.TryGetProperty("userId", out var userId) &&
            Guid.TryParse(userId.GetString(), out var parsedUserId) &&
            parsedUserId == expectedUserId &&
            root.TryGetProperty("seed", out var seed) &&
            seed.TryGetProperty("actorCount", out var actorCount) &&
            actorCount.TryGetInt32(out var parsedActorCount) &&
            parsedActorCount == PmcsTestDataSet.Actors.Count;
    }

    private static bool HasProblemCode(string body, string expected)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("code", out var code) &&
            string.Equals(code.GetString(), expected, StringComparison.Ordinal);
    }

    private sealed record ExploratoryReadSurface(
        string Code,
        string Path,
        IReadOnlyList<string> Operations);

    private sealed record ExploratoryHttpResult(
        HttpStatusCode StatusCode,
        string Body,
        string? CorrelationId,
        string? CacheControl,
        string? ContentTypeOptions,
        string? WwwAuthenticate);
}
