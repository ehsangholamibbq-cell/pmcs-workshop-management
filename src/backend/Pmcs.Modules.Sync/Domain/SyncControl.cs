using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Sync.Domain;

public sealed class RegisteredSyncDevice : AggregateRoot
{
    private RegisteredSyncDevice()
    {
    }

    private RegisteredSyncDevice(
        Guid id,
        Guid tenantId,
        Guid userId,
        string deviceId,
        string displayName,
        string platform,
        string appVersion,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        DeviceId = NormalizeRequired(deviceId, 120, "sync.device.invalid");
        DisplayName = NormalizeRequired(displayName, 160, "sync.device.name.invalid");
        Platform = NormalizeRequired(platform, 120, "sync.device.platform.invalid");
        AppVersion = NormalizeRequired(appVersion, 40, "sync.client.version.invalid");
        Status = SyncDeviceStatus.Active;
        RegisteredAt = now;
        LastSeenAt = now;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string DeviceId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Platform { get; private set; } = string.Empty;

    public string AppVersion { get; private set; } = string.Empty;

    public SyncDeviceStatus Status { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedBy { get; private set; }

    public string? RevocationReason { get; private set; }

    public static RegisteredSyncDevice Create(
        Guid id,
        Guid tenantId,
        Guid userId,
        string deviceId,
        string displayName,
        string platform,
        string appVersion,
        DateTimeOffset now)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainRuleException("sync.device.identity.required", "Device, tenant and user identifiers are required.");
        }

        return new RegisteredSyncDevice(id, tenantId, userId, deviceId, displayName, platform, appVersion, now);
    }

    public void Touch(string displayName, string platform, string appVersion, DateTimeOffset now)
    {
        if (Status != SyncDeviceStatus.Active)
        {
            throw new DomainRuleException("sync.device.revoked", "A revoked device cannot open a new synchronization session.");
        }

        DisplayName = NormalizeRequired(displayName, 160, "sync.device.name.invalid");
        Platform = NormalizeRequired(platform, 120, "sync.device.platform.invalid");
        AppVersion = NormalizeRequired(appVersion, 40, "sync.client.version.invalid");
        LastSeenAt = now;
        AdvanceRevision();
    }

    public void Revoke(Guid revokedBy, string reason, DateTimeOffset now)
    {
        if (revokedBy == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw new DomainRuleException("sync.device.revocation.invalid", "Revoker and a bounded revocation reason are required.");
        }

        if (Status == SyncDeviceStatus.Revoked)
        {
            return;
        }

        Status = SyncDeviceStatus.Revoked;
        RevokedAt = now;
        RevokedBy = revokedBy;
        RevocationReason = reason.Trim();
        LastSeenAt = now;
        AdvanceRevision();
    }

    private static string NormalizeRequired(string value, int maximumLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new DomainRuleException(code, "A required bounded text value is invalid.");
        }

        return normalized;
    }
}

public sealed class OfflineAuthorizationLease
{
    private OfflineAuthorizationLease()
    {
    }

    private OfflineAuthorizationLease(
        Guid id,
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string deviceId,
        long authorizationVersion,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        ProjectId = projectId;
        DeviceId = deviceId;
        AuthorizationVersion = authorizationVersion;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        Status = OfflineLeaseStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string DeviceId { get; private set; } = string.Empty;

    public long AuthorizationVersion { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public OfflineLeaseStatus Status { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static OfflineAuthorizationLease Issue(
        Guid id,
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string deviceId,
        long authorizationVersion,
        DateTimeOffset issuedAt,
        TimeSpan duration)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || userId == Guid.Empty || projectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(deviceId) || authorizationVersion < 1 ||
            duration <= TimeSpan.Zero || duration > SyncPolicy.MaximumLeaseDuration)
        {
            throw new DomainRuleException("sync.lease.invalid", "Offline authorization lease parameters are invalid.");
        }

        return new OfflineAuthorizationLease(
            id,
            tenantId,
            userId,
            projectId,
            deviceId.Trim(),
            authorizationVersion,
            issuedAt,
            issuedAt.Add(duration));
    }

    public void Supersede(DateTimeOffset now)
    {
        if (Status == OfflineLeaseStatus.Active)
        {
            Status = OfflineLeaseStatus.Superseded;
            RevokedAt = now;
        }
    }

    public void Revoke(DateTimeOffset now)
    {
        Status = OfflineLeaseStatus.Revoked;
        RevokedAt = now;
    }

    public bool Covers(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string deviceId,
        long authorizationVersion,
        DateTimeOffset createdAtDevice,
        DateTimeOffset serverNow) =>
        (Status == OfflineLeaseStatus.Active ||
            (Status == OfflineLeaseStatus.Superseded && RevokedAt.HasValue &&
                createdAtDevice <= RevokedAt.Value.Add(SyncPolicy.AllowedClockSkew))) &&
        TenantId == tenantId &&
        UserId == userId &&
        ProjectId == projectId &&
        string.Equals(DeviceId, deviceId, StringComparison.Ordinal) &&
        AuthorizationVersion == authorizationVersion &&
        createdAtDevice >= IssuedAt.Subtract(SyncPolicy.AllowedClockSkew) &&
        createdAtDevice <= ExpiresAt.Add(SyncPolicy.AllowedClockSkew) &&
        createdAtDevice >= serverNow.Subtract(SyncPolicy.MaximumOfflineOperationAge) &&
        createdAtDevice <= serverNow.Add(SyncPolicy.AllowedClockSkew);
}

public sealed class SyncSession
{
    private SyncSession()
    {
    }

    private SyncSession(
        Guid id,
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string deviceId,
        Guid leaseId,
        long startingSequence,
        int protocolVersion,
        int localSchemaVersion,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        bool bootstrapRequired,
        long clockSkewSeconds)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        ProjectId = projectId;
        DeviceId = deviceId;
        LeaseId = leaseId;
        StartingSequence = startingSequence;
        ProtocolVersion = protocolVersion;
        LocalSchemaVersion = localSchemaVersion;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        BootstrapRequired = bootstrapRequired;
        ClockSkewSeconds = clockSkewSeconds;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public Guid LeaseId { get; private set; }
    public long StartingSequence { get; private set; }
    public int ProtocolVersion { get; private set; }
    public int LocalSchemaVersion { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool BootstrapRequired { get; private set; }
    public long ClockSkewSeconds { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static SyncSession Open(
        Guid id,
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string deviceId,
        Guid leaseId,
        long startingSequence,
        int protocolVersion,
        int localSchemaVersion,
        DateTimeOffset now,
        bool bootstrapRequired,
        long clockSkewSeconds)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || userId == Guid.Empty || projectId == Guid.Empty ||
            leaseId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId) || startingSequence < 0)
        {
            throw new DomainRuleException("sync.session.invalid", "Synchronization session parameters are invalid.");
        }

        return new SyncSession(
            id,
            tenantId,
            userId,
            projectId,
            deviceId.Trim(),
            leaseId,
            startingSequence,
            protocolVersion,
            localSchemaVersion,
            now,
            now.Add(SyncPolicy.SessionDuration),
            bootstrapRequired,
            clockSkewSeconds);
    }

    public bool IsValidFor(Guid tenantId, Guid userId, Guid projectId, string deviceId, DateTimeOffset now) =>
        ClosedAt is null &&
        ExpiresAt > now &&
        TenantId == tenantId &&
        UserId == userId &&
        ProjectId == projectId &&
        string.Equals(DeviceId, deviceId, StringComparison.Ordinal);

    public void Close(DateTimeOffset now) => ClosedAt ??= now;
}

public sealed class SyncConflictCase : AggregateRoot
{
    private SyncConflictCase()
    {
    }

    private SyncConflictCase(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid userId,
        string deviceId,
        string operationId,
        string entityType,
        Guid entityId,
        string commandType,
        long? localBaseRevision,
        long? serverRevision,
        string reasonCode,
        string localIntentJson,
        string? serverProjectionJson,
        DateTimeOffset detectedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        UserId = userId;
        DeviceId = deviceId;
        OperationId = operationId;
        EntityType = entityType;
        EntityId = entityId;
        CommandType = commandType;
        LocalBaseRevision = localBaseRevision;
        ServerRevision = serverRevision;
        ReasonCode = reasonCode;
        LocalIntentJson = localIntentJson;
        ServerProjectionJson = serverProjectionJson;
        Status = SyncConflictStatus.Open;
        DetectedAt = detectedAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string OperationId { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string CommandType { get; private set; } = string.Empty;
    public long? LocalBaseRevision { get; private set; }
    public long? ServerRevision { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string LocalIntentJson { get; private set; } = "{}";
    public string? ServerProjectionJson { get; private set; }
    public SyncConflictStatus Status { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public SyncConflictResolutionType? ResolutionType { get; private set; }
    public string? ResolutionComment { get; private set; }
    public string? ReplacementOperationId { get; private set; }

    public static SyncConflictCase Detect(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid userId,
        string deviceId,
        string operationId,
        string entityType,
        Guid entityId,
        string commandType,
        long? localBaseRevision,
        long? serverRevision,
        string reasonCode,
        string localIntentJson,
        string? serverProjectionJson,
        DateTimeOffset detectedAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || userId == Guid.Empty ||
            entityId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(operationId) ||
            string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(commandType) ||
            string.IsNullOrWhiteSpace(reasonCode) || string.IsNullOrWhiteSpace(localIntentJson))
        {
            throw new DomainRuleException("sync.conflict.invalid", "Conflict identity and both sides of its context are required.");
        }

        return new SyncConflictCase(
            id, tenantId, projectId, userId, deviceId.Trim(), operationId.Trim(), entityType.Trim(), entityId,
            commandType.Trim(), localBaseRevision, serverRevision, reasonCode.Trim(), localIntentJson,
            serverProjectionJson, detectedAt);
    }

    public SyncConflictResolution Resolve(
        Guid resolutionId,
        long baseRevision,
        Guid resolver,
        SyncConflictResolutionType resolutionType,
        string? replacementOperationId,
        string? comment,
        DateTimeOffset now)
    {
        if (Status != SyncConflictStatus.Open)
        {
            throw new DomainRuleException("sync.conflict.already_resolved", "Conflict has already been resolved.");
        }

        if (Revision != baseRevision)
        {
            throw new DomainRuleException("sync.conflict.revision.conflict", "Conflict changed after it was loaded.");
        }

        if (resolver == Guid.Empty || !Enum.IsDefined(resolutionType))
        {
            throw new DomainRuleException("sync.conflict.resolution.invalid", "Conflict resolver and resolution are required.");
        }

        var normalizedReplacement = string.IsNullOrWhiteSpace(replacementOperationId) ? null : replacementOperationId.Trim();
        if (resolutionType == SyncConflictResolutionType.Reapply &&
            (normalizedReplacement is null || normalizedReplacement == OperationId))
        {
            throw new DomainRuleException(
                "sync.conflict.replacement.required",
                "Reapply resolution requires a new immutable operation identifier.");
        }

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalizedComment?.Length > 1_000)
        {
            throw new DomainRuleException("sync.conflict.comment.too_long", "Resolution comment is too long.");
        }

        Status = SyncConflictStatus.Resolved;
        ResolvedAt = now;
        ResolvedBy = resolver;
        ResolutionType = resolutionType;
        ResolutionComment = normalizedComment;
        ReplacementOperationId = normalizedReplacement;
        AdvanceRevision();

        return SyncConflictResolution.Create(
            resolutionId,
            Id,
            TenantId,
            ProjectId,
            resolver,
            resolutionType,
            normalizedReplacement,
            normalizedComment,
            now);
    }
}

public sealed class SyncConflictResolution
{
    private SyncConflictResolution()
    {
    }

    private SyncConflictResolution(
        Guid id,
        Guid conflictId,
        Guid tenantId,
        Guid projectId,
        Guid resolvedBy,
        SyncConflictResolutionType resolutionType,
        string? replacementOperationId,
        string? comment,
        DateTimeOffset resolvedAt)
    {
        Id = id;
        ConflictId = conflictId;
        TenantId = tenantId;
        ProjectId = projectId;
        ResolvedBy = resolvedBy;
        ResolutionType = resolutionType;
        ReplacementOperationId = replacementOperationId;
        Comment = comment;
        ResolvedAt = resolvedAt;
    }

    public Guid Id { get; private set; }
    public Guid ConflictId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ResolvedBy { get; private set; }
    public SyncConflictResolutionType ResolutionType { get; private set; }
    public string? ReplacementOperationId { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset ResolvedAt { get; private set; }

    internal static SyncConflictResolution Create(
        Guid id,
        Guid conflictId,
        Guid tenantId,
        Guid projectId,
        Guid resolvedBy,
        SyncConflictResolutionType resolutionType,
        string? replacementOperationId,
        string? comment,
        DateTimeOffset resolvedAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException("sync.conflict.resolution.identity.required", "Resolution identifier is required.");
        }

        return new SyncConflictResolution(
            id, conflictId, tenantId, projectId, resolvedBy, resolutionType, replacementOperationId, comment, resolvedAt);
    }
}

public static class SyncPolicy
{
    public const int ProtocolVersion = 3;
    public const int LocalSchemaVersion = 6;
    public const int MaximumBatchSize = 100;
    public const string PolicyVersion = "sync-policy-v3";
    public static readonly TimeSpan SessionDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaximumLeaseDuration = TimeSpan.FromDays(7);
    public static readonly TimeSpan MaximumOfflineOperationAge = TimeSpan.FromDays(7);
    public static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

    public static SyncCompatibility EvaluateCompatibility(string appVersion, int protocolVersion, int localSchemaVersion)
    {
        var normalizedVersion = appVersion?.Split('-', 2, StringSplitOptions.TrimEntries)[0];
        return !Version.TryParse(normalizedVersion, out var parsed) || parsed < new Version(0, 1, 0)
            ? new SyncCompatibility(false, "sync.client.update_required")
            : protocolVersion != ProtocolVersion
                ? new SyncCompatibility(false, "sync.protocol.unsupported")
                : localSchemaVersion != LocalSchemaVersion
                    ? new SyncCompatibility(false, "sync.schema.update_required")
                    : new SyncCompatibility(true, null);
    }
}

public sealed record SyncCompatibility(bool IsCompatible, string? BlockingCode);

public static class SyncRecoveryHealth
{
    public static SyncRecoveryState Evaluate(
        SyncDeviceStatus deviceStatus,
        DateTimeOffset? leaseExpiresAt,
        DateTimeOffset now,
        long checkpointSequence,
        long serverWatermark,
        int openConflictCount,
        int recentRejectedOperationCount)
    {
        if (deviceStatus == SyncDeviceStatus.Revoked)
        {
            return SyncRecoveryState.DeviceRevoked;
        }

        if (!leaseExpiresAt.HasValue)
        {
            return SyncRecoveryState.NeverSynchronized;
        }

        if (leaseExpiresAt.Value <= now)
        {
            return SyncRecoveryState.LeaseExpired;
        }

        if (openConflictCount > 0 || recentRejectedOperationCount > 0)
        {
            return SyncRecoveryState.AttentionRequired;
        }

        if (checkpointSequence > serverWatermark)
        {
            return SyncRecoveryState.AttentionRequired;
        }

        return checkpointSequence < serverWatermark
            ? SyncRecoveryState.PendingPull
            : SyncRecoveryState.Healthy;
    }
}

public enum SyncRecoveryState
{
    NeverSynchronized = 1,
    Healthy = 2,
    PendingPull = 3,
    AttentionRequired = 4,
    LeaseExpired = 5,
    DeviceRevoked = 6
}

public enum SyncDeviceStatus
{
    Active = 1,
    Revoked = 2
}

public enum OfflineLeaseStatus
{
    Active = 1,
    Superseded = 2,
    Revoked = 3
}

public enum SyncConflictStatus
{
    Open = 1,
    Resolved = 2
}

public enum SyncConflictResolutionType
{
    KeepServer = 1,
    Reapply = 2
}
