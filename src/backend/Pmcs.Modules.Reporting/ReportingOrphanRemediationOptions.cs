using Microsoft.Extensions.Configuration;

namespace Pmcs.Modules.Reporting;

public sealed record ReportingOrphanRemediationOptions(
    ReportingOrphanRemediationMode Mode,
    TimeSpan MinimumAge,
    TimeSpan PollingInterval,
    int BatchSize,
    int MaximumCandidatesPerSweep)
{
    public static ReportingOrphanRemediationOptions Create(IConfiguration configuration)
    {
        var configuredMode = configuration["ReportingCenter:OrphanRemediationMode"]?.Trim();
        var mode = string.IsNullOrWhiteSpace(configuredMode)
            ? ReportingOrphanRemediationMode.Disabled
            : Enum.TryParse<ReportingOrphanRemediationMode>(
                configuredMode,
                ignoreCase: false,
                out var parsedMode) && Enum.IsDefined(parsedMode)
                    ? parsedMode
                    : throw new InvalidOperationException(
                        "ReportingCenter:OrphanRemediationMode must be Disabled, InventoryOnly or ApplyEligible.");
        var batchSize = ReadInt(
            configuration,
            "ReportingCenter:OrphanRemediationBatchSize",
            25,
            1,
            100);
        var maximumCandidates = ReadInt(
            configuration,
            "ReportingCenter:OrphanRemediationMaximumCandidatesPerSweep",
            500,
            1,
            2_000);
        if (maximumCandidates < batchSize)
        {
            throw new InvalidOperationException(
                "ReportingCenter:OrphanRemediationMaximumCandidatesPerSweep must not be smaller than the batch size.");
        }

        return new ReportingOrphanRemediationOptions(
            mode,
            TimeSpan.FromHours(ReadInt(
                configuration,
                "ReportingCenter:OrphanRemediationMinimumAgeHours",
                24,
                24,
                8_760)),
            TimeSpan.FromSeconds(ReadInt(
                configuration,
                "ReportingCenter:OrphanRemediationPollSeconds",
                3_600,
                5,
                86_400)),
            batchSize,
            maximumCandidates);
    }

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
}

public enum ReportingOrphanRemediationMode
{
    Disabled = 0,
    InventoryOnly = 1,
    ApplyEligible = 2
}
