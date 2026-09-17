using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Pmcs.Modules.QualityAssurance;

namespace Pmcs.Api.Infrastructure;

internal sealed class QualityAssuranceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    QualityAssuranceRuntimeOptions qaOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PmcsQualityAssurance";
    public const string ApiKeyHeaderName = "X-Pmcs-QA-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!qaOptions.Enabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (Request.Headers["Authorization"].Count > 0)
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "QA authentication cannot be combined with Authorization credentials."));
        }

        var suppliedKeys = Request.Headers[ApiKeyHeaderName];
        if (suppliedKeys.Count != 1 ||
            !KeysMatch(qaOptions.AuthenticationKey, suppliedKeys[0]))
        {
            return Task.FromResult(AuthenticateResult.Fail("QA authentication key is invalid."));
        }

        if (!Guid.TryParse(Request.Headers["X-Tenant-Id"], out var tenantId) ||
            !Guid.TryParse(Request.Headers["X-User-Id"], out var userId) ||
            tenantId == Guid.Empty ||
            userId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "QA authentication requires valid tenant and user identities."));
        }

        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new("sub", userId.ToString()),
            new("device_id", "pmcs-qa-harness")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "sub", "role"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }

    internal static bool KeysMatch(string expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
