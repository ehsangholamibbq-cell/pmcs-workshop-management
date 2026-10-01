using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectReaction
{
    private ProjectReaction() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Emoji { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static ProjectReaction Create(Guid id, Guid tenantId, Guid projectId,
        Guid messageId, Guid actorUserId, string emoji, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            messageId == Guid.Empty || actorUserId == Guid.Empty)
            throw new DomainRuleException("collaboration.reaction.identity.invalid", "Reaction scope is required.");
        ValidateEmoji(emoji);
        return new ProjectReaction
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, MessageId = messageId,
            ActorUserId = actorUserId, Emoji = emoji, CreatedAt = createdAt
        };
    }

    public static void ValidateEmoji(string emoji)
    {
        if (emoji is not ("👍" or "✅" or "⚠️" or "❤️"))
            throw new DomainRuleException("collaboration.reaction.emoji.invalid", "Reaction is not supported.");
    }
}
