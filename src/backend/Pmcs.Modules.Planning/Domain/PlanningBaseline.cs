using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Planning.Domain;

public sealed class PlanningBaseline : AggregateRoot
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private PlanningBaseline()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string VersionCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public PlanningBaselineKind Kind { get; private set; }

    public string? SourceSystem { get; private set; }

    public string? SourceReference { get; private set; }

    public string DefinitionJson { get; private set; } = "[]";

    public PlanningBaselineStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public IReadOnlyCollection<PlanningBaselineEntry> Entries =>
        JsonSerializer.Deserialize<PlanningBaselineEntry[]>(DefinitionJson, SerializerOptions) ?? [];

    public bool HasSchedule => Kind != PlanningBaselineKind.MeasurementWeights;

    public static PlanningBaseline Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string versionCode,
        string title,
        PlanningBaselineKind kind,
        string? sourceSystem,
        string? sourceReference,
        IReadOnlyCollection<PlanningBaselineEntry> entries,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "planning.baseline.identity.required",
                "Baseline, tenant, project and creator ids are required.");
        }

        ValidateKind(kind);
        var normalizedSourceSystem = Optional(sourceSystem, 120, "planning.baseline.source_system.too_long");
        var normalizedSourceReference = Optional(sourceReference, 500, "planning.baseline.source_reference.too_long");
        var normalizedEntries = NormalizeEntries(kind, entries);
        EnsureSource(kind, normalizedSourceSystem, normalizedSourceReference);

        return new PlanningBaseline
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            VersionCode = Required(versionCode, 80, "planning.baseline.version.invalid").ToUpperInvariant(),
            Title = Required(title, 240, "planning.baseline.title.invalid"),
            Kind = kind,
            SourceSystem = normalizedSourceSystem,
            SourceReference = normalizedSourceReference,
            DefinitionJson = JsonSerializer.Serialize(normalizedEntries, SerializerOptions),
            Status = PlanningBaselineStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Amend(
        long baseRevision,
        string title,
        string? sourceSystem,
        string? sourceReference,
        IReadOnlyCollection<PlanningBaselineEntry> entries)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        var normalizedSourceSystem = Optional(sourceSystem, 120, "planning.baseline.source_system.too_long");
        var normalizedSourceReference = Optional(sourceReference, 500, "planning.baseline.source_reference.too_long");
        var normalizedEntries = NormalizeEntries(Kind, entries);
        EnsureSource(Kind, normalizedSourceSystem, normalizedSourceReference);

        Title = Required(title, 240, "planning.baseline.title.invalid");
        SourceSystem = normalizedSourceSystem;
        SourceReference = normalizedSourceReference;
        DefinitionJson = JsonSerializer.Serialize(normalizedEntries, SerializerOptions);
        AdvanceRevision();
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        Status = PlanningBaselineStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        AdvanceRevision();
    }

    public void Approve(long baseRevision, string? comment, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = PlanningBaselineStatus.Approved;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        ReviewComment = Optional(comment, 1_000, "planning.baseline.review_comment.too_long");
        AdvanceRevision();
    }

    public void ReturnForCorrection(long baseRevision, string reason, Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = PlanningBaselineStatus.Returned;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        ReviewComment = Required(reason, 1_000, "planning.baseline.return_reason.invalid");
        AdvanceRevision();
    }

    public void Supersede(Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        if (Status != PlanningBaselineStatus.Approved)
        {
            throw new DomainRuleException(
                "planning.baseline.supersede.invalid_state",
                "Only an approved baseline can be superseded.");
        }

        Status = PlanningBaselineStatus.Superseded;
        ReviewedBy = RequiredActor(reviewedBy);
        ReviewedAt = reviewedAt;
        AdvanceRevision();
    }

    public bool Matches(PlanningMode mode) => (Kind, mode) switch
    {
        (PlanningBaselineKind.MeasurementWeights, PlanningMode.SimpleWorkList) => true,
        (PlanningBaselineKind.MilestonePlan, PlanningMode.Milestones) => true,
        (PlanningBaselineKind.WbsBaseline, PlanningMode.WbsBaseline) => true,
        (PlanningBaselineKind.ExternalSchedule, PlanningMode.ExternalSchedule) => true,
        _ => false
    };

    private static PlanningBaselineEntry[] NormalizeEntries(
        PlanningBaselineKind kind,
        IReadOnlyCollection<PlanningBaselineEntry> entries)
    {
        if (entries is null || entries.Count is < 1 or > 5_000)
        {
            throw new DomainRuleException(
                "planning.baseline.entries.invalid",
                "A baseline requires between one and 5000 entries.");
        }

        var normalized = entries
            .Select((entry, index) => NormalizeEntry(kind, entry, index + 1))
            .ToArray();
        if (normalized.Select(entry => entry.Id).Distinct().Count() != normalized.Length)
        {
            throw new DomainRuleException("planning.baseline.entry_id.duplicate", "Baseline entry ids must be unique.");
        }

        if (normalized.Select(entry => entry.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            throw new DomainRuleException("planning.baseline.entry_code.duplicate", "Baseline entry codes must be unique.");
        }

        var ids = normalized.Select(entry => entry.Id).ToHashSet();
        foreach (var entry in normalized.Where(entry => entry.ParentEntryId.HasValue))
        {
            if (entry.ParentEntryId == entry.Id || !ids.Contains(entry.ParentEntryId!.Value))
            {
                throw new DomainRuleException(
                    "planning.baseline.parent.invalid",
                    "Every parent entry must exist in the same baseline and cannot reference itself.");
            }

            if (normalized.Single(item => item.Id == entry.ParentEntryId.Value).Kind != PlanningEntryKind.Summary)
            {
                throw new DomainRuleException(
                    "planning.baseline.parent.not_summary",
                    "A parent entry must be a summary node.");
            }
        }

        EnsureNoCycles(normalized);
        var progressEntries = normalized.Where(entry => entry.Kind != PlanningEntryKind.Summary).ToArray();
        var weightTotal = progressEntries.Sum(entry => entry.WeightPercent ?? 0m);
        if (progressEntries.Any(entry => !entry.WeightPercent.HasValue) || weightTotal != 100m)
        {
            throw new DomainRuleException(
                "planning.baseline.weights.invalid",
                "Every progress entry requires a weight and the total must equal 100 percent.");
        }

        var mappedMeasurementIds = progressEntries
            .Where(entry => entry.MeasurementItemId.HasValue)
            .Select(entry => entry.MeasurementItemId!.Value)
            .ToArray();
        if (mappedMeasurementIds.Distinct().Count() != mappedMeasurementIds.Length)
        {
            throw new DomainRuleException(
                "planning.baseline.measurement_mapping.duplicate",
                "A measurement item can be mapped to only one weighted baseline entry.");
        }

        if (kind == PlanningBaselineKind.WbsBaseline && normalized.All(entry => entry.Kind != PlanningEntryKind.Summary))
        {
            throw new DomainRuleException(
                "planning.baseline.wbs_summary.required",
                "A WBS baseline requires at least one summary node.");
        }

        return normalized;
    }

    private static PlanningBaselineEntry NormalizeEntry(
        PlanningBaselineKind baselineKind,
        PlanningBaselineEntry entry,
        int defaultSortOrder)
    {
        if (entry.Id == Guid.Empty || !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.MeasurementMethod))
        {
            throw new DomainRuleException("planning.baseline.entry.invalid", "Baseline entry identity or type is invalid.");
        }

        var kind = entry.Kind;
        var method = entry.MeasurementMethod;
        if (kind == PlanningEntryKind.Summary)
        {
            if (entry.WeightPercent.HasValue || entry.MeasurementItemId.HasValue || method != ProgressMeasurementMethod.None)
            {
                throw new DomainRuleException(
                    "planning.baseline.summary.invalid",
                    "Summary entries cannot carry a weight, measurement mapping or progress method.");
            }
        }
        else
        {
            if (entry.WeightPercent is <= 0 or > 100 ||
                decimal.Round(entry.WeightPercent!.Value, 4, MidpointRounding.AwayFromZero) != entry.WeightPercent.Value)
            {
                throw new DomainRuleException(
                    "planning.baseline.entry_weight.invalid",
                    "Progress entry weight must be positive with at most four decimal places.");
            }

            if (kind == PlanningEntryKind.Milestone && method != ProgressMeasurementMethod.ManualPercent)
            {
                throw new DomainRuleException(
                    "planning.baseline.milestone_method.invalid",
                    "Milestone progress must use the reviewed manual percentage method.");
            }

            if (method == ProgressMeasurementMethod.QuantityBased && !entry.MeasurementItemId.HasValue)
            {
                throw new DomainRuleException(
                    "planning.baseline.measurement_item.required",
                    "Quantity-based progress requires a measurement item mapping.");
            }

            if (method == ProgressMeasurementMethod.ManualPercent && kind != PlanningEntryKind.Milestone)
            {
                throw new DomainRuleException(
                    "planning.baseline.manual_method.invalid",
                    "Manual percentage is allowed only for milestones.");
            }
        }

        if (baselineKind == PlanningBaselineKind.MeasurementWeights)
        {
            if (kind != PlanningEntryKind.MeasurementItem || method != ProgressMeasurementMethod.QuantityBased ||
                entry.PlannedStart.HasValue || entry.PlannedFinish.HasValue)
            {
                throw new DomainRuleException(
                    "planning.baseline.measurement_weights.invalid",
                    "A simple weighting basis contains only quantity-based measurement entries without schedule dates.");
            }
        }
        else
        {
            if (!entry.PlannedStart.HasValue || !entry.PlannedFinish.HasValue ||
                entry.PlannedFinish.Value < entry.PlannedStart.Value)
            {
                throw new DomainRuleException(
                    "planning.baseline.dates.invalid",
                    "Scheduled entries require valid planned start and finish dates.");
            }

            if (kind == PlanningEntryKind.Milestone && entry.PlannedStart != entry.PlannedFinish)
            {
                throw new DomainRuleException(
                    "planning.baseline.milestone_date.invalid",
                    "A milestone must use the same planned start and finish date.");
            }
        }

        if (baselineKind == PlanningBaselineKind.MilestonePlan && kind != PlanningEntryKind.Milestone)
        {
            throw new DomainRuleException(
                "planning.baseline.milestone_plan.invalid",
                "A milestone plan can contain milestone entries only.");
        }

        var externalId = Optional(entry.ExternalId, 200, "planning.baseline.external_id.too_long");
        if (baselineKind == PlanningBaselineKind.ExternalSchedule && externalId is null)
        {
            throw new DomainRuleException(
                "planning.baseline.external_id.required",
                "Every external schedule entry requires its source activity id.");
        }

        return new PlanningBaselineEntry(
            entry.Id,
            entry.ParentEntryId,
            Required(entry.Code, 80, "planning.baseline.entry_code.invalid").ToUpperInvariant(),
            Required(entry.Title, 240, "planning.baseline.entry_title.invalid"),
            kind,
            method,
            entry.MeasurementItemId,
            entry.PlannedStart,
            entry.PlannedFinish,
            entry.WeightPercent,
            externalId,
            entry.SortOrder > 0 ? entry.SortOrder : defaultSortOrder);
    }

    private static void EnsureNoCycles(IReadOnlyCollection<PlanningBaselineEntry> entries)
    {
        var parents = entries.ToDictionary(entry => entry.Id, entry => entry.ParentEntryId);
        foreach (var entry in entries)
        {
            var visited = new HashSet<Guid>();
            var current = entry.Id;
            while (parents[current].HasValue)
            {
                if (!visited.Add(current))
                {
                    throw new DomainRuleException("planning.baseline.hierarchy.cycle", "Baseline hierarchy cannot contain a cycle.");
                }

                current = parents[current]!.Value;
            }
        }
    }

    private static void EnsureSource(PlanningBaselineKind kind, string? sourceSystem, string? sourceReference)
    {
        if (kind == PlanningBaselineKind.ExternalSchedule && (sourceSystem is null || sourceReference is null))
        {
            throw new DomainRuleException(
                "planning.baseline.external_source.required",
                "External schedules require both source system and source reference.");
        }
    }

    private static void ValidateKind(PlanningBaselineKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainRuleException("planning.baseline.kind.invalid", "Planning baseline kind is invalid.");
        }
    }

    private void EnsureEditable()
    {
        if (Status is not PlanningBaselineStatus.Draft and not PlanningBaselineStatus.Returned)
        {
            throw new DomainRuleException(
                "planning.baseline.amend.invalid_state",
                "Only draft or returned baselines can be changed.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != PlanningBaselineStatus.Submitted)
        {
            throw new DomainRuleException(
                "planning.baseline.review.invalid_state",
                "Only a submitted baseline can be reviewed.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException(
                "planning.baseline.revision.conflict",
                "The baseline changed after it was loaded.");
        }
    }

    private static Guid RequiredActor(Guid actorId) => actorId != Guid.Empty
        ? actorId
        : throw new DomainRuleException("planning.baseline.reviewer.required", "Reviewer is required.");

    private static string Required(string value, int maximumLength, string errorCode) =>
        Optional(value, maximumLength, errorCode) ?? throw new DomainRuleException(errorCode, "A value is required.");

    private static string? Optional(string? value, int maximumLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(errorCode, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed record PlanningBaselineEntry(
    Guid Id,
    Guid? ParentEntryId,
    string Code,
    string Title,
    PlanningEntryKind Kind,
    ProgressMeasurementMethod MeasurementMethod,
    Guid? MeasurementItemId,
    DateOnly? PlannedStart,
    DateOnly? PlannedFinish,
    decimal? WeightPercent,
    string? ExternalId,
    int SortOrder);

public enum PlanningBaselineKind
{
    MeasurementWeights = 1,
    MilestonePlan = 2,
    WbsBaseline = 3,
    ExternalSchedule = 4
}

public enum PlanningEntryKind
{
    Summary = 1,
    MeasurementItem = 2,
    Activity = 3,
    Milestone = 4
}

public enum ProgressMeasurementMethod
{
    None = 0,
    QuantityBased = 1,
    ManualPercent = 2
}

public enum PlanningBaselineStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Superseded = 5
}
