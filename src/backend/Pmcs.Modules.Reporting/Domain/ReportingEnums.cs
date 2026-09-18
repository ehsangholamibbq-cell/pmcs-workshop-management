namespace Pmcs.Modules.Reporting.Domain;

public enum ReportDefinitionScope
{
    Project = 1,
    Tenant = 2
}

public enum ReportClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

public enum ReportDefinitionStatus
{
    Active = 1,
    Retired = 2
}

public enum ReportFormat
{
    Pdf = 1,
    Xlsx = 2,
    Csv = 3
}

public enum ReportRunStatus
{
    Queued = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5
}

public enum ReportPipelineStage
{
    Queued = 1,
    BuildingSnapshot = 2,
    SnapshotReady = 3,
    Rendering = 4,
    Complete = 5,
    Failed = 6,
    Cancelled = 7
}

public enum ReportDataStatus
{
    Pending = 1,
    Available = 2,
    NoData = 3,
    InsufficientData = 4,
    NotConfigured = 5
}

public enum ReportOutputArchiveState
{
    Active = 1,
    Archived = 2
}
