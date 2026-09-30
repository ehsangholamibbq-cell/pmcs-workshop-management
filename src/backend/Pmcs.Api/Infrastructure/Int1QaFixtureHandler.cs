using System.Net;
using System.Text;
using System.Text.Json;

namespace Pmcs.Api.Infrastructure;

// Loaded only by the isolated, authenticated QA gateway and never used by a deployed model client.
internal sealed class Int1QaFixtureHandler : HttpMessageHandler
{
    private const string ProjectId = "33333333-3333-3333-3333-333333333333";
    private const string ForeignProjectId = "44444444-4444-4444-8444-444444444444";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        using var payload = JsonDocument.Parse(body);
        var tool = payload.RootElement.TryGetProperty("tools", out _);
        var unavailable = body.Contains("int1-fixture-unavailable", StringComparison.Ordinal);
        if (tool && request.RequestUri?.Host == "api.openai.com" &&
            body.Contains("int1-fixture-timeout", StringComparison.Ordinal))
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        if (unavailable && request.RequestUri?.Host == "api.openai.com")
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        if (request.RequestUri?.Host == "generativelanguage.googleapis.com" &&
            body.Contains("int1-fixture-gemini-unavailable", StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        if (!tool && request.RequestUri?.Host == "api.openai.com" &&
            body.Contains("int1-fixture-invalid-answer", StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"not-json"}]}],"usage":{"input_tokens":10,"output_tokens":8}}""",
                    Encoding.UTF8, "application/json")
            };
        var unknown = body.Contains("int1-fixture-unknown-tool", StringComparison.Ordinal);
        var crossProject = body.Contains("int1-fixture-cross-project", StringComparison.Ordinal);
        var toolName = unknown ? "unregistered_tool" : "reporting_catalog_list";
        var answer = "{\"answer\":\"Fixture catalog result.\",\"citations\":[\"reporting.catalog.list\"]}";
        var result = ((request.RequestUri?.Host, tool) switch
        {
            ("api.openai.com", true) =>
                """{"status":"completed","output":[{"type":"function_call","name":"TOOL","arguments":"{\"projectId\":\"PROJECT\"}"}],"usage":{"input_tokens":12,"output_tokens":9}}""",
            ("api.openai.com", false) =>
                """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":ANSWER}]}],"usage":{"input_tokens":10,"output_tokens":8}}""",
            ("generativelanguage.googleapis.com", true) =>
                """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"functionCall":{"name":"TOOL","args":{"projectId":"PROJECT"}}}]}}],"usageMetadata":{"promptTokenCount":12,"candidatesTokenCount":9}}""",
            ("generativelanguage.googleapis.com", false) =>
                """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":ANSWER}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":8}}""",
            ("api.anthropic.com", true) =>
                """{"stop_reason":"tool_use","content":[{"type":"tool_use","name":"TOOL","input":{"projectId":"PROJECT"}}],"usage":{"input_tokens":12,"output_tokens":9}}""",
            ("api.anthropic.com", false) =>
                """{"stop_reason":"end_turn","content":[{"type":"text","text":ANSWER}],"usage":{"input_tokens":10,"output_tokens":8}}""",
            _ => throw new InvalidOperationException("Unexpected INT1 QA provider request.")
        }).Replace("PROJECT", crossProject ? ForeignProjectId : ProjectId, StringComparison.Ordinal)
            .Replace("TOOL", toolName, StringComparison.Ordinal)
            .Replace("ANSWER", JsonSerializer.Serialize(answer), StringComparison.Ordinal);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(result, Encoding.UTF8, "application/json")
        };
    }
}
