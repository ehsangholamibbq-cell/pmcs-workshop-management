using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Sync.Domain;
using Pmcs.Modules.Sync.Persistence;

namespace Pmcs.Modules.Sync.Endpoints;

internal static partial class SyncGatewayEndpoints
{
    private const string SessionHeader = "X-Pmcs-Sync-Session";
    private const string Dataset = "field-operations-v1";
    private const int MaximumAttachmentSizeBytes = 25 * 1024 * 1024;
    private const int MaximumOperationPayloadCharacters = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapSyncGatewayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sync").WithTags("Offline Sync");
        group.MapPost("/handshake", HandshakeAsync);
        group.MapPost("/operations", PushAsync);
        group.MapGet("/pull", PullAsync);
        group.MapPost("/checkpoints", AdvanceCheckpointAsync);
        group.MapGet("/conflicts", ListConflictsAsync);
        group.MapPost("/conflicts/{conflictId:guid}/resolve", ResolveConflictAsync);
        group.MapGet("/devices", ListDevicesAsync);
        group.MapPost("/devices/{registrationId:guid}/revoke", RevokeDeviceAsync);
        group.MapGet("/diagnostics", GetDiagnosticsAsync);
    }

    private static async Task<IResult> HandshakeAsync(
        SyncHandshakeRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IProjectDirectory projects,
        SyncDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!IsValidDeviceId(request.DeviceId) ||
            (actor.DeviceId is not null && !string.Equals(actor.DeviceId, request.DeviceId, StringComparison.Ordinal)))
        {
            return Problem(StatusCodes.Status400BadRequest, "sync.device.invalid");
        }

        var compatibility = SyncPolicy.EvaluateCompatibility(
            request.AppVersion,
            request.ProtocolVersion,
            request.LocalSchemaVersion);
        if (!compatibility.IsCompatible)
        {
            return Problem(StatusCodes.Status426UpgradeRequired, compatibility.BlockingCode!);
        }

        if (!ValidQueueSummary(request.Queue))
        {
            return Problem(StatusCodes.Status400BadRequest, "sync.queue.summary.invalid");
        }

        var canCaptureField = await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, request.ProjectId, "field.daily-reports.capture", cancellationToken);
        var canCaptureQuality = await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, request.ProjectId, "quality.intake.capture", cancellationToken);
        var canCaptureHse = await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, request.ProjectId, "hse.intake.capture", cancellationToken);
        var project = await projects.FindProfileAsync(actor.TenantId, request.ProjectId, cancellationToken);
        if ((!canCaptureField && !canCaptureQuality && !canCaptureHse) || project is null)
        {
            return Problem(StatusCodes.Status403Forbidden, "sync.project.access_denied");
        }

        if (project.Status != ProjectStatus.Active)
        {
            return Problem(StatusCodes.Status409Conflict, "sync.project.not_active");
        }

        var allowedOperations = new List<string>(2);
        var entityTypes = new List<string>(2);
        var scopes = new List<string>(3);
        if (canCaptureField)
        {
            allowedOperations.Add("CaptureDailyReportFact");
            entityTypes.Add("DailyReport");
            scopes.Add("field.daily-reports.capture");
            scopes.Add("field.daily-reports.read");
        }
        if (canCaptureQuality || canCaptureHse)
        {
            allowedOperations.Add("CaptureQualitySafetyIntake");
            entityTypes.Add("QualitySafetyIntake");
            if (canCaptureQuality) scopes.Add("quality.intake.capture");
            if (canCaptureHse) scopes.Add("hse.intake.capture");
        }

        var now = clock.UtcNow;
        var skew = SafeClockSkewSeconds(request.DeviceTime, now);
        var watermark = await CurrentWatermarkAsync(db, actor.TenantId, request.ProjectId, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var device = await db.Devices.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId && item.DeviceId == request.DeviceId,
            cancellationToken);
        if (device?.Status == SyncDeviceStatus.Revoked)
        {
            return Results.Json(new
            {
                code = "sync.device.revoked",
                purgeRequired = true,
                pendingDataMustBeReviewed = request.Queue.PendingOperations > 0 || request.Queue.PendingAttachments > 0
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (device is null)
        {
            device = RegisteredSyncDevice.Create(
                Guid.NewGuid(),
                actor.TenantId,
                actor.UserId,
                request.DeviceId,
                request.DeviceName,
                request.Platform,
                request.AppVersion,
                now);
            db.Devices.Add(device);
        }
        else
        {
            device.Touch(request.DeviceName, request.Platform, request.AppVersion, now);
        }

        var activeLeases = await db.OfflineLeases.Where(item =>
            item.TenantId == actor.TenantId &&
            item.UserId == actor.UserId &&
            item.ProjectId == request.ProjectId &&
            item.DeviceId == request.DeviceId &&
            item.Status == OfflineLeaseStatus.Active).ToListAsync(cancellationToken);
        foreach (var activeLease in activeLeases)
        {
            activeLease.Supersede(now);
        }

        var lastAuthorizationVersion = await db.OfflineLeases
            .Where(item => item.TenantId == actor.TenantId &&
                item.UserId == actor.UserId &&
                item.ProjectId == request.ProjectId &&
                item.DeviceId == request.DeviceId)
            .Select(item => (long?)item.AuthorizationVersion)
            .MaxAsync(cancellationToken) ?? 0;
        var lease = OfflineAuthorizationLease.Issue(
            Guid.NewGuid(),
            actor.TenantId,
            actor.UserId,
            request.ProjectId,
            request.DeviceId,
            checked(lastAuthorizationVersion + 1),
            now,
            SyncPolicy.MaximumLeaseDuration);
        db.OfflineLeases.Add(lease);

        var checkpoint = await db.DeviceCheckpoints.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId &&
            item.UserId == actor.UserId &&
            item.ProjectId == request.ProjectId &&
            item.DeviceId == request.DeviceId &&
            item.Dataset == Dataset,
            cancellationToken);
        var suppliedCheckpoint = string.IsNullOrWhiteSpace(request.LastCheckpoint) ? null : request.LastCheckpoint.Trim();
        var checkpointMatches = checkpoint is null
            ? suppliedCheckpoint is null
            : string.Equals(checkpoint.CurrentToken, suppliedCheckpoint, StringComparison.Ordinal);
        var bootstrapRequired = !checkpointMatches;
        var startingSequence = checkpointMatches ? checkpoint?.LastSequence ?? 0 : 0;

        var session = SyncSession.Open(
            Guid.NewGuid(),
            actor.TenantId,
            actor.UserId,
            request.ProjectId,
            request.DeviceId,
            lease.Id,
            startingSequence,
            request.ProtocolVersion,
            request.LocalSchemaVersion,
            now,
            bootstrapRequired,
            skew);
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAuditAsync(
            db.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new AuditEntry(
            actor.TenantId,
            request.ProjectId,
            actor.UserId,
            "SyncSessionStarted",
            "SyncSession",
            session.Id.ToString(),
            now,
            new Dictionary<string, object?>
            {
                ["deviceId"] = request.DeviceId,
                ["leaseId"] = lease.Id,
                ["authorizationVersion"] = lease.AuthorizationVersion,
                ["bootstrapRequired"] = bootstrapRequired,
                ["pendingOperations"] = request.Queue.PendingOperations,
                ["pendingAttachments"] = request.Queue.PendingAttachments,
                ["clockSkewSeconds"] = skew
            },
            httpContext.TraceIdentifier),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new SyncHandshakeResponse(
            session.Id,
            session.ExpiresAt,
            now,
            new SyncLeaseModel(lease.Id, lease.AuthorizationVersion, lease.IssuedAt, lease.ExpiresAt),
            SyncPolicy.PolicyVersion,
            SyncPolicy.ProtocolVersion,
            SyncPolicy.LocalSchemaVersion,
            bootstrapRequired,
            watermark,
            checkpointMatches ? checkpoint?.CurrentToken : null,
            checkpointMatches ? checkpoint?.LastSequence ?? 0 : 0,
            SyncPolicy.MaximumBatchSize,
            MaximumAttachmentSizeBytes,
            skew,
            Math.Abs(skew) > SyncPolicy.AllowedClockSkew.TotalSeconds,
            allowedOperations,
            new SyncDatasetManifestModel(
                actor.TenantId,
                request.ProjectId,
                actor.UserId,
                request.DeviceId,
                entityTypes,
                scopes,
                "General",
                now,
                lease.ExpiresAt)));
    }

    private static async Task<IResult> PushAsync(
        SyncPushRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IOfflineFieldOperationHandler fieldHandler,
        SyncDbContext db,
        IClock clock,
        IAuditTrail audit,
        ILogger<SyncModule> logger,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!IsValidDeviceId(request.DeviceId) || request.Operations is null ||
            request.Operations.Count is < 1 or > SyncPolicy.MaximumBatchSize)
        {
            return Problem(StatusCodes.Status400BadRequest, "sync.batch.invalid");
        }

        var duplicate = request.Operations.GroupBy(item => item.OperationId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            return Results.BadRequest(new { code = "sync.operation.duplicate", operationId = duplicate });
        }

        var sessionResult = await RequireSessionAsync(httpContext, actor, request.DeviceId, db, clock.UtcNow, cancellationToken);
        if (sessionResult.Error is not null)
        {
            return sessionResult.Error;
        }

        var session = sessionResult.Session!;
        var results = new List<SyncOperationResponse>(request.Operations.Count);
        var resultByOperation = new Dictionary<string, OfflineFieldOperationStatus>(StringComparer.Ordinal);
        var leaseCache = new Dictionary<Guid, OfflineAuthorizationLease?>();

        foreach (var requestOperation in request.Operations
                     .OrderBy(item => item.LocalSequence)
                     .ThenBy(item => item.OperationId, StringComparer.Ordinal))
        {
            var operationNow = clock.UtcNow;
            var operationCorrelationId = IsValidCorrelationId(requestOperation.CorrelationId)
                ? requestOperation.CorrelationId
                : httpContext.TraceIdentifier;
            var envelopeError = await ValidateEnvelopeAsync(
                requestOperation,
                session.ProjectId,
                resultByOperation,
                db,
                actor,
                request.DeviceId,
                cancellationToken);
            if (envelopeError is not null)
            {
                results.Add(envelopeError);
                resultByOperation[requestOperation.OperationId] = envelopeError.Status;
                await RecordOperationReceiptAsync(
                    db, actor, session.ProjectId, request.DeviceId, requestOperation, envelopeError,
                    operationNow, operationCorrelationId, cancellationToken);
                await AuditSyncResultAsync(
                    audit, actor, session.ProjectId, request.DeviceId, requestOperation, envelopeError.Status,
                    envelopeError.Code, null, operationNow, operationCorrelationId, cancellationToken);
                continue;
            }

            if (!leaseCache.TryGetValue(requestOperation.OfflineLeaseId, out var operationLease))
            {
                operationLease = await db.OfflineLeases.AsNoTracking().SingleOrDefaultAsync(
                    item => item.Id == requestOperation.OfflineLeaseId,
                    cancellationToken);
                leaseCache[requestOperation.OfflineLeaseId] = operationLease;
            }

            if (operationLease is null || !operationLease.Covers(
                    actor.TenantId,
                    actor.UserId,
                    requestOperation.ProjectId,
                    request.DeviceId,
                    requestOperation.AuthorizationVersion,
                    requestOperation.CreatedAtDevice,
                    clock.UtcNow))
            {
                var rejected = Rejected(requestOperation, "sync.lease.operation_not_covered");
                results.Add(rejected);
                resultByOperation[requestOperation.OperationId] = rejected.Status;
                await RecordOperationReceiptAsync(
                    db, actor, session.ProjectId, request.DeviceId, requestOperation, rejected,
                    operationNow, operationCorrelationId, cancellationToken);
                await AuditSyncResultAsync(
                    audit, actor, session.ProjectId, request.DeviceId, requestOperation, rejected.Status,
                    rejected.Code, null, operationNow, operationCorrelationId, cancellationToken);
                continue;
            }

            var context = new OfflineFieldOperationContext(
                actor.TenantId,
                actor.UserId,
                request.DeviceId,
                operationCorrelationId);
            var operation = new OfflineFieldOperation(
                requestOperation.OperationId,
                requestOperation.ProjectId,
                requestOperation.EntityType,
                requestOperation.EntityId,
                requestOperation.CommandType,
                requestOperation.BaseRevision,
                requestOperation.PayloadSchemaVersion,
                requestOperation.CreatedAtDevice,
                requestOperation.Payload);
            var handled = await fieldHandler.HandleAsync(context, operation, cancellationToken);
            Guid? conflictId = null;
            if (handled.Status == OfflineFieldOperationStatus.Conflict)
            {
                conflictId = await EnsureConflictAsync(db, actor, request.DeviceId, requestOperation, handled, operationNow, cancellationToken);
            }
            else if (handled.Status == OfflineFieldOperationStatus.Applied)
            {
                await EnsureChangeFeedAsync(db, actor, request.DeviceId, requestOperation, handled, operationNow, cancellationToken);
            }

            var response = new SyncOperationResponse(
                handled.OperationId,
                handled.Status,
                handled.EntityId,
                handled.ServerRevision,
                handled.Code,
                handled.Message,
                handled.WasReplay,
                conflictId);
            results.Add(response);
            resultByOperation[requestOperation.OperationId] = response.Status;
            await RecordOperationReceiptAsync(
                db, actor, session.ProjectId, request.DeviceId, requestOperation, response,
                operationNow, operationCorrelationId, cancellationToken);
            if (handled.Status != OfflineFieldOperationStatus.Applied)
            {
                await AuditSyncResultAsync(
                    audit, actor, session.ProjectId, request.DeviceId, requestOperation, handled.Status,
                    handled.Code, conflictId, operationNow, operationCorrelationId, cancellationToken);
            }
        }

        var appliedCount = results.Count(item => item.Status == OfflineFieldOperationStatus.Applied);
        var conflictCount = results.Count(item => item.Status == OfflineFieldOperationStatus.Conflict);
        var rejectedCount = results.Count(item =>
            item.Status is OfflineFieldOperationStatus.Rejected or OfflineFieldOperationStatus.Unsupported);
        SyncGatewayLog.BatchProcessed(
            logger,
            actor.TenantId,
            actor.UserId,
            session.ProjectId,
            request.DeviceId,
            results.Count,
            appliedCount,
            conflictCount,
            rejectedCount);
        return Results.Ok(new SyncPushResponse(clock.UtcNow, results));
    }

    private static async Task<IResult> PullAsync(
        Guid projectId,
        string? checkpoint,
        int? limit,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        SyncDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var sessionResult = await RequireSessionAsync(httpContext, actor, null, db, clock.UtcNow, cancellationToken);
        if (sessionResult.Error is not null)
        {
            return sessionResult.Error;
        }

        var session = sessionResult.Session!;
        if (session.ProjectId != projectId || !await permissions.HasProjectPermissionAsync(
                actor.TenantId, actor.UserId, projectId, "field.daily-reports.read", cancellationToken))
        {
            return Problem(StatusCodes.Status403Forbidden, "sync.pull.access_denied");
        }

        var checkpointRecord = await db.DeviceCheckpoints.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId &&
            item.UserId == actor.UserId &&
            item.ProjectId == projectId &&
            item.DeviceId == session.DeviceId &&
            item.Dataset == Dataset,
            cancellationToken);
        var supplied = string.IsNullOrWhiteSpace(checkpoint) ? null : checkpoint.Trim();
        long fromSequence;
        if (supplied is not null)
        {
            if (checkpointRecord is null || !string.Equals(checkpointRecord.CurrentToken, supplied, StringComparison.Ordinal))
            {
                return Problem(StatusCodes.Status409Conflict, "sync.checkpoint.mismatch");
            }

            fromSequence = checkpointRecord.LastSequence;
        }
        else
        {
            fromSequence = session.BootstrapRequired ? 0 : session.StartingSequence;
        }

        var pageSize = Math.Clamp(limit ?? 100, 1, 200);
        var entries = await db.ChangeFeed.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId &&
                item.ProjectId == projectId && item.Sequence > fromSequence)
            .OrderBy(item => item.Sequence)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var nextSequence = entries.Count == 0 ? fromSequence : entries[^1].Sequence;
        var now = clock.UtcNow;
        var offer = new CheckpointOffer
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            TenantId = actor.TenantId,
            UserId = actor.UserId,
            ProjectId = projectId,
            DeviceId = session.DeviceId,
            Dataset = Dataset,
            Sequence = nextSequence,
            CreatedAt = now,
            ExpiresAt = session.ExpiresAt
        };
        db.CheckpointOffers.Add(offer);
        await db.SaveChangesAsync(cancellationToken);

        var watermark = await CurrentWatermarkAsync(db, actor.TenantId, projectId, cancellationToken);
        return Results.Ok(new SyncPullResponse(
            now,
            watermark,
            offer.Id,
            offer.ExpiresAt,
            nextSequence < watermark,
            entries.Select(ToChangeModel).ToArray()));
    }

    private static async Task<IResult> AdvanceCheckpointAsync(
        AdvanceCheckpointRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        SyncDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var now = clock.UtcNow;
        var sessionResult = await RequireSessionAsync(httpContext, actor, null, db, now, cancellationToken);
        if (sessionResult.Error is not null)
        {
            return sessionResult.Error;
        }

        var session = sessionResult.Session!;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var offer = await db.CheckpointOffers.SingleOrDefaultAsync(item => item.Id == request.CheckpointOffer, cancellationToken);
        if (offer is null || offer.ExpiresAt <= now ||
            offer.SessionId != session.Id || offer.TenantId != actor.TenantId || offer.UserId != actor.UserId ||
            offer.ProjectId != request.ProjectId || offer.ProjectId != session.ProjectId ||
            !string.Equals(offer.DeviceId, session.DeviceId, StringComparison.Ordinal))
        {
            return Problem(StatusCodes.Status409Conflict, "sync.checkpoint.offer.invalid");
        }

        var checkpoint = await db.DeviceCheckpoints.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId &&
            item.UserId == actor.UserId &&
            item.ProjectId == request.ProjectId &&
            item.DeviceId == session.DeviceId &&
            item.Dataset == Dataset,
            cancellationToken);
        if (offer.ConsumedAt.HasValue)
        {
            return checkpoint is not null && string.Equals(checkpoint.CurrentToken, offer.Id.ToString(), StringComparison.Ordinal)
                ? Results.Ok(new AdvanceCheckpointResponse(
                    checkpoint.CurrentToken,
                    checkpoint.LastSequence,
                    checkpoint.AdvancedAt))
                : Problem(StatusCodes.Status409Conflict, "sync.checkpoint.offer.invalid");
        }

        if (checkpoint is not null &&
            (offer.Sequence < checkpoint.LastSequence ||
                (offer.Sequence == checkpoint.LastSequence && offer.CreatedAt < checkpoint.AdvancedAt)))
        {
            return Problem(StatusCodes.Status409Conflict, "sync.checkpoint.regression");
        }

        if (checkpoint is null)
        {
            checkpoint = new DeviceCheckpoint
            {
                Id = Guid.NewGuid(),
                TenantId = actor.TenantId,
                UserId = actor.UserId,
                ProjectId = request.ProjectId,
                DeviceId = session.DeviceId,
                Dataset = Dataset
            };
            db.DeviceCheckpoints.Add(checkpoint);
        }

        checkpoint.LastSequence = offer.Sequence;
        checkpoint.CurrentToken = offer.Id.ToString();
        checkpoint.AdvancedAt = now;
        offer.ConsumedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        await sideEffects.WriteAuditAsync(
            db.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new AuditEntry(
            actor.TenantId,
            request.ProjectId,
            actor.UserId,
            "CheckpointAdvanced",
            "DeviceCheckpoint",
            checkpoint.Id.ToString(),
            now,
            new Dictionary<string, object?>
            {
                ["deviceId"] = session.DeviceId,
                ["sequence"] = checkpoint.LastSequence,
                ["checkpointOffer"] = offer.Id
            },
            httpContext.TraceIdentifier),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new AdvanceCheckpointResponse(
            checkpoint.CurrentToken,
            checkpoint.LastSequence,
            checkpoint.AdvancedAt));
    }

    private static async Task<IResult> ListConflictsAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        SyncDbContext db,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissions.HasProjectPermissionAsync(
                actor.TenantId, actor.UserId, projectId, "field.daily-reports.read", cancellationToken))
        {
            return Results.Forbid();
        }

        var canManageAll = await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, projectId, "sync.conflicts.manage", cancellationToken);
        var query = db.Conflicts.AsNoTracking().Where(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (!canManageAll)
        {
            query = query.Where(item => item.UserId == actor.UserId);
        }

        var conflicts = await query.OrderBy(item => item.Status).ThenByDescending(item => item.DetectedAt)
            .Take(200).ToListAsync(cancellationToken);
        return Results.Ok(conflicts.Select(ToConflictModel).ToArray());
    }

    private static async Task<IResult> ResolveConflictAsync(
        Guid conflictId,
        ResolveSyncConflictRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        SyncDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var conflict = await db.Conflicts.SingleOrDefaultAsync(
            item => item.Id == conflictId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (conflict is null)
        {
            return Results.NotFound(new { code = "sync.conflict.not_found" });
        }

        var canResolve = await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, conflict.ProjectId, "field.daily-reports.capture", cancellationToken);
        var canManageAll = await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, conflict.ProjectId, "sync.conflicts.manage", cancellationToken);
        if (!canResolve || (conflict.UserId != actor.UserId && !canManageAll))
        {
            return Results.Forbid();
        }

        if (request.Resolution == SyncConflictResolutionType.Reapply &&
            !IsValidUlid(request.ReplacementOperationId))
        {
            return Problem(StatusCodes.Status400BadRequest, "sync.conflict.replacement.invalid");
        }

        if (conflict.Status == SyncConflictStatus.Resolved)
        {
            var sameResolution = conflict.ResolutionType == request.Resolution &&
                string.Equals(
                    conflict.ReplacementOperationId,
                    string.IsNullOrWhiteSpace(request.ReplacementOperationId) ? null : request.ReplacementOperationId.Trim(),
                    StringComparison.Ordinal);
            return sameResolution
                ? Results.Ok(ToConflictModel(conflict))
                : Problem(StatusCodes.Status409Conflict, "sync.conflict.already_resolved");
        }

        var resolution = conflict.Resolve(
            Guid.NewGuid(),
            request.BaseRevision,
            actor.UserId,
            request.Resolution,
            request.ReplacementOperationId,
            request.Comment,
            clock.UtcNow);
        db.ConflictResolutions.Add(resolution);
        await db.SaveChangesAsync(cancellationToken);

        await sideEffects.WriteAuditAsync(
            db.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new AuditEntry(
            actor.TenantId,
            conflict.ProjectId,
            actor.UserId,
            "ConflictResolved",
            "SyncConflict",
            conflict.Id.ToString(),
            clock.UtcNow,
            new Dictionary<string, object?>
            {
                ["operationId"] = conflict.OperationId,
                ["resolution"] = request.Resolution.ToString(),
                ["replacementOperationId"] = request.ReplacementOperationId,
                ["deviceId"] = conflict.DeviceId
            },
            httpContext.TraceIdentifier),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(ToConflictModel(conflict));
    }

    private static async Task<IResult> ListDevicesAsync(
        ICurrentActor actor,
        SyncDbContext db,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var devices = await db.Devices.AsNoTracking().Where(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId)
            .OrderByDescending(item => item.LastSeenAt)
            .Take(50)
            .Select(item => new SyncDeviceModel(
                item.Id,
                item.DeviceId,
                item.DisplayName,
                item.Platform,
                item.AppVersion,
                item.Status,
                item.RegisteredAt,
                item.LastSeenAt,
                item.RevokedAt,
                item.Revision))
            .ToListAsync(cancellationToken);
        return Results.Ok(devices);
    }

    private static async Task<IResult> RevokeDeviceAsync(
        Guid registrationId,
        RevokeSyncDeviceRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        SyncDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var device = await db.Devices.SingleOrDefaultAsync(item =>
            item.Id == registrationId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (device is null)
        {
            return Results.NotFound(new { code = "sync.device.not_found" });
        }

        var canManageAll = await permissions.HasTenantPermissionAsync(
            actor.TenantId, actor.UserId, "sync.devices.manage", cancellationToken);
        if (device.UserId != actor.UserId && !canManageAll)
        {
            return Results.Forbid();
        }

        if (device.Status == SyncDeviceStatus.Revoked)
        {
            return Results.NoContent();
        }

        if (device.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "sync.device.revision.conflict", currentRevision = device.Revision });
        }

        var now = clock.UtcNow;
        device.Revoke(actor.UserId, request.Reason, now);
        var leases = await db.OfflineLeases.Where(item =>
            item.TenantId == actor.TenantId && item.UserId == device.UserId && item.DeviceId == device.DeviceId &&
            item.Status != OfflineLeaseStatus.Revoked).ToListAsync(cancellationToken);
        foreach (var lease in leases)
        {
            lease.Revoke(now);
        }

        var sessions = await db.Sessions.Where(item =>
            item.TenantId == actor.TenantId && item.UserId == device.UserId && item.DeviceId == device.DeviceId &&
            item.ClosedAt == null).ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Close(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAuditAsync(
            db.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new AuditEntry(
            actor.TenantId,
            null,
            actor.UserId,
            "SyncDeviceRevoked",
            "SyncDevice",
            device.Id.ToString(),
            now,
            new Dictionary<string, object?>
            {
                ["deviceId"] = device.DeviceId,
                ["deviceUserId"] = device.UserId,
                ["reason"] = request.Reason
            },
            httpContext.TraceIdentifier),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetDiagnosticsAsync(
        Guid projectId,
        string deviceId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        SyncDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!IsValidDeviceId(deviceId) || !await permissions.HasProjectPermissionAsync(
                actor.TenantId, actor.UserId, projectId, "field.daily-reports.read", cancellationToken))
        {
            return Results.Forbid();
        }

        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId && item.DeviceId == deviceId,
            cancellationToken);
        if (device is null)
        {
            return Results.NotFound(new { code = "sync.device.not_found" });
        }

        var leaseExpiresAt = await db.OfflineLeases.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
                item.ProjectId == projectId && item.DeviceId == deviceId && item.Status == OfflineLeaseStatus.Active)
            .Select(item => (DateTimeOffset?)item.ExpiresAt)
            .MaxAsync(cancellationToken);
        var lastHandshakeAt = await db.Sessions.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
                item.ProjectId == projectId && item.DeviceId == deviceId)
            .Select(item => (DateTimeOffset?)item.IssuedAt)
            .MaxAsync(cancellationToken);
        var checkpoint = await db.DeviceCheckpoints.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId && item.ProjectId == projectId &&
            item.DeviceId == deviceId && item.Dataset == Dataset,
            cancellationToken);
        var conflictCount = await db.Conflicts.AsNoTracking().CountAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId && item.ProjectId == projectId &&
            item.DeviceId == deviceId && item.Status == SyncConflictStatus.Open,
            cancellationToken);
        var recentBoundary = clock.UtcNow.AddHours(-24);
        var recentRejectedCount = await db.OperationReceipts.AsNoTracking().CountAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId && item.ProjectId == projectId &&
            item.DeviceId == deviceId && item.LastAttemptAt >= recentBoundary &&
            (item.Status == nameof(OfflineFieldOperationStatus.Rejected) ||
                item.Status == nameof(OfflineFieldOperationStatus.Unsupported)),
            cancellationToken);
        var recentReplayCount = await db.OperationReceipts.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
                item.ProjectId == projectId && item.DeviceId == deviceId && item.LastAttemptAt >= recentBoundary)
            .Select(item => (int?)item.ReplayCount)
            .SumAsync(cancellationToken) ?? 0;
        var lastOperationAt = await db.OperationReceipts.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
                item.ProjectId == projectId && item.DeviceId == deviceId)
            .Select(item => (DateTimeOffset?)item.LastAttemptAt)
            .MaxAsync(cancellationToken);
        var watermark = await CurrentWatermarkAsync(db, actor.TenantId, projectId, cancellationToken);
        var checkpointSequence = checkpoint?.LastSequence ?? 0;
        var checkpointLag = Math.Max(0, watermark - checkpointSequence);
        var recoveryState = SyncRecoveryHealth.Evaluate(
            device.Status,
            leaseExpiresAt,
            clock.UtcNow,
            checkpointSequence,
            watermark,
            conflictCount,
            recentRejectedCount);

        return Results.Ok(new SyncDiagnosticsModel(
            projectId,
            deviceId,
            device.Status,
            clock.UtcNow,
            leaseExpiresAt,
            lastHandshakeAt,
            checkpointSequence,
            checkpoint?.AdvancedAt,
            conflictCount,
            recentRejectedCount,
            recentReplayCount,
            lastOperationAt,
            watermark,
            checkpointLag,
            recoveryState,
            SyncPolicy.PolicyVersion));
    }

    private static async Task<(SyncSession? Session, IResult? Error)> RequireSessionAsync(
        HttpContext httpContext,
        ICurrentActor actor,
        string? requestDeviceId,
        SyncDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(httpContext.Request.Headers[SessionHeader].ToString(), out var sessionId))
        {
            return (null, Problem(StatusCodes.Status401Unauthorized, "sync.session.required"));
        }

        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null || session.ClosedAt.HasValue || session.ExpiresAt <= now ||
            session.TenantId != actor.TenantId || session.UserId != actor.UserId ||
            (requestDeviceId is not null && !string.Equals(session.DeviceId, requestDeviceId, StringComparison.Ordinal)) ||
            (actor.DeviceId is not null && !string.Equals(session.DeviceId, actor.DeviceId, StringComparison.Ordinal)))
        {
            return (null, Problem(StatusCodes.Status401Unauthorized, "sync.session.invalid_or_expired"));
        }

        var deviceIsActive = await db.Devices.AsNoTracking().AnyAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
            item.DeviceId == session.DeviceId && item.Status == SyncDeviceStatus.Active,
            cancellationToken);
        var sessionLeaseIsActive = await db.OfflineLeases.AsNoTracking().AnyAsync(item =>
            item.Id == session.LeaseId && item.Status == OfflineLeaseStatus.Active && item.ExpiresAt > now,
            cancellationToken);
        return !deviceIsActive || !sessionLeaseIsActive
            ? (null, Problem(StatusCodes.Status403Forbidden, "sync.device_or_lease.revoked"))
            : (session, null);
    }

    private static async Task<SyncOperationResponse?> ValidateEnvelopeAsync(
        SyncOperationRequest operation,
        Guid sessionProjectId,
        Dictionary<string, OfflineFieldOperationStatus> priorResults,
        SyncDbContext db,
        ICurrentActor actor,
        string deviceId,
        CancellationToken cancellationToken)
    {
        if (!IsValidUlid(operation.OperationId) || operation.EntityId == Guid.Empty ||
            operation.ProjectId == Guid.Empty || operation.ProjectId != sessionProjectId ||
            operation.OfflineLeaseId == Guid.Empty || operation.AuthorizationVersion < 1 ||
            operation.LocalSequence < 1 || !IsValidCorrelationId(operation.CorrelationId) ||
            operation.DeviceTimezoneOffsetMinutes is < -840 or > 840 ||
            !IsValidBoundedText(operation.EntityType, 120) ||
            !IsValidBoundedText(operation.CommandType, 160) ||
            operation.Payload.ValueKind == JsonValueKind.Undefined ||
            operation.Payload.GetRawText().Length > MaximumOperationPayloadCharacters)
        {
            return Rejected(operation, "sync.operation.envelope.invalid");
        }

        var dependencies = operation.Dependencies ?? [];
        if (dependencies.Count > 50 ||
            dependencies.Any(item => !IsValidUlid(item) || string.Equals(item, operation.OperationId, StringComparison.Ordinal)) ||
            dependencies.Distinct(StringComparer.Ordinal).Count() != dependencies.Count)
        {
            return Rejected(operation, "sync.operation.dependencies.invalid");
        }

        var unresolvedDependencies = dependencies
            .Where(item => !priorResults.ContainsKey(item))
            .ToArray();
        var persistedDependencies = unresolvedDependencies.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await db.OperationReceipts.AsNoTracking()
                .Where(item => item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
                    item.ProjectId == operation.ProjectId && item.DeviceId == deviceId &&
                    unresolvedDependencies.Contains(item.OperationId))
                .ToDictionaryAsync(item => item.OperationId, item => item.Status, StringComparer.Ordinal, cancellationToken);

        foreach (var dependency in dependencies)
        {
            if (priorResults.TryGetValue(dependency, out var status))
            {
                if (status != OfflineFieldOperationStatus.Applied)
                {
                    return Rejected(operation, "sync.operation.dependency.blocked");
                }

                continue;
            }

            if (!persistedDependencies.TryGetValue(dependency, out var persistedStatus))
            {
                return Rejected(operation, "sync.operation.dependency.out_of_order");
            }

            if (!string.Equals(persistedStatus, nameof(OfflineFieldOperationStatus.Applied), StringComparison.Ordinal))
            {
                return Rejected(operation, "sync.operation.dependency.blocked");
            }
        }

        return null;
    }

    private static async Task<Guid> EnsureConflictAsync(
        SyncDbContext db,
        ICurrentActor actor,
        string deviceId,
        SyncOperationRequest operation,
        OfflineFieldOperationResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Conflicts.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
            item.DeviceId == deviceId && item.OperationId == operation.OperationId,
            cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var conflict = SyncConflictCase.Detect(
            Guid.NewGuid(),
            actor.TenantId,
            operation.ProjectId,
            actor.UserId,
            deviceId,
            operation.OperationId,
            operation.EntityType,
            result.EntityId ?? operation.EntityId,
            operation.CommandType,
            operation.BaseRevision,
            result.ServerRevision,
            result.Code ?? "sync.conflict",
            JsonSerializer.Serialize(operation, SerializerOptions),
            result.ServerProjectionJson,
            now);
        db.Conflicts.Add(conflict);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return conflict.Id;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            return await db.Conflicts.AsNoTracking()
                .Where(item => item.TenantId == actor.TenantId && item.UserId == actor.UserId &&
                    item.DeviceId == deviceId && item.OperationId == operation.OperationId)
                .Select(item => item.Id)
                .SingleAsync(cancellationToken);
        }
    }

    private static async Task EnsureChangeFeedAsync(
        SyncDbContext db,
        ICurrentActor actor,
        string deviceId,
        SyncOperationRequest operation,
        OfflineFieldOperationResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await db.ChangeFeed.AnyAsync(item =>
                item.TenantId == actor.TenantId && item.ActorUserId == actor.UserId &&
                item.SourceDeviceId == deviceId &&
                item.OperationId == operation.OperationId,
                cancellationToken))
        {
            return;
        }

        db.ChangeFeed.Add(new SyncChangeFeedEntry
        {
            Id = Guid.NewGuid(),
            TenantId = actor.TenantId,
            ProjectId = operation.ProjectId,
            ActorUserId = actor.UserId,
            SourceDeviceId = deviceId,
            OperationId = operation.OperationId,
            EntityType = operation.EntityType,
            EntityId = result.EntityId ?? operation.EntityId,
            ChangeType = "ServerAccepted",
            Revision = result.ServerRevision,
            ProjectionJson = result.ServerProjectionJson ?? "{}",
            Classification = "General",
            EffectiveAt = operation.CreatedAtDevice,
            ServerAt = now,
            CorrelationId = operation.CorrelationId
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
        }
    }

    private static Task<int> RecordOperationReceiptAsync(
        SyncDbContext db,
        ICurrentActor actor,
        Guid projectId,
        string deviceId,
        SyncOperationRequest operation,
        SyncOperationResponse response,
        DateTimeOffset attemptedAt,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!IsValidUlid(operation.OperationId))
        {
            return Task.FromResult(0);
        }

        var entityType = SafeDiagnosticText(operation.EntityType, 120, "Invalid");
        var commandType = SafeDiagnosticText(operation.CommandType, 160, "Invalid");
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into sync_control.operation_receipts (
                id, tenant_id, project_id, user_id, device_id, operation_id, local_sequence,
                entity_type, entity_id, command_type, status, code, conflict_id, server_revision,
                attempt_count, replay_count, first_attempt_at, last_attempt_at, last_correlation_id)
            values (
                {Guid.NewGuid()}, {actor.TenantId}, {projectId}, {actor.UserId}, {deviceId},
                {operation.OperationId}, {operation.LocalSequence}, {entityType}, {operation.EntityId},
                {commandType}, {response.Status.ToString()}, {response.Code}, {response.ConflictId},
                {response.ServerRevision}, 1, {(response.WasReplay ? 1 : 0)}, {attemptedAt}, {attemptedAt}, {correlationId})
            on conflict (tenant_id, user_id, device_id, operation_id) do update set
                status = case when excluded.code = 'sync.operation.reused'
                    then sync_control.operation_receipts.status else excluded.status end,
                code = case when excluded.code = 'sync.operation.reused'
                    then sync_control.operation_receipts.code else excluded.code end,
                conflict_id = case when excluded.code = 'sync.operation.reused'
                    then sync_control.operation_receipts.conflict_id
                    else coalesce(excluded.conflict_id, sync_control.operation_receipts.conflict_id) end,
                server_revision = case when excluded.code = 'sync.operation.reused'
                    then sync_control.operation_receipts.server_revision
                    else coalesce(excluded.server_revision, sync_control.operation_receipts.server_revision) end,
                attempt_count = sync_control.operation_receipts.attempt_count + 1,
                replay_count = sync_control.operation_receipts.replay_count + excluded.replay_count,
                last_attempt_at = excluded.last_attempt_at,
                last_correlation_id = excluded.last_correlation_id;
            """, cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static SyncChangeModel ToChangeModel(SyncChangeFeedEntry entry) => new(
        entry.Sequence,
        entry.Id,
        entry.EntityType,
        entry.EntityId,
        entry.ChangeType,
        entry.Revision,
        ParseJson(entry.ProjectionJson),
        entry.EffectiveAt,
        entry.ServerAt,
        entry.CorrelationId);

    private static Task AuditSyncResultAsync(
        IAuditTrail audit,
        ICurrentActor actor,
        Guid projectId,
        string deviceId,
        SyncOperationRequest operation,
        OfflineFieldOperationStatus status,
        string? code,
        Guid? conflictId,
        DateTimeOffset now,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var resourceType = SafeDiagnosticText(operation.EntityType, 120, "SyncOperation");
        var commandType = SafeDiagnosticText(operation.CommandType, 160, "Invalid");
        return audit.WriteAsync(new AuditEntry(
            actor.TenantId,
            projectId,
            actor.UserId,
            status == OfflineFieldOperationStatus.Conflict ? "ConflictDetected" : "OfflineOperationRejected",
            resourceType,
            operation.EntityId.ToString(),
            now,
            new Dictionary<string, object?>
            {
                ["operationId"] = IsValidUlid(operation.OperationId) ? operation.OperationId : "invalid",
                ["deviceId"] = deviceId,
                ["commandType"] = commandType,
                ["status"] = status.ToString(),
                ["code"] = code,
                ["conflictId"] = conflictId,
                ["localSequence"] = operation.LocalSequence
            },
            correlationId), cancellationToken);
    }

    private static SyncConflictModel ToConflictModel(SyncConflictCase conflict) => new(
        conflict.Id,
        conflict.ProjectId,
        conflict.DeviceId,
        conflict.OperationId,
        conflict.EntityType,
        conflict.EntityId,
        conflict.CommandType,
        conflict.LocalBaseRevision,
        conflict.ServerRevision,
        conflict.ReasonCode,
        ParseJson(conflict.LocalIntentJson),
        string.IsNullOrWhiteSpace(conflict.ServerProjectionJson) ? null : ParseJson(conflict.ServerProjectionJson),
        conflict.Status,
        conflict.DetectedAt,
        conflict.ResolvedAt,
        conflict.ResolvedBy,
        conflict.ResolutionType,
        conflict.ReplacementOperationId,
        conflict.Revision);

    private static JsonElement ParseJson(string value) => JsonSerializer.Deserialize<JsonElement>(value, SerializerOptions);

    private static async Task<long> CurrentWatermarkAsync(
        SyncDbContext db,
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken) =>
        await db.ChangeFeed.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .Select(item => (long?)item.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

    private static bool ValidQueueSummary(SyncQueueSummary? queue) =>
        queue is not null && queue.PendingOperations >= 0 && queue.PendingAttachments >= 0 &&
        queue.PendingAttachmentBytes >= 0 &&
        queue.PendingOperations <= 100_000 && queue.PendingAttachments <= 10_000;

    private static long SafeClockSkewSeconds(DateTimeOffset deviceTime, DateTimeOffset serverTime)
    {
        var seconds = (deviceTime - serverTime).TotalSeconds;
        return seconds >= long.MaxValue ? long.MaxValue : seconds <= long.MinValue ? long.MinValue : (long)Math.Round(seconds);
    }

    private static SyncOperationResponse Rejected(SyncOperationRequest operation, string code) =>
        new(operation.OperationId, OfflineFieldOperationStatus.Rejected, operation.EntityId, null, code, null, false, null);

    private static IResult Problem(int statusCode, string code) =>
        Results.Json(new { code }, statusCode: statusCode);

    private static bool IsValidDeviceId(string? value) =>
        value is not null && DeviceIdPattern().IsMatch(value);

    private static bool IsValidUlid(string? value) =>
        value is not null && UlidPattern().IsMatch(value);

    private static bool IsValidCorrelationId(string? value) =>
        value is not null && CorrelationIdPattern().IsMatch(value);

    private static bool IsValidBoundedText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Length <= maximumLength && !normalized.Any(char.IsControl);
    }

    private static string SafeDiagnosticText(string? value, int maximumLength, string fallback) =>
        IsValidBoundedText(value, maximumLength) ? value!.Trim() : fallback;

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{8,120}$", RegexOptions.CultureInvariant)]
    private static partial Regex DeviceIdPattern();

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.CultureInvariant)]
    private static partial Regex UlidPattern();

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();
}

internal static partial class SyncGatewayLog
{
    [LoggerMessage(
        EventId = 4601,
        Level = LogLevel.Information,
        Message = "Sync batch processed for tenant {TenantId}, user {UserId}, project {ProjectId}, device {DeviceId}: total={Total}, applied={Applied}, conflicts={Conflicts}, rejected={Rejected}")]
    public static partial void BatchProcessed(
        ILogger logger,
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string deviceId,
        int total,
        int applied,
        int conflicts,
        int rejected);
}
