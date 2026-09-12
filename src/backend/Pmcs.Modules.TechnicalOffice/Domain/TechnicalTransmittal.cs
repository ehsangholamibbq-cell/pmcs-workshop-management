using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.TechnicalOffice.Domain;

public sealed class TechnicalTransmittal : AggregateRoot
{
    private TechnicalTransmittal() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Sender { get; private set; } = string.Empty;
    public string RecipientsJson { get; private set; } = "[]";
    public string RevisionIdsJson { get; private set; } = "[]";
    public string Purpose { get; private set; } = string.Empty;
    public string DeliveryChannel { get; private set; } = string.Empty;
    public DateOnly? DueResponseDate { get; private set; }
    public TransmittalStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? IssuedAt { get; private set; }
    public Guid? IssuedBy { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public Guid? AcknowledgedBy { get; private set; }
    public string? AcknowledgmentReference { get; private set; }

    public IReadOnlyCollection<string> Recipients =>
        JsonSerializer.Deserialize<string[]>(RecipientsJson, TechnicalOfficeRules.JsonOptions) ?? [];
    public IReadOnlyCollection<Guid> RevisionIds => TechnicalOfficeRules.DeserializeIds(RevisionIdsJson);

    public static TechnicalTransmittal Create(
        Guid id, Guid tenantId, Guid projectId, string sender, IReadOnlyCollection<string> recipients,
        IReadOnlyCollection<Guid> revisionIds, string purpose, string deliveryChannel,
        DateOnly? dueResponseDate, Guid createdBy, DateTimeOffset createdAt)
    {
        TechnicalOfficeRules.Identity(id, tenantId, projectId, createdBy);
        var normalizedRecipients = recipients?
            .Select(value => TechnicalOfficeRules.Required(value, 240, "technical.transmittal.recipient.invalid"))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (normalizedRecipients.Length is < 1 or > 50)
        {
            throw new DomainRuleException("technical.transmittal.recipients.invalid", "One to 50 recipients are required.");
        }

        var normalizedRevisions = TechnicalOfficeRules.SerializeIds(revisionIds, 100, "technical.transmittal.revisions.invalid");
        if (TechnicalOfficeRules.DeserializeIds(normalizedRevisions).Count == 0)
        {
            throw new DomainRuleException("technical.transmittal.revisions.required", "At least one document revision is required.");
        }

        return new TechnicalTransmittal
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Number = TechnicalOfficeRules.OfficialNumber("TRN", createdAt, id),
            Sender = TechnicalOfficeRules.Required(sender, 240, "technical.transmittal.sender.invalid"),
            RecipientsJson = JsonSerializer.Serialize(normalizedRecipients, TechnicalOfficeRules.JsonOptions),
            RevisionIdsJson = normalizedRevisions,
            Purpose = TechnicalOfficeRules.Required(purpose, 500, "technical.transmittal.purpose.invalid"),
            DeliveryChannel = TechnicalOfficeRules.Required(deliveryChannel, 120, "technical.transmittal.channel.invalid"),
            DueResponseDate = dueResponseDate,
            Status = TransmittalStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Issue(long baseRevision, Guid issuer, DateTimeOffset at)
    {
        TechnicalOfficeRules.Revision(Revision, baseRevision, "technical.transmittal.revision.conflict");
        TechnicalOfficeRules.Identity(issuer);
        if (Status != TransmittalStatus.Draft)
        {
            throw new DomainRuleException("technical.transmittal.issue.invalid_state", "Only a draft transmittal can be issued.");
        }

        Status = TransmittalStatus.Issued;
        IssuedBy = issuer;
        IssuedAt = at;
        AdvanceRevision();
    }

    public void Acknowledge(long baseRevision, Guid actor, DateTimeOffset at, string reference)
    {
        TechnicalOfficeRules.Revision(Revision, baseRevision, "technical.transmittal.revision.conflict");
        TechnicalOfficeRules.Identity(actor);
        if (Status != TransmittalStatus.Issued)
        {
            throw new DomainRuleException("technical.transmittal.acknowledge.invalid_state", "Only an issued transmittal can be acknowledged.");
        }

        Status = TransmittalStatus.Acknowledged;
        AcknowledgedBy = actor;
        AcknowledgedAt = at;
        AcknowledgmentReference = TechnicalOfficeRules.Required(reference, 500, "technical.transmittal.acknowledgment.invalid");
        AdvanceRevision();
    }
}
