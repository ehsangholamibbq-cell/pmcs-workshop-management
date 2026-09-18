using Microsoft.Extensions.Configuration;

namespace Pmcs.Modules.Reporting;

internal sealed record ReportingWorkerQualificationOptions(
    string WorkerInstanceId,
    ReportingWorkerQualificationPausePoint PausePoint,
    Guid? TargetRunId)
{
    private const string QaGatewayConfigurationKey = "PMCS_QA_GATEWAY_ENABLED";

    public static ReportingWorkerQualificationOptions Create(IConfiguration configuration)
    {
        var workerInstanceId = configuration["ReportingCenter:WorkerInstanceId"]?.Trim();
        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            workerInstanceId = $"worker-{Guid.NewGuid():N}";
        }
        if (workerInstanceId.Length > 80 || workerInstanceId.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new InvalidOperationException(
                "ReportingCenter:WorkerInstanceId must contain at most 80 ASCII letters, digits, '-', '_' or '.'.");
        }

        var configuredPausePoint = configuration["ReportingCenter:QualificationPausePoint"]?.Trim();
        var pausePoint = string.IsNullOrWhiteSpace(configuredPausePoint)
            ? ReportingWorkerQualificationPausePoint.None
            : Enum.TryParse<ReportingWorkerQualificationPausePoint>(
                configuredPausePoint,
                ignoreCase: false,
                out var parsedPausePoint) && Enum.IsDefined(parsedPausePoint)
                    ? parsedPausePoint
                    : throw new InvalidOperationException(
                        "ReportingCenter:QualificationPausePoint is invalid.");

        Guid? targetRunId = null;
        if (pausePoint != ReportingWorkerQualificationPausePoint.None)
        {
            var qaGatewayEnabled = bool.TryParse(
                configuration[QaGatewayConfigurationKey],
                out var qaEnabled) && qaEnabled;
            if (!qaGatewayEnabled)
            {
                throw new InvalidOperationException(
                    "Reporting worker qualification pauses require the isolated QA gateway.");
            }

            if (!Guid.TryParse(
                    configuration["ReportingCenter:QualificationTargetRunId"],
                    out var parsedTargetRunId) || parsedTargetRunId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "ReportingCenter:QualificationTargetRunId is required for a qualification pause.");
            }
            targetRunId = parsedTargetRunId;
        }

        return new ReportingWorkerQualificationOptions(workerInstanceId, pausePoint, targetRunId);
    }

    public bool ShouldPause(ReportingWorkerQualificationPausePoint point, Guid runId) =>
        PausePoint == point && TargetRunId == runId;
}

internal enum ReportingWorkerQualificationPausePoint
{
    None = 0,
    AfterSnapshotRowLock = 1,
    BeforeStorage = 2,
    AfterStorage = 3
}
