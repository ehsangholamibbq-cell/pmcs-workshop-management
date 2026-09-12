using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class Tenant : AggregateRoot
{
    private Tenant()
    {
    }

    private Tenant(Guid id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Status = TenantStatus.Active;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Create(Guid id, string name, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException("tenant.id.required", "Tenant id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("tenant.name.required", "Tenant name is required.");
        }

        return new Tenant(id, name.Trim(), createdAt);
    }
}

public enum TenantStatus
{
    Active = 1,
    Suspended = 2,
    Deactivated = 3
}
