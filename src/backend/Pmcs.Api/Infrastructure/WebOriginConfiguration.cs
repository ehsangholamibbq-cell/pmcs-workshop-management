namespace Pmcs.Api.Infrastructure;

internal static class WebOriginConfiguration
{
    public static IReadOnlyList<string> Read(IConfiguration configuration)
    {
        var configured = configuration["PMCS_WEB_ORIGINS"] ?? configuration["PMCS_WEB_ORIGIN"];
        return (configured ?? "http://localhost:3000")
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
