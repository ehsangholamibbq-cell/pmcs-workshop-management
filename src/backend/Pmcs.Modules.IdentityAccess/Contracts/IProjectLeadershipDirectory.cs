namespace Pmcs.Modules.IdentityAccess.Contracts;

public interface IProjectLeadershipDirectory
{
    Task<IReadOnlyCollection<ProjectLeaderRecord>> ListProjectManagersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectLeaderRecord(
    Guid ProjectId,
    Guid UserId,
    string DisplayName);
