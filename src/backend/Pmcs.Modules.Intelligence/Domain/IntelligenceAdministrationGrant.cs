namespace Pmcs.Modules.Intelligence.Domain;

// An explicit grant, independent of the legacy tenant/project role wildcards.
internal sealed class IntelligenceAdministrationGrant
{
    public Guid Id { get; private set; }
    public Guid ActorTenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid? ScopeTenantId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
    public Guid IssuedBy { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public long Revision { get; private set; }

    private IntelligenceAdministrationGrant() { }

    internal static IntelligenceAdministrationGrant Issue(
        Guid id, Guid actorTenantId, Guid actorId, Guid? scopeTenantId,
        string permission, Guid issuedBy, DateTimeOffset startsAt, DateTimeOffset? expiresAt)
    {
        if (id == Guid.Empty || actorTenantId == Guid.Empty || actorId == Guid.Empty || issuedBy == Guid.Empty ||
            (scopeTenantId.HasValue && scopeTenantId == Guid.Empty) ||
            (expiresAt.HasValue && expiresAt <= startsAt) ||
            !IntelligenceAdministrationPermissions.IsValidScope(permission, scopeTenantId))
        {
            throw new ArgumentException("Invalid intelligence administration grant.");
        }

        return new IntelligenceAdministrationGrant
        {
            Id = id, ActorTenantId = actorTenantId, ActorId = actorId,
            ScopeTenantId = scopeTenantId, Permission = permission, IssuedBy = issuedBy,
            StartsAt = startsAt, ExpiresAt = expiresAt, Revision = 1
        };
    }

    internal bool Allows(Guid actorTenantId, Guid actorId, Guid? scopeTenantId,
        string permission, DateTimeOffset now) =>
        ActorTenantId == actorTenantId && ActorId == actorId &&
        ScopeTenantId == scopeTenantId &&
        string.Equals(Permission, permission, StringComparison.Ordinal) &&
        RevokedAt is null && StartsAt <= now && (ExpiresAt is null || ExpiresAt > now);

    internal void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null || now < StartsAt)
        {
            throw new InvalidOperationException("Grant cannot be revoked in this state.");
        }

        RevokedAt = now;
        Revision++;
    }
}

internal static class IntelligenceAdministrationPermissions
{
    internal const string ProvidersManage = "intelligence.providers.manage";
    internal const string ProfilesPublish = "intelligence.profiles.publish";
    internal const string ProfilesSelect = "intelligence.profiles.select";
    internal const string CatalogRead = "intelligence.catalog.read";

    internal static bool IsValidScope(string permission, Guid? tenantId) =>
        tenantId is null
            ? permission is ProvidersManage or ProfilesPublish or CatalogRead
            : permission is ProfilesSelect or CatalogRead;
}
