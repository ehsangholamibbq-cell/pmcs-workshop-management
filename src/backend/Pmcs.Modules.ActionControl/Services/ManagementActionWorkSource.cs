using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Persistence;

namespace Pmcs.Modules.ActionControl.Services;

internal sealed class ManagementActionWorkSource(ActionControlDbContext dbContext) : IManagementActionWorkSource
{
    public async Task<IReadOnlyCollection<ManagementActionWorkRecord>> ListAssignedAsync(
        Guid tenantId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Actions
            .AsNoTracking()
            .Where(action => action.TenantId == tenantId && action.ProjectId == projectId &&
                action.AssigneeUserId == userId &&
                action.Status != ManagementActionStatus.Done &&
                action.Status != ManagementActionStatus.Cancelled)
            .OrderBy(action => action.DueDate)
            .ThenByDescending(action => action.Priority)
            .Take(200)
            .Select(action => new ManagementActionWorkRecord(
                action.Id,
                action.Title,
                action.Description,
                action.DueDate,
                action.Priority,
                action.Status,
                action.LastChangedAt ?? action.CreatedAt,
                action.Revision))
            .ToArrayAsync(cancellationToken);
}
