using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Endpoints;

namespace Pmcs.Modules.Reporting.Services;

internal static class DailyReportSnapshotBuilder
{
    public const string SnapshotSchemaVersion = "pmcs.reporting.daily-report/v1";

    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ProjectControlProfile project,
        DateTimeOffset asOfUtc,
        DailyReportReportParameters parameters,
        DailyReportReportingChain? chain,
        DateTimeOffset builtAt)
    {
        var orderedVersions = chain?.Versions
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId)
            .ToArray() ?? [];
        var current = chain?.CurrentOfficialReportId is { } currentId
            ? orderedVersions.SingleOrDefault(version => version.ReportId == currentId)
            : null;
        var dataStatus = orderedVersions.Length == 0
            ? ReportDataStatus.NoData
            : current is null || current.Facts.Count == 0
                ? ReportDataStatus.InsufficientData
                : ReportDataStatus.Available;
        DailyReportReportingVersion[] includedVersions = parameters.IncludeRevisionChain
            ? orderedVersions
            : current is null ? [] : [current];

        var sourceManifest = new DailyReportSourceManifest(
            "field.daily-report",
            parameters.DailyReportId,
            chain?.RootReportId,
            chain?.CurrentOfficialReportId,
            asOfUtc.ToUniversalTime(),
            includedVersions.Select(version => new DailyReportSourceVersion(
                version.ReportId,
                version.VersionNumber,
                version.Revision,
                version.ApprovedAt.ToUniversalTime(),
                version.SupersededAt?.ToUniversalTime(),
                version.LastModifiedAt.ToUniversalTime(),
                version.Facts.Count)).ToArray());
        var sourceManifestJson = CanonicalJson.Serialize(sourceManifest);
        var payload = new DailyReportSemanticSnapshot(
            SnapshotSchemaVersion,
            "daily-report-certified",
            "1.0.0",
            dataStatus,
            new ReportProjectIdentity(
                project.Id,
                project.Code,
                project.Name,
                project.TimeZone,
                project.Revision),
            asOfUtc.ToUniversalTime(),
            parameters,
            chain?.RootReportId,
            chain?.CurrentOfficialReportId,
            CanonicalJson.Sha256(sourceManifestJson),
            includedVersions);

        return ReportSnapshot.Create(
            Guid.NewGuid(),
            runId,
            tenantId,
            project.Id,
            SnapshotSchemaVersion,
            dataStatus,
            CanonicalJson.Serialize(payload),
            sourceManifestJson,
            ReportClassification.Internal,
            builtAt,
            asOfUtc);
    }

    private sealed record DailyReportSemanticSnapshot(
        string SchemaVersion,
        string DefinitionCode,
        string TemplateVersion,
        ReportDataStatus DataStatus,
        ReportProjectIdentity Project,
        DateTimeOffset AsOfUtc,
        DailyReportReportParameters Parameters,
        Guid? RootReportId,
        Guid? CurrentOfficialReportId,
        string SourceManifestSha256,
        IReadOnlyCollection<DailyReportReportingVersion> Versions);

    private sealed record ReportProjectIdentity(
        Guid Id,
        string Code,
        string Name,
        string TimeZone,
        long Revision);

    private sealed record DailyReportSourceManifest(
        string Source,
        Guid RequestedReportId,
        Guid? RootReportId,
        Guid? CurrentOfficialReportId,
        DateTimeOffset SourceCutoffUtc,
        IReadOnlyCollection<DailyReportSourceVersion> Versions);

    private sealed record DailyReportSourceVersion(
        Guid ReportId,
        int VersionNumber,
        long Revision,
        DateTimeOffset ApprovedAt,
        DateTimeOffset? SupersededAt,
        DateTimeOffset LastModifiedAt,
        int FactCount);
}
