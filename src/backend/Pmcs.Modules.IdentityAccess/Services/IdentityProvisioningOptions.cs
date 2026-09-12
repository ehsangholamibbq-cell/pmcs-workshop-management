namespace Pmcs.Modules.IdentityAccess.Services;

public sealed class IdentityProvisioningOptions
{
    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string Realm { get; init; } = "pmcs";

    public string ClientId { get; init; } = "pmcs-identity-admin";

    public string ClientSecret { get; init; } = string.Empty;

    public string WebClientId { get; init; } = "pmcs-web";

    public string WebReturnUrl { get; init; } = string.Empty;

    public int InvitationLifespanSeconds { get; init; } = 86_400;

    public int PollSeconds { get; init; } = 5;

    public int MaximumAttempts { get; init; } = 5;

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) ||
            string.IsNullOrWhiteSpace(Realm) ||
            string.IsNullOrWhiteSpace(ClientId) ||
            string.IsNullOrWhiteSpace(ClientSecret) ||
            string.IsNullOrWhiteSpace(WebClientId) ||
            !Uri.TryCreate(WebReturnUrl, UriKind.Absolute, out _) ||
            InvitationLifespanSeconds is < 300 or > 604_800 ||
            PollSeconds is < 1 or > 300 ||
            MaximumAttempts is < 1 or > 10)
        {
            throw new InvalidOperationException("IdentityProvisioning configuration is invalid.");
        }
    }
}
