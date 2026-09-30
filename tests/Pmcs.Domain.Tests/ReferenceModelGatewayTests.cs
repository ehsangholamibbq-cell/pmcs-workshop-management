using System.Net;
using System.Text;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.Intelligence.Services;
using Pmcs.Modules.Reporting;

namespace Pmcs.Domain.Tests;

public sealed class ReferenceModelGatewayTests
{
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("GoogleGemini")]
    [InlineData("AnthropicClaude")]
    public async Task EachAdapterNormalizesOneReadOnlyToolAndStrictAnswer(string provider)
    {
        var projectId = Guid.NewGuid();
        var handler = new FixtureHandler(provider, projectId);
        using var client = new HttpClient(handler);
        var adapter = Create(provider, client);
        var tools = new ReportingModule().Descriptor.Tools
            .Where(tool => tool.Id == "reporting.catalog.list").ToArray();

        var decision = await adapter.DecideToolAsync("What reports?", tools,
            CancellationToken.None);
        Assert.Equal("reporting.catalog.list", decision.ToolId);
        Assert.Equal(projectId, decision.Arguments.GetProperty("projectId").GetGuid());
        var answer = await adapter.AnswerAsync("What reports?", decision.ToolId,
            "{\"definitions\":[]}", 100, CancellationToken.None);
        Assert.Equal("No available reports.", answer.Answer);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("reporting_catalog_list", handler.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("api-key", handler.Requests[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("GoogleGemini")]
    [InlineData("AnthropicClaude")]
    public async Task MissingCredentialIsExplicitUnavailable(string provider)
    {
        using var client = new HttpClient(new FixtureHandler(provider, Guid.NewGuid()));
        var adapter = provider switch
        {
            "OpenAI" => (IReferenceModelAdapter)new OpenAiReferenceAdapter(client,
                new ModelProviderConfiguration("configured", null)),
            "GoogleGemini" => new GeminiReferenceAdapter(client,
                new ModelProviderConfiguration("configured", null)),
            _ => new ClaudeReferenceAdapter(client,
                new ModelProviderConfiguration("configured", null))
        };
        Assert.False(adapter.IsConfigured);
        var tool = new ReportingModule().Descriptor.Tools.Take(1).ToArray();
        var error = await Assert.ThrowsAsync<ReferenceGatewayException>(() =>
            adapter.DecideToolAsync("x", tool, CancellationToken.None));
        Assert.Equal("ai.provider.not_configured", error.Code);
    }

    private static IReferenceModelAdapter Create(string provider, HttpClient client)
    {
        var config = new ModelProviderConfiguration("configured", "test-secret");
        return provider switch
        {
            "OpenAI" => new OpenAiReferenceAdapter(client, config),
            "GoogleGemini" => new GeminiReferenceAdapter(client, config),
            _ => new ClaudeReferenceAdapter(client, config)
        };
    }

    private sealed class FixtureHandler(string provider, Guid projectId) : HttpMessageHandler
    {
        internal List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var first = Requests.Count == 1;
            var json = (provider, first) switch
            {
                ("OpenAI", true) => """{"status":"completed","output":[{"type":"function_call","name":"reporting_catalog_list","arguments":"{\"projectId\":\"PROJECT\"}"}],"usage":{"input_tokens":12,"output_tokens":9}}""",
                ("OpenAI", false) => """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"{\"answer\":\"No available reports.\",\"citations\":[\"reporting.catalog.list\"]}"}]}],"usage":{"input_tokens":10,"output_tokens":8}}""",
                ("GoogleGemini", true) => """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"functionCall":{"name":"reporting_catalog_list","args":{"projectId":"PROJECT"}}}]}}],"usageMetadata":{"promptTokenCount":12,"candidatesTokenCount":9}}""",
                ("GoogleGemini", false) => """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"{\"answer\":\"No available reports.\",\"citations\":[\"reporting.catalog.list\"]}"}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":8}}""",
                ("AnthropicClaude", true) => """{"stop_reason":"tool_use","content":[{"type":"tool_use","name":"reporting_catalog_list","input":{"projectId":"PROJECT"}}],"usage":{"input_tokens":12,"output_tokens":9}}""",
                _ => """{"stop_reason":"end_turn","content":[{"type":"text","text":"{\"answer\":\"No available reports.\",\"citations\":[\"reporting.catalog.list\"]}"}],"usage":{"input_tokens":10,"output_tokens":8}}"""
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json.Replace("PROJECT", projectId.ToString(),
                    StringComparison.Ordinal), Encoding.UTF8, "application/json")
            };
        }
    }
}
