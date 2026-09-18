using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class LoginExperience : AggregateRoot
{
    private LoginExperience()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public int VersionNumber { get; private set; }

    public LoginExperienceStatus Status { get; private set; }

    public LoginCompositionVariant CompositionVariant { get; private set; }

    public LoginSurfaceTone SurfaceTone { get; private set; }

    public LoginAccentPalette AccentPalette { get; private set; }

    public LoginMotionPolicy MotionPolicy { get; private set; }

    public string Eyebrow { get; private set; } = string.Empty;

    public string Headline { get; private set; } = string.Empty;

    public string SupportingText { get; private set; } = string.Empty;

    public Guid? LogoDocumentId { get; private set; }

    public Guid? HeroDocumentId { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? PublishedBy { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public static LoginExperience CreateDraft(
        Guid id,
        Guid tenantId,
        int versionNumber,
        LoginCompositionVariant compositionVariant,
        LoginSurfaceTone surfaceTone,
        LoginAccentPalette accentPalette,
        LoginMotionPolicy motionPolicy,
        string eyebrow,
        string headline,
        string supportingText,
        Guid? logoDocumentId,
        Guid? heroDocumentId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "login-experience.identity.required",
                "Login experience, tenant and creator ids are required.");
        }

        if (versionNumber <= 0)
        {
            throw new DomainRuleException(
                "login-experience.version.invalid",
                "Login experience version must be positive.");
        }

        ValidateEnum(compositionVariant, "composition");
        ValidateEnum(surfaceTone, "surface tone");
        ValidateEnum(accentPalette, "accent palette");
        ValidateEnum(motionPolicy, "motion policy");
        ValidateAssetId(logoDocumentId);
        ValidateAssetId(heroDocumentId);

        return new LoginExperience
        {
            Id = id,
            TenantId = tenantId,
            VersionNumber = versionNumber,
            Status = LoginExperienceStatus.Draft,
            CompositionVariant = compositionVariant,
            SurfaceTone = surfaceTone,
            AccentPalette = accentPalette,
            MotionPolicy = motionPolicy,
            Eyebrow = RequiredPlainText(eyebrow, 80, "login-experience.eyebrow.invalid"),
            Headline = RequiredPlainText(headline, 140, "login-experience.headline.invalid"),
            SupportingText = RequiredPlainText(
                supportingText,
                320,
                "login-experience.supporting-text.invalid"),
            LogoDocumentId = logoDocumentId,
            HeroDocumentId = heroDocumentId,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public bool MatchesDraft(
        LoginCompositionVariant compositionVariant,
        LoginSurfaceTone surfaceTone,
        LoginAccentPalette accentPalette,
        LoginMotionPolicy motionPolicy,
        string eyebrow,
        string headline,
        string supportingText,
        Guid? logoDocumentId,
        Guid? heroDocumentId) =>
        CompositionVariant == compositionVariant &&
        SurfaceTone == surfaceTone &&
        AccentPalette == accentPalette &&
        MotionPolicy == motionPolicy &&
        string.Equals(Eyebrow, eyebrow.Trim(), StringComparison.Ordinal) &&
        string.Equals(Headline, headline.Trim(), StringComparison.Ordinal) &&
        string.Equals(SupportingText, supportingText.Trim(), StringComparison.Ordinal) &&
        LogoDocumentId == logoDocumentId &&
        HeroDocumentId == heroDocumentId;

    public void Publish(long baseRevision, Guid publishedBy, DateTimeOffset publishedAt)
    {
        RequireRevision(baseRevision);
        if (Status is not LoginExperienceStatus.Draft and not LoginExperienceStatus.Superseded)
        {
            throw new DomainRuleException(
                "login-experience.publish.invalid-state",
                "Only a draft or superseded login experience can be published.");
        }

        if (publishedBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "login-experience.publisher.required",
                "A publisher is required.");
        }

        Status = LoginExperienceStatus.Published;
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        AdvanceRevision();
    }

    public void MarkSuperseded(Guid actorUserId, DateTimeOffset changedAt)
    {
        if (Status != LoginExperienceStatus.Published)
        {
            return;
        }

        if (actorUserId == Guid.Empty)
        {
            throw new DomainRuleException(
                "login-experience.actor.required",
                "A superseding actor is required.");
        }

        Status = LoginExperienceStatus.Superseded;
        PublishedBy = actorUserId;
        PublishedAt = changedAt;
        AdvanceRevision();
    }

    private void RequireRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException(
                "login-experience.revision.conflict",
                "The login experience changed after it was loaded.");
        }
    }

    private static string RequiredPlainText(string value, int maximumLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength ||
            normalized.Any(char.IsControl) || normalized.Contains('<') || normalized.Contains('>'))
        {
            throw new DomainRuleException(code, "Login presentation text is outside the allowlisted policy.");
        }

        return normalized;
    }

    private static void ValidateAssetId(Guid? value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleException(
                "login-experience.asset.invalid",
                "Login experience asset id cannot be empty.");
        }
    }

    private static void ValidateEnum<TEnum>(TEnum value, string label)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainRuleException(
                "login-experience.token.invalid",
                $"Login experience {label} is not allowlisted.");
        }
    }
}

public enum LoginExperienceStatus
{
    Draft = 1,
    Published = 2,
    Superseded = 3
}

public enum LoginCompositionVariant
{
    BlueprintSplit = 1,
    MonolithFocus = 2,
    WarmMinimal = 3
}

public enum LoginSurfaceTone
{
    WarmStone = 1,
    WarmIvory = 2,
    DeepNavy = 3
}

public enum LoginAccentPalette
{
    CorporateNavyGreen = 1,
    NavySilver = 2,
    GreenStone = 3
}

public enum LoginMotionPolicy
{
    Calm = 1,
    Balanced = 2,
    Expressive = 3
}
