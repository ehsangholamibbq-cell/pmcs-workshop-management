using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Pmcs.Api.Infrastructure;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Services;

namespace Pmcs.Domain.Tests;

public sealed class InfrastructureBoundaryTests
{
    [Theory]
    [InlineData("abc-123_DEF.xyz", true)]
    [InlineData("contains a space", false)]
    [InlineData("contains\nnewline", false)]
    [InlineData("", false)]
    public void CorrelationIdValidationAllowsOnlyBoundedLogSafeValues(string value, bool expected)
    {
        Assert.Equal(expected, CorrelationIdMiddleware.IsValid(value));
    }

    [Fact]
    public void CorrelationIdValidationRejectsMoreThanSixtyFourCharacters()
    {
        Assert.False(CorrelationIdMiddleware.IsValid(new string('a', 65)));
    }

    [Fact]
    public void ProductionConfigurationAcceptsExplicitSecureBoundary()
    {
        ProductionConfigurationValidator.Validate(
            new TestEnvironment("Production"), ValidProductionConfiguration(), ValidReleaseIdentity());
    }

    [Fact]
    public void ProductionConfigurationRejectsDevelopmentCredentials()
    {
        var values = ValidProductionValues();
        values["ConnectionStrings:Pmcs"] =
            "Host=postgres;Database=pmcs;Username=pmcs;Password=pmcs_dev_only";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                new TestEnvironment("Production"),
                new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
                ValidReleaseIdentity()));

        Assert.Contains("development credential", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionConfigurationRejectsAutomaticBucketCreation()
    {
        var values = ValidProductionValues();
        values["ObjectStorage:CreateBucketIfMissing"] = "true";

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                new TestEnvironment("Production"),
                new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
                ValidReleaseIdentity()));
    }

    [Fact]
    public void ProductionConfigurationRejectsInsecureBackchannelMetadata()
    {
        var values = ValidProductionValues();
        values["Authentication:MetadataAddress"] =
            "http://identity-internal/realms/pmcs/.well-known/openid-configuration";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                new TestEnvironment("Production"),
                new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
                ValidReleaseIdentity()));

        Assert.Contains("MetadataAddress", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionConfigurationRejectsInsecureIdentityAdministrationEndpoint()
    {
        var values = ValidProductionValues();
        values["IdentityProvisioning:Enabled"] = "true";
        values["IdentityProvisioning:BaseUrl"] = "http://identity-internal";
        values["IdentityProvisioning:Realm"] = "pmcs";
        values["IdentityProvisioning:ClientId"] = "pmcs-identity-admin";
        values["IdentityProvisioning:ClientSecret"] = "a-long-production-identity-secret";
        values["IdentityProvisioning:WebReturnUrl"] = "https://pmcs.example.com";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                new TestEnvironment("Production"),
                new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
                ValidReleaseIdentity()));

        Assert.Contains("IdentityProvisioning:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionConfigurationAcceptsSecureIdentityAdministrationBoundary()
    {
        var values = ValidProductionValues();
        values["IdentityProvisioning:Enabled"] = "true";
        values["IdentityProvisioning:BaseUrl"] = "https://identity-admin.example.com";
        values["IdentityProvisioning:Realm"] = "pmcs";
        values["IdentityProvisioning:ClientId"] = "pmcs-identity-admin";
        values["IdentityProvisioning:ClientSecret"] = "a-long-production-identity-secret";
        values["IdentityProvisioning:WebReturnUrl"] = "https://pmcs.example.com";

        ProductionConfigurationValidator.Validate(
            new TestEnvironment("Production"),
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            ValidReleaseIdentity());
    }

    [Theory]
    [InlineData("development", "1.0.0", "2026-09-12T12:00:00Z")]
    [InlineData("6DEA26EFBC75EAC5D195C630BB4C4B9B64CCB7B2", "1.0.0", "2026-09-12T12:00:00Z")]
    [InlineData("6dea26efbc75eac5d195c630bb4c4b9b64ccb7b2", "release", "2026-09-12T12:00:00Z")]
    [InlineData("6dea26efbc75eac5d195c630bb4c4b9b64ccb7b2", "1.0.0", "unknown")]
    public void ProductionReleaseIdentityRejectsUnverifiableArtifact(
        string commit,
        string version,
        string builtAt)
    {
        var release = new ReleaseIdentity(1, "api", commit, version, builtAt);

        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                new TestEnvironment("Production"), ValidProductionConfiguration(), release));
    }

    [Fact]
    public void CurrentActorReadsValidatedIdentityClaims()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("tenant_id", tenantId.ToString()),
                    new Claim("sub", userId.ToString()),
                    new Claim("iat", "1789113600"),
                    new Claim("device_id", "field-device-01")
                ],
                "test"))
        };
        var actor = new HttpCurrentActor(new HttpContextAccessor { HttpContext = context });

        Assert.True(actor.IsAuthenticated);
        Assert.Equal(tenantId, actor.TenantId);
        Assert.Equal(userId, actor.UserId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1789113600), actor.TokenIssuedAt);
        Assert.Equal("field-device-01", actor.DeviceId);
    }

    [Fact]
    public void CurrentActorRejectsAuthenticatedPrincipalWithoutTenantBoundary()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", Guid.NewGuid().ToString())],
                "test"))
        };
        var actor = new HttpCurrentActor(new HttpContextAccessor { HttpContext = context });

        Assert.False(actor.IsAuthenticated);
        Assert.Equal(Guid.Empty, actor.TenantId);
    }

    [Fact]
    public void CurrentActorRejectsProductionIdentityWithoutIssuedAtClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("tenant_id", Guid.NewGuid().ToString()),
                    new Claim("sub", Guid.NewGuid().ToString())
                ],
                "Bearer"))
        };

        var actor = new HttpCurrentActor(new HttpContextAccessor { HttpContext = context });

        Assert.False(actor.IsAuthenticated);
        Assert.Null(actor.TokenIssuedAt);
    }

    [Fact]
    public void CurrentActorAllowsDevelopmentAdapterWithoutIssuedAtClaim()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("tenant_id", Guid.NewGuid().ToString()),
                    new Claim("sub", Guid.NewGuid().ToString())
                ],
                DevelopmentIdentityAuthenticationHandler.SchemeName))
        };

        var actor = new HttpCurrentActor(new HttpContextAccessor { HttpContext = context });

        Assert.True(actor.IsAuthenticated);
        Assert.Equal(DateTimeOffset.MaxValue, actor.TokenIssuedAt);
    }

    [Fact]
    public void AccessEpochRejectsTokensFromBeforeOrDuringStatusTransitionSecond()
    {
        var transition = DateTimeOffset.FromUnixTimeSeconds(1_789_113_600).AddMilliseconds(500);

        Assert.False(ActorAccessValidator.IsTokenCurrent(
            DateTimeOffset.FromUnixTimeSeconds(1_789_113_600),
            transition));
        Assert.True(ActorAccessValidator.IsTokenCurrent(
            DateTimeOffset.FromUnixTimeSeconds(1_789_113_601),
            transition));
    }

    [Fact]
    public async Task ActorAccessMiddlewareStopsMalformedAuthenticatedIdentity()
    {
        var nextCalled = false;
        var middleware = new ActorAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())], "Bearer")),
            Response = { Body = new MemoryStream() }
        };
        var actor = new StubActor(false, Guid.Empty, Guid.Empty, null);

        await middleware.InvokeAsync(context, actor, new StubAccessValidator(true));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ActorAccessMiddlewareStopsInactiveOrStaleAccount()
    {
        var nextCalled = false;
        var middleware = new ActorAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())], "Bearer")),
            Response = { Body = new MemoryStream() }
        };
        var actor = new StubActor(
            true,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.FromUnixTimeSeconds(1_789_113_601));

        await middleware.InvokeAsync(context, actor, new StubAccessValidator(false));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ActorAccessMiddlewareAllowsCurrentActiveAccount()
    {
        var nextCalled = false;
        var middleware = new ActorAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())], "Bearer"))
        };
        var actor = new StubActor(
            true,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.FromUnixTimeSeconds(1_789_113_601));

        await middleware.InvokeAsync(context, actor, new StubAccessValidator(true));

        Assert.True(nextCalled);
    }

    private static IConfiguration ValidProductionConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(ValidProductionValues()).Build();

    private static ReleaseIdentity ValidReleaseIdentity() => new(
        1,
        "api",
        "6dea26efbc75eac5d195c630bb4c4b9b64ccb7b2",
        "1.20.0-rc.1",
        "2026-09-12T12:00:00Z");

    private static Dictionary<string, string?> ValidProductionValues() => new()
    {
        ["Authentication:Authority"] = "https://identity.example.com",
        ["Authentication:Audience"] = "pmcs-api",
        ["PMCS_WEB_ORIGINS"] = "https://pmcs.example.com",
        ["AllowedHosts"] = "api.pmcs.example.com",
        ["ConnectionStrings:Pmcs"] =
            "Host=postgres;Database=pmcs;Username=pmcs_app;Password=a-long-production-password",
        ["ObjectStorage:ServiceUrl"] = "https://objects.example.com",
        ["ObjectStorage:AccessKey"] = "production-access-key",
        ["ObjectStorage:SecretKey"] = "a-long-production-secret",
        ["ObjectStorage:BucketName"] = "pmcs-production",
        ["ObjectStorage:CreateBucketIfMissing"] = "false"
    };

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Pmcs.Domain.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = environmentName;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record StubActor(
        bool IsAuthenticated,
        Guid TenantId,
        Guid UserId,
        DateTimeOffset? TokenIssuedAt) : ICurrentActor
    {
        public string? DeviceId => null;
    }

    private sealed class StubAccessValidator(bool allowed) : IActorAccessValidator
    {
        public Task<bool> HasAccessAsync(
            Guid tenantId,
            Guid userId,
            DateTimeOffset tokenIssuedAt,
            CancellationToken cancellationToken = default) => Task.FromResult(allowed);
    }
}
