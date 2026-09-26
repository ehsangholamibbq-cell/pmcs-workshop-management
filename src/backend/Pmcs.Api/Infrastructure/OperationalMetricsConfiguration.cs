using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Pmcs.Api.Infrastructure;

internal static class OperationalMetricsConfiguration
{
    internal const string EndpointKey = "Observability:OtlpEndpoint";
    internal const string ReportingMeterName = "Pmcs.Reporting";

    public static void Configure(
        IServiceCollection services,
        IConfiguration configuration,
        ReleaseIdentity releaseIdentity)
    {
        var endpoint = ResolveEndpoint(configuration);
        if (endpoint is null)
        {
            return;
        }

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "pmcs-api",
                serviceVersion: releaseIdentity.Version))
            .WithMetrics(metrics => metrics
                .AddMeter(RequestTelemetryMiddleware.MeterName, ReportingMeterName)
                .AddOtlpExporter(options => options.Endpoint = endpoint));
    }

    internal static Uri? ResolveEndpoint(IConfiguration configuration)
    {
        var value = configuration[EndpointKey]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) &&
                !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new InvalidOperationException(
                $"{EndpointKey} must be an absolute HTTP(S) collector URI without credentials, query or fragment.");
        }

        return endpoint;
    }
}
