using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class UserAccount : AggregateRoot
{
    private UserAccount()
    {
    }

    private UserAccount(
        Guid id,
        Guid tenantId,
        string displayName,
        string email,
        TenantRole tenantRole,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        DisplayName = displayName;
        Email = email;
        TenantRole = tenantRole;
        Status = UserAccountStatus.Active;
        CreatedAt = createdAt;
        AccessValidAfter = createdAt.AddSeconds(-1);
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public TenantRole TenantRole { get; private set; }

    public UserAccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset AccessValidAfter { get; private set; }

    public static UserAccount Create(
        Guid id,
        Guid tenantId,
        string displayName,
        string email,
        TenantRole tenantRole,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new DomainRuleException("user.identity.required", "User and tenant ids are required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainRuleException("user.display_name.required", "Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainRuleException("user.email.invalid", "A valid email is required.");
        }

        if (!Enum.IsDefined(tenantRole))
        {
            throw new DomainRuleException("user.tenant_role.invalid", "Tenant role is invalid.");
        }

        return new UserAccount(id, tenantId, displayName.Trim(), email.Trim().ToLowerInvariant(), tenantRole, createdAt);
    }

    public void Suspend(DateTimeOffset at)
    {
        if (Status is UserAccountStatus.Deactivated)
        {
            throw new DomainRuleException("user.status.terminal", "A deactivated account cannot be suspended.");
        }

        Status = UserAccountStatus.Suspended;
        AccessValidAfter = at;
        AdvanceRevision();
    }

    public void Reactivate(DateTimeOffset at)
    {
        if (Status is UserAccountStatus.Deactivated)
        {
            throw new DomainRuleException("user.status.terminal", "A deactivated account cannot be reactivated.");
        }

        Status = UserAccountStatus.Active;
        AccessValidAfter = at;
        AdvanceRevision();
    }

    public void Deactivate(DateTimeOffset at)
    {
        Status = UserAccountStatus.Deactivated;
        AccessValidAfter = at;
        AdvanceRevision();
    }

    public void ChangeTenantRole(TenantRole tenantRole)
    {
        if (!Enum.IsDefined(tenantRole))
        {
            throw new DomainRuleException("user.tenant_role.invalid", "Tenant role is invalid.");
        }

        TenantRole = tenantRole;
        AdvanceRevision();
    }
}

public enum TenantRole
{
    Member = 1,
    PortfolioViewer = 2,
    TenantAdministrator = 3
}

public enum UserAccountStatus
{
    Invited = 1,
    Active = 2,
    Suspended = 3,
    Deactivated = 4
}
