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

internal sealed record RegisterIntelligenceModelRequest(
    string Provider, string Model, ModelCapability Capabilities,
    IntelligenceDataClass MaximumDataClass);

internal sealed record ChangeIntelligenceModelRequest(long BaseRevision);

internal sealed record IntelligenceModelResponse(
    Guid Id, int Version, string Provider, string Model, ModelCapability Capabilities,
    IntelligenceDataClass MaximumDataClass, bool Enabled, DateTimeOffset? VerifiedAt,
    long Revision)
{
    internal static IntelligenceModelResponse From(IntelligenceModelCatalog entry) =>
        new(entry.Id, entry.Version, entry.Provider, entry.Model, entry.Capabilities,
            entry.MaximumDataClass, entry.Enabled, entry.VerifiedAt, entry.Revision);
}

internal static class IntelligenceModelCatalogEndpoints
{
    internal static void MapIntelligenceModelCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var models = endpoints.MapGroup("/api/v1/intelligence/admin/models")
            .WithTags("Intelligence administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
        models.MapGet("", ListAsync);
        models.MapPost("", RegisterAsync);
        models.MapPost("/{modelId:guid}/verify", VerifyAsync);
        models.MapPost("/{modelId:guid}/disable", DisableAsync);
    }

    private static async Task<IResult> ListAsync(
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IntelligenceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanReadCatalogAsync(actor, null, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var models = await dbContext.ModelCatalog.AsNoTracking()
            .OrderBy(x => x.Provider).ThenBy(x => x.Model).ThenByDescending(x => x.Version)
            .Take(100).ToArrayAsync(cancellationToken);
        return Results.Ok(models.Select(IntelligenceModelResponse.From).ToArray());
    }

    private static async Task<IResult> RegisterAsync(
        RegisterIntelligenceModelRequest request, HttpContext context, ICurrentActor actor,
        IntelligenceAdministrationAccess access, IEnumerable<IModelProviderProbe> probes,
        IntelligenceDbContext dbContext, IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var probe = probes.SingleOrDefault(x => x.Provider == request.Provider);
        if (probe is null || probe.Availability.Status == ProviderProbeStatus.Unavailable ||
            probe.Availability.Model != request.Model)
            return Results.BadRequest(new { code = "ai.model.configuration_unavailable" });

        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotencyStore, "intelligence.models.register", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;

        var latest = await dbContext.ModelCatalog.AsNoTracking()
            .Where(x => x.Provider == request.Provider && x.Model == request.Model)
            .Select(x => (int?)x.Version).MaxAsync(cancellationToken) ?? 0;
        IntelligenceModelCatalog entry;
        try
        {
            entry = IntelligenceModelCatalog.Create(Guid.NewGuid(), latest + 1,
                request.Provider, request.Model, request.Capabilities,
                request.MaximumDataClass, actor.UserId, clock.UtcNow);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { code = "ai.model.invalid" });
        }

        dbContext.ModelCatalog.Add(entry);
        var response = IntelligenceModelResponse.From(entry);
        await IntelligenceAdminMutationSupport.CommitAsync(dbContext, sideEffects, context, actor,
            clock, "intelligence.models.register", "IntelligenceModelRegistered",
            "IntelligenceModelCatalog", entry.Id.ToString(),
            new Dictionary<string, object?> { ["provider"] = entry.Provider,
                ["model"] = entry.Model, ["version"] = entry.Version },
            response, replay.Key, replay.Hash, StatusCodes.Status201Created, cancellationToken);
        return Results.Created($"/api/v1/intelligence/admin/models/{entry.Id}", response);
    }

    private static async Task<IResult> VerifyAsync(
        Guid modelId, ChangeIntelligenceModelRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> probes, IntelligenceDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotencyStore, "intelligence.models.verify", new { modelId, request.BaseRevision }, cancellationToken);
        if (replay.Result is not null) return replay.Result;

        var entry = await dbContext.ModelCatalog.SingleOrDefaultAsync(x => x.Id == modelId, cancellationToken);
        if (entry is null) return Results.NotFound(new { code = "ai.model.not_found" });
        if (entry.Revision != request.BaseRevision)
            return Results.Conflict(new { code = "ai.model.revision_conflict" });
        if ((entry.Capabilities & ModelCapability.ToolCalling) != 0)
            return Results.Conflict(new { code = "ai.model.tool_capability_unverified" });
        var probe = probes.Single(x => x.Provider == entry.Provider);
        if (probe.Availability.Model != entry.Model)
            return Results.Conflict(new { code = "ai.model.configuration_changed" });
        var result = await probe.ProbeAsync(cancellationToken);
        if (result.Status != ProviderProbeStatus.Available)
            return Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);

        entry.Verify(result.Provider, result.Model!, clock.UtcNow);
        var response = IntelligenceModelResponse.From(entry);
        await IntelligenceAdminMutationSupport.CommitAsync(dbContext, sideEffects, context, actor,
            clock, "intelligence.models.verify", "IntelligenceModelVerified",
            "IntelligenceModelCatalog", entry.Id.ToString(),
            new Dictionary<string, object?> { ["provider"] = entry.Provider,
                ["version"] = entry.Version, ["revision"] = entry.Revision },
            response, replay.Key, replay.Hash, StatusCodes.Status200OK, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> DisableAsync(
        Guid modelId, ChangeIntelligenceModelRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IntelligenceDbContext dbContext, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotencyStore, "intelligence.models.disable", new { modelId, request.BaseRevision }, cancellationToken);
        if (replay.Result is not null) return replay.Result;

        var entry = await dbContext.ModelCatalog.SingleOrDefaultAsync(x => x.Id == modelId, cancellationToken);
        if (entry is null) return Results.NotFound(new { code = "ai.model.not_found" });
        if (entry.Revision != request.BaseRevision)
            return Results.Conflict(new { code = "ai.model.revision_conflict" });

        entry.Disable();
        var response = IntelligenceModelResponse.From(entry);
        await IntelligenceAdminMutationSupport.CommitAsync(dbContext, sideEffects, context, actor,
            clock, "intelligence.models.disable", "IntelligenceModelDisabled",
            "IntelligenceModelCatalog", entry.Id.ToString(),
            new Dictionary<string, object?> { ["provider"] = entry.Provider,
                ["version"] = entry.Version, ["revision"] = entry.Revision },
            response, replay.Key, replay.Hash, StatusCodes.Status200OK, cancellationToken);
        return Results.Ok(response);
    }
}
