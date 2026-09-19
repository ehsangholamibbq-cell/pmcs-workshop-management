using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Pmcs.Modules.Reporting.Persistence;

namespace Pmcs.Modules.Reporting.Services;

internal static class ReportRunAdvisoryLock
{
    public static async Task AcquireAsync(
        ReportingDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty || dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A report-run advisory lock requires a non-empty run id and an active transaction.");
        }

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction)dbContext.Database.CurrentTransaction.GetDbTransaction();
        await using var command = new NpgsqlCommand(
            "select pg_advisory_xact_lock(hashtext('pmcs-reporting-run'), hashtext(@run_id));",
            connection,
            transaction);
        command.Parameters.AddWithValue("run_id", runId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
