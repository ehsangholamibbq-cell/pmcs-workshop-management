using Pmcs.Modules.TechnicalOffice.Contracts;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ProjectTechnicalOfficeReportSemanticSnapshot(
    string SchemaVersion, string SemanticContractId, string DefinitionCode,
    string DefinitionVersion, string PolicyVersion, ReportDataStatus DataStatus,
    IReadOnlyCollection<TechnicalReportingReason> Reasons,
    ProjectTechnicalOfficeReportParameters Parameters,
    ProjectTechnicalOfficeProjectIdentity Project,
    ProjectTechnicalOfficeCutoffIdentity Cutoff,
    ReportClassification Classification,
    TechnicalReportingSection<TechnicalReportingDocumentRow> Documents,
    TechnicalReportingSection<TechnicalReportingTransmittalRow> Transmittals,
    TechnicalReportingSection<TechnicalReportingRfiRow> Rfis,
    TechnicalReportingSection<TechnicalReportingSubmittalRow> Submittals,
    string SourceManifestSha256, string SourceSemanticSha256);

internal sealed record ProjectTechnicalOfficeProjectIdentity(
    Guid Id, Guid TenantId, string Code, string Name, string TimeZone,
    long Revision, long ConfigurationVersion, DateTimeOffset ConfigurationChangedAtUtc);

internal sealed record ProjectTechnicalOfficeCutoffIdentity(
    DateTimeOffset SourceCutoffUtc, DateOnly CutoffLocalDate);
