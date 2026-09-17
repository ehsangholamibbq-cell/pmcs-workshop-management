using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;

namespace Pmcs.Modules.Platform.Endpoints;

internal static class PlatformModuleEndpoints
{
    public static void MapPlatformModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var modules = endpoints.MapGroup("/api/v1/platform/modules").WithTags("Platform Extensibility");
        modules.MapGet("/", ListAsync);
        modules.MapGet("/{moduleId}", GetAsync);
    }

    private static async Task<IResult> ListAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IModuleCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "platform.modules.read",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Ok(catalog.Modules);
    }

    private static async Task<IResult> GetAsync(
        string moduleId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IModuleCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "platform.modules.read",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return catalog.TryGet(moduleId, out var descriptor)
            ? Results.Ok(descriptor)
            : Results.NotFound(new { code = "platform.module.not_found" });
    }
}
