using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.Modules.IdentityAccess.Domain;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class KeycloakIdentityProviderAdministration(
    HttpClient httpClient,
    IdentityProvisioningOptions options) : IIdentityProviderAdministration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] RequiredActions = ["VERIFY_EMAIL", "UPDATE_PASSWORD", "CONFIGURE_TOTP"];

    public async Task<ProvisionIdentityResult> EnsureUserAsync(
        ProvisionIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var token = await GetTokenAsync(cancellationToken);
        var existing = await FindByEmailAsync(request.Email, token, cancellationToken);
        Guid userId;
        if (existing is not null)
        {
            ValidateManagedIdentity(existing, request);
            userId = ParseUserId(existing.Id);
        }
        else
        {
            userId = await CreateUserAsync(request, token, cancellationToken);
        }

        return new ProvisionIdentityResult(userId);
    }

    public async Task SendInvitationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var token = await GetTokenAsync(cancellationToken);
        await SendInvitationEmailAsync(userId, token, cancellationToken);
    }

    public async Task ApplyAsync(
        Guid userId,
        IdentityProviderOperationType operation,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var token = await GetTokenAsync(cancellationToken);
        switch (operation)
        {
            case IdentityProviderOperationType.Enable:
                await SetEnabledAsync(userId, true, token, cancellationToken);
                break;
            case IdentityProviderOperationType.DisableAndLogout:
                await SetEnabledAsync(userId, false, token, cancellationToken);
                await LogoutAsync(userId, token, cancellationToken);
                break;
            case IdentityProviderOperationType.Logout:
                await LogoutAsync(userId, token, cancellationToken);
                break;
            case IdentityProviderOperationType.Delete:
                await DeleteAsync(userId, token, cancellationToken);
                break;
            default:
                throw new IdentityProviderAdministrationException(
                    "identity_provider.operation.invalid",
                    false,
                    "Unsupported identity provider operation.");
        }
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"realms/{Escape(options.Realm)}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret
                })
            };
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw FromStatus("identity_provider.token.rejected", response.StatusCode);
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
            return string.IsNullOrWhiteSpace(token?.AccessToken)
                ? throw new IdentityProviderAdministrationException(
                    "identity_provider.token.invalid",
                    true,
                    "Identity provider returned no access token.")
                : token.AccessToken;
        }
        catch (IdentityProviderAdministrationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.unavailable",
                true,
                "Identity provider is unavailable.",
                exception);
        }
    }

    private async Task<KeycloakUser?> FindByEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = Authorized(
            HttpMethod.Get,
            $"admin/realms/{Escape(options.Realm)}/users?email={Uri.EscapeDataString(email)}&exact=true",
            token);
        using var response = await SendAsync(request, "identity_provider.users.read_failed", cancellationToken);
        var users = await response.Content.ReadFromJsonAsync<KeycloakUser[]>(JsonOptions, cancellationToken) ?? [];
        var exact = users.Where(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (exact.Length > 1)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.email.ambiguous",
                false,
                "Identity provider returned multiple exact users.");
        }

        return exact.SingleOrDefault();
    }

    private async Task<Guid> CreateUserAsync(
        ProvisionIdentityRequest invitation,
        string token,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            username = invitation.Email,
            email = invitation.Email,
            emailVerified = false,
            enabled = true,
            firstName = invitation.DisplayName,
            attributes = new Dictionary<string, string[]>
            {
                ["tenant_id"] = [invitation.TenantId.ToString()],
                ["pmcs_invitation_id"] = [invitation.InvitationId.ToString()],
                ["locale"] = ["fa"]
            },
            requiredActions = RequiredActions
        };
        using var request = Authorized(HttpMethod.Post, $"admin/realms/{Escape(options.Realm)}/users", token);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var existing = await FindByEmailAsync(invitation.Email, token, cancellationToken);
            if (existing is null)
            {
                throw new IdentityProviderAdministrationException(
                    "identity_provider.user.conflict",
                    true,
                    "Identity provider reported a conflict without an exact user.");
            }

            ValidateManagedIdentity(existing, invitation);
            return ParseUserId(existing.Id);
        }

        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw FromStatus("identity_provider.user.create_failed", response.StatusCode);
        }

        var identifier = response.Headers.Location?.Segments.LastOrDefault()?.Trim('/');
        return ParseUserId(identifier);
    }

    private async Task SendInvitationEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        var query = $"client_id={Uri.EscapeDataString(options.WebClientId)}" +
            $"&lifespan={options.InvitationLifespanSeconds}" +
            $"&redirect_uri={Uri.EscapeDataString(options.WebReturnUrl)}";
        using var request = Authorized(
            HttpMethod.Put,
            $"admin/realms/{Escape(options.Realm)}/users/{userId}/execute-actions-email?{query}",
            token);
        request.Content = JsonContent.Create(
            RequiredActions,
            options: JsonOptions);
        using var response = await SendAsync(request, "identity_provider.invitation.delivery_failed", cancellationToken);
    }

    private async Task SetEnabledAsync(
        Guid userId,
        bool enabled,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = Authorized(
            HttpMethod.Put,
            $"admin/realms/{Escape(options.Realm)}/users/{userId}",
            token);
        request.Content = JsonContent.Create(new { enabled }, options: JsonOptions);
        using var response = await SendAsync(request, "identity_provider.user.update_failed", cancellationToken);
    }

    private async Task LogoutAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        using var request = Authorized(
            HttpMethod.Post,
            $"admin/realms/{Escape(options.Realm)}/users/{userId}/logout",
            token);
        using var response = await SendAsync(request, "identity_provider.logout.failed", cancellationToken);
    }

    private async Task DeleteAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        using var request = Authorized(
            HttpMethod.Delete,
            $"admin/realms/{Escape(options.Realm)}/users/{userId}",
            token);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            throw FromStatus("identity_provider.user.delete_failed", response.StatusCode);
        }
        catch (IdentityProviderAdministrationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.unavailable",
                true,
                "Identity provider is unavailable.",
                exception);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string errorCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                throw FromStatus(errorCode, response.StatusCode);
            }

            return response;
        }
        catch (IdentityProviderAdministrationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.unavailable",
                true,
                "Identity provider is unavailable.",
                exception);
        }
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static void ValidateManagedIdentity(KeycloakUser user, ProvisionIdentityRequest invitation)
    {
        var invitationMarker = user.Attributes?.GetValueOrDefault("pmcs_invitation_id")?.SingleOrDefault();
        var tenantMarker = user.Attributes?.GetValueOrDefault("tenant_id")?.SingleOrDefault();
        if (!Guid.TryParse(invitationMarker, out var invitationId) || invitationId != invitation.InvitationId ||
            !Guid.TryParse(tenantMarker, out var tenantId) || tenantId != invitation.TenantId)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.email.already_owned",
                false,
                "Existing identity is not owned by this PMCS invitation.");
        }
    }

    private static Guid ParseUserId(string? value) =>
        Guid.TryParse(value, out var userId) && userId != Guid.Empty
            ? userId
            : throw new IdentityProviderAdministrationException(
                "identity_provider.user_id.invalid",
                false,
                "Identity provider returned a non-UUID user id.");

    private static IdentityProviderAdministrationException FromStatus(string code, HttpStatusCode statusCode) =>
        new(
            code,
            statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500,
            $"Identity provider returned HTTP {(int)statusCode}.");

    private static string Escape(string value) => Uri.EscapeDataString(value.Trim());

    private void EnsureEnabled()
    {
        if (!options.Enabled)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.not_configured",
                true,
                "Identity provider administration is not configured.");
        }
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record KeycloakUser(
        string Id,
        string? Email,
        Dictionary<string, string[]>? Attributes);
}
