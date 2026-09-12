using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

internal static class SupplyRules
{
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void Identity(params Guid[] ids)
    {
        if (ids.Any(id => id == Guid.Empty))
            throw new DomainRuleException("supply.identity.required", "Supply entity identity is required.");
    }

    public static string Code(string? value, int maximumLength, string code)
    {
        var normalized = CommercialRules.Required(value, maximumLength, code).ToUpperInvariant();
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            throw new DomainRuleException(code, "Code contains unsupported characters.");
        return normalized;
    }

    public static string Unit(string value) => Code(value, 24, "supply.unit.invalid");

    public static decimal PositiveQuantity(decimal value, string code)
    {
        if (value <= 0 || decimal.Round(value, 6, MidpointRounding.AwayFromZero) != value)
            throw new DomainRuleException(code, "Quantity must be positive with no more than six decimal places.");
        return value;
    }

    public static decimal NonNegativeQuantity(decimal value, string code)
    {
        if (value < 0 || decimal.Round(value, 6, MidpointRounding.AwayFromZero) != value)
            throw new DomainRuleException(code, "Quantity must not be negative and may have at most six decimal places.");
        return value;
    }

    public static string Evidence(IReadOnlyCollection<string>? values, string code)
    {
        var normalized = values?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => CommercialRules.Required(value, 700, code))
            .Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (normalized.Length > 50)
            throw new DomainRuleException(code, "Too many evidence references were supplied.");
        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static IReadOnlyCollection<string> ReadEvidence(string json) =>
        JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];

    public static string OfficialNumber(string prefix, DateTimeOffset at, Guid id) =>
        $"{prefix}-{PersianDateCode.FromInstant(at)}-{id:N}"[..(prefix.Length + 18)].ToUpperInvariant();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
