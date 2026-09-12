using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.QualitySafety.Domain;

internal static class QualitySafetyRules
{
    public static void Identity(params Guid[] values)
    {
        if (values.Any(value => value == Guid.Empty))
            throw new DomainRuleException("quality_safety.identity.required", "All required identities must be valid.");
    }

    public static void Revision(long current, long supplied, string code)
    {
        if (current != supplied)
            throw new DomainRuleException(code, "The record changed after it was loaded.");
    }

    public static string Required(string? value, int maximumLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainRuleException(code, "A value is required.");
        return Optional(value, maximumLength, code)!;
    }

    public static string? Optional(string? value, int maximumLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new DomainRuleException(code, $"Value must be at most {maximumLength} characters.");
        return normalized;
    }

    public static string JsonList(IReadOnlyCollection<string>? values, int itemLength, string code, bool required = false)
    {
        var normalized = (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Required(value, itemLength, code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();
        if (required && normalized.Length == 0)
            throw new DomainRuleException(code, "At least one item is required.");
        return JsonSerializer.Serialize(normalized);
    }

    public static IReadOnlyCollection<string> ReadList(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();

    public static string Number(string prefix, DateTimeOffset at, Guid id) =>
        $"{prefix}-{PersianDateCode.FromInstant(at)}-{id.ToString("N")[..8].ToUpperInvariant()}";
}

public enum QualityOperatingMode { Disabled = 0, DefectPunchOnly = 1, InspectionAndNcrLite = 2, FullV1 = 3 }
public enum HseOperatingMode { Disabled = 0, ObservationIntakeOnly = 1, HseLite = 2, FullV1 = 3 }
public enum ControlArea { Quality = 1, Hse = 2 }
public enum DataClassification { GeneralProject = 1, RestrictedQuality = 2, ConfidentialHse = 3, PersonalMedical = 4, LegalInvestigation = 5 }
public enum IntakeKind { QualityObservation = 1, Defect = 2, UnsafeCondition = 3, UnsafeAct = 4, NearMiss = 5, IncidentIntake = 6, EnvironmentalObservation = 7, SafeObservation = 8, PositiveIntervention = 9 }
public enum InitialSeverity { Unassessed = 0, Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum IntakeStatus { Captured = 1, UnderTriage = 2, Converted = 3, RetainedAsGeneralIssue = 4, Dismissed = 5 }
public enum IntakeConversionType { None = 0, QualityObservation = 1, Defect = 2, NonConformance = 3, HseObservation = 4, Incident = 5, GeneralIssue = 6 }
public enum InspectionReadiness { NotAssessed = 0, Ready = 1, NotReady = 2 }
public enum InspectionStatus { Requested = 1, ReadinessRecorded = 2, ResultRecorded = 3, Cancelled = 4 }
public enum InspectionResult { Pass = 1, PassWithObservation = 2, Fail = 3, NotReadyOrNotInspected = 4, HoldOrDeferred = 5 }
public enum NcrStatus { Draft = 1, Issued = 2, Containment = 3, InvestigationDisposition = 4, ActionImplementation = 5, ReinspectionVerification = 6, Closed = 7, Reopened = 8 }
public enum NcrDisposition { NotDecided = 0, Rework = 1, Repair = 2, Replace = 3, UseAsIsWithConcession = 4, RejectOrRemove = 5, FurtherEvaluation = 6 }
public enum DefectStatus { Open = 1, Assigned = 2, Rectified = 3, ReadyForVerification = 4, Accepted = 5, Rejected = 6, Closed = 7, Reopened = 8 }
public enum IncidentStatus { Reported = 1, Triage = 2, Contained = 3, UnderInvestigation = 4, ActionsOpen = 5, FinalReview = 6, Closed = 7, Reopened = 8 }
public enum CorrectiveActionKind { Immediate = 1, Corrective = 2, Preventive = 3 }
public enum CorrectiveActionStatus { Open = 1, InProgress = 2, Completed = 3, ReadyForVerification = 4, Verified = 5, Closed = 6, Reopened = 7, Cancelled = 8 }
public enum RootCauseStatus { NotStarted = 0, Proposed = 1, UnderReview = 2, Confirmed = 3, Disputed = 4 }
public enum PermitStatus { Draft = 1, Submitted = 2, Approved = 3, Active = 4, Suspended = 5, Closed = 6, Cancelled = 7 }
public enum InspectionPointType { Hold = 1, Witness = 2, Review = 3 }
public enum QualityTestResult { Pass = 1, Fail = 2, Inconclusive = 3 }
