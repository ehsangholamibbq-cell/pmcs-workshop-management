using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Persistence;
using Pmcs.Modules.Intelligence.Services;

namespace Pmcs.Modules.Intelligence.Endpoints;

internal sealed record ChangeProviderRegistrationRequest(long BaseRevision);

internal static class ModelProviderAdministrationEndpoints
{
    internal static void MapModelProviderAdministration(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/intelligence/admin/providers", ListAsync)
            .WithTags("Intelligence administration");
        endpoints.MapPost("/api/v1/intelligence/admin/providers/{provider}/probe", ProbeAsync)
            .WithTags("Intelligence administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
        var administration = endpoints.MapGroup("/api/v1/intelligence/admin/providers/{provider}")
            .WithTags("Intelligence administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
        administration.MapPost("/register", RegisterAsync);
        administration.MapPost("/activate", ActivateAsync);
        administration.MapPost("/disable", DisableAsync);
    }

    private static async Task<IResult> ListAsync(
        ICurrentActor actor,
        IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> providers,
        IntelligenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanReadCatalogAsync(actor, null, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var registrations = await dbContext.ProviderRegistrations.AsNoTracking()
            .ToArrayAsync(cancellationToken);
        return Results.Ok(providers.Select(provider => new
        {
            connection = provider.Availability,
            registration = registrations.SingleOrDefault(item => item.Provider == provider.Provider) is { } item
                ? new { item.Version, item.Enabled, item.VerifiedAt, item.Revision }
                : null
        }).ToArray());
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

    private static async Task<IResult> RegisterAsync(string provider, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IntelligenceDbContext db, IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotency, $"intelligence.providers.register:{provider}", provider, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        if (await db.ProviderRegistrations.AnyAsync(item => item.Provider == provider,
                cancellationToken))
            return Results.Conflict(new { code = "ai.provider.already_registered" });
        IntelligenceProviderRegistration item;
        try { item = IntelligenceProviderRegistration.Register(provider, actor.UserId, clock.UtcNow); }
        catch (ArgumentException) { return Results.BadRequest(new { code = "ai.provider.unknown" }); }
        db.ProviderRegistrations.Add(item);
        var result = new { item.Provider, item.Version, item.Enabled, item.Revision };
        await IntelligenceAdminMutationSupport.CommitAsync(db, sideEffects, context, actor,
            clock, $"intelligence.providers.register:{provider}", "IntelligenceProviderRegistered",
            "IntelligenceProviderRegistration", provider,
            new Dictionary<string, object?> { ["provider"] = provider, ["version"] = item.Version },
            result, replay.Key, replay.Hash, StatusCodes.Status201Created, cancellationToken);
        return Results.Created($"/api/v1/intelligence/admin/providers/{provider}", result);
    }

    private static async Task<IResult> ActivateAsync(string provider,
        ChangeProviderRegistrationRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> probes, IntelligenceDbContext db,
        IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var operation = $"intelligence.providers.activate:{provider}";
        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotency, operation, request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var item = await db.ProviderRegistrations.SingleOrDefaultAsync(x => x.Provider == provider,
            cancellationToken);
        if (item is null) return Results.NotFound(new { code = "ai.provider.not_registered" });
        if (item.Revision != request.BaseRevision)
            return Results.Conflict(new { code = "ai.provider.revision_conflict" });
        var probe = probes.Single(x => x.Provider == provider);
        var structured = await probe.ProbeAsync(cancellationToken);
        if (structured.Status != ProviderProbeStatus.Available)
            return Results.Json(structured, statusCode: StatusCodes.Status503ServiceUnavailable);
        var tool = await probe.ProbeToolCallingAsync(cancellationToken);
        if (tool.Status != ProviderProbeStatus.Available)
            return Results.Json(tool, statusCode: StatusCodes.Status503ServiceUnavailable);
        item.Activate(clock.UtcNow);
        var result = new { item.Provider, item.Version, item.Enabled, item.VerifiedAt, item.Revision };
        await IntelligenceAdminMutationSupport.CommitAsync(db, sideEffects, context, actor,
            clock, operation, "IntelligenceProviderActivated",
            "IntelligenceProviderRegistration", provider,
            new Dictionary<string, object?> { ["provider"] = provider,
                ["version"] = item.Version, ["revision"] = item.Revision },
            result, replay.Key, replay.Hash, StatusCodes.Status200OK, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> DisableAsync(string provider,
        ChangeProviderRegistrationRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IntelligenceDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var operation = $"intelligence.providers.disable:{provider}";
        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotency, operation, request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        var item = await db.ProviderRegistrations.SingleOrDefaultAsync(x => x.Provider == provider,
            cancellationToken);
        if (item is null) return Results.NotFound(new { code = "ai.provider.not_registered" });
        if (item.Revision != request.BaseRevision)
            return Results.Conflict(new { code = "ai.provider.revision_conflict" });
        item.Disable();
        var result = new { item.Provider, item.Version, item.Enabled, item.Revision };
        await IntelligenceAdminMutationSupport.CommitAsync(db, sideEffects, context, actor,
            clock, operation, "IntelligenceProviderDisabled",
            "IntelligenceProviderRegistration", provider,
            new Dictionary<string, object?> { ["provider"] = provider,
                ["version"] = item.Version, ["revision"] = item.Revision },
            result, replay.Key, replay.Hash, StatusCodes.Status200OK, cancellationToken);
        return Results.Ok(result);
    }
}
