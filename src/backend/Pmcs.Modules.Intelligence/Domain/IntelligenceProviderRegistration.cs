namespace Pmcs.Modules.Intelligence.Domain;

internal sealed class IntelligenceProviderRegistration
{
    public string Provider { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long Revision { get; private set; }

    private IntelligenceProviderRegistration() { }

    internal static IntelligenceProviderRegistration Register(string provider,
        Guid actorId, DateTimeOffset now)
    {
        if (provider is not ("OpenAI" or "GoogleGemini" or "AnthropicClaude") ||
            actorId == Guid.Empty)
            throw new ArgumentException("Unknown INT1 provider or actor.");
        return new IntelligenceProviderRegistration
        {
            Provider = provider, Version = 1, CreatedBy = actorId,
            CreatedAt = now, Revision = 1
        };
    }

    internal void Activate(DateTimeOffset now)
    {
        if (now < CreatedAt) throw new InvalidOperationException("Verification is stale.");
        VerifiedAt = now;
        Enabled = true;
        Version++;
        Revision++;
    }

    internal void Disable()
    {
        if (!Enabled) return;
        Enabled = false;
        Version++;
        Revision++;
    }
}
