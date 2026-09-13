namespace Pmcs.Modules.Projects.Contracts;

public interface IProjectLocationDirectory
{
    Task<ProjectLocationReference?> FindActiveAsync(
        Guid tenantId,
        Guid projectId,
        Guid locationId,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectLocationReference(
    Guid Id,
    Guid ProjectId,
    string Code,
    string Name,
    Guid? ParentLocationId);
