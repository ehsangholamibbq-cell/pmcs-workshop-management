using System.Net;
using System.Text;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyCollaborationFieldEvidenceConversionsAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var projectId = PmcsTestDataSet.ProjectId;
        var path = $"/api/v1/projects/{projectId}/collaboration";
        var reportPath = $"/api/v1/projects/{projectId}/daily-reports";
        var supervisor = Actor("site-supervisor");
        var observer = Actor("observer");
        var scanner = Actor("qa-super-admin");
        var assertions = new List<VerificationAssertion>();
        var reportId = Guid.Parse("ca110000-0000-4000-8000-000000000401");
        var factId = Guid.Parse("ca110000-0000-4000-8000-000000000403");
        var message = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages", new
            {
                clientMessageId = Guid.Parse("ca110000-0000-4000-8000-000000000402"),
                body = "مشاهدهٔ مستقل کارگاه برای گزارش روزانه"
            }, "qa-col1-field-message");
        var report = await SendAsync(client, key, supervisor, HttpMethod.Post,
            reportPath, new { clientGeneratedId = reportId, reportDate = "2099-01-03",
                locationName = "کارگاه", narrative = "گزارش مستقل تبدیل گفت‌وگو" },
            "qa-col1-field-report");
        Record(assertions, "collaboration.field.draft-owner-target",
            message.StatusCode == HttpStatusCode.Created &&
            report.StatusCode == HttpStatusCode.Created &&
            ReadInt64(report.Payload, "revision") == 1,
            $"message={(int)message.StatusCode};report={(int)report.StatusCode}");
        if (message.StatusCode != HttpStatusCode.Created ||
            report.StatusCode != HttpStatusCode.Created) return Report(assertions);
        var messageId = message.Payload.GetProperty("id").GetGuid();
        var factRequest = new
        {
            destinationId = factId, destinationType = "DailyFact", confirmed = true,
            baseRevision = 1, details = new { reportId, baseReportRevision = 1,
                kind = "Note", locationId = PmcsTestDataSet.RootLocationId }
        };
        var fact = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", factRequest, "qa-col1-field-fact");
        var replay = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", factRequest, "qa-col1-field-fact");
        var owner = await SendAsync(client, key, supervisor, HttpMethod.Get,
            $"{reportPath}/{reportId}");
        Record(assertions, "collaboration.field.fact-owner-revision-and-idempotence",
            fact.StatusCode == HttpStatusCode.Created &&
            replay.StatusCode == HttpStatusCode.Created &&
            owner.StatusCode == HttpStatusCode.OK &&
            owner.Payload.GetProperty("status").GetString() == "Draft" &&
            ReadInt64(owner.Payload, "revision") == 2 &&
            owner.Payload.GetProperty("facts").EnumerateArray().Any(item =>
                HasGuid(item, "id", factId) &&
                item.GetProperty("referenceCode").GetString() == $"chat:{messageId:N}:v1"),
            $"fact={(int)fact.StatusCode};replay={(int)replay.StatusCode};owner={(int)owner.StatusCode}");

        var documentId = Guid.Parse("ca110000-0000-4000-8000-000000000404");
        var evidenceId = Guid.Parse("ca110000-0000-4000-8000-000000000405");
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n% PMCS field evidence conversion\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        var sha256 = Sha256(bytes);
        var withoutFile = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = evidenceId, destinationType = "Evidence",
                baseRevision = 1, confirmed = true,
                details = new { dailyReportId = reportId, dailyFactId = factId }
            }, "qa-col1-field-evidence-without-file");
        var session = await SendAsync(client, key, supervisor, HttpMethod.Post,
            "/api/v1/upload-sessions", new
            {
                clientGeneratedId = documentId, projectId, ownerType = "ProjectChat",
                ownerId = messageId, originalFileName = "field-source.pdf",
                contentType = "application/pdf", sizeBytes = bytes.LongLength,
                sha256, classification = "Internal", retentionPolicy = "Standard",
                retainUntil = (DateTimeOffset?)null, legalHold = false
            }, "qa-col1-field-document-session");
        var upload = await SendBinaryAsync(client, key, supervisor, HttpMethod.Put,
            $"/api/v1/documents/{documentId}/content", bytes,
            "application/pdf", "qa-col1-field-document-upload");
        var release = await SendAsync(client, key, scanner, HttpMethod.Post,
            $"/api/v1/documents/{documentId}/release",
            new { baseRevision = ReadInt64(upload.Payload, "revision") ?? 0 },
            "qa-col1-field-document-release");
        var attach = await SendAsync(client, key, supervisor, HttpMethod.Put,
            $"{path}/messages/{messageId}/attachments/{documentId}");
        Record(assertions, "collaboration.field.evidence-quarantine-and-owner-file",
            withoutFile.StatusCode == HttpStatusCode.UnprocessableEntity &&
            session.StatusCode == HttpStatusCode.Created &&
            upload.StatusCode == HttpStatusCode.OK &&
            release.StatusCode == HttpStatusCode.OK &&
            attach.StatusCode == HttpStatusCode.OK,
            $"unreleased={(int)withoutFile.StatusCode};session={(int)session.StatusCode};upload={(int)upload.StatusCode};release={(int)release.StatusCode};attach={(int)attach.StatusCode}");
        var evidence = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages/{messageId}/conversions", new
            {
                destinationId = evidenceId, destinationType = "Evidence",
                baseRevision = 1, confirmed = true, documentIds = new[] { documentId },
                details = new { dailyReportId = reportId, dailyFactId = factId }
            }, "qa-col1-field-evidence");
        var official = await SendAsync(client, key, observer, HttpMethod.Get,
            $"/api/v1/projects/{projectId}/evidence/{evidenceId}");
        var before = await DownloadFileAsync(client, key, observer,
            $"/api/v1/projects/{projectId}/evidence/{evidenceId}/content");
        var deleted = await SendAsync(client, key, supervisor, HttpMethod.Post,
            $"{path}/messages/{messageId}/delete", new { baseRevision = 1 },
            "qa-col1-field-delete");
        var after = await DownloadFileAsync(client, key, observer,
            $"/api/v1/projects/{projectId}/evidence/{evidenceId}/content");
        var chatAfter = await SendAsync(client, key, observer, HttpMethod.Get,
            $"{path}/messages/{messageId}/attachments/{documentId}/content");
        var lineage = await SendAsync(client, key, supervisor, HttpMethod.Get,
            $"{path}/messages/{messageId}/conversions");
        Record(assertions, "collaboration.field.official-evidence-lineage-and-tombstone",
            evidence.StatusCode == HttpStatusCode.Created &&
            official.StatusCode == HttpStatusCode.OK &&
            HasGuid(official.Payload, "sourceMessageId", messageId) &&
            HasGuid(official.Payload, "sourceDocumentId", documentId) &&
            official.Payload.GetProperty("sha256").GetString() == sha256 &&
            official.Payload.GetProperty("status").GetString() == "Uploaded" &&
            before.StatusCode == HttpStatusCode.OK && before.Bytes.SequenceEqual(bytes) &&
            deleted.StatusCode == HttpStatusCode.OK &&
            after.StatusCode == HttpStatusCode.OK && after.Bytes.SequenceEqual(bytes) && after.NoStore &&
            chatAfter.StatusCode == HttpStatusCode.NotFound &&
            lineage.StatusCode == HttpStatusCode.OK && lineage.Payload.GetArrayLength() == 2,
            $"evidence={(int)evidence.StatusCode};official={(int)official.StatusCode};before={(int)before.StatusCode};delete={(int)deleted.StatusCode};after={(int)after.StatusCode};lineage={(int)lineage.StatusCode}");
        return Report(assertions);

        static int Report(List<VerificationAssertion> assertions)
        {
            var failed = assertions.Count(item => !item.Passed);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                contractVersion = 1, stage = "col1-ms05-field-evidence-connected",
                passed = failed == 0, assertionCount = assertions.Count, failed, assertions
            }, JsonOptions));
            return failed == 0 ? 0 : 1;
        }
    }
}
