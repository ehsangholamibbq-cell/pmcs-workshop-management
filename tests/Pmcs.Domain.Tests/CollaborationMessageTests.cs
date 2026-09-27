using Microsoft.Extensions.Configuration;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.Collaboration;
using Pmcs.Modules.Collaboration.Domain;
using Pmcs.Modules.IdentityAccess.Services;

namespace Pmcs.Domain.Tests;

public sealed class CollaborationMessageTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public void StableClientIdAndNormalizedPersianTextProduceSameRetryFingerprint()
    {
        var clientId = Guid.NewGuid();
        var first = ProjectMessage.Create(Guid.NewGuid(), TenantId, ProjectId,
            1, ActorId, clientId, "  گزارش\r\nکارگاه  ", DateTimeOffset.UtcNow);
        var retry = ProjectMessage.Create(Guid.NewGuid(), TenantId, ProjectId,
            2, ActorId, clientId, "گزارش\nکارگاه", DateTimeOffset.UtcNow);

        Assert.Equal("گزارش\nکارگاه", first.Body);
        Assert.Equal(first.RequestHash, retry.RequestHash);
        Assert.NotEqual(first.RequestHash, ProjectMessage.HashBody("گزارش دیگری"));
        Assert.Equal(64, first.RequestHash.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("line\u0000break")]
    public void BlankOrControlTextIsRejected(string body)
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            ProjectMessage.Create(Guid.NewGuid(), TenantId, ProjectId, 1,
                ActorId, Guid.NewGuid(), body, DateTimeOffset.UtcNow));
        Assert.Equal("collaboration.message.body.invalid", exception.Code);
    }

    [Fact]
    public void MissingScopeOrOversizedMessageIsRejected()
    {
        Assert.Throws<DomainRuleException>(() => ProjectMessage.Create(
            Guid.NewGuid(), TenantId, Guid.Empty, 1, ActorId, Guid.NewGuid(), "ok", DateTimeOffset.UtcNow));
        Assert.Throws<DomainRuleException>(() => ProjectMessage.Create(
            Guid.NewGuid(), TenantId, ProjectId, 0, ActorId, Guid.NewGuid(), "ok", DateTimeOffset.UtcNow));
        Assert.Throws<DomainRuleException>(() => ProjectMessage.Create(
            Guid.NewGuid(), TenantId, ProjectId, 1, ActorId, Guid.NewGuid(),
            new string('x', 4001), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RoleMatrixSeparatesReadWriteAndModeration()
    {
        Assert.True(ProjectPermissionService.GrantsRole("Observer", "collaboration.read"));
        Assert.False(ProjectPermissionService.GrantsRole("Observer", "collaboration.send"));
        Assert.False(ProjectPermissionService.GrantsRole("Observer", "collaboration.convert"));
        Assert.True(ProjectPermissionService.GrantsRole("SiteSupervisor", "collaboration.send"));
        Assert.False(ProjectPermissionService.GrantsRole("SiteSupervisor", "collaboration.moderate"));
        Assert.True(ProjectPermissionService.GrantsRole("ProjectController", "collaboration.moderate"));
        Assert.True(ProjectPermissionService.GrantsRole("ProjectManager", "collaboration.convert"));
    }

    [Fact]
    public void ManifestIsProjectScopedAndDefaultRolloutIsOff()
    {
        var descriptor = new CollaborationModule().Descriptor;
        var keys = descriptor.Permissions.Select(item => item.Key).ToHashSet();
        Assert.Equal("collaboration.project", descriptor.ModuleId);
        Assert.Contains("identity-access.core", descriptor.Dependencies);
        Assert.Contains("documents.shared", descriptor.Dependencies);
        Assert.Equal(6, keys.Count);
        Assert.All(descriptor.Permissions, permission =>
            Assert.Equal(PermissionScope.Project, permission.Scope));
        Assert.DoesNotContain(descriptor.Capabilities, item =>
            item.Contains("private", StringComparison.OrdinalIgnoreCase));
        Assert.False(CollaborationRuntimeOptions.Create(new ConfigurationBuilder().Build()).Enabled);
    }

    [Fact]
    public void ReplyAndMentionsArePartOfTheStableRetryIdentity()
    {
        var reply = Guid.NewGuid();
        var firstMention = Guid.NewGuid();
        var secondMention = Guid.NewGuid();
        var one = ProjectMessage.Create(Guid.NewGuid(), TenantId, ProjectId,
            1, ActorId, Guid.NewGuid(), "پاسخ", DateTimeOffset.UtcNow,
            reply, [firstMention, secondMention]);
        var reordered = ProjectMessage.Create(Guid.NewGuid(), TenantId, ProjectId,
            2, ActorId, Guid.NewGuid(), "پاسخ", DateTimeOffset.UtcNow,
            reply, [secondMention, firstMention, firstMention]);
        var changedTarget = ProjectMessage.Create(Guid.NewGuid(), TenantId, ProjectId,
            3, ActorId, Guid.NewGuid(), "پاسخ", DateTimeOffset.UtcNow,
            Guid.NewGuid(), [firstMention, secondMention]);

        Assert.Equal(one.RequestHash, reordered.RequestHash);
        Assert.NotEqual(one.RequestHash, changedTarget.RequestHash);
        Assert.Equal(2, one.MentionedUserIds.Length);
        Assert.Equal(reply, one.ReplyToMessageId);
    }

    [Fact]
    public void InvalidMentionsAndUnknownReactionsFailClosed()
    {
        Assert.Throws<DomainRuleException>(() => ProjectMessage.NormalizeMentions([Guid.Empty], ActorId));
        Assert.Throws<DomainRuleException>(() => ProjectMessage.NormalizeMentions([ActorId], ActorId));
        Assert.Throws<DomainRuleException>(() => ProjectMessage.NormalizeMentions(
            Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToArray(), ActorId));
        Assert.Throws<DomainRuleException>(() => ProjectReaction.Create(Guid.NewGuid(),
            TenantId, ProjectId, Guid.NewGuid(), ActorId, "🪙", DateTimeOffset.UtcNow));
        Assert.Equal("👍", ProjectReaction.Create(Guid.NewGuid(), TenantId, ProjectId,
            Guid.NewGuid(), ActorId, "👍", DateTimeOffset.UtcNow).Emoji);
    }

    [Fact]
    public void PinAndUnpinPreserveAuthorAndOriginalContent()
    {
        var message = ProjectMessage.Create(Guid.NewGuid(), TenantId, ProjectId,
            1, ActorId, Guid.NewGuid(), "اصل", DateTimeOffset.UtcNow);
        var moderator = Guid.NewGuid();
        message.SetPin(moderator, DateTimeOffset.UtcNow);
        Assert.Equal(moderator, message.PinnedBy);
        message.ClearPin();
        Assert.Null(message.PinnedAt);
        Assert.Equal("اصل", message.Body);
        Assert.Equal(ActorId, message.AuthorUserId);
    }
}
