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

    public static ProjectMessage Create(
        Guid id, Guid tenantId, Guid projectId, long sequence,
        Guid authorUserId, Guid clientMessageId, string? body, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            authorUserId == Guid.Empty || clientMessageId == Guid.Empty || sequence < 1)
        {
            throw new DomainRuleException("collaboration.message.identity.invalid", "Message scope and sequence are required.");
        }

        var normalized = NormalizeBody(body);
        return new ProjectMessage
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Sequence = sequence,
            AuthorUserId = authorUserId, ClientMessageId = clientMessageId,
            Body = normalized, RequestHash = HashBody(normalized), CreatedAt = createdAt
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
}
