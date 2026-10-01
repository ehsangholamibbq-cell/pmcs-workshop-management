namespace Pmcs.Modules.Collaboration.Contracts;

public sealed record ProjectChatContextMessage(
    Guid Id, long Sequence, Guid AuthorUserId, string Body, DateTimeOffset CreatedAt);

public interface IProjectChatContextReadService
{
    Task<IReadOnlyCollection<ProjectChatContextMessage>?> ListRecentAsync(
        Guid tenantId, Guid actorUserId, Guid projectId,
        CancellationToken cancellationToken = default);
}
