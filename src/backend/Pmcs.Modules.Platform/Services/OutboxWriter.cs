using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Platform.Persistence;

namespace Pmcs.Modules.Platform.Services;

internal sealed class OutboxWriter(PlatformDbContext dbContext) : IOutboxWriter
{
    public async Task EnqueueAsync(OutboxEnvelope envelope, CancellationToken cancellationToken = default)
    {
        dbContext.OutboxMessages.Add(new OutboxMessageRecord
        {
            MessageId = envelope.MessageId,
            TenantId = envelope.TenantId,
            ProjectId = envelope.ProjectId,
            EventType = envelope.EventType,
            EventVersion = envelope.EventVersion,
            OccurredAt = envelope.OccurredAt,
            Payload = envelope.Payload,
            CorrelationId = envelope.CorrelationId,
            Attempts = 0
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
