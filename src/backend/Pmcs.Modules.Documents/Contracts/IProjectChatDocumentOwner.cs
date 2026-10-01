namespace Pmcs.Modules.Documents.Contracts;

// The document owner is resolved by Collaboration without Documents reading its tables.
public interface IProjectChatDocumentOwner
{
    Task<bool> CanUploadAsync(Guid tenantId, Guid projectId, Guid messageId,
        Guid actorUserId, CancellationToken cancellationToken = default);
}
