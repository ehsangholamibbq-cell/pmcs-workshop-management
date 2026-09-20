using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ExecutiveProjectStateReportSnapshotBuilder
{
    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ProjectControlProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectStateReportingSelection source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt) => Build(
        runId,
        tenantId,
        ExecutiveProjectStatePinnedProjectProfile.Capture(project),
        sourceCutoffUtc,
        source,
        validatedAtUtc,
        builtAt);

    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ExecutiveProjectStatePinnedProjectProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectStateReportingSelection source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var validatedAt = validatedAtUtc.ToUniversalTime();
        var timeZone = project.ValidateForRun(tenantId, project.Id, cutoff, validatedAt);
        var cutoffLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, timeZone).DateTime);
        var canonical = ValidateAndCanonicalizeSource(tenantId, project.Id, cutoffLocalDate, cutoff, source);
        var selected = canonical.SelectedSnapshot;
        var reasons = ResolveReasons(project, canonical);
        var dataStatus = ResolveDataStatus(canonical.SourceState, selected, reasons);
        var sourceManifest = BuildSourceManifest(canonical);
        var sourceManifestJson = CanonicalJson.Serialize(sourceManifest);
        var sourceManifestSha256 = CanonicalJson.Sha256(sourceManifestJson);
        var projectConfigurationCurrent = selected is null
            ? (bool?)null
            : selected.ProjectConfigurationRevision == project.Revision;
        var approvedSourceCurrent = selected is null
            ? (bool?)null
            : IsApprovedSourceCurrent(selected, canonical.LatestApprovedSourceChangedAt);
        var payload = new ExecutiveProjectStateReportSemanticSnapshot(
            ExecutiveProjectStateReportRuntimeContract.SnapshotSchemaVersion,
            ExecutiveProjectStateReportRuntimeContract.DefinitionCode,
            ExecutiveProjectStateReportRuntimeContract.DefinitionVersion,
            dataStatus,
            reasons,
            new ExecutiveProjectStateReportParameters(),
            new ExecutiveProjectStateProjectIdentity(
                project.Id,
                project.TenantId,
                project.Code,
                project.Name,
                project.TimeZone,
                project.BaseCurrencyCode,
                project.Revision,
                project.ConfigurationVersion,
                project.ConfigurationChangedAt.ToUniversalTime()),
            new ExecutiveProjectStateCutoffIdentity(cutoff, cutoffLocalDate),
            canonical.SourceState,
            selected is null ? null : MapSnapshot(selected),
            new ExecutiveProjectStateCurrency(
                projectConfigurationCurrent,
                approvedSourceCurrent,
                canonical.LatestApprovedSourceChangedAt),
            canonical.Trend.Select(MapTrend).ToArray(),
            sourceManifestSha256);

        return ReportSnapshot.Create(
            Guid.NewGuid(),
            runId,
            tenantId,
            project.Id,
            ExecutiveProjectStateReportRuntimeContract.SnapshotSchemaVersion,
            dataStatus,
            CanonicalJson.Serialize(payload),
            sourceManifestJson,
            MapClassification(canonical.Classification),
            builtAt,
            cutoff);
    }

    private static ProjectStateReportingSelection ValidateAndCanonicalizeSource(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        ProjectStateReportingSelection source)
    {
        if (!string.Equals(source.ContractVersion, ProjectStateReportingContract.Version, StringComparison.Ordinal) ||
            source.TenantId != tenantId || source.ProjectId != projectId ||
            source.CutoffLocalDate != cutoffLocalDate ||
            source.SourceCutoffUtc.ToUniversalTime() != sourceCutoffUtc ||
            !Enum.IsDefined(source.SourceState) || !Enum.IsDefined(source.Classification) ||
            source.LatestApprovedSourceChangedAt?.ToUniversalTime() > sourceCutoffUtc ||
            source.Trend is null)
        {
            throw new DomainRuleException(
                "reporting.executive_state.source.invalid",
                "The Project State reporting source violates its versioned scope contract.");
        }

        if (source.SourceState == ProjectStateReportingSourceState.NotConfigured)
        {
            if (source.SelectedSnapshot is not null || source.Trend.Count > 0 ||
                source.LatestApprovedSourceChangedAt.HasValue)
            {
                throw new DomainRuleException(
                    "reporting.executive_state.source.not_configured_invalid",
                    "A disabled Project State reporting source cannot expose official data.");
            }

            return source with
            {
                SourceCutoffUtc = sourceCutoffUtc,
                LatestApprovedSourceChangedAt = null,
                Trend = []
            };
        }

        if (source.SelectedSnapshot is null)
        {
            if (source.Trend.Count > 0)
            {
                throw new DomainRuleException(
                    "reporting.executive_state.source.selection_missing",
                    "Project State trend data cannot exist without an official selected snapshot.");
            }

            return source with
            {
                SourceCutoffUtc = sourceCutoffUtc,
                LatestApprovedSourceChangedAt = source.LatestApprovedSourceChangedAt?.ToUniversalTime(),
                Trend = []
            };
        }

        var selected = ValidateSnapshot(
            source.SelectedSnapshot,
            tenantId,
            projectId,
            cutoffLocalDate,
            sourceCutoffUtc);
        if (source.Trend.Count > ProjectStateReportingContract.MaximumTrendDates)
        {
            throw new DomainRuleException(
                "reporting.executive_state.trend.limit_exceeded",
                "Project State trend exceeds the versioned reporting limit.");
        }

        var trend = source.Trend
            .Select(snapshot => ValidateSnapshot(snapshot, tenantId, projectId, cutoffLocalDate, sourceCutoffUtc))
            .OrderBy(snapshot => snapshot.AsOfDate)
            .ThenBy(snapshot => snapshot.CalculatedAt)
            .ThenBy(snapshot => snapshot.SnapshotId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (trend.Select(snapshot => snapshot.SnapshotId).Distinct().Count() != trend.Length ||
            trend.Select(snapshot => snapshot.AsOfDate).Distinct().Count() != trend.Length ||
            trend.Any(snapshot => snapshot.AsOfDate > selected.AsOfDate) ||
            trend.Length == 0 || trend[^1].SnapshotId != selected.SnapshotId ||
            !string.Equals(
                SnapshotContentSha256(trend[^1]),
                SnapshotContentSha256(selected),
                StringComparison.Ordinal))
        {
            throw new DomainRuleException(
                "reporting.executive_state.trend.invalid",
                "Project State trend is not a canonical sequence ending at the selected snapshot.");
        }

        var highestClassification = trend
            .Select(snapshot => snapshot.Classification)
            .Append(selected.Classification)
            .Max();
        if (source.Classification < highestClassification)
        {
            throw new DomainRuleException(
                "reporting.executive_state.classification.downgrade",
                "Project State reporting classification cannot be lower than its source snapshots.");
        }

        return source with
        {
            SourceCutoffUtc = sourceCutoffUtc,
            LatestApprovedSourceChangedAt = source.LatestApprovedSourceChangedAt?.ToUniversalTime(),
            SelectedSnapshot = selected,
            Trend = trend
        };
    }

    private static ProjectStateReportingSnapshot ValidateSnapshot(
        ProjectStateReportingSnapshot snapshot,
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var calculatedAt = snapshot.CalculatedAt.ToUniversalTime();
        var sourceMaxChangedAt = snapshot.SourceMaxChangedAt?.ToUniversalTime();
        if (snapshot.SnapshotId == Guid.Empty || snapshot.TenantId != tenantId || snapshot.ProjectId != projectId ||
            string.IsNullOrWhiteSpace(snapshot.ProjectCode) || string.IsNullOrWhiteSpace(snapshot.ProjectName) ||
            !Enum.IsDefined(snapshot.Classification) ||
            !ProjectStateReportingContract.SupportsCalculationVersion(snapshot.CalculationVersion) ||
            snapshot.ProjectConfigurationRevision <= 0 || snapshot.AsOfDate == default ||
            snapshot.AsOfDate > cutoffLocalDate || calculatedAt == default || calculatedAt > sourceCutoffUtc ||
            snapshot.WindowStart == default || snapshot.WindowEnd == default ||
            snapshot.WindowStart > snapshot.WindowEnd || snapshot.WindowEnd > snapshot.AsOfDate ||
            !Enum.IsDefined(snapshot.AssessmentScope) || !Enum.IsDefined(snapshot.OperationalStatus) ||
            !Enum.IsDefined(snapshot.CoverageStatus) || !Enum.IsDefined(snapshot.FreshnessStatus) ||
            !Enum.IsDefined(snapshot.ConfidenceStatus) || !Enum.IsDefined(snapshot.CoverageBasis) ||
            !Enum.IsDefined(snapshot.ContractState) || !Enum.IsDefined(snapshot.PlanningState) ||
            !Enum.IsDefined(snapshot.BudgetState) || !Enum.IsDefined(snapshot.QualityState) ||
            !Enum.IsDefined(snapshot.HseState) || snapshot.CoveragePercent is < 0 or > 100 ||
            snapshot.ExpectedReportDays < 0 || snapshot.ApprovedReportDays < 0 ||
            snapshot.ApprovedReportDays > snapshot.ExpectedReportDays || snapshot.ApprovedFactCount < 0 ||
            snapshot.ProgressFactCount < 0 || snapshot.LaborFactCount < 0 || snapshot.EquipmentFactCount < 0 ||
            snapshot.MaterialFactCount < 0 || snapshot.IssueCount < 0 || snapshot.StoppageCount < 0 ||
            snapshot.HighImpactCount < 0 || snapshot.CriticalImpactCount < 0 ||
            snapshot.HighImpactCount + snapshot.CriticalImpactCount > snapshot.IssueCount + snapshot.StoppageCount ||
            snapshot.OldestAttentionAgeDays < 0 || sourceMaxChangedAt > calculatedAt ||
            snapshot.AttentionItems is null)
        {
            throw new DomainRuleException(
                "reporting.executive_state.snapshot.invalid",
                "An official Project State snapshot violates the semantic source contract.");
        }

        var attention = snapshot.AttentionItems
            .Select(item => ValidateAttention(item, snapshot.AsOfDate))
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => item.AgeDays)
            .ThenBy(item => item.ReportDate)
            .ThenBy(item => item.SourceReportId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(item => item.SourceFactId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var issueCount = attention.Count(item => item.Kind == ProjectAttentionKind.Issue);
        var stoppageCount = attention.Count(item => item.Kind == ProjectAttentionKind.Stoppage);
        var highCount = attention.Count(item => item.Priority == ProjectAttentionPriority.High);
        var criticalCount = attention.Count(item => item.Priority == ProjectAttentionPriority.Critical);
        var oldestAgeDays = attention.Length == 0
            ? (int?)null
            : attention.Max(item => item.AgeDays);
        if (attention.Select(item => item.SourceFactId).Distinct().Count() != attention.Length ||
            issueCount != snapshot.IssueCount || stoppageCount != snapshot.StoppageCount ||
            highCount != snapshot.HighImpactCount || criticalCount != snapshot.CriticalImpactCount ||
            oldestAgeDays != snapshot.OldestAttentionAgeDays)
        {
            throw new DomainRuleException(
                "reporting.executive_state.attention.lineage_invalid",
                "Project State attention lineage is duplicate or incomplete.");
        }

        return snapshot with
        {
            CalculatedAt = calculatedAt,
            SourceMaxChangedAt = sourceMaxChangedAt,
            AttentionItems = attention
        };
    }

    private static ProjectStateReportingAttentionItem ValidateAttention(
        ProjectStateReportingAttentionItem item,
        DateOnly snapshotAsOfDate)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.SourceReportId == Guid.Empty || item.SourceFactId == Guid.Empty || item.ReportDate == default ||
            item.ReportDate > snapshotAsOfDate || !Enum.IsDefined(item.Kind) ||
            string.IsNullOrWhiteSpace(item.Description) || item.LocationId == Guid.Empty ||
            (item.ObservedImpact.HasValue && !Enum.IsDefined(item.ObservedImpact.Value)) ||
            !Enum.IsDefined(item.Priority) || item.AgeDays < 0 || !Enum.IsDefined(item.AgeBand) ||
            !Enum.IsDefined(item.Status) ||
            item.AgeDays != Math.Max(0, snapshotAsOfDate.DayNumber - item.ReportDate.DayNumber))
        {
            throw new DomainRuleException(
                "reporting.executive_state.attention.invalid",
                "A Project State attention item violates immutable reporting lineage.");
        }

        return item;
    }

    private static ExecutiveProjectStateReportReasonCode[] ResolveReasons(
        ExecutiveProjectStatePinnedProjectProfile project,
        ProjectStateReportingSelection source)
    {
        var reasons = new HashSet<ExecutiveProjectStateReportReasonCode>();
        if (source.SourceState == ProjectStateReportingSourceState.NotConfigured)
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.ProjectStateReportingNotConfigured);
            return OrderReasons(reasons);
        }

        var snapshot = source.SelectedSnapshot;
        if (snapshot is null)
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.OfficialSnapshotMissing);
            return OrderReasons(reasons);
        }

        if (IsNoData(snapshot))
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.OfficialSnapshotNoData);
            return OrderReasons(reasons);
        }

        if (snapshot.OperationalStatus == ProjectOperationalStatus.InsufficientData)
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.OfficialSnapshotInsufficient);
        }
        if (snapshot.CoverageStatus == DataCoverageStatus.Insufficient)
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.CoverageInsufficient);
        }
        if (snapshot.FreshnessStatus == DataFreshnessStatus.Stale)
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.FreshnessStale);
        }
        if (snapshot.ConfidenceStatus == DataConfidenceStatus.Low)
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.ConfidenceLow);
        }
        if (snapshot.ProjectConfigurationRevision != project.Revision)
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.ProjectConfigurationRevisionOutdated);
        }
        if (!IsApprovedSourceCurrent(snapshot, source.LatestApprovedSourceChangedAt))
        {
            reasons.Add(ExecutiveProjectStateReportReasonCode.ApprovedSourceChangedAfterSnapshot);
        }

        return OrderReasons(reasons);
    }

    private static ReportDataStatus ResolveDataStatus(
        ProjectStateReportingSourceState sourceState,
        ProjectStateReportingSnapshot? snapshot,
        ExecutiveProjectStateReportReasonCode[] reasons)
    {
        if (sourceState == ProjectStateReportingSourceState.NotConfigured)
        {
            return ReportDataStatus.NotConfigured;
        }
        if (snapshot is null || IsNoData(snapshot))
        {
            return ReportDataStatus.NoData;
        }
        return reasons.Length > 0
            ? ReportDataStatus.InsufficientData
            : ReportDataStatus.Available;
    }

    private static bool IsNoData(ProjectStateReportingSnapshot snapshot) =>
        snapshot.OperationalStatus == ProjectOperationalStatus.NoData ||
        snapshot.CoverageStatus == DataCoverageStatus.NoData ||
        snapshot.ConfidenceStatus == DataConfidenceStatus.NoData;

    private static bool IsApprovedSourceCurrent(
        ProjectStateReportingSnapshot snapshot,
        DateTimeOffset? latestApprovedSourceChangedAt)
    {
        if (!latestApprovedSourceChangedAt.HasValue)
        {
            return !snapshot.SourceMaxChangedAt.HasValue;
        }

        return snapshot.SourceMaxChangedAt.HasValue &&
            latestApprovedSourceChangedAt.Value.ToUniversalTime() <=
            snapshot.SourceMaxChangedAt.Value.ToUniversalTime();
    }

    private static ExecutiveProjectStateReportReasonCode[] OrderReasons(
        IEnumerable<ExecutiveProjectStateReportReasonCode> reasons) =>
        reasons
            .Distinct()
            .OrderBy(reason => reason.ToString(), StringComparer.Ordinal)
            .ToArray();

    private static ExecutiveProjectStateSnapshot MapSnapshot(ProjectStateReportingSnapshot snapshot) => new(
        snapshot.SnapshotId,
        snapshot.ProjectCode,
        snapshot.ProjectName,
        snapshot.Classification,
        snapshot.CalculationVersion,
        snapshot.ProjectConfigurationRevision,
        snapshot.AsOfDate,
        snapshot.CalculatedAt,
        snapshot.WindowStart,
        snapshot.WindowEnd,
        snapshot.AssessmentScope,
        snapshot.IsPartial,
        snapshot.OperationalStatus,
        snapshot.CoverageStatus,
        snapshot.FreshnessStatus,
        snapshot.ConfidenceStatus,
        snapshot.CoverageBasis,
        snapshot.CoveragePercent,
        snapshot.ExpectedReportDays,
        snapshot.ApprovedReportDays,
        snapshot.LastApprovedReportDate,
        new ExecutiveProjectStateFactCounts(
            snapshot.ApprovedFactCount,
            snapshot.ProgressFactCount,
            snapshot.LaborFactCount,
            snapshot.EquipmentFactCount,
            snapshot.MaterialFactCount),
        new ExecutiveProjectStateAttentionSummary(
            snapshot.IssueCount,
            snapshot.StoppageCount,
            snapshot.HighImpactCount,
            snapshot.CriticalImpactCount,
            snapshot.OldestAttentionAgeDays),
        new ExecutiveProjectStateFeatureStates(
            snapshot.ContractState,
            snapshot.PlanningState,
            snapshot.BudgetState,
            snapshot.QualityState,
            snapshot.HseState),
        snapshot.SourceMaxChangedAt,
        snapshot.AttentionItems.Select(item => new ExecutiveProjectStateAttentionItem(
            item.SourceReportId,
            item.SourceFactId,
            item.ReportDate,
            item.Kind,
            item.Description,
            item.Category,
            item.LocationId,
            item.LocationName,
            item.ObservedImpact,
            item.Priority,
            item.AgeDays,
            item.AgeBand,
            item.Status,
            item.ReferenceCode)).ToArray());

    private static ExecutiveProjectStateTrendPoint MapTrend(ProjectStateReportingSnapshot snapshot) => new(
        snapshot.SnapshotId,
        snapshot.CalculationVersion,
        snapshot.AsOfDate,
        snapshot.CalculatedAt,
        snapshot.OperationalStatus,
        snapshot.CoverageStatus,
        snapshot.FreshnessStatus,
        snapshot.ConfidenceStatus,
        snapshot.CoveragePercent,
        snapshot.IssueCount,
        snapshot.StoppageCount,
        snapshot.HighImpactCount,
        snapshot.CriticalImpactCount);

    private static ExecutiveProjectStateSourceManifest BuildSourceManifest(
        ProjectStateReportingSelection source)
    {
        var snapshots = source.Trend
            .OrderBy(snapshot => snapshot.AsOfDate)
            .ThenBy(snapshot => snapshot.CalculatedAt)
            .ThenBy(snapshot => snapshot.SnapshotId.ToString("D"), StringComparer.Ordinal)
            .Select(snapshot =>
            {
                var semantic = MapSnapshot(snapshot);
                return new ExecutiveProjectStateSourceManifestEntry(
                    snapshot.SnapshotId,
                    snapshot.CalculationVersion,
                    snapshot.ProjectConfigurationRevision,
                    snapshot.AsOfDate,
                    snapshot.CalculatedAt,
                    snapshot.SourceMaxChangedAt,
                    snapshot.Classification,
                    SnapshotContentSha256(semantic));
            })
            .ToArray();
        return new ExecutiveProjectStateSourceManifest(
            source.ContractVersion,
            source.TenantId,
            source.ProjectId,
            source.CutoffLocalDate,
            source.SourceCutoffUtc,
            source.SourceState,
            source.Classification,
            source.LatestApprovedSourceChangedAt,
            source.SelectedSnapshot?.SnapshotId,
            snapshots);
    }

    private static string SnapshotContentSha256(ProjectStateReportingSnapshot snapshot) =>
        SnapshotContentSha256(MapSnapshot(snapshot));

    private static string SnapshotContentSha256(ExecutiveProjectStateSnapshot snapshot) =>
        CanonicalJson.Sha256(CanonicalJson.Serialize(snapshot));

    private static ReportClassification MapClassification(
        ProjectStateReportingClassification classification) => classification switch
    {
        ProjectStateReportingClassification.Internal => ReportClassification.Internal,
        ProjectStateReportingClassification.Confidential => ReportClassification.Confidential,
        ProjectStateReportingClassification.Restricted => ReportClassification.Restricted,
        _ => throw new DomainRuleException(
            "reporting.executive_state.classification.invalid",
            "The Project State reporting classification is invalid.")
    };

    private static int PriorityRank(ProjectAttentionPriority priority) => priority switch
    {
        ProjectAttentionPriority.Critical => 5,
        ProjectAttentionPriority.High => 4,
        ProjectAttentionPriority.Medium => 3,
        ProjectAttentionPriority.Low => 2,
        ProjectAttentionPriority.Unassessed => 1,
        _ => throw new DomainRuleException(
            "reporting.executive_state.attention.priority_invalid",
            "The Project State attention priority is invalid.")
    };

    private sealed record ExecutiveProjectStateSourceManifest(
        string ContractVersion,
        Guid TenantId,
        Guid ProjectId,
        DateOnly CutoffLocalDate,
        DateTimeOffset SourceCutoffUtc,
        ProjectStateReportingSourceState SourceState,
        ProjectStateReportingClassification Classification,
        DateTimeOffset? LatestApprovedSourceChangedAt,
        Guid? SelectedSnapshotId,
        IReadOnlyCollection<ExecutiveProjectStateSourceManifestEntry> Snapshots);

    private sealed record ExecutiveProjectStateSourceManifestEntry(
        Guid SnapshotId,
        string CalculationVersion,
        long ProjectConfigurationRevision,
        DateOnly AsOfDate,
        DateTimeOffset CalculatedAt,
        DateTimeOffset? SourceMaxChangedAt,
        ProjectStateReportingClassification Classification,
        string ContentSha256);
}
