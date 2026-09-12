using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Sync.Domain;

namespace Pmcs.Domain.Tests;

public sealed class SyncControlTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("0.1.0", 2, 5, true)]
    [InlineData("0.1.0-beta", 2, 5, true)]
    [InlineData("0.0.9", 2, 5, false)]
    [InlineData("0.1.0", 1, 5, false)]
    [InlineData("0.1.0", 2, 4, false)]
    [InlineData("0.1.0", 2, 6, false)]
    [InlineData("invalid", 2, 5, false)]
    public void CompatibilityRequiresCurrentProtocolAndRecoverableLocalSchema(
        string appVersion,
        int protocolVersion,
        int schemaVersion,
        bool expected)
    {
        Assert.Equal(expected, SyncPolicy.EvaluateCompatibility(appVersion, protocolVersion, schemaVersion).IsCompatible);
    }

    [Fact]
    public void OfflineLeaseIsBoundToActorDeviceProjectVersionAndCaptureWindow()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var lease = OfflineAuthorizationLease.Issue(
            Guid.NewGuid(), tenantId, userId, projectId, "device-001", 3, Now, TimeSpan.FromDays(7));

        Assert.True(lease.Covers(tenantId, userId, projectId, "device-001", 3, Now.AddDays(2), Now.AddDays(2)));
        Assert.False(lease.Covers(tenantId, userId, projectId, "device-002", 3, Now.AddDays(2), Now.AddDays(2)));
        Assert.False(lease.Covers(tenantId, userId, projectId, "device-001", 2, Now.AddDays(2), Now.AddDays(2)));
        Assert.False(lease.Covers(tenantId, userId, projectId, "device-001", 3, Now.AddDays(8), Now.AddDays(8)));

        lease.Supersede(Now.AddDays(2));
        Assert.True(lease.Covers(tenantId, userId, projectId, "device-001", 3, Now.AddDays(1), Now.AddDays(2)));
        Assert.False(lease.Covers(tenantId, userId, projectId, "device-001", 3, Now.AddDays(3), Now.AddDays(3)));

        lease.Revoke(Now.AddDays(2));
        Assert.False(lease.Covers(tenantId, userId, projectId, "device-001", 3, Now.AddDays(1), Now.AddDays(2)));
    }

    [Fact]
    public void LeaseCannotExceedSevenDayOfflinePolicy()
    {
        Assert.Throws<DomainRuleException>(() => OfflineAuthorizationLease.Issue(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-001", 1,
            Now, TimeSpan.FromDays(8)));
    }

    [Fact]
    public void RevokedDeviceCannotOpenAnotherSession()
    {
        var device = RegisteredSyncDevice.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-001", "Field tablet", "Browser", "0.1.0", Now);

        device.Revoke(Guid.NewGuid(), "Lost device", Now.AddMinutes(1));

        Assert.Equal(SyncDeviceStatus.Revoked, device.Status);
        Assert.Throws<DomainRuleException>(() => device.Touch("Field tablet", "Browser", "0.1.0", Now.AddMinutes(2)));
    }

    [Fact]
    public void SyncSessionIsShortLivedAndBoundToExactScope()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var session = SyncSession.Open(
            Guid.NewGuid(), tenantId, userId, projectId, "device-001", Guid.NewGuid(), 12,
            SyncPolicy.ProtocolVersion, SyncPolicy.LocalSchemaVersion, Now, false, 0);

        Assert.True(session.IsValidFor(tenantId, userId, projectId, "device-001", Now.AddMinutes(14)));
        Assert.False(session.IsValidFor(tenantId, userId, Guid.NewGuid(), "device-001", Now.AddMinutes(1)));
        Assert.False(session.IsValidFor(tenantId, userId, projectId, "device-001", Now.AddMinutes(15)));
    }

    [Fact]
    public void ConflictResolutionPreservesBothSidesAndRequiresNewOperationForReapply()
    {
        var conflict = CreateConflict();

        Assert.Throws<DomainRuleException>(() => conflict.Resolve(
            Guid.NewGuid(), conflict.Revision, Guid.NewGuid(), SyncConflictResolutionType.Reapply,
            conflict.OperationId, null, Now.AddMinutes(2)));

        var resolution = conflict.Resolve(
            Guid.NewGuid(),
            conflict.Revision,
            Guid.NewGuid(),
            SyncConflictResolutionType.Reapply,
            "01K4ZQ9G5V7Q0M8M2V4R6D8F1C",
            "Reviewed against server state",
            Now.AddMinutes(3));

        Assert.Equal(SyncConflictStatus.Resolved, conflict.Status);
        Assert.Equal(SyncConflictResolutionType.Reapply, resolution.ResolutionType);
        Assert.Contains("local", conflict.LocalIntentJson, StringComparison.Ordinal);
        Assert.Contains("server", conflict.ServerProjectionJson!, StringComparison.Ordinal);
        Assert.Throws<DomainRuleException>(() => conflict.Resolve(
            Guid.NewGuid(), conflict.Revision, Guid.NewGuid(), SyncConflictResolutionType.KeepServer,
            null, null, Now.AddMinutes(4)));
    }

    [Fact]
    public void ConflictResolutionIsRevisionControlled()
    {
        var conflict = CreateConflict();

        Assert.Throws<DomainRuleException>(() => conflict.Resolve(
            Guid.NewGuid(), conflict.Revision + 1, Guid.NewGuid(), SyncConflictResolutionType.KeepServer,
            null, null, Now.AddMinutes(1)));
    }

    private static SyncConflictCase CreateConflict() => SyncConflictCase.Detect(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "device-001",
        "01K4ZQ9G5V7Q0M8M2V4R6D8F1B",
        "DailyReport",
        Guid.NewGuid(),
        "CaptureDailyReportFact",
        2,
        3,
        "daily_report.date.duplicate",
        "{\"side\":\"local\"}",
        "{\"side\":\"server\"}",
        Now);
}
