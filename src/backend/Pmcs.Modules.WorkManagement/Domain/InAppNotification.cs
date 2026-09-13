using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.WorkManagement.Domain;

public sealed class InAppNotification : AggregateRoot
{
    private InAppNotification()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string DeduplicationKey { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public static InAppNotification Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid recipientUserId,
        string deduplicationKey,
        string category,
        string title,
        string body,
        string targetType,
        Guid targetId,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            recipientUserId == Guid.Empty || targetId == Guid.Empty)
        {
            throw new DomainRuleException("notification.identity.required", "Notification identities are required.");
        }

        return new InAppNotification
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            RecipientUserId = recipientUserId,
            DeduplicationKey = Required(deduplicationKey, 240, "notification.deduplication_key.invalid"),
            Category = Required(category, 80, "notification.category.invalid"),
            Title = Required(title, 240, "notification.title.invalid"),
            Body = Required(body, 2_000, "notification.body.invalid"),
            TargetType = Required(targetType, 80, "notification.target_type.invalid"),
            TargetId = targetId,
            OccurredAt = occurredAt
        };
    }

    public void MarkRead(long baseRevision, Guid actorUserId, DateTimeOffset readAt)
    {
        EnsureRecipient(actorUserId);
        EnsureRevision(baseRevision);
        ReadAt ??= readAt;
        AdvanceRevision();
    }

    public void Acknowledge(long baseRevision, Guid actorUserId, DateTimeOffset acknowledgedAt)
    {
        EnsureRecipient(actorUserId);
        EnsureRevision(baseRevision);
        ReadAt ??= acknowledgedAt;
        AcknowledgedAt ??= acknowledgedAt;
        AdvanceRevision();
    }

    private void EnsureRecipient(Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || actorUserId != RecipientUserId)
        {
            throw new DomainRuleException(
                "notification.recipient.required",
                "Only the notification recipient can change its receipt state.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException(
                "notification.revision.conflict",
                "The notification changed after it was loaded.");
        }
    }

    private static string Required(string value, int maximumLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw new DomainRuleException(code, $"Value is required and must be at most {maximumLength} characters.");
        }

        return value.Trim();
    }
}
