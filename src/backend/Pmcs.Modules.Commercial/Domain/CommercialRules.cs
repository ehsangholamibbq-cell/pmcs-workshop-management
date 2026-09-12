using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

internal static class CommercialRules
{
    public static string Required(string? value, int maximumLength, string code)
    {
        var normalized = Optional(value, maximumLength, code);
        return normalized ?? throw new DomainRuleException(code, "A value is required.");
    }

    public static string? Optional(string? value, int maximumLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    public static string Currency(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new DomainRuleException("commercial.currency.invalid", "Currency must be a three-letter ISO-style code.");
        }

        return normalized;
    }

    public static decimal? OptionalPositiveAmount(decimal? amount, string code)
    {
        if (!amount.HasValue)
        {
            return null;
        }

        if (amount <= 0 || decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero) != amount)
        {
            throw new DomainRuleException(code, "Amount must be positive and have at most two decimal places.");
        }

        return amount;
    }

    public static void Identity(params Guid[] ids)
    {
        if (ids.Any(id => id == Guid.Empty))
        {
            throw new DomainRuleException("commercial.identity.required", "Commercial entity identity is required.");
        }
    }

    public static void Revision(long currentRevision, long baseRevision, string code)
    {
        if (currentRevision != baseRevision)
        {
            throw new DomainRuleException(code, "The item changed after it was loaded.");
        }
    }
}
