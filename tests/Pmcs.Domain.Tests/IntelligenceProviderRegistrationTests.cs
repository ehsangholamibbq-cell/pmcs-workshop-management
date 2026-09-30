using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Domain.Tests;

public sealed class IntelligenceProviderRegistrationTests
{
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("GoogleGemini")]
    [InlineData("AnthropicClaude")]
    public void RegistrationRequiresExplicitActivationAndCanBeDisabled(string provider)
    {
        var now = DateTimeOffset.UtcNow;
        var item = IntelligenceProviderRegistration.Register(provider, Guid.NewGuid(), now);
        Assert.False(item.Enabled);
        Assert.Null(item.VerifiedAt);
        Assert.Throws<InvalidOperationException>(() => item.Activate(now.AddMinutes(-1)));
        item.Activate(now);
        Assert.True(item.Enabled);
        Assert.Equal(2, item.Version);
        item.Disable();
        Assert.False(item.Enabled);
        Assert.Equal(3, item.Version);
    }

    [Fact]
    public void UnknownProviderCannotBeRegistered()
    {
        Assert.Throws<ArgumentException>(() =>
            IntelligenceProviderRegistration.Register("arbitrary", Guid.NewGuid(),
                DateTimeOffset.UtcNow));
    }
}
