using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Pmcs.Api.Infrastructure;

internal sealed class DevelopmentIdentityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PmcsDevelopment";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var enabled = environment.IsDevelopment() &&
            bool.TryParse(configuration["PMCS_DEV_IDENTITY_ENABLED"], out var configured) &&
            configured;

        if (!enabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(Request.Headers["X-Tenant-Id"], out var tenantId) ||
            !Guid.TryParse(Request.Headers["X-User-Id"], out var userId) ||
            tenantId == Guid.Empty ||
            userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", userId.ToString())
        };
        var deviceId = Request.Headers["X-Device-Id"].ToString().Trim();
        if (deviceId is { Length: > 0 and <= 200 })
        {
            claims.Add(new Claim("device_id", deviceId));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "sub", "role"));
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
