using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Finance.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Services;

internal sealed class FinancialStateSource(
    FinanceDbContext dbContext,
    IProjectDirectory projectDirectory,
    IClock clock) : IFinancialStateSource
{
    public async Task<FinancialStateRecord?> GetCurrentAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var latest = await dbContext.FinancialStateSnapshots
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CalculatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null)
        {
            return From(latest);
        }

        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var calculation = FinancialStateCalculator.Calculate(
            project,
            Array.Empty<PostedFinancialEntry>(),
            null,
            ResolveLocalDate(clock.UtcNow, project.TimeZone),
            clock.UtcNow);
        return From(calculation);
    }

    public async Task<IReadOnlyDictionary<Guid, FinancialStateRecord>> GetPortfolioAsync(
        Guid tenantId,
        IReadOnlyCollection<ProjectControlProfile> projects,
        CancellationToken cancellationToken = default)
    {
        if (projects.Count == 0)
        {
            return new Dictionary<Guid, FinancialStateRecord>();
        }

        var projectIds = projects.Select(project => project.Id).Distinct().ToArray();
        var latestSnapshotIds = dbContext.FinancialStateSnapshots
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && projectIds.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(group => group
                .OrderByDescending(item => item.CalculatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => item.Id)
                .First());
        var snapshots = await dbContext.FinancialStateSnapshots
            .AsNoTracking()
            .Where(item => latestSnapshotIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        var result = snapshots.ToDictionary(item => item.ProjectId, From);

        foreach (var project in projects.Where(project => !result.ContainsKey(project.Id)))
        {
            var calculation = FinancialStateCalculator.Calculate(
                project,
                [],
                null,
                ResolveLocalDate(clock.UtcNow, project.TimeZone),
                clock.UtcNow);
            result[project.Id] = From(calculation);
        }

        return result;
    }

    internal static FinancialStateRecord From(FinancialStateSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.CalculationVersion,
        snapshot.AsOfDate,
        snapshot.CalculatedAt,
        snapshot.CurrencyCode,
        snapshot.Status,
        snapshot.DataQualityStatus,
        snapshot.BudgetComparisonState,
        snapshot.PostedRecordCount,
        snapshot.TotalReceipts,
        snapshot.DirectPayments,
        snapshot.PettyCashFunding,
        snapshot.PettyCashExpenses,
        snapshot.ExternalNetCash,
        snapshot.RecognizedSpend,
        snapshot.PettyCashBalance,
        snapshot.ApprovedBudgetAmount,
        snapshot.BudgetRemainingAmount,
        snapshot.BudgetConsumedPercent,
        snapshot.SourceMaxChangedAt);

    internal static FinancialStateRecord From(FinancialStateCalculation calculation) => new(
        null,
        calculation.CalculationVersion,
        calculation.AsOfDate,
        calculation.CalculatedAt,
        calculation.CurrencyCode,
        calculation.Status,
        calculation.DataQualityStatus,
        calculation.BudgetComparisonState,
        calculation.PostedRecordCount,
        calculation.TotalReceipts,
        calculation.DirectPayments,
        calculation.PettyCashFunding,
        calculation.PettyCashExpenses,
        calculation.ExternalNetCash,
        calculation.RecognizedSpend,
        calculation.PettyCashBalance,
        calculation.ApprovedBudgetAmount,
        calculation.BudgetRemainingAmount,
        calculation.BudgetConsumedPercent,
        calculation.SourceMaxChangedAt);

    internal static DateOnly ResolveLocalDate(DateTimeOffset now, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
    }
}
