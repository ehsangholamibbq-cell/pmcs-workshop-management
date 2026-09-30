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

internal sealed record PublishIntelligenceProfileRequest(
    Guid TenantId, string UseCase, Guid[] ProjectIds,
    Guid DefaultModelId, Guid[] AllowedModelIds, Guid[] FallbackModelIds,
    bool AllowFallback, ModelCapability RequiredCapabilities,
    IntelligenceDataClass MaximumDataClass, int MaximumInputTokens,
    int MaximumOutputTokens, int TimeoutSeconds, long MaximumCostMicrounits,
    string PromptVersion, string PolicyVersion);

internal sealed record SelectIntelligenceProfileRequest(
    Guid TenantId, Guid ProfileVersionId, Guid ModelId, long BaseRevision);

internal sealed record IntelligenceProfileResponse(
    Guid Id, int Version, Guid TenantId, string UseCase,
    Guid[] ProjectIds, Guid DefaultModelId, Guid[] AllowedModelIds,
    Guid[] FallbackModelIds, bool AllowFallback,
    ModelCapability RequiredCapabilities, IntelligenceDataClass MaximumDataClass,
    int MaximumInputTokens, int MaximumOutputTokens, int TimeoutSeconds,
    long MaximumCostMicrounits, string PromptVersion, string PolicyVersion,
    DateTimeOffset PublishedAt)
{
    internal static IntelligenceProfileResponse From(IntelligenceProfileVersion version)
    {
        var policy = version.ToPolicy();
        return new(version.Id, version.Version, version.TenantId, version.UseCase,
            policy.ProjectIds.ToArray(), policy.DefaultModelId,
            policy.AllowedModelIds.ToArray(), policy.FallbackModelIds.ToArray(),
            policy.AllowFallback, policy.RequiredCapabilities, policy.MaximumDataClass,
            policy.MaximumInputTokens, policy.MaximumOutputTokens, policy.TimeoutSeconds,
            policy.MaximumCostMicrounits, policy.PromptVersion, policy.PolicyVersion,
            version.PublishedAt);
    }
}

internal sealed record IntelligenceSelectionResponse(
    Guid TenantId, string UseCase, Guid ProfileVersionId, Guid ModelId,
    long Revision, DateTimeOffset UpdatedAt)
{
    internal static IntelligenceSelectionResponse From(IntelligenceProfileSelection selection) =>
        new(selection.TenantId, selection.UseCase, selection.ProfileVersionId,
            selection.ModelId, selection.Revision, selection.UpdatedAt);
}

internal static class IntelligenceProfileAdministrationEndpoints
{
    internal static void MapIntelligenceProfileAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var profiles = endpoints.MapGroup("/api/v1/intelligence/admin/profiles")
            .WithTags("Intelligence administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
        profiles.MapGet("", ListAsync);
        profiles.MapPost("", PublishAsync);
        profiles.MapGet("/selection", GetSelectionAsync);
        profiles.MapPost("/selection", SelectAsync);
        profiles.MapPost("/rollback", RollbackAsync);
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId, ICurrentActor actor, IntelligenceAdministrationAccess access,
        IntelligenceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await CanReadTenantAsync(actor, tenantId, access, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var versions = await dbContext.ProfileVersions.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.PublishedAt).Take(100).ToArrayAsync(cancellationToken);
        return Results.Ok(versions.Select(IntelligenceProfileResponse.From).ToArray());
    }

    private static async Task<IResult> GetSelectionAsync(
        Guid tenantId, string useCase, ICurrentActor actor,
        IntelligenceAdministrationAccess access, IntelligenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await CanReadTenantAsync(actor, tenantId, access, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var selection = await dbContext.ProfileSelections.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.UseCase == useCase, cancellationToken);
        return selection is null
            ? Results.NotFound(new { code = "ai.profile.selection_not_found" })
            : Results.Ok(IntelligenceSelectionResponse.From(selection));
    }

    private static async Task<IResult> PublishAsync(
        PublishIntelligenceProfileRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> probes, IntelligenceDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanPublishProfilesAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotencyStore, "intelligence.profiles.publish", request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        if (request.UseCase != "int1.reference" || request.ProjectIds is null ||
            request.AllowedModelIds is null || request.FallbackModelIds is null ||
            (request.RequiredCapabilities & ModelCapability.StructuredOutput) == 0)
            return Results.BadRequest(new { code = "ai.profile.invalid" });

        var latest = await dbContext.ProfileVersions.AsNoTracking()
            .Where(item => item.TenantId == request.TenantId && item.UseCase == request.UseCase)
            .Select(item => (int?)item.Version).MaxAsync(cancellationToken) ?? 0;
        var profile = new ModelExecutionProfile(Guid.NewGuid(), latest + 1,
            request.UseCase, request.TenantId, request.ProjectIds.ToHashSet(),
            request.DefaultModelId, request.AllowedModelIds, request.FallbackModelIds,
            request.AllowFallback, request.RequiredCapabilities, request.MaximumDataClass,
            request.MaximumInputTokens, request.MaximumOutputTokens, request.TimeoutSeconds,
            request.MaximumCostMicrounits, request.PromptVersion, request.PolicyVersion);
        try
        {
            ModelSelectionPolicy.ValidateProfile(profile);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { code = "ai.profile.invalid" });
        }

        var models = await dbContext.ModelCatalog.AsNoTracking()
            .Where(item => request.AllowedModelIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        var catalog = models.Select(item => item.ToPolicy()).ToArray();
        if (profile.AllowedModelIds.Any(id => !ModelSelectionPolicy.IsApprovedModel(profile, catalog, id)) ||
            models.Any(item => !probes.Any(probe => probe.Provider == item.Provider &&
                probe.Availability.Model == item.Model &&
                probe.Availability.Status != ProviderProbeStatus.Unavailable)))
            return Results.Conflict(new { code = "ai.profile.model_unavailable" });

        var published = IntelligenceProfileVersion.Publish(profile, actor.UserId, clock.UtcNow);
        dbContext.ProfileVersions.Add(published);
        var response = IntelligenceProfileResponse.From(published);
        await IntelligenceAdminMutationSupport.CommitAsync(dbContext, sideEffects, context, actor,
            clock, "intelligence.profiles.publish", "IntelligenceProfilePublished",
            "IntelligenceProfileVersion", published.Id.ToString(),
            new Dictionary<string, object?> { ["targetTenantId"] = profile.TenantId,
                ["useCase"] = profile.UseCase, ["version"] = profile.Version },
            response, replay.Key, replay.Hash, StatusCodes.Status201Created, cancellationToken);
        return Results.Created($"/api/v1/intelligence/admin/profiles/{published.Id}", response);
    }

    private static Task<IResult> SelectAsync(
        SelectIntelligenceProfileRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> probes, IntelligenceDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken) =>
        ChangeSelectionAsync(request, false, context, actor, access, probes, dbContext,
            clock, sideEffects, idempotencyStore, cancellationToken);

    private static Task<IResult> RollbackAsync(
        SelectIntelligenceProfileRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> probes, IntelligenceDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken) =>
        ChangeSelectionAsync(request, true, context, actor, access, probes, dbContext,
            clock, sideEffects, idempotencyStore, cancellationToken);

    private static async Task<IResult> ChangeSelectionAsync(
        SelectIntelligenceProfileRequest request, bool rollback, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IEnumerable<IModelProviderProbe> probes, IntelligenceDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (request.TenantId == Guid.Empty ||
            !(await access.CanPublishProfilesAsync(actor, cancellationToken) ||
              !rollback && await access.CanSelectProfileAsync(actor, request.TenantId, cancellationToken)))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var operation = rollback ? "intelligence.profiles.rollback" : "intelligence.profiles.select";
        var replay = await IntelligenceAdminMutationSupport.CheckAsync(context, actor,
            idempotencyStore, operation, request, cancellationToken);
        if (replay.Result is not null) return replay.Result;

        var version = await dbContext.ProfileVersions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.ProfileVersionId && item.TenantId == request.TenantId,
            cancellationToken);
        if (version is null) return Results.NotFound(new { code = "ai.profile.not_found" });
        var profile = version.ToPolicy();
        var model = await dbContext.ModelCatalog.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.ModelId, cancellationToken);
        if (model is null || !ModelSelectionPolicy.IsApprovedModel(profile, [model.ToPolicy()], request.ModelId) ||
            !probes.Any(probe => probe.Provider == model.Provider &&
                probe.Availability.Model == model.Model &&
                probe.Availability.Status != ProviderProbeStatus.Unavailable))
            return Results.Conflict(new { code = "ai.profile.model_unavailable" });

        var selection = await dbContext.ProfileSelections.SingleOrDefaultAsync(item =>
            item.TenantId == request.TenantId && item.UseCase == profile.UseCase,
            cancellationToken);
        if (selection is null)
        {
            if (rollback || request.BaseRevision != 0)
                return Results.Conflict(new { code = "ai.profile.revision_conflict" });
            selection = IntelligenceProfileSelection.Create(profile, request.ModelId, actor.UserId, clock.UtcNow);
            dbContext.ProfileSelections.Add(selection);
        }
        else
        {
            if (selection.Revision != request.BaseRevision ||
                rollback && (await dbContext.ProfileVersions.AsNoTracking().SingleAsync(
                    item => item.Id == selection.ProfileVersionId, cancellationToken)).Version <= version.Version)
                return Results.Conflict(new { code = "ai.profile.revision_conflict" });
            selection.Change(profile, request.ModelId, request.BaseRevision, actor.UserId, clock.UtcNow);
        }

        var response = IntelligenceSelectionResponse.From(selection);
        await IntelligenceAdminMutationSupport.CommitAsync(dbContext, sideEffects, context, actor,
            clock, operation, rollback ? "IntelligenceProfileRolledBack" : "IntelligenceProfileSelected",
            "IntelligenceProfileSelection", $"{request.TenantId:N}:{profile.UseCase}",
            new Dictionary<string, object?> { ["targetTenantId"] = request.TenantId,
                ["profileVersionId"] = version.Id, ["modelId"] = model.Id,
                ["revision"] = selection.Revision },
            response, replay.Key, replay.Hash, StatusCodes.Status200OK, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<bool> CanReadTenantAsync(ICurrentActor actor, Guid tenantId,
        IntelligenceAdministrationAccess access, CancellationToken cancellationToken) =>
        tenantId != Guid.Empty &&
        (await access.CanPublishProfilesAsync(actor, cancellationToken) ||
         await access.CanSelectProfileAsync(actor, tenantId, cancellationToken));
}
