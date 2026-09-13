using Pmcs.Modules.ActionControl.Domain;

namespace Pmcs.Modules.ActionControl.Contracts;

public interface IManagementActionWorkSource
{
    Task<IReadOnlyCollection<ManagementActionWorkRecord>> ListAssignedAsync(
        Guid tenantId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record ManagementActionWorkRecord(
    Guid ActionId,
    string Title,
    string? Description,
    DateOnly DueDate,
    ActionPriority Priority,
    ManagementActionStatus Status,
    DateTimeOffset ChangedAt,
    long Revision);
