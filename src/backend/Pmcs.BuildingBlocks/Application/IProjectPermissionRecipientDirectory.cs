namespace Pmcs.BuildingBlocks.Application;

public interface IProjectPermissionRecipientDirectory
{
    Task<IReadOnlyCollection<ProjectPermissionRecipient>> ListAsync(
        Guid tenantId,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectPermissionRecipient(Guid UserId, string DisplayName);
