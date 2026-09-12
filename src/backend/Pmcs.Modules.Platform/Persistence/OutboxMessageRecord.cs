namespace Pmcs.Modules.Platform.Persistence;

internal sealed class OutboxMessageRecord
{
    public Guid MessageId { get; init; }

    public Guid TenantId { get; init; }

    public Guid? ProjectId { get; init; }

    public required string EventType { get; init; }

    public int EventVersion { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public required string Payload { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
