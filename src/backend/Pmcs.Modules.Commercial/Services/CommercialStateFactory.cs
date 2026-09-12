using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Commercial.Services;

internal sealed class CommercialStateFactory(
    CommercialDbContext dbContext,
    IProjectDirectory projectDirectory,
    IClock clock)
{
    public async Task<CommercialStateSnapshot?> CreateSnapshotAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var contracts = await dbContext.Contracts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .Select(item => new CommercialContractFact(
                item.Id,
                item.Status,
                item.OriginalApprovedAmount,
                item.CurrencyCode,
                item.EndDate,
                item.ChangedAt))
            .ToListAsync(cancellationToken);
        var amendments = await dbContext.ContractAmendments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .Select(item => new CommercialAmendmentFact(
                item.Id,
                item.ContractId,
                item.Status,
                item.AmountDelta,
                item.CurrencyCode,
                item.ChangedAt))
            .ToListAsync(cancellationToken);
        var requests = await dbContext.PurchaseRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .Select(item => new CommercialRequestFact(item.Id, item.Status, item.CurrencyCode, item.ChangedAt))
            .ToListAsync(cancellationToken);
        var orders = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .Select(item => new CommercialOrderFact(
                item.Id,
                item.Status,
                item.Amount,
                item.CurrencyCode,
                item.DeliveryDueDate,
                item.ChangedAt))
            .ToListAsync(cancellationToken);
        var activePartyCount = await dbContext.Parties.AsNoTracking().CountAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Status == PartyStatus.Active,
            cancellationToken);
        var calculation = CommercialStateCalculator.Calculate(
            project,
            contracts,
            amendments,
            requests,
            orders,
            activePartyCount,
            ResolveLocalDate(clock.UtcNow, project.TimeZone),
            clock.UtcNow);
        return CommercialStateSnapshot.Create(Guid.NewGuid(), calculation);
    }

    internal static DateOnly ResolveLocalDate(DateTimeOffset now, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
    }
}
