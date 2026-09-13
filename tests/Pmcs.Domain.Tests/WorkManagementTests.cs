using Pmcs.BuildingBlocks.Domain;
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
