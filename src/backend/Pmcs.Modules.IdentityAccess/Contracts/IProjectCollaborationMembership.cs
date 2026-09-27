namespace Pmcs.Modules.IdentityAccess.Contracts;

/// <summary>Current Identity-owned membership boundary for project group collaboration.</summary>
public interface IProjectCollaborationMembership
{
    Task<bool> IsActiveAsync(
        Guid tenantId, Guid projectId, Guid userId,
        CancellationToken cancellationToken = default);
}
