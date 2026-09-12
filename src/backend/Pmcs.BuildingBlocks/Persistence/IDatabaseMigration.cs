namespace Pmcs.BuildingBlocks.Persistence;

public interface IDatabaseMigration
{
    string ModuleName { get; }

    long Order { get; }

    string Version { get; }

    string Description { get; }

    string Sql { get; }
}
