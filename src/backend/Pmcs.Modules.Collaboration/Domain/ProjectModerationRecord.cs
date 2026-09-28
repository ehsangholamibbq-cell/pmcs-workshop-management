namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectModerationRecord
{
    private ProjectModerationRecord() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MessageId { get; private set; }
    public long MessageRevision { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static ProjectModerationRecord Create(ProjectMessage message,
        string action, string? reason, Guid actorUserId, DateTimeOffset at)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 500 ||
            normalized.Any(character => char.IsControl(character) && character != '\n'))
            throw new ArgumentException("An audited moderation reason of 1 to 500 characters is required.");
        return new ProjectModerationRecord
        {
            Id = Guid.NewGuid(), TenantId = message.TenantId,
            ProjectId = message.ProjectId, MessageId = message.Id,
            MessageRevision = message.Revision, Action = action, Reason = normalized,
            ActorUserId = actorUserId, OccurredAt = at
        };
    }
}
