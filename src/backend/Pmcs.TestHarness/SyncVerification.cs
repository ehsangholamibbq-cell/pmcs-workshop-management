using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private const string PrimaryDeviceId = "pmcs-qa-harness";
    private const string NegativeDeviceId = "pmcs-qa-harness";
    private const string PrimaryOperationId = "01K4ZQ9G5V7Q0M8M2V4R6D8F1D";
    private const string ConflictOperationId = "01K4ZQ9G5V7Q0M8M2V4R6D8F1E";
    private const string InvalidEnvelopeOperationId = "01K4ZQ9G5V7Q0M8M2V4R6D8F1F";
    private const string CrossProjectOperationId = "01K4ZQ9G5V7Q0M8M2V4R6D8F1G";
    private const string ReplacementOperationId = "01K4ZQ9G5V7Q0M8M2V4R6D8F1H";
    private const string PrimaryCorrelationId = "qa-sync-primary-correlation";
    private const string SecondUserCorrelationId = "qa-sync-second-user-correlation";
    private const string ConflictCorrelationId = "qa-sync-conflict-correlation";

    private static readonly Guid PrimarySyncReportId =
        Guid.Parse("70000000-0000-4000-8000-000000000008");

    private static readonly Guid PrimarySyncFactId =
        Guid.Parse("70000000-0000-4000-8000-000000000009");

    private static readonly Guid SecondUserSyncReportId =
        Guid.Parse("70000000-0000-4000-8000-000000000010");

    private static readonly Guid SecondUserSyncFactId =
        Guid.Parse("70000000-0000-4000-8000-000000000011");

    private static readonly Guid ConflictSyncReportId =
        Guid.Parse("70000000-0000-4000-8000-000000000012");

    private static readonly Guid ConflictSyncFactId =
        Guid.Parse("70000000-0000-4000-8000-000000000013");

    private static readonly Guid OtherSyncProjectId =
        Guid.Parse("33333333-3333-4333-8333-333333333398");

    private static async Task<int> VerifySyncAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = CreateClient();
        var assertions = new List<VerificationAssertion>();
        var siteSupervisor = Actor("site-supervisor");
        var technicalOffice = Actor("technical-office");
        var observer = Actor("observer");
        var projectManager = PmcsTestDataSet.QaSuperAdministrator;

        var incompatible = await OpenSyncSessionAsync(
            client, key, siteSupervisor, PrimaryDeviceId, protocolVersion: 2);
        Record(
            assertions,
            "sync.compatibility.unsupported-protocol",
            incompatible.StatusCode == HttpStatusCode.UpgradeRequired &&
            HasProblemCode(incompatible.Payload, "sync.protocol.unsupported"),
            $"http={(int)incompatible.StatusCode}");

        var observerDenied = await OpenSyncSessionAsync(
            client, key, observer, PrimaryDeviceId);
        Record(
            assertions,
            "sync.permission.observer-handshake-denied",
            observerDenied.StatusCode == HttpStatusCode.Forbidden &&
            HasProblemCode(observerDenied.Payload, "sync.project.access_denied"),
            $"http={(int)observerDenied.StatusCode}");

        var firstHandshake = await OpenSyncSessionAsync(
            client, key, siteSupervisor, PrimaryDeviceId, pendingOperations: 1);
        var firstSessionId = ReadGuid(firstHandshake.Payload, "sessionId");
        var firstLeaseId = ReadNestedGuid(firstHandshake.Payload, "lease", "leaseId");
        var firstAuthorizationVersion = ReadNestedInt64(
            firstHandshake.Payload,
            "lease",
            "authorizationVersion") ?? 0;
        var firstLeaseIssuedAt = ReadNestedDateTimeOffset(
            firstHandshake.Payload,
            "lease",
            "issuedAt") ?? DateTimeOffset.UtcNow;
        Record(
            assertions,
            "sync.handshake.initial-lease",
            firstHandshake.StatusCode == HttpStatusCode.OK &&
            firstSessionId != Guid.Empty &&
            firstLeaseId != Guid.Empty &&
            firstAuthorizationVersion >= 1 &&
            HasString(firstHandshake.Payload, "policyVersion", "sync-policy-v3") &&
            ContainsString(firstHandshake.Payload, "allowedOperations", "CaptureDailyReportFact"),
            $"http={(int)firstHandshake.StatusCode};authorizationVersion={firstAuthorizationVersion}");

        var reconnectHandshake = await OpenSyncSessionAsync(
            client, key, siteSupervisor, PrimaryDeviceId, pendingOperations: 1);
        var primarySessionId = ReadGuid(reconnectHandshake.Payload, "sessionId");
        var activeLeaseId = ReadNestedGuid(reconnectHandshake.Payload, "lease", "leaseId");
        var activeAuthorizationVersion = ReadNestedInt64(
            reconnectHandshake.Payload,
            "lease",
            "authorizationVersion") ?? 0;
        Record(
            assertions,
            "sync.reconnect.superseding-lease",
            reconnectHandshake.StatusCode == HttpStatusCode.OK &&
            primarySessionId != Guid.Empty &&
            primarySessionId != firstSessionId &&
            activeLeaseId != Guid.Empty &&
            activeLeaseId != firstLeaseId &&
            activeAuthorizationVersion == firstAuthorizationVersion + 1,
            $"http={(int)reconnectHandshake.StatusCode};authorizationVersion={activeAuthorizationVersion}");

        var primaryOperation = SyncOperation(
            PrimaryOperationId,
            PrimarySyncReportId,
            PrimarySyncFactId,
            new DateOnly(2099, 12, 30),
            "QA offline fact captured before reconnect",
            firstLeaseId,
            firstAuthorizationVersion,
            firstLeaseIssuedAt,
            PrimaryCorrelationId);
        var missingSession = await SendSyncAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            "/api/v1/sync/operations",
            new { deviceId = PrimaryDeviceId, operations = new[] { primaryOperation } });
        Record(
            assertions,
            "sync.session.required",
            missingSession.StatusCode == HttpStatusCode.Unauthorized &&
            HasProblemCode(missingSession.Payload, "sync.session.required"),
            $"http={(int)missingSession.StatusCode}");

        var wrongActorSession = await SendSyncAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Get,
            $"/api/v1/sync/pull?projectId={PmcsTestDataSet.ProjectId}&limit=100",
            sessionId: primarySessionId);
        Record(
            assertions,
            "sync.session.actor-scope-enforced",
            wrongActorSession.StatusCode == HttpStatusCode.Unauthorized &&
            HasProblemCode(wrongActorSession.Payload, "sync.session.invalid_or_expired"),
            $"http={(int)wrongActorSession.StatusCode}");

        var applied = await PushOperationAsync(
            client, key, siteSupervisor, PrimaryDeviceId, primarySessionId, primaryOperation);
        Record(
            assertions,
            "sync.reconnect.superseded-lease-operation-applied",
            applied.StatusCode == HttpStatusCode.OK &&
            HasSyncOperation(
                applied.Payload,
                PrimaryOperationId,
                "Applied",
                expectedReplay: false,
                expectedEntityId: PrimarySyncReportId),
            SyncOperationDetail(applied, PrimaryOperationId));

        var replay = await PushOperationAsync(
            client, key, siteSupervisor, PrimaryDeviceId, primarySessionId, primaryOperation);
        Record(
            assertions,
            "sync.retry.identical-operation-replayed",
            replay.StatusCode == HttpStatusCode.OK &&
            HasSyncOperation(
                replay.Payload,
                PrimaryOperationId,
                "Applied",
                expectedReplay: true,
                expectedEntityId: PrimarySyncReportId),
            SyncOperationDetail(replay, PrimaryOperationId));

        var reused = await PushOperationAsync(
            client,
            key,
            siteSupervisor,
            PrimaryDeviceId,
            primarySessionId,
            primaryOperation with
            {
                Payload = primaryOperation.Payload with
                {
                    Description = "A changed payload must not replace the accepted operation."
                }
            });
        Record(
            assertions,
            "sync.retry.operation-id-reuse-rejected",
            reused.StatusCode == HttpStatusCode.OK &&
            HasSyncOperation(
                reused.Payload,
                PrimaryOperationId,
                "Rejected",
                expectedReplay: false,
                expectedCode: "sync.operation.reused"),
            SyncOperationDetail(reused, PrimaryOperationId));

        var secondUserHandshake = await OpenSyncSessionAsync(
            client, key, technicalOffice, PrimaryDeviceId, pendingOperations: 2);
        var secondUserSessionId = ReadGuid(secondUserHandshake.Payload, "sessionId");
        var secondUserLeaseId = ReadNestedGuid(secondUserHandshake.Payload, "lease", "leaseId");
        var secondUserAuthorizationVersion = ReadNestedInt64(
            secondUserHandshake.Payload,
            "lease",
            "authorizationVersion") ?? 0;
        var secondUserLeaseIssuedAt = ReadNestedDateTimeOffset(
            secondUserHandshake.Payload,
            "lease",
            "issuedAt") ?? DateTimeOffset.UtcNow;
        Record(
            assertions,
            "sync.multi-user.same-device-session-isolated",
            secondUserHandshake.StatusCode == HttpStatusCode.OK &&
            secondUserSessionId != Guid.Empty &&
            secondUserLeaseId != Guid.Empty,
            $"http={(int)secondUserHandshake.StatusCode}");

        var secondUserOperation = SyncOperation(
            PrimaryOperationId,
            SecondUserSyncReportId,
            SecondUserSyncFactId,
            new DateOnly(2099, 12, 31),
            "Independent second-user operation with the same device and operation id",
            secondUserLeaseId,
            secondUserAuthorizationVersion,
            secondUserLeaseIssuedAt,
            SecondUserCorrelationId);
        var secondUserApplied = await PushOperationAsync(
            client,
            key,
            technicalOffice,
            PrimaryDeviceId,
            secondUserSessionId,
            secondUserOperation);
        Record(
            assertions,
            "sync.multi-user.idempotency-identity-isolated",
            secondUserApplied.StatusCode == HttpStatusCode.OK &&
            HasSyncOperation(
                secondUserApplied.Payload,
                PrimaryOperationId,
                "Applied",
                expectedReplay: false,
                expectedEntityId: SecondUserSyncReportId),
            SyncOperationDetail(secondUserApplied, PrimaryOperationId));

        var conflictOperation = SyncOperation(
            ConflictOperationId,
            ConflictSyncReportId,
            ConflictSyncFactId,
            new DateOnly(2099, 12, 30),
            "Concurrent second-user intent for an existing report date",
            secondUserLeaseId,
            secondUserAuthorizationVersion,
            secondUserLeaseIssuedAt,
            ConflictCorrelationId,
            localSequence: 2);
        var conflictResult = await PushOperationAsync(
            client,
            key,
            technicalOffice,
            PrimaryDeviceId,
            secondUserSessionId,
            conflictOperation);
        var conflictId = ReadSyncOperationGuid(conflictResult.Payload, ConflictOperationId, "conflictId");
        Record(
            assertions,
            "sync.conflict.concurrent-user-detected",
            conflictResult.StatusCode == HttpStatusCode.OK &&
            conflictId != Guid.Empty &&
            HasSyncOperation(
                conflictResult.Payload,
                ConflictOperationId,
                "Conflict",
                expectedReplay: false,
                expectedCode: "daily_report.date.duplicate"),
            SyncOperationDetail(conflictResult, ConflictOperationId));

        var siteConflictList = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Get,
            $"/api/v1/sync/conflicts?projectId={PmcsTestDataSet.ProjectId}");
        Record(
            assertions,
            "sync.conflict.non-manager-actor-isolation",
            siteConflictList.StatusCode == HttpStatusCode.OK &&
            !ContainsArrayItem(siteConflictList.Payload, "conflictId", conflictId.ToString()),
            $"http={(int)siteConflictList.StatusCode}");

        var ownerConflictList = await SendAsync(
            client,
            key,
            technicalOffice,
            HttpMethod.Get,
            $"/api/v1/sync/conflicts?projectId={PmcsTestDataSet.ProjectId}");
        Record(
            assertions,
            "sync.conflict.owner-can-read",
            ownerConflictList.StatusCode == HttpStatusCode.OK &&
            ContainsArrayItem(ownerConflictList.Payload, "conflictId", conflictId.ToString()),
            $"http={(int)ownerConflictList.StatusCode}");

        var managerConflictList = await SendAsync(
            client,
            key,
            projectManager,
            HttpMethod.Get,
            $"/api/v1/sync/conflicts?projectId={PmcsTestDataSet.ProjectId}");
        var conflictRevision = ReadArrayItemInt64(
            managerConflictList.Payload,
            "conflictId",
            conflictId.ToString(),
            "revision") ?? -1;
        Record(
            assertions,
            "sync.conflict.manager-can-read-all",
            managerConflictList.StatusCode == HttpStatusCode.OK && conflictRevision >= 0,
            $"http={(int)managerConflictList.StatusCode};revision={conflictRevision}");

        var resolutionRequest = new
        {
            baseRevision = conflictRevision,
            resolution = "KeepServer",
            replacementOperationId = (string?)null,
            comment = "QA independent concurrent-user resolution"
        };
        var resolved = await SendAsync(
            client,
            key,
            projectManager,
            HttpMethod.Post,
            $"/api/v1/sync/conflicts/{conflictId}/resolve",
            resolutionRequest);
        Record(
            assertions,
            "sync.conflict.manager-resolution",
            resolved.StatusCode == HttpStatusCode.OK &&
            HasString(resolved.Payload, "status", "Resolved") &&
            HasString(resolved.Payload, "resolutionType", "KeepServer") &&
            HasGuid(resolved.Payload, "resolvedBy", projectManager.UserId),
            $"http={(int)resolved.StatusCode};revision={ReadInt64(resolved.Payload, "revision")}");

        var resolutionReplay = await SendAsync(
            client,
            key,
            projectManager,
            HttpMethod.Post,
            $"/api/v1/sync/conflicts/{conflictId}/resolve",
            resolutionRequest);
        Record(
            assertions,
            "sync.conflict.resolution-idempotent-replay",
            resolutionReplay.StatusCode == HttpStatusCode.OK &&
            HasString(resolutionReplay.Payload, "resolutionType", "KeepServer"),
            $"http={(int)resolutionReplay.StatusCode}");

        var contradictoryResolution = await SendAsync(
            client,
            key,
            projectManager,
            HttpMethod.Post,
            $"/api/v1/sync/conflicts/{conflictId}/resolve",
            new
            {
                baseRevision = conflictRevision,
                resolution = "Reapply",
                replacementOperationId = ReplacementOperationId,
                comment = "A contradictory replay must be rejected."
            });
        Record(
            assertions,
            "sync.conflict.contradictory-replay-rejected",
            contradictoryResolution.StatusCode == HttpStatusCode.Conflict &&
            HasProblemCode(contradictoryResolution.Payload, "sync.conflict.already_resolved"),
            $"http={(int)contradictoryResolution.StatusCode}");

        var pulled = await SendSyncAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Get,
            $"/api/v1/sync/pull?projectId={PmcsTestDataSet.ProjectId}&limit=100",
            sessionId: primarySessionId);
        var checkpointOffer = ReadGuid(pulled.Payload, "checkpointOffer");
        var serverWatermark = ReadInt64(pulled.Payload, "serverWatermark") ?? -1;
        Record(
            assertions,
            "sync.pull.project-change-feed",
            pulled.StatusCode == HttpStatusCode.OK &&
            checkpointOffer != Guid.Empty &&
            serverWatermark >= 2 &&
            ContainsArrayGuid(pulled.Payload, "changes", "entityId", PrimarySyncReportId) &&
            ContainsArrayGuid(pulled.Payload, "changes", "entityId", SecondUserSyncReportId),
            $"http={(int)pulled.StatusCode};watermark={serverWatermark}");

        var checkpointRequest = new
        {
            projectId = PmcsTestDataSet.ProjectId,
            checkpointOffer
        };
        var checkpoint = await SendSyncAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            "/api/v1/sync/checkpoints",
            checkpointRequest,
            primarySessionId);
        var checkpointSequence = ReadInt64(checkpoint.Payload, "sequence") ?? -1;
        Record(
            assertions,
            "sync.checkpoint.advanced-after-apply",
            checkpoint.StatusCode == HttpStatusCode.OK &&
            checkpointSequence == serverWatermark &&
            HasString(checkpoint.Payload, "checkpoint", checkpointOffer.ToString()),
            $"http={(int)checkpoint.StatusCode};sequence={checkpointSequence}");

        var checkpointReplay = await SendSyncAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Post,
            "/api/v1/sync/checkpoints",
            checkpointRequest,
            primarySessionId);
        Record(
            assertions,
            "sync.checkpoint.acknowledgement-replay",
            checkpointReplay.StatusCode == HttpStatusCode.OK &&
            ReadInt64(checkpointReplay.Payload, "sequence") == checkpointSequence,
            $"http={(int)checkpointReplay.StatusCode};sequence={ReadInt64(checkpointReplay.Payload, "sequence")}");

        var diagnostics = await SendAsync(
            client,
            key,
            siteSupervisor,
            HttpMethod.Get,
            $"/api/v1/sync/diagnostics?projectId={PmcsTestDataSet.ProjectId}&deviceId={PrimaryDeviceId}");
        Record(
            assertions,
            "sync.recovery.healthy-local-server-alignment",
            diagnostics.StatusCode == HttpStatusCode.OK &&
            HasString(diagnostics.Payload, "recoveryState", "Healthy") &&
            ReadInt64(diagnostics.Payload, "checkpointLag") == 0 &&
            (ReadInt64(diagnostics.Payload, "recentReplayCount") ?? 0) >= 1 &&
            ReadInt64(diagnostics.Payload, "recentRejectedOperationCount") == 0,
            $"http={(int)diagnostics.StatusCode};state={ReadString(diagnostics.Payload, "recoveryState")};lag={ReadInt64(diagnostics.Payload, "checkpointLag")}");

        var negativeHandshake = await OpenSyncSessionAsync(
            client, key, projectManager, NegativeDeviceId, pendingOperations: 2);
        var negativeSessionId = ReadGuid(negativeHandshake.Payload, "sessionId");
        var negativeLeaseId = ReadNestedGuid(negativeHandshake.Payload, "lease", "leaseId");
        var negativeAuthorizationVersion = ReadNestedInt64(
            negativeHandshake.Payload,
            "lease",
            "authorizationVersion") ?? 0;
        var negativeLeaseIssuedAt = ReadNestedDateTimeOffset(
            negativeHandshake.Payload,
            "lease",
            "issuedAt") ?? DateTimeOffset.UtcNow;
        var invalidEnvelope = SyncOperation(
            InvalidEnvelopeOperationId,
            Guid.Parse("70000000-0000-4000-8000-000000000014"),
            Guid.Parse("70000000-0000-4000-8000-000000000015"),
            new DateOnly(2100, 1, 1),
            "Invalid envelope probe",
            negativeLeaseId,
            negativeAuthorizationVersion,
            negativeLeaseIssuedAt,
            "qa-sync-invalid-envelope") with
        {
            EntityType = new string('X', 121)
        };
        var crossProject = SyncOperation(
            CrossProjectOperationId,
            Guid.Parse("70000000-0000-4000-8000-000000000016"),
            Guid.Parse("70000000-0000-4000-8000-000000000017"),
            new DateOnly(2100, 1, 2),
            "Cross-project envelope probe",
            negativeLeaseId,
            negativeAuthorizationVersion,
            negativeLeaseIssuedAt,
            "qa-sync-cross-project",
            localSequence: 2) with
        {
            ProjectId = OtherSyncProjectId
        };
        var rejectedEnvelopes = await SendSyncAsync(
            client,
            key,
            projectManager,
            HttpMethod.Post,
            "/api/v1/sync/operations",
            new { deviceId = NegativeDeviceId, operations = new[] { invalidEnvelope, crossProject } },
            negativeSessionId);
        Record(
            assertions,
            "sync.envelope.invalid-inputs-rejected-without-server-error",
            rejectedEnvelopes.StatusCode == HttpStatusCode.OK &&
            HasSyncOperation(
                rejectedEnvelopes.Payload,
                InvalidEnvelopeOperationId,
                "Rejected",
                expectedReplay: false,
                expectedCode: "sync.operation.envelope.invalid") &&
            HasSyncOperation(
                rejectedEnvelopes.Payload,
                CrossProjectOperationId,
                "Rejected",
                expectedReplay: false,
                expectedCode: "sync.operation.envelope.invalid"),
            $"http={(int)rejectedEnvelopes.StatusCode}");

        var devices = await SendAsync(
            client,
            key,
            projectManager,
            HttpMethod.Get,
            "/api/v1/sync/devices");
        var negativeRegistrationId = ReadArrayItemGuid(
            devices.Payload,
            "deviceId",
            NegativeDeviceId,
            "registrationId");
        var negativeDeviceRevision = ReadArrayItemInt64(
            devices.Payload,
            "deviceId",
            NegativeDeviceId,
            "revision") ?? -1;
        Record(
            assertions,
            "sync.device.registration-readable",
            devices.StatusCode == HttpStatusCode.OK &&
            negativeRegistrationId != Guid.Empty &&
            negativeDeviceRevision >= 0,
            $"http={(int)devices.StatusCode};revision={negativeDeviceRevision}");

        var revoked = await SendAsync(
            client,
            key,
            projectManager,
            HttpMethod.Post,
            $"/api/v1/sync/devices/{negativeRegistrationId}/revoke",
            new { baseRevision = negativeDeviceRevision, reason = "QA deterministic revocation probe" });
        Record(
            assertions,
            "sync.device.self-revocation",
            revoked.StatusCode == HttpStatusCode.NoContent,
            $"http={(int)revoked.StatusCode}");

        var revokedSessionPush = await PushOperationAsync(
            client,
            key,
            projectManager,
            NegativeDeviceId,
            negativeSessionId,
            invalidEnvelope);
        Record(
            assertions,
            "sync.device.revocation-closes-session",
            revokedSessionPush.StatusCode == HttpStatusCode.Unauthorized &&
            HasProblemCode(revokedSessionPush.Payload, "sync.session.invalid_or_expired"),
            $"http={(int)revokedSessionPush.StatusCode}");

        var revokedHandshake = await OpenSyncSessionAsync(
            client, key, projectManager, NegativeDeviceId, pendingOperations: 1);
        Record(
            assertions,
            "sync.device.revoked-reconnect-requires-purge-review",
            revokedHandshake.StatusCode == HttpStatusCode.Forbidden &&
            HasProblemCode(revokedHandshake.Payload, "sync.device.revoked") &&
            HasBoolean(revokedHandshake.Payload, "purgeRequired", expected: true) &&
            HasBoolean(revokedHandshake.Payload, "pendingDataMustBeReviewed", expected: true),
            $"http={(int)revokedHandshake.StatusCode}");

        var revokedDiagnostics = await SendAsync(
            client,
            key,
            projectManager,
            HttpMethod.Get,
            $"/api/v1/sync/diagnostics?projectId={PmcsTestDataSet.ProjectId}&deviceId={NegativeDeviceId}");
        Record(
            assertions,
            "sync.recovery.revoked-device-diagnostics",
            revokedDiagnostics.StatusCode == HttpStatusCode.OK &&
            HasString(revokedDiagnostics.Payload, "deviceStatus", "Revoked") &&
            HasString(revokedDiagnostics.Payload, "recoveryState", "DeviceRevoked"),
            $"http={(int)revokedDiagnostics.StatusCode};state={ReadString(revokedDiagnostics.Payload, "recoveryState")}");

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "offline-sync-api-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            primaryDeviceId = PrimaryDeviceId,
            negativeDeviceId = NegativeDeviceId,
            primaryOperationId = PrimaryOperationId,
            conflictOperationId = ConflictOperationId,
            primarySyncReportId = PrimarySyncReportId,
            secondUserSyncReportId = SecondUserSyncReportId,
            conflictId,
            serverWatermark,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static Task<HarnessHttpResult> OpenSyncSessionAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string deviceId,
        int pendingOperations = 0,
        int protocolVersion = 3) =>
        SendAsync(
            client,
            key,
            actor,
            HttpMethod.Post,
            "/api/v1/sync/handshake",
            new
            {
                deviceId,
                projectId = PmcsTestDataSet.ProjectId,
                deviceName = $"QA {deviceId}",
                platform = "PMCS.TestHarness",
                appVersion = "0.2.0",
                protocolVersion,
                localSchemaVersion = 6,
                lastCheckpoint = (string?)null,
                deviceTime = DateTimeOffset.UtcNow,
                queue = new
                {
                    pendingOperations,
                    pendingAttachments = 0,
                    pendingAttachmentBytes = 0,
                    oldestOperationAt = pendingOperations > 0 ? DateTimeOffset.UtcNow : (DateTimeOffset?)null
                }
            });

    private static Task<HarnessHttpResult> PushOperationAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        string deviceId,
        Guid sessionId,
        SyncOperationProbe operation) =>
        SendSyncAsync(
            client,
            key,
            actor,
            HttpMethod.Post,
            "/api/v1/sync/operations",
            new { deviceId, operations = new[] { operation } },
            sessionId);

    private static async Task<HarnessHttpResult> SendSyncAsync(
        HttpClient client,
        string key,
        PmcsTestActor actor,
        HttpMethod method,
        string path,
        object? payload = null,
        Guid? sessionId = null)
    {
        using var request = new HttpRequestMessage(method, path);
        AddQaHeaders(request, key, actor, null);
        if (sessionId.HasValue)
        {
            request.Headers.Add("X-Pmcs-Sync-Session", sessionId.Value.ToString());
        }
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        using var response = await client.SendAsync(request);
        return await ReadJsonResultAsync(response);
    }

    private static SyncOperationProbe SyncOperation(
        string operationId,
        Guid reportId,
        Guid factId,
        DateOnly reportDate,
        string description,
        Guid leaseId,
        long authorizationVersion,
        DateTimeOffset createdAtDevice,
        string correlationId,
        long localSequence = 1) =>
        new(
            operationId,
            PmcsTestDataSet.ProjectId,
            "DailyReport",
            reportId,
            "CaptureDailyReportFact",
            null,
            1,
            createdAtDevice,
            new SyncFactPayloadProbe(
                factId,
                reportDate,
                null,
                "Note",
                description,
                PmcsTestDataSet.RootLocationId),
            leaseId,
            authorizationVersion,
            localSequence,
            [],
            correlationId,
            210);

    private static bool HasSyncOperation(
        JsonElement payload,
        string operationId,
        string status,
        bool expectedReplay,
        string? expectedCode = null,
        Guid? expectedEntityId = null)
    {
        if (!TryFindArrayItem(payload, "operations", "operationId", operationId, out var operation) ||
            !HasString(operation, "status", status) ||
            !HasBoolean(operation, "wasReplay", expectedReplay))
        {
            return false;
        }

        if (expectedCode is not null && !HasString(operation, "code", expectedCode))
        {
            return false;
        }

        return !expectedEntityId.HasValue || HasGuid(operation, "entityId", expectedEntityId.Value);
    }

    private static string SyncOperationDetail(HarnessHttpResult result, string operationId)
    {
        if (!TryFindArrayItem(result.Payload, "operations", "operationId", operationId, out var operation))
        {
            return $"http={(int)result.StatusCode};operation=missing";
        }

        return $"http={(int)result.StatusCode};status={ReadString(operation, "status")};code={ReadString(operation, "code")};replay={ReadBoolean(operation, "wasReplay")}";
    }

    private static Guid ReadSyncOperationGuid(JsonElement payload, string operationId, string property) =>
        TryFindArrayItem(payload, "operations", "operationId", operationId, out var operation)
            ? ReadGuid(operation, property)
            : Guid.Empty;

    private static bool TryFindArrayItem(
        JsonElement payload,
        string arrayProperty,
        string matchProperty,
        string expected,
        out JsonElement item)
    {
        item = default;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(arrayProperty, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var candidate in array.EnumerateArray())
        {
            if (TryReadString(candidate, matchProperty, out var actual) &&
                string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                item = candidate.Clone();
                return true;
            }
        }

        return false;
    }

    private static bool ContainsArrayItem(
        JsonElement array,
        string matchProperty,
        string expected) =>
        array.ValueKind == JsonValueKind.Array &&
        array.EnumerateArray().Any(candidate =>
            TryReadString(candidate, matchProperty, out var actual) &&
            string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsArrayGuid(
        JsonElement payload,
        string arrayProperty,
        string guidProperty,
        Guid expected) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(arrayProperty, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.EnumerateArray().Any(item => HasGuid(item, guidProperty, expected));

    private static bool ContainsString(JsonElement payload, string property, string expected) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(property, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String &&
            string.Equals(item.GetString(), expected, StringComparison.Ordinal));

    private static Guid ReadArrayItemGuid(
        JsonElement array,
        string matchProperty,
        string expected,
        string valueProperty) =>
        TryFindArrayItemValue(array, matchProperty, expected, out var item)
            ? ReadGuid(item, valueProperty)
            : Guid.Empty;

    private static long? ReadArrayItemInt64(
        JsonElement array,
        string matchProperty,
        string expected,
        string valueProperty) =>
        TryFindArrayItemValue(array, matchProperty, expected, out var item)
            ? ReadInt64(item, valueProperty)
            : null;

    private static bool TryFindArrayItemValue(
        JsonElement array,
        string matchProperty,
        string expected,
        out JsonElement item)
    {
        item = default;
        if (array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var candidate in array.EnumerateArray())
        {
            if (TryReadString(candidate, matchProperty, out var actual) &&
                string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                item = candidate.Clone();
                return true;
            }
        }

        return false;
    }

    private static Guid ReadGuid(JsonElement payload, string property) =>
        TryReadString(payload, property, out var value) && Guid.TryParse(value, out var parsed)
            ? parsed
            : Guid.Empty;

    private static Guid ReadNestedGuid(JsonElement payload, string parent, string property) =>
        TryReadObject(payload, parent, out var nested) ? ReadGuid(nested, property) : Guid.Empty;

    private static long? ReadNestedInt64(JsonElement payload, string parent, string property) =>
        TryReadObject(payload, parent, out var nested) ? ReadInt64(nested, property) : null;

    private static DateTimeOffset? ReadNestedDateTimeOffset(
        JsonElement payload,
        string parent,
        string property) =>
        TryReadObject(payload, parent, out var nested) &&
        TryReadString(nested, property, out var value) &&
        DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;

    private static bool TryReadObject(JsonElement payload, string property, out JsonElement value)
    {
        value = default;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(property, out var nested) ||
            nested.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        value = nested;
        return true;
    }

    private static string? ReadString(JsonElement payload, string property) =>
        TryReadString(payload, property, out var value) ? value : null;

    private static bool? ReadBoolean(JsonElement payload, string property) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static bool HasBoolean(JsonElement payload, string property, bool expected) =>
        ReadBoolean(payload, property) == expected;

    private sealed record SyncOperationProbe(
        string OperationId,
        Guid ProjectId,
        string EntityType,
        Guid EntityId,
        string CommandType,
        long? BaseRevision,
        int PayloadSchemaVersion,
        DateTimeOffset CreatedAtDevice,
        SyncFactPayloadProbe Payload,
        Guid OfflineLeaseId,
        long AuthorizationVersion,
        long LocalSequence,
        IReadOnlyCollection<string> Dependencies,
        string CorrelationId,
        int DeviceTimezoneOffsetMinutes);

    private sealed record SyncFactPayloadProbe(
        Guid FactId,
        DateOnly ReportDate,
        string? LocationName,
        string Kind,
        string Description,
        Guid LocationId);
}
