using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.Modules.TechnicalOffice.Contracts;

namespace Pmcs.Modules.TechnicalOffice.Services;

public static class TechnicalReportingHash
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Compute<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, Options);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, element);
        }
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }

    public static string ComputeResult(ProjectTechnicalOfficeReportingResult result) => Compute(new
    {
        result.ContractVersion, result.PolicyVersion, result.TenantId, result.ProjectId,
        result.CutoffLocalDate, result.SourceCutoffUtc, result.Classification,
        result.DataStatus, result.Reasons, result.Documents, result.Transmittals,
        result.Rfis, result.Submittals, result.SourceManifestSha256
    });

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(element.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(element.GetRawText(), skipInputValidation: true); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new ArgumentOutOfRangeException(nameof(element));
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
