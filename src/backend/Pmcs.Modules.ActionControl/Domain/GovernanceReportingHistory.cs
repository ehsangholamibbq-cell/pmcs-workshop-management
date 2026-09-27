using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

// Minimal owner-owned transition facts. No narrative, actor name, evidence or source payload
// is copied into the reporting ledger. Null on persisted rows means unprovable legacy history.
public sealed record GovernanceReportingEvent(
    long Sequence, DateTimeOffset AtUtc, string State,
    DateOnly? DueDate = null, string? Rating = null, int? MatrixVersion = null,
    Guid? RelatedId = null, int? OccurrenceCount = null);

internal static class GovernanceReportingHistory
{
    public static string Start(DateTimeOffset at, string state, DateOnly? dueDate = null,
        string? rating = null, int? matrixVersion = null, Guid? relatedId = null,
        int? occurrenceCount = null) =>
        JsonSerializer.Serialize(new[] { new GovernanceReportingEvent(1, at.ToUniversalTime(),
            state, dueDate, rating, matrixVersion, relatedId, occurrenceCount) });

    public static IReadOnlyCollection<GovernanceReportingEvent>? Read(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<GovernanceReportingEvent[]>(json);

    public static string? Append(string? json, DateTimeOffset at, string state,
        DateOnly? dueDate = null, string? rating = null, int? matrixVersion = null,
        Guid? relatedId = null, int? occurrenceCount = null)
    {
        if (json is null) return null; // A later transition cannot repair missing legacy events.
        var events = Read(json)?.ToArray() ?? throw new DomainRuleException(
            "governance.reporting_history.invalid", "Reporting transition history is invalid.");
        if (events.Length == 0 || at == default || at.ToUniversalTime() < events[^1].AtUtc ||
            events.Length >= 20_000)
            throw new DomainRuleException("governance.reporting_history.chronology",
                "Transition time must follow the preceding event within the reporting bound.");
        return JsonSerializer.Serialize(events.Append(new GovernanceReportingEvent(events.Length + 1,
            at.ToUniversalTime(), state, dueDate, rating, matrixVersion, relatedId,
            occurrenceCount)).ToArray());
    }
}
