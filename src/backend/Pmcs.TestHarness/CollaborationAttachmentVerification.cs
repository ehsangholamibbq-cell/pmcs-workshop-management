using System.Net;
using System.Text;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationAttachmentAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var projectId = PmcsTestDataSet.ProjectId;
        var path = $"/api/v1/projects/{projectId}/collaboration";
        var author = Actor("site-supervisor");
        var observer = Actor("observer");
        var scanner = Actor("qa-super-admin");
        var messageId = (await SendAsync(client, key, author, HttpMethod.Get,
            $"{path}/messages")).Payload.GetProperty("messages").EnumerateArray()
            .First(item => HasGuid(item, "clientMessageId",
                Guid.Parse("ca110000-0000-4000-8000-000000000001")))
            .GetProperty("id").GetGuid();
        var documentId = Guid.Parse("ca110000-0000-4000-8000-000000000101");
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n% PMCS collaboration attachment\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        var sha256 = Sha256(bytes);
        var assertions = new List<VerificationAssertion>();
        var sessionRequest = new
        {
            clientGeneratedId = documentId, projectId, ownerType = "ProjectChat",
            ownerId = messageId, originalFileName = "collaboration.pdf",
            contentType = "application/pdf", sizeBytes = bytes.LongLength,
            sha256, classification = "Internal", retentionPolicy = "Permanent",
            retainUntil = (DateTimeOffset?)null, legalHold = true
        };
        // Custom governance requires documents.classify, so use the default
        // document policy here; message retention is independently no-purge.
        var defaultRequest = new
        {
            sessionRequest.clientGeneratedId, sessionRequest.projectId,
            sessionRequest.ownerType, sessionRequest.ownerId,
            sessionRequest.originalFileName, sessionRequest.contentType,
            sessionRequest.sizeBytes, sessionRequest.sha256,
            sessionRequest.classification, retentionPolicy = "Standard",
            retainUntil = (DateTimeOffset?)null, legalHold = false
        };
        var invalidOwner = await SendAsync(client, key, author, HttpMethod.Post,
            "/api/v1/upload-sessions", new
            {
                clientGeneratedId = Guid.NewGuid(), projectId, ownerType = "ProjectChat",
                ownerId = Guid.NewGuid(), originalFileName = "invalid.pdf",
                contentType = "application/pdf", sizeBytes = bytes.LongLength,
                sha256, classification = "Internal", retentionPolicy = "Standard",
                retainUntil = (DateTimeOffset?)null, legalHold = false
            }, "qa-col1-doc-invalid-owner");
        Record(assertions, "collaboration.attachment.owner-must-be-authored-message",
            invalidOwner.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)invalidOwner.StatusCode}");

        var session = await SendAsync(client, key, author, HttpMethod.Post,
            "/api/v1/upload-sessions", defaultRequest, "qa-col1-doc-session");
        Record(assertions, "collaboration.attachment.scoped-session",
            session.StatusCode == HttpStatusCode.Created &&
            HasGuid(session.Payload.GetProperty("document"), "id", documentId),
            $"http={(int)session.StatusCode}");
        var upload = await SendBinaryAsync(client, key, author, HttpMethod.Put,
            $"/api/v1/documents/{documentId}/content", bytes,
            "application/pdf", "qa-col1-doc-upload");
        var beforeRelease = await SendAsync(client, key, author, HttpMethod.Put,
            $"{path}/messages/{messageId}/attachments/{documentId}");
        Record(assertions, "collaboration.attachment.quarantine-blocked",
            upload.StatusCode == HttpStatusCode.OK &&
            beforeRelease.StatusCode == HttpStatusCode.NotFound,
            $"upload={(int)upload.StatusCode};attach={(int)beforeRelease.StatusCode}");

        var revision = ReadInt64(upload.Payload, "revision") ?? 0;
        var release = await SendAsync(client, key, scanner, HttpMethod.Post,
            $"/api/v1/documents/{documentId}/release", new { baseRevision = revision },
            "qa-col1-doc-release");
        var denied = await SendAsync(client, key, observer, HttpMethod.Put,
            $"{path}/messages/{messageId}/attachments/{documentId}");
        var attach = await SendAsync(client, key, author, HttpMethod.Put,
            $"{path}/messages/{messageId}/attachments/{documentId}");
        var repeat = await SendAsync(client, key, author, HttpMethod.Put,
            $"{path}/messages/{messageId}/attachments/{documentId}");
        Record(assertions, "collaboration.attachment.released-and-repeatable",
            release.StatusCode == HttpStatusCode.OK && denied.StatusCode == HttpStatusCode.Forbidden &&
            attach.StatusCode == HttpStatusCode.OK && repeat.StatusCode == HttpStatusCode.OK &&
            HasGuid(attach.Payload, "documentId", documentId),
            $"release={(int)release.StatusCode};denied={(int)denied.StatusCode};attach={(int)attach.StatusCode}");

        var list = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages/{messageId}/attachments");
        var generic = await SendAsync(client, key, observer, HttpMethod.Get,
            $"/api/v1/documents/{documentId}");
        var download = await DownloadFileAsync(client, key, observer,
            $"{path}/messages/{messageId}/attachments/{documentId}/content");
        var crossScope = await SendAsync(client, key, observer, HttpMethod.Get,
            $"/api/v1/projects/{Guid.NewGuid()}/collaboration/messages/{messageId}/attachments/{documentId}/content");
        Record(assertions, "collaboration.attachment.private-download-and-scope",
            list.StatusCode == HttpStatusCode.OK && list.Payload.GetArrayLength() == 1 &&
            generic.StatusCode == HttpStatusCode.NotFound &&
            download.StatusCode == HttpStatusCode.OK && download.Bytes.SequenceEqual(bytes) &&
            download.NoStore && crossScope.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"list={(int)list.StatusCode};generic={(int)generic.StatusCode};download={(int)download.StatusCode}");

        var failed = assertions.Count(item => !item.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1, stage = "col1-ms04-attachment-connected",
            passed = failed == 0, assertionCount = assertions.Count, failed, assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }
}
