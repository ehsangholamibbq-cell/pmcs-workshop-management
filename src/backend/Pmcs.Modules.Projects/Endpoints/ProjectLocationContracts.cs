using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Endpoints;

public sealed record CreateProjectLocationRequest(
    string Code,
    string Name,
    Guid ParentLocationId);

public sealed record RetireProjectLocationRequest(long BaseRevision);

public sealed record ProjectLocationResponse(
    Guid Id,
    Guid ProjectId,
    string Code,
    string Name,
    Guid? ParentLocationId,
    ProjectLocationStatus Status,
    DateTimeOffset ChangedAt,
    long Revision)
{
    public static ProjectLocationResponse From(ProjectLocation location) => new(
        location.Id,
        location.ProjectId,
        location.Code,
        location.Name,
        location.ParentLocationId,
        location.Status,
        location.ChangedAt,
        location.Revision);
}
