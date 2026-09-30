using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pmcs.Modules.Intelligence.Services;

internal enum ProviderProbeStatus { Unavailable, Unverified, Available, Failed }

internal sealed record ProviderProbeResult(
    string Provider, string? Model, ProviderProbeStatus Status, string Code,
    int? InputTokens = null, int? OutputTokens = null);

internal sealed record ModelProviderConfiguration(string? Model, string? ApiKey);

internal interface IModelProviderProbe
{
    string Provider { get; }
    ProviderProbeResult Availability { get; }
    Task<ProviderProbeResult> ProbeAsync(CancellationToken cancellationToken);
}

// Probes send no PMCS project data. Provider credentials never enter a response, DB row or log.
internal abstract class ModelProviderProbe(
    HttpClient client, ModelProviderConfiguration configuration) : IModelProviderProbe
{
    private const int MaximumResponseBytes = 32_768;
    protected static readonly string[] RequiredProbeFields = ["ok"];
    protected static readonly object ProbeSchema = new
    {
        type = "object",
        properties = new { ok = new { type = "boolean" } },
        required = RequiredProbeFields,
        additionalProperties = false
    };

    public abstract string Provider { get; }
    public ProviderProbeResult Availability =>
        string.IsNullOrWhiteSpace(configuration.Model) || string.IsNullOrWhiteSpace(configuration.ApiKey)
            ? new(Provider, configuration.Model, ProviderProbeStatus.Unavailable, "ai.provider.not_configured")
            : new(Provider, configuration.Model, ProviderProbeStatus.Unverified, "ai.provider.configured_unverified");

    protected string Model => configuration.Model!;
    protected string ApiKey => configuration.ApiKey!;
    protected abstract HttpRequestMessage CreateRequest();
    protected abstract (string Text, int? InputTokens, int? OutputTokens) ParseResponse(JsonElement root);

    public async Task<ProviderProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var availability = Availability;
        if (availability.Status == ProviderProbeStatus.Unavailable)
        {
            return availability;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var request = CreateRequest();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new(Provider, Model, ProviderProbeStatus.Failed,
                    response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        ? "ai.provider.credentials_rejected" : "ai.provider.connection_failed");
            }

            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
            {
                return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.invalid_response");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[4_096];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + read > MaximumResponseBytes)
                {
                    return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.invalid_response");
                }
                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            using var document = JsonDocument.Parse(buffer, new JsonDocumentOptions { MaxDepth = 32 });
            var (text, inputTokens, outputTokens) = ParseResponse(document.RootElement);
            using var output = JsonDocument.Parse(text);
            if (output.RootElement.ValueKind != JsonValueKind.Object ||
                !output.RootElement.TryGetProperty("ok", out var ok) ||
                ok.ValueKind != JsonValueKind.True || output.RootElement.EnumerateObject().Count() != 1)
            {
                return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.invalid_response");
            }

            return new(Provider, Model, ProviderProbeStatus.Available, "ai.provider.connection_verified",
                inputTokens, outputTokens);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.timeout");
        }
        catch (HttpRequestException)
        {
            return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.connection_failed");
        }
        catch (JsonException)
        {
            return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.invalid_response");
        }
        catch (InvalidOperationException)
        {
            return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.invalid_response");
        }
        catch (KeyNotFoundException)
        {
            return new(Provider, Model, ProviderProbeStatus.Failed, "ai.provider.invalid_response");
        }
    }

    protected static string Text(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? throw new InvalidOperationException();

    protected static int? Tokens(JsonElement root, string usageProperty, string countProperty) =>
        root.TryGetProperty(usageProperty, out var usage) &&
        usage.TryGetProperty(countProperty, out var count) && count.TryGetInt32(out var value)
            ? value : null;
}

internal sealed class OpenAiModelProbe(HttpClient client, ModelProviderConfiguration config)
    : ModelProviderProbe(client, config)
{
    public override string Provider => "OpenAI";

    protected override HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = Model, store = false, max_output_tokens = 64,
            input = "Return {\"ok\":true} for this connection test.",
            text = new { format = new { type = "json_schema", name = "pmcs_connection_probe",
                strict = true, schema = ProbeSchema } }
        });
        return request;
    }

    protected override (string, int?, int?) ParseResponse(JsonElement root)
    {
        if (Text(root, "status") != "completed") throw new InvalidOperationException();
        var content = root.GetProperty("output").EnumerateArray()
            .SelectMany(item => item.TryGetProperty("content", out var parts) &&
                parts.ValueKind == JsonValueKind.Array ? parts.EnumerateArray().ToArray() : [])
            .First(part => Text(part, "type") == "output_text");
        return (Text(content, "text"), Tokens(root, "usage", "input_tokens"),
            Tokens(root, "usage", "output_tokens"));
    }
}

internal sealed class GeminiModelProbe(HttpClient client, ModelProviderConfiguration config)
    : ModelProviderProbe(client, config)
{
    public override string Provider => "GoogleGemini";

    protected override HttpRequestMessage CreateRequest()
    {
        var path = Uri.EscapeDataString(Model);
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{path}:generateContent");
        request.Headers.Add("x-goog-api-key", ApiKey);
        request.Content = JsonContent.Create(new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = "Return {\"ok\":true} for this connection test." } } } },
            generationConfig = new
            {
                maxOutputTokens = 64, responseMimeType = "application/json",
                responseSchema = new { type = "OBJECT", properties = new { ok = new { type = "BOOLEAN" } },
                    required = RequiredProbeFields }
            }
        });
        return request;
    }

    protected override (string, int?, int?) ParseResponse(JsonElement root)
    {
        var candidate = root.GetProperty("candidates").EnumerateArray().First();
        if (Text(candidate, "finishReason") != "STOP") throw new InvalidOperationException();
        var part = candidate.GetProperty("content").GetProperty("parts").EnumerateArray().Single();
        return (Text(part, "text"), Tokens(root, "usageMetadata", "promptTokenCount"),
            Tokens(root, "usageMetadata", "candidatesTokenCount"));
    }
}

internal sealed class AnthropicModelProbe(HttpClient client, ModelProviderConfiguration config)
    : ModelProviderProbe(client, config)
{
    public override string Provider => "AnthropicClaude";

    protected override HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(new
        {
            model = Model, max_tokens = 64,
            messages = new[] { new { role = "user", content = "Return {\"ok\":true} for this connection test." } },
            output_config = new { format = new { type = "json_schema", schema = ProbeSchema } }
        });
        return request;
    }

    protected override (string, int?, int?) ParseResponse(JsonElement root)
    {
        if (Text(root, "stop_reason") != "end_turn") throw new InvalidOperationException();
        var part = root.GetProperty("content").EnumerateArray()
            .First(item => Text(item, "type") == "text");
        return (Text(part, "text"), Tokens(root, "usage", "input_tokens"),
            Tokens(root, "usage", "output_tokens"));
    }
}
