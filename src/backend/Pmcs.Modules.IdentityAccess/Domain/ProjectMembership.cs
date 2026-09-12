using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class ProjectMembership : AggregateRoot
{
    private ProjectMembership()
    {
    }

    private ProjectMembership(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid userId,
        string roleCode,
        DateTimeOffset startsAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        UserId = userId;
        RoleCode = roleCode;
        StartsAt = startsAt;
        Status = MembershipStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    public string RoleCode { get; private set; } = string.Empty;

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset? EndsAt { get; private set; }

    public MembershipStatus Status { get; private set; }

    public static ProjectMembership Assign(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid userId,
        string roleCode,
        DateTimeOffset startsAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainRuleException("membership.identity.required", "Membership, tenant, project and user ids are required.");
        }

        if (!ProjectRoleCatalog.IsSupported(roleCode))
        {
            throw new DomainRuleException("membership.role.invalid", "Project role is not supported.");
        }

        return new ProjectMembership(id, tenantId, projectId, userId, roleCode.Trim(), startsAt);
    }

    public void ChangeRole(string roleCode)
    {
        ValidateRole(roleCode);
        RoleCode = roleCode.Trim();
        AdvanceRevision();
    }

    public void Activate(DateTimeOffset startsAt)
    {
        StartsAt = startsAt;
        EndsAt = null;
        Status = MembershipStatus.Active;
        AdvanceRevision();
    }

    public void Suspend()
    {
        Status = MembershipStatus.Suspended;
        AdvanceRevision();
    }

    public void Revoke(DateTimeOffset endedAt)
    {
        EndsAt = endedAt;
        Status = MembershipStatus.Revoked;
        AdvanceRevision();
    }

    private static void ValidateRole(string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode) || roleCode.Trim().Length > 100)
        {
            throw new DomainRuleException("membership.role.invalid", "A valid project role is required.");
        }
    }
}

public enum MembershipStatus
{
    Proposed = 1,
    Active = 2,
    Suspended = 3,
    Expired = 4,
    Revoked = 5
}
