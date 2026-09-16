using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Persistence;

namespace Pmcs.Modules.Commercial.Services;

internal sealed class CommercialReferenceDirectory(CommercialDbContext dbContext) : ICommercialReferenceDirectory
{
    public Task<CommercialReferenceValidation> ValidateAsync(
        Guid tenantId,
        Guid projectId,
        Guid? contractId,
        Guid? commitmentId,
        CancellationToken cancellationToken = default) =>
        ValidateAsync(tenantId, projectId, contractId, commitmentId, null, cancellationToken);

    public async Task<CommercialReferenceValidation> ValidateAsync(
        Guid tenantId,
        Guid projectId,
        Guid? contractId,
        Guid? commitmentId,
        Guid? partyId = null,
        CancellationToken cancellationToken = default)
    {
        if (partyId.HasValue && !await dbContext.Parties.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == partyId,
                cancellationToken))
        {
            return new(false, "finance.party.not_found");
        }

        if (contractId.HasValue)
        {
            var contract = await dbContext.Contracts.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == contractId)
                .Select(item => new { item.PartyId })
                .SingleOrDefaultAsync(cancellationToken);
            if (contract is null)
            {
                return new(false, "finance.contract.not_found");
            }

            if (partyId.HasValue && contract.PartyId != partyId)
            {
                return new(false, "finance.party_contract.mismatch");
            }
        }

        if (!commitmentId.HasValue)
        {
            return CommercialReferenceValidation.Valid;
        }

        var commitment = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == commitmentId)
            .Select(item => new { item.ContractId, item.PartyId })
            .SingleOrDefaultAsync(cancellationToken);
        if (commitment is null)
        {
            return new(false, "finance.commitment.not_found");
        }

        if (contractId.HasValue && commitment.ContractId != contractId)
        {
            return new(false, "finance.commercial_reference.mismatch");
        }

        return partyId.HasValue && commitment.PartyId != partyId
            ? new(false, "finance.party_commitment.mismatch")
            : CommercialReferenceValidation.Valid;
    }
}
