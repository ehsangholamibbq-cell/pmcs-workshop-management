using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ProjectQualityHseReportSemanticSnapshot(
    string SchemaVersion, string SemanticContractId, string DefinitionCode,
    string DefinitionVersion, string PolicyVersion, ReportDataStatus DataStatus,
    IReadOnlyCollection<QualityHseReportingReason> Reasons,
    ProjectQualityHseReportParameters Parameters,
    ProjectQualityHseReportProjectIdentity Project,
    ProjectQualityHseReportCutoffIdentity Cutoff,
    ReportClassification Classification,
    ProjectQualityHseReportSection Quality, ProjectQualityHseReportSection Hse,
    string SourceManifestSha256, string SemanticSha256);

internal sealed record ProjectQualityHseReportProjectIdentity(
    Guid Id, Guid TenantId, string Code, string Name, string TimeZone,
    long Revision, long ConfigurationVersion, DateTimeOffset ConfigurationChangedAtUtc,
    DateTimeOffset ProfileCapturedAtUtc, ProjectFeatureState Quality, ProjectFeatureState Hse);

internal sealed record ProjectQualityHseReportCutoffIdentity(
    DateTimeOffset SourceCutoffUtc, DateOnly CutoffLocalDate);

internal sealed record ProjectQualityHseReportSection(
    QualityHseReportingStatus Status, int? OfficialCount,
    IReadOnlyCollection<QualityHseReportingFact> Rows,
    IReadOnlyCollection<QualityHseReportingReason> Reasons,
    ReportClassification Classification);
