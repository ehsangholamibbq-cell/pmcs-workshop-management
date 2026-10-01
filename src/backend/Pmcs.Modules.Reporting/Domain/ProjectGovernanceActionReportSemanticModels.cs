using Pmcs.Modules.ActionControl.Contracts;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ProjectGovernanceActionReportSemanticSnapshot(
    string SchemaVersion, string SemanticContractId, string DefinitionCode,
    string DefinitionVersion, string PolicyVersion, ReportDataStatus DataStatus,
    IReadOnlyCollection<GovernanceActionReportingReason> Reasons,
    ProjectGovernanceActionReportParameters Parameters,
    ProjectGovernanceActionReportProjectIdentity Project,
    ProjectGovernanceActionReportCutoffIdentity Cutoff,
    ReportClassification Classification,
    ProjectGovernanceActionReportSection Issues, ProjectGovernanceActionReportSection Risks,
    ProjectGovernanceActionReportSection Decisions, ProjectGovernanceActionReportSection Escalations,
    ProjectGovernanceActionReportSection Actions,
    string SourceManifestSha256, string SemanticSha256);

internal sealed record ProjectGovernanceActionReportProjectIdentity(
    Guid Id, Guid TenantId, string Code, string Name, string TimeZone,
    long Revision, long ConfigurationVersion, DateTimeOffset ConfigurationChangedAtUtc,
    DateTimeOffset ProfileCapturedAtUtc);

internal sealed record ProjectGovernanceActionReportCutoffIdentity(
    DateTimeOffset SourceCutoffUtc, DateOnly CutoffLocalDate);

internal sealed record ProjectGovernanceActionReportSection(
    GovernanceActionReportingStatus Status, int? OfficialCount,
    IReadOnlyCollection<GovernanceActionReportingFact> Rows,
    IReadOnlyCollection<GovernanceActionReportingReason> Reasons,
    ReportClassification Classification);
