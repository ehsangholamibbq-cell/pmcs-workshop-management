using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Finance.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Services;

internal sealed class FinanceControlReadService(
    FinanceDbContext dbContext,
    IFinancialStateSource financialStateSource,
    IProjectDirectory projectDirectory,
    IClock clock) : IFinanceControlReadService
{
    public async Task<FinanceControlStateRecord?> GetCurrentAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var financialState = await financialStateSource.GetCurrentAsync(tenantId, projectId, cancellationToken);
        if (financialState is null)
        {
            return null;
        }

        var obligations = await dbContext.FinancialObligations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                (item.Status == FinancialObligationStatus.Approved ||
                 item.Status == FinancialObligationStatus.PartiallySettled ||
                 item.Status == FinancialObligationStatus.Settled))
            .Select(item => new FinancialObligationEntry(
                item.Id,
                item.Type,
                item.IssueDate,
                item.DueDate,
                item.Amount,
                item.SettledAmount))
            .ToListAsync(cancellationToken);
        var pettyCash = await dbContext.PettyCashRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .Select(item => new PettyCashControlEntry(
                item.Id,
                item.ReconciliationDueDate,
                item.ApprovedAmount,
                item.Status))
            .ToListAsync(cancellationToken);
        var policy = await dbContext.ManagementFeePolicies.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.Status == ManagementFeePolicyStatus.Approved)
            .OrderByDescending(item => item.ReviewedAt)
            .Select(item => new ApprovedManagementFeePolicy(
                item.Id,
                item.RatePercent,
                item.CalculationBase,
                item.EffectiveFrom))
            .FirstOrDefaultAsync(cancellationToken);
        var asOfDate = FinancialStateSource.ResolveLocalDate(clock.UtcNow, project.TimeZone);
        var control = FinanceControlCalculator.Calculate(
            asOfDate,
            financialState.RecognizedSpend,
            obligations,
            pettyCash,
            policy);
        return new FinanceControlStateRecord(financialState, control);
    }
}
