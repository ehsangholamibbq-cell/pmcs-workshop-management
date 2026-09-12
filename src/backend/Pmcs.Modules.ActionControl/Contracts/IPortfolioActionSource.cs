using Pmcs.Modules.ActionControl.Domain;

namespace Pmcs.Modules.ActionControl.Contracts;

public interface IPortfolioActionSource
{
    Task<IReadOnlyCollection<PortfolioActionRecord>> ListOpenAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);
}

public sealed record PortfolioActionRecord(
    Guid Id,
    Guid ProjectId,
    string Title,
    Guid AssigneeUserId,
    string AssigneeDisplayName,
    DateOnly DueDate,
    ActionPriority Priority,
    ManagementActionStatus Status,
    long Revision);
