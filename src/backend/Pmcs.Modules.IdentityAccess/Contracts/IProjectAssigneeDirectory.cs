namespace Pmcs.Modules.IdentityAccess.Contracts;

public interface IProjectAssigneeDirectory
{
    Task<IReadOnlyCollection<ProjectAssignee>> ListAssignableAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectAssignee?> FindAssignableAsync(
        Guid tenantId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectAssignee(Guid UserId, string DisplayName, bool CanDecide);
