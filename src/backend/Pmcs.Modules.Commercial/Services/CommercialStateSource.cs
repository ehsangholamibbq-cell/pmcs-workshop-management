using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Commercial.Services;

internal sealed class CommercialStateSource(
    CommercialDbContext dbContext,
    IProjectDirectory projectDirectory,
    IClock clock) : ICommercialStateSource
{
    public async Task<CommercialStateRecord?> GetCurrentAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var latest = await dbContext.CommercialStateSnapshots.AsNoTracking()
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

        var calculation = CommercialStateCalculator.Calculate(
            project,
            [],
            [],
            [],
            [],
            0,
            CommercialStateFactory.ResolveLocalDate(clock.UtcNow, project.TimeZone),
            clock.UtcNow);
        return From(calculation);
    }

    public async Task<IReadOnlyDictionary<Guid, CommercialStateRecord>> GetPortfolioAsync(
        Guid tenantId,
        IReadOnlyCollection<ProjectControlProfile> projects,
        CancellationToken cancellationToken = default)
    {
        if (projects.Count == 0)
        {
            return new Dictionary<Guid, CommercialStateRecord>();
        }

        var projectIds = projects.Select(project => project.Id).Distinct().ToArray();
        var latestSnapshotIds = dbContext.CommercialStateSnapshots
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && projectIds.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(group => group
                .OrderByDescending(item => item.CalculatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => item.Id)
                .First());
        var snapshots = await dbContext.CommercialStateSnapshots
            .AsNoTracking()
            .Where(item => latestSnapshotIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        var result = snapshots.ToDictionary(item => item.ProjectId, From);

        foreach (var project in projects.Where(project => !result.ContainsKey(project.Id)))
        {
            var calculation = CommercialStateCalculator.Calculate(
                project,
                [],
                [],
                [],
                [],
                0,
                CommercialStateFactory.ResolveLocalDate(clock.UtcNow, project.TimeZone),
                clock.UtcNow);
            result[project.Id] = From(calculation);
        }

        return result;
    }

    internal static CommercialStateRecord From(CommercialStateSnapshot item) => new(
        item.Id,
        item.CalculationVersion,
        item.AsOfDate,
        item.CalculatedAt,
        item.CurrencyCode,
        item.ContractState,
        item.ContractDataQualityStatus,
        item.ProcurementState,
        item.ProcurementDataQualityStatus,
        item.ActivePartyCount,
        item.RegisteredContractCount,
        item.ActiveContractCount,
        item.ContractsWithoutCeilingCount,
        item.PendingContractApprovalCount,
        item.ExpiredActiveContractCount,
        item.ApprovedAmendmentCount,
        item.ApprovedAmendmentDelta,
        item.ApprovedContractCeilingAmount,
        item.PurchaseRequestCount,
        item.PendingProcurementApprovalCount,
        item.ApprovedRequestsAwaitingOrderCount,
        item.OpenCommitmentCount,
        item.OverdueCommitmentCount,
        item.TotalCommittedAmount,
        item.OpenCommitmentAmount,
        item.SourceMaxChangedAt);

    internal static CommercialStateRecord From(CommercialStateCalculation item) => new(
        null,
        item.CalculationVersion,
        item.AsOfDate,
        item.CalculatedAt,
        item.CurrencyCode,
        item.ContractState,
        item.ContractDataQualityStatus,
        item.ProcurementState,
        item.ProcurementDataQualityStatus,
        item.ActivePartyCount,
        item.RegisteredContractCount,
        item.ActiveContractCount,
        item.ContractsWithoutCeilingCount,
        item.PendingContractApprovalCount,
        item.ExpiredActiveContractCount,
        item.ApprovedAmendmentCount,
        item.ApprovedAmendmentDelta,
        item.ApprovedContractCeilingAmount,
        item.PurchaseRequestCount,
        item.PendingProcurementApprovalCount,
        item.ApprovedRequestsAwaitingOrderCount,
        item.OpenCommitmentCount,
        item.OverdueCommitmentCount,
        item.TotalCommittedAmount,
        item.OpenCommitmentAmount,
        item.SourceMaxChangedAt);
}
