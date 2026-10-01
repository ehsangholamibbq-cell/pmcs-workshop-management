using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Pmcs.Modules.Reporting.Services;

internal sealed class ReportingWorkerTelemetry
{
    internal const string MeterName = "Pmcs.Reporting";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Claims = Meter.CreateCounter<long>(
        "pmcs.reporting.worker.claims",
        unit: "{run}");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "pmcs.reporting.worker.outcomes",
        unit: "{run}");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "pmcs.reporting.worker.duration",
        unit: "ms");
    private static readonly Histogram<long> OutputBytes = Meter.CreateHistogram<long>(
        "pmcs.reporting.worker.output.size",
        unit: "By");
    private static readonly Histogram<double> ClaimedQueueAge = Meter.CreateHistogram<double>(
        "pmcs.reporting.worker.claim.queue_age",
        unit: "s");

    private long lastHeartbeatUnixMilliseconds;
    private long queuedRuns;
    private long oldestQueueAgeMilliseconds;
    private int activeRuns;

    public ReportingWorkerTelemetry()
    {
        Meter.CreateObservableGauge(
            "pmcs.reporting.worker.heartbeat.age",
            () => LastHeartbeatUtc is { } heartbeat
                ? Math.Max(0, (DateTimeOffset.UtcNow - heartbeat).TotalSeconds)
                : -1,
            unit: "s");
        Meter.CreateObservableGauge(
            "pmcs.reporting.worker.active",
            () => Volatile.Read(ref activeRuns),
            unit: "{run}");
        Meter.CreateObservableGauge(
            "pmcs.reporting.worker.queue.depth",
            () => Interlocked.Read(ref queuedRuns),
            unit: "{run}");
        Meter.CreateObservableGauge(
            "pmcs.reporting.worker.queue.oldest_age",
            () => Interlocked.Read(ref oldestQueueAgeMilliseconds) / 1_000d,
            unit: "s");
    }

    public DateTimeOffset? LastHeartbeatUtc
    {
        get
        {
            var value = Interlocked.Read(ref lastHeartbeatUnixMilliseconds);
            return value == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(value);
        }
    }

    public int ActiveRuns => Volatile.Read(ref activeRuns);

    public void Heartbeat(DateTimeOffset now) =>
        Interlocked.Exchange(ref lastHeartbeatUnixMilliseconds, now.ToUnixTimeMilliseconds());

    public void RecordClaim(string workKind, bool retry, TimeSpan queueAge)
    {
        Interlocked.Increment(ref activeRuns);
        Claims.Add(
            1,
            new KeyValuePair<string, object?>("reporting.work_kind", workKind),
            new KeyValuePair<string, object?>("reporting.retry", retry));
        ClaimedQueueAge.Record(
            Math.Max(0, queueAge.TotalSeconds),
            new KeyValuePair<string, object?>("reporting.work_kind", workKind));
    }

    public void RecordCompletion(string workKind, TimeSpan duration, long outputSizeBytes)
    {
        Interlocked.Decrement(ref activeRuns);
        RecordOutcome(workKind, "succeeded", diagnosticCode: null, duration);
        OutputBytes.Record(
            outputSizeBytes,
            new KeyValuePair<string, object?>("reporting.work_kind", workKind));
    }

    public void RecordFailure(
        string workKind,
        string diagnosticCode,
        bool willRetry,
        TimeSpan duration,
        bool activeClaim = true)
    {
        if (activeClaim)
        {
            Interlocked.Decrement(ref activeRuns);
        }
        RecordOutcome(workKind, willRetry ? "requeued" : "failed", diagnosticCode, duration);
    }

    public void ObserveQueue(long count, TimeSpan oldestAge)
    {
        Interlocked.Exchange(ref queuedRuns, Math.Max(0, count));
        Interlocked.Exchange(
            ref oldestQueueAgeMilliseconds,
            Math.Max(0, (long)oldestAge.TotalMilliseconds));
    }

    private static void RecordOutcome(
        string workKind,
        string outcome,
        string? diagnosticCode,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { "reporting.work_kind", workKind },
            { "reporting.outcome", outcome }
        };
        if (diagnosticCode is not null)
        {
            tags.Add("reporting.diagnostic_code", diagnosticCode);
        }
        Outcomes.Add(1, tags);
        Duration.Record(duration.TotalMilliseconds, tags);
    }
}
