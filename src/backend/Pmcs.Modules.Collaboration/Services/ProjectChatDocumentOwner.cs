using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Collaboration.Persistence;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.IdentityAccess.Contracts;

namespace Pmcs.Modules.Collaboration.Services;

internal sealed class ProjectChatDocumentOwner(
    CollaborationRuntimeOptions runtime, CollaborationDbContext db,
    IProjectCollaborationMembership membership, IProjectPermissionService permissions)
    : IProjectChatDocumentOwner
{
    public async Task<bool> CanUploadAsync(Guid tenantId, Guid projectId,
        Guid messageId, Guid actorUserId, CancellationToken cancellationToken = default) =>
        runtime.Enabled && tenantId != Guid.Empty && projectId != Guid.Empty &&
        messageId != Guid.Empty && actorUserId != Guid.Empty &&
        await membership.IsActiveAsync(tenantId, projectId, actorUserId, cancellationToken) &&
        await permissions.HasProjectPermissionAsync(tenantId, actorUserId,
            projectId, "collaboration.upload", cancellationToken) &&
        await db.Messages.AsNoTracking().AnyAsync(message =>
            message.TenantId == tenantId && message.ProjectId == projectId &&
            message.Id == messageId && message.AuthorUserId == actorUserId,
            cancellationToken);
}
