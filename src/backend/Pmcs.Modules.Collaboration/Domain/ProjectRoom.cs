namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectRoom
{
    private ProjectRoom() { }

    public Guid ProjectId { get; private set; }
    public Guid TenantId { get; private set; }
    public long LastSequence { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
