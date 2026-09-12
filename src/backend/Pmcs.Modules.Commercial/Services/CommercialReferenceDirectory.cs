using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Persistence;

namespace Pmcs.Modules.Commercial.Services;

internal sealed class CommercialReferenceDirectory(CommercialDbContext dbContext) : ICommercialReferenceDirectory
{
    public async Task<CommercialReferenceValidation> ValidateAsync(
        Guid tenantId,
        Guid projectId,
        Guid? contractId,
        Guid? commitmentId,
        CancellationToken cancellationToken = default)
    {
        if (contractId.HasValue && !await dbContext.Contracts.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == contractId,
                cancellationToken))
        {
            return new(false, "finance.contract.not_found");
        }

        if (!commitmentId.HasValue)
        {
            return CommercialReferenceValidation.Valid;
        }

        var commitment = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == commitmentId)
            .Select(item => new { item.ContractId })
            .SingleOrDefaultAsync(cancellationToken);
        if (commitment is null)
        {
            return new(false, "finance.commitment.not_found");
        }

        return contractId.HasValue && commitment.ContractId != contractId
            ? new(false, "finance.commercial_reference.mismatch")
            : CommercialReferenceValidation.Valid;
    }
}
