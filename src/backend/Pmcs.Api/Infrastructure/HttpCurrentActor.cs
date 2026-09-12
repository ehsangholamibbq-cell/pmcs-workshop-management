using Pmcs.BuildingBlocks.Application;

namespace Pmcs.Api.Infrastructure;

internal sealed class HttpCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    private HttpContext? Context => httpContextAccessor.HttpContext;

    public bool IsAuthenticated =>
        Context?.User.Identity?.IsAuthenticated == true &&
        TenantId != Guid.Empty &&
        UserId != Guid.Empty &&
        TokenIssuedAt.HasValue;

    public Guid TenantId => ReadGuidClaim("tenant_id");

    public Guid UserId => ReadGuidClaim("sub");

    public DateTimeOffset? TokenIssuedAt
    {
        get
        {
            if (string.Equals(
                    Context?.User.Identity?.AuthenticationType,
                    DevelopmentIdentityAuthenticationHandler.SchemeName,
                    StringComparison.Ordinal))
            {
                return DateTimeOffset.MaxValue;
            }

            return long.TryParse(Context?.User.FindFirst("iat")?.Value, out var issuedAt) && issuedAt >= 0
                ? TryReadIssuedAt(issuedAt)
                : null;
        }
    }

    public string? DeviceId => Context?.User.FindFirst("device_id")?.Value is { Length: > 0 } value
        ? value[..Math.Min(value.Length, 200)]
        : null;

    private Guid ReadGuidClaim(string claimType) =>
        Guid.TryParse(Context?.User.FindFirst(claimType)?.Value, out var value) ? value : Guid.Empty;

    private static DateTimeOffset? TryReadIssuedAt(long value)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
