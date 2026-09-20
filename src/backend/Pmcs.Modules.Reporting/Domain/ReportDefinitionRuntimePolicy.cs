namespace Pmcs.Modules.Reporting.Domain;

internal static class ReportDefinitionRuntimePolicy
{
    public const string DailyDefinitionCode = "daily-report-certified";
    public const string DailyReportSourcePermission = "field.daily-reports.read";
    public const string ProjectStateSourcePermission = "project-state.read";
    public const string ProjectProgressSourcePermission = "planning.progress.read";
    public const string ProjectBaselineSourcePermission = "planning.baselines.read";
    public const string ProjectMilestoneSourcePermission = "planning.milestones.read";

    private static readonly string[] DailyReportSourcePermissions =
        [DailyReportSourcePermission];
    private static readonly string[] ProjectStateSourcePermissions =
        [ProjectStateSourcePermission];
    private static readonly string[] ProjectProgressSourcePermissions =
    [
        ProjectProgressSourcePermission,
        ProjectBaselineSourcePermission,
        ProjectMilestoneSourcePermission
    ];

    public static readonly string[] SupportedDefinitionCodes =
    [
        DailyDefinitionCode,
        ProjectPeriodicReportRuntimeContract.DefinitionCode,
        ExecutiveProjectStateReportRuntimeContract.DefinitionCode,
        ProjectProgressReportRuntimeContract.DefinitionCode
    ];

    public static bool TryGetSourcePermissions(
        string definitionCode,
        out IReadOnlyList<string> permissions)
    {
        permissions = definitionCode switch
        {
            DailyDefinitionCode => DailyReportSourcePermissions,
            ProjectPeriodicReportRuntimeContract.DefinitionCode => DailyReportSourcePermissions,
            ExecutiveProjectStateReportRuntimeContract.DefinitionCode => ProjectStateSourcePermissions,
            ProjectProgressReportRuntimeContract.DefinitionCode => ProjectProgressSourcePermissions,
            _ => Array.Empty<string>()
        };
        return permissions.Count > 0;
    }

    public static IReadOnlyList<string> RequireSourcePermissions(string definitionCode) =>
        TryGetSourcePermissions(definitionCode, out var permissions)
            ? permissions
            : throw new InvalidOperationException("Unsupported report definition.");
}
