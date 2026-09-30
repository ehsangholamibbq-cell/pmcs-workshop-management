namespace Pmcs.Modules.Intelligence.Domain;

[Flags]
internal enum ModelCapability
{
    None = 0,
    StructuredOutput = 1,
    ToolCalling = 2
}

internal enum IntelligenceDataClass
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

internal sealed record ModelCatalogEntry(
    Guid Id, int Version, string Provider, string Model,
    ModelCapability Capabilities, IntelligenceDataClass MaximumDataClass,
    bool Enabled, bool ConnectionVerified);

internal sealed record ModelExecutionProfile(
    Guid Id, int Version, string UseCase,
    Guid TenantId, IReadOnlySet<Guid> ProjectIds,
    Guid DefaultModelId, IReadOnlyList<Guid> AllowedModelIds,
    IReadOnlyList<Guid> FallbackModelIds, bool AllowFallback,
    ModelCapability RequiredCapabilities, IntelligenceDataClass MaximumDataClass,
    int MaximumInputTokens, int MaximumOutputTokens, int TimeoutSeconds,
    long MaximumCostMicrounits, string PromptVersion, string PolicyVersion);

internal sealed record ModelSelectionDecision(
    bool Allowed, string Code, ModelCatalogEntry? Model, bool Fallback, string? FallbackReason)
{
    internal static ModelSelectionDecision Denied(string code) => new(false, code, null, false, null);
}

internal static class ModelSelectionPolicy
{
    internal static void ValidateProfile(ModelExecutionProfile profile)
    {
        if (profile.Id == Guid.Empty || profile.Version < 1 || profile.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(profile.UseCase) || profile.UseCase.Length > 100 ||
            string.IsNullOrWhiteSpace(profile.PromptVersion) || profile.PromptVersion.Length > 100 ||
            string.IsNullOrWhiteSpace(profile.PolicyVersion) || profile.PolicyVersion.Length > 100 ||
            profile.DefaultModelId == Guid.Empty || profile.AllowedModelIds.Count is < 1 or > 20 ||
            profile.AllowedModelIds.Distinct().Count() != profile.AllowedModelIds.Count ||
            !profile.AllowedModelIds.Contains(profile.DefaultModelId) ||
            profile.FallbackModelIds.Count > 5 ||
            profile.FallbackModelIds.Distinct().Count() != profile.FallbackModelIds.Count ||
            profile.FallbackModelIds.Any(id => id == profile.DefaultModelId || !profile.AllowedModelIds.Contains(id)) ||
            !profile.AllowFallback && profile.FallbackModelIds.Count > 0 ||
            profile.RequiredCapabilities == ModelCapability.None ||
            (profile.RequiredCapabilities & ~(ModelCapability.StructuredOutput | ModelCapability.ToolCalling)) != 0 ||
            !Enum.IsDefined(profile.MaximumDataClass) ||
            profile.MaximumInputTokens is < 1 or > 100_000 ||
            profile.MaximumOutputTokens is < 1 or > 20_000 ||
            profile.TimeoutSeconds is < 5 or > 300 ||
            profile.MaximumCostMicrounits is < 1 or > 1_000_000_000 ||
            profile.ProjectIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Invalid INT1 execution profile.");
        }
    }

    internal static ModelSelectionDecision Select(
        ModelExecutionProfile profile, IReadOnlyCollection<ModelCatalogEntry> catalog,
        Guid tenantId, Guid projectId, IntelligenceDataClass dataClass,
        long estimatedCostMicrounits, Guid? requestedModelId = null,
        bool fallback = false, string? failureCode = null)
    {
        ValidateProfile(profile);
        if (tenantId != profile.TenantId || projectId == Guid.Empty ||
            profile.ProjectIds.Count > 0 && !profile.ProjectIds.Contains(projectId))
            return ModelSelectionDecision.Denied("ai.profile.scope_denied");
        if (!Enum.IsDefined(dataClass) || dataClass > profile.MaximumDataClass)
            return ModelSelectionDecision.Denied("ai.profile.data_denied");
        if (estimatedCostMicrounits < 0 || estimatedCostMicrounits > profile.MaximumCostMicrounits)
            return ModelSelectionDecision.Denied("ai.profile.budget_exceeded");

        var id = requestedModelId ?? profile.DefaultModelId;
        if (!profile.AllowedModelIds.Contains(id))
            return ModelSelectionDecision.Denied("ai.profile.model_not_allowed");
        if (fallback && (!profile.AllowFallback ||
            !profile.FallbackModelIds.Contains(id) ||
            failureCode is not ("ai.provider.timeout" or "ai.provider.unavailable" or
                "ai.provider.invalid_response")))
            return ModelSelectionDecision.Denied("ai.profile.fallback_denied");

        var matches = catalog.Where(item => item.Id == id).Take(2).ToArray();
        if (matches.Length != 1 || !matches[0].Enabled || !matches[0].ConnectionVerified)
            return ModelSelectionDecision.Denied("ai.profile.model_unavailable");
        var model = matches[0];
        if ((model.Capabilities & profile.RequiredCapabilities) != profile.RequiredCapabilities)
            return ModelSelectionDecision.Denied("ai.profile.capability_missing");
        if (dataClass > model.MaximumDataClass)
            return ModelSelectionDecision.Denied("ai.profile.data_denied");

        return new(true, "ai.profile.model_selected", model, fallback, fallback ? failureCode : null);
    }
}
