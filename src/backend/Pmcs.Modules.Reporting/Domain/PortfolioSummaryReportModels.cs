using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

internal static class PortfolioSummaryReportRuntimeContract
{
    public const string DefinitionCode = "portfolio-summary-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string TemplateVersion = "1.0.0";
    public const string TemplateContentDigest =
        "9a2df8c64c6f4d461130f00cd106822d529e713de0a97b9ee72c90468bd8115f";
    public const string RendererContractVersion = "pmcs.reporting.portfolio-summary.renderer/v1";
    public const string LayoutContractVersion = "pmcs.reporting.portfolio-summary.layout/v1";
    public const string SnapshotSchemaVersion = "pmcs.reporting.portfolio-summary.snapshot/v1";
    public const string SourceManifestVersion = "pmcs.reporting.portfolio-summary.source-manifest/v1";
    public const int MaximumProjects = 200;
}

internal enum PortfolioDimensionStatus
{
    NotAuthorized = 1,
    NotConfigured = 2,
    NotEnabled = 3,
    SetupRequired = 4,
    Suspended = 5,
    NoData = 6,
    InsufficientData = 7,
    Available = 8
}

internal sealed record PortfolioFinancialDimension(
    PortfolioDimensionStatus Status,
    string? CurrencyCode,
    decimal? RecognizedSpend,
    decimal? ExternalNetCash,
    string? SourceManifestSha256,
    string? ReasonCode,
    ReportClassification Classification);

internal sealed record PortfolioCommercialDimension(
    PortfolioDimensionStatus Status,
    string? CurrencyCode,
    decimal? TotalCommittedAmount,
    decimal? OpenCommitmentAmount,
    string? SourceManifestSha256,
    string? ReasonCode,
    ReportClassification Classification);

internal sealed record PortfolioProjectSelection(
    Guid ProjectId,
    string? Code,
    string? Name,
    ProjectStatus Lifecycle,
    DateOnly CutoffLocalDate,
    string TimeZone,
    string? BaseCurrencyCode,
    long? ConfigurationVersion,
    DateTimeOffset? ConfigurationChangedAtUtc,
    bool ConfigurationProvenAtCutoff,
    string PermissionPolicyVersion,
    PortfolioDimensionStatus OperationalStatus,
    ProjectOperationalStatus? OperationalAssessment,
    DataCoverageStatus? Coverage,
    DataFreshnessStatus? Freshness,
    DataConfidenceStatus? Confidence,
    bool? IsPartial,
    Guid? OperationalSnapshotId,
    DateTimeOffset? OperationalWatermarkUtc,
    string? OperationalSourceSha256,
    string? OperationalReasonCode,
    PortfolioFinancialDimension Financial,
    PortfolioCommercialDimension Commercial,
    ReportClassification Classification);

internal sealed record PortfolioSummarySelection(
    Guid TenantId,
    Guid RequestedBy,
    DateTimeOffset SourceCutoffUtc,
    IReadOnlyCollection<PortfolioProjectSelection> Projects);

internal sealed record PortfolioPinnedProject(
    Guid ProjectId, bool FinancialAuthorized, bool CommercialAuthorized,
    string PermissionPolicyVersion);

internal sealed record PortfolioPinnedCohort(
    string SchemaVersion, Guid TenantId, Guid RequestedBy, DateTimeOffset AsOfUtc,
    IReadOnlyCollection<PortfolioPinnedProject> Projects)
{
    public const string Version = "pmcs.reporting.portfolio-summary.pinned-cohort/v1";

    public void Validate()
    {
        if (SchemaVersion != Version || TenantId == Guid.Empty || RequestedBy == Guid.Empty ||
            AsOfUtc == default || AsOfUtc.Offset != TimeSpan.Zero || Projects is null ||
            Projects.Count > PortfolioSummaryReportRuntimeContract.MaximumProjects ||
            Projects.Any(item => item is null || item.ProjectId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.PermissionPolicyVersion)) ||
            Projects.Select(item => item.ProjectId).Distinct().Count() != Projects.Count ||
            !Projects.Select(item => item.ProjectId).SequenceEqual(
                Projects.Select(item => item.ProjectId).Order()))
            throw new Pmcs.BuildingBlocks.Domain.DomainRuleException(
                "reporting.portfolio.cohort.invalid", "Pinned Portfolio cohort is invalid.");
    }
}

internal sealed record PortfolioCurrencyGroup(
    string CurrencyCode,
    int FinancialContributorCount,
    decimal? RecognizedSpendSubtotal,
    decimal? ExternalNetCashSubtotal,
    int CommercialContributorCount,
    decimal? TotalCommittedSubtotal,
    decimal? OpenCommitmentSubtotal,
    bool HasIncompleteFinancial,
    bool HasIncompleteCommercial);

internal sealed record PortfolioSummarySemanticSnapshot(
    string SchemaVersion,
    string DefinitionCode,
    DateTimeOffset AsOfUtc,
    Guid TenantId,
    int AuthorizedProjectCount,
    ReportDataStatus DataStatus,
    IReadOnlyCollection<PortfolioProjectSelection> Projects,
    IReadOnlyCollection<PortfolioCurrencyGroup> CurrencyGroups,
    string SourceManifestSha256);

internal sealed record PortfolioProjectSourceManifest(
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    string TimeZone,
    long? ConfigurationVersion,
    DateTimeOffset? ConfigurationChangedAtUtc,
    string PermissionPolicyVersion,
    bool FinancialAuthorized,
    bool CommercialAuthorized,
    ReportClassification Classification,
    Guid? OperationalSnapshotId,
    DateTimeOffset? OperationalWatermarkUtc,
    string? OperationalSourceSha256,
    string? FinancialSourceManifestSha256,
    string? CommercialSourceManifestSha256);

internal sealed record PortfolioSummarySourceManifest(
    string Version,
    string ProjectStateSourceContractVersion,
    string FinancialSourceContractVersion,
    string CommercialSourceContractVersion,
    Guid TenantId,
    DateTimeOffset SourceCutoffUtc,
    IReadOnlyCollection<PortfolioProjectSourceManifest> Projects);
