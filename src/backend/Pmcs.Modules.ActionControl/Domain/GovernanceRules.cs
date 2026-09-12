using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

internal static class GovernanceRules
{
    public static void Identities(params Guid[] values)
    {
        if (values.Any(value => value == Guid.Empty))
            throw new DomainRuleException("governance.identity.required", "Governance identities are required.");
    }

    public static void Revision(long current, long supplied, string code)
    {
        if (current != supplied) throw new DomainRuleException(code, "The record changed after it was loaded.");
    }

    public static string Required(string? value, int maximum, string code)
    {
        var result = Optional(value, maximum, code);
        return result ?? throw new DomainRuleException(code, "A required value is missing.");
    }

    public static string? Optional(string? value, int maximum, string code)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > maximum) throw new DomainRuleException(code, "The supplied value is too long.");
        return result;
    }

    public static string JsonList(IReadOnlyCollection<string>? values, int itemMaximum, string code, bool required = false)
    {
        var normalized = (values ?? []).Select(value => Required(value, itemMaximum, code))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (required && normalized.Length == 0) throw new DomainRuleException(code, "At least one value is required.");
        return JsonSerializer.Serialize(normalized);
    }

    public static IReadOnlyCollection<string> ReadList(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

    public static string Number(string prefix, DateTimeOffset at, Guid id) =>
        $"{prefix}-{PersianDateCode.FromInstant(at)}-{id.ToString("N")[..8].ToUpperInvariant()}";
}

public enum GovernanceSeverity { Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum GovernanceUrgency { Routine = 1, Soon = 2, Immediate = 3 }
public enum RecordConfidentiality { GeneralProject = 1, RestrictedManagement = 2, ConfidentialHse = 3, CommercialSensitive = 4 }
public enum IssueStatus { Open = 1, UnderAssessment = 2, ResponseInProgress = 3, PendingVerification = 4, Resolved = 5, Closed = 6, Reopened = 7, NotAnIssue = 8, Void = 9 }
public enum RiskType { Threat = 1, Opportunity = 2 }
public enum ProbabilityBand { Rare = 1, Unlikely = 2, Possible = 3, Likely = 4, AlmostCertain = 5 }
public enum ImpactBand { Negligible = 1, Minor = 2, Moderate = 3, Major = 4, Severe = 5 }
public enum RiskRatingBand { Low = 1, Moderate = 2, High = 3, Critical = 4 }
public enum RiskResponseStrategy { Avoid = 1, Mitigate = 2, Transfer = 3, Accept = 4, Exploit = 5 }
public enum RiskStatus { Proposed = 1, Assessed = 2, Active = 3, Monitoring = 4, Materialized = 5, Expired = 6, Closed = 7, Reopened = 8 }
public enum DecisionRequestStatus { Draft = 1, ReadyForDecision = 2, InDecision = 3, MoreInformationRequired = 4, Decided = 5, Implementing = 6, EffectReviewed = 7, Closed = 8, Withdrawn = 9 }
public enum DecisionChannel { InSystem = 1, Meeting = 2, WrittenDirective = 3, VerbalRecordedLater = 4 }
public enum DecisionRecordStatus { Recorded = 1, EffectReviewed = 2, Superseded = 3 }
public enum SlaEntityType { Issue = 1, Risk = 2, DecisionRequest = 3 }
public enum SlaDurationUnit { ElapsedHours = 1, ProjectWorkingDays = 2 }
public enum DeadlineSignal { OnTrack = 1, DueSoon = 2, Overdue = 3, EscalationDue = 4 }
public enum EscalationReason { DueSoon = 1, Overdue = 2, CriticalSeverity = 3, ReviewOverdue = 4, DecisionOverdue = 5, Blocked = 6 }
public enum EscalationStatus { Open = 1, Acknowledged = 2, ClosedBySourceResolution = 3 }
