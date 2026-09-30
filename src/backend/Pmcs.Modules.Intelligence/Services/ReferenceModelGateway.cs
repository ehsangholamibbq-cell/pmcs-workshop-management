using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pmcs.BuildingBlocks.Modules;

namespace Pmcs.Modules.Intelligence.Services;

internal sealed record ReferenceToolDecision(string ToolId, JsonElement Arguments,
    int InputTokens, int OutputTokens);
internal sealed record ReferenceModelAnswer(string Answer, int InputTokens, int OutputTokens);

internal sealed class ReferenceGatewayException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}

internal interface IReferenceModelAdapter
{
    string Provider { get; }
    string? ConfiguredModel { get; }
    bool IsConfigured { get; }
    Task<ReferenceToolDecision> DecideToolAsync(string question,
        IReadOnlyCollection<ToolManifest> tools, CancellationToken cancellationToken);
    Task<ReferenceModelAnswer> AnswerAsync(string question, string toolId,
        string toolJson, int maximumOutputTokens, CancellationToken cancellationToken);
}

internal abstract class ReferenceModelAdapter(HttpClient client, ModelProviderConfiguration config)
    : IReferenceModelAdapter
{
    private const int MaximumResponseBytes = 65_536;
    internal static readonly string[] AnswerFields = ["answer", "citations"];
    internal static readonly object AnswerSchema = new
    {
        type = "object",
        properties = new
        {
            answer = new { type = "string" },
            citations = new { type = "array", items = new { type = "string" } }
        },
        required = AnswerFields,
        additionalProperties = false
    };
    protected static string Alias(string toolId) => toolId.Replace('.', '_');
    protected static object InputSchema(string toolId, bool gemini = false)
    {
        var project = new { type = gemini ? "STRING" : "string" };
        var itemName = toolId switch
        {
            "reporting.runs.get" => "runId",
            "reporting.outputs.describe" => "outputId",
            _ => null
        };
        var properties = new Dictionary<string, object> { ["projectId"] = project };
        if (itemName is not null) properties[itemName] = project;
        return new
        {
            type = gemini ? "OBJECT" : "object", properties,
            required = itemName is null ? new[] { "projectId" } : new[] { "projectId", itemName },
            additionalProperties = false
        };
    }

    public abstract string Provider { get; }
    public string? ConfiguredModel => config.Model;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config.ApiKey) &&
        !string.IsNullOrWhiteSpace(config.Model);
    protected string Model => config.Model!;
    protected string ApiKey => config.ApiKey!;

    protected abstract HttpRequestMessage ToolRequest(string question,
        IReadOnlyCollection<ToolManifest> tools);
    protected abstract HttpRequestMessage AnswerRequest(string question,
        string toolId, string toolJson, int maximumOutputTokens);
    protected abstract ReferenceToolDecision ParseTool(JsonElement root);
    protected abstract (string Text, int InputTokens, int OutputTokens) ParseAnswer(JsonElement root);

    public async Task<ReferenceToolDecision> DecideToolAsync(string question,
        IReadOnlyCollection<ToolManifest> tools, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new ReferenceGatewayException("ai.provider.not_configured");
        if (tools.Count is < 1 or > 8 || tools.Any(tool => tool.AccessMode != ToolAccessMode.ReadOnly))
            throw new ReferenceGatewayException("ai.tool.registry_invalid");
        using var response = await SendAsync(ToolRequest(question, tools), cancellationToken);
        ReferenceToolDecision decision;
        try { decision = ParseTool(response.RootElement); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            KeyNotFoundException)
        { throw new ReferenceGatewayException("ai.provider.invalid_response"); }
        var match = tools.SingleOrDefault(tool => Alias(tool.Id) == decision.ToolId);
        if (match is null)
            throw new ReferenceGatewayException("ai.tool.unknown");
        if (decision.InputTokens < 0 || decision.OutputTokens < 0)
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        return decision with { ToolId = match.Id };
    }

    public async Task<ReferenceModelAnswer> AnswerAsync(string question, string toolId,
        string toolJson, int maximumOutputTokens, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new ReferenceGatewayException("ai.provider.not_configured");
        if (toolJson.Length > 12_000 || maximumOutputTokens is < 64 or > 2_000)
            throw new ReferenceGatewayException("ai.profile.limit_exceeded");
        using var response = await SendAsync(AnswerRequest(question, toolId,
            toolJson, maximumOutputTokens), cancellationToken);
        (string Text, int InputTokens, int OutputTokens) parsed;
        try { parsed = ParseAnswer(response.RootElement); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            KeyNotFoundException)
        { throw new ReferenceGatewayException("ai.provider.invalid_response"); }
        try
        {
            using var content = JsonDocument.Parse(parsed.Text,
                new JsonDocumentOptions { MaxDepth = 8 });
            var value = content.RootElement;
            if (value.ValueKind != JsonValueKind.Object ||
                value.EnumerateObject().Count() != 2 ||
                !value.TryGetProperty("answer", out var answer) ||
                answer.ValueKind != JsonValueKind.String ||
                !value.TryGetProperty("citations", out var citations) ||
                citations.ValueKind != JsonValueKind.Array ||
                citations.GetArrayLength() != 1 ||
                citations[0].ValueKind != JsonValueKind.String ||
                citations[0].GetString() != toolId ||
                answer.GetString() is not { Length: > 0 and <= 2_000 } text ||
                parsed.InputTokens < 0 || parsed.OutputTokens < 0)
                throw new ReferenceGatewayException("ai.provider.invalid_response");
            return new ReferenceModelAnswer(text, parsed.InputTokens, parsed.OutputTokens);
        }
        catch (JsonException)
        {
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        }
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await client.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new ReferenceGatewayException(response.StatusCode is
                        HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                        (int)response.StatusCode >= 500
                            ? "ai.provider.unavailable" : "ai.provider.rejected");
                if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                    throw new ReferenceGatewayException("ai.provider.invalid_response");
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var buffer = new MemoryStream();
                var chunk = new byte[4_096];
                int read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
                {
                    if (buffer.Length + read > MaximumResponseBytes)
                        throw new ReferenceGatewayException("ai.provider.invalid_response");
                    buffer.Write(chunk, 0, read);
                }
                buffer.Position = 0;
                return JsonDocument.Parse(buffer, new JsonDocumentOptions { MaxDepth = 32 });
            }
            catch (HttpRequestException)
            {
                throw new ReferenceGatewayException("ai.provider.unavailable");
            }
            catch (JsonException)
            {
                throw new ReferenceGatewayException("ai.provider.invalid_response");
            }
        }
    }

    protected static string Text(JsonElement item, string key) =>
        item.GetProperty(key).GetString() ?? throw new ReferenceGatewayException("ai.provider.invalid_response");
    protected static int Tokens(JsonElement root, string usageKey, string key) =>
        root.TryGetProperty(usageKey, out var usage) &&
        usage.TryGetProperty(key, out var count) && count.TryGetInt32(out var value)
            ? value : -1;
}

internal sealed class OpenAiReferenceAdapter(HttpClient client, ModelProviderConfiguration config)
    : ReferenceModelAdapter(client, config)
{
    public override string Provider => "OpenAI";
    protected override HttpRequestMessage ToolRequest(string question,
        IReadOnlyCollection<ToolManifest> tools)
    {
        var request = NewRequest();
        request.Content = JsonContent.Create(new
        {
            model = Model, store = false, max_output_tokens = 256,
            input = question, tool_choice = "required", parallel_tool_calls = false,
            tools = tools.Select(tool => new
            {
                type = "function", name = Alias(tool.Id), description = tool.Description,
                parameters = InputSchema(tool.Id),
                strict = true
            }).ToArray()
        });
        return request;
    }
    protected override HttpRequestMessage AnswerRequest(string question, string toolId,
        string toolJson, int maximumOutputTokens)
    {
        var request = NewRequest();
        request.Content = JsonContent.Create(new
        {
            model = Model, store = false, max_output_tokens = maximumOutputTokens,
            input = $"Answer this question using only the read-only result of {toolId}. " +
                $"Cite exactly {toolId}. Question: {question}\nTool result: {toolJson}",
            text = new { format = new { type = "json_schema", name = "pmcs_reference_answer",
                strict = true, schema = AnswerSchema } }
        });
        return request;
    }
    protected override ReferenceToolDecision ParseTool(JsonElement root)
    {
        if (Text(root, "status") != "completed")
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        var calls = root.GetProperty("output").EnumerateArray()
            .Where(item => Text(item, "type") == "function_call").ToArray();
        if (calls.Length != 1) throw new ReferenceGatewayException("ai.provider.invalid_response");
        using var args = JsonDocument.Parse(Text(calls[0], "arguments"));
        return new ReferenceToolDecision(Text(calls[0], "name"), args.RootElement.Clone(),
            Tokens(root, "usage", "input_tokens"), Tokens(root, "usage", "output_tokens"));
    }
    protected override (string, int, int) ParseAnswer(JsonElement root)
    {
        if (Text(root, "status") != "completed")
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        var parts = root.GetProperty("output").EnumerateArray()
            .SelectMany(item => item.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Array ? content.EnumerateArray().ToArray() : [])
            .Where(item => Text(item, "type") == "output_text").ToArray();
        if (parts.Length != 1) throw new ReferenceGatewayException("ai.provider.invalid_response");
        return (Text(parts[0], "text"), Tokens(root, "usage", "input_tokens"),
            Tokens(root, "usage", "output_tokens"));
    }
    private HttpRequestMessage NewRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
        return request;
    }
}

internal sealed class GeminiReferenceAdapter(HttpClient client, ModelProviderConfiguration config)
    : ReferenceModelAdapter(client, config)
{
    public override string Provider => "GoogleGemini";
    protected override HttpRequestMessage ToolRequest(string question,
        IReadOnlyCollection<ToolManifest> tools)
    {
        var request = NewRequest();
        request.Content = JsonContent.Create(new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = question } } } },
            tools = new[] { new { functionDeclarations = tools.Select(tool => new
            {
                name = Alias(tool.Id), description = tool.Description,
                parameters = InputSchema(tool.Id, gemini: true)
            }).ToArray() } },
            toolConfig = new { functionCallingConfig = new { mode = "ANY",
                allowedFunctionNames = tools.Select(tool => Alias(tool.Id)).ToArray() } },
            generationConfig = new { maxOutputTokens = 256 }
        });
        return request;
    }
    protected override HttpRequestMessage AnswerRequest(string question, string toolId,
        string toolJson, int maximumOutputTokens)
    {
        var request = NewRequest();
        request.Content = JsonContent.Create(new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text =
                $"Answer using only {toolId} result. Cite exactly {toolId}. Question: {question}\nResult: {toolJson}" } } } },
            generationConfig = new
            {
                maxOutputTokens = maximumOutputTokens, responseMimeType = "application/json",
                responseSchema = new { type = "OBJECT", properties = new
                {
                    answer = new { type = "STRING" },
                    citations = new { type = "ARRAY", items = new { type = "STRING" } }
                }, required = AnswerFields }
            }
        });
        return request;
    }
    protected override ReferenceToolDecision ParseTool(JsonElement root)
    {
        var candidate = root.GetProperty("candidates").EnumerateArray().Single();
        if (Text(candidate, "finishReason") != "STOP")
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        var calls = candidate.GetProperty("content").GetProperty("parts").EnumerateArray()
            .Where(part => part.TryGetProperty("functionCall", out _))
            .Select(part => part.GetProperty("functionCall")).ToArray();
        if (calls.Length != 1) throw new ReferenceGatewayException("ai.provider.invalid_response");
        return new ReferenceToolDecision(Text(calls[0], "name"),
            calls[0].GetProperty("args").Clone(),
            Tokens(root, "usageMetadata", "promptTokenCount"),
            Tokens(root, "usageMetadata", "candidatesTokenCount"));
    }
    protected override (string, int, int) ParseAnswer(JsonElement root)
    {
        var candidate = root.GetProperty("candidates").EnumerateArray().Single();
        if (Text(candidate, "finishReason") != "STOP")
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        var parts = candidate.GetProperty("content").GetProperty("parts").EnumerateArray().ToArray();
        if (parts.Length != 1) throw new ReferenceGatewayException("ai.provider.invalid_response");
        return (Text(parts[0], "text"), Tokens(root, "usageMetadata", "promptTokenCount"),
            Tokens(root, "usageMetadata", "candidatesTokenCount"));
    }
    private HttpRequestMessage NewRequest()
    {
        var path = Uri.EscapeDataString(Model);
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{path}:generateContent");
        request.Headers.Add("x-goog-api-key", ApiKey);
        return request;
    }
}

internal sealed class ClaudeReferenceAdapter(HttpClient client, ModelProviderConfiguration config)
    : ReferenceModelAdapter(client, config)
{
    public override string Provider => "AnthropicClaude";
    protected override HttpRequestMessage ToolRequest(string question,
        IReadOnlyCollection<ToolManifest> tools)
    {
        var request = NewRequest();
        request.Content = JsonContent.Create(new
        {
            model = Model, max_tokens = 256,
            messages = new[] { new { role = "user", content = question } },
            tools = tools.Select(tool => new
            {
                name = Alias(tool.Id), description = tool.Description,
                input_schema = InputSchema(tool.Id)
            }).ToArray(), tool_choice = new { type = "any" }
        });
        return request;
    }
    protected override HttpRequestMessage AnswerRequest(string question, string toolId,
        string toolJson, int maximumOutputTokens)
    {
        var request = NewRequest();
        request.Content = JsonContent.Create(new
        {
            model = Model, max_tokens = maximumOutputTokens,
            messages = new[] { new { role = "user", content =
                $"Answer using only {toolId} result. Cite exactly {toolId}. Question: {question}\nResult: {toolJson}" } },
            output_config = new { format = new { type = "json_schema", schema = AnswerSchema } }
        });
        return request;
    }
    protected override ReferenceToolDecision ParseTool(JsonElement root)
    {
        if (Text(root, "stop_reason") != "tool_use")
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        var calls = root.GetProperty("content").EnumerateArray()
            .Where(item => Text(item, "type") == "tool_use").ToArray();
        if (calls.Length != 1) throw new ReferenceGatewayException("ai.provider.invalid_response");
        return new ReferenceToolDecision(Text(calls[0], "name"),
            calls[0].GetProperty("input").Clone(),
            Tokens(root, "usage", "input_tokens"), Tokens(root, "usage", "output_tokens"));
    }
    protected override (string, int, int) ParseAnswer(JsonElement root)
    {
        if (Text(root, "stop_reason") != "end_turn")
            throw new ReferenceGatewayException("ai.provider.invalid_response");
        var parts = root.GetProperty("content").EnumerateArray()
            .Where(item => Text(item, "type") == "text").ToArray();
        if (parts.Length != 1) throw new ReferenceGatewayException("ai.provider.invalid_response");
        return (Text(parts[0], "text"), Tokens(root, "usage", "input_tokens"),
            Tokens(root, "usage", "output_tokens"));
    }
    private HttpRequestMessage NewRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return request;
    }
}
