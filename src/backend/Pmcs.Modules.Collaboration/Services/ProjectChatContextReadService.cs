using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Collaboration.Contracts;
using Pmcs.Modules.Collaboration.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Collaboration.Services;

internal sealed class ProjectChatContextReadService(
    CollaborationRuntimeOptions runtime, CollaborationDbContext db,
    IProjectCollaborationMembership membership, IProjectPermissionService permissions,
    IProjectDirectory projects) : IProjectChatContextReadService
{
    public async Task<IReadOnlyCollection<ProjectChatContextMessage>?> ListRecentAsync(
        Guid tenantId, Guid actorUserId, Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!runtime.Enabled || tenantId == Guid.Empty || actorUserId == Guid.Empty ||
            projectId == Guid.Empty ||
            !await membership.IsActiveAsync(tenantId, projectId, actorUserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(tenantId, actorUserId,
                projectId, "collaboration.read", cancellationToken) ||
            !await projects.ExistsAsync(tenantId, projectId, cancellationToken))
            return null;

        var messages = await db.Messages.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.DeletedAt == null && item.RedactedAt == null)
            .OrderByDescending(item => item.Sequence).Take(20)
            .Select(item => new ProjectChatContextMessage(item.Id, item.Sequence,
                item.AuthorUserId, item.Body, item.CreatedAt))
            .ToArrayAsync(cancellationToken);

        // A membership change while querying must not release previously read content.
        return await membership.IsActiveAsync(tenantId, projectId, actorUserId, cancellationToken) &&
            await permissions.HasProjectPermissionAsync(tenantId, actorUserId,
                projectId, "collaboration.read", cancellationToken)
            ? messages.Reverse().ToArray() : null;
    }
}
