namespace Pmcs.Modules.Platform.Persistence;

internal sealed class AuditEventRecord
{
    public Guid EventId { get; init; }

    public Guid TenantId { get; init; }

    public Guid? ProjectId { get; init; }

    public Guid ActorUserId { get; init; }

    public required string EventType { get; init; }

    public required string ResourceType { get; init; }

    public required string ResourceId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public required string Data { get; init; }

    public string? CorrelationId { get; init; }
}
