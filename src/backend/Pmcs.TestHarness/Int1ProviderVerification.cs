using System.Text.Json;
using Pmcs.Modules.Intelligence.Services;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> ProbeInt1ProvidersAsync()
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        IModelProviderProbe[] providers =
        [
            new OpenAiModelProbe(client, new ModelProviderConfiguration(
                Environment.GetEnvironmentVariable("OPENAI_MODEL"),
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"))),
            new GeminiModelProbe(client, new ModelProviderConfiguration(
                Environment.GetEnvironmentVariable("GEMINI_MODEL"),
                Environment.GetEnvironmentVariable("GEMINI_API_KEY"))),
            new AnthropicModelProbe(client, new ModelProviderConfiguration(
                Environment.GetEnvironmentVariable("ANTHROPIC_MODEL"),
                Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        ];
        var evidence = new List<object>();
        var configuredFailure = false;
        foreach (var provider in providers)
        {
            var structured = await provider.ProbeAsync(CancellationToken.None);
            var tool = structured.Status == ProviderProbeStatus.Available
                ? await provider.ProbeToolCallingAsync(CancellationToken.None)
                : structured;
            if (provider.Availability.Status != ProviderProbeStatus.Unavailable &&
                (structured.Status != ProviderProbeStatus.Available ||
                 tool.Status != ProviderProbeStatus.Available))
                configuredFailure = true;
            evidence.Add(new
            {
                provider = provider.Provider,
                configuration = provider.Availability.Status.ToString(),
                structured = structured.Status.ToString(), structuredCode = structured.Code,
                toolCalling = tool.Status.ToString(), toolCode = tool.Code,
                structured.InputTokens, structured.OutputTokens
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            stage = "V1.1-INT1", liveProviderEvidence = evidence,
            configuredFailure, allAvailable = providers.All(provider =>
                provider.Availability.Status != ProviderProbeStatus.Unavailable) &&
                !configuredFailure
        }));
        return configuredFailure ? 1 : 0;
    }
}
