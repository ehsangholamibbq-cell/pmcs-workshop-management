namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectReadCursor
{
    private ProjectReadCursor() { }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public long LastReadSequence { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
