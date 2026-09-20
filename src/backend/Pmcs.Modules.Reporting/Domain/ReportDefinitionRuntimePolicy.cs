namespace Pmcs.Modules.Reporting.Domain;

internal static class ReportDefinitionRuntimePolicy
{
    public const string DailyDefinitionCode = "daily-report-certified";
    public const string DailyReportSourcePermission = "field.daily-reports.read";
    public const string ProjectStateSourcePermission = "project-state.read";

    public static readonly string[] SupportedDefinitionCodes =
    [
        DailyDefinitionCode,
        ProjectPeriodicReportRuntimeContract.DefinitionCode,
        ExecutiveProjectStateReportRuntimeContract.DefinitionCode
    ];

    public static bool TryGetSourcePermission(string definitionCode, out string permission)
    {
        permission = definitionCode switch
        {
            DailyDefinitionCode => DailyReportSourcePermission,
            ProjectPeriodicReportRuntimeContract.DefinitionCode => DailyReportSourcePermission,
            ExecutiveProjectStateReportRuntimeContract.DefinitionCode => ProjectStateSourcePermission,
            _ => string.Empty
        };
        return permission.Length > 0;
    }

    public static string RequireSourcePermission(string definitionCode) =>
        TryGetSourcePermission(definitionCode, out var permission)
            ? permission
            : throw new InvalidOperationException("Unsupported report definition.");
}
