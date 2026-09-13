using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Persistence;

internal sealed class ProjectStateSnapshot
{
    private readonly List<ProjectStateAttentionItem> _attentionItems = [];

    private ProjectStateSnapshot()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string ProjectCode { get; private set; } = string.Empty;

    public string ProjectName { get; private set; } = string.Empty;

    public string CalculationVersion { get; private set; } = string.Empty;

    public long ProjectConfigurationRevision { get; private set; }

    public DateOnly AsOfDate { get; private set; }

    public DateOnly WindowStart { get; private set; }

    public DateOnly WindowEnd { get; private set; }

    public DateTimeOffset CalculatedAt { get; private set; }

    public ProjectAssessmentScope AssessmentScope { get; private set; }

    public bool IsPartial { get; private set; }

    public ProjectOperationalStatus OperationalStatus { get; private set; }

    public DataCoverageStatus CoverageStatus { get; private set; }

    public DataFreshnessStatus FreshnessStatus { get; private set; }

    public DataConfidenceStatus ConfidenceStatus { get; private set; }

    public ProjectCoverageBasis CoverageBasis { get; private set; }

    public decimal CoveragePercent { get; private set; }

    public int ExpectedReportDays { get; private set; }

    public int ApprovedReportDays { get; private set; }

    public DateOnly? LastApprovedReportDate { get; private set; }

    public int ApprovedFactCount { get; private set; }

    public int ProgressFactCount { get; private set; }

    public int LaborFactCount { get; private set; }

    public int EquipmentFactCount { get; private set; }

    public int MaterialFactCount { get; private set; }

    public int IssueCount { get; private set; }

    public int StoppageCount { get; private set; }

    public int HighImpactCount { get; private set; }

    public int CriticalImpactCount { get; private set; }

    public int? OldestAttentionAgeDays { get; private set; }

    public ProjectFeatureState ContractState { get; private set; }

    public ProjectFeatureState PlanningState { get; private set; }

    public ProjectFeatureState BudgetState { get; private set; }

    public ProjectFeatureState QualityState { get; private set; }

    public ProjectFeatureState HseState { get; private set; }

    public DateTimeOffset? SourceMaxChangedAt { get; private set; }

    public IReadOnlyCollection<ProjectStateAttentionItem> AttentionItems => _attentionItems.AsReadOnly();

    public static ProjectStateSnapshot Create(Guid id, ProjectStateCalculation calculation)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Snapshot id is required.", nameof(id));
        }

        var snapshot = new ProjectStateSnapshot
        {
            Id = id,
            TenantId = calculation.TenantId,
            ProjectId = calculation.ProjectId,
            ProjectCode = calculation.ProjectCode,
            ProjectName = calculation.ProjectName,
            CalculationVersion = calculation.CalculationVersion,
            ProjectConfigurationRevision = calculation.ProjectConfigurationRevision,
            AsOfDate = calculation.AsOfDate,
            WindowStart = calculation.WindowStart,
            WindowEnd = calculation.WindowEnd,
            CalculatedAt = calculation.CalculatedAt,
            AssessmentScope = calculation.AssessmentScope,
            IsPartial = calculation.IsPartial,
            OperationalStatus = calculation.OperationalStatus,
            CoverageStatus = calculation.CoverageStatus,
            FreshnessStatus = calculation.FreshnessStatus,
            ConfidenceStatus = calculation.ConfidenceStatus,
            CoverageBasis = calculation.CoverageBasis,
            CoveragePercent = calculation.CoveragePercent,
            ExpectedReportDays = calculation.ExpectedReportDays,
            ApprovedReportDays = calculation.ApprovedReportDays,
            LastApprovedReportDate = calculation.LastApprovedReportDate,
            ApprovedFactCount = calculation.ApprovedFactCount,
            ProgressFactCount = calculation.ProgressFactCount,
            LaborFactCount = calculation.LaborFactCount,
            EquipmentFactCount = calculation.EquipmentFactCount,
            MaterialFactCount = calculation.MaterialFactCount,
            IssueCount = calculation.IssueCount,
            StoppageCount = calculation.StoppageCount,
            HighImpactCount = calculation.HighImpactCount,
            CriticalImpactCount = calculation.CriticalImpactCount,
            OldestAttentionAgeDays = calculation.OldestAttentionAgeDays,
            ContractState = calculation.ContractState,
            PlanningState = calculation.PlanningState,
            BudgetState = calculation.BudgetState,
            QualityState = calculation.QualityState,
            HseState = calculation.HseState,
            SourceMaxChangedAt = calculation.SourceMaxChangedAt
        };

        snapshot._attentionItems.AddRange(calculation.AttentionItems.Select(item => ProjectStateAttentionItem.Create(snapshot.Id, item)));
        return snapshot;
    }
}

internal sealed class ProjectStateAttentionItem
{
    private ProjectStateAttentionItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid SnapshotId { get; private set; }

    public Guid SourceReportId { get; private set; }

    public Guid SourceFactId { get; private set; }

    public DateOnly ReportDate { get; private set; }

    public ProjectAttentionKind Kind { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? Category { get; private set; }

    public string? LocationName { get; private set; }

    public Guid? LocationId { get; private set; }

    public ProjectObservedImpact? ObservedImpact { get; private set; }

    public ProjectAttentionPriority Priority { get; private set; }

    public int AgeDays { get; private set; }

    public ProjectAttentionAgeBand AgeBand { get; private set; }

    public ProjectAttentionStatus Status { get; private set; }

    public string? ReferenceCode { get; private set; }

    public static ProjectStateAttentionItem Create(
        Guid snapshotId,
        ProjectAttentionCalculation calculation) => new()
        {
            Id = Guid.NewGuid(),
            SnapshotId = snapshotId,
            SourceReportId = calculation.SourceReportId,
            SourceFactId = calculation.SourceFactId,
            ReportDate = calculation.ReportDate,
            Kind = calculation.Kind,
            Description = calculation.Description,
            Category = calculation.Category,
            LocationName = calculation.LocationName,
            LocationId = calculation.LocationId,
            ObservedImpact = calculation.ObservedImpact,
            Priority = calculation.Priority,
            AgeDays = calculation.AgeDays,
            AgeBand = calculation.AgeBand,
            Status = calculation.Status,
            ReferenceCode = calculation.ReferenceCode
        };
}
