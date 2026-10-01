namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectMessageRevision
{
    private ProjectMessageRevision() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MessageId { get; private set; }
    public long FromRevision { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static ProjectMessageRevision Capture(ProjectMessage message,
        string action, Guid actorUserId, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(), TenantId = message.TenantId,
        ProjectId = message.ProjectId, MessageId = message.Id,
        FromRevision = message.Revision, Body = message.Body,
        Action = action, ActorUserId = actorUserId, OccurredAt = at
    };
}
