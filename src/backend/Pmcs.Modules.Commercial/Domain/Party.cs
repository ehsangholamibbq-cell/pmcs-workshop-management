using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class Party : AggregateRoot
{
    private Party()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public PartyType Type { get; private set; }

    public string? NationalId { get; private set; }

    public string? ContactName { get; private set; }

    public string? Phone { get; private set; }

    public PartyStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public static Party Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string code,
        string legalName,
        PartyType type,
        string? nationalId,
        string? contactName,
        string? phone,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        CommercialRules.Identity(id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("commercial.party.type.invalid", "Party type is invalid.");
        }

        return new Party
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Code = NormalizeCode(code),
            LegalName = CommercialRules.Required(legalName, 200, "commercial.party.name.invalid"),
            Type = type,
            NationalId = CommercialRules.Optional(nationalId, 50, "commercial.party.national_id.too_long"),
            ContactName = CommercialRules.Optional(contactName, 160, "commercial.party.contact.too_long"),
            Phone = CommercialRules.Optional(phone, 50, "commercial.party.phone.too_long"),
            Status = PartyStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ChangedAt = createdAt
        };
    }

    public void Amend(
        long baseRevision,
        string legalName,
        PartyType type,
        string? nationalId,
        string? contactName,
        string? phone,
        DateTimeOffset changedAt)
    {
        CommercialRules.Revision(Revision, baseRevision, "commercial.party.revision.conflict");
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("commercial.party.type.invalid", "Party type is invalid.");
        }

        LegalName = CommercialRules.Required(legalName, 200, "commercial.party.name.invalid");
        Type = type;
        NationalId = CommercialRules.Optional(nationalId, 50, "commercial.party.national_id.too_long");
        ContactName = CommercialRules.Optional(contactName, 160, "commercial.party.contact.too_long");
        Phone = CommercialRules.Optional(phone, 50, "commercial.party.phone.too_long");
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    public void SetStatus(long baseRevision, PartyStatus status, DateTimeOffset changedAt)
    {
        CommercialRules.Revision(Revision, baseRevision, "commercial.party.revision.conflict");
        if (!Enum.IsDefined(status))
        {
            throw new DomainRuleException("commercial.party.status.invalid", "Party status is invalid.");
        }

        Status = status;
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    private static string NormalizeCode(string value)
    {
        var normalized = CommercialRules.Required(value, 32, "commercial.party.code.invalid").ToUpperInvariant();
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new DomainRuleException("commercial.party.code.invalid", "Party code may contain letters, numbers, dashes and underscores.");
        }

        return normalized;
    }
}

public enum PartyType
{
    Supplier = 1,
    Subcontractor = 2,
    Consultant = 3,
    LaborCrew = 4,
    Client = 5,
    Other = 6
}

public enum PartyStatus
{
    Active = 1,
    Inactive = 2
}
