using System.Net;
using System.Text;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationTechnicalConversionsAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var projectId = PmcsTestDataSet.ProjectId;
        var path = $"/api/v1/projects/{projectId}/collaboration";
        var office = Actor("technical-office");
        var observer = Actor("observer");
        var scanner = Actor("qa-super-admin");
        var assertions = new List<VerificationAssertion>();
        var officeRoom = await SendAsync(client, key, office, HttpMethod.Get, path);
        var observerRoom = await SendAsync(client, key, observer, HttpMethod.Get, path);
        Record(assertions, "collaboration.technical.rfi-room-capability",
            officeRoom.StatusCode == HttpStatusCode.OK &&
            observerRoom.StatusCode == HttpStatusCode.OK &&
            officeRoom.Payload.GetProperty("canConvertRfi").GetBoolean() &&
            officeRoom.Payload.GetProperty("canConvertTechnicalDocument").GetBoolean() &&
            !observerRoom.Payload.GetProperty("canConvertRfi").GetBoolean() &&
            !observerRoom.Payload.GetProperty("canConvertTechnicalDocument").GetBoolean(),
            $"office={(int)officeRoom.StatusCode};observer={(int)observerRoom.StatusCode}");
        var message = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages", new
            {
                clientMessageId = Guid.Parse("ca110000-0000-4000-8000-000000000301"),
                body = "پرسش فنی دربارهٔ مشخصات بتن و مدرک پیوست"
            }, "qa-col1-technical-message");
        Record(assertions, "collaboration.technical.source-message",
            message.StatusCode == HttpStatusCode.Created,
            $"http={(int)message.StatusCode}");
        if (message.StatusCode != HttpStatusCode.Created) return Report(assertions);
        var messageId = message.Payload.GetProperty("id").GetGuid();
        var documentId = Guid.Parse("ca110000-0000-4000-8000-000000000310");
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n% PMCS technical conversion\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        var sha256 = Sha256(bytes);
        var session = await SendAsync(client, key, office, HttpMethod.Post,
            "/api/v1/upload-sessions", new
            {
                clientGeneratedId = documentId, projectId, ownerType = "ProjectChat",
                ownerId = messageId, originalFileName = "technical-source.pdf",
                contentType = "application/pdf", sizeBytes = bytes.LongLength,
                sha256, classification = "Internal", retentionPolicy = "Standard",
                retainUntil = (DateTimeOffset?)null, legalHold = false
            }, "qa-col1-technical-session");
        var upload = await SendBinaryAsync(client, key, office, HttpMethod.Put,
            $"/api/v1/documents/{documentId}/content", bytes,
            "application/pdf", "qa-col1-technical-upload");
        var release = await SendAsync(client, key, scanner, HttpMethod.Post,
            $"/api/v1/documents/{documentId}/release", new
            {
                baseRevision = ReadInt64(upload.Payload, "revision") ?? 0
            }, "qa-col1-technical-release");
        var attach = await SendAsync(client, key, office, HttpMethod.Put,
            $"{path}/messages/{messageId}/attachments/{documentId}");
        Record(assertions, "collaboration.technical.released-evidence",
            session.StatusCode == HttpStatusCode.Created &&
            upload.StatusCode == HttpStatusCode.OK &&
            release.StatusCode == HttpStatusCode.OK &&
            attach.StatusCode == HttpStatusCode.OK,
            $"session={(int)session.StatusCode};upload={(int)upload.StatusCode};release={(int)release.StatusCode};attach={(int)attach.StatusCode}");

        var rfiId = Guid.Parse("ca110000-0000-4000-8000-000000000302");
        var technicalId = Guid.Parse("ca110000-0000-4000-8000-000000000303");
        var rfi = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = rfiId, destinationType = "RFI", baseRevision = 1,
                confirmed = true, documentIds = new[] { documentId },
                details = new { title = "پرسش مشخصات بتن", requestedFrom = "مشاور",
                    discipline = "سازه", potentialImpact = "Time, Quality", isBlocking = false,
                    requiredByDate = "2099-01-01" }
            }, "qa-col1-technical-rfi");
        var duplicateRfi = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = Guid.NewGuid(), destinationType = "RFI", baseRevision = 1,
                confirmed = true, documentIds = Array.Empty<Guid>(),
                details = new { title = "پرسش تکراری", requestedFrom = "مشاور",
                    discipline = "سازه", potentialImpact = "Quality", isBlocking = false }
            }, "qa-ux2-technical-rfi-duplicate");
        var technical = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = technicalId, destinationType = "TechnicalDocument",
                baseRevision = 1, confirmed = true, documentIds = new[] { documentId },
                details = new { title = "مشخصات بتن", type = "Specification",
                    discipline = "سازه", revisionCode = "A0" }
            }, "qa-col1-technical-document");
        var duplicateTechnical = await SendAsync(client, key, office, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = Guid.Parse("ca110000-0000-4000-8000-000000000308"),
                destinationType = "TechnicalDocument", baseRevision = 1, confirmed = true,
                documentIds = new[] { documentId },
                details = new { title = "مشخصات تکراری", type = "Specification",
                    discipline = "سازه", revisionCode = "A0" }
            }, "qa-ux2-technical-document-duplicate");
        var lineage = await SendAsync(client, key, office, HttpMethod.Get,
            $"{path}/messages/{messageId}/conversions");
        var state = await SendAsync(client, key, office, HttpMethod.Get,
            $"/api/v1/projects/{projectId}/technical-office/state");
        var validState = state.StatusCode == HttpStatusCode.OK;
        var officialRfi = validState ? state.Payload.GetProperty("rfis").EnumerateArray()
            .FirstOrDefault(item => HasGuid(item, "id", rfiId)) : default;
        var officialDocument = validState ? state.Payload.GetProperty("documents").EnumerateArray()
            .FirstOrDefault(item => HasGuid(item, "id", technicalId)) : default;
        var revision = validState ? state.Payload.GetProperty("documentRevisions").EnumerateArray()
            .FirstOrDefault(item => HasGuid(item, "documentId", technicalId)) : default;
        var revisionId = revision.ValueKind == JsonValueKind.Object
            ? revision.GetProperty("id").GetGuid() : Guid.Empty;
        Record(assertions, "collaboration.technical.owner-rows-and-lineage",
            rfi.StatusCode == HttpStatusCode.Created &&
            duplicateRfi.StatusCode == HttpStatusCode.Conflict &&
            technical.StatusCode == HttpStatusCode.Created &&
            duplicateTechnical.StatusCode == HttpStatusCode.Conflict &&
            duplicateTechnical.Payload.GetProperty("code").GetString() ==
                "collaboration.conversion.technical_document.source.already_created" &&
            lineage.StatusCode == HttpStatusCode.OK &&
            lineage.Payload.GetArrayLength() == 2 &&
            officialRfi.ValueKind == JsonValueKind.Object &&
            officialRfi.GetProperty("status").GetString() == "Draft" &&
            officialRfi.GetProperty("potentialImpact").GetString() == "Time, Quality" &&
            officialRfi.GetProperty("evidenceReferences").EnumerateArray().Any(item =>
                item.GetString() == $"pmcs:chat-document:{messageId:N}:{documentId:N}:v1:sha256:{sha256}") &&
            officialDocument.ValueKind == JsonValueKind.Object &&
            revisionId != Guid.Empty && revision.GetProperty("status").GetString() == "Draft" &&
            revision.GetProperty("sha256").GetString() == sha256,
            $"rfi={(int)rfi.StatusCode};duplicate={(int)duplicateRfi.StatusCode};document={(int)technical.StatusCode};state={(int)state.StatusCode};lineage={(int)lineage.StatusCode}");

        if (revisionId != Guid.Empty)
        {
            var officialBefore = await DownloadFileAsync(client, key, observer,
                $"/api/v1/projects/{projectId}/technical-office/document-revisions/{revisionId}/content");
            var rfiBefore = await DownloadFileAsync(client, key, observer,
                $"/api/v1/projects/{projectId}/technical-office/rfis/{rfiId}/evidence/{documentId}/content");
            var deleted = await SendAsync(client, key, office, HttpMethod.Post,
                $"{path}/messages/{messageId}/delete", new { baseRevision = 1 },
                "qa-col1-technical-source-tombstone");
            var chatAfter = await SendAsync(client, key, observer, HttpMethod.Get,
                $"{path}/messages/{messageId}/attachments/{documentId}/content");
            var officialAfter = await DownloadFileAsync(client, key, observer,
                $"/api/v1/projects/{projectId}/technical-office/document-revisions/{revisionId}/content");
            var rfiAfter = await DownloadFileAsync(client, key, observer,
                $"/api/v1/projects/{projectId}/technical-office/rfis/{rfiId}/evidence/{documentId}/content");
            var wrongProject = await SendAsync(client, key, observer, HttpMethod.Get,
                $"/api/v1/projects/{Guid.NewGuid()}/technical-office/document-revisions/{revisionId}/content");
            Record(assertions, "collaboration.technical.official-content-survives-chat-tombstone",
                officialBefore.StatusCode == HttpStatusCode.OK &&
                officialBefore.Bytes.SequenceEqual(bytes) && officialBefore.NoStore &&
                rfiBefore.StatusCode == HttpStatusCode.OK && rfiBefore.Bytes.SequenceEqual(bytes) && rfiBefore.NoStore &&
                deleted.StatusCode == HttpStatusCode.OK &&
                chatAfter.StatusCode == HttpStatusCode.NotFound &&
                officialAfter.StatusCode == HttpStatusCode.OK &&
                officialAfter.Bytes.SequenceEqual(bytes) && officialAfter.NoStore &&
                rfiAfter.StatusCode == HttpStatusCode.OK && rfiAfter.Bytes.SequenceEqual(bytes) && rfiAfter.NoStore &&
                wrongProject.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"official={(int)officialBefore.StatusCode};delete={(int)deleted.StatusCode};chat={(int)chatAfter.StatusCode};retained={(int)officialAfter.StatusCode};scope={(int)wrongProject.StatusCode}");
        }
        return Report(assertions);

        static int Report(List<VerificationAssertion> assertions)
        {
            var failed = assertions.Count(item => !item.Passed);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                contractVersion = 1, stage = "col1-ms05-rfi-technical-connected",
                passed = failed == 0, assertionCount = assertions.Count, failed, assertions
            }, JsonOptions));
            return failed == 0 ? 0 : 1;
        }
    }
}
