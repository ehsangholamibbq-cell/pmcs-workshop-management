using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ProjectBootstrapTests
{
    [Fact]
    public void PreviewDigestControlsExecutionAndActivationRemainsIndependent()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var plan = CreatePlan(now);

        plan.RecordPreview(
            plan.Revision,
            "pmcs.project-bootstrap/v1.0.0",
            new string('a', 64),
            new string('b', 64),
            "{\"items\":[]}",
            4,
            1,
            now,
            now.AddMinutes(30));

        Assert.Equal(ProjectBootstrapStatus.PreviewReady, plan.Status);
        Assert.Throws<DomainRuleException>(() =>
            plan.EnsurePreviewUsable(new string('c', 64), now.AddMinutes(1)));

        plan.EnsurePreviewUsable(new string('a', 64), now.AddMinutes(1));
        plan.Complete(
            plan.Revision,
            new string('a', 64),
            "{\"validation\":\"passed\"}",
            2,
            now.AddMinutes(2));

        Assert.Equal(ProjectBootstrapStatus.Completed, plan.Status);
        Assert.NotNull(plan.ExecutedAt);
        Assert.Null(plan.ActivatedAt);

        plan.MarkActivated(plan.Revision, now.AddMinutes(3));

        Assert.Equal(ProjectBootstrapStatus.Activated, plan.Status);
        Assert.NotNull(plan.ActivatedAt);
    }

    [Fact]
    public void ExpiredPreviewCannotExecute()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var plan = CreatePlan(now);
        plan.RecordPreview(
            plan.Revision,
            "pmcs.project-bootstrap/v1.0.0",
            new string('a', 64),
            string.Empty,
            "{\"items\":[]}",
            1,
            1,
            now,
            now.AddMinutes(30));

        var exception = Assert.Throws<DomainRuleException>(() =>
            plan.EnsurePreviewUsable(new string('a', 64), now.AddMinutes(31)));

        Assert.Equal("project.bootstrap.preview.expired", exception.Code);
        Assert.Equal(ProjectBootstrapStatus.PreviewReady, plan.Status);
    }

    [Fact]
    public void SourceAndTargetMustRemainIndependent()
    {
        var projectId = Guid.NewGuid();
        var exception = Assert.Throws<DomainRuleException>(() => ProjectBootstrapPlan.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            projectId,
            projectId,
            ProjectBootstrapConflictPolicy.FailOnConflict,
            "[]",
            "[]",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.Equal("project.bootstrap.source_target.same", exception.Code);
    }

    private static ProjectBootstrapPlan CreatePlan(DateTimeOffset now) => ProjectBootstrapPlan.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        ProjectBootstrapConflictPolicy.FailOnConflict,
        "[\"BaseSettings\"]",
        "[]",
        Guid.NewGuid(),
        now);
}
