namespace Pmcs.BuildingBlocks.Application;

public sealed record OutboxEnvelope(
    Guid MessageId,
    Guid TenantId,
    Guid? ProjectId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    string Payload,
    string? CorrelationId);

public interface IOutboxWriter
{
    Task EnqueueAsync(OutboxEnvelope envelope, CancellationToken cancellationToken = default);
}
