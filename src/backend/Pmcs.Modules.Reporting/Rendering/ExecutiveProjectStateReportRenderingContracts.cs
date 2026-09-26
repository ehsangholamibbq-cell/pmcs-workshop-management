using System.Text.Json;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ExecutiveProjectStateReportRenderSnapshot
{
    public static ExecutiveProjectStateReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ExecutiveProjectStateReportSemanticSnapshot>(
                payloadJson,
                CanonicalJson.SerializerOptions)
                ?? throw Invalid("Executive Project State snapshot payload is empty.");
            ExecutiveProjectStateReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid("Executive Project State snapshot payload cannot be rendered.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw Invalid("Executive Project State snapshot payload uses an unsupported value.", exception);
        }
    }

    private static ReportRenderingException Invalid(string message, Exception? innerException = null) => new(
        "reporting.executive_state.snapshot.payload_invalid",
        transient: false,
        message,
        innerException);
}

internal sealed record ExecutiveProjectStateReportRenderRequest(
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
    ExecutiveProjectStateReportSemanticSnapshot Snapshot);

internal interface IExecutiveProjectStateReportRenderer
{
    ReportFormat Format { get; }

    RenderedReportArtifact Render(ExecutiveProjectStateReportRenderRequest request);
}

internal sealed record ExecutiveProjectStateFeatureRow(string Domain, ProjectFeatureState State);

internal sealed record ExecutiveProjectStateFactRow(string Kind, int Count);

internal sealed class ExecutiveProjectStateReportRenderModel
{
    private ExecutiveProjectStateReportRenderModel(ExecutiveProjectStateReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        ReasonCodes = Snapshot.ReasonCodes
            .OrderBy(item => item.ToString(), StringComparer.Ordinal)
            .ToArray();
        AttentionItems = Snapshot.OfficialSnapshot?.AttentionItems
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => item.AgeDays)
            .ThenBy(item => item.ReportDate)
            .ThenBy(item => item.SourceReportId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(item => item.SourceFactId.ToString("D"), StringComparer.Ordinal)
            .ToArray() ?? [];
        Trend = Snapshot.Trend
            .OrderBy(item => item.AsOfDate)
            .ThenBy(item => item.CalculatedAt)
            .ThenBy(item => item.SnapshotId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        FeatureStates = Snapshot.OfficialSnapshot is null
            ? []
            :
            [
                new ExecutiveProjectStateFeatureRow("قرارداد", Snapshot.OfficialSnapshot.FeatureStates.Contract),
                new ExecutiveProjectStateFeatureRow("برنامه‌ریزی", Snapshot.OfficialSnapshot.FeatureStates.Planning),
                new ExecutiveProjectStateFeatureRow("بودجه", Snapshot.OfficialSnapshot.FeatureStates.Budget),
                new ExecutiveProjectStateFeatureRow("کیفیت", Snapshot.OfficialSnapshot.FeatureStates.Quality),
                new ExecutiveProjectStateFeatureRow("HSE", Snapshot.OfficialSnapshot.FeatureStates.Hse)
            ];
        FactCounts = Snapshot.OfficialSnapshot is null
            ? []
            :
            [
                new ExecutiveProjectStateFactRow("کل Fact رسمی", Snapshot.OfficialSnapshot.FactCounts.Approved),
                new ExecutiveProjectStateFactRow("پیشرفت کار", Snapshot.OfficialSnapshot.FactCounts.Progress),
                new ExecutiveProjectStateFactRow("نیروی انسانی", Snapshot.OfficialSnapshot.FactCounts.Labor),
                new ExecutiveProjectStateFactRow("ماشین‌آلات", Snapshot.OfficialSnapshot.FactCounts.Equipment),
                new ExecutiveProjectStateFactRow("مصالح", Snapshot.OfficialSnapshot.FactCounts.Material)
            ];
    }

    public ExecutiveProjectStateReportRenderRequest Request { get; }

    public ExecutiveProjectStateReportSemanticSnapshot Snapshot { get; }

    public IReadOnlyList<ExecutiveProjectStateReportReasonCode> ReasonCodes { get; }

    public IReadOnlyList<ExecutiveProjectStateAttentionItem> AttentionItems { get; }

    public IReadOnlyList<ExecutiveProjectStateTrendPoint> Trend { get; }

    public IReadOnlyList<ExecutiveProjectStateFeatureRow> FeatureStates { get; }

    public IReadOnlyList<ExecutiveProjectStateFactRow> FactCounts { get; }

    public static ExecutiveProjectStateReportRenderModel Create(
        ExecutiveProjectStateReportRenderRequest request)
    {
        ExecutiveProjectStateReportRenderingContract.ValidateRequest(request);
        return new ExecutiveProjectStateReportRenderModel(request);
    }

    private static int PriorityRank(ProjectAttentionPriority priority) => priority switch
    {
        ProjectAttentionPriority.Critical => 5,
        ProjectAttentionPriority.High => 4,
        ProjectAttentionPriority.Medium => 3,
        ProjectAttentionPriority.Low => 2,
        ProjectAttentionPriority.Unassessed => 1,
        _ => 0
    };
}

internal static class ExecutiveProjectStateReportRenderingContract
{
    public const int MaximumAttentionDescriptionLength = 4_000;
    public const int MaximumAttentionCategoryLength = 240;
    public const int MaximumAttentionLocationLength = 240;
    public const int MaximumAttentionReferenceLength = 160;

    public static void ValidateRequest(ExecutiveProjectStateReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        var extension = request.Format switch
        {
            ReportFormat.Pdf => ".pdf",
            ReportFormat.Xlsx => ".xlsx",
            _ => throw InvalidRequest("The Executive Project State output format is unsupported.")
        };
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            !string.Equals(request.DefinitionCode, ExecutiveProjectStateReportRuntimeContract.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(request.DefinitionVersion, ExecutiveProjectStateReportRuntimeContract.DefinitionVersion, StringComparison.Ordinal) ||
            !string.Equals(request.TemplateVersion, ExecutiveProjectStateReportRuntimeContract.TemplateVersion, StringComparison.Ordinal) ||
            !string.Equals(request.TemplateContentDigest, ExecutiveProjectStateReportRuntimeContract.TemplateContentDigest, StringComparison.Ordinal) ||
            !string.Equals(request.RendererContractVersion, ExecutiveProjectStateReportRuntimeContract.RendererContractVersion, StringComparison.Ordinal) ||
            !string.Equals(request.LayoutContractVersion, ExecutiveProjectStateReportRuntimeContract.LayoutContractVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            !request.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.FileName, ReportArtifactIdentity.FileName(request.Snapshot, request.Format), StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !IsSha256(request.TemplateContentDigest) ||
            !IsSha256(request.ManifestSha256) ||
            !IsSha256(request.SnapshotSha256) ||
            !IsSha256(request.SourceManifestSha256))
        {
            throw InvalidRequest("The Executive Project State render request violates its pinned contract.");
        }

        var canonicalSnapshot = CanonicalJson.Serialize(request.Snapshot);
        if (!string.Equals(
                CanonicalJson.Sha256(canonicalSnapshot),
                request.SnapshotSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.SourceManifestSha256,
                request.Snapshot.SourceManifestSha256,
                StringComparison.Ordinal) ||
            request.SourceCutoffUtc.ToUniversalTime() !=
                request.Snapshot.Cutoff.SourceCutoffUtc.ToUniversalTime())
        {
            throw InvalidRequest("The render request does not match its immutable snapshot identity.");
        }
    }

    public static void ValidateSnapshot(ExecutiveProjectStateReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.SchemaVersion, ExecutiveProjectStateReportRuntimeContract.SnapshotSchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DefinitionCode, ExecutiveProjectStateReportRuntimeContract.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DefinitionVersion, ExecutiveProjectStateReportRuntimeContract.DefinitionVersion, StringComparison.Ordinal) ||
            !Enum.IsDefined(snapshot.DataStatus) || snapshot.DataStatus == ReportDataStatus.Pending ||
            snapshot.ReasonCodes is null || snapshot.Project is null || snapshot.Cutoff is null ||
            snapshot.Currency is null || snapshot.Trend is null ||
            !Enum.IsDefined(snapshot.SourceState) || !IsSha256(snapshot.SourceManifestSha256))
        {
            throw InvalidSnapshot("Executive Project State snapshot identity or required collections are invalid.");
        }

        ValidateProject(snapshot.Project);
        ValidateCutoff(snapshot.Cutoff);
        ValidateReasons(snapshot.ReasonCodes);
        if (snapshot.OfficialSnapshot is not null)
        {
            ValidateOfficialSnapshot(snapshot.OfficialSnapshot, snapshot.Project, snapshot.Cutoff);
        }
        ValidateTrend(snapshot.Trend, snapshot.OfficialSnapshot, snapshot.Cutoff);
        ValidateStatus(snapshot);
    }

    private static void ValidateProject(ExecutiveProjectStateProjectIdentity project)
    {
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(project.Code) || string.IsNullOrWhiteSpace(project.Name) ||
            string.IsNullOrWhiteSpace(project.TimeZone) || string.IsNullOrWhiteSpace(project.BaseCurrencyCode) ||
            project.Revision <= 0 || project.ConfigurationVersion <= 0 ||
            project.ConfigurationChangedAt == default)
        {
            throw InvalidSnapshot("Executive Project State project identity is invalid.");
        }
    }

    private static void ValidateCutoff(ExecutiveProjectStateCutoffIdentity cutoff)
    {
        if (cutoff.SourceCutoffUtc == default || cutoff.CutoffLocalDate == default ||
            cutoff.SourceCutoffUtc.Offset != TimeSpan.Zero)
        {
            throw InvalidSnapshot("Executive Project State cutoff identity is invalid.");
        }
    }

    private static void ValidateReasons(
        IReadOnlyCollection<ExecutiveProjectStateReportReasonCode> reasons)
    {
        var canonical = reasons
            .OrderBy(item => item.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (reasons.Any(reason => !Enum.IsDefined(reason)) ||
            reasons.Distinct().Count() != reasons.Count ||
            !reasons.SequenceEqual(canonical))
        {
            throw InvalidSnapshot("Executive Project State reason codes are invalid or non-canonical.");
        }
    }

    private static void ValidateOfficialSnapshot(
        ExecutiveProjectStateSnapshot official,
        ExecutiveProjectStateProjectIdentity project,
        ExecutiveProjectStateCutoffIdentity cutoff)
    {
        if (official.SnapshotId == Guid.Empty ||
            !string.Equals(official.ProjectCode, project.Code, StringComparison.Ordinal) ||
            !string.Equals(official.ProjectName, project.Name, StringComparison.Ordinal) ||
            !Enum.IsDefined(official.Classification) ||
            string.IsNullOrWhiteSpace(official.CalculationVersion) ||
            official.ProjectConfigurationRevision <= 0 || official.AsOfDate == default ||
            official.AsOfDate > cutoff.CutoffLocalDate || official.CalculatedAt == default ||
            official.CalculatedAt.Offset != TimeSpan.Zero ||
            official.CalculatedAt > cutoff.SourceCutoffUtc || official.WindowStart == default ||
            official.WindowEnd == default || official.WindowStart > official.WindowEnd ||
            official.WindowEnd > official.AsOfDate || !Enum.IsDefined(official.AssessmentScope) ||
            !Enum.IsDefined(official.OperationalStatus) || !Enum.IsDefined(official.CoverageStatus) ||
            !Enum.IsDefined(official.FreshnessStatus) || !Enum.IsDefined(official.ConfidenceStatus) ||
            !Enum.IsDefined(official.CoverageBasis) || official.CoveragePercent is < 0 or > 100 ||
            official.ExpectedReportDays < 0 || official.ApprovedReportDays < 0 ||
            official.ApprovedReportDays > official.ExpectedReportDays ||
            official.LastApprovedReportDate > official.AsOfDate || official.FactCounts is null ||
            official.AttentionSummary is null || official.FeatureStates is null ||
            official.AttentionItems is null || official.SourceMaxChangedAt > official.CalculatedAt)
        {
            throw InvalidSnapshot("The official Executive Project State snapshot is invalid.");
        }

        if (official.FactCounts.Approved < 0 || official.FactCounts.Progress < 0 ||
            official.FactCounts.Labor < 0 || official.FactCounts.Equipment < 0 ||
            official.FactCounts.Material < 0 || official.AttentionSummary.Issues < 0 ||
            official.AttentionSummary.Stoppages < 0 || official.AttentionSummary.High < 0 ||
            official.AttentionSummary.Critical < 0 || official.AttentionSummary.OldestAgeDays < 0 ||
            !Enum.IsDefined(official.FeatureStates.Contract) ||
            !Enum.IsDefined(official.FeatureStates.Planning) ||
            !Enum.IsDefined(official.FeatureStates.Budget) ||
            !Enum.IsDefined(official.FeatureStates.Quality) ||
            !Enum.IsDefined(official.FeatureStates.Hse))
        {
            throw InvalidSnapshot("Executive Project State metric or feature-state values are invalid.");
        }

        ValidateAttention(official.AttentionItems, official);
    }

    private static void ValidateAttention(
        IReadOnlyCollection<ExecutiveProjectStateAttentionItem> attention,
        ExecutiveProjectStateSnapshot official)
    {
        foreach (var item in attention)
        {
            if (item is null || item.SourceReportId == Guid.Empty || item.SourceFactId == Guid.Empty ||
                item.ReportDate == default || item.ReportDate > official.AsOfDate ||
                !Enum.IsDefined(item.Kind) || string.IsNullOrWhiteSpace(item.Description) ||
                item.Description.Length > MaximumAttentionDescriptionLength ||
                item.Category?.Length > MaximumAttentionCategoryLength ||
                item.LocationId == Guid.Empty || item.LocationName?.Length > MaximumAttentionLocationLength ||
                item.ObservedImpact.HasValue && !Enum.IsDefined(item.ObservedImpact.Value) ||
                !Enum.IsDefined(item.Priority) || item.AgeDays < 0 || !Enum.IsDefined(item.AgeBand) ||
                !Enum.IsDefined(item.Status) || item.ReferenceCode?.Length > MaximumAttentionReferenceLength ||
                item.AgeDays != Math.Max(0, official.AsOfDate.DayNumber - item.ReportDate.DayNumber))
            {
                throw InvalidSnapshot("An Executive Project State attention item is invalid.");
            }
        }

        var canonical = attention
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => item.AgeDays)
            .ThenBy(item => item.ReportDate)
            .ThenBy(item => item.SourceReportId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(item => item.SourceFactId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var oldest = attention.Count == 0 ? (int?)null : attention.Max(item => item.AgeDays);
        if (attention.Select(item => item.SourceFactId).Distinct().Count() != attention.Count ||
            !attention.SequenceEqual(canonical) ||
            attention.Count(item => item.Kind == ProjectAttentionKind.Issue) != official.AttentionSummary.Issues ||
            attention.Count(item => item.Kind == ProjectAttentionKind.Stoppage) != official.AttentionSummary.Stoppages ||
            attention.Count(item => item.Priority == ProjectAttentionPriority.High) != official.AttentionSummary.High ||
            attention.Count(item => item.Priority == ProjectAttentionPriority.Critical) != official.AttentionSummary.Critical ||
            oldest != official.AttentionSummary.OldestAgeDays)
        {
            throw InvalidSnapshot("Executive Project State attention ordering or summary is invalid.");
        }
    }

    private static void ValidateTrend(
        IReadOnlyCollection<ExecutiveProjectStateTrendPoint> trend,
        ExecutiveProjectStateSnapshot? official,
        ExecutiveProjectStateCutoffIdentity cutoff)
    {
        foreach (var item in trend)
        {
            if (item is null || item.SnapshotId == Guid.Empty || string.IsNullOrWhiteSpace(item.CalculationVersion) ||
                item.AsOfDate == default || item.AsOfDate > cutoff.CutoffLocalDate ||
                item.CalculatedAt == default || item.CalculatedAt.Offset != TimeSpan.Zero ||
                item.CalculatedAt > cutoff.SourceCutoffUtc || !Enum.IsDefined(item.OperationalStatus) ||
                !Enum.IsDefined(item.CoverageStatus) || !Enum.IsDefined(item.FreshnessStatus) ||
                !Enum.IsDefined(item.ConfidenceStatus) || item.CoveragePercent is < 0 or > 100 ||
                item.IssueCount < 0 || item.StoppageCount < 0 ||
                item.HighImpactCount < 0 || item.CriticalImpactCount < 0)
            {
                throw InvalidSnapshot("An Executive Project State trend point is invalid.");
            }
        }

        var canonical = trend
            .OrderBy(item => item.AsOfDate)
            .ThenBy(item => item.CalculatedAt)
            .ThenBy(item => item.SnapshotId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (trend.Count > 14 || trend.Select(item => item.SnapshotId).Distinct().Count() != trend.Count ||
            trend.Select(item => item.AsOfDate).Distinct().Count() != trend.Count ||
            !trend.SequenceEqual(canonical) ||
            official is null && trend.Count > 0 ||
            official is not null && (trend.Count == 0 || trend.Last().SnapshotId != official.SnapshotId ||
                trend.Last().AsOfDate != official.AsOfDate ||
                trend.Last().CalculatedAt != official.CalculatedAt ||
                trend.Last().OperationalStatus != official.OperationalStatus ||
                trend.Last().CoverageStatus != official.CoverageStatus ||
                trend.Last().FreshnessStatus != official.FreshnessStatus ||
                trend.Last().ConfidenceStatus != official.ConfidenceStatus ||
                trend.Last().CoveragePercent != official.CoveragePercent ||
                trend.Last().IssueCount != official.AttentionSummary.Issues ||
                trend.Last().StoppageCount != official.AttentionSummary.Stoppages ||
                trend.Last().HighImpactCount != official.AttentionSummary.High ||
                trend.Last().CriticalImpactCount != official.AttentionSummary.Critical))
        {
            throw InvalidSnapshot("Executive Project State trend is invalid or non-canonical.");
        }
    }

    private static void ValidateStatus(ExecutiveProjectStateReportSemanticSnapshot snapshot)
    {
        var reasons = snapshot.ReasonCodes;
        var valid = snapshot.DataStatus switch
        {
            ReportDataStatus.NotConfigured =>
                snapshot.SourceState == ProjectStateReportingSourceState.NotConfigured &&
                snapshot.OfficialSnapshot is null && snapshot.Trend.Count == 0 &&
                reasons.SequenceEqual(
                    [ExecutiveProjectStateReportReasonCode.ProjectStateReportingNotConfigured]),
            ReportDataStatus.NoData =>
                snapshot.SourceState == ProjectStateReportingSourceState.Configured && reasons.Count > 0 &&
                (snapshot.OfficialSnapshot is null
                    ? snapshot.Trend.Count == 0 && reasons.SequenceEqual(
                        [ExecutiveProjectStateReportReasonCode.OfficialSnapshotMissing])
                    : reasons.Contains(ExecutiveProjectStateReportReasonCode.OfficialSnapshotNoData)),
            ReportDataStatus.InsufficientData =>
                snapshot.SourceState == ProjectStateReportingSourceState.Configured &&
                snapshot.OfficialSnapshot is not null && reasons.Count > 0,
            ReportDataStatus.Available =>
                snapshot.SourceState == ProjectStateReportingSourceState.Configured &&
                snapshot.OfficialSnapshot is not null && reasons.Count == 0,
            _ => false
        };
        if (!valid || snapshot.OfficialSnapshot is null &&
            (snapshot.Currency.ProjectConfigurationCurrent.HasValue ||
                snapshot.Currency.ApprovedSourceCurrent.HasValue))
        {
            throw InvalidSnapshot("Executive Project State data status is inconsistent with its semantic content.");
        }
    }

    private static int PriorityRank(ProjectAttentionPriority priority) => priority switch
    {
        ProjectAttentionPriority.Critical => 5,
        ProjectAttentionPriority.High => 4,
        ProjectAttentionPriority.Medium => 3,
        ProjectAttentionPriority.Low => 2,
        ProjectAttentionPriority.Unassessed => 1,
        _ => 0
    };

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static ReportRenderingException InvalidSnapshot(string message) => new(
        "reporting.executive_state.snapshot.payload_invalid",
        transient: false,
        message);

    private static ReportRenderingException InvalidRequest(string message) => new(
        "reporting.executive_state.render_request.invalid",
        transient: false,
        message);
}
