using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectPeriodicReportSnapshotBuilder
{
    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ProjectControlProfile project,
        DateTimeOffset asOfUtc,
        ProjectPeriodicReportParameters parameters,
        DailyReportReportingPeriod source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(source);
        var normalizedValidationTime = validatedAtUtc.ToUniversalTime();
        if (tenantId == Guid.Empty || project.Id == Guid.Empty || project.TenantId != tenantId ||
            project.Status != ProjectStatus.Active || project.Revision <= 0 || project.ConfigurationVersion <= 0 ||
            !project.ConfigurationChangedAt.HasValue ||
            project.ConfigurationChangedAt.Value.ToUniversalTime() > normalizedValidationTime)
        {
            throw new DomainRuleException(
                "reporting.period.project_scope.invalid",
                "The pinned project does not match the report tenant scope.");
        }

        var period = ProjectPeriodicReportPeriodResolver.Resolve(
            parameters,
            project.TimeZone,
            asOfUtc,
            normalizedValidationTime);
        var roots = ValidateAndCanonicalizeSource(tenantId, project.Id, period, source);
        var configurationReasons = ResolveConfigurationReasons(project);
        var expectedSlots = configurationReasons.Count == 0
            ? BuildExpectedSlots(project, period)
            : null;
        var officialReports = roots
            .Where(root => root.CurrentOfficialReportId.HasValue)
            .Select(root => new ProjectPeriodicOfficialReport(
                root.Classification,
                root.Versions.Single(version => version.ReportId == root.CurrentOfficialReportId!.Value)))
            .OrderBy(report => report.Version.ReportDate)
            .ThenBy(report => report.Version.RootReportId)
            .ToArray();
        var officialReportDates = officialReports
            .Select(report => report.Version.ReportDate)
            .Distinct()
            .OrderBy(date => date)
            .ToArray();
        var coverage = BuildCoverage(expectedSlots, officialReportDates);

        var reasonCodes = new HashSet<ProjectPeriodicReportReasonCode>(configurationReasons);
        if (!period.PeriodClosedAtCutoff)
        {
            reasonCodes.Add(ProjectPeriodicReportReasonCode.PeriodOpenAtCutoff);
        }
        if (roots.Any(root => !root.CurrentOfficialReportId.HasValue))
        {
            reasonCodes.Add(ProjectPeriodicReportReasonCode.OfficialVersionMissing);
        }
        if (officialReports.Any(report => report.Version.Facts.Count == 0))
        {
            reasonCodes.Add(ProjectPeriodicReportReasonCode.OfficialReportEmpty);
        }
        if (coverage.MissingExpectedDates is { Count: > 0 })
        {
            reasonCodes.Add(ProjectPeriodicReportReasonCode.ExpectedSlotMissing);
        }

        var orderedReasons = reasonCodes.OrderBy(reason => reason).ToArray();
        var dataStatus = configurationReasons.Count > 0
            ? ReportDataStatus.NotConfigured
            : officialReports.Length == 0
                ? ReportDataStatus.NoData
                : orderedReasons.Length > 0
                    ? ReportDataStatus.InsufficientData
                    : ReportDataStatus.Available;
        var facts = officialReports
            .SelectMany(report => report.Version.Facts)
            .OrderBy(fact => fact.Kind)
            .ThenBy(fact => fact.FactId)
            .ToArray();
        var manifest = BuildSourceManifest(project, period, roots);
        var sourceManifestJson = CanonicalJson.Serialize(manifest);
        var sourceManifestSha256 = CanonicalJson.Sha256(sourceManifestJson);
        var payload = new ProjectPeriodicReportSemanticSnapshot(
            ProjectPeriodicReportRuntimeContract.SnapshotSchemaVersion,
            ProjectPeriodicReportRuntimeContract.DefinitionCode,
            ProjectPeriodicReportRuntimeContract.DefinitionVersion,
            dataStatus,
            orderedReasons,
            new ProjectPeriodicReportProjectIdentity(
                project.Id,
                project.TenantId,
                project.Code,
                project.Name,
                project.TimeZone,
                project.Revision,
                project.ConfigurationVersion,
                project.ConfigurationChangedAt.Value.ToUniversalTime(),
                project.ReportingFrequency,
                project.DailyReportWorkflow,
                project.DailyCutoffLocalTime,
                project.Calendar.State,
                project.Calendar.WorkingDaysMask),
            new ProjectPeriodicReportPeriodIdentity(
                period.PeriodKind,
                period.PeriodStartLocalDate,
                period.PeriodEndLocalDateExclusive,
                period.PeriodStartUtc,
                period.PeriodEndUtcExclusive,
                period.SourceCutoffUtc,
                period.PeriodClosedAtCutoff),
            coverage,
            officialReports,
            CountFacts(facts),
            SumQuantities(facts),
            SumResourceObservations(facts),
            SelectHighImpactFacts(officialReports),
            sourceManifestSha256);

        return ReportSnapshot.Create(
            Guid.NewGuid(),
            runId,
            tenantId,
            project.Id,
            ProjectPeriodicReportRuntimeContract.SnapshotSchemaVersion,
            dataStatus,
            CanonicalJson.Serialize(payload),
            sourceManifestJson,
            ResolveClassification(roots),
            builtAt,
            period.SourceCutoffUtc);
    }

    private static DailyReportReportingRoot[] ValidateAndCanonicalizeSource(
        Guid tenantId,
        Guid projectId,
        ResolvedProjectReportPeriod period,
        DailyReportReportingPeriod source)
    {
        if (!string.Equals(
                source.ContractVersion,
                DailyReportPeriodReportingContract.Version,
                StringComparison.Ordinal))
        {
            throw new DomainRuleException(
                "reporting.period.source_contract.invalid",
                "The daily-report period source contract version is unsupported.");
        }
        if (source.TenantId != tenantId || source.ProjectId != projectId)
        {
            throw new DomainRuleException(
                "reporting.period.source_scope.invalid",
                "The daily-report period source does not match the report scope.");
        }
        if (source.PeriodStartLocalDate != period.PeriodStartLocalDate ||
            source.PeriodEndLocalDateExclusive != period.PeriodEndLocalDateExclusive ||
            source.AsOfUtc.ToUniversalTime() != period.SourceCutoffUtc)
        {
            throw new DomainRuleException(
                "reporting.period.source_window.invalid",
                "The daily-report source window does not match the resolved report period.");
        }

        var roots = source.Roots
            .OrderBy(root => root.ReportDate)
            .ThenBy(root => root.RootReportId)
            .Select(root => CanonicalizeRoot(root, period))
            .ToArray();
        if (roots.Select(root => root.RootReportId).Distinct().Count() != roots.Length)
        {
            throw new DomainRuleException(
                "reporting.period.source_root.duplicate",
                "The daily-report source contains a duplicate root identity.");
        }
        if (roots.Select(root => root.ReportDate).Distinct().Count() != roots.Length)
        {
            throw new DomainRuleException(
                "reporting.period.source_root_date.duplicate",
                "The daily-report source contains more than one root for a report date.");
        }
        if (roots
            .SelectMany(root => root.Versions)
            .Select(version => version.ReportId)
            .Distinct()
            .Count() != roots.Sum(root => root.Versions.Count))
        {
            throw new DomainRuleException(
                "reporting.period.source_report.duplicate",
                "The daily-report source contains a duplicate report identity.");
        }

        return roots;
    }

    private static DailyReportReportingRoot CanonicalizeRoot(
        DailyReportReportingRoot root,
        ResolvedProjectReportPeriod period)
    {
        if (root.RootReportId == Guid.Empty ||
            root.ReportDate < period.PeriodStartLocalDate ||
            root.ReportDate >= period.PeriodEndLocalDateExclusive ||
            !Enum.IsDefined(root.Classification) ||
            root.CurrentOfficialReportId == Guid.Empty)
        {
            throw new DomainRuleException(
                "reporting.period.source_root.invalid",
                "A daily-report source root is outside the canonical source contract.");
        }

        var versions = root.Versions
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId)
            .Select(version => CanonicalizeVersion(root, version, period.SourceCutoffUtc))
            .ToArray();
        if (versions.Select(version => version.ReportId).Distinct().Count() != versions.Length ||
            versions.Select(version => version.VersionNumber).Distinct().Count() != versions.Length)
        {
            throw new DomainRuleException(
                "reporting.period.source_version.duplicate",
                "A daily-report root contains duplicate official versions.");
        }

        var current = versions
            .Where(version => DailyReportReportingRules.IsOfficialAt(
                version.ApprovedAt,
                version.SupersededAt,
                period.SourceCutoffUtc))
            .ToArray();
        if (current.Length > 1)
        {
            throw new DomainRuleException(
                "reporting.period.source_current.duplicate",
                "A daily-report root contains multiple current official versions.");
        }

        var computedCurrentId = current.SingleOrDefault()?.ReportId;
        if (computedCurrentId != root.CurrentOfficialReportId)
        {
            throw new DomainRuleException(
                "reporting.period.source_current.mismatch",
                "The selected current official daily-report version is inconsistent.");
        }

        return root with { Versions = versions };
    }

    private static DailyReportReportingVersion CanonicalizeVersion(
        DailyReportReportingRoot root,
        DailyReportReportingVersion version,
        DateTimeOffset sourceCutoffUtc)
    {
        if (version.ReportId == Guid.Empty || version.RootReportId != root.RootReportId ||
            version.ReportDate != root.ReportDate || version.VersionNumber <= 0 || version.Revision <= 0 ||
            !Enum.IsDefined(version.State) || version.ApprovedAt.ToUniversalTime() > sourceCutoffUtc ||
            version.CreatedAt.ToUniversalTime() > sourceCutoffUtc ||
            version.LastModifiedAt.ToUniversalTime() > sourceCutoffUtc ||
            version.SupersededAt?.ToUniversalTime() > sourceCutoffUtc ||
            version.SupersedesReportId == Guid.Empty || version.SupersededByReportId == Guid.Empty)
        {
            throw new DomainRuleException(
                "reporting.period.source_version.invalid",
                "An official daily-report version violates the canonical cutoff contract.");
        }

        var facts = version.Facts
            .OrderBy(fact => fact.Kind)
            .ThenBy(fact => fact.FactId)
            .Select(fact => CanonicalizeFact(fact, sourceCutoffUtc))
            .ToArray();
        if (facts.Select(fact => fact.FactId).Distinct().Count() != facts.Length)
        {
            throw new DomainRuleException(
                "reporting.period.source_fact.duplicate",
                "An official daily report contains duplicate fact identities.");
        }

        return version with
        {
            ApprovedAt = version.ApprovedAt.ToUniversalTime(),
            SupersededAt = version.SupersededAt?.ToUniversalTime(),
            CreatedAt = version.CreatedAt.ToUniversalTime(),
            LastModifiedAt = version.LastModifiedAt.ToUniversalTime(),
            Facts = facts
        };
    }

    private static DailyReportReportingFact CanonicalizeFact(
        DailyReportReportingFact fact,
        DateTimeOffset sourceCutoffUtc)
    {
        if (fact.FactId == Guid.Empty || !Enum.IsDefined(fact.Kind) ||
            (fact.ImpactLevel.HasValue && !Enum.IsDefined(fact.ImpactLevel.Value)) ||
            fact.Quantity < 0 || fact.ResourceCount <= 0 || fact.Hours < 0 ||
            fact.CreatedAt.ToUniversalTime() > sourceCutoffUtc ||
            fact.CopiedFromFactId == Guid.Empty || fact.LocationId == Guid.Empty ||
            fact.MeasurementItemId == Guid.Empty)
        {
            throw new DomainRuleException(
                "reporting.period.source_fact.invalid",
                "An official daily-report fact violates the canonical source contract.");
        }

        return fact with { CreatedAt = fact.CreatedAt.ToUniversalTime() };
    }

    private static HashSet<ProjectPeriodicReportReasonCode> ResolveConfigurationReasons(
        ProjectControlProfile project)
    {
        if (!Enum.IsDefined(project.ReportingFrequency) ||
            !Enum.IsDefined(project.DailyReportWorkflow) ||
            !Enum.IsDefined(project.Calendar.State))
        {
            throw new DomainRuleException(
                "reporting.period.project_configuration.invalid",
                "The pinned project reporting configuration is invalid.");
        }

        var reasons = new HashSet<ProjectPeriodicReportReasonCode>();
        if (project.ReportingFrequency == ReportingFrequency.NotConfigured)
        {
            reasons.Add(ProjectPeriodicReportReasonCode.ReportingCadenceMissing);
        }
        if (project.DailyReportWorkflow == DailyReportWorkflow.NotConfigured)
        {
            reasons.Add(ProjectPeriodicReportReasonCode.DailyWorkflowMissing);
        }
        if (!project.DailyCutoffLocalTime.HasValue)
        {
            reasons.Add(ProjectPeriodicReportReasonCode.DailyCutoffMissing);
        }
        if (project.ReportingFrequency == ReportingFrequency.WorkingDays &&
            (project.Calendar.State != ProjectCalendarConfigurationState.Configured ||
                project.Calendar.WorkingDaysMask is null or <= 0 or > 127))
        {
            reasons.Add(ProjectPeriodicReportReasonCode.WorkingCalendarMissing);
        }

        return reasons;
    }

    private static ProjectPeriodicReportCoverage BuildCoverage(
        IReadOnlyCollection<ExpectedCoverageSlot>? expectedSlots,
        IReadOnlyCollection<DateOnly> officialReportDates)
    {
        if (expectedSlots is null)
        {
            return new ProjectPeriodicReportCoverage(
                null,
                null,
                null,
                officialReportDates,
                null,
                null);
        }

        var slots = expectedSlots
            .Select(slot =>
            {
                var coveredDates = officialReportDates
                    .Where(date => date >= slot.StartLocalDate && date < slot.EndLocalDateExclusive)
                    .OrderBy(date => date)
                    .ToArray();
                return new ProjectPeriodicReportCoverageSlot(
                    slot.SlotDate,
                    slot.StartLocalDate,
                    slot.EndLocalDateExclusive,
                    slot.DueAtUtc,
                    coveredDates.Length > 0,
                    coveredDates);
            })
            .ToArray();
        return new ProjectPeriodicReportCoverage(
            slots.Length,
            slots.Count(slot => slot.Covered),
            slots.Select(slot => slot.SlotDate).ToArray(),
            officialReportDates,
            slots.Where(slot => !slot.Covered).Select(slot => slot.SlotDate).ToArray(),
            slots);
    }

    private static ExpectedCoverageSlot[] BuildExpectedSlots(
        ProjectControlProfile project,
        ResolvedProjectReportPeriod period)
    {
        var cutoff = project.DailyCutoffLocalTime
            ?? throw new InvalidOperationException("Configuration validation must run before coverage resolution.");
        var slots = new List<ExpectedCoverageSlot>();
        if (project.ReportingFrequency is ReportingFrequency.Daily or ReportingFrequency.WorkingDays)
        {
            for (var date = period.PeriodStartLocalDate;
                 date < period.PeriodEndLocalDateExclusive;
                 date = date.AddDays(1))
            {
                if (project.ReportingFrequency == ReportingFrequency.WorkingDays &&
                    !project.Calendar.IsWorkingDay(date.DayOfWeek))
                {
                    continue;
                }

                var dueAtUtc = ProjectPeriodicReportPeriodResolver.ResolveLocalInstantUtc(
                    date,
                    cutoff,
                    period.TimeZone);
                if (dueAtUtc <= period.SourceCutoffUtc)
                {
                    slots.Add(new ExpectedCoverageSlot(date, date, date.AddDays(1), dueAtUtc));
                }
            }
        }
        else if (project.ReportingFrequency == ReportingFrequency.Weekly)
        {
            var cursor = period.PeriodStartLocalDate;
            while (cursor < period.PeriodEndLocalDateExclusive)
            {
                var daysSinceSaturday = ((int)cursor.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
                var weekStart = cursor.AddDays(-daysSinceSaturday);
                var weekEndExclusive = weekStart.AddDays(7);
                var slotEndExclusive = weekEndExclusive < period.PeriodEndLocalDateExclusive
                    ? weekEndExclusive
                    : period.PeriodEndLocalDateExclusive;
                var slotDate = slotEndExclusive.AddDays(-1);
                var dueAtUtc = ProjectPeriodicReportPeriodResolver.ResolveLocalInstantUtc(
                    slotDate,
                    cutoff,
                    period.TimeZone);
                if (dueAtUtc <= period.SourceCutoffUtc)
                {
                    slots.Add(new ExpectedCoverageSlot(slotDate, cursor, slotEndExclusive, dueAtUtc));
                }
                cursor = slotEndExclusive;
            }
        }

        return slots.ToArray();
    }

    private static ProjectPeriodicFactCount[] CountFacts(
        IReadOnlyCollection<DailyReportReportingFact> facts) =>
        facts
            .GroupBy(fact => fact.Kind)
            .OrderBy(group => group.Key)
            .Select(group => new ProjectPeriodicFactCount(group.Key, group.Count()))
            .ToArray();

    private static ProjectPeriodicQuantityTotal[] SumQuantities(
        IReadOnlyCollection<DailyReportReportingFact> facts) =>
        facts
            .Where(fact => fact.Quantity.HasValue)
            .GroupBy(fact => new QuantityKey(fact.Kind, fact.Unit), QuantityKeyComparer.Instance)
            .OrderBy(group => group.Key.Kind)
            .ThenBy(group => group.Key.Unit is null ? 0 : 1)
            .ThenBy(group => group.Key.Unit, StringComparer.Ordinal)
            .Select(group => new ProjectPeriodicQuantityTotal(
                group.Key.Kind,
                group.Key.Unit,
                group.Key.Unit is null
                    ? ProjectPeriodicReportUnitState.UnitMissing
                    : ProjectPeriodicReportUnitState.SourceUnit,
                group.Sum(fact => fact.Quantity!.Value)))
            .ToArray();

    private static ProjectPeriodicResourceObservationTotal[] SumResourceObservations(
        IReadOnlyCollection<DailyReportReportingFact> facts) =>
        facts
            .Where(fact => fact.ResourceCount.HasValue || fact.Hours.HasValue)
            .GroupBy(fact => fact.Kind)
            .OrderBy(group => group.Key)
            .Select(group => new ProjectPeriodicResourceObservationTotal(
                group.Key,
                group.Any(fact => fact.ResourceCount.HasValue)
                    ? group.Where(fact => fact.ResourceCount.HasValue).Sum(fact => fact.ResourceCount!.Value)
                    : null,
                group.Any(fact => fact.Hours.HasValue)
                    ? group.Where(fact => fact.Hours.HasValue).Sum(fact => fact.Hours!.Value)
                    : null))
            .ToArray();

    private static ProjectPeriodicHighImpactFact[] SelectHighImpactFacts(
        IReadOnlyCollection<ProjectPeriodicOfficialReport> reports) =>
        reports
            .SelectMany(report => report.Version.Facts
                .Where(fact =>
                    fact.Kind is DailyReportReportingFactKind.Issue or DailyReportReportingFactKind.Stoppage &&
                    fact.ImpactLevel is DailyReportReportingImpactLevel.High or DailyReportReportingImpactLevel.Critical)
                .Select(fact => new ProjectPeriodicHighImpactFact(
                    report.Version.ReportDate,
                    report.Version.RootReportId,
                    report.Version.ReportId,
                    fact)))
            .OrderBy(item => item.ReportDate)
            .ThenBy(item => item.RootReportId)
            .ThenBy(item => item.Fact.Kind)
            .ThenBy(item => item.Fact.FactId)
            .ToArray();

    private static ProjectPeriodicSourceManifest BuildSourceManifest(
        ProjectControlProfile project,
        ResolvedProjectReportPeriod period,
        IReadOnlyCollection<DailyReportReportingRoot> roots) =>
        new(
            DailyReportPeriodReportingContract.Version,
            project.TenantId,
            project.Id,
            project.Revision,
            project.ConfigurationVersion,
            project.ConfigurationChangedAt!.Value.ToUniversalTime(),
            period.PeriodStartLocalDate,
            period.PeriodEndLocalDateExclusive,
            period.SourceCutoffUtc,
            roots.Select(root => new ProjectPeriodicSourceRootManifest(
                root.RootReportId,
                root.ReportDate,
                root.CurrentOfficialReportId,
                root.Classification,
                root.Versions.Select(version => new ProjectPeriodicSourceVersionManifest(
                    version.ReportId,
                    version.VersionNumber,
                    version.Revision,
                    version.ApprovedAt,
                    version.SupersededAt,
                    version.LastModifiedAt,
                    version.Facts.Select(fact => fact.FactId).ToArray(),
                    CanonicalJson.Sha256(CanonicalJson.Serialize(version))))
                    .ToArray()))
                .ToArray());

    private static ReportClassification ResolveClassification(
        IReadOnlyCollection<DailyReportReportingRoot> roots)
    {
        var classification = roots
            .Select(root => root.Classification switch
            {
                DailyReportReportingClassification.Internal => ReportClassification.Internal,
                DailyReportReportingClassification.Confidential => ReportClassification.Confidential,
                DailyReportReportingClassification.Restricted => ReportClassification.Restricted,
                _ => throw new DomainRuleException(
                    "reporting.period.source_classification.invalid",
                    "The daily-report source classification is invalid.")
            })
            .DefaultIfEmpty(ReportClassification.Internal)
            .Max();
        return classification < ReportClassification.Internal
            ? ReportClassification.Internal
            : classification;
    }

    private sealed record ExpectedCoverageSlot(
        DateOnly SlotDate,
        DateOnly StartLocalDate,
        DateOnly EndLocalDateExclusive,
        DateTimeOffset DueAtUtc);

    private readonly record struct QuantityKey(
        DailyReportReportingFactKind Kind,
        string? Unit);

    private sealed class QuantityKeyComparer : IEqualityComparer<QuantityKey>
    {
        public static QuantityKeyComparer Instance { get; } = new();

        public bool Equals(QuantityKey x, QuantityKey y) =>
            x.Kind == y.Kind && StringComparer.Ordinal.Equals(x.Unit, y.Unit);

        public int GetHashCode(QuantityKey value) => HashCode.Combine(
            value.Kind,
            value.Unit is null ? 0 : StringComparer.Ordinal.GetHashCode(value.Unit));
    }

    private sealed record ProjectPeriodicSourceManifest(
        string ContractVersion,
        Guid TenantId,
        Guid ProjectId,
        long ProjectRevision,
        long ConfigurationVersion,
        DateTimeOffset ConfigurationChangedAt,
        DateOnly PeriodStartLocalDate,
        DateOnly PeriodEndLocalDateExclusive,
        DateTimeOffset SourceCutoffUtc,
        IReadOnlyCollection<ProjectPeriodicSourceRootManifest> Roots);

    private sealed record ProjectPeriodicSourceRootManifest(
        Guid RootReportId,
        DateOnly ReportDate,
        Guid? CurrentOfficialReportId,
        DailyReportReportingClassification Classification,
        IReadOnlyCollection<ProjectPeriodicSourceVersionManifest> Versions);

    private sealed record ProjectPeriodicSourceVersionManifest(
        Guid ReportId,
        int VersionNumber,
        long Revision,
        DateTimeOffset ApprovedAt,
        DateTimeOffset? SupersededAt,
        DateTimeOffset LastModifiedAt,
        IReadOnlyCollection<Guid> FactIds,
        string ContentSha256);
}
