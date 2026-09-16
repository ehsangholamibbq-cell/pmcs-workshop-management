using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Finance.Domain;

internal static class FinanceRules
{
    public static void Identity(string errorCode, params Guid[] values)
    {
        if (values.Any(value => value == Guid.Empty))
        {
            throw new DomainRuleException(errorCode, "All required finance identities must be non-empty.");
        }
    }

    public static decimal PositiveMoney(decimal value, string errorCode)
    {
        if (value <= 0 || decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
        {
            throw new DomainRuleException(errorCode, "Amount must be positive and have at most two decimal places.");
        }

        return value;
    }

    public static decimal NonNegativeMoney(decimal value, string errorCode)
    {
        if (value < 0 || decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
        {
            throw new DomainRuleException(errorCode, "Amount cannot be negative and must have at most two decimal places.");
        }

        return value;
    }

    public static string Currency(string? value, string errorCode)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new DomainRuleException(errorCode, "Currency must be a three-letter ISO-style code.");
        }

        return normalized;
    }

    public static string Required(string? value, int maximumLength, string errorCode) =>
        Optional(value, maximumLength, errorCode) ??
        throw new DomainRuleException(errorCode, "A value is required.");

    public static string? Optional(string? value, int maximumLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(errorCode, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    public static void Revision(long current, long expected, string errorCode)
    {
        if (current != expected)
        {
            throw new DomainRuleException(errorCode, "The finance record changed after it was loaded.");
        }
    }
}
