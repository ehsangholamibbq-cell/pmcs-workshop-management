using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ProjectIntelligence.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Services;

internal static class ProjectStateReportingSelector
{
    public static ProjectStateReportingSelection Select(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        ProjectStateReportingSourceState sourceState,
        ProjectStateReportingClassification sourceClassification,
        DateTimeOffset? latestApprovedSourceChangedAt,
        IReadOnlyCollection<ProjectStateReportingSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        ValidateScope(tenantId, projectId, cutoffLocalDate, cutoff, sourceState, sourceClassification);

        var normalizedSnapshots = snapshots
            .Select(snapshot => ValidateAndNormalize(snapshot, tenantId, projectId))
            .ToArray();
        if (normalizedSnapshots.Select(snapshot => snapshot.SnapshotId).Distinct().Count() != normalizedSnapshots.Length)
        {
            throw new DomainRuleException(
                "project_state.reporting.snapshot.duplicate",
                "The Project State reporting source contains duplicate snapshot identities.");
        }

        if (sourceState == ProjectStateReportingSourceState.NotConfigured && normalizedSnapshots.Length > 0)
        {
            throw new DomainRuleException(
                "project_state.reporting.source_state.invalid",
                "A disabled Project State reporting source cannot expose snapshots.");
        }

        var candidates = normalizedSnapshots
            .Where(snapshot => snapshot.AsOfDate <= cutoffLocalDate && snapshot.CalculatedAt <= cutoff)
            .ToArray();
        if (candidates.Any(snapshot =>
                !ProjectStateReportingContract.SupportsCalculationVersion(snapshot.CalculationVersion)))
        {
            throw new DomainRuleException(
                "project_state.reporting.calculation_version.unsupported",
                "An eligible Project State snapshot uses an unsupported calculation version.");
        }

        var selected = candidates
            .OrderByDescending(snapshot => snapshot.AsOfDate)
            .ThenByDescending(snapshot => snapshot.CalculatedAt)
            .ThenByDescending(snapshot => snapshot.SnapshotId.ToString("D"), StringComparer.Ordinal)
            .FirstOrDefault();
        var trend = selected is null
            ? []
            : candidates
                .Where(snapshot => snapshot.AsOfDate <= selected.AsOfDate)
                .GroupBy(snapshot => snapshot.AsOfDate)
                .Select(group => group
                    .OrderByDescending(snapshot => snapshot.CalculatedAt)
                    .ThenByDescending(snapshot => snapshot.SnapshotId.ToString("D"), StringComparer.Ordinal)
                    .First())
                .OrderByDescending(snapshot => snapshot.AsOfDate)
                .Take(ProjectStateReportingContract.MaximumTrendDates)
                .OrderBy(snapshot => snapshot.AsOfDate)
                .ThenBy(snapshot => snapshot.CalculatedAt)
                .ThenBy(snapshot => snapshot.SnapshotId.ToString("D"), StringComparer.Ordinal)
                .ToArray();
        var classification = candidates
            .Select(snapshot => snapshot.Classification)
            .Append(sourceClassification)
            .Max();

        return new ProjectStateReportingSelection(
            ProjectStateReportingContract.Version,
            tenantId,
            projectId,
            cutoffLocalDate,
            cutoff,
            sourceState,
            classification,
            latestApprovedSourceChangedAt?.ToUniversalTime(),
            selected,
            trend);
    }

    private static void ValidateScope(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        ProjectStateReportingSourceState sourceState,
        ProjectStateReportingClassification sourceClassification)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || cutoffLocalDate == default ||
            sourceCutoffUtc == default || !Enum.IsDefined(sourceState) ||
            !Enum.IsDefined(sourceClassification))
        {
            throw new DomainRuleException(
                "project_state.reporting.scope.invalid",
                "The Project State reporting selection scope is invalid.");
        }
    }

    private static ProjectStateReportingSnapshot ValidateAndNormalize(
        ProjectStateReportingSnapshot snapshot,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var calculatedAt = snapshot.CalculatedAt.ToUniversalTime();
        var sourceMaxChangedAt = snapshot.SourceMaxChangedAt?.ToUniversalTime();
        if (snapshot.SnapshotId == Guid.Empty || snapshot.TenantId != tenantId || snapshot.ProjectId != projectId ||
            snapshot.AsOfDate == default || calculatedAt == default)
        {
            throw new DomainRuleException(
                "project_state.reporting.snapshot.identity.invalid",
                "A Project State snapshot violates the reporting identity contract.");
        }

        return snapshot with
        {
            CalculatedAt = calculatedAt,
            SourceMaxChangedAt = sourceMaxChangedAt
        };
    }
}
