using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Endpoints;

public enum ProjectBootstrapCategory
{
    BaseSettings = 1,
    Calendar = 2,
    Locations = 3,
    RoleTemplates = 4,
    WorkflowTemplates = 5,
    FormTemplates = 6,
    ReportTemplates = 7,
    Lookups = 8,
    Members = 9,
    NotificationDefaults = 10,
    GroupDefaults = 11
}

public sealed record ProjectBootstrapTargetRequest(
    string Code,
    string Name,
    ProjectType ProjectType,
    ProjectExecutionPhase ExecutionPhase,
    string CountryCode,
    string Region,
    DateOnly? StartDate,
    DateOnly? PlannedFinishDate,
    string ShortDescription,
    string TimeZone,
    string BaseCurrencyCode,
    ProjectUnitSystem UnitSystem,
    bool OfflinePolicyAccepted);

public sealed record ProjectBootstrapMemberSelectionRequest(
    Guid UserId,
    string RoleCode,
    string AccessScope = "Project");

public sealed record CreateProjectBootstrapRequest(
    Guid SourceProjectId,
    ProjectBootstrapTargetRequest Target,
    IReadOnlyCollection<ProjectBootstrapCategory> Categories,
    IReadOnlyCollection<ProjectBootstrapMemberSelectionRequest>? Members,
    ProjectBootstrapConflictPolicy ConflictPolicy = ProjectBootstrapConflictPolicy.FailOnConflict);

public sealed record RefreshProjectBootstrapPreviewRequest(long BaseRevision);

public sealed record ExecuteProjectBootstrapRequest(long BaseRevision, string PreviewDigest);

public sealed record ActivateProjectBootstrapRequest(long BaseRevision, long TargetBaseRevision);

public sealed record ProjectBootstrapContributorResponse(
    string ContributorId,
    string SchemaVersion,
    ProjectBootstrapCategory Category,
    string Permission,
    int Order,
    IReadOnlyCollection<string> Dependencies,
    IReadOnlyCollection<string> Allowlist,
    string ConflictPolicy,
    string FailSafeBehavior,
    string PostValidation);

public sealed record ProjectBootstrapItemResponse(
    string ContributorId,
    ProjectBootstrapCategory Category,
    ProjectMembershipBootstrapDisposition Disposition,
    string Code,
    string Title,
    string Detail,
    string? SourceReference,
    string? TargetReference);

public sealed record ProjectBootstrapSummaryResponse(
    int Added,
    int Skipped,
    int Conflicts,
    int Blocked)
{
    public static ProjectBootstrapSummaryResponse From(
        IEnumerable<ProjectBootstrapItemResponse> items)
    {
        var snapshot = items.ToArray();
        return new ProjectBootstrapSummaryResponse(
            snapshot.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Added),
            snapshot.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Skipped),
            snapshot.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Conflict),
            snapshot.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Blocked));
    }
}

public sealed record ProjectBootstrapPreviewResponse(
    Guid PlanId,
    Guid SourceProjectId,
    string SourceProjectCode,
    Guid TargetProjectId,
    ProjectResponse TargetProject,
    ProjectBootstrapStatus Status,
    ProjectBootstrapConflictPolicy ConflictPolicy,
    IReadOnlyCollection<ProjectBootstrapCategory> SelectedCategories,
    string ContributorCatalogVersion,
    string PreviewDigest,
    DateTimeOffset PreviewedAt,
    DateTimeOffset PreviewExpiresAt,
    IReadOnlyCollection<ProjectBootstrapContributorResponse> Contributors,
    IReadOnlyCollection<ProjectBootstrapItemResponse> Items,
    ProjectBootstrapSummaryResponse Summary,
    IReadOnlyCollection<string> AlwaysExcluded,
    long PlanRevision);

public sealed record ProjectBootstrapValidationResponse(
    string Code,
    bool Passed,
    string Detail);

public sealed record ProjectBootstrapResultResponse(
    Guid PlanId,
    Guid SourceProjectId,
    Guid TargetProjectId,
    ProjectResponse TargetProject,
    ProjectBootstrapStatus Status,
    string ContributorCatalogVersion,
    string PreviewDigest,
    IReadOnlyCollection<ProjectBootstrapItemResponse> Items,
    ProjectBootstrapSummaryResponse Summary,
    IReadOnlyCollection<ProjectBootstrapValidationResponse> Validation,
    DateTimeOffset ExecutedAt,
    long PlanRevision);

internal sealed record ProjectBootstrapPreviewDocument(
    string SourceProjectCode,
    IReadOnlyCollection<ProjectBootstrapCategory> SelectedCategories,
    IReadOnlyCollection<ProjectBootstrapContributorResponse> Contributors,
    IReadOnlyCollection<ProjectBootstrapItemResponse> Items,
    IReadOnlyCollection<string> AlwaysExcluded);
