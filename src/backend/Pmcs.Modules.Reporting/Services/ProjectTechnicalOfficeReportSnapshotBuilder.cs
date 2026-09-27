using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.TechnicalOffice.Contracts;
using Pmcs.Modules.TechnicalOffice.Services;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectTechnicalOfficeReportSnapshotBuilder
{
    internal static ReportSnapshot Build(
        Guid runId, Guid tenantId, ProjectTechnicalOfficePinnedProjectProfile project,
        DateTimeOffset sourceCutoffUtc, ProjectTechnicalOfficeReportingResult source,
        DateTimeOffset acceptedAtUtc, DateTimeOffset builtAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var zone = project.ValidateForRun(tenantId, project.Id, cutoff, acceptedAtUtc);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, zone).DateTime);
        Validate(source, project, tenantId, localDate, cutoff);
        var classification = source.Classification switch
        {
            TechnicalReportingClassification.Confidential => ReportClassification.Confidential,
            TechnicalReportingClassification.Restricted => ReportClassification.Restricted,
            _ => throw Invalid("classification.invalid", "F07 cannot be published below Confidential.")
        };
        var dataStatus = source.DataStatus switch
        {
            TechnicalReportingStatus.NotConfigured => ReportDataStatus.NotConfigured,
            TechnicalReportingStatus.NoData => ReportDataStatus.NoData,
            TechnicalReportingStatus.InsufficientData => ReportDataStatus.InsufficientData,
            TechnicalReportingStatus.Available => ReportDataStatus.Available,
            _ => throw Invalid("status.invalid", "F07 source status is unknown.")
        };
        var manifestJson = CanonicalJson.Serialize(source.SourceManifest);
        if (CanonicalJson.Sha256(manifestJson) != source.SourceManifestSha256)
            throw Invalid("manifest.hash_mismatch", "F07 manifest hash does not match its content.");
        var payload = new ProjectTechnicalOfficeReportSemanticSnapshot(
            ProjectTechnicalOfficeReportRuntimeContract.SnapshotSchemaVersion,
            ProjectTechnicalOfficeReportRuntimeContract.SemanticContractId,
            ProjectTechnicalOfficeReportRuntimeContract.DefinitionCode,
            ProjectTechnicalOfficeReportRuntimeContract.DefinitionVersion,
            source.PolicyVersion, dataStatus, source.Reasons,
            new ProjectTechnicalOfficeReportParameters(),
            new ProjectTechnicalOfficeProjectIdentity(
                project.Id, project.TenantId, project.Code, project.Name, project.TimeZone,
                project.Revision, project.ConfigurationVersion, project.ConfigurationChangedAtUtc),
            new ProjectTechnicalOfficeCutoffIdentity(cutoff, localDate), classification,
            source.Documents, source.Transmittals, source.Rfis, source.Submittals,
            source.SourceManifestSha256, source.SemanticSha256);
        return ReportSnapshot.Create(Guid.NewGuid(), runId, tenantId, project.Id,
            ProjectTechnicalOfficeReportRuntimeContract.SnapshotSchemaVersion,
            dataStatus, CanonicalJson.Serialize(payload), manifestJson,
            classification, builtAtUtc, cutoff);
    }

    private static void Validate(ProjectTechnicalOfficeReportingResult source,
        ProjectTechnicalOfficePinnedProjectProfile project,
        Guid tenantId, Guid projectId, DateOnly localDate, DateTimeOffset cutoff)
    {
        var manifest = source.SourceManifest;
        if (source.ContractVersion != ProjectTechnicalOfficeReportingContract.Version ||
            source.PolicyVersion != ProjectTechnicalOfficeReportingContract.PolicyVersion ||
            source.TenantId != tenantId || source.ProjectId != projectId ||
            source.SourceCutoffUtc.ToUniversalTime() != cutoff || source.CutoffLocalDate != localDate ||
            !Enum.IsDefined(source.Classification) ||
            source.Classification < TechnicalReportingClassification.Confidential ||
            !Enum.IsDefined(source.DataStatus) || source.Reasons is null ||
            source.Reasons.Distinct().Count() != source.Reasons.Count ||
            source.Reasons.Any(reason => !Enum.IsDefined(reason)) ||
            source.Documents is null || source.Transmittals is null ||
            source.Rfis is null || source.Submittals is null || manifest is null ||
            manifest.ManifestVersion != ProjectTechnicalOfficeReportingContract.ManifestVersion ||
            manifest.ContractVersion != source.ContractVersion || manifest.PolicyVersion != source.PolicyVersion ||
            manifest.TenantId != tenantId || manifest.ProjectId != projectId ||
            manifest.SourceCutoffUtc.ToUniversalTime() != cutoff || manifest.CutoffLocalDate != localDate ||
            manifest.Classification != source.Classification || manifest.WatermarkUtc != cutoff ||
            manifest.ConfigurationVersion != project.ConfigurationVersion ||
            manifest.ConfigurationEffectiveAtUtc.ToUniversalTime() != project.ConfigurationChangedAtUtc ||
            manifest.ConfigurationEffectiveAtUtc > cutoff ||
            source.Classification == TechnicalReportingClassification.Restricted &&
                !manifest.RestrictedPublicationApproved ||
            manifest.Collections is null || manifest.Collections.Count != 5 ||
            source.SemanticSha256 != TechnicalReportingHash.ComputeResult(source))
            throw Invalid("source.invalid", "F07 result, version, scope or semantic hash is invalid.");
        if (manifest.SourceEnabled == (source.DataStatus == TechnicalReportingStatus.NotConfigured))
            throw Invalid("configuration.invalid", "F07 configuration evidence contradicts the result.");
        if (source.Documents.Status == TechnicalReportingStatus.Available &&
                source.Documents.OfficialCount != source.Documents.Rows.Count ||
            source.Transmittals.Status == TechnicalReportingStatus.Available &&
                source.Transmittals.OfficialCount != source.Transmittals.Rows.Count ||
            source.Rfis.Status == TechnicalReportingStatus.Available &&
                source.Rfis.OfficialCount != source.Rfis.Rows.Count ||
            source.Submittals.Status == TechnicalReportingStatus.Available &&
                source.Submittals.OfficialCount != source.Submittals.Rows.Count ||
            new[] { source.Documents.Status, source.Transmittals.Status,
                source.Rfis.Status, source.Submittals.Status }.Any(item =>
                    item == TechnicalReportingStatus.InsufficientData) &&
                source.DataStatus != TechnicalReportingStatus.InsufficientData)
            throw Invalid("sections.invalid", "F07 section completeness contradicts result status.");
    }

    private static DomainRuleException Invalid(string code, string message) =>
        new($"reporting.project_technical_office.{code}", message);
}
