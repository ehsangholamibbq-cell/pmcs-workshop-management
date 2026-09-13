namespace Pmcs.BuildingBlocks.Application;

public sealed record ProjectPermissionScope(
    bool AllProjects,
    IReadOnlySet<Guid> ProjectIds);

public sealed record ProjectAccessReadiness(
    int ActiveAdministratorCount,
    int ActiveProjectManagerCount,
    int ActiveOperationalUserCount);

public sealed record EffectivePermissionDecision(
    string Operation,
    bool Allowed,
    string Source,
    string Scope,
    string Condition,
    string? DenyReason,
    DateTimeOffset? ExpiresAt,
    string DelegationEffect);

public sealed record EffectivePermissionPreview(
    Guid UserId,
    Guid ProjectId,
    string AccountStatus,
    string? TenantRole,
    string? ProjectRole,
    string PolicyVersion,
    DateTimeOffset EvaluatedAt,
    IReadOnlyCollection<EffectivePermissionDecision> Decisions);

public interface IProjectPermissionService
{
    Task<bool> HasTenantPermissionAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default);

    Task<bool> HasProjectPermissionAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken = default);

    Task<ProjectPermissionScope> GetProjectScopeAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default);

    Task<ProjectAccessReadiness> GetProjectAccessReadinessAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<EffectivePermissionPreview> PreviewProjectPermissionsAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string? proposedProjectRoleCode = null,
        IReadOnlyCollection<string>? operations = null,
        CancellationToken cancellationToken = default);
}
