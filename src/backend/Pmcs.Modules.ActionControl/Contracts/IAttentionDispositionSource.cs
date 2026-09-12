namespace Pmcs.Modules.ActionControl.Contracts;

public interface IAttentionDispositionSource
{
    Task<IReadOnlyDictionary<Guid, AttentionDispositionRecord>> LoadAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyCollection<Guid> sourceFactIds,
        CancellationToken cancellationToken = default);
}

public sealed record AttentionDispositionRecord(
    Guid SourceFactId,
    AttentionDispositionRecordKind Kind,
    Guid? ActionId,
    string? Reason,
    DateTimeOffset DecidedAt);

public enum AttentionDispositionRecordKind
{
    ConvertedToAction = 1,
    Dismissed = 2
}
