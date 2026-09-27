using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

// Minimal owner-owned transition facts. No narrative, actor name, evidence or source payload
// is copied into the reporting ledger. Null on persisted rows means unprovable legacy history.
public sealed record GovernanceReportingEvent(
    long Sequence, DateTimeOffset AtUtc, string State,
    DateOnly? DueDate = null, string? Rating = null, int? MatrixVersion = null,
    Guid? RelatedId = null, int? OccurrenceCount = null);

public static class GovernanceReportingHistory
{
    internal static string Start(DateTimeOffset at, string state, DateOnly? dueDate = null,
        string? rating = null, int? matrixVersion = null, Guid? relatedId = null,
        int? occurrenceCount = null) =>
        JsonSerializer.Serialize(new[] { new GovernanceReportingEvent(1, at.ToUniversalTime(),
            state, dueDate, rating, matrixVersion, relatedId, occurrenceCount) });

    internal static IReadOnlyCollection<GovernanceReportingEvent>? Read(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<GovernanceReportingEvent[]>(json);

    internal static string? Append(string? json, DateTimeOffset at, string state,
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

    // A null result is reserved for legacy rows with no complete ledger. Malformed ledgers
    // fail the whole read; they must never be reported as an empty or partial register.
    public static GovernanceReportingEvent? Select<TState>(
        IReadOnlyCollection<GovernanceReportingEvent>? events, long revision,
        DateTimeOffset createdAt, TState initialState, TState currentState,
        DateTimeOffset cutoff)
        where TState : struct, Enum
    {
        if (events is null) return null;
        var ordered = events.ToArray();
        if (revision < 1 || ordered.Length != revision || ordered.Length is < 1 or > 20_000 ||
            !string.Equals(ordered[0].State, initialState.ToString(), StringComparison.Ordinal) ||
            !string.Equals(ordered[^1].State, currentState.ToString(), StringComparison.Ordinal) ||
            Microseconds(ordered[0].AtUtc) != Microseconds(createdAt) ||
            ordered.Where((item, index) => item.Sequence != index + 1 ||
                item.AtUtc == default || item.AtUtc.Offset != TimeSpan.Zero ||
                string.IsNullOrWhiteSpace(item.State) ||
                !Enum.TryParse<TState>(item.State, false, out var state) || !Enum.IsDefined(state) ||
                index > 0 && item.AtUtc < ordered[index - 1].AtUtc).Any())
            throw new DomainRuleException("governance.reporting_history.integrity",
                "The owner transition chronology cannot be verified.");
        return ordered.LastOrDefault(item => Microseconds(item.AtUtc) <= Microseconds(cutoff));
    }

    private static DateTimeOffset Microseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }
}
