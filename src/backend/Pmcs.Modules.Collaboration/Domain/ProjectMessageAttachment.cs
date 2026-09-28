namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectMessageAttachment
{
    private ProjectMessageAttachment() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string DocumentSha256 { get; private set; } = string.Empty;
    public int DocumentVersion { get; private set; }
    public Guid AttachedBy { get; private set; }
    public DateTimeOffset AttachedAt { get; private set; }

    public static ProjectMessageAttachment Create(Guid tenantId, Guid projectId,
        Guid messageId, Guid documentId, string sha256, int documentVersion,
        Guid actorUserId, DateTimeOffset at)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || messageId == Guid.Empty ||
            documentId == Guid.Empty || actorUserId == Guid.Empty || documentVersion < 1 ||
            sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Released document scope and hash are required.");
        return new ProjectMessageAttachment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = projectId,
            MessageId = messageId, DocumentId = documentId,
            DocumentSha256 = sha256.ToLowerInvariant(), DocumentVersion = documentVersion,
            AttachedBy = actorUserId, AttachedAt = at
        };
    }
}
