using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Platform.Services;

internal sealed partial class DatabaseMigrationRunner(
    IConfiguration configuration,
    IEnumerable<IDatabaseMigration> migrations,
    ILogger<DatabaseMigrationRunner> logger) : IHostedService
{
    private const string BootstrapSql = """
        create schema if not exists foundation;
        create table if not exists foundation.schema_migrations (
            module varchar(100) not null,
            version varchar(80) not null,
            description varchar(300) not null,
            applied_at timestamptz not null,
            primary key (module, version)
        );
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, BootstrapSql, cancellationToken);
        await ExecuteAsync(connection, "select pg_advisory_lock(hashtext('pmcs-foundation-migrations'));", cancellationToken);

        try
        {
            foreach (var migration in migrations.OrderBy(x => x.Order).ThenBy(x => x.ModuleName, StringComparer.Ordinal))
            {
                if (await IsAppliedAsync(connection, migration, cancellationToken))
                {
                    continue;
                }

                LogApplyingMigration(
                    logger,
                    migration.ModuleName,
                    migration.Version,
                    migration.Description);

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using (var command = new NpgsqlCommand(migration.Sql, connection, transaction))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var record = new NpgsqlCommand(
                    """
                    insert into foundation.schema_migrations(module, version, description, applied_at)
                    values (@module, @version, @description, now());
                    """,
                    connection,
                    transaction))
                {
                    record.Parameters.AddWithValue("module", migration.ModuleName);
                    record.Parameters.AddWithValue("version", migration.Version);
                    record.Parameters.AddWithValue("description", migration.Description);
                    await record.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            await ExecuteAsync(connection, "select pg_advisory_unlock(hashtext('pmcs-foundation-migrations'));", cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<bool> IsAppliedAsync(
        NpgsqlConnection connection,
        IDatabaseMigration migration,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select exists(select 1 from foundation.schema_migrations where module = @module and version = @version);",
            connection);
        command.Parameters.AddWithValue("module", migration.ModuleName);
        command.Parameters.AddWithValue("version", migration.Version);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Applying database migration {ModuleName}/{Version}: {Description}")]
    private static partial void LogApplyingMigration(
        ILogger logger,
        string moduleName,
        string version,
        string description);
}
