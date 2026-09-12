using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Modules.Intelligence.Services;

public sealed record OpenAiSettings(
    string? ApiKey,
    string? Model,
    Uri Endpoint,
    TimeSpan Timeout)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}

public sealed record AdvisoryModelResult(
    AdvisoryInsightOutput Output,
    string Provider,
    string Model,
    string? ProviderResponseId);

public sealed class AdvisoryModelException(string code, bool isTransient = false) : Exception(code)
{
    public string Code { get; } = code;
    public bool IsTransient { get; } = isTransient;
}

internal interface IAdvisoryModelClient
{
    bool IsConfigured { get; }
    Task<AdvisoryModelResult> GenerateAsync(
        Guid generationRequestId,
        AdvisoryContext context,
        CancellationToken cancellationToken = default);
}

internal sealed class OpenAiResponsesClient(HttpClient httpClient, OpenAiSettings settings) : IAdvisoryModelClient
{
    public const string PromptVersion = "pmcs-advisory-fa-v1";
    public const string PolicyVersion = "pmcs-advisory-policy-v1";

    public bool IsConfigured => settings.IsConfigured;

    public async Task<AdvisoryModelResult> GenerateAsync(
        Guid generationRequestId,
        AdvisoryContext context,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
        {
            throw new AdvisoryModelException("ai.provider.not_configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.Add("Idempotency-Key", $"pmcs-insight-{generationRequestId:N}");
        request.Content = JsonContent.Create(BuildRequest(settings.Model!, context.ContextJson));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdvisoryModelException("ai.provider.timeout", isTransient: true);
        }
        catch (HttpRequestException)
        {
            throw new AdvisoryModelException("ai.provider.unavailable", isTransient: true);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500;
                throw new AdvisoryModelException(
                    transient ? "ai.provider.unavailable" : "ai.provider.rejected",
                    transient);
            }

            try
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeout.Token);
                return ParseResponse(document.RootElement, settings.Model!);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AdvisoryModelException("ai.provider.timeout", isTransient: true);
            }
            catch (JsonException)
            {
                throw new AdvisoryModelException("ai.provider.invalid_response");
            }
        }
    }

    public static AdvisoryModelResult ParseResponse(JsonElement root, string model)
    {
        if (root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
            string.Equals(status.GetString(), "incomplete", StringComparison.OrdinalIgnoreCase))
        {
            throw new AdvisoryModelException("ai.provider.incomplete", isTransient: true);
        }

        string? outputText = null;
        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
                        string.Equals(type.GetString(), "refusal", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new AdvisoryModelException("ai.provider.refusal");
                    }

                    if (part.TryGetProperty("type", out type) && type.ValueKind == JsonValueKind.String &&
                        string.Equals(type.GetString(), "output_text", StringComparison.OrdinalIgnoreCase) &&
                        part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        outputText = text.GetString();
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new AdvisoryModelException("ai.provider.invalid_response");
        }

        AdvisoryInsightOutput? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AdvisoryInsightOutput>(outputText, AdvisoryJson.Options);
        }
        catch (JsonException)
        {
            throw new AdvisoryModelException("ai.provider.invalid_response");
        }

        if (parsed is null)
        {
            throw new AdvisoryModelException("ai.provider.invalid_response");
        }

        var responseId = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
        return new AdvisoryModelResult(parsed, "OpenAI", model, responseId);
    }

    internal static object BuildRequest(string model, string contextJson) => new
    {
        model,
        store = false,
        max_output_tokens = 2_500,
        input = new object[]
        {
            new
            {
                role = "system",
                content = """
                    شما تحلیل‌گر مشورتی سامانه کنترل مدیریت پروژه عمرانی هستید. فقط از Context ساختاریافته استفاده کنید و خروجی را فارسی بنویسید. هیچ عدد، علت یا رویدادی را اختراع نکنید. هر ادعای واقعی باید دست‌کم یک evidenceReference معتبر داشته باشد. فرض‌ها و کمبود داده را صریح بنویسید. نبود WBS، بودجه مبنا یا قابلیت HSE را ریسک یا عملکرد بد تفسیر نکنید. نتیجه صرفاً پیشنهاد مدیریتی و نیازمند بازبینی انسان است؛ وضعیت رسمی، تأیید، بستن یا ایجاد اقدام انجام ندهید.
                    """
            },
            new
            {
                role = "user",
                content = $"برای این Context یک خلاصه مدیریتی مشورتی تولید کن:\n{contextJson}"
            }
        },
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "pmcs_advisory_insight_v1",
                strict = true,
                schema = OutputSchema
            }
        }
    };

    private static readonly object OutputSchema = new
    {
        type = "object",
        additionalProperties = false,
        properties = new
        {
            insightType = new { type = "string", @enum = new[] { "ExecutiveSummary", "EmergingRisk", "LikelyCause", "MissingDataWarning" } },
            statement = new { type = "string" },
            evidenceReferences = new { type = "array", items = new { type = "string" } },
            factsUsed = new { type = "array", items = new { type = "string" } },
            assumptions = new { type = "array", items = new { type = "string" } },
            dataGaps = new { type = "array", items = new { type = "string" } },
            confidenceBand = new { type = "string", @enum = new[] { "Low", "Medium", "High" } },
            potentialImpact = new { type = "string" },
            suggestedActions = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new
                    {
                        title = new { type = "string" },
                        rationale = new { type = "string" }
                    },
                    required = new[] { "title", "rationale" }
                }
            },
            suggestedOwnerRole = new { type = "string" }
        },
        required = new[]
        {
            "insightType", "statement", "evidenceReferences", "factsUsed", "assumptions",
            "dataGaps", "confidenceBand", "potentialImpact", "suggestedActions", "suggestedOwnerRole"
        }
    };
}
