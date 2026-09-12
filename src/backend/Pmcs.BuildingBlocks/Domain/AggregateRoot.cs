namespace Pmcs.BuildingBlocks.Domain;

public abstract class AggregateRoot : Entity
{
    public long Revision { get; protected set; } = 1;

    protected void AdvanceRevision() => Revision++;
}
