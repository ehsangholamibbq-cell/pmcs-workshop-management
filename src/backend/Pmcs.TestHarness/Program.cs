using System.Net.Http.Json;
using System.Text.Json;
using Pmcs.BuildingBlocks.Testing;
using Pmcs.Modules.QualityAssurance;

namespace Pmcs.TestHarness;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault()?.ToLowerInvariant() switch
            {
                "guard" => GuardDatabase(),
                "manifest" => WriteManifest(),
                "probe" => await ProbeAsync(),
                _ => WriteUsage()
            };
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int GuardDatabase()
    {
        var connectionString = ReadRequiredEnvironment("PMCS_QA_CONNECTION_STRING");
        Console.WriteLine(QaDatabaseSafety.RequireIsolatedDatabase(connectionString));
        return 0;
    }

    private static int WriteManifest()
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            rootLocationId = PmcsTestDataSet.RootLocationId,
            actors = PmcsTestDataSet.Actors
        }, JsonOptions));
        return 0;
    }

    private static async Task<int> ProbeAsync()
    {
        var baseUrl = ReadRequiredEnvironment("PMCS_QA_BASE_URL");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("PMCS_QA_BASE_URL must be an absolute HTTP or HTTPS URL.");
        }

        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        using var client = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/qa/v1/diagnostics");
        request.Headers.Add("X-Pmcs-QA-Key", key);
        request.Headers.Add("X-Tenant-Id", PmcsTestDataSet.TenantId.ToString());
        request.Headers.Add("X-User-Id", PmcsTestDataSet.QaSuperAdministrator.UserId.ToString());

        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"QA diagnostics returned HTTP {(int)response.StatusCode}.");
        }

        if (!payload.TryGetProperty("overallHealth", out var overallHealth) ||
            !string.Equals(overallHealth.GetString(), "Healthy", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("QA diagnostics did not report a healthy system.");
        }

        Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        return 0;
    }

    private static int WriteUsage()
    {
        Console.Error.WriteLine("Usage: Pmcs.TestHarness <guard|manifest|probe>");
        return 2;
    }

    private static string ReadRequiredEnvironment(string key)
    {
        var value = Environment.GetEnvironmentVariable(key)?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} is required.")
            : value;
    }
}
