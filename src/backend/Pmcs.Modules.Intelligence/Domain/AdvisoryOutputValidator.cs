using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Intelligence.Domain;

public static class AdvisoryOutputValidator
{
    public static AdvisoryInsightOutput NormalizeAndValidate(
        AdvisoryInsightOutput output,
        IReadOnlySet<string>? allowedEvidenceReferences = null)
    {
        var evidence = NormalizeItems(output.EvidenceReferences, 30, 180, "insight.evidence.invalid");
        if (evidence.Length == 0)
        {
            throw new DomainRuleException("insight.evidence.required", "At least one evidence reference is required.");
        }

        if (allowedEvidenceReferences is not null && evidence.Any(item => !allowedEvidenceReferences.Contains(item)))
        {
            throw new DomainRuleException("insight.citation.unsupported", "Insight contains an unsupported evidence reference.");
        }

        return output with
        {
            Statement = RequiredPersian(output.Statement, 2_000, "insight.statement.invalid"),
            EvidenceReferences = evidence,
            FactsUsed = NormalizeItems(output.FactsUsed, 30, 500, "insight.facts.invalid"),
            Assumptions = NormalizeItems(output.Assumptions, 20, 500, "insight.assumptions.invalid"),
            DataGaps = NormalizeItems(output.DataGaps, 20, 500, "insight.data_gaps.invalid"),
            PotentialImpact = RequiredPersian(output.PotentialImpact, 1_000, "insight.impact.invalid"),
            SuggestedActions = NormalizeActions(output.SuggestedActions),
            SuggestedOwnerRole = RequiredPersian(output.SuggestedOwnerRole, 120, "insight.owner_role.invalid")
        };
    }

    private static string[] NormalizeItems(
        IReadOnlyCollection<string>? items,
        int maximumCount,
        int maximumLength,
        string code)
    {
        if (items is null || items.Count > maximumCount)
        {
            throw new DomainRuleException(code, "Advisory list is missing or too large.");
        }

        var result = items.Select(item => Required(item, maximumLength, code))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return result;
    }

    private static AdvisorySuggestedAction[] NormalizeActions(
        IReadOnlyCollection<AdvisorySuggestedAction>? actions)
    {
        if (actions is null || actions.Count > 10)
        {
            throw new DomainRuleException("insight.actions.invalid", "Suggested actions are missing or too large.");
        }

        return actions.Select(action => new AdvisorySuggestedAction(
                RequiredPersian(action.Title, 300, "insight.action_title.invalid"),
                RequiredPersian(action.Rationale, 600, "insight.action_rationale.invalid")))
            .ToArray();
    }

    private static string Required(string? value, int maximumLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, "Advisory value is missing or too long.");
        }

        return normalized;
    }

    private static string RequiredPersian(string? value, int maximumLength, string code)
    {
        var normalized = Required(value, maximumLength, code);
        if (!normalized.Any(character => character is >= '\u0600' and <= '\u06ff'))
        {
            throw new DomainRuleException("insight.language.invalid", "Advisory output must be Persian.");
        }

        return normalized;
    }
}
