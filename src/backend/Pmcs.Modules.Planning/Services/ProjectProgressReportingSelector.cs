using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;

namespace Pmcs.Modules.Planning.Services;

internal static class ProjectProgressReportingSelector
{
    public static ProjectProgressReportingSelection Select(ProjectProgressReportingProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var cutoff = projection.SourceCutoffUtc.ToUniversalTime();
        ValidateScope(projection, cutoff);

        var configurations = projection.Configurations
            .Select(configuration => ValidateAndNormalize(configuration))
            .OrderBy(configuration => configuration.EffectiveFromUtc)
            .ThenBy(configuration => configuration.ConfigurationVersion)
            .ToArray();
        if (configurations.Select(configuration => configuration.ConfigurationVersion).Distinct().Count() !=
            configurations.Length)
        {
            throw Invalid("configuration.duplicate", "Progress reporting contains duplicate configuration versions.");
        }

        var effectiveConfigurations = configurations
            .Where(configuration => IsEffectiveAt(
                configuration.EffectiveFromUtc,
                configuration.EffectiveToUtc,
                cutoff))
            .ToArray();
        if (effectiveConfigurations.Length > 1)
        {
            throw Invalid("configuration.overlap", "More than one planning configuration is effective at the cutoff.");
        }

        var baselines = projection.Baselines
            .Where(baseline => baseline.ApprovedAt.ToUniversalTime() <= cutoff)
            .Select(ValidateAndNormalize)
            .OrderBy(baseline => baseline.ApprovedAt)
            .ThenBy(baseline => baseline.BaselineId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (baselines.Select(baseline => baseline.BaselineId).Distinct().Count() != baselines.Length ||
            baselines.Select(baseline => baseline.VersionCode).Distinct(StringComparer.Ordinal).Count() != baselines.Length)
        {
            throw Invalid("baseline.duplicate", "Progress reporting contains duplicate baseline identity or version.");
        }

        var effectiveBaselines = baselines
            .Where(baseline => IsEffectiveAt(baseline.ApprovedAt, baseline.SupersededAt, cutoff))
            .ToArray();
        if (effectiveBaselines.Length > 1)
        {
            throw Invalid("baseline.overlap", "More than one official planning baseline is effective at the cutoff.");
        }

        var milestoneUpdates = projection.MilestoneUpdates
            .Where(update => update.ApprovedAt.ToUniversalTime() <= cutoff)
            .Select(ValidateAndNormalize)
            .OrderBy(update => update.BaselineId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(update => update.BaselineEntryId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(update => update.StatusDate)
            .ThenBy(update => update.ApprovedAt)
            .ThenBy(update => update.UpdateId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (milestoneUpdates.Select(update => update.UpdateId).Distinct().Count() != milestoneUpdates.Length)
        {
            throw Invalid("milestone.duplicate", "Progress reporting contains duplicate milestone update identities.");
        }

        ValidateMilestoneLineage(milestoneUpdates, baselines);
        var evidence = ValidateAndCanonicalizeEvidence(projection, cutoff);
        var selectedConfiguration = effectiveConfigurations.SingleOrDefault();
        var selectedBaseline = effectiveBaselines.SingleOrDefault();
        var classification = ResolveClassification(
            selectedConfiguration,
            baselines,
            milestoneUpdates,
            evidence);
        var sourceMaxChangedAt = ResolveSourceMaxChangedAt(
            selectedConfiguration,
            baselines,
            milestoneUpdates,
            evidence,
            cutoff);
        var manifest = BuildManifest(
            projection,
            cutoff,
            selectedConfiguration,
            baselines,
            milestoneUpdates,
            evidence);

        return new ProjectProgressReportingSelection(
            ProjectProgressReportingContract.Version,
            projection.TenantId,
            projection.ProjectId,
            projection.CutoffLocalDate,
            cutoff,
            selectedConfiguration,
            selectedBaseline,
            baselines,
            milestoneUpdates,
            evidence,
            classification,
            sourceMaxChangedAt,
            manifest,
            ProjectProgressCanonicalJson.Sha256(ProjectProgressCanonicalJson.Serialize(manifest)));
    }

    private static void ValidateScope(ProjectProgressReportingProjection projection, DateTimeOffset cutoff)
    {
        if (!string.Equals(
                projection.ContractVersion,
                ProjectProgressReportingContract.Version,
                StringComparison.Ordinal) ||
            projection.TenantId == Guid.Empty || projection.ProjectId == Guid.Empty ||
            projection.CutoffLocalDate == default || cutoff == default ||
            projection.Configurations is null || projection.Baselines is null ||
            projection.MilestoneUpdates is null || projection.Evidence is null)
        {
            throw Invalid("scope.invalid", "The project progress reporting projection violates its versioned scope.");
        }
    }

    private static ProjectProgressConfigurationVersion ValidateAndNormalize(
        ProjectProgressConfigurationVersion configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var effectiveFrom = configuration.EffectiveFromUtc.ToUniversalTime();
        var effectiveTo = configuration.EffectiveToUtc?.ToUniversalTime();
        if (configuration.ConfigurationVersion <= 0 || configuration.ProjectRevision <= 0 ||
            configuration.CalendarRevision <= 0 || !Enum.IsDefined(configuration.PlanningMode) ||
            !Enum.IsDefined(configuration.CalendarState) || !Enum.IsDefined(configuration.Classification) ||
            effectiveFrom == default || effectiveTo <= effectiveFrom ||
            (configuration.CalendarState == ProjectProgressCalendarState.NotConfigured &&
                configuration.WorkingDaysMask.HasValue) ||
            (configuration.CalendarState == ProjectProgressCalendarState.WorkingWeek &&
                configuration.WorkingDaysMask is not (>= 1 and <= 127)))
        {
            throw Invalid(
                "configuration.invalid",
                "A planning configuration version violates the progress reporting contract.");
        }

        return configuration with { EffectiveFromUtc = effectiveFrom, EffectiveToUtc = effectiveTo };
    }

    private static ProjectProgressBaselineVersion ValidateAndNormalize(ProjectProgressBaselineVersion baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        var approvedAt = baseline.ApprovedAt.ToUniversalTime();
        var supersededAt = baseline.SupersededAt?.ToUniversalTime();
        if (baseline.BaselineId == Guid.Empty || string.IsNullOrWhiteSpace(baseline.VersionCode) ||
            baseline.VersionCode.Trim().Length > 80 || string.IsNullOrWhiteSpace(baseline.Title) ||
            baseline.Title.Trim().Length > 240 || baseline.SourceSystem?.Trim().Length > 120 ||
            baseline.SourceReference?.Trim().Length > 500 || !Enum.IsDefined(baseline.Kind) ||
            baseline.ApprovalRevision <= 0 || approvedAt == default || supersededAt <= approvedAt ||
            !Enum.IsDefined(baseline.Classification) || baseline.Entries is null ||
            baseline.Entries.Count is < 1 or > ProjectProgressReportingContract.MaximumBaselineEntries)
        {
            throw Invalid("baseline.invalid", "An official planning baseline violates the reporting contract.");
        }

        var entries = baseline.Entries
            .Select(entry => ValidateAndNormalize(entry, baseline.Kind, approvedAt))
            .OrderBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Code, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (entries.Select(entry => entry.EntryId).Distinct().Count() != entries.Length ||
            entries.Select(entry => entry.Code).Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            throw Invalid("baseline.entry_duplicate", "A baseline contains duplicate entry identity or code.");
        }

        ValidateHierarchy(entries);
        var weighted = entries.Where(entry => entry.Kind != PlanningEntryKind.Summary).ToArray();
        if (weighted.Length == 0 || weighted.Any(entry => !entry.WeightPercent.HasValue) ||
            weighted.Sum(entry => entry.WeightPercent!.Value) != 100.0000m)
        {
            throw Invalid(
                "baseline.weights.invalid",
                "Every non-summary baseline entry requires a weight and the total must equal 100.0000.");
        }

        var measurementIds = weighted
            .Where(entry => entry.MeasurementItemId.HasValue)
            .Select(entry => entry.MeasurementItemId!.Value)
            .ToArray();
        if (measurementIds.Distinct().Count() != measurementIds.Length)
        {
            throw Invalid(
                "baseline.measurement_mapping.duplicate",
                "A measurement item can map to only one weighted baseline entry.");
        }

        return baseline with
        {
            VersionCode = baseline.VersionCode.Trim().ToUpperInvariant(),
            Title = baseline.Title.Trim(),
            SourceSystem = Optional(baseline.SourceSystem),
            SourceReference = Optional(baseline.SourceReference),
            ApprovedAt = approvedAt,
            SupersededAt = supersededAt,
            Entries = entries
        };
    }

    private static ProjectProgressBaselineEntry ValidateAndNormalize(
        ProjectProgressBaselineEntry entry,
        PlanningBaselineKind baselineKind,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.EntryId == Guid.Empty || entry.ParentEntryId == Guid.Empty ||
            string.IsNullOrWhiteSpace(entry.Code) || string.IsNullOrWhiteSpace(entry.Title) ||
            entry.Code.Trim().Length > 80 || entry.Title.Trim().Length > 240 ||
            !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.MeasurementMethod) || entry.SortOrder <= 0)
        {
            throw Invalid("baseline.entry.invalid", "A baseline entry has invalid identity or metadata.");
        }

        if (entry.Kind == PlanningEntryKind.Summary)
        {
            if (entry.WeightPercent.HasValue || entry.MeasurementItemId.HasValue || entry.PinnedTarget is not null ||
                entry.MeasurementMethod != ProgressMeasurementMethod.None)
            {
                throw Invalid("baseline.summary.invalid", "Summary entries cannot carry progress measurement data.");
            }
        }
        else
        {
            if (entry.WeightPercent is <= 0 or > 100 ||
                decimal.Round(entry.WeightPercent!.Value, 4, MidpointRounding.AwayFromZero) !=
                    entry.WeightPercent.Value)
            {
                throw Invalid("baseline.entry_weight.invalid", "A weighted entry has an invalid precision or value.");
            }

            if (entry.MeasurementMethod == ProgressMeasurementMethod.QuantityBased)
            {
                if (!entry.MeasurementItemId.HasValue || entry.PinnedTarget is null ||
                    entry.PinnedTarget.MeasurementItemId != entry.MeasurementItemId.Value)
                {
                    throw Invalid(
                        "baseline.target_snapshot.missing",
                        "Quantity progress requires an approval-time measurement target snapshot.");
                }

                ValidatePinnedTarget(entry.PinnedTarget, approvedAt);
            }
            else if (entry.MeasurementMethod == ProgressMeasurementMethod.ManualPercent)
            {
                if (entry.Kind != PlanningEntryKind.Milestone || entry.MeasurementItemId.HasValue ||
                    entry.PinnedTarget is not null)
                {
                    throw Invalid(
                        "baseline.manual_progress.invalid",
                        "Manual progress is valid only for a milestone without a measurement target.");
                }
            }
            else
            {
                throw Invalid("baseline.measurement_method.invalid", "A weighted entry requires a supported method.");
            }
        }

        if (baselineKind == PlanningBaselineKind.MeasurementWeights)
        {
            if (entry.Kind != PlanningEntryKind.MeasurementItem ||
                entry.MeasurementMethod != ProgressMeasurementMethod.QuantityBased ||
                entry.PlannedStart.HasValue || entry.PlannedFinish.HasValue)
            {
                throw Invalid(
                    "baseline.measurement_weights.invalid",
                    "A measurement weighting baseline cannot contain schedule or non-measurement entries.");
            }
        }
        else if (!entry.PlannedStart.HasValue || !entry.PlannedFinish.HasValue ||
            entry.PlannedFinish.Value < entry.PlannedStart.Value ||
            (entry.Kind == PlanningEntryKind.Milestone && entry.PlannedStart != entry.PlannedFinish))
        {
            throw Invalid("baseline.schedule.invalid", "A scheduled baseline entry has invalid planned dates.");
        }

        if (baselineKind == PlanningBaselineKind.MilestonePlan && entry.Kind != PlanningEntryKind.Milestone)
        {
            throw Invalid("baseline.milestone_plan.invalid", "A milestone plan can contain milestones only.");
        }

        return entry with { Code = entry.Code.Trim().ToUpperInvariant(), Title = entry.Title.Trim() };
    }

    private static void ValidatePinnedTarget(
        ProjectProgressPinnedMeasurementTarget target,
        DateTimeOffset approvedAt)
    {
        var capturedAt = target.CapturedAtUtc.ToUniversalTime();
        if (target.MeasurementItemId == Guid.Empty || string.IsNullOrWhiteSpace(target.Code) ||
            string.IsNullOrWhiteSpace(target.Title) || string.IsNullOrWhiteSpace(target.Unit) ||
            target.Code.Trim().Length > 80 || target.Title.Trim().Length > 240 ||
            target.Unit.Trim().Length > 40 ||
            target.TargetQuantity <= 0 || target.Revision <= 0 || capturedAt != approvedAt)
        {
            throw Invalid(
                "baseline.target_snapshot.invalid",
                "A measurement target snapshot must be complete and captured at baseline approval.");
        }
    }

    private static void ValidateHierarchy(ProjectProgressBaselineEntry[] entries)
    {
        var byId = entries.ToDictionary(entry => entry.EntryId);
        foreach (var entry in entries.Where(entry => entry.ParentEntryId.HasValue))
        {
            if (!byId.TryGetValue(entry.ParentEntryId!.Value, out var parent) ||
                parent.Kind != PlanningEntryKind.Summary || parent.EntryId == entry.EntryId)
            {
                throw Invalid("baseline.hierarchy.invalid", "A baseline parent must be a different summary entry.");
            }

            var seen = new HashSet<Guid>();
            var cursor = entry;
            while (cursor.ParentEntryId.HasValue)
            {
                if (!seen.Add(cursor.EntryId) ||
                    !byId.TryGetValue(cursor.ParentEntryId.Value, out var parent))
                {
                    throw Invalid("baseline.hierarchy.cycle", "A baseline hierarchy contains a cycle.");
                }

                cursor = parent;
            }
        }
    }

    private static ProjectProgressMilestoneUpdateVersion ValidateAndNormalize(
        ProjectProgressMilestoneUpdateVersion update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var approvedAt = update.ApprovedAt.ToUniversalTime();
        var supersededAt = update.SupersededAt?.ToUniversalTime();
        if (update.UpdateId == Guid.Empty || update.BaselineId == Guid.Empty ||
            update.BaselineEntryId == Guid.Empty || update.StatusDate == default ||
            update.ProgressPercent is < 0 or > 100 ||
            decimal.Round(update.ProgressPercent, 2, MidpointRounding.AwayFromZero) != update.ProgressPercent ||
            update.ApprovalRevision <= 0 || approvedAt == default || supersededAt <= approvedAt ||
            !Enum.IsDefined(update.Classification))
        {
            throw Invalid("milestone.invalid", "An official milestone update violates the reporting contract.");
        }

        return update with { ApprovedAt = approvedAt, SupersededAt = supersededAt };
    }

    private static void ValidateMilestoneLineage(
        ProjectProgressMilestoneUpdateVersion[] updates,
        ProjectProgressBaselineVersion[] baselines)
    {
        var baselineEntries = baselines.ToDictionary(
            baseline => baseline.BaselineId,
            baseline => baseline.Entries.ToDictionary(entry => entry.EntryId));
        foreach (var update in updates)
        {
            if (!baselineEntries.TryGetValue(update.BaselineId, out var entries) ||
                !entries.TryGetValue(update.BaselineEntryId, out var entry) ||
                entry.Kind != PlanningEntryKind.Milestone ||
                entry.MeasurementMethod != ProgressMeasurementMethod.ManualPercent)
            {
                throw Invalid(
                    "milestone.lineage.invalid",
                    "A milestone update does not reference a manual milestone in its official baseline.");
            }
        }
    }

    private static ProgressEvidenceReportingProjection ValidateAndCanonicalizeEvidence(
        ProjectProgressReportingProjection projection,
        DateTimeOffset cutoff)
    {
        var evidence = projection.Evidence;
        if (!string.Equals(
                evidence.ContractVersion,
                ProgressEvidenceReportingContract.Version,
                StringComparison.Ordinal) ||
            evidence.TenantId != projection.TenantId || evidence.ProjectId != projection.ProjectId ||
            evidence.ThroughLocalDate != projection.CutoffLocalDate ||
            evidence.SourceCutoffUtc.ToUniversalTime() != cutoff ||
            !Enum.IsDefined(evidence.Classification) || evidence.Roots is null)
        {
            throw Invalid("evidence.scope.invalid", "Progress evidence does not match the planning reporting scope.");
        }

        var roots = evidence.Roots
            .Select(root => ValidateAndCanonicalizeRoot(root, cutoff, projection.CutoffLocalDate))
            .OrderBy(root => root.ReportDate)
            .ThenBy(root => root.RootReportId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (roots.Select(root => root.RootReportId).Distinct().Count() != roots.Length ||
            roots.Select(root => root.ReportDate).Distinct().Count() != roots.Length)
        {
            throw Invalid("evidence.root.duplicate", "Progress evidence contains duplicate roots or report dates.");
        }

        var versions = roots.SelectMany(root => root.Versions).ToArray();
        var facts = versions.SelectMany(version => version.Facts).ToArray();
        if (versions.Select(version => version.ReportId).Distinct().Count() != versions.Length ||
            facts.Select(fact => fact.FactId).Distinct().Count() != facts.Length)
        {
            throw Invalid("evidence.lineage.duplicate", "Progress evidence contains duplicate version or fact lineage.");
        }

        return evidence with { SourceCutoffUtc = cutoff, Roots = roots };
    }

    private static ProgressEvidenceReportingRoot ValidateAndCanonicalizeRoot(
        ProgressEvidenceReportingRoot root,
        DateTimeOffset cutoff,
        DateOnly cutoffLocalDate)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root.RootReportId == Guid.Empty || root.ReportDate == default || root.ReportDate > cutoffLocalDate ||
            root.CurrentOfficialReportId == Guid.Empty || !Enum.IsDefined(root.Classification) || root.Versions is null)
        {
            throw Invalid("evidence.root.invalid", "A progress evidence root violates its bounded contract.");
        }

        var versions = root.Versions
            .Select(version => ValidateAndCanonicalizeVersion(root, version, cutoff))
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (versions.Select(version => version.VersionNumber).Distinct().Count() != versions.Length)
        {
            throw Invalid("evidence.version.duplicate", "A progress evidence root has duplicate version numbers.");
        }

        var official = versions
            .Where(version => IsEffectiveAt(version.ApprovedAt, version.SupersededAt, cutoff))
            .ToArray();
        if (official.Length > 1 || official.SingleOrDefault()?.ReportId != root.CurrentOfficialReportId)
        {
            throw Invalid(
                "evidence.current_official.invalid",
                "The selected official daily-report version is inconsistent with lifecycle evidence.");
        }

        return root with { Versions = versions };
    }

    private static ProgressEvidenceReportingVersion ValidateAndCanonicalizeVersion(
        ProgressEvidenceReportingRoot root,
        ProgressEvidenceReportingVersion version,
        DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(version);
        var approvedAt = version.ApprovedAt.ToUniversalTime();
        var supersededAt = version.SupersededAt?.ToUniversalTime();
        if (version.ReportId == Guid.Empty || version.RootReportId != root.RootReportId ||
            version.ReportDate != root.ReportDate || version.VersionNumber <= 0 ||
            approvedAt == default || approvedAt > cutoff || supersededAt <= approvedAt || version.Facts is null)
        {
            throw Invalid("evidence.version.invalid", "An official progress evidence version is invalid.");
        }

        var facts = version.Facts
            .Select(fact => ValidateAndCanonicalizeFact(fact, approvedAt))
            .OrderBy(fact => fact.FactId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        return version with { ApprovedAt = approvedAt, SupersededAt = supersededAt, Facts = facts };
    }

    private static ProgressEvidenceReportingFact ValidateAndCanonicalizeFact(
        ProgressEvidenceReportingFact fact,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var createdAt = fact.CreatedAt.ToUniversalTime();
        if (fact.FactId == Guid.Empty || fact.MeasurementItemId == Guid.Empty || fact.Quantity < 0 ||
            createdAt == default || createdAt > approvedAt ||
            (fact.Quantity.HasValue && string.IsNullOrWhiteSpace(fact.Unit)) || fact.Unit?.Trim().Length > 40)
        {
            throw Invalid("evidence.fact.invalid", "A minimized official progress fact is invalid.");
        }

        return fact with
        {
            Unit = string.IsNullOrWhiteSpace(fact.Unit) ? null : fact.Unit.Trim(),
            CreatedAt = createdAt
        };
    }

    private static ProjectProgressReportingClassification ResolveClassification(
        ProjectProgressConfigurationVersion? configuration,
        ProjectProgressBaselineVersion[] baselines,
        ProjectProgressMilestoneUpdateVersion[] milestones,
        ProgressEvidenceReportingProjection evidence)
    {
        var values = new List<ProjectProgressReportingClassification>
        {
            ProjectProgressReportingClassification.Internal,
            MapClassification(evidence.Classification)
        };
        if (configuration is not null)
        {
            values.Add(configuration.Classification);
        }
        values.AddRange(baselines.Select(baseline => baseline.Classification));
        values.AddRange(milestones.Select(update => update.Classification));
        values.AddRange(evidence.Roots.Select(root => MapClassification(root.Classification)));
        return values.Max();
    }

    private static ProjectProgressReportingClassification MapClassification(
        ProgressEvidenceReportingClassification classification) => classification switch
    {
        ProgressEvidenceReportingClassification.Internal => ProjectProgressReportingClassification.Internal,
        ProgressEvidenceReportingClassification.Confidential => ProjectProgressReportingClassification.Confidential,
        ProgressEvidenceReportingClassification.Restricted => ProjectProgressReportingClassification.Restricted,
        _ => throw Invalid("classification.invalid", "Progress evidence classification is unknown.")
    };

    private static DateTimeOffset? ResolveSourceMaxChangedAt(
        ProjectProgressConfigurationVersion? configuration,
        ProjectProgressBaselineVersion[] baselines,
        ProjectProgressMilestoneUpdateVersion[] milestones,
        ProgressEvidenceReportingProjection evidence,
        DateTimeOffset cutoff)
    {
        var values = new List<DateTimeOffset>();
        if (configuration is not null)
        {
            values.Add(configuration.EffectiveFromUtc);
            AddAtOrBefore(values, configuration.EffectiveToUtc, cutoff);
        }
        foreach (var baseline in baselines)
        {
            values.Add(baseline.ApprovedAt);
            AddAtOrBefore(values, baseline.SupersededAt, cutoff);
        }
        foreach (var milestone in milestones)
        {
            values.Add(milestone.ApprovedAt);
            AddAtOrBefore(values, milestone.SupersededAt, cutoff);
        }
        foreach (var version in evidence.Roots.SelectMany(root => root.Versions))
        {
            values.Add(version.ApprovedAt);
            AddAtOrBefore(values, version.SupersededAt, cutoff);
            values.AddRange(version.Facts.Select(fact => fact.CreatedAt));
        }

        return values.Count == 0 ? null : values.Max().ToUniversalTime();
    }

    private static void AddAtOrBefore(
        List<DateTimeOffset> values,
        DateTimeOffset? candidate,
        DateTimeOffset cutoff)
    {
        if (candidate.HasValue && candidate.Value <= cutoff)
        {
            values.Add(candidate.Value.ToUniversalTime());
        }
    }

    private static ProjectProgressSourceManifest BuildManifest(
        ProjectProgressReportingProjection projection,
        DateTimeOffset cutoff,
        ProjectProgressConfigurationVersion? configuration,
        ProjectProgressBaselineVersion[] baselines,
        ProjectProgressMilestoneUpdateVersion[] milestones,
        ProgressEvidenceReportingProjection evidence) => new(
        ProjectProgressReportingContract.SourceManifestVersion,
        ProjectProgressReportingContract.Version,
        projection.TenantId,
        projection.ProjectId,
        projection.CutoffLocalDate,
        cutoff,
        configuration is null
            ? null
            : new ProjectProgressConfigurationManifest(
                configuration.ConfigurationVersion,
                configuration.ProjectRevision,
                configuration.PlanningMode,
                configuration.CalendarState,
                configuration.WorkingDaysMask,
                configuration.CalendarRevision,
                configuration.EffectiveFromUtc,
                configuration.EffectiveToUtc),
        baselines.Select(baseline => new ProjectProgressBaselineManifest(
            baseline.BaselineId,
            baseline.VersionCode,
            baseline.Kind,
            baseline.ApprovalRevision,
            baseline.ApprovedAt,
            baseline.SupersededAt,
            ProjectProgressCanonicalJson.Sha256(ProjectProgressCanonicalJson.Serialize(baseline.Entries))))
            .ToArray(),
        milestones.Select(update => new ProjectProgressMilestoneManifest(
            update.UpdateId,
            update.BaselineId,
            update.BaselineEntryId,
            update.StatusDate,
            update.ApprovedAt,
            update.SupersededAt))
            .ToArray(),
        evidence.Roots.Select(root => new ProjectProgressEvidenceRootManifest(
            root.RootReportId,
            root.ReportDate,
            root.CurrentOfficialReportId,
            root.Versions.Select(version => new ProjectProgressEvidenceVersionManifest(
                version.ReportId,
                version.VersionNumber,
                version.ApprovedAt,
                version.SupersededAt,
                version.Facts.Select(fact => fact.FactId).ToArray()))
                .ToArray()))
            .ToArray());

    private static bool IsEffectiveAt(
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset? effectiveToUtc,
        DateTimeOffset cutoffUtc) =>
        effectiveFromUtc <= cutoffUtc && (!effectiveToUtc.HasValue || cutoffUtc < effectiveToUtc.Value);

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"planning.progress_reporting.{suffix}", message);
}

internal static class ProjectProgressCanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Sha256(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), element.ValueKind, "Unsupported JSON value kind.");
        }
    }
}
