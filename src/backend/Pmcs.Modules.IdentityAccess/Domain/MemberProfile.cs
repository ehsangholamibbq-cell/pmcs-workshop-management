using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class MemberProfile : AggregateRoot
{
    private MemberProfile()
    {
    }

    private MemberProfile(Guid userId, Guid tenantId, DateTimeOffset createdAt)
    {
        UserId = userId;
        TenantId = tenantId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public string? JobTitle { get; private set; }

    public string? OrganizationUnit { get; private set; }

    public string? WorkPhone { get; private set; }

    public Guid? AvatarDocumentId { get; private set; }

    public decimal? AvatarCropX { get; private set; }

    public decimal? AvatarCropY { get; private set; }

    public decimal? AvatarCropWidth { get; private set; }

    public decimal? AvatarCropHeight { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static MemberProfile Create(Guid userId, Guid tenantId, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new DomainRuleException(
                "member-profile.identity.required",
                "User and tenant ids are required for a member profile.");
        }

        return new MemberProfile(userId, tenantId, createdAt);
    }

    public void UpdateSelf(
        long baseRevision,
        string? jobTitle,
        string? workPhone,
        Guid? avatarDocumentId,
        ProfileAvatarCrop? avatarCrop,
        Guid actorUserId,
        DateTimeOffset updatedAt) =>
        Apply(
            baseRevision,
            jobTitle,
            OrganizationUnit,
            workPhone,
            avatarDocumentId,
            avatarCrop,
            actorUserId,
            updatedAt);

    public void UpdateDirectory(
        long baseRevision,
        string? jobTitle,
        string? organizationUnit,
        string? workPhone,
        Guid? avatarDocumentId,
        ProfileAvatarCrop? avatarCrop,
        Guid actorUserId,
        DateTimeOffset updatedAt) =>
        Apply(
            baseRevision,
            jobTitle,
            organizationUnit,
            workPhone,
            avatarDocumentId,
            avatarCrop,
            actorUserId,
            updatedAt);

    private void Apply(
        long baseRevision,
        string? jobTitle,
        string? organizationUnit,
        string? workPhone,
        Guid? avatarDocumentId,
        ProfileAvatarCrop? avatarCrop,
        Guid actorUserId,
        DateTimeOffset updatedAt)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException(
                "member-profile.revision.conflict",
                "The member profile changed after it was loaded.");
        }

        if (actorUserId == Guid.Empty)
        {
            throw new DomainRuleException(
                "member-profile.actor.required",
                "A profile update actor is required.");
        }

        if (avatarDocumentId == Guid.Empty)
        {
            throw new DomainRuleException(
                "member-profile.avatar.invalid",
                "Avatar document id cannot be empty.");
        }

        var normalizedCrop = NormalizeCrop(avatarDocumentId, avatarCrop);
        JobTitle = Optional(jobTitle, 160, "member-profile.job-title.invalid");
        OrganizationUnit = Optional(
            organizationUnit,
            160,
            "member-profile.organization-unit.invalid");
        WorkPhone = NormalizePhone(workPhone);
        AvatarDocumentId = avatarDocumentId;
        AvatarCropX = normalizedCrop?.X;
        AvatarCropY = normalizedCrop?.Y;
        AvatarCropWidth = normalizedCrop?.Width;
        AvatarCropHeight = normalizedCrop?.Height;
        UpdatedBy = actorUserId;
        UpdatedAt = updatedAt;
        AdvanceRevision();
    }

    private static ProfileAvatarCrop? NormalizeCrop(
        Guid? avatarDocumentId,
        ProfileAvatarCrop? crop)
    {
        if (!avatarDocumentId.HasValue)
        {
            if (crop is not null)
            {
                throw new DomainRuleException(
                    "member-profile.avatar-crop.without-avatar",
                    "Avatar crop requires an avatar document.");
            }

            return null;
        }

        var resolved = crop ?? ProfileAvatarCrop.FullFrame;
        if (resolved.X < 0 || resolved.Y < 0 || resolved.Width <= 0 || resolved.Height <= 0 ||
            resolved.X + resolved.Width > 1 || resolved.Y + resolved.Height > 1)
        {
            throw new DomainRuleException(
                "member-profile.avatar-crop.invalid",
                "Avatar crop must be a normalized rectangle inside the image.");
        }

        return resolved;
    }

    private static string? NormalizePhone(string? value)
    {
        var normalized = Optional(value, 40, "member-profile.work-phone.invalid");
        if (normalized is not null && normalized.Any(character =>
                !char.IsDigit(character) && character is not '+' and not '-' and not '(' and not ')' and not ' '))
        {
            throw new DomainRuleException(
                "member-profile.work-phone.invalid",
                "Work phone contains unsupported characters.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string code)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new DomainRuleException(code, "Profile value is outside the allowed policy.");
        }

        return normalized;
    }
}

public sealed record ProfileAvatarCrop(decimal X, decimal Y, decimal Width, decimal Height)
{
    public static ProfileAvatarCrop FullFrame { get; } = new(0, 0, 1, 1);
}
