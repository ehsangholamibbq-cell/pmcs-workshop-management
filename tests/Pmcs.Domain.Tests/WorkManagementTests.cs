using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.IdentityAccess.Services;
using Pmcs.Modules.WorkManagement.Domain;

namespace Pmcs.Domain.Tests;

public sealed class WorkManagementTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecipientCanReadThenAcknowledgeWithRevisionControl()
    {
        var recipientId = Guid.NewGuid();
        var notification = Create(recipientId);

        notification.MarkRead(1, recipientId, OccurredAt.AddMinutes(1));
        notification.Acknowledge(2, recipientId, OccurredAt.AddMinutes(2));

        Assert.Equal(OccurredAt.AddMinutes(1), notification.ReadAt);
        Assert.Equal(OccurredAt.AddMinutes(2), notification.AcknowledgedAt);
        Assert.Equal(3, notification.Revision);
    }

    [Fact]
    public void AnotherUserCannotChangeNotificationReceipt()
    {
        var notification = Create(Guid.NewGuid());

        var exception = Assert.Throws<DomainRuleException>(() =>
            notification.Acknowledge(1, Guid.NewGuid(), OccurredAt.AddMinutes(1)));

        Assert.Equal("notification.recipient.required", exception.Code);
    }

    [Fact]
    public void StaleReceiptRevisionIsRejected()
    {
        var recipientId = Guid.NewGuid();
        var notification = Create(recipientId);
        notification.MarkRead(1, recipientId, OccurredAt.AddMinutes(1));

        var exception = Assert.Throws<DomainRuleException>(() =>
            notification.Acknowledge(1, recipientId, OccurredAt.AddMinutes(2)));

        Assert.Equal("notification.revision.conflict", exception.Code);
    }

    [Fact]
    public void MyWorkSourcePermissionsRemainRoleScopedAndTestable()
    {
        Assert.True(ProjectPermissionService.GrantsRole("SiteSupervisor", "projects.read"));
        Assert.True(ProjectPermissionService.GrantsRole("SiteSupervisor", "field.daily-reports.read"));
        Assert.True(ProjectPermissionService.GrantsRole("SiteSupervisor", "field.daily-reports.submit"));
        Assert.False(ProjectPermissionService.GrantsRole("SiteSupervisor", "field.daily-reports.review"));
        Assert.True(ProjectPermissionService.GrantsRole("TechnicalOffice", "field.daily-reports.review"));
        Assert.False(ProjectPermissionService.GrantsRole("ProjectController", "field.daily-reports.review"));
        Assert.True(ProjectPermissionService.GrantsRole("Observer", "actions.read"));
        Assert.False(ProjectPermissionService.GrantsRole("Observer", "actions.update"));
    }

    private static InAppNotification Create(Guid recipientId) => InAppNotification.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        recipientId,
        "daily-report:submitted:1",
        "DailyReportReview",
        "گزارش جدید",
        "یک گزارش برای بازبینی آماده است.",
        "DailyReport",
        Guid.NewGuid(),
        OccurredAt);
}
