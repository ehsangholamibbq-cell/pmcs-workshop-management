using System.Net;
using System.Text;
using Pmcs.Modules.Intelligence.Services;

namespace Pmcs.Domain.Tests;

public sealed class ModelProviderProbeTests
{
    [Theory]
    [InlineData("OpenAI", "api.openai.com", """{"status":"completed","output":[{"content":[{"type":"output_text","text":"{\"ok\":true}"}]}],"usage":{"input_tokens":3,"output_tokens":2}}""")]
    [InlineData("GoogleGemini", "generativelanguage.googleapis.com", """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"ok\":true}"}]}}],"usageMetadata":{"promptTokenCount":3,"candidatesTokenCount":2}}""")]
    [InlineData("AnthropicClaude", "api.anthropic.com", """{"stop_reason":"end_turn","content":[{"type":"text","text":"{\"ok\":true}"}],"usage":{"input_tokens":3,"output_tokens":2}}""")]
    public async Task ConfiguredProbeUsesOwnEndpointAndRequiresValidStructuredOutput(
        string provider, string host, string responseJson)
    {
        var handler = new CaptureHandler(responseJson);
        var probe = Create(provider, new HttpClient(handler), new("test-model", "private-test-key"));
        Assert.Equal(ProviderProbeStatus.Unverified, probe.Availability.Status);

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ProviderProbeStatus.Available, result.Status);
        Assert.Equal("ai.provider.connection_verified", result.Code);
        Assert.Equal(3, result.InputTokens);
        Assert.Equal(2, result.OutputTokens);
        Assert.Equal(host, handler.RequestHost);
        Assert.DoesNotContain("private-test-key", handler.RequestBody);
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("GoogleGemini")]
    [InlineData("AnthropicClaude")]
    public async Task MissingCredentialNeverCallsProvider(string provider)
    {
        var handler = new CaptureHandler("{}", HttpStatusCode.OK);
        var probe = Create(provider, new HttpClient(handler), new("test-model", null));

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ProviderProbeStatus.Unavailable, result.Status);
        Assert.Equal("ai.provider.not_configured", result.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData("OpenAI", """{"status":"completed","output":[{"type":"function_call","name":"pmcs_probe","arguments":"{\"ok\":true}"}],"usage":{"input_tokens":3,"output_tokens":2}}""")]
    [InlineData("GoogleGemini", """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"functionCall":{"name":"pmcs_probe","args":{"ok":true}}}]}}],"usageMetadata":{"promptTokenCount":3,"candidatesTokenCount":2}}""")]
    [InlineData("AnthropicClaude", """{"stop_reason":"tool_use","content":[{"type":"tool_use","name":"pmcs_probe","input":{"ok":true}}],"usage":{"input_tokens":3,"output_tokens":2}}""")]
    public async Task NativeToolCompatibilityRequiresExactDeclaredCall(string provider, string responseJson)
    {
        var handler = new CaptureHandler(responseJson);
        var probe = Create(provider, new HttpClient(handler), new("test-model", "private-test-key"));

        var result = await probe.ProbeToolCallingAsync(CancellationToken.None);

        Assert.Equal(ProviderProbeStatus.Available, result.Status);
        Assert.Equal("ai.provider.tool_call_verified", result.Code);
        Assert.Contains("pmcs_probe", handler.RequestBody);
        if (provider == "GoogleGemini")
            Assert.Contains("parametersJsonSchema", handler.RequestBody, StringComparison.Ordinal);
        if (provider == "AnthropicClaude")
            Assert.Contains("\"strict\":true", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("private-test-key", handler.RequestBody);
    }

    [Theory]
    [InlineData("OpenAI", """{"status":"completed","output":[{"type":"function_call","name":"unexpected","arguments":"{\"ok\":true}"}]}""")]
    [InlineData("GoogleGemini", """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"functionCall":{"name":"unexpected","args":{"ok":true}}}]}}]}""")]
    [InlineData("AnthropicClaude", """{"stop_reason":"tool_use","content":[{"type":"tool_use","name":"unexpected","input":{"ok":true}}]}""")]
    public async Task UndeclaredToolCallFailsClosed(string provider, string responseJson)
    {
        var probe = Create(provider, new HttpClient(new CaptureHandler(responseJson)),
            new("test-model", "private-test-key"));
        var result = await probe.ProbeToolCallingAsync(CancellationToken.None);
        Assert.Equal(ProviderProbeStatus.Failed, result.Status);
    }

    [Theory]
    [InlineData("OpenAI", """{"status":"incomplete","output":[]}""")]
    [InlineData("GoogleGemini", """{"candidates":[{"finishReason":"MAX_TOKENS","content":{"parts":[{"text":"{\"ok\":true}"}]}}]}""")]
    [InlineData("AnthropicClaude", """{"stop_reason":"max_tokens","content":[{"type":"text","text":"{\"ok\":true}"}]}""")]
    public async Task IncompleteOutputFailsClosed(string provider, string responseJson)
    {
        var probe = Create(provider, new HttpClient(new CaptureHandler(responseJson)),
            new("test-model", "private-test-key"));

        var result = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ProviderProbeStatus.Failed, result.Status);
        Assert.Equal("ai.provider.invalid_response", result.Code);
    }

    private static IModelProviderProbe Create(
        string provider, HttpClient client, ModelProviderConfiguration config) => provider switch
        {
            "OpenAI" => new OpenAiModelProbe(client, config),
            "GoogleGemini" => new GeminiModelProbe(client, config),
            "AnthropicClaude" => new AnthropicModelProbe(client, config),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

    private sealed class CaptureHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }
        internal string? RequestHost { get; private set; }
        internal string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestHost = request.RequestUri?.Host;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
