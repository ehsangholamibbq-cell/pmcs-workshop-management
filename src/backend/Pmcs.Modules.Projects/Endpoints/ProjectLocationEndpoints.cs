using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Projects.Persistence;

namespace Pmcs.Modules.Projects.Endpoints;

internal static class ProjectLocationEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapProjectLocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/locations").WithTags("Project Locations");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{locationId:guid}/retire", RetireAsync);
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissions.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "projects.read",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var locations = await dbContext.ProjectLocations
            .AsNoTracking()
            .Where(location => location.TenantId == actor.TenantId && location.ProjectId == projectId)
            .OrderBy(location => location.ParentLocationId)
            .ThenBy(location => location.Code)
            .ToListAsync(cancellationToken);
        return Results.Ok(locations.Select(ProjectLocationResponse.From).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        Guid projectId,
        CreateProjectLocationRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissions.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "projects.locations.manage",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var replay = await GetReplayAsync(
            httpContext,
            actor,
            idempotency,
            "projects.locations.create",
            request,
            cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        if (!await dbContext.Projects.AsNoTracking().AnyAsync(
                project => project.TenantId == actor.TenantId && project.Id == projectId,
                cancellationToken))
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (request.ParentLocationId == Guid.Empty ||
            !await dbContext.ProjectLocations.AsNoTracking().AnyAsync(
                location => location.TenantId == actor.TenantId &&
                    location.ProjectId == projectId &&
                    location.Id == request.ParentLocationId &&
                    location.Status == ProjectLocationStatus.Active,
                cancellationToken))
        {
            return Results.UnprocessableEntity(new { code = "project.location.parent.not_active" });
        }

        var location = ProjectLocation.Create(
            Guid.NewGuid(),
            actor.TenantId,
            projectId,
            request.Code,
            request.Name,
            request.ParentLocationId,
            actor.UserId,
            clock.UtcNow);
        if (await dbContext.ProjectLocations.AsNoTracking().AnyAsync(
                item => item.TenantId == actor.TenantId &&
                    item.ProjectId == projectId &&
                    item.Code == location.Code,
                cancellationToken))
        {
            return Results.Conflict(new { code = "project.location.code.duplicate" });
        }

        dbContext.ProjectLocations.Add(location);
        var response = ProjectLocationResponse.From(location);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            location,
            response,
            replay,
            "ProjectLocationCreated",
            StatusCodes.Status201Created,
            sideEffects,
            clock,
            cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/locations/{location.Id}", response);
    }

    private static async Task<IResult> RetireAsync(
        Guid projectId,
        Guid locationId,
        RetireProjectLocationRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissions.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "projects.locations.manage",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var replay = await GetReplayAsync(
            httpContext,
            actor,
            idempotency,
            "projects.locations.retire",
            request,
            cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var location = await dbContext.ProjectLocations.SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.ProjectId == projectId && item.Id == locationId,
            cancellationToken);
        if (location is null)
        {
            return Results.NotFound(new { code = "project.location.not_found" });
        }

        if (location.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "project.location.revision.conflict", currentRevision = location.Revision });
        }

        if (await dbContext.ProjectLocations.AsNoTracking().AnyAsync(
                item => item.TenantId == actor.TenantId &&
                    item.ProjectId == projectId &&
                    item.ParentLocationId == locationId &&
                    item.Status == ProjectLocationStatus.Active,
                cancellationToken))
        {
            return Results.Conflict(new { code = "project.location.active_children" });
        }

        location.Retire(request.BaseRevision, clock.UtcNow);
        var response = ProjectLocationResponse.From(location);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            location,
            response,
            replay,
            "ProjectLocationRetired",
            StatusCodes.Status200OK,
            sideEffects,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<(string Key, string Hash, string Operation, IResult? Result)> GetReplayAsync<TRequest>(
        HttpContext httpContext,
        ICurrentActor actor,
        IIdempotencyStore store,
        string operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (string.Empty, string.Empty, operation, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }

        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await store.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return (key, hash, operation, replay is null
            ? null
            : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static async Task PersistAsync(
        ProjectsDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        ProjectLocation location,
        ProjectLocationResponse response,
        (string Key, string Hash, string Operation, IResult? Result) idempotency,
        string eventType,
        int statusCode,
        ITransactionalSideEffectWriter sideEffects,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    location.ProjectId,
                    actor.UserId,
                    eventType,
                    "ProjectLocation",
                    location.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["code"] = location.Code,
                        ["parentLocationId"] = location.ParentLocationId,
                        ["status"] = location.Status.ToString(),
                        ["revision"] = location.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    location.ProjectId,
                    $"Projects.{eventType}",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    idempotency.Operation,
                    idempotency.Hash,
                    statusCode,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
