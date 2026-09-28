using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationLiveAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var projectId = PmcsTestDataSet.ProjectId;
        var path = $"/api/v1/projects/{projectId}/collaboration";
        var supervisor = Actor("site-supervisor");
        var observer = Actor("observer");
        var messageId = Guid.Parse("ca110000-0000-4000-8000-000000000005");
        var assertions = new List<VerificationAssertion>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));

        using var firstSocket = await OpenCollaborationSocketAsync(key, observer, path, 4,
            timeout.Token);
        using var secondSocket = await OpenCollaborationSocketAsync(key, supervisor, path, 4,
            timeout.Token);
        var longPoll = SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/events?after=4&waitSeconds=8");
        await Task.Delay(500, timeout.Token);
        var created = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages", new { clientMessageId = messageId,
                body = "پیام هم‌زمان و قابل بازیابی" }, "qa-col1-live-five");
        var sequence = ReadInt64(created.Payload, "sequence");
        var frames = await Task.WhenAll(
            ReceiveMessageEventAsync(firstSocket, timeout.Token),
            ReceiveMessageEventAsync(secondSocket, timeout.Token));
        var fallback = await longPoll;
        Record(assertions, "collaboration.live.two-users-and-fallback",
            created.StatusCode == HttpStatusCode.Created && sequence == 5 &&
            frames.All(frame => HasGuid(frame, "messageId", created.Payload.GetProperty("id").GetGuid()) &&
                ReadInt64(frame, "sequence") == sequence && !frame.TryGetProperty("body", out _)) &&
            fallback.StatusCode == HttpStatusCode.OK &&
            fallback.Payload.GetProperty("events").GetArrayLength() == 1 &&
            ReadInt64(fallback.Payload, "nextSequence") == sequence,
            $"created={(int)created.StatusCode};poll={(int)fallback.StatusCode};sequence={sequence}");

        using var reconnected = await OpenCollaborationSocketAsync(key, observer, path,
            sequence - 1, timeout.Token);
        var recovered = await ReceiveMessageEventAsync(reconnected, timeout.Token);
        var retry = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages", new { clientMessageId = messageId,
                body = "پیام هم‌زمان و قابل بازیابی" }, "qa-col1-live-five-offline-retry");
        var noDuplicate = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/events?after={sequence - 1}");
        Record(assertions, "collaboration.live.reconnect-and-stable-retry",
            HasGuid(recovered, "messageId", created.Payload.GetProperty("id").GetGuid()) &&
            retry.StatusCode == HttpStatusCode.OK && ReadInt64(retry.Payload, "sequence") == sequence &&
            noDuplicate.Payload.GetProperty("events").GetArrayLength() == 1,
            $"retry={(int)retry.StatusCode};count={noDuplicate.Payload.GetProperty("events").GetArrayLength()}");

        var invalid = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/events?after=-1");
        var foreign = await SendAsync(client, key, observer, HttpMethod.Get,
            $"/api/v1/projects/{Guid.NewGuid()}/collaboration/events?after=0");
        Record(assertions, "collaboration.live.cursor-and-scope",
            invalid.StatusCode == HttpStatusCode.BadRequest &&
            foreign.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"invalid={(int)invalid.StatusCode};foreign={(int)foreign.StatusCode}");

        var failed = assertions.Count(item => !item.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms03-connected", passed = failed == 0,
            assertionCount = assertions.Count, failed, assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static async Task<ClientWebSocket> OpenCollaborationSocketAsync(string key,
        PmcsTestActor actor, string path, long after, CancellationToken cancellationToken)
    {
        var root = new Uri(ReadRequiredEnvironment("PMCS_QA_BASE_URL"));
        var uri = new UriBuilder(root)
        {
            Scheme = root.Scheme == "https" ? "wss" : "ws",
            Path = $"{path}/live", Query = $"after={after}"
        }.Uri;
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("X-Pmcs-QA-Key", key);
        socket.Options.SetRequestHeader("X-Tenant-Id", PmcsTestDataSet.TenantId.ToString());
        socket.Options.SetRequestHeader("X-User-Id", actor.UserId.ToString());
        await socket.ConnectAsync(uri, cancellationToken);
        return socket;
    }

    private static async Task<JsonElement> ReceiveMessageEventAsync(ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[2048];
        while (true)
        {
            var received = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
            if (received.MessageType != WebSocketMessageType.Text || !received.EndOfMessage)
                throw new InvalidOperationException("Collaboration live frame was not bounded text.");
            using var parsed = JsonDocument.Parse(buffer.AsMemory(0, received.Count));
            var frame = parsed.RootElement;
            if (frame.GetProperty("type").GetString() == "message.created")
                return frame.Clone();
        }
    }
}
