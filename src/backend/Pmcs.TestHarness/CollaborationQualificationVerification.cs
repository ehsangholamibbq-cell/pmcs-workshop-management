using System.Net;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationQualificationAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var path = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/collaboration";
        var office = Actor("technical-office");
        var supervisor = Actor("site-supervisor");
        var assertions = new List<VerificationAssertion>();
        var source = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages", new
            {
                clientMessageId = Guid.Parse("ca110000-0000-4000-8000-000000000501"),
                body = "پرسش فنی هم‌زمان از دو نقش مجاز"
            }, "qa-col1-qualification-message");
        Record(assertions, "collaboration.qualification.race-source",
            source.StatusCode == HttpStatusCode.Created, $"http={(int)source.StatusCode}");
        if (source.StatusCode != HttpStatusCode.Created) return Report(assertions);
        var messageId = source.Payload.GetProperty("id").GetGuid();
        var destinationId = Guid.Parse("ca110000-0000-4000-8000-000000000502");
        var request = new
        {
            destinationId, destinationType = "RFI", baseRevision = 1,
            confirmed = true, details = new { title = "پرسش هم‌زمان",
                requestedFrom = "مشاور", discipline = "سازه",
                potentialImpact = "Quality", isBlocking = false }
        };
        var unsupported = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = Guid.NewGuid(), destinationType = "FinancePosting",
                baseRevision = 1, confirmed = true, details = new { amount = 100 }
            }, "qa-col1-qualification-unsupported");
        var unconfirmed = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                request.destinationId, request.destinationType, request.baseRevision,
                confirmed = false, request.details
            }, "qa-col1-qualification-unconfirmed");
        Record(assertions, "collaboration.qualification.no-implicit-official-command",
            unsupported.StatusCode == HttpStatusCode.UnprocessableEntity &&
            unconfirmed.StatusCode == HttpStatusCode.BadRequest,
            $"unsupported={(int)unsupported.StatusCode};confirmation={(int)unconfirmed.StatusCode}");

        var contenders = await Task.WhenAll(
            SendAsync(client, key, office, HttpMethod.Post,
                $"{path}/messages/{messageId}/conversions", request,
                "qa-col1-qualification-office-race"),
            SendAsync(client, key, supervisor, HttpMethod.Post,
                $"{path}/messages/{messageId}/conversions", request,
                "qa-col1-qualification-supervisor-race"));
        var lineage = await SendAsync(client, key, office, HttpMethod.Get,
            $"{path}/messages/{messageId}/conversions");
        var state = await SendAsync(client, key, supervisor, HttpMethod.Get,
            $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/technical-office/state");
        Record(assertions, "collaboration.qualification.two-actor-one-official-destination",
            contenders.Count(item => item.StatusCode == HttpStatusCode.Created) == 1 &&
            contenders.Count(item => item.StatusCode == HttpStatusCode.Conflict) == 1 &&
            lineage.StatusCode == HttpStatusCode.OK &&
            lineage.Payload.GetArrayLength() == 1 &&
            HasGuid(lineage.Payload[0], "destinationId", destinationId) &&
            state.StatusCode == HttpStatusCode.OK &&
            state.Payload.GetProperty("rfis").EnumerateArray().Count(item =>
                HasGuid(item, "id", destinationId)) == 1,
            $"race={string.Join(',', contenders.Select(item => (int)item.StatusCode))};lineage={(int)lineage.StatusCode};owner={(int)state.StatusCode}");
        return Report(assertions);

        static int Report(List<VerificationAssertion> assertions)
        {
            var failed = assertions.Count(item => !item.Passed);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                contractVersion = 1, stage = "col1-ms06-concurrent-owner-qualification",
                passed = failed == 0, assertionCount = assertions.Count, failed, assertions
            }, JsonOptions));
            return failed == 0 ? 0 : 1;
        }
    }

    private static async Task<int> VerifyCollaborationOfficialRevokedAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var projectId = PmcsTestDataSet.ProjectId;
        var path = $"/api/v1/projects/{projectId}/collaboration";
        var supervisor = Actor("site-supervisor");
        var office = Actor("technical-office");
        var state = await SendAsync(client, key, office, HttpMethod.Get,
            $"/api/v1/projects/{projectId}/technical-office/state");
        var revision = state.Payload.GetProperty("documentRevisions").EnumerateArray()
            .First(item => HasGuid(item, "documentId",
                Guid.Parse("ca110000-0000-4000-8000-000000000303")));
        var revisionId = revision.GetProperty("id").GetGuid();
        var messages = await SendAsync(client, key, office, HttpMethod.Get,
            $"{path}/messages");
        var messageId = messages.Payload.GetProperty("messages").EnumerateArray()
            .First(item => HasGuid(item, "clientMessageId",
                Guid.Parse("ca110000-0000-4000-8000-000000000501")))
            .GetProperty("id").GetGuid();
        var read = await SendAsync(client, key, supervisor, HttpMethod.Get,
            $"{path}/messages");
        var convert = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = Guid.NewGuid(), destinationType = "RFI", baseRevision = 1,
                confirmed = true, details = new { title = "ممنوع", requestedFrom = "مشاور",
                    discipline = "سازه", potentialImpact = "Quality", isBlocking = false }
            }, "qa-col1-qualification-revoked-convert");
        var evidence = await SendAsync(client, key, supervisor, HttpMethod.Get,
            $"/api/v1/projects/{projectId}/evidence/ca110000-0000-4000-8000-000000000405/content");
        var document = await SendAsync(client, key, supervisor, HttpMethod.Get,
            $"/api/v1/projects/{projectId}/technical-office/document-revisions/{revisionId}/content");
        var passed = state.StatusCode == HttpStatusCode.OK &&
            read.StatusCode == HttpStatusCode.Forbidden &&
            convert.StatusCode == HttpStatusCode.Forbidden &&
            evidence.StatusCode == HttpStatusCode.Forbidden &&
            document.StatusCode == HttpStatusCode.Forbidden;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms06-revoked-official-content",
            passed, read = (int)read.StatusCode, convert = (int)convert.StatusCode,
            evidence = (int)evidence.StatusCode, document = (int)document.StatusCode
        }, JsonOptions));
        return passed ? 0 : 1;
    }
}
