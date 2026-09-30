using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ModelSelectionPolicyTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid projectId = Guid.NewGuid();
    private readonly Guid primaryId = Guid.NewGuid();
    private readonly Guid fallbackId = Guid.NewGuid();

    [Fact]
    public void SwitchingApprovedModelDoesNotChangeScopeOrCapabilities()
    {
        var profile = Profile();
        var catalog = Catalog();

        Assert.True(ModelSelectionPolicy.Select(profile, catalog, tenantId, projectId,
            IntelligenceDataClass.Confidential, 100, primaryId).Allowed);
        Assert.True(ModelSelectionPolicy.Select(profile, catalog, tenantId, projectId,
            IntelligenceDataClass.Confidential, 100, fallbackId).Allowed);
        Assert.Equal("ai.profile.scope_denied", ModelSelectionPolicy.Select(profile, catalog,
            Guid.NewGuid(), projectId, IntelligenceDataClass.Internal, 100, fallbackId).Code);
        Assert.Equal("ai.profile.scope_denied", ModelSelectionPolicy.Select(profile, catalog,
            tenantId, Guid.NewGuid(), IntelligenceDataClass.Internal, 100, fallbackId).Code);
        Assert.Equal("ai.profile.model_not_allowed", ModelSelectionPolicy.Select(profile, catalog,
            tenantId, projectId, IntelligenceDataClass.Internal, 100, Guid.NewGuid()).Code);
    }

    [Fact]
    public void FallbackFailsClosedForUnapprovedUnverifiedOrInsufficientModel()
    {
        var profile = Profile();
        var catalog = Catalog();
        Assert.Equal("ai.profile.fallback_denied", ModelSelectionPolicy.Select(profile, catalog,
            tenantId, projectId, IntelligenceDataClass.Internal, 100,
            fallbackId, fallback: true).Code);

        var decision = ModelSelectionPolicy.Select(profile, catalog, tenantId, projectId,
            IntelligenceDataClass.Internal, 100, fallbackId, fallback: true,
            failureCode: "ai.provider.timeout");
        Assert.True(decision.Allowed);
        Assert.Equal("ai.provider.timeout", decision.FallbackReason);

        Assert.Equal("ai.profile.model_unavailable", ModelSelectionPolicy.Select(profile,
            [catalog[0], catalog[1] with { ConnectionVerified = false }], tenantId, projectId,
            IntelligenceDataClass.Internal, 100, fallbackId, true, "ai.provider.timeout").Code);
        Assert.Equal("ai.profile.capability_missing", ModelSelectionPolicy.Select(profile,
            [catalog[0], catalog[1] with { Capabilities = ModelCapability.StructuredOutput }],
            tenantId, projectId, IntelligenceDataClass.Internal, 100,
            fallbackId, true, "ai.provider.timeout").Code);
    }

    [Fact]
    public void ClassificationAndBudgetAreBoundedByProfileAndModel()
    {
        var profile = Profile();
        Assert.Equal("ai.profile.data_denied", ModelSelectionPolicy.Select(profile, Catalog(),
            tenantId, projectId, IntelligenceDataClass.Restricted, 100).Code);
        Assert.Equal("ai.profile.budget_exceeded", ModelSelectionPolicy.Select(profile, Catalog(),
            tenantId, projectId, IntelligenceDataClass.Internal, 1001).Code);
        Assert.Equal("ai.profile.data_denied", ModelSelectionPolicy.Select(profile,
            [Catalog()[0] with { MaximumDataClass = IntelligenceDataClass.Internal }],
            tenantId, projectId, IntelligenceDataClass.Confidential, 100).Code);
    }

    [Fact]
    public void PublishedVersionsRemainDistinctAndSelectionCanRollbackWithRevision()
    {
        var now = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var manager = Guid.NewGuid();
        var first = IntelligenceProfileVersion.Publish(Profile(), manager, now);
        var second = IntelligenceProfileVersion.Publish(
            Profile() with { Version = 2, DefaultModelId = fallbackId }, manager, now.AddMinutes(1));
        var selection = IntelligenceProfileSelection.Create(first.ToPolicy(), primaryId, manager, now);

        selection.Change(second.ToPolicy(), fallbackId, 1, manager, now.AddMinutes(2));
        Assert.Equal(second.Id, selection.ProfileVersionId);
        Assert.Equal(2, selection.Revision);
        Assert.Throws<InvalidOperationException>(() => selection.Change(first.ToPolicy(),
            primaryId, 1, manager, now.AddMinutes(3)));

        selection.Change(first.ToPolicy(), primaryId, 2, manager, now.AddMinutes(3));
        Assert.Equal(first.Id, selection.ProfileVersionId);
        Assert.Equal(3, selection.Revision);
        Assert.Equal(first.Id, first.ToPolicy().Id);
    }

    private ModelExecutionProfile Profile() => new(
        Guid.NewGuid(), 1, "reference-read", tenantId, new HashSet<Guid> { projectId },
        primaryId, [primaryId, fallbackId], [fallbackId], true,
        ModelCapability.StructuredOutput | ModelCapability.ToolCalling,
        IntelligenceDataClass.Confidential, 2_000, 500, 30, 1_000,
        "prompt-v1", "policy-v1");

    private ModelCatalogEntry[] Catalog() =>
    [
        new(primaryId, 1, "OpenAI", "test-primary", ModelCapability.StructuredOutput |
            ModelCapability.ToolCalling, IntelligenceDataClass.Confidential, true, true),
        new(fallbackId, 1, "GoogleGemini", "test-fallback", ModelCapability.StructuredOutput |
            ModelCapability.ToolCalling, IntelligenceDataClass.Confidential, true, true)
    ];
}
