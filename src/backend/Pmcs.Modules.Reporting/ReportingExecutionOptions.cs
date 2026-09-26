using Microsoft.Extensions.Configuration;

namespace Pmcs.Modules.Reporting;

public sealed record ReportingExecutionOptions(
    int MaximumAttempts,
    int MaximumPdfFacts,
    int MaximumXlsxRows,
    long MaximumOutputBytes,
    TimeSpan RetryBaseDelay,
    TimeSpan ProcessingTimeout,
    TimeSpan QueueAgeWarning)
{
    public static ReportingExecutionOptions Default { get; } = new(
        MaximumAttempts: 3,
        MaximumPdfFacts: 2_000,
        MaximumXlsxRows: 5_000,
        MaximumOutputBytes: 25L * 1024L * 1024L,
        RetryBaseDelay: TimeSpan.FromSeconds(30),
        ProcessingTimeout: TimeSpan.FromSeconds(120),
        QueueAgeWarning: TimeSpan.FromSeconds(120));

    public static ReportingExecutionOptions Create(IConfiguration configuration) => new(
        ReadInt(configuration, "ReportingCenter:MaximumAttempts", 3, 1, 10),
        ReadInt(configuration, "ReportingCenter:MaximumPdfFacts", 2_000, 1, 20_000),
        ReadInt(configuration, "ReportingCenter:MaximumXlsxRows", 5_000, 1, 100_000),
        ReadLong(
            configuration,
            "ReportingCenter:MaximumOutputBytes",
            25L * 1024L * 1024L,
            1_024,
            100L * 1024L * 1024L),
        TimeSpan.FromSeconds(ReadInt(
            configuration,
            "ReportingCenter:RetryBaseDelaySeconds",
            30,
            1,
            300)),
        TimeSpan.FromSeconds(ReadInt(
            configuration,
            "ReportingCenter:ProcessingTimeoutSeconds",
            120,
            5,
            900)),
        TimeSpan.FromSeconds(ReadInt(
            configuration,
            "ReportingCenter:QueueAgeWarningSeconds",
            120,
            5,
            3_600)));

    private static int ReadInt(
        IConfiguration configuration,
        string key,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new InvalidOperationException($"{key} must be between {minimum} and {maximum}.");
        }
        return parsed;
    }

    private static long ReadLong(
        IConfiguration configuration,
        string key,
        long defaultValue,
        long minimum,
        long maximum)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!long.TryParse(raw, out var parsed) || parsed < minimum || parsed > maximum)
        {
            throw new InvalidOperationException($"{key} must be between {minimum} and {maximum}.");
        }
        return parsed;
    }
}
