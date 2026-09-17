using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly Guid VerifiedEvidenceId =
        Guid.Parse("70000000-0000-4000-8000-000000000003");

    private static readonly Guid SignatureMismatchEvidenceId =
        Guid.Parse("70000000-0000-4000-8000-000000000004");

    private static readonly Guid DeniedEvidenceId =
        Guid.Parse("70000000-0000-4000-8000-000000000005");

    private static readonly Guid MissingTargetEvidenceId =
        Guid.Parse("70000000-0000-4000-8000-000000000006");

    private static readonly Guid UnsupportedEvidenceId =
        Guid.Parse("70000000-0000-4000-8000-000000000007");

    private static readonly Guid OtherProjectId =
        Guid.Parse("33333333-3333-4333-8333-333333333399");

    private static async Task<int> VerifyFilesAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var validContent = Encoding.ASCII.GetBytes(
            "%PDF-1.7\n% PMCS deterministic QA evidence\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        var disguisedContent = Encoding.ASCII.GetBytes("MZ disguised executable content");
        var validHash = Sha256(validContent);
        var disguisedHash = Sha256(disguisedContent);
        var evidencePath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/evidence";
        var siteSupervisor = Actor("site-supervisor");
        var observer = Actor("observer");
        var financeOperator = Actor("finance-operator");
        var validRequest = SessionRequest(
            VerifiedEvidenceId,
            "qa-evidence.pdf",
            "application/pdf",
            validContent.LongLength,
            validHash);

        var missingIdempotency = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            validRequest);
        Record(
            assertions,
            "file.session.idempotency-required",
            missingIdempotency.StatusCode == HttpStatusCode.BadRequest &&
            HasProblemCode(missingIdempotency.Payload, "idempotency.key.required"),
            $"http={(int)missingIdempotency.StatusCode}");

        var observerDenied = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            SessionRequest(DeniedEvidenceId, "denied.pdf", "application/pdf", validContent.LongLength, validHash),
            "qa-v3-evidence-observer-denied");
        Record(
            assertions,
            "file.permission.observer-upload-denied",
            observerDenied.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)observerDenied.StatusCode}");

        var crossProjectRead = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"/api/v1/projects/{OtherProjectId}/evidence/");
        Record(
            assertions,
            "file.scope.other-project-denied",
            crossProjectRead.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)crossProjectRead.StatusCode}");

        var missingTarget = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            new EvidenceSessionRequest(
                MissingTargetEvidenceId,
                Guid.Parse("70000000-0000-4000-8000-000000000098"),
                null,
                "missing-parent.pdf",
                "application/pdf",
                validContent.LongLength,
                validHash,
                new DateTimeOffset(2099, 12, 29, 8, 30, 0, TimeSpan.Zero)),
            "qa-v3-evidence-missing-target");
        Record(
            assertions,
            "file.lineage.missing-parent-rejected",
            missingTarget.StatusCode == HttpStatusCode.NotFound &&
            HasProblemCode(missingTarget.Payload, "evidence.target.not_found"),
            $"http={(int)missingTarget.StatusCode}");

        var unsupportedType = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            SessionRequest(
                UnsupportedEvidenceId,
                "unsupported.exe",
                "application/x-msdownload",
                disguisedContent.LongLength,
                disguisedHash),
            "qa-v3-evidence-unsupported-type");
        Record(
            assertions,
            "file.content-type.unsupported-rejected",
            unsupportedType.StatusCode == HttpStatusCode.BadRequest &&
            HasProblemCode(unsupportedType.Payload, "evidence.content_type.unsupported"),
            $"http={(int)unsupportedType.StatusCode}");

        var signatureRequest = SessionRequest(
            SignatureMismatchEvidenceId,
            "disguised.pdf",
            "application/pdf",
            disguisedContent.LongLength,
            disguisedHash);
        var signatureSession = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            signatureRequest,
            "qa-v3-evidence-signature-session");
        Record(
            assertions,
            "file.signature.session-created",
            signatureSession.StatusCode == HttpStatusCode.Created &&
            HasSession(signatureSession.Payload, SignatureMismatchEvidenceId, "PendingUpload"),
            $"http={(int)signatureSession.StatusCode}");

        var signatureRejected = await SendBinaryAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Put,
            $"{evidencePath}/{SignatureMismatchEvidenceId}/content",
            disguisedContent,
            "application/pdf",
            "qa-v3-evidence-signature-rejected");
        Record(
            assertions,
            "file.signature.disguised-content-rejected",
            signatureRejected.StatusCode == HttpStatusCode.UnprocessableEntity &&
            HasProblemCode(signatureRejected.Payload, "evidence.content_signature.mismatch"),
            $"http={(int)signatureRejected.StatusCode}");

        var created = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            validRequest,
            "qa-v3-evidence-session");
        Record(
            assertions,
            "file.session.created",
            created.StatusCode == HttpStatusCode.Created &&
            HasSession(created.Payload, VerifiedEvidenceId, "PendingUpload") &&
            HasString(created.Payload, "uploadMethod", "PUT") &&
            HasString(created.Payload, "uploadUrl", $"{evidencePath}/{VerifiedEvidenceId}/content"),
            $"http={(int)created.StatusCode}");

        var sessionReplay = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            validRequest,
            "qa-v3-evidence-session");
        Record(
            assertions,
            "file.session.idempotent-replay",
            sessionReplay.StatusCode == HttpStatusCode.Created &&
            HasSession(sessionReplay.Payload, VerifiedEvidenceId, "PendingUpload"),
            $"http={(int)sessionReplay.StatusCode}");

        var reusedClientId = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            $"{evidencePath}/upload-sessions",
            validRequest with { Sha256 = disguisedHash },
            "qa-v3-evidence-client-id-reused");
        Record(
            assertions,
            "file.session.client-id-conflict",
            reusedClientId.StatusCode == HttpStatusCode.Conflict &&
            HasProblemCode(reusedClientId.Payload, "evidence.client_id.reused"),
            $"http={(int)reusedClientId.StatusCode}");

        var wrongContentType = await SendBinaryAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Put,
            $"{evidencePath}/{VerifiedEvidenceId}/content",
            validContent,
            "image/png",
            "qa-v3-evidence-wrong-content-type");
        Record(
            assertions,
            "file.upload.content-type-mismatch-rejected",
            wrongContentType.StatusCode == HttpStatusCode.UnprocessableEntity &&
            HasProblemCode(wrongContentType.Payload, "evidence.content_type.mismatch"),
            $"http={(int)wrongContentType.StatusCode}");

        var wrongHashContent = validContent.ToArray();
        wrongHashContent[^2] ^= 0x01;
        var wrongHash = await SendBinaryAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Put,
            $"{evidencePath}/{VerifiedEvidenceId}/content",
            wrongHashContent,
            "application/pdf",
            "qa-v3-evidence-wrong-hash");
        Record(
            assertions,
            "file.upload.sha256-mismatch-rejected",
            wrongHash.StatusCode == HttpStatusCode.UnprocessableEntity &&
            HasProblemCode(wrongHash.Payload, "evidence.sha256.mismatch"),
            $"http={(int)wrongHash.StatusCode}");

        var uploaded = await SendBinaryAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Put,
            $"{evidencePath}/{VerifiedEvidenceId}/content",
            validContent,
            "application/pdf",
            "qa-v3-evidence-content");
        Record(
            assertions,
            "file.upload.completed",
            uploaded.StatusCode == HttpStatusCode.OK &&
            HasGuid(uploaded.Payload, "id", VerifiedEvidenceId) &&
            HasString(uploaded.Payload, "status", "Uploaded") &&
            HasString(uploaded.Payload, "sha256", validHash) &&
            ReadInt64(uploaded.Payload, "revision") == 2,
            $"http={(int)uploaded.StatusCode};revision={ReadInt64(uploaded.Payload, "revision")}");

        var uploadReplay = await SendBinaryAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Put,
            $"{evidencePath}/{VerifiedEvidenceId}/content",
            validContent,
            "application/pdf",
            "qa-v3-evidence-content");
        Record(
            assertions,
            "file.upload.idempotent-replay",
            uploadReplay.StatusCode == HttpStatusCode.OK &&
            HasGuid(uploadReplay.Payload, "id", VerifiedEvidenceId) &&
            ReadInt64(uploadReplay.Payload, "revision") == 2,
            $"http={(int)uploadReplay.StatusCode};revision={ReadInt64(uploadReplay.Payload, "revision")}");

        var metadata = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"{evidencePath}/{VerifiedEvidenceId}");
        Record(
            assertions,
            "file.read.observer-metadata",
            metadata.StatusCode == HttpStatusCode.OK &&
            HasGuid(metadata.Payload, "id", VerifiedEvidenceId) &&
            HasGuid(metadata.Payload, "dailyReportId", WorkflowReportId) &&
            HasGuid(metadata.Payload, "dailyFactId", WorkflowFactId) &&
            HasString(metadata.Payload, "status", "Uploaded"),
            $"http={(int)metadata.StatusCode}");

        var pendingMetadata = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"{evidencePath}/{SignatureMismatchEvidenceId}");
        Record(
            assertions,
            "file.signature.rejected-remains-pending",
            pendingMetadata.StatusCode == HttpStatusCode.OK &&
            HasString(pendingMetadata.Payload, "status", "PendingUpload") &&
            ReadInt64(pendingMetadata.Payload, "revision") == 1,
            $"http={(int)pendingMetadata.StatusCode};revision={ReadInt64(pendingMetadata.Payload, "revision")}");

        var rejectedDownload = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"{evidencePath}/{SignatureMismatchEvidenceId}/content");
        Record(
            assertions,
            "file.signature.rejected-content-not-downloadable",
            rejectedDownload.StatusCode == HttpStatusCode.NotFound,
            $"http={(int)rejectedDownload.StatusCode}");

        var list = await SendAsync(
            client,
            key,
            observer,
            HttpMethod.Get,
            $"{evidencePath}/?dailyReportId={WorkflowReportId}&dailyFactId={WorkflowFactId}");
        Record(
            assertions,
            "file.read.filtered-list-lineage",
            list.StatusCode == HttpStatusCode.OK &&
            ContainsId(list.Payload, null, VerifiedEvidenceId) &&
            ContainsId(list.Payload, null, SignatureMismatchEvidenceId),
            $"http={(int)list.StatusCode}");

        var download = await DownloadFileAsync(
            client,
            key,
            observer,
            $"{evidencePath}/{VerifiedEvidenceId}/content");
        Record(
            assertions,
            "file.download.byte-integrity",
            download.StatusCode == HttpStatusCode.OK &&
            download.Bytes.AsSpan().SequenceEqual(validContent) &&
            string.Equals(Sha256(download.Bytes), validHash, StringComparison.Ordinal),
            $"http={(int)download.StatusCode};bytes={download.Bytes.LongLength}");
        Record(
            assertions,
            "file.download.private-response-boundary",
            string.Equals(download.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(download.FileName, "qa-evidence.pdf", StringComparison.Ordinal) &&
            download.NoStore &&
            download.NoSniff,
            $"contentType={download.ContentType};fileName={download.FileName};noStore={download.NoStore};noSniff={download.NoSniff}");

        var financeReadDenied = await SendAsync(
            client,
            key,
            financeOperator,
            HttpMethod.Get,
            $"{evidencePath}/{VerifiedEvidenceId}/content");
        Record(
            assertions,
            "file.permission.finance-read-denied",
            financeReadDenied.StatusCode == HttpStatusCode.Forbidden,
            $"http={(int)financeReadDenied.StatusCode}");

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "file-attachment-api-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            workflowReportId = WorkflowReportId,
            workflowFactId = WorkflowFactId,
            verifiedEvidenceId = VerifiedEvidenceId,
            signatureMismatchEvidenceId = SignatureMismatchEvidenceId,
            verifiedSha256 = validHash,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static EvidenceSessionRequest SessionRequest(
        Guid id,
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256) =>
        new(
            id,
            WorkflowReportId,
            WorkflowFactId,
            fileName,
            contentType,
            sizeBytes,
            sha256,
            new DateTimeOffset(2099, 12, 29, 8, 30, 0, TimeSpan.Zero));

    private static async Task<HarnessHttpResult> SendBinaryAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        HttpMethod method,
        string path,
        byte[] payload,
        string contentType,
        string idempotencyKey)
    {
        using var request = new HttpRequestMessage(method, path);
        AddQaHeaders(request, key, actor, idempotencyKey);
        request.Content = new ByteArrayContent(payload);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await client.SendAsync(request);
        return await ReadJsonResultAsync(response);
    }

    private static async Task<FileDownloadResult> DownloadFileAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddQaHeaders(request, key, actor, null);
        using var response = await client.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"');
        var noSniff = response.Headers.TryGetValues("X-Content-Type-Options", out var values) &&
            values.Any(value => string.Equals(value, "nosniff", StringComparison.OrdinalIgnoreCase));
        return new FileDownloadResult(
            response.StatusCode,
            bytes,
            response.Content.Headers.ContentType?.MediaType,
            fileName,
            response.Headers.CacheControl?.NoStore == true,
            noSniff);
    }

    private static async Task<HarnessHttpResult> ReadJsonResultAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return new HarnessHttpResult(response.StatusCode, default);
        }

        using var document = JsonDocument.Parse(body);
        return new HarnessHttpResult(response.StatusCode, document.RootElement.Clone());
    }

    private static void AddQaHeaders(
        HttpRequestMessage request,
        string key,
        PmcsTestActor actor,
        string? idempotencyKey)
    {
        request.Headers.Add("X-Pmcs-QA-Key", key);
        request.Headers.Add("X-Tenant-Id", PmcsTestDataSet.TenantId.ToString());
        request.Headers.Add("X-User-Id", actor.UserId.ToString());
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }
    }

    private static bool HasSession(JsonElement payload, Guid evidenceId, string status) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("evidence", out var evidence) &&
        HasGuid(evidence, "id", evidenceId) &&
        HasString(evidence, "status", status);

    private static bool HasProblemCode(JsonElement payload, string code) =>
        HasString(payload, "code", code);

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed record EvidenceSessionRequest(
        Guid ClientGeneratedId,
        Guid DailyReportId,
        Guid? DailyFactId,
        string OriginalFileName,
        string ContentType,
        long SizeBytes,
        string Sha256,
        DateTimeOffset CapturedAtDevice);

    private sealed record FileDownloadResult(
        HttpStatusCode StatusCode,
        byte[] Bytes,
        string? ContentType,
        string? FileName,
        bool NoStore,
        bool NoSniff);
}
