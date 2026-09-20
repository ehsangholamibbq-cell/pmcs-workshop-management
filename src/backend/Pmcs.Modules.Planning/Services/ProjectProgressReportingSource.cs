using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Planning.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Planning.Services;

internal sealed class ProjectProgressReportingSource(
    PlanningDbContext dbContext,
    IProjectDirectory projectDirectory,
    IProgressEvidenceReportingSource evidenceSource) : IProjectProgressReportingSource
{
    public async Task<ProjectProgressReportingResult> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            throw new DomainRuleException(
                "planning.progress_reporting.project.not_found",
                "The project progress reporting scope does not exist.");
        }
        if (!project.ConfigurationChangedAt.HasValue ||
            project.ConfigurationChangedAt.Value.ToUniversalTime() > cutoff)
        {
            throw new DomainRuleException(
                "planning.progress_reporting.configuration_history.unavailable",
                "The current project profile cannot safely reconstruct planning configuration at this cutoff.");
        }

        var storedBaselines = await dbContext.PlanningBaselines
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff &&
                (item.Status == PlanningBaselineStatus.Approved ||
                    item.Status == PlanningBaselineStatus.Superseded))
            .ToArrayAsync(cancellationToken);
        if (storedBaselines.Any(item => item.Status == PlanningBaselineStatus.Superseded &&
            item.SubmittedAt <= cutoff))
        {
            throw new DomainRuleException(
                "planning.progress_reporting.baseline_history.unavailable",
                "A legacy superseded baseline does not retain independent approval and supersession timestamps.");
        }

        var eligibleBaselines = storedBaselines
            .Where(item => item.Status == PlanningBaselineStatus.Approved &&
                item.ReviewedAt.HasValue && item.ReviewedAt.Value <= cutoff)
            .ToArray();
        var measurementIds = eligibleBaselines
            .SelectMany(item => item.Entries)
            .Where(entry => entry.MeasurementMethod == ProgressMeasurementMethod.QuantityBased &&
                entry.MeasurementItemId.HasValue)
            .Select(entry => entry.MeasurementItemId!.Value)
            .Distinct()
            .ToArray();
        MeasurementItem[] measurementItems = measurementIds.Length == 0
            ? []
            : await dbContext.MeasurementItems
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                    measurementIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken);
        var measurementById = measurementItems.ToDictionary(item => item.Id);

        var storedMilestones = await dbContext.MilestoneProgressUpdates
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff &&
                (item.Status == MilestoneProgressStatus.Approved ||
                    item.Status == MilestoneProgressStatus.Superseded))
            .ToArrayAsync(cancellationToken);
        if (storedMilestones.Any(item => item.Status == MilestoneProgressStatus.Superseded &&
            item.SubmittedAt <= cutoff))
        {
            throw new DomainRuleException(
                "planning.progress_reporting.milestone_history.unavailable",
                "A legacy superseded milestone update does not retain independent lifecycle timestamps.");
        }

        var evidence = await evidenceSource.LoadAsync(
            tenantId,
            projectId,
            cutoffLocalDate,
            cutoff,
            cancellationToken);
        var changedAt = project.ConfigurationChangedAt.Value.ToUniversalTime();
        var projection = new ProjectProgressReportingProjection(
            ProjectProgressReportingContract.Version,
            tenantId,
            projectId,
            cutoffLocalDate,
            cutoff,
            [new ProjectProgressConfigurationVersion(
                project.ConfigurationVersion,
                project.Revision,
                ProgressReportingEnabled: true,
                project.PlanningMode,
                project.Calendar.State == ProjectCalendarConfigurationState.Configured
                    ? ProjectProgressCalendarState.WorkingWeek
                    : ProjectProgressCalendarState.NotConfigured,
                project.Calendar.WorkingDaysMask,
                project.ConfigurationVersion,
                changedAt,
                null,
                ProjectProgressReportingClassification.Internal)],
            eligibleBaselines.Select(item => Map(item, measurementById)).ToArray(),
            storedMilestones
                .Where(item => item.Status == MilestoneProgressStatus.Approved &&
                    item.ReviewedAt.HasValue && item.ReviewedAt.Value <= cutoff)
                .Select(Map)
                .ToArray(),
            evidence);

        return ProjectProgressReportingCalculator.Calculate(
            ProjectProgressReportingSelector.Select(projection));
    }

    private static ProjectProgressBaselineVersion Map(
        PlanningBaseline baseline,
        IReadOnlyDictionary<Guid, MeasurementItem> measurementItems)
    {
        var approvedAt = baseline.ReviewedAt?.ToUniversalTime()
            ?? throw new DomainRuleException(
                "planning.progress_reporting.baseline_approval.missing",
                "An approved baseline requires an approval timestamp.");
        return new ProjectProgressBaselineVersion(
            baseline.Id,
            baseline.VersionCode,
            baseline.Title,
            baseline.Kind,
            baseline.Revision,
            approvedAt,
            null,
            baseline.SourceSystem,
            baseline.SourceReference,
            ProjectProgressReportingClassification.Internal,
            baseline.Entries.Select(entry => Map(entry, approvedAt, measurementItems)).ToArray());
    }

    private static ProjectProgressBaselineEntry Map(
        PlanningBaselineEntry entry,
        DateTimeOffset approvedAt,
        IReadOnlyDictionary<Guid, MeasurementItem> measurementItems)
    {
        ProjectProgressPinnedMeasurementTarget? target = null;
        if (entry.MeasurementMethod == ProgressMeasurementMethod.QuantityBased)
        {
            if (!entry.MeasurementItemId.HasValue ||
                !measurementItems.TryGetValue(entry.MeasurementItemId.Value, out var measurement) ||
                measurement.CreatedAt > approvedAt || measurement.LastModifiedAt > approvedAt ||
                !measurement.TargetQuantity.HasValue)
            {
                throw new DomainRuleException(
                    "planning.progress_reporting.target_history.unavailable",
                    "The current measurement item cannot safely reconstruct its approval-time target snapshot.");
            }

            target = new ProjectProgressPinnedMeasurementTarget(
                measurement.Id,
                measurement.Code,
                measurement.Title,
                measurement.Unit,
                measurement.TargetQuantity.Value,
                measurement.Revision,
                approvedAt);
        }

        return new ProjectProgressBaselineEntry(
            entry.Id,
            entry.ParentEntryId,
            entry.Code,
            entry.Title,
            entry.Kind,
            entry.MeasurementMethod,
            entry.MeasurementItemId,
            entry.PlannedStart,
            entry.PlannedFinish,
            entry.WeightPercent,
            entry.SortOrder,
            target);
    }

    private static ProjectProgressMilestoneUpdateVersion Map(MilestoneProgressUpdate update) => new(
        update.Id,
        update.BaselineId,
        update.BaselineEntryId,
        update.StatusDate,
        update.ProgressPercent,
        update.Revision,
        update.ReviewedAt?.ToUniversalTime()
            ?? throw new DomainRuleException(
                "planning.progress_reporting.milestone_approval.missing",
                "An approved milestone update requires an approval timestamp."),
        null,
        ProjectProgressReportingClassification.Internal);
}
