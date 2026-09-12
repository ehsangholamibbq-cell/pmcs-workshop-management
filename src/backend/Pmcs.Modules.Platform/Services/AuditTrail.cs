using System.Text.Json;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Platform.Persistence;

namespace Pmcs.Modules.Platform.Services;

internal sealed class AuditTrail(PlatformDbContext dbContext) : IAuditTrail
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.AuditEvents.Add(new AuditEventRecord
        {
            EventId = Guid.NewGuid(),
            TenantId = entry.TenantId,
            ProjectId = entry.ProjectId,
            ActorUserId = entry.ActorUserId,
            EventType = entry.EventType,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            OccurredAt = entry.OccurredAt,
            Data = JsonSerializer.Serialize(entry.Data),
            CorrelationId = entry.CorrelationId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
