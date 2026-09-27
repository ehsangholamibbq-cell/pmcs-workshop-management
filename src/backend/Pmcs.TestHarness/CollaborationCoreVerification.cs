using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationRevokedAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var actor = Actor("site-supervisor");
        var path = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/collaboration";
        var read = await SendAsync(client, key, actor, HttpMethod.Get, $"{path}/messages");
        var write = await SendAsync(client, key, actor, HttpMethod.Post, $"{path}/messages",
            new { clientMessageId = Guid.NewGuid(), body = "نباید ثبت شود" }, "qa-col1-revoked-write");
        var passed = read.StatusCode == HttpStatusCode.Forbidden &&
            write.StatusCode == HttpStatusCode.Forbidden;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms01-revocation-connected", passed,
            readStatus = (int)read.StatusCode, writeStatus = (int)write.StatusCode
        }, JsonOptions));
        return passed ? 0 : 1;
    }

    private static async Task<int> VerifyCollaborationCoreAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var projectId = PmcsTestDataSet.ProjectId;
        var path = $"/api/v1/projects/{projectId}/collaboration";
        var supervisor = Actor("site-supervisor");
        var observer = Actor("observer");
        var controller = Actor("project-controller");
        var assertions = new List<VerificationAssertion>();

        var room = await SendAsync(client, key, supervisor, HttpMethod.Get, path);
        Record(assertions, "collaboration.core.default-room",
            room.StatusCode == HttpStatusCode.OK &&
            HasGuid(room.Payload, "projectId", projectId) &&
            HasGuid(room.Payload, "id", projectId), $"http={(int)room.StatusCode}");

        var denied = await SendAsync(client, key, observer, HttpMethod.Post,
            $"{path}/messages", new { clientMessageId = Guid.NewGuid(), body = "no" }, "qa-col1-observer");
        Record(assertions, "collaboration.core.read-does-not-send",
            denied.StatusCode == HttpStatusCode.Forbidden, $"http={(int)denied.StatusCode}");

        var missingProject = await SendAsync(client, key, supervisor, HttpMethod.Get,
            $"/api/v1/projects/{Guid.NewGuid()}/collaboration/messages");
        Record(assertions, "collaboration.core.no-cross-project-room",
            missingProject.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"http={(int)missingProject.StatusCode}");

        var clientId = Guid.Parse("ca110000-0000-4000-8000-000000000001");
        var payload = new { clientMessageId = clientId, body = "گزارش کارگاه" };
        var created = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages", payload, "qa-col1-message-one");
        var firstSequence = ReadInt64(created.Payload, "sequence");
        Record(assertions, "collaboration.core.send",
            created.StatusCode == HttpStatusCode.Created && firstSequence >= 1 &&
            HasGuid(created.Payload, "clientMessageId", clientId),
            $"http={(int)created.StatusCode};sequence={firstSequence}");

        var replay = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages", payload, "qa-col1-message-one");
        var offlineRetry = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages", payload, "qa-col1-message-one-offline-retry");
        Record(assertions, "collaboration.core.stable-retry",
            replay.StatusCode == HttpStatusCode.Created &&
            offlineRetry.StatusCode == HttpStatusCode.OK &&
            ReadInt64(replay.Payload, "sequence") == firstSequence &&
            ReadInt64(offlineRetry.Payload, "sequence") == firstSequence,
            $"replay={(int)replay.StatusCode};retry={(int)offlineRetry.StatusCode}");

        var changed = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages", new { clientMessageId = clientId, body = "تغییر ناخواسته" },
            "qa-col1-message-one-changed");
        Record(assertions, "collaboration.core.changed-retry-conflict",
            changed.StatusCode == HttpStatusCode.Conflict, $"http={(int)changed.StatusCode}");

        var parallel = await Task.WhenAll(
            SendAsync(client, key, supervisor, HttpMethod.Post, $"{path}/messages",
                new { clientMessageId = Guid.Parse("ca110000-0000-4000-8000-000000000002"), body = "پیام دوم" },
                "qa-col1-parallel-two"),
            SendAsync(client, key, controller, HttpMethod.Post, $"{path}/messages",
                new { clientMessageId = Guid.Parse("ca110000-0000-4000-8000-000000000003"), body = "پیام سوم" },
                "qa-col1-parallel-three"));
        var sequences = parallel.Select(result => ReadInt64(result.Payload, "sequence")).Order().ToArray();
        Record(assertions, "collaboration.core.two-actor-order",
            parallel.All(result => result.StatusCode == HttpStatusCode.Created) &&
            sequences.SequenceEqual(new[] { firstSequence + 1, firstSequence + 2 }),
            $"http={string.Join(',', parallel.Select(item => (int)item.StatusCode))};" +
            $"sequences={string.Join(',', sequences)}");

        var page = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages?after={firstSequence}");
        var pageCount = page.Payload.ValueKind == JsonValueKind.Object &&
            page.Payload.TryGetProperty("messages", out var messages) &&
            messages.ValueKind == JsonValueKind.Array ? messages.GetArrayLength() : 0;
        Record(assertions, "collaboration.core.permission-filtered-cursor",
            page.StatusCode == HttpStatusCode.OK && pageCount == 2 &&
            ReadInt64(page.Payload, "nextSequence") == firstSequence + 2,
            $"http={(int)page.StatusCode};count={pageCount}");

        var failed = assertions.Count(item => !item.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms01-connected", passed = failed == 0,
            assertionCount = assertions.Count, failed,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
