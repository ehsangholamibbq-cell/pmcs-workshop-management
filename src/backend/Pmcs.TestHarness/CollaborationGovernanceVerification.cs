using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationGovernanceAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var path = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/collaboration";
        var author = Actor("site-supervisor");
        var moderator = Actor("project-controller");
        var observer = Actor("observer");
        var messages = (await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages")).Payload.GetProperty("messages");
        var originalId = messages.EnumerateArray().First(item => HasGuid(item,
            "clientMessageId", Guid.Parse("ca110000-0000-4000-8000-000000000001")))
            .GetProperty("id").GetGuid();
        var moderatorMessageId = messages.EnumerateArray().First(item => HasGuid(item,
            "clientMessageId", Guid.Parse("ca110000-0000-4000-8000-000000000003")))
            .GetProperty("id").GetGuid();
        var documentId = Guid.Parse("ca110000-0000-4000-8000-000000000101");
        var assertions = new List<VerificationAssertion>();

        var observerEdit = await SendAsync(client, key, observer, HttpMethod.Patch,
            $"{path}/messages/{originalId}", new { baseRevision = 1,
                body = "مجاز نیست" }, "qa-col1-governance-observer-edit");
        var editPayload = new { baseRevision = 1, body = "متن ویرایش شده محرمانه" };
        var edit = await SendAsync(client, key, author, HttpMethod.Patch,
            $"{path}/messages/{originalId}", editPayload, "qa-col1-governance-edit");
        var replay = await SendAsync(client, key, author, HttpMethod.Patch,
            $"{path}/messages/{originalId}", editPayload, "qa-col1-governance-edit");
        var stale = await SendAsync(client, key, author, HttpMethod.Patch,
            $"{path}/messages/{originalId}", new { baseRevision = 1,
                body = "تلاش کهنه" }, "qa-col1-governance-stale");
        Record(assertions, "collaboration.governance.edit-own-revision-and-replay",
            observerEdit.StatusCode == HttpStatusCode.Forbidden &&
            edit.StatusCode == HttpStatusCode.OK && replay.StatusCode == HttpStatusCode.OK &&
            ReadInt64(edit.Payload, "revision") == 2 &&
            ReadInt64(replay.Payload, "revision") == 2 &&
            stale.StatusCode == HttpStatusCode.Conflict,
            $"observer={(int)observerEdit.StatusCode};edit={(int)edit.StatusCode};stale={(int)stale.StatusCode}");

        var observerHistory = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages/{originalId}/history");
        var authorHistory = await SendAsync(client, key, author, HttpMethod.Get,
            $"{path}/messages/{originalId}/history");
        Record(assertions, "collaboration.governance.private-immutable-history",
            observerHistory.StatusCode == HttpStatusCode.Forbidden &&
            authorHistory.StatusCode == HttpStatusCode.OK &&
            authorHistory.Payload.GetProperty("revisions").GetArrayLength() == 1 &&
            authorHistory.Payload.GetProperty("revisions")[0].GetProperty("body").GetString() == "گزارش کارگاه",
            $"observer={(int)observerHistory.StatusCode};author={(int)authorHistory.StatusCode}");

        var hold = await SendAsync(client, key, moderator, HttpMethod.Put,
            $"{path}/messages/{originalId}/legal-hold", new { baseRevision = 2,
                enabled = true, reason = "نگهداری برای بررسی" }, "qa-col1-governance-hold");
        var blockedDelete = await SendAsync(client, key, author, HttpMethod.Post,
            $"{path}/messages/{originalId}/delete", new { baseRevision = 3 },
            "qa-col1-governance-delete-held");
        var releaseHold = await SendAsync(client, key, moderator, HttpMethod.Put,
            $"{path}/messages/{originalId}/legal-hold", new { baseRevision = 3,
                enabled = false, reason = "پایان بررسی" }, "qa-col1-governance-release-hold");
        Record(assertions, "collaboration.governance.legal-hold-overrides-delete",
            hold.StatusCode == HttpStatusCode.OK &&
            ReadInt64(hold.Payload, "revision") == 3 &&
            blockedDelete.StatusCode == HttpStatusCode.Conflict &&
            releaseHold.StatusCode == HttpStatusCode.OK &&
            ReadInt64(releaseHold.Payload, "revision") == 4,
            $"hold={(int)hold.StatusCode};blocked={(int)blockedDelete.StatusCode};release={(int)releaseHold.StatusCode}");

        var deleted = await SendAsync(client, key, author, HttpMethod.Post,
            $"{path}/messages/{originalId}/delete", new { baseRevision = 4 },
            "qa-col1-governance-delete");
        var afterDeleteAttachment = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages/{originalId}/attachments/{documentId}/content");
        var afterDeleteList = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages/{originalId}/attachments");
        var search = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages/search?q={Uri.EscapeDataString("ویرایش شده محرمانه")}");
        var retainedHistory = await SendAsync(client, key, author, HttpMethod.Get,
            $"{path}/messages/{originalId}/history");
        Record(assertions, "collaboration.governance.tombstone-retains-history-and-hides-content",
            deleted.StatusCode == HttpStatusCode.OK &&
            ReadInt64(deleted.Payload, "revision") == 5 &&
            deleted.Payload.GetProperty("deletedAt").ValueKind == JsonValueKind.String &&
            afterDeleteAttachment.StatusCode == HttpStatusCode.NotFound &&
            afterDeleteList.StatusCode == HttpStatusCode.NotFound &&
            search.Payload.GetArrayLength() == 0 &&
            retainedHistory.Payload.GetProperty("revisions").GetArrayLength() == 2,
            $"delete={(int)deleted.StatusCode};attachment={(int)afterDeleteAttachment.StatusCode}");

        var redacted = await SendAsync(client, key, moderator, HttpMethod.Post,
            $"{path}/messages/{moderatorMessageId}/redact", new { baseRevision = 1,
                reason = "متن نیازمند پنهان‌سازی" }, "qa-col1-governance-redact");
        var moderationHistory = await SendAsync(client, key, moderator, HttpMethod.Get,
            $"{path}/messages/{moderatorMessageId}/history");
        var list = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages");
        Record(assertions, "collaboration.governance.moderation-reason-and-visible-tombstones",
            redacted.StatusCode == HttpStatusCode.OK &&
            moderationHistory.Payload.GetProperty("moderation").GetArrayLength() == 1 &&
            moderationHistory.Payload.GetProperty("moderation")[0].GetProperty("reason").GetString() ==
                "متن نیازمند پنهان‌سازی" &&
            list.Payload.GetProperty("messages").EnumerateArray().Count(item =>
                item.GetProperty("deletedAt").ValueKind == JsonValueKind.String ||
                item.GetProperty("redactedAt").ValueKind == JsonValueKind.String) == 2,
            $"redact={(int)redacted.StatusCode};list={(int)list.StatusCode}");

        var failed = assertions.Count(item => !item.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms04-governance-connected",
            passed = failed == 0, assertionCount = assertions.Count, failed, assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
