using System.Text.Json;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectProgressReportRenderSnapshot
{
    public static ProjectProgressReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectProgressReportSemanticSnapshot>(
                payloadJson,
                CanonicalJson.SerializerOptions)
                ?? throw Invalid("Project Progress snapshot payload is empty.");
            ProjectProgressReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid("Project Progress snapshot payload cannot be rendered.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw Invalid("Project Progress snapshot payload uses an unsupported value.", exception);
        }
    }

    private static ReportRenderingException Invalid(string message, Exception? innerException = null) => new(
        "reporting.project_progress.snapshot.payload_invalid",
        transient: false,
        message,
        innerException);
}

internal sealed record ProjectProgressReportRenderRequest(
    Guid RunId,
    Guid OutputId,
    Guid SnapshotId,
    Guid TemplateVersionId,
    string DefinitionCode,
    string DefinitionVersion,
    string TemplateVersion,
    string TemplateContentDigest,
    string RendererContractVersion,
    string LayoutContractVersion,
    ReportFormat Format,
    string FileName,
    string VerificationCode,
    string ManifestSha256,
    string SnapshotSha256,
    string SourceManifestSha256,
    DateTimeOffset SourceCutoffUtc,
    ProjectProgressReportSemanticSnapshot Snapshot);

internal interface IProjectProgressReportRenderer
{
    ReportFormat Format { get; }

    RenderedReportArtifact Render(ProjectProgressReportRenderRequest request);
}

internal sealed class ProjectProgressReportRenderModel
{
    private ProjectProgressReportRenderModel(ProjectProgressReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        ReasonCodes = Snapshot.ReasonCodes.OrderBy(item => item).ToArray();
        Entries = Snapshot.Entries
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.EntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        Milestones = Snapshot.Milestones
            .OrderBy(item => item.PlannedDate)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.BaselineEntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        Curve = Snapshot.Curve.OrderBy(item => item.PointDate).ToArray();
    }

    public ProjectProgressReportRenderRequest Request { get; }

    public ProjectProgressReportSemanticSnapshot Snapshot { get; }

    public IReadOnlyList<ProjectProgressReportReasonCode> ReasonCodes { get; }

    public IReadOnlyList<ProjectProgressReportEntry> Entries { get; }

    public IReadOnlyList<ProjectProgressReportMilestone> Milestones { get; }

    public IReadOnlyList<ProjectProgressReportCurvePoint> Curve { get; }

    public static ProjectProgressReportRenderModel Create(ProjectProgressReportRenderRequest request)
    {
        ProjectProgressReportRenderingContract.ValidateRequest(request);
        return new ProjectProgressReportRenderModel(request);
    }
}

internal static class ProjectProgressReportRenderingContract
{
    public const int MaximumProjectCodeLength = 160;
    public const int MaximumProjectNameLength = 400;
    public const int MaximumEntryCodeLength = 160;
    public const int MaximumEntryTitleLength = 400;
    public const int MaximumUnitLength = 80;
    public const int MaximumSourceTextLength = 400;

    public static void ValidateRequest(ProjectProgressReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        var extension = request.Format switch
        {
            ReportFormat.Pdf => ".pdf",
            ReportFormat.Xlsx => ".xlsx",
            _ => throw InvalidRequest("The Project Progress output format is unsupported.")
        };
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            !string.Equals(request.DefinitionCode, ProjectProgressReportRuntimeContract.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(request.DefinitionVersion, ProjectProgressReportRuntimeContract.DefinitionVersion, StringComparison.Ordinal) ||
            !string.Equals(request.TemplateVersion, ProjectProgressReportRuntimeContract.TemplateVersion, StringComparison.Ordinal) ||
            !string.Equals(request.TemplateContentDigest, ProjectProgressReportRuntimeContract.TemplateContentDigest, StringComparison.Ordinal) ||
            !string.Equals(request.RendererContractVersion, ProjectProgressReportRuntimeContract.RendererContractVersion, StringComparison.Ordinal) ||
            !string.Equals(request.LayoutContractVersion, ProjectProgressReportRuntimeContract.LayoutContractVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            !request.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.FileName, ReportArtifactIdentity.FileName(request.Snapshot, request.Format), StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !IsSha256(request.TemplateContentDigest) || !IsSha256(request.ManifestSha256) ||
            !IsSha256(request.SnapshotSha256) || !IsSha256(request.SourceManifestSha256))
        {
            throw InvalidRequest("The Project Progress render request violates its pinned contract.");
        }

        var canonicalSnapshot = CanonicalJson.Serialize(request.Snapshot);
        if (!string.Equals(CanonicalJson.Sha256(canonicalSnapshot), request.SnapshotSha256, StringComparison.Ordinal) ||
            !string.Equals(request.SourceManifestSha256, request.Snapshot.SourceManifestSha256, StringComparison.Ordinal) ||
            request.SourceCutoffUtc.ToUniversalTime() != request.Snapshot.Cutoff.SourceCutoffUtc.ToUniversalTime())
        {
            throw InvalidRequest("The render request does not match its immutable snapshot identity.");
        }
    }

    public static void ValidateSnapshot(ProjectProgressReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.SchemaVersion, ProjectProgressReportRuntimeContract.SnapshotSchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DefinitionCode, ProjectProgressReportRuntimeContract.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DefinitionVersion, ProjectProgressReportRuntimeContract.DefinitionVersion, StringComparison.Ordinal) ||
            !Enum.IsDefined(snapshot.DataStatus) || snapshot.DataStatus == ReportDataStatus.Pending ||
            snapshot.ReasonCodes is null || snapshot.Parameters is null || snapshot.Project is null ||
            snapshot.Cutoff is null || !Enum.IsDefined(snapshot.Classification) ||
            !Enum.IsDefined(snapshot.ActualStatus) || !Enum.IsDefined(snapshot.ScheduleStatus) ||
            !Enum.IsDefined(snapshot.CurveStatus) ||
            (snapshot.CalendarBasis.HasValue && !Enum.IsDefined(snapshot.CalendarBasis.Value)) ||
            snapshot.Entries is null || snapshot.Milestones is null || snapshot.Sampling is null ||
            snapshot.Curve is null || snapshot.ApprovedProgressOutsideBaselineCount < 0 ||
            !IsSha256(snapshot.SourceManifestSha256))
        {
            throw InvalidSnapshot("Project Progress snapshot identity or required collections are invalid.");
        }

        ValidateProject(snapshot.Project);
        ValidateCutoff(snapshot.Cutoff);
        ValidateReasons(snapshot.ReasonCodes);
        ValidateConfiguration(snapshot.Configuration, snapshot.Cutoff);
        ValidateBaseline(snapshot.Baseline, snapshot.Cutoff);
        ValidateEntries(snapshot.Entries);
        ValidateMilestones(snapshot.Milestones);
        ValidateSamplingAndCurve(snapshot.Sampling, snapshot.Curve, snapshot.Cutoff);
        ValidateSummary(snapshot.Summary, snapshot.Curve, snapshot.Cutoff.CutoffLocalDate);
        ValidateStatus(snapshot);
        if (snapshot.SourceMaxChangedAt.HasValue &&
            (snapshot.SourceMaxChangedAt.Value.Offset != TimeSpan.Zero ||
                snapshot.SourceMaxChangedAt.Value > snapshot.Cutoff.SourceCutoffUtc))
        {
            throw InvalidSnapshot("Project Progress source change time is invalid.");
        }
    }

    private static void ValidateProject(ProjectProgressReportProjectIdentity project)
    {
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(project.Code) || project.Code.Length > MaximumProjectCodeLength ||
            string.IsNullOrWhiteSpace(project.Name) || project.Name.Length > MaximumProjectNameLength ||
            string.IsNullOrWhiteSpace(project.TimeZone) || project.Revision <= 0 ||
            project.ConfigurationVersion <= 0 || project.ConfigurationChangedAt == default ||
            project.ConfigurationChangedAt.Offset != TimeSpan.Zero)
        {
            throw InvalidSnapshot("Project Progress project identity is invalid.");
        }
    }

    private static void ValidateCutoff(ProjectProgressReportCutoffIdentity cutoff)
    {
        if (cutoff.SourceCutoffUtc == default || cutoff.SourceCutoffUtc.Offset != TimeSpan.Zero ||
            cutoff.CutoffLocalDate == default)
        {
            throw InvalidSnapshot("Project Progress cutoff identity is invalid.");
        }
    }

    private static void ValidateReasons(IReadOnlyCollection<ProjectProgressReportReasonCode> reasons)
    {
        var canonical = reasons.OrderBy(item => item).ToArray();
        if (reasons.Any(reason => !Enum.IsDefined(reason)) ||
            reasons.Distinct().Count() != reasons.Count || !reasons.SequenceEqual(canonical))
        {
            throw InvalidSnapshot("Project Progress reason codes are invalid or non-canonical.");
        }
    }

    private static void ValidateConfiguration(
        ProjectProgressReportConfiguration? configuration,
        ProjectProgressReportCutoffIdentity cutoff)
    {
        if (configuration is null)
        {
            return;
        }

        if (configuration.ConfigurationVersion <= 0 || configuration.ProjectRevision <= 0 ||
            !Enum.IsDefined(configuration.PlanningMode) || !Enum.IsDefined(configuration.CalendarState) ||
            configuration.CalendarRevision <= 0 || configuration.EffectiveFromUtc == default ||
            configuration.EffectiveFromUtc.Offset != TimeSpan.Zero ||
            configuration.EffectiveFromUtc > cutoff.SourceCutoffUtc ||
            configuration.EffectiveToUtc.HasValue &&
                (configuration.EffectiveToUtc.Value.Offset != TimeSpan.Zero ||
                    configuration.EffectiveToUtc.Value <= cutoff.SourceCutoffUtc) ||
            configuration.WorkingDaysMask.HasValue &&
                configuration.WorkingDaysMask.Value is not (>= 1 and <= 127) ||
            configuration.CalendarState == ProjectProgressCalendarState.WorkingWeek &&
                !configuration.WorkingDaysMask.HasValue ||
            configuration.CalendarState == ProjectProgressCalendarState.NotConfigured &&
                configuration.WorkingDaysMask.HasValue)
        {
            throw InvalidSnapshot("Project Progress effective configuration is invalid.");
        }
    }

    private static void ValidateBaseline(
        ProjectProgressReportBaseline? baseline,
        ProjectProgressReportCutoffIdentity cutoff)
    {
        if (baseline is null)
        {
            return;
        }

        if (baseline.BaselineId == Guid.Empty || string.IsNullOrWhiteSpace(baseline.VersionCode) ||
            baseline.VersionCode.Length > MaximumEntryCodeLength || string.IsNullOrWhiteSpace(baseline.Title) ||
            baseline.Title.Length > MaximumEntryTitleLength || !Enum.IsDefined(baseline.Kind) ||
            baseline.ApprovalRevision <= 0 || baseline.ApprovedAt == default ||
            baseline.ApprovedAt.Offset != TimeSpan.Zero || baseline.ApprovedAt > cutoff.SourceCutoffUtc ||
            baseline.SupersededAt.HasValue &&
                (baseline.SupersededAt.Value.Offset != TimeSpan.Zero ||
                    baseline.SupersededAt.Value <= cutoff.SourceCutoffUtc) ||
            baseline.SourceSystem?.Length > MaximumSourceTextLength ||
            baseline.SourceReference?.Length > MaximumSourceTextLength ||
            !IsSha256(baseline.DefinitionSha256))
        {
            throw InvalidSnapshot("Project Progress baseline identity is invalid.");
        }
    }

    private static void ValidateEntries(IReadOnlyCollection<ProjectProgressReportEntry> entries)
    {
        if (entries.Count > ProjectProgressReportingContract.MaximumBaselineEntries ||
            entries.Any(item => item is null))
        {
            throw InvalidSnapshot("Project Progress entries are null or exceed their budget.");
        }

        var canonical = entries
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.EntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var ids = entries.Select(item => item.EntryId).ToHashSet();
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw InvalidSnapshot("A Project Progress entry is invalid.");
            }
            var isSummary = entry.Kind == PlanningEntryKind.Summary;
            if (entry.EntryId == Guid.Empty ||
                entry.ParentEntryId == entry.EntryId || entry.ParentEntryId.HasValue && !ids.Contains(entry.ParentEntryId.Value) ||
                string.IsNullOrWhiteSpace(entry.Code) || entry.Code.Length > MaximumEntryCodeLength ||
                string.IsNullOrWhiteSpace(entry.Title) || entry.Title.Length > MaximumEntryTitleLength ||
                !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.MeasurementMethod) || entry.SortOrder <= 0 ||
                isSummary && entry.WeightPercent.HasValue ||
                !isSummary && entry.WeightPercent is not (> 0 and <= 100) ||
                entry.ApprovedQuantity < 0 || entry.ActualPercent < 0 ||
                entry.PlannedPercent is < 0 or > 100 ||
                entry.PlannedStart.HasValue != entry.PlannedFinish.HasValue ||
                entry.PlannedStart > entry.PlannedFinish)
            {
                throw InvalidSnapshot("A Project Progress entry is invalid.");
            }

            if (entry.MeasurementTarget is not null &&
                (entry.MeasurementTarget.MeasurementItemId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(entry.MeasurementTarget.Code) ||
                    entry.MeasurementTarget.Code.Length > MaximumEntryCodeLength ||
                    string.IsNullOrWhiteSpace(entry.MeasurementTarget.Title) ||
                    entry.MeasurementTarget.Title.Length > MaximumEntryTitleLength ||
                    string.IsNullOrWhiteSpace(entry.MeasurementTarget.Unit) ||
                    entry.MeasurementTarget.Unit.Length > MaximumUnitLength ||
                    entry.MeasurementTarget.TargetQuantity <= 0))
            {
                throw InvalidSnapshot("A Project Progress measurement target is invalid.");
            }
            if ((!isSummary && entry.MeasurementMethod == ProgressMeasurementMethod.QuantityBased &&
                    entry.MeasurementTarget is null) ||
                (entry.MeasurementMethod != ProgressMeasurementMethod.QuantityBased &&
                    entry.MeasurementTarget is not null))
            {
                throw InvalidSnapshot("A Project Progress measurement method does not match its pinned target.");
            }
            ValidateVariance(entry.ActualPercent, entry.PlannedPercent, entry.VariancePercent);
        }

        if (entries.Select(item => item.EntryId).Distinct().Count() != entries.Count ||
            !entries.SequenceEqual(canonical))
        {
            throw InvalidSnapshot("Project Progress entries are duplicated or non-canonical.");
        }
    }

    private static void ValidateMilestones(IReadOnlyCollection<ProjectProgressReportMilestone> milestones)
    {
        var canonical = milestones
            .OrderBy(item => item.PlannedDate)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.BaselineEntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (milestones.Any(item => item is null || item.BaselineEntryId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.Code) || item.Code.Length > MaximumEntryCodeLength ||
                string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > MaximumEntryTitleLength ||
                item.PlannedDate == default || item.WeightPercent is <= 0 or > 100 ||
                item.ApprovedProgressPercent is < 0 or > 100) ||
            milestones.Select(item => item.BaselineEntryId).Distinct().Count() != milestones.Count ||
            !milestones.SequenceEqual(canonical))
        {
            throw InvalidSnapshot("Project Progress milestones are invalid or non-canonical.");
        }
    }

    private static void ValidateSamplingAndCurve(
        ProjectProgressCurveSampling sampling,
        IReadOnlyCollection<ProjectProgressReportCurvePoint> curve,
        ProjectProgressReportCutoffIdentity cutoff)
    {
        if (!Enum.IsDefined(sampling.Kind) || sampling.BaseGridPointCount < 0 ||
            sampling.PointCount != curve.Count || curve.Count > ProjectProgressReportingContract.MaximumCurvePoints ||
            sampling.Kind == ProjectProgressCurveSamplingKind.NotConfigured &&
                (sampling.IsSampled || sampling.BaseGridPointCount != 0 || curve.Count != 0))
        {
            throw InvalidSnapshot("Project Progress curve sampling is invalid.");
        }

        var canonical = curve.OrderBy(item => item.PointDate).ToArray();
        foreach (var point in curve)
        {
            if (point is null || point.PointDate == default || point.PlannedPercent is < 0 or > 100 ||
                point.ActualPercent < 0 || point.MissingActualEntryCount < 0 ||
                point.PointDate > cutoff.CutoffLocalDate &&
                    (point.ActualPercent.HasValue || point.VariancePercent.HasValue))
            {
                throw InvalidSnapshot("A Project Progress curve point is invalid.");
            }
            ValidateVariance(point.ActualPercent, point.PlannedPercent, point.VariancePercent);
        }
        if (curve.Select(item => item.PointDate).Distinct().Count() != curve.Count ||
            !curve.SequenceEqual(canonical) ||
            curve.Count > 0 && curve.All(item => item.PointDate != cutoff.CutoffLocalDate))
        {
            throw InvalidSnapshot("Project Progress curve is duplicated, non-canonical, or missing its cutoff.");
        }
    }

    private static void ValidateSummary(
        ProjectProgressReportSummary? summary,
        IReadOnlyCollection<ProjectProgressReportCurvePoint> curve,
        DateOnly cutoffLocalDate)
    {
        if (summary is null)
        {
            return;
        }
        if (summary.ActualPercent < 0 || summary.PlannedPercent is < 0 or > 100 ||
            summary.MissingActualEntryCount < 0)
        {
            throw InvalidSnapshot("Project Progress summary is invalid.");
        }
        ValidateVariance(summary.ActualPercent, summary.PlannedPercent, summary.VariancePercent);
        var cutoff = curve.SingleOrDefault(item => item.PointDate == cutoffLocalDate);
        if (cutoff is not null &&
            (cutoff.ActualPercent != summary.ActualPercent || cutoff.PlannedPercent != summary.PlannedPercent ||
                cutoff.VariancePercent != summary.VariancePercent ||
                cutoff.MissingActualEntryCount != summary.MissingActualEntryCount))
        {
            throw InvalidSnapshot("Project Progress cutoff summary does not match its curve point.");
        }
    }

    private static void ValidateStatus(ProjectProgressReportSemanticSnapshot snapshot)
    {
        var valid = snapshot.DataStatus switch
        {
            ReportDataStatus.NotConfigured => snapshot.Baseline is null && snapshot.Summary is null &&
                snapshot.Entries.Count == 0 && snapshot.Curve.Count == 0 && snapshot.ReasonCodes.Count == 1 &&
                snapshot.ReasonCodes.Single() is ProjectProgressReportReasonCode.ProgressReportingNotConfigured or
                    ProjectProgressReportReasonCode.PlanningModeNone,
            ReportDataStatus.NoData => snapshot.Baseline is null && snapshot.Summary is null &&
                snapshot.Entries.Count == 0 && snapshot.Curve.Count == 0 &&
                snapshot.ReasonCodes.SequenceEqual([ProjectProgressReportReasonCode.OfficialBaselineMissing]),
            ReportDataStatus.InsufficientData => snapshot.Baseline is not null && snapshot.ReasonCodes.Any(reason =>
                reason is ProjectProgressReportReasonCode.PlanningModeBaselineMismatch or
                    ProjectProgressReportReasonCode.OfficialActualMissing or
                    ProjectProgressReportReasonCode.OfficialActualIncomplete),
            ReportDataStatus.Available => snapshot.Baseline is not null && snapshot.Summary?.ActualPercent is not null &&
                !snapshot.ReasonCodes.Any(reason => reason is
                    ProjectProgressReportReasonCode.PlanningModeBaselineMismatch or
                    ProjectProgressReportReasonCode.OfficialActualMissing or
                    ProjectProgressReportReasonCode.OfficialActualIncomplete),
            _ => false
        };
        var samplingConfigured = snapshot.Sampling.Kind != ProjectProgressCurveSamplingKind.NotConfigured;
        var scheduleNotConfigured = snapshot.ReasonCodes.Contains(
            ProjectProgressReportReasonCode.ScheduleNotConfiguredForMeasurementWeights);
        var scheduleReasonExpected = snapshot.ScheduleStatus == ProjectProgressMetricStatus.NotConfigured &&
            snapshot.CurveStatus == ProjectProgressMetricStatus.NotConfigured &&
            snapshot.Baseline?.Kind == PlanningBaselineKind.MeasurementWeights;
        if (!valid || samplingConfigured &&
                (snapshot.CurveStatus != ProjectProgressMetricStatus.Available || snapshot.Curve.Count == 0) ||
            snapshot.Baseline is null && (snapshot.Entries.Count > 0 || snapshot.Milestones.Count > 0) ||
            scheduleNotConfigured != scheduleReasonExpected)
        {
            throw InvalidSnapshot("Project Progress data or metric status is inconsistent with its semantic content.");
        }
    }

    private static void ValidateVariance(decimal? actual, decimal? planned, decimal? variance)
    {
        var expected = actual.HasValue && planned.HasValue
            ? decimal.Round(actual.Value - planned.Value, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        if (variance != expected)
        {
            throw InvalidSnapshot("Project Progress variance must equal Actual minus Planned.");
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static ReportRenderingException InvalidSnapshot(string message) => new(
        "reporting.project_progress.snapshot.payload_invalid",
        transient: false,
        message);

    private static ReportRenderingException InvalidRequest(string message) => new(
        "reporting.project_progress.render_request.invalid",
        transient: false,
        message);
}
