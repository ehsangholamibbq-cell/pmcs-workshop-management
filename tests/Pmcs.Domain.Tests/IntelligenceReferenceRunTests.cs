using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Domain.Tests;

public sealed class IntelligenceReferenceRunTests
{
    [Fact]
    public void RunPinsSelectionAndTransitionsWithoutRetainingRequestText()
    {
        var tenant = Guid.NewGuid();
        var project = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var profile = new ModelExecutionProfile(Guid.NewGuid(), 3, "int1.reference",
            tenant, new HashSet<Guid> { project }, modelId, [modelId], [], false,
            ModelCapability.StructuredOutput, IntelligenceDataClass.Internal,
            1000, 100, 30, 1000, "prompt-v1", "policy-v1");
        var model = new ModelCatalogEntry(modelId, 2, "OpenAI", "configured-model",
            ModelCapability.StructuredOutput, IntelligenceDataClass.Internal, true, true, 2, 4);
        var now = DateTimeOffset.UtcNow;
        var run = IntelligenceReferenceRun.Request(Guid.NewGuid(), tenant, project,
            Guid.NewGuid(), profile, model, 2, new string('a', 64), now);

        Assert.Equal(IntelligenceRunStatus.Requested, run.Status);
        Assert.NotEqual(Guid.Empty, run.SessionId);
        Assert.Equal(now.AddMinutes(10), run.SessionExpiresAt);
        Assert.Equal(profile.Id, run.ProfileVersionId);
        Assert.Equal(3, run.ProfileVersion);
        Assert.Equal(2, run.ModelVersion);
        Assert.Equal(2, run.ProviderVersion);
        Assert.Equal(model.Id, run.InitialModelCatalogId);
        Assert.Equal("prompt-v1", run.PromptVersion);
        Assert.Throws<InvalidOperationException>(() => run.Start(now));
        run.Validate(now);
        run.Start(now);
        run.RecordTool("reporting.catalog.list", "Allowed");
        run.Complete(20, 10, 300, now.AddSeconds(1));
        Assert.Equal(IntelligenceRunStatus.Completed, run.Status);
        Assert.Equal("reporting.catalog.list", run.ToolId);
        Assert.Equal(1_000L, run.LatencyMilliseconds);
        Assert.Equal(300L, run.CostMicrounits);
        Assert.Throws<InvalidOperationException>(() => run.Fail("ai.provider.timeout", now));
    }

    [Fact]
    public void FallbackRequiresAllowlistedFailureAndRunningState()
    {
        var tenant = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var profile = new ModelExecutionProfile(Guid.NewGuid(), 1, "int1.reference",
            tenant, new HashSet<Guid>(), first, [first, second], [second], true,
            ModelCapability.StructuredOutput, IntelligenceDataClass.Internal,
            1000, 100, 30, 1000, "prompt-v1", "policy-v1");
        var model = new ModelCatalogEntry(first, 1, "OpenAI", "a",
            ModelCapability.StructuredOutput, IntelligenceDataClass.Internal, true, true, 2, 4);
        var alternate = model with { Id = second, Provider = "GoogleGemini", Model = "b" };
        var now = DateTimeOffset.UtcNow;
        var run = IntelligenceReferenceRun.Request(Guid.NewGuid(), tenant, Guid.NewGuid(),
            Guid.NewGuid(), profile, model, 2, new string('b', 64), now);
        Assert.Throws<InvalidOperationException>(() =>
            run.SelectFallback(alternate, 3, "ai.provider.timeout"));
        run.Validate(now);
        run.Start(now);
        Assert.Throws<InvalidOperationException>(() =>
            run.SelectFallback(alternate, 3, "ai.provider.rejected"));
        run.SelectFallback(alternate, 3, "ai.provider.timeout");
        Assert.Equal("GoogleGemini", run.Provider);
        Assert.Equal(3, run.ProviderVersion);
        Assert.Equal(first, run.InitialModelCatalogId);
        Assert.Equal("OpenAI", run.InitialProvider);
        Assert.Equal(2, run.InitialProviderVersion);
        Assert.Equal("ai.provider.timeout", run.FallbackReason);
        Assert.Throws<InvalidOperationException>(() =>
            run.SelectFallback(model, 2, "ai.provider.timeout"));
    }
}
