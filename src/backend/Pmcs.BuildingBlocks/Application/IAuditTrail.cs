namespace Pmcs.BuildingBlocks.Application;

public sealed record AuditEntry(
    Guid TenantId,
    Guid? ProjectId,
    Guid ActorUserId,
    string EventType,
    string ResourceType,
    string ResourceId,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, object?> Data,
    string? CorrelationId = null);

public interface IAuditTrail
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
