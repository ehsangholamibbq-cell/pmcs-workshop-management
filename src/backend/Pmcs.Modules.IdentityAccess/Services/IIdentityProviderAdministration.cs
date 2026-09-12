using Pmcs.Modules.IdentityAccess.Domain;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed record ProvisionIdentityRequest(
    Guid InvitationId,
    Guid TenantId,
    string DisplayName,
    string Email);

internal sealed record ProvisionIdentityResult(Guid UserId);

internal interface IIdentityProviderAdministration
{
    Task<ProvisionIdentityResult> EnsureUserAsync(
        ProvisionIdentityRequest request,
        CancellationToken cancellationToken = default);

    Task SendInvitationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        Guid userId,
        IdentityProviderOperationType operation,
        CancellationToken cancellationToken = default);
}

internal sealed class IdentityProviderAdministrationException(
    string code,
    bool isTransient,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;

    public bool IsTransient { get; } = isTransient;
}
