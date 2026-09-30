using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.Intelligence.Services;

namespace Pmcs.Modules.Intelligence.Endpoints;

internal static class ModelProviderAdministrationEndpoints
{
    internal static void MapModelProviderAdministration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/intelligence/admin/providers", ListAsync)
            .WithTags("Intelligence administration");
        endpoints.MapPost("/api/v1/intelligence/admin/providers/{provider}/probe", ProbeAsync)
            .WithTags("Intelligence administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
    }

    private static async Task<IResult> ListAsync(
        ICurrentActor actor,
        IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> providers,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanReadCatalogAsync(actor, null, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return Results.Ok(providers.Select(provider => provider.Availability).ToArray());
    }

    // This is a bounded external connection check without a PMCS state mutation or user payload.
    private static async Task<IResult> ProbeAsync(
        string provider,
        ICurrentActor actor,
        IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> providers,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var selected = providers.SingleOrDefault(item =>
            string.Equals(item.Provider, provider, StringComparison.Ordinal));
        if (selected is null) return Results.NotFound(new { code = "ai.provider.unknown" });

        return Results.Ok(await selected.ProbeAsync(cancellationToken));
    }
}
