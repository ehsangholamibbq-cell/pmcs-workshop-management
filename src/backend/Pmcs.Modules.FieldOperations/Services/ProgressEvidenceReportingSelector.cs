using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;

namespace Pmcs.Modules.FieldOperations.Services;

internal static class ProgressEvidenceReportingSelector
{
    public static ProgressEvidenceReportingProjection Select(
        Guid tenantId,
        Guid projectId,
        DateOnly throughLocalDate,
        DateTimeOffset sourceCutoffUtc,
        IReadOnlyCollection<ProgressEvidenceReportingVersion> versions,
        ProgressEvidenceReportingClassification sourceClassification =
            ProgressEvidenceReportingClassification.Internal)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        if (tenantId == Guid.Empty || projectId == Guid.Empty || throughLocalDate == default || cutoff == default ||
            !Enum.IsDefined(sourceClassification))
        {
            throw new DomainRuleException(
                "field.progress_reporting.scope.invalid",
                "The progress evidence reporting scope is invalid.");
        }

        var eligible = versions
            .Where(version => version.ReportDate <= throughLocalDate &&
                version.ApprovedAt.ToUniversalTime() <= cutoff)
            .Select(version => ValidateAndNormalize(version, cutoff))
            .ToArray();
        if (eligible.Select(version => version.ReportId).Distinct().Count() != eligible.Length)
        {
            throw new DomainRuleException(
                "field.progress_reporting.version.duplicate",
                "Progress evidence contains duplicate daily-report version identities.");
        }

        var duplicateFact = eligible
            .SelectMany(version => version.Facts)
            .GroupBy(fact => fact.FactId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateFact is not null)
        {
            throw new DomainRuleException(
                "field.progress_reporting.fact.duplicate",
                "Progress evidence contains duplicate fact identities.");
        }

        var roots = eligible
            .GroupBy(version => version.RootReportId)
            .Select(group => BuildRoot(group.Key, group.ToArray(), cutoff, sourceClassification))
            .OrderBy(root => root.ReportDate)
            .ThenBy(root => root.RootReportId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (roots.GroupBy(root => root.ReportDate).Any(group => group.Count() > 1))
        {
            throw new DomainRuleException(
                "field.progress_reporting.root_date.duplicate",
                "More than one daily-report root exists for a progress evidence date.");
        }

        var classification = roots
            .Select(root => root.Classification)
            .Append(sourceClassification)
            .Max();
        return new ProgressEvidenceReportingProjection(
            ProgressEvidenceReportingContract.Version,
            tenantId,
            projectId,
            throughLocalDate,
            cutoff,
            classification,
            roots);
    }

    private static ProgressEvidenceReportingRoot BuildRoot(
        Guid rootReportId,
        ProgressEvidenceReportingVersion[] versions,
        DateTimeOffset sourceCutoffUtc,
        ProgressEvidenceReportingClassification classification)
    {
        if (rootReportId == Guid.Empty || versions.Select(version => version.ReportDate).Distinct().Count() != 1 ||
            versions.Select(version => version.VersionNumber).Distinct().Count() != versions.Count)
        {
            throw new DomainRuleException(
                "field.progress_reporting.root.invalid",
                "A progress evidence root has invalid date or version lineage.");
        }

        var canonical = versions
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var current = canonical
            .Where(version => IsOfficialAt(version.ApprovedAt, version.SupersededAt, sourceCutoffUtc))
            .ToArray();
        if (current.Length > 1)
        {
            throw new DomainRuleException(
                "field.progress_reporting.current_official.duplicate",
                "A daily-report root has more than one official version at the cutoff.");
        }

        return new ProgressEvidenceReportingRoot(
            rootReportId,
            canonical[0].ReportDate,
            current.SingleOrDefault()?.ReportId,
            classification,
            canonical);
    }

    private static ProgressEvidenceReportingVersion ValidateAndNormalize(
        ProgressEvidenceReportingVersion version,
        DateTimeOffset sourceCutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(version);
        var approvedAt = version.ApprovedAt.ToUniversalTime();
        var supersededAt = version.SupersededAt?.ToUniversalTime();
        if (version.ReportId == Guid.Empty || version.RootReportId == Guid.Empty ||
            version.VersionNumber <= 0 || version.ReportDate == default || approvedAt == default ||
            approvedAt > sourceCutoffUtc || supersededAt <= approvedAt || version.Facts is null)
        {
            throw new DomainRuleException(
                "field.progress_reporting.version.invalid",
                "A progress evidence version violates its official lifecycle contract.");
        }

        var facts = version.Facts
            .Select(fact => ValidateAndNormalize(fact, approvedAt))
            .OrderBy(fact => fact.FactId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (facts.Select(fact => fact.FactId).Distinct().Count() != facts.Length)
        {
            throw new DomainRuleException(
                "field.progress_reporting.fact.duplicate",
                "A daily-report version contains duplicate progress facts.");
        }

        return version with
        {
            ApprovedAt = approvedAt,
            SupersededAt = supersededAt,
            Facts = facts
        };
    }

    private static ProgressEvidenceReportingFact ValidateAndNormalize(
        ProgressEvidenceReportingFact fact,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var createdAt = fact.CreatedAt.ToUniversalTime();
        if (fact.FactId == Guid.Empty || fact.MeasurementItemId == Guid.Empty || fact.Quantity < 0 ||
            createdAt == default || createdAt > approvedAt ||
            (fact.Quantity.HasValue && string.IsNullOrWhiteSpace(fact.Unit)) || fact.Unit?.Trim().Length > 40)
        {
            throw new DomainRuleException(
                "field.progress_reporting.fact.invalid",
                "A progress fact violates the minimized reporting evidence contract.");
        }

        return fact with
        {
            Unit = string.IsNullOrWhiteSpace(fact.Unit) ? null : fact.Unit.Trim(),
            CreatedAt = createdAt
        };
    }

    private static bool IsOfficialAt(
        DateTimeOffset approvedAt,
        DateTimeOffset? supersededAt,
        DateTimeOffset sourceCutoffUtc) =>
        approvedAt <= sourceCutoffUtc &&
        (!supersededAt.HasValue || sourceCutoffUtc < supersededAt.Value);
}
