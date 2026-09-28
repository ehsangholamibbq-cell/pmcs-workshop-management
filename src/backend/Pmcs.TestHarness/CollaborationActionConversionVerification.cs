using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationActionConversionsAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var path = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/collaboration";
        var controller = Actor("project-controller");
        var supervisor = Actor("site-supervisor");
        var observer = Actor("observer");
        var messages = (await SendAsync(client, key, controller, HttpMethod.Get,
            $"{path}/messages")).Payload.GetProperty("messages");
        var sourceId = messages.EnumerateArray().First(item => HasGuid(item,
            "clientMessageId", Guid.Parse("ca110000-0000-4000-8000-000000000002")))
            .GetProperty("id").GetGuid();
        var deletedId = messages.EnumerateArray().First(item => HasGuid(item,
            "clientMessageId", Guid.Parse("ca110000-0000-4000-8000-000000000001")))
            .GetProperty("id").GetGuid();
        var actionId = Guid.Parse("ca110000-0000-4000-8000-000000000201");
        var issueId = Guid.Parse("ca110000-0000-4000-8000-000000000202");
        var actionDetails = new { assigneeUserId = supervisor.UserId,
            dueDate = "2099-01-01", priority = "High", title = "اقدام تأیید شده از گفت‌وگو",
            description = "انتقال صریح با مجوز مالک" };
        var actionRequest = new { destinationId = actionId, destinationType = "Action",
            baseRevision = 1, confirmed = true, details = actionDetails,
            documentIds = Array.Empty<Guid>() };
        var assertions = new List<VerificationAssertion>();

        var unconfirmed = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages/{sourceId}/conversions", new { destinationId = actionId,
                destinationType = "Action", baseRevision = 1, confirmed = false,
                details = actionDetails }, "qa-col1-action-unconfirmed");
        var observerDenied = await SendAsync(client, key, observer, HttpMethod.Post,
            $"{path}/messages/{sourceId}/conversions", actionRequest,
            "qa-col1-action-observer-denied");
        var deletedDenied = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages/{deletedId}/conversions", new { destinationId = Guid.NewGuid(),
                destinationType = "Action", baseRevision = 5, confirmed = true,
                details = actionDetails }, "qa-col1-action-tombstone");
        Record(assertions, "collaboration.conversion.human-confirmation-permission-tombstone",
            unconfirmed.StatusCode == HttpStatusCode.BadRequest &&
            observerDenied.StatusCode == HttpStatusCode.Forbidden &&
            deletedDenied.StatusCode == HttpStatusCode.Conflict,
            $"confirmation={(int)unconfirmed.StatusCode};observer={(int)observerDenied.StatusCode};tombstone={(int)deletedDenied.StatusCode}");

        var action = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages/{sourceId}/conversions", actionRequest,
            "qa-col1-action-conversion");
        var replay = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages/{sourceId}/conversions", actionRequest,
            "qa-col1-action-conversion");
        var changed = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages/{sourceId}/conversions", new { destinationId = actionId,
                destinationType = "Action", baseRevision = 1, confirmed = true,
                details = new { actionDetails.assigneeUserId, actionDetails.dueDate,
                    actionDetails.priority, title = "عنوان متفاوت", actionDetails.description } },
            "qa-col1-action-changed");
        var officialAction = await SendAsync(client, key, controller, HttpMethod.Get,
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/actions/{actionId}");
        Record(assertions, "collaboration.conversion.action-owner-and-idempotence",
            action.StatusCode == HttpStatusCode.Created &&
            replay.StatusCode == HttpStatusCode.Created &&
            changed.StatusCode == HttpStatusCode.Conflict &&
            officialAction.StatusCode == HttpStatusCode.OK &&
            HasGuid(officialAction.Payload, "sourceMessageId", sourceId) &&
            officialAction.Payload.GetProperty("sourceFactId").ValueKind == JsonValueKind.Null,
            $"create={(int)action.StatusCode};replay={(int)replay.StatusCode};changed={(int)changed.StatusCode};owner={(int)officialAction.StatusCode}");

        var issue = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages/{sourceId}/conversions", new { destinationId = issueId,
                destinationType = "Issue", baseRevision = 1, confirmed = true,
                details = new { title = "مسئلهٔ رسمی از گفت‌وگو", category = "Coordination",
                    severity = "Medium", urgency = "Soon", ownerUserId = supervisor.UserId,
                    targetResolutionDate = "2099-01-01", confidentiality = "GeneralProject" } },
            "qa-col1-issue-conversion");
        var duplicateIssue = await SendAsync(client, key, controller, HttpMethod.Post,
            $"{path}/messages/{sourceId}/conversions", new { destinationId = Guid.NewGuid(),
                destinationType = "Issue", baseRevision = 1, confirmed = true,
                details = new { title = "تکرار مسئله", category = "Coordination",
                    severity = "Medium", urgency = "Soon", ownerUserId = supervisor.UserId,
                    targetResolutionDate = "2099-01-01", confidentiality = "GeneralProject" } },
            "qa-ux2-issue-duplicate");
        var lineage = await SendAsync(client, key, controller, HttpMethod.Get,
            $"{path}/messages/{sourceId}/conversions");
        var governance = await SendAsync(client, key, controller, HttpMethod.Get,
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/governance");
        Record(assertions, "collaboration.conversion.issue-owner-and-lineage",
            issue.StatusCode == HttpStatusCode.Created &&
            duplicateIssue.StatusCode == HttpStatusCode.Conflict &&
            HasGuid(issue.Payload, "destinationId", issueId) &&
            lineage.StatusCode == HttpStatusCode.OK &&
            lineage.Payload.GetArrayLength() == 2 &&
            governance.StatusCode == HttpStatusCode.OK &&
            governance.Payload.GetProperty("issues").EnumerateArray().Any(item =>
                HasGuid(item, "id", issueId) &&
                item.GetProperty("sourceModule").GetString() == "collaboration" &&
                item.GetProperty("evidenceReferences").EnumerateArray().Any(reference =>
                    reference.GetString() == $"collaboration:message:{sourceId:N}:revision:1")),
            $"issue={(int)issue.StatusCode};duplicate={(int)duplicateIssue.StatusCode};lineage={(int)lineage.StatusCode};governance={(int)governance.StatusCode}");

        var failed = assertions.Count(item => !item.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms05-action-issue-connected",
            passed = failed == 0, assertionCount = assertions.Count, failed, assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
