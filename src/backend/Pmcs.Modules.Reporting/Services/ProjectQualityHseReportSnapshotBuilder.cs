using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectQualityHseReportSnapshotBuilder
{
    private static readonly string[] RegisterNames =
    [
        "intakes", "inspections", "nonconformances", "defects", "incidents",
        "corrective_actions", "permits", "toolbox_talks", "inspection_test_plan_versions",
        "checklist_template_versions", "test_records", "competency_records",
        "exposure_hours", "risk_matrix_versions"
    ];

    public static ReportSnapshot Build(Guid runId, Guid tenantId, ProjectControlProfile project,
        DateTimeOffset cutoffUtc, ProjectQualityHseReportingResult source,
        DateTimeOffset acceptedAtUtc, DateTimeOffset builtAt) => Build(runId, tenantId,
        ProjectQualityHsePinnedProjectProfile.Capture(project, acceptedAtUtc), cutoffUtc,
        source, acceptedAtUtc, builtAt);

    public static ReportSnapshot Build(Guid runId, Guid tenantId,
        ProjectQualityHsePinnedProjectProfile project, DateTimeOffset cutoffUtc,
        ProjectQualityHseReportingResult source, DateTimeOffset acceptedAtUtc, DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = cutoffUtc.ToUniversalTime();
        var zone = project.ValidateForRun(tenantId, project.Id, cutoff, acceptedAtUtc);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, zone).DateTime);
        Validate(source, tenantId, project, cutoff, date);
        var manifestJson = CanonicalJson.Serialize(source.SourceManifest);
        var classification = MapClassification(source.Classification);
        var payload = new ProjectQualityHseReportSemanticSnapshot(
            ProjectQualityHseReportRuntimeContract.SnapshotSchemaVersion,
            ProjectQualityHseReportRuntimeContract.SemanticContractId,
            ProjectQualityHseReportRuntimeContract.DefinitionCode,
            ProjectQualityHseReportRuntimeContract.DefinitionVersion, source.PolicyVersion,
            MapStatus(source.DataStatus), source.Reasons,
            new ProjectQualityHseReportParameters(),
            new ProjectQualityHseReportProjectIdentity(project.Id, project.TenantId,
                project.Code, project.Name, project.TimeZone, project.Revision,
                project.ConfigurationVersion, project.ConfigurationChangedAtUtc,
                project.CapturedAtUtc, project.Quality, project.Hse),
            new ProjectQualityHseReportCutoffIdentity(cutoff, date), classification,
            Section(source.Quality), Section(source.Hse),
            source.SourceManifestSha256, source.SemanticSha256);
        return ReportSnapshot.Create(Guid.NewGuid(), runId, tenantId, project.Id,
            ProjectQualityHseReportRuntimeContract.SnapshotSchemaVersion,
            MapStatus(source.DataStatus), CanonicalJson.Serialize(payload), manifestJson,
            classification, builtAt, cutoff);
    }

    private static void Validate(ProjectQualityHseReportingResult source, Guid tenantId,
        ProjectQualityHsePinnedProjectProfile project, DateTimeOffset cutoff, DateOnly date)
    {
        if (source.ContractVersion != ProjectQualityHseReportingContract.Version ||
            source.PolicyVersion != ProjectQualityHseReportingContract.PolicyVersion ||
            source.TenantId != tenantId || source.ProjectId != project.Id ||
            source.CutoffLocalDate != date || source.SourceCutoffUtc.ToUniversalTime() != cutoff ||
            source.Quality is null || source.Hse is null || source.Reasons is null ||
            source.SourceManifest is null ||
            !Enum.IsDefined(source.DataStatus) || !Enum.IsDefined(source.Classification) ||
            source.Classification < QualityHseReportingClassification.Confidential ||
            source.DataStatus != AggregateStatus(source.Quality.Status, source.Hse.Status) ||
            !source.Reasons.SequenceEqual(source.Quality.Reasons.Concat(source.Hse.Reasons)
                .Distinct().OrderBy(x => x)) ||
            source.Reasons.Any(x => !Enum.IsDefined(x)) ||
            !ValidSection(source.Quality, cutoff) || !ValidSection(source.Hse, cutoff) ||
            source.Classification != (source.Quality.Classification == QualityHseReportingClassification.Restricted ||
                source.Hse.Classification == QualityHseReportingClassification.Restricted
                ? QualityHseReportingClassification.Restricted : QualityHseReportingClassification.Confidential) ||
            source.SourceManifestSha256 != QualityHseReportingHash.Compute(source.SourceManifest) ||
            source.SemanticSha256 != QualityHseReportingHash.Result(source))
            throw Invalid("source.invalid", "Quality/HSE source content or semantic digest is invalid.");

        var manifest = source.SourceManifest;
        if (manifest.ManifestVersion != ProjectQualityHseReportingContract.ManifestVersion ||
            manifest.ContractVersion != source.ContractVersion || manifest.PolicyVersion != source.PolicyVersion ||
            manifest.TenantId != tenantId || manifest.ProjectId != project.Id ||
            manifest.CutoffLocalDate != date || manifest.SourceCutoffUtc.ToUniversalTime() != cutoff ||
            manifest.WatermarkUtc.ToUniversalTime() != cutoff ||
            manifest.ProjectConfigurationVersion != project.ConfigurationVersion ||
            manifest.ProjectConfigurationChangedAtUtc.ToUniversalTime() != project.ConfigurationChangedAtUtc ||
            manifest.Classification != source.Classification ||
            manifest.QualityEnabled && project.Quality != Pmcs.Modules.Projects.Contracts.ProjectFeatureState.Active ||
            manifest.HseEnabled && project.Hse != Pmcs.Modules.Projects.Contracts.ProjectFeatureState.Active ||
            manifest.QualitySafetyConfigurationRevision is > 1 &&
                (source.Quality.Status != QualityHseReportingStatus.InsufficientData ||
                 source.Hse.Status != QualityHseReportingStatus.InsufficientData) ||
            manifest.QualitySafetyConfigurationChangedAtUtc > cutoff ||
            manifest.Registers is null || manifest.Registers.Count != RegisterNames.Length ||
            !manifest.Registers.Select(x => x.Name).SequenceEqual(RegisterNames) ||
            manifest.Registers.Any(x => x.SourceCount < 0 ||
                x.SourceCount > ProjectQualityHseReportingContract.MaximumPerRegister ||
                !IsDigest(x.FactSha256)) ||
            !IsDigest(source.SourceManifestSha256) || !IsDigest(source.SemanticSha256))
            throw Invalid("source_manifest.invalid", "Quality/HSE source manifest is inconsistent with the pinned scope.");
    }

    private static bool ValidSection(QualityHseReportingSection section, DateTimeOffset cutoff)
    {
        if (!Enum.IsDefined(section.Status) || !Enum.IsDefined(section.Classification) ||
            section.Classification < QualityHseReportingClassification.Confidential ||
            section.Rows is null || section.Reasons is null ||
            section.Reasons.Any(x => !Enum.IsDefined(x)) ||
            section.Reasons.Distinct().Count() != section.Reasons.Count ||
            section.Rows.Count > ProjectQualityHseReportingContract.MaximumFacts ||
            section.Rows.Select(x => x.Id).Distinct().Count() != section.Rows.Count ||
            section.Rows.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Number) ||
                !Enum.IsDefined(x.Kind) || string.IsNullOrWhiteSpace(x.State) ||
                x.OfficialAtUtc.ToUniversalTime() > cutoff || x.Hours < 0))
            return false;
        return section.Status switch
        {
            QualityHseReportingStatus.Available => section.OfficialCount == section.Rows.Count &&
                section.Rows.Count > 0,
            QualityHseReportingStatus.NoData => section.OfficialCount == 0 && section.Rows.Count == 0,
            QualityHseReportingStatus.NotConfigured or QualityHseReportingStatus.InsufficientData =>
                section.OfficialCount is null && section.Rows.Count == 0 && section.Reasons.Count > 0,
            _ => false
        };
    }

    private static QualityHseReportingStatus AggregateStatus(
        QualityHseReportingStatus quality, QualityHseReportingStatus hse)
    {
        if (quality == QualityHseReportingStatus.InsufficientData || hse == QualityHseReportingStatus.InsufficientData)
            return QualityHseReportingStatus.InsufficientData;
        if (quality == QualityHseReportingStatus.Available || hse == QualityHseReportingStatus.Available)
            return QualityHseReportingStatus.Available;
        return quality == QualityHseReportingStatus.NotConfigured && hse == QualityHseReportingStatus.NotConfigured
            ? QualityHseReportingStatus.NotConfigured : QualityHseReportingStatus.NoData;
    }

    private static bool IsDigest(string value) => value is { Length: 64 } &&
        value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static ProjectQualityHseReportSection Section(QualityHseReportingSection section) =>
        new(section.Status, section.OfficialCount, section.Rows, section.Reasons,
            MapClassification(section.Classification));

    private static ReportDataStatus MapStatus(QualityHseReportingStatus status) => status switch
    {
        QualityHseReportingStatus.NotConfigured => ReportDataStatus.NotConfigured,
        QualityHseReportingStatus.NoData => ReportDataStatus.NoData,
        QualityHseReportingStatus.InsufficientData => ReportDataStatus.InsufficientData,
        QualityHseReportingStatus.Available => ReportDataStatus.Available,
        _ => throw Invalid("status.invalid", "Unknown Quality/HSE status.")
    };

    private static ReportClassification MapClassification(QualityHseReportingClassification classification) =>
        classification switch
        {
            QualityHseReportingClassification.Confidential => ReportClassification.Confidential,
            QualityHseReportingClassification.Restricted => ReportClassification.Restricted,
            _ => throw Invalid("classification.invalid", "Unknown Quality/HSE classification.")
        };

    private static DomainRuleException Invalid(string code, string message) =>
        new($"reporting.project_quality_hse.{code}", message);
}
