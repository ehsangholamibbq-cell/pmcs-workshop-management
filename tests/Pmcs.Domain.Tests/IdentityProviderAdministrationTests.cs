using System.Net;
using System.Text;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Services;

namespace Pmcs.Domain.Tests;

public sealed class IdentityProviderAdministrationTests
{
    [Fact]
    public async Task ProvisioningCreatesManagedUserBeforeInvitationDelivery()
    {
        var invitationId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var handler = new RecordingHandler((index, _) => index switch
        {
            0 => Json(HttpStatusCode.OK, "{\"access_token\":\"service-token\"}"),
            1 => Json(HttpStatusCode.OK, "[]"),
            2 => Created($"http://identity.test/admin/realms/pmcs/users/{userId}"),
            _ => throw new InvalidOperationException("Unexpected request.")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test/") };
        var service = new KeycloakIdentityProviderAdministration(httpClient, Options());

        var result = await service.EnsureUserAsync(new ProvisionIdentityRequest(
            invitationId, tenantId, "کاربر نمونه", "user@example.com"));

        Assert.Equal(userId, result.UserId);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.EndsWith("/protocol/openid-connect/token", handler.Requests[0].Path, StringComparison.Ordinal);
        Assert.Contains("pmcs_invitation_id", handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.Contains(invitationId.ToString(), handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.Contains(tenantId.ToString(), handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.Equal(3, handler.Requests.Count);

        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"service-token\"}", Encoding.UTF8, "application/json")
        });
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        await service.SendInvitationAsync(userId);

        Assert.Contains("CONFIGURE_TOTP", handler.Requests[4].Body, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.Requests[4].AuthorizationScheme);
    }

    [Fact]
    public async Task ProvisioningNeverClaimsAnUnmanagedExistingEmail()
    {
        var userId = Guid.NewGuid();
        var handler = new RecordingHandler((index, _) => index switch
        {
            0 => Json(HttpStatusCode.OK, "{\"access_token\":\"service-token\"}"),
            1 => Json(HttpStatusCode.OK, $"[{{\"id\":\"{userId}\",\"email\":\"user@example.com\",\"attributes\":{{}}}}]"),
            _ => throw new InvalidOperationException("Unexpected request.")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test/") };
        var service = new KeycloakIdentityProviderAdministration(httpClient, Options());

        var exception = await Assert.ThrowsAsync<IdentityProviderAdministrationException>(() =>
            service.EnsureUserAsync(new ProvisionIdentityRequest(
                Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com")));

        Assert.Equal("identity_provider.email.already_owned", exception.Code);
        Assert.False(exception.IsTransient);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DeleteIsIdempotentWhenProviderUserIsAlreadyGone()
    {
        var handler = new RecordingHandler((index, _) => index switch
        {
            0 => Json(HttpStatusCode.OK, "{\"access_token\":\"service-token\"}"),
            1 => new HttpResponseMessage(HttpStatusCode.NotFound),
            _ => throw new InvalidOperationException("Unexpected request.")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://identity.test/") };
        var service = new KeycloakIdentityProviderAdministration(httpClient, Options());

        await service.ApplyAsync(Guid.NewGuid(), IdentityProviderOperationType.Delete);

        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
    }

    private static IdentityProvisioningOptions Options() => new()
    {
        Enabled = true,
        BaseUrl = "http://identity.test",
        Realm = "pmcs",
        ClientId = "pmcs-identity-admin",
        ClientSecret = "test-secret",
        WebClientId = "pmcs-web",
        WebReturnUrl = "http://pmcs.test",
        InvitationLifespanSeconds = 86_400,
        PollSeconds = 5,
        MaximumAttempts = 5
    };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Created(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri(location);
        return response;
    }

    private sealed class RecordingHandler(Func<int, HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> queuedResponses = new();

        public List<RequestSnapshot> Requests { get; } = [];

        public void Enqueue(HttpResponseMessage response) => queuedResponses.Enqueue(response);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                body,
                request.Headers.Authorization?.Scheme));
            return queuedResponses.Count > 0
                ? queuedResponses.Dequeue()
                : responseFactory(Requests.Count - 1, request);
        }
    }

    private sealed record RequestSnapshot(HttpMethod Method, string Path, string Body, string? AuthorizationScheme);
}
