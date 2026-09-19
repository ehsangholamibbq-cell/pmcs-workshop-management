namespace Pmcs.Modules.Reporting.Domain;

public static class ProjectPeriodicReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F02-SEMANTIC-001";
    public const string DefinitionCode = "project-periodic-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion = "pmcs.reporting.project-periodic.parameters/v1";
    public const string SnapshotSchemaVersion = "pmcs.reporting.project-periodic.snapshot/v1";
}

public sealed record ProjectPeriodicReportParameters(
    ProjectReportPeriodKind PeriodKind,
    DateOnly PeriodStartLocalDate);

public enum ProjectReportPeriodKind
{
    Weekly = 1,
    Monthly = 2
}

public enum ProjectPeriodicReportReasonCode
{
    ReportingCadenceMissing = 1,
    DailyWorkflowMissing = 2,
    DailyCutoffMissing = 3,
    WorkingCalendarMissing = 4,
    PeriodOpenAtCutoff = 5,
    ExpectedSlotMissing = 6,
    OfficialVersionMissing = 7,
    OfficialReportEmpty = 8
}

public enum ProjectPeriodicReportUnitState
{
    SourceUnit = 1,
    UnitMissing = 2
}
