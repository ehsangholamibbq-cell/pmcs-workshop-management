using System.Text.Json;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Sync.Domain;

namespace Pmcs.Modules.Sync.Endpoints;

public sealed record SyncHandshakeRequest(
    string DeviceId,
    Guid ProjectId,
    string DeviceName,
    string Platform,
    string AppVersion,
    int ProtocolVersion,
    int LocalSchemaVersion,
    string? LastCheckpoint,
    DateTimeOffset DeviceTime,
    SyncQueueSummary Queue);

public sealed record SyncQueueSummary(
    int PendingOperations,
    int PendingAttachments,
    long PendingAttachmentBytes,
    DateTimeOffset? OldestOperationAt);

public sealed record SyncHandshakeResponse(
    Guid SessionId,
    DateTimeOffset SessionExpiresAt,
    DateTimeOffset ServerAt,
    SyncLeaseModel Lease,
    string PolicyVersion,
    int ProtocolVersion,
    int LocalSchemaVersion,
    bool BootstrapRequired,
    long ServerWatermark,
    string? CurrentCheckpoint,
    long CurrentCheckpointSequence,
    int MaximumBatchSize,
    int MaximumAttachmentSizeBytes,
    long ClockSkewSeconds,
    bool ClockSkewWarning,
    IReadOnlyCollection<string> AllowedOperations,
    SyncDatasetManifestModel Dataset);

public sealed record SyncLeaseModel(
    Guid LeaseId,
    long AuthorizationVersion,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record SyncDatasetManifestModel(
    Guid TenantId,
    Guid ProjectId,
    Guid UserId,
    string DeviceId,
    IReadOnlyCollection<string> EntityTypes,
    IReadOnlyCollection<string> Scopes,
    string Classification,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt);

public sealed record SyncPushRequest(
    string DeviceId,
    IReadOnlyCollection<SyncOperationRequest> Operations);

public sealed record SyncOperationRequest(
    string OperationId,
    Guid ProjectId,
    string EntityType,
    Guid EntityId,
    string CommandType,
    long? BaseRevision,
    int PayloadSchemaVersion,
    DateTimeOffset CreatedAtDevice,
    JsonElement Payload,
    Guid OfflineLeaseId,
    long AuthorizationVersion,
    long LocalSequence,
    IReadOnlyCollection<string>? Dependencies,
    string CorrelationId,
    int DeviceTimezoneOffsetMinutes);

public sealed record SyncPushResponse(
    DateTimeOffset ProcessedAt,
    IReadOnlyCollection<SyncOperationResponse> Operations);

public sealed record SyncOperationResponse(
    string OperationId,
    OfflineFieldOperationStatus Status,
    Guid? EntityId,
    long? ServerRevision,
    string? Code,
    string? Message,
    bool WasReplay,
    Guid? ConflictId);

public sealed record SyncPullResponse(
    DateTimeOffset ServerAt,
    long ServerWatermark,
    Guid CheckpointOffer,
    DateTimeOffset CheckpointOfferExpiresAt,
    bool HasMore,
    IReadOnlyCollection<SyncChangeModel> Changes);

public sealed record SyncChangeModel(
    long Sequence,
    Guid ChangeId,
    string EntityType,
    Guid EntityId,
    string ChangeType,
    long? Revision,
    JsonElement Projection,
    DateTimeOffset EffectiveAt,
    DateTimeOffset ServerAt,
    string CorrelationId);

public sealed record AdvanceCheckpointRequest(Guid ProjectId, Guid CheckpointOffer);

public sealed record AdvanceCheckpointResponse(
    string Checkpoint,
    long Sequence,
    DateTimeOffset AdvancedAt);

public sealed record ResolveSyncConflictRequest(
    long BaseRevision,
    SyncConflictResolutionType Resolution,
    string? ReplacementOperationId,
    string? Comment);

public sealed record RevokeSyncDeviceRequest(long BaseRevision, string Reason);

public sealed record SyncDeviceModel(
    Guid RegistrationId,
    string DeviceId,
    string DisplayName,
    string Platform,
    string AppVersion,
    SyncDeviceStatus Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? RevokedAt,
    long Revision);

public sealed record SyncConflictModel(
    Guid ConflictId,
    Guid ProjectId,
    string DeviceId,
    string OperationId,
    string EntityType,
    Guid EntityId,
    string CommandType,
    long? LocalBaseRevision,
    long? ServerRevision,
    string ReasonCode,
    JsonElement LocalIntent,
    JsonElement? ServerProjection,
    SyncConflictStatus Status,
    DateTimeOffset DetectedAt,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedBy,
    SyncConflictResolutionType? ResolutionType,
    string? ReplacementOperationId,
    long Revision);

public sealed record SyncDiagnosticsModel(
    Guid ProjectId,
    string DeviceId,
    SyncDeviceStatus DeviceStatus,
    DateTimeOffset ServerAt,
    DateTimeOffset? LeaseExpiresAt,
    DateTimeOffset? LastHandshakeAt,
    long LastCheckpointSequence,
    DateTimeOffset? CheckpointAdvancedAt,
    int OpenConflictCount,
    int RecentRejectedOperationCount,
    int RecentReplayCount,
    DateTimeOffset? LastOperationAt,
    long ServerWatermark,
    long CheckpointLag,
    SyncRecoveryState RecoveryState,
    string PolicyVersion);
