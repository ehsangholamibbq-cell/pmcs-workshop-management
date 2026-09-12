using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ActionControl.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ManagementActionTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActionHasExplicitAssigneeDueDatePriorityAndOpenState()
    {
        var action = Create();

        Assert.Equal(ManagementActionStatus.Open, action.Status);
        Assert.Equal(ActionPriority.High, action.Priority);
        Assert.Equal(new DateOnly(2026, 9, 12), action.DueDate);
        Assert.NotEqual(Guid.Empty, action.AssigneeUserId);
    }

    [Fact]
    public void ActionCanMoveToInProgressThenDoneWithRevisionControl()
    {
        var action = Create();
        var actorId = Guid.NewGuid();

        action.Transition(1, ManagementActionStatus.InProgress, actorId, CreatedAt.AddHours(1));
        action.Transition(2, ManagementActionStatus.Done, actorId, CreatedAt.AddHours(2));

        Assert.Equal(ManagementActionStatus.Done, action.Status);
        Assert.Equal(3, action.Revision);
        Assert.Equal(CreatedAt.AddHours(2), action.CompletedAt);
    }

    [Fact]
    public void CompletedActionIsTerminal()
    {
        var action = Create();
        action.Transition(1, ManagementActionStatus.Done, Guid.NewGuid(), CreatedAt.AddHours(1));

        var exception = Assert.Throws<DomainRuleException>(() =>
            action.Transition(2, ManagementActionStatus.InProgress, Guid.NewGuid(), CreatedAt.AddHours(2)));

        Assert.Equal("action.transition.invalid_state", exception.Code);
    }

    [Fact]
    public void DismissalRequiresAuditableReason()
    {
        var exception = Assert.Throws<DomainRuleException>(() => AttentionDisposition.Dismissed(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " ", Guid.NewGuid(), CreatedAt));

        Assert.Equal("attention_disposition.reason.invalid", exception.Code);
    }

    private static ManagementAction Create() => ManagementAction.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "رفع مانع نقشه اجرایی",
        null,
        Guid.NewGuid(),
        "مدیر پروژه",
        new DateOnly(2026, 9, 12),
        ActionPriority.High,
        Guid.NewGuid(),
        CreatedAt);
}
