namespace Pmcs.BuildingBlocks.Application;

public enum ProjectMembershipBootstrapDisposition
{
    Added = 1,
    Skipped = 2,
    Conflict = 3,
    Blocked = 4
}

public sealed record ProjectMembershipBootstrapSelection(
    Guid UserId,
    string RoleCode,
    string AccessScope);

public sealed record ProjectMembershipBootstrapItem(
    Guid UserId,
    string DisplayName,
    string SourceRoleCode,
    string RequestedRoleCode,
    string AccessScope,
    string AccountStatus,
    string MembershipStatus,
    ProjectMembershipBootstrapDisposition Disposition,
    string Code,
    string Detail);

public sealed record ProjectMembershipBootstrapPreview(
    string ContributorId,
    string ContributorVersion,
    string SnapshotToken,
    IReadOnlyCollection<ProjectMembershipBootstrapItem> Items);

public sealed record ProjectMembershipBootstrapExecution(
    string ContributorId,
    string ContributorVersion,
    string SnapshotToken,
    IReadOnlyCollection<ProjectMembershipBootstrapItem> Items)
{
    public int AddedCount => Items.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Added);

    public int SkippedCount => Items.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Skipped);

    public int ConflictCount => Items.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Conflict);

    public int BlockedCount => Items.Count(item => item.Disposition == ProjectMembershipBootstrapDisposition.Blocked);
}

public sealed class ProjectMembershipBootstrapChangedException(string message) : Exception(message);

public sealed class ProjectMembershipBootstrapPermissionException(string message) : Exception(message);

public interface IProjectMembershipBootstrapService
{
    Task<ProjectMembershipBootstrapPreview> PreviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid sourceProjectId,
        Guid targetProjectId,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> selections,
        CancellationToken cancellationToken = default);

    Task<ProjectMembershipBootstrapExecution> ExecuteAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid planId,
        Guid sourceProjectId,
        Guid targetProjectId,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> selections,
        string expectedSnapshotToken,
        string correlationId,
        CancellationToken cancellationToken = default);
}
