using System.Security.Cryptography;
using System.Text;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectMessage
{
    private ProjectMessage() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public long Sequence { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ReplyToMessageId { get; private set; }
    public Guid[] MentionedUserIds { get; private set; } = [];
    public DateTimeOffset? PinnedAt { get; private set; }
    public Guid? PinnedBy { get; private set; }
    public long Revision { get; private set; } = 1;
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? RedactedAt { get; private set; }
    public bool LegalHold { get; private set; }

    public static ProjectMessage Create(
        Guid id, Guid tenantId, Guid projectId, long sequence,
        Guid authorUserId, Guid clientMessageId, string? body, DateTimeOffset createdAt,
        Guid? replyToMessageId = null, IReadOnlyCollection<Guid>? mentionedUserIds = null)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            authorUserId == Guid.Empty || clientMessageId == Guid.Empty || sequence < 1)
        {
            throw new DomainRuleException("collaboration.message.identity.invalid", "Message scope and sequence are required.");
        }

        var normalized = NormalizeBody(body);
        if (replyToMessageId == Guid.Empty)
            throw new DomainRuleException("collaboration.reply.invalid", "Reply target must be a message.");
        var mentions = NormalizeMentions(mentionedUserIds, authorUserId);
        return new ProjectMessage
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Sequence = sequence,
            AuthorUserId = authorUserId, ClientMessageId = clientMessageId,
            Body = normalized, RequestHash = HashRequest(normalized, replyToMessageId, mentions),
            CreatedAt = createdAt, ReplyToMessageId = replyToMessageId, MentionedUserIds = mentions
        };
    }

    public static string NormalizeBody(string? body)
    {
        var normalized = body?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 4_000 ||
            normalized.Any(character => char.IsControl(character) && character != '\n' && character != '\t'))
        {
            throw new DomainRuleException("collaboration.message.body.invalid", "Plain text of 1 to 4000 characters is required.");
        }
        return normalized;
    }

    public static string HashBody(string normalized) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

    public static Guid[] NormalizeMentions(IReadOnlyCollection<Guid>? values, Guid authorUserId)
    {
        var mentions = (values ?? []).Distinct().Order().ToArray();
        if (mentions.Length > 20 || mentions.Any(value => value == Guid.Empty || value == authorUserId))
            throw new DomainRuleException("collaboration.mentions.invalid", "A message can mention up to 20 other members.");
        return mentions;
    }

    public static string HashRequest(string normalizedBody, Guid? replyToMessageId, IReadOnlyCollection<Guid> mentions)
    {
        var bodyHash = HashBody(normalizedBody);
        if (replyToMessageId is null && mentions.Count == 0) return bodyHash;
        return HashBody($"{bodyHash}|{replyToMessageId?.ToString("N") ?? ""}|" +
            string.Join(',', mentions.Order().Select(value => value.ToString("N"))));
    }

    public void SetPin(Guid moderatorId, DateTimeOffset at)
    {
        if (moderatorId == Guid.Empty)
            throw new DomainRuleException("collaboration.pin.moderator.invalid", "Moderator identity is required.");
        PinnedBy = moderatorId;
        PinnedAt = at;
    }

    public ProjectMessageRevision Edit(long baseRevision, string? body,
        Guid actorUserId, DateTimeOffset at)
    {
        EnsureWritable(baseRevision, actorUserId);
        var normalized = NormalizeBody(body);
        if (normalized == Body)
            throw new DomainRuleException("collaboration.edit.no_change", "Message content is unchanged.");
        var history = ProjectMessageRevision.Capture(this, "Edited", actorUserId, at);
        Body = normalized;
        EditedAt = at;
        Revision++;
        return history;
    }

    public ProjectMessageRevision Tombstone(long baseRevision, Guid actorUserId,
        DateTimeOffset at)
    {
        EnsureWritable(baseRevision, actorUserId);
        var history = ProjectMessageRevision.Capture(this, "Deleted", actorUserId, at);
        Body = "پیام حذف شده است";
        DeletedAt = at;
        PinnedAt = null;
        PinnedBy = null;
        Revision++;
        return history;
    }

    public ProjectMessageRevision Redact(long baseRevision, Guid moderatorId,
        DateTimeOffset at)
    {
        EnsureWritable(baseRevision, moderatorId);
        var history = ProjectMessageRevision.Capture(this, "Redacted", moderatorId, at);
        Body = "پیام توسط ناظر پنهان شده است";
        RedactedAt = at;
        PinnedAt = null;
        PinnedBy = null;
        Revision++;
        return history;
    }

    public void SetLegalHold(long baseRevision, bool enabled, Guid moderatorId)
    {
        EnsureRevision(baseRevision);
        if (moderatorId == Guid.Empty || LegalHold == enabled)
            throw new DomainRuleException("collaboration.hold.invalid", "A changed hold and moderator are required.");
        LegalHold = enabled;
        Revision++;
    }

    private void EnsureWritable(long baseRevision, Guid actorUserId)
    {
        EnsureRevision(baseRevision);
        if (actorUserId == Guid.Empty || DeletedAt.HasValue || RedactedAt.HasValue)
            throw new DomainRuleException("collaboration.message.not_writable", "Message is no longer writable.");
    }

    private void EnsureRevision(long baseRevision)
    {
        if (baseRevision != Revision)
            throw new DomainRuleException("collaboration.message.revision.conflict", "Message revision changed.");
    }

    public void ClearPin()
    {
        PinnedBy = null;
        PinnedAt = null;
    }
}
