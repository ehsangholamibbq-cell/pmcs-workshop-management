namespace Pmcs.Modules.Sync.Persistence;

internal sealed class SyncChangeFeedEntry
{
    public long Sequence { get; set; }
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ActorUserId { get; set; }
    public string SourceDeviceId { get; set; } = string.Empty;
    public string? OperationId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public long? Revision { get; set; }
    public string ProjectionJson { get; set; } = "{}";
    public string Classification { get; set; } = "General";
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset ServerAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

internal sealed class DeviceCheckpoint
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Dataset { get; set; } = string.Empty;
    public long LastSequence { get; set; }
    public string CurrentToken { get; set; } = string.Empty;
    public DateTimeOffset AdvancedAt { get; set; }
}

internal sealed class CheckpointOffer
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Dataset { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
