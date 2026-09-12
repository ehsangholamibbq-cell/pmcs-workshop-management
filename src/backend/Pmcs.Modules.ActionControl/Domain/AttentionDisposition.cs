using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

public sealed class AttentionDisposition
{
    private AttentionDisposition()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid SourceFactId { get; private set; }

    public AttentionDispositionKind Kind { get; private set; }

    public Guid? ActionId { get; private set; }

    public string? Reason { get; private set; }

    public Guid DecidedBy { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    public static AttentionDisposition Converted(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid sourceFactId,
        Guid actionId,
        Guid decidedBy,
        DateTimeOffset decidedAt) => Create(
            id, tenantId, projectId, sourceFactId, AttentionDispositionKind.ConvertedToAction,
            actionId, null, decidedBy, decidedAt);

    public static AttentionDisposition Dismissed(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid sourceFactId,
        string reason,
        Guid decidedBy,
        DateTimeOffset decidedAt) => Create(
            id, tenantId, projectId, sourceFactId, AttentionDispositionKind.Dismissed,
            null, RequiredReason(reason), decidedBy, decidedAt);

    private static AttentionDisposition Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid sourceFactId,
        AttentionDispositionKind kind,
        Guid? actionId,
        string? reason,
        Guid decidedBy,
        DateTimeOffset decidedAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            sourceFactId == Guid.Empty || decidedBy == Guid.Empty)
        {
            throw new DomainRuleException("attention_disposition.identity.required", "Disposition identities are required.");
        }

        if ((kind == AttentionDispositionKind.ConvertedToAction && (!actionId.HasValue || actionId.Value == Guid.Empty)) ||
            (kind == AttentionDispositionKind.Dismissed && actionId.HasValue))
        {
            throw new DomainRuleException("attention_disposition.payload.invalid", "Disposition payload does not match its kind.");
        }

        return new AttentionDisposition
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            SourceFactId = sourceFactId,
            Kind = kind,
            ActionId = actionId,
            Reason = reason,
            DecidedBy = decidedBy,
            DecidedAt = decidedAt
        };
    }

    private static string RequiredReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1_000)
        {
            throw new DomainRuleException("attention_disposition.reason.invalid", "A dismissal reason of at most 1000 characters is required.");
        }

        return reason.Trim();
    }
}

public enum AttentionDispositionKind
{
    ConvertedToAction = 1,
    Dismissed = 2
}
