using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Planning.Persistence;

namespace Pmcs.Modules.Planning.Services;

internal sealed class MeasurementItemDirectory(PlanningDbContext dbContext) : IMeasurementItemDirectory
{
    public async Task<MeasurementItemValidation> ValidateAsync(
        Guid tenantId,
        Guid projectId,
        Guid measurementItemId,
        string? unit,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.MeasurementItems
            .AsNoTracking()
            .Where(candidate => candidate.TenantId == tenantId &&
                candidate.ProjectId == projectId &&
                candidate.Id == measurementItemId)
            .Select(candidate => new { candidate.Title, candidate.Unit, candidate.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return MeasurementItemValidation.Invalid("measurement_item.not_found");
        }

        if (item.Status != MeasurementItemStatus.Active)
        {
            return MeasurementItemValidation.Invalid("measurement_item.inactive");
        }

        if (string.IsNullOrWhiteSpace(unit) ||
            !string.Equals(item.Unit, unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return MeasurementItemValidation.Invalid("measurement_item.unit.mismatch");
        }

        return MeasurementItemValidation.Valid(item.Title, item.Unit);
    }
}
