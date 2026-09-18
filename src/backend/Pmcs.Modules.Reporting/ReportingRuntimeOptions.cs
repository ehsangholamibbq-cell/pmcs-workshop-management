using Microsoft.Extensions.Configuration;

namespace Pmcs.Modules.Reporting;

public sealed record ReportingRuntimeOptions(
    bool Phase1Enabled,
    bool WorkerEnabled,
    TimeSpan PollingInterval)
{
    public static ReportingRuntimeOptions Create(IConfiguration configuration)
    {
        var phase1Enabled = bool.TryParse(
            configuration["ReportingCenter:Phase1Enabled"],
            out var enabled) && enabled;
        var workerEnabled = phase1Enabled && bool.TryParse(
            configuration["ReportingCenter:WorkerEnabled"],
            out var worker) && worker;
        var pollSeconds = int.TryParse(
            configuration["ReportingCenter:PollSeconds"],
            out var parsedSeconds) && parsedSeconds is >= 1 and <= 300
                ? parsedSeconds
                : 5;
        return new ReportingRuntimeOptions(
            phase1Enabled,
            workerEnabled,
            TimeSpan.FromSeconds(pollSeconds));
    }
}
