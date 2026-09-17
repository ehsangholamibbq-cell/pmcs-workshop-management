using System.Text.Json;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Services;

namespace Pmcs.Domain.Tests;

public sealed class OfflineOperationIdentityTests
{
    private const string OperationId = "01K4ZQ9G5V7Q0M8M2V4R6D8F1D";

    [Fact]
    public void IdempotencyIdentityIsBoundToUserDeviceAndOperationWithinPlatformLimit()
    {
        var tenantId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var operation = CreateOperation();
        var first = new OfflineFieldOperationContext(tenantId, firstUserId, "shared-device", "correlation-1");
        var same = new OfflineFieldOperationContext(tenantId, firstUserId, "shared-device", "correlation-2");
        var otherUser = new OfflineFieldOperationContext(tenantId, secondUserId, "shared-device", "correlation-3");
        var otherDevice = new OfflineFieldOperationContext(tenantId, firstUserId, new string('d', 120), "correlation-4");

        var firstKey = OfflineDailyReportOperationHandler.CreateIdempotencyKey(first, operation);

        Assert.Equal(firstKey, OfflineDailyReportOperationHandler.CreateIdempotencyKey(same, operation));
        Assert.NotEqual(firstKey, OfflineDailyReportOperationHandler.CreateIdempotencyKey(otherUser, operation));
        Assert.NotEqual(firstKey, OfflineDailyReportOperationHandler.CreateIdempotencyKey(otherDevice, operation));
        Assert.InRange(firstKey.Length, 1, IdempotencyKeyRules.MaxKeyLength);
        Assert.InRange(
            OfflineDailyReportOperationHandler.CreateIdempotencyKey(otherDevice, operation).Length,
            1,
            IdempotencyKeyRules.MaxKeyLength);
    }

    private static OfflineFieldOperation CreateOperation() => new(
        OperationId,
        Guid.NewGuid(),
        "DailyReport",
        Guid.NewGuid(),
        "CaptureDailyReportFact",
        null,
        1,
        new DateTimeOffset(2099, 12, 30, 8, 0, 0, TimeSpan.Zero),
        JsonSerializer.SerializeToElement(new { factId = Guid.NewGuid() }));
}
