using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Persistence;

namespace Pmcs.Modules.ActionControl.Services;

internal sealed class PortfolioActionSource(ActionControlDbContext dbContext) : IPortfolioActionSource
{
    public async Task<IReadOnlyCollection<PortfolioActionRecord>> ListOpenAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
        {
            return [];
        }

        var ids = projectIds.Distinct().ToArray();
        return await dbContext.Actions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                ids.Contains(item.ProjectId) &&
                item.Status != ManagementActionStatus.Done &&
                item.Status != ManagementActionStatus.Cancelled)
            .OrderBy(item => item.DueDate)
            .ThenByDescending(item => item.Priority)
            .Select(item => new PortfolioActionRecord(
                item.Id,
                item.ProjectId,
                item.Title,
                item.AssigneeUserId,
                item.AssigneeDisplayName,
                item.DueDate,
                item.Priority,
                item.Status,
                item.Revision))
            .ToArrayAsync(cancellationToken);
    }
}
