using System.Text.Json;

namespace Pmcs.Modules.Intelligence.Domain;

internal sealed class IntelligenceProfileVersion
{
    public Guid Id { get; private set; }
    public int Version { get; private set; }
    public string UseCase { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public string ProjectIdsJson { get; private set; } = "[]";
    public Guid DefaultModelId { get; private set; }
    public string AllowedModelIdsJson { get; private set; } = "[]";
    public string FallbackModelIdsJson { get; private set; } = "[]";
    public bool AllowFallback { get; private set; }
    public ModelCapability RequiredCapabilities { get; private set; }
    public IntelligenceDataClass MaximumDataClass { get; private set; }
    public int MaximumInputTokens { get; private set; }
    public int MaximumOutputTokens { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public long MaximumCostMicrounits { get; private set; }
    public string PromptVersion { get; private set; } = string.Empty;
    public string PolicyVersion { get; private set; } = string.Empty;
    public Guid PublishedBy { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    private IntelligenceProfileVersion() { }

    internal static IntelligenceProfileVersion Publish(ModelExecutionProfile profile,
        Guid publishedBy, DateTimeOffset now)
    {
        ModelSelectionPolicy.ValidateProfile(profile);
        if (publishedBy == Guid.Empty) throw new ArgumentException("Publisher is required.");
        return new IntelligenceProfileVersion
        {
            Id = profile.Id, Version = profile.Version, UseCase = profile.UseCase,
            TenantId = profile.TenantId,
            ProjectIdsJson = JsonSerializer.Serialize(profile.ProjectIds.Order()),
            DefaultModelId = profile.DefaultModelId,
            AllowedModelIdsJson = JsonSerializer.Serialize(profile.AllowedModelIds),
            FallbackModelIdsJson = JsonSerializer.Serialize(profile.FallbackModelIds),
            AllowFallback = profile.AllowFallback,
            RequiredCapabilities = profile.RequiredCapabilities,
            MaximumDataClass = profile.MaximumDataClass,
            MaximumInputTokens = profile.MaximumInputTokens,
            MaximumOutputTokens = profile.MaximumOutputTokens,
            TimeoutSeconds = profile.TimeoutSeconds,
            MaximumCostMicrounits = profile.MaximumCostMicrounits,
            PromptVersion = profile.PromptVersion, PolicyVersion = profile.PolicyVersion,
            PublishedBy = publishedBy, PublishedAt = now
        };
    }

    internal ModelExecutionProfile ToPolicy() => new(
        Id, Version, UseCase, TenantId,
        (JsonSerializer.Deserialize<Guid[]>(ProjectIdsJson) ?? []).ToHashSet(),
        DefaultModelId, JsonSerializer.Deserialize<Guid[]>(AllowedModelIdsJson) ?? [],
        JsonSerializer.Deserialize<Guid[]>(FallbackModelIdsJson) ?? [],
        AllowFallback, RequiredCapabilities, MaximumDataClass,
        MaximumInputTokens, MaximumOutputTokens, TimeoutSeconds,
        MaximumCostMicrounits, PromptVersion, PolicyVersion);
}

internal sealed class IntelligenceProfileSelection
{
    public Guid TenantId { get; private set; }
    public string UseCase { get; private set; } = string.Empty;
    public Guid ProfileVersionId { get; private set; }
    public Guid ModelId { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Revision { get; private set; }

    private IntelligenceProfileSelection() { }

    internal static IntelligenceProfileSelection Create(
        ModelExecutionProfile profile, Guid modelId, Guid actorId, DateTimeOffset now)
    {
        ModelSelectionPolicy.ValidateProfile(profile);
        if (actorId == Guid.Empty || !profile.AllowedModelIds.Contains(modelId))
            throw new ArgumentException("Model is outside the published profile.");
        return new IntelligenceProfileSelection
        {
            TenantId = profile.TenantId, UseCase = profile.UseCase,
            ProfileVersionId = profile.Id, ModelId = modelId,
            UpdatedBy = actorId, UpdatedAt = now, Revision = 1
        };
    }

    internal void Change(ModelExecutionProfile profile, Guid modelId,
        long baseRevision, Guid actorId, DateTimeOffset now)
    {
        ModelSelectionPolicy.ValidateProfile(profile);
        if (TenantId != profile.TenantId || UseCase != profile.UseCase ||
            !profile.AllowedModelIds.Contains(modelId) || actorId == Guid.Empty ||
            Revision != baseRevision || now < UpdatedAt)
            throw new InvalidOperationException("Profile selection is invalid or stale.");
        ProfileVersionId = profile.Id;
        ModelId = modelId;
        UpdatedBy = actorId;
        UpdatedAt = now;
        Revision++;
    }
}
