using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ActorAccessValidator(IdentityAccessDbContext dbContext) : IActorAccessValidator
{
    public async Task<bool> HasAccessAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset tokenIssuedAt,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        var accessValidAfter = await (
            from user in dbContext.Users.AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
            where user.Id == userId &&
                user.TenantId == tenantId &&
                user.Status == UserAccountStatus.Active &&
                tenant.Status == TenantStatus.Active
            select (DateTimeOffset?)user.AccessValidAfter)
            .SingleOrDefaultAsync(cancellationToken);

        if (!accessValidAfter.HasValue)
        {
            return false;
        }

        if (tokenIssuedAt == DateTimeOffset.MaxValue)
        {
            return true;
        }

        return IsTokenCurrent(tokenIssuedAt, accessValidAfter.Value);
    }

    internal static bool IsTokenCurrent(DateTimeOffset tokenIssuedAt, DateTimeOffset accessValidAfter) =>
        tokenIssuedAt == DateTimeOffset.MaxValue ||
        tokenIssuedAt.ToUnixTimeSeconds() > accessValidAfter.ToUnixTimeSeconds();
}
