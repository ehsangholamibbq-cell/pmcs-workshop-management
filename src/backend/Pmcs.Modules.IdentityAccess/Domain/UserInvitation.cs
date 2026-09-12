using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class UserInvitation : AggregateRoot
{
    private UserInvitation()
    {
    }

    private UserInvitation(
        Guid id,
        Guid tenantId,
        string displayName,
        string email,
        TenantRole tenantRole,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        DisplayName = displayName;
        Email = email;
        TenantRole = tenantRole;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Status = UserInvitationStatus.Queued;
        NextAttemptAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public TenantRole TenantRole { get; private set; }

    public UserInvitationStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid? ProviderUserId { get; private set; }

    public int Attempts { get; private set; }

    public string? LastErrorCode { get; private set; }

    public void BindProviderUser(Guid providerUserId)
    {
        if (providerUserId == Guid.Empty)
        {
            throw new DomainRuleException("invitation.provider_user.required", "Provisioned identity id is required.");
        }

        if (ProviderUserId.HasValue && ProviderUserId.Value != providerUserId)
        {
            throw new DomainRuleException("invitation.provider_user.conflict", "Invitation is already bound to another identity.");
        }

        ProviderUserId = providerUserId;
        AdvanceRevision();
    }

    public void ReleaseDeletedProviderUser(Guid providerUserId)
    {
        if (Status is not (UserInvitationStatus.Failed or UserInvitationStatus.Expired) ||
            !ProviderUserId.HasValue ||
            ProviderUserId.Value != providerUserId)
        {
            throw new DomainRuleException(
                "invitation.provider_user.release_denied",
                "Only a failed or expired invitation can release its deleted identity.");
        }

        ProviderUserId = null;
        AdvanceRevision();
    }

    public static UserInvitation Create(
        Guid id,
        Guid tenantId,
        string displayName,
        string email,
        TenantRole tenantRole,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("invitation.identity.required", "Invitation, tenant and creator ids are required.");
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
        {
            throw new DomainRuleException("invitation.display_name.invalid", "A valid display name is required.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Trim().Length > 320)
        {
            throw new DomainRuleException("invitation.email.invalid", "A valid email is required.");
        }

        if (!Enum.IsDefined(tenantRole))
        {
            throw new DomainRuleException("invitation.tenant_role.invalid", "Tenant role is invalid.");
        }

        if (expiresAt <= createdAt)
        {
            throw new DomainRuleException("invitation.expiry.invalid", "Invitation expiry must be after creation.");
        }

        return new UserInvitation(
            id,
            tenantId,
            displayName.Trim(),
            email.Trim().ToLowerInvariant(),
            tenantRole,
            createdBy,
            createdAt,
            expiresAt);
    }

    public void BeginAttempt(DateTimeOffset at)
    {
        if (Status is UserInvitationStatus.Revoked or UserInvitationStatus.Sent or UserInvitationStatus.Expired)
        {
            throw new DomainRuleException("invitation.status.invalid", "Invitation cannot be processed in its current status.");
        }

        if (at >= ExpiresAt)
        {
            Expire();
            return;
        }

        Status = UserInvitationStatus.Processing;
        Attempts++;
        NextAttemptAt = null;
        LastErrorCode = null;
        AdvanceRevision();
    }

    public void MarkSent(Guid userId, DateTimeOffset at)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("invitation.user.required", "Provisioned user id is required.");
        }

        UserId = userId;
        Status = UserInvitationStatus.Sent;
        SentAt = at;
        NextAttemptAt = null;
        LastErrorCode = null;
        AdvanceRevision();
    }

    public void ScheduleRetry(string errorCode, DateTimeOffset nextAttemptAt)
    {
        LastErrorCode = NormalizeError(errorCode);
        Status = UserInvitationStatus.RetryScheduled;
        NextAttemptAt = nextAttemptAt;
        AdvanceRevision();
    }

    public void Fail(string errorCode)
    {
        LastErrorCode = NormalizeError(errorCode);
        Status = UserInvitationStatus.Failed;
        NextAttemptAt = null;
        AdvanceRevision();
    }

    public void Requeue(DateTimeOffset at, DateTimeOffset expiresAt)
    {
        if (Status is UserInvitationStatus.Revoked)
        {
            throw new DomainRuleException("invitation.status.invalid", "A revoked invitation cannot be resent.");
        }

        Status = UserInvitationStatus.Queued;
        ExpiresAt = expiresAt;
        NextAttemptAt = at;
        LastErrorCode = null;
        AdvanceRevision();
    }

    public void Revoke()
    {
        if (Status is UserInvitationStatus.Sent)
        {
            throw new DomainRuleException("invitation.status.invalid", "A provisioned invitation cannot be revoked.");
        }

        Status = UserInvitationStatus.Revoked;
        NextAttemptAt = null;
        AdvanceRevision();
    }

    public void Expire()
    {
        Status = UserInvitationStatus.Expired;
        NextAttemptAt = null;
        AdvanceRevision();
    }

    private static string NormalizeError(string errorCode) =>
        string.IsNullOrWhiteSpace(errorCode) ? "identity_provider.unavailable" : errorCode.Trim()[..Math.Min(errorCode.Trim().Length, 120)];
}

public enum UserInvitationStatus
{
    Queued = 1,
    Processing = 2,
    RetryScheduled = 3,
    Sent = 4,
    Failed = 5,
    Revoked = 6,
    Expired = 7
}
