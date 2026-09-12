using Npgsql;

namespace Pmcs.Api.Infrastructure;

internal static class ProductionConfigurationValidator
{
    private static readonly string[] DevelopmentMarkers = ["pmcs_dev_only", "pmcs_dev"];

    public static void Validate(
        IHostEnvironment environment,
        IConfiguration configuration,
        ReleaseIdentity releaseIdentity)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        RejectEnabledDevelopmentSwitch(configuration, "PMCS_DEV_IDENTITY_ENABLED");
        RejectEnabledDevelopmentSwitch(configuration, "PMCS_SEED_ENABLED");
        releaseIdentity.ValidateForProduction();

        var authority = Require(configuration, "Authentication:Authority");
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
            authorityUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Authentication:Authority must be an absolute HTTPS URI outside Development.");
        }

        _ = Require(configuration, "Authentication:Audience");
        var metadataAddress = configuration["Authentication:MetadataAddress"]?.Trim();
        if (!string.IsNullOrWhiteSpace(metadataAddress) &&
            (!Uri.TryCreate(metadataAddress, UriKind.Absolute, out var metadataUri) ||
                metadataUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Authentication:MetadataAddress must be an absolute HTTPS URI outside Development when configured.");
        }

        var origins = WebOriginConfiguration.Read(configuration);
        if (origins.Count == 0 || origins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("At least one absolute HTTPS PMCS web origin is required outside Development.");
        }

        if (string.Equals(configuration["AllowedHosts"], "*", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AllowedHosts cannot be '*' outside Development.");
        }

        var connectionString = Require(configuration, "ConnectionStrings:Pmcs");
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        RejectDevelopmentSecret(connection.Password, "ConnectionStrings:Pmcs");
        RejectDevelopmentSecret(Require(configuration, "ObjectStorage:AccessKey"), "ObjectStorage:AccessKey");
        RejectDevelopmentSecret(Require(configuration, "ObjectStorage:SecretKey"), "ObjectStorage:SecretKey");
        _ = Require(configuration, "ObjectStorage:ServiceUrl");
        _ = Require(configuration, "ObjectStorage:BucketName");
        if (bool.TryParse(configuration["ObjectStorage:CreateBucketIfMissing"], out var createBucket) && createBucket)
        {
            throw new InvalidOperationException(
                "ObjectStorage:CreateBucketIfMissing must be false outside Development; provision the private bucket separately.");
        }

        if (bool.TryParse(configuration["IdentityProvisioning:Enabled"], out var provisioningEnabled) && provisioningEnabled)
        {
            RequireHttps(configuration, "IdentityProvisioning:BaseUrl");
            RequireHttps(configuration, "IdentityProvisioning:WebReturnUrl");
            _ = Require(configuration, "IdentityProvisioning:Realm");
            _ = Require(configuration, "IdentityProvisioning:ClientId");
            RejectDevelopmentSecret(
                Require(configuration, "IdentityProvisioning:ClientSecret"),
                "IdentityProvisioning:ClientSecret");
        }
    }

    private static void RejectEnabledDevelopmentSwitch(IConfiguration configuration, string key)
    {
        if (bool.TryParse(configuration[key], out var enabled) && enabled)
        {
            throw new InvalidOperationException($"{key} cannot be enabled outside Development.");
        }
    }

    private static string Require(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} is required outside Development.")
            : value;
    }

    private static void RejectDevelopmentSecret(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            DevelopmentMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{key} must not use a development credential outside Development.");
        }
    }

    private static void RequireHttps(IConfiguration configuration, string key)
    {
        var value = Require(configuration, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTPS URI outside Development.");
        }
    }
}
