using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Planning.Domain;

public sealed class MeasurementItem : AggregateRoot
{
    private MeasurementItem()
    {
    }

    private MeasurementItem(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string code,
        string title,
        string unit,
        decimal? targetQuantity,
        string? notes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Code = NormalizeRequired(code, 80, "measurement_item.code.invalid").ToUpperInvariant();
        Title = NormalizeRequired(title, 240, "measurement_item.title.invalid");
        Unit = NormalizeRequired(unit, 40, "measurement_item.unit.invalid");
        TargetQuantity = ValidateTarget(targetQuantity);
        Notes = NormalizeOptional(notes, 2_000, "measurement_item.notes.too_long");
        Status = MeasurementItemStatus.Active;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        LastModifiedBy = createdBy;
        LastModifiedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Unit { get; private set; } = string.Empty;

    public decimal? TargetQuantity { get; private set; }

    public string? Notes { get; private set; }

    public MeasurementItemStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid LastModifiedBy { get; private set; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public static MeasurementItem Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string code,
        string title,
        string unit,
        decimal? targetQuantity,
        string? notes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "measurement_item.identity.required",
                "Measurement item, tenant, project and creator ids are required.");
        }

        return new MeasurementItem(
            id, tenantId, projectId, code, title, unit, targetQuantity, notes, createdBy, createdAt);
    }

    public void Amend(
        long baseRevision,
        string title,
        decimal? targetQuantity,
        string? notes,
        Guid changedBy,
        DateTimeOffset changedAt)
    {
        EnsureActive();
        EnsureRevision(baseRevision);
        if (changedBy == Guid.Empty)
        {
            throw new DomainRuleException("measurement_item.actor.required", "Actor id is required.");
        }

        Title = NormalizeRequired(title, 240, "measurement_item.title.invalid");
        TargetQuantity = ValidateTarget(targetQuantity);
        Notes = NormalizeOptional(notes, 2_000, "measurement_item.notes.too_long");
        LastModifiedBy = changedBy;
        LastModifiedAt = changedAt;
        AdvanceRevision();
    }

    public void Deactivate(long baseRevision, Guid changedBy, DateTimeOffset changedAt)
    {
        EnsureActive();
        EnsureRevision(baseRevision);
        if (changedBy == Guid.Empty)
        {
            throw new DomainRuleException("measurement_item.actor.required", "Actor id is required.");
        }

        Status = MeasurementItemStatus.Inactive;
        LastModifiedBy = changedBy;
        LastModifiedAt = changedAt;
        AdvanceRevision();
    }

    private void EnsureActive()
    {
        if (Status != MeasurementItemStatus.Active)
        {
            throw new DomainRuleException("measurement_item.inactive", "Inactive measurement items cannot be changed.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException("measurement_item.revision.conflict", "Measurement item changed after it was loaded.");
        }
    }

    private static decimal? ValidateTarget(decimal? value)
    {
        if (value is <= 0 or > 1_000_000_000_000m)
        {
            throw new DomainRuleException(
                "measurement_item.target.invalid",
                "Target quantity must be greater than zero and within the supported range.");
        }

        return value;
    }

    private static string NormalizeRequired(string value, int maximum, string code)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximum)
        {
            throw new DomainRuleException(code, $"Value is required and must be at most {maximum} characters.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value, int maximum, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximum)
        {
            throw new DomainRuleException(code, $"Value must be at most {maximum} characters.");
        }

        return normalized;
    }
}

public enum MeasurementItemStatus
{
    Active = 1,
    Inactive = 2
}
