using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationInteractionsAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var projectId = PmcsTestDataSet.ProjectId;
        var path = $"/api/v1/projects/{projectId}/collaboration";
        var supervisor = Actor("site-supervisor");
        var observer = Actor("observer");
        var controller = Actor("project-controller");
        var assertions = new List<VerificationAssertion>();

        var prior = await SendAsync(client, key, supervisor, HttpMethod.Get, $"{path}/messages");
        var original = prior.Payload.GetProperty("messages").EnumerateArray().First(item =>
            HasGuid(item, "clientMessageId", Guid.Parse("ca110000-0000-4000-8000-000000000001")));
        var originalId = original.GetProperty("id").GetGuid();
        var replyId = Guid.Parse("ca110000-0000-4000-8000-000000000004");
        var reply = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages", new
            {
                clientMessageId = replyId, body = "پاسخ به گزارش کارگاه",
                replyToMessageId = originalId,
                mentionedUserIds = new[] { observer.UserId }
            }, "qa-col1-interaction-reply");
        var lastSequence = ReadInt64(reply.Payload, "sequence");
        Record(assertions, "collaboration.interaction.reply-and-mention",
            reply.StatusCode == HttpStatusCode.Created &&
            HasGuid(reply.Payload, "replyToMessageId", originalId) &&
            lastSequence >= 4, $"http={(int)reply.StatusCode};sequence={lastSequence}");

        var badReply = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages", new { clientMessageId = Guid.NewGuid(), body = "نامعتبر",
                replyToMessageId = Guid.NewGuid() }, "qa-col1-interaction-bad-reply");
        var badMention = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages", new { clientMessageId = Guid.NewGuid(), body = "نامعتبر",
                mentionedUserIds = new[] { Guid.NewGuid() } }, "qa-col1-interaction-bad-mention");
        Record(assertions, "collaboration.interaction.no-cross-scope-target",
            badReply.StatusCode == HttpStatusCode.NotFound &&
            badMention.StatusCode == HttpStatusCode.BadRequest,
            $"reply={(int)badReply.StatusCode};mention={(int)badMention.StatusCode}");

        var search = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages/search?q={Uri.EscapeDataString("پاسخ به گزارش")}");
        Record(assertions, "collaboration.interaction.permission-filtered-search",
            search.StatusCode == HttpStatusCode.OK &&
            search.Payload.ValueKind == JsonValueKind.Array &&
            search.Payload.EnumerateArray().Any(item => HasGuid(item, "clientMessageId", replyId)),
            $"http={(int)search.StatusCode}");

        var unread = await SendAsync(client, key, observer, HttpMethod.Get, $"{path}/unread");
        Record(assertions, "collaboration.interaction.unread",
            unread.StatusCode == HttpStatusCode.OK &&
            ReadInt64(unread.Payload, "unreadCount") == 4,
            $"http={(int)unread.StatusCode};unread={ReadInt64(unread.Payload, "unreadCount")}");
        var cursor = await SendAsync(client, key, observer, HttpMethod.Put,
            $"{path}/read-cursor", new { lastReadSequence = lastSequence });
        var rewind = await SendAsync(client, key, observer, HttpMethod.Put,
            $"{path}/read-cursor", new { lastReadSequence = 0 });
        var cleared = await SendAsync(client, key, observer, HttpMethod.Get, $"{path}/unread");
        Record(assertions, "collaboration.interaction.monotonic-read-cursor",
            cursor.StatusCode == HttpStatusCode.OK && rewind.StatusCode == HttpStatusCode.OK &&
            ReadInt64(rewind.Payload, "lastReadSequence") == lastSequence &&
            ReadInt64(cleared.Payload, "unreadCount") == 0,
            $"cursor={(int)cursor.StatusCode};rewind={(int)rewind.StatusCode}");

        var observerReaction = await SendAsync(client, key, observer, HttpMethod.Put,
            $"{path}/messages/{originalId}/reactions/{Uri.EscapeDataString("👍")}");
        var reactionPath = $"{path}/messages/{originalId}/reactions/{Uri.EscapeDataString("👍")}";
        var reaction = await SendAsync(client, key, supervisor, HttpMethod.Put, reactionPath);
        var reactionRepeat = await SendAsync(client, key, supervisor, HttpMethod.Put, reactionPath);
        Record(assertions, "collaboration.interaction.reaction-permission-and-idempotency",
            observerReaction.StatusCode == HttpStatusCode.Forbidden &&
            reaction.StatusCode == HttpStatusCode.OK && reactionRepeat.StatusCode == HttpStatusCode.OK,
            $"observer={(int)observerReaction.StatusCode};send={(int)reaction.StatusCode}");

        var supervisorPin = await SendAsync(client, key, supervisor, HttpMethod.Put,
            $"{path}/messages/{originalId}/pin");
        var pin = await SendAsync(client, key, controller, HttpMethod.Put,
            $"{path}/messages/{originalId}/pin");
        var unpin = await SendAsync(client, key, controller, HttpMethod.Delete,
            $"{path}/messages/{originalId}/pin");
        Record(assertions, "collaboration.interaction.moderated-pin",
            supervisorPin.StatusCode == HttpStatusCode.Forbidden &&
            pin.StatusCode == HttpStatusCode.OK && unpin.StatusCode == HttpStatusCode.OK &&
            pin.Payload.GetProperty("pinnedAt").ValueKind == JsonValueKind.String &&
            unpin.Payload.GetProperty("pinnedAt").ValueKind == JsonValueKind.Null,
            $"supervisor={(int)supervisorPin.StatusCode};moderator={(int)pin.StatusCode}");

        var failed = assertions.Count(item => !item.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms02-connected", passed = failed == 0,
            assertionCount = assertions.Count, failed, assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
