using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.ActionControl.Persistence;

namespace Pmcs.Modules.ActionControl.Services;

internal sealed class AttentionDispositionSource(ActionControlDbContext dbContext) : IAttentionDispositionSource
{
    public async Task<IReadOnlyDictionary<Guid, AttentionDispositionRecord>> LoadAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyCollection<Guid> sourceFactIds,
        CancellationToken cancellationToken = default)
    {
        if (sourceFactIds.Count == 0)
        {
            return new Dictionary<Guid, AttentionDispositionRecord>();
        }

        var dispositions = await dbContext.AttentionDispositions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                sourceFactIds.Contains(item.SourceFactId))
            .ToListAsync(cancellationToken);
        return dispositions.ToDictionary(
            item => item.SourceFactId,
            item => new AttentionDispositionRecord(
                item.SourceFactId,
                Enum.Parse<AttentionDispositionRecordKind>(item.Kind.ToString()),
                item.ActionId,
                item.Reason,
                item.DecidedAt));
    }
}
