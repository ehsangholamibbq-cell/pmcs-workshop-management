using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.QualityAssurance.Contracts;

namespace Pmcs.Modules.QualityAssurance.Services;

internal sealed class QaDiagnosticsService(
    HealthCheckService healthChecks,
    QualityAssuranceRuntimeOptions options,
    IClock clock) : IQaDiagnosticsService
{
    public async Task<QaDiagnosticsSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var probes = report.Entries
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new QaHealthProbe(
                entry.Key,
                entry.Value.Status.ToString(),
                (long)Math.Ceiling(entry.Value.Duration.TotalMilliseconds),
                entry.Value.Description))
            .ToArray();

        return new QaDiagnosticsSnapshot(
            1,
            options.EnvironmentName,
            options.DatabaseName,
            options.ReleaseCommit,
            options.ReleaseVersion,
            options.ReleaseBuiltAt,
            report.Status.ToString(),
            probes,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["authentication"] = "independent-key-real-actor",
                ["authorization"] = "central-permission-service",
                ["database"] = "pmcs_qa_prefix_required",
                ["destructiveReset"] = "external-confirmed-harness-only",
                ["publicResetEndpoint"] = "absent",
                ["moduleTableAccess"] = "forbidden"
            },
            clock.UtcNow);
    }
}
