using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Projects.Domain;

public sealed class ProjectLocation : AggregateRoot
{
    private ProjectLocation()
    {
    }

    private ProjectLocation(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string code,
        string name,
        Guid? parentLocationId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Code = code;
        Name = name;
        ParentLocationId = parentLocationId;
        Status = ProjectLocationStatus.Active;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        ChangedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid? ParentLocationId { get; private set; }

    public ProjectLocationStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public static ProjectLocation Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string code,
        string name,
        Guid? parentLocationId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("project.location.identity.required", "Location, project, tenant and creator ids are required.");
        }

        if (parentLocationId == Guid.Empty || parentLocationId == id)
        {
            throw new DomainRuleException("project.location.parent.invalid", "A location cannot be its own parent.");
        }

        var normalizedCode = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedCode.Length is < 1 or > 40 ||
            normalizedCode.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new DomainRuleException(
                "project.location.code.invalid",
                "Location code must be 1-40 letters, numbers, dots, dashes or underscores.");
        }

        var isRoot = string.Equals(normalizedCode, "ROOT", StringComparison.Ordinal);
        if (isRoot != (parentLocationId is null))
        {
            throw new DomainRuleException(
                "project.location.hierarchy.invalid",
                "Only the ROOT location can omit a parent, and ROOT cannot have a parent.");
        }

        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 1 or > 200)
        {
            throw new DomainRuleException("project.location.name.invalid", "Location name is required and must be at most 200 characters.");
        }

        return new ProjectLocation(
            id,
            tenantId,
            projectId,
            normalizedCode,
            normalizedName,
            parentLocationId,
            createdBy,
            createdAt);
    }

    public void Retire(long baseRevision, DateTimeOffset changedAt)
    {
        EnsureRevision(baseRevision);
        if (Status != ProjectLocationStatus.Active)
        {
            throw new DomainRuleException("project.location.retire.invalid_state", "Only an active location can be retired.");
        }

        if (ParentLocationId is null)
        {
            throw new DomainRuleException("project.location.root.retire.denied", "The project root location cannot be retired.");
        }

        Status = ProjectLocationStatus.Retired;
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException("project.location.revision.conflict", "The location changed after it was loaded.");
        }
    }
}

public enum ProjectLocationStatus
{
    Active = 1,
    Retired = 2
}
