using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Planning.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Planning.Endpoints;

internal static partial class PlanningEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/planning").WithTags("Planning and Progress");
        group.MapGet("/measurement-items", ListMeasurementItemsAsync);
        group.MapPost("/measurement-items", CreateMeasurementItemAsync);
        group.MapPut("/measurement-items/{itemId:guid}", AmendMeasurementItemAsync);
        group.MapPost("/measurement-items/{itemId:guid}/deactivate", DeactivateMeasurementItemAsync);
        group.MapGet("/progress", GetProgressAsync);
        MapPlanningBasisEndpoints(group);
    }

    private static async Task<IResult> ListMeasurementItemsAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        PlanningDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.measurement-items.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var items = await dbContext.MeasurementItems
            .AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(MeasurementItemResponse.From).ToArray());
    }

    private static async Task<IResult> CreateMeasurementItemAsync(
        Guid projectId,
        CreateMeasurementItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.measurement-items.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            $"planning.measurement-items.create:{projectId:N}",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (!await projectDirectory.ExistsAsync(actor.TenantId, projectId, cancellationToken))
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var normalizedCode = string.IsNullOrWhiteSpace(request.Code)
            ? string.Empty
            : request.Code.Trim().ToUpperInvariant();
        if (await dbContext.MeasurementItems.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.Code == normalizedCode,
                cancellationToken))
        {
            return Results.Conflict(new { code = "measurement_item.code.duplicate" });
        }

        var item = MeasurementItem.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.Code,
            request.Title,
            request.Unit,
            request.TargetQuantity,
            request.Notes,
            actor.UserId,
            clock.UtcNow);
        dbContext.MeasurementItems.Add(item);
        var response = MeasurementItemResponse.From(item);
        await PersistAsync(
            dbContext, httpContext, actor, item, "MeasurementItemCreated", response,
            idempotency, StatusCodes.Status201Created, sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/planning/measurement-items/{item.Id}", response);
    }

    private static async Task<IResult> AmendMeasurementItemAsync(
        Guid projectId,
        Guid itemId,
        AmendMeasurementItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.measurement-items.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            $"planning.measurement-items.amend:{itemId:N}",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var item = await FindItemAsync(dbContext, actor.TenantId, projectId, itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }
        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict(item.Revision);
        }

        item.Amend(request.BaseRevision, request.Title, request.TargetQuantity, request.Notes, actor.UserId, clock.UtcNow);
        var response = MeasurementItemResponse.From(item);
        await PersistAsync(
            dbContext, httpContext, actor, item, "MeasurementItemAmended", response,
            idempotency, StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> DeactivateMeasurementItemAsync(
        Guid projectId,
        Guid itemId,
        DeactivateMeasurementItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.measurement-items.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            $"planning.measurement-items.deactivate:{itemId:N}",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var item = await FindItemAsync(dbContext, actor.TenantId, projectId, itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }
        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict(item.Revision);
        }

        item.Deactivate(request.BaseRevision, actor.UserId, clock.UtcNow);
        var response = MeasurementItemResponse.From(item);
        await PersistAsync(
            dbContext, httpContext, actor, item, "MeasurementItemDeactivated", response,
            idempotency, StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetProgressAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        IProgressFactSource factSource,
        PlanningDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.progress.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var items = await dbContext.MeasurementItems
            .AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var facts = await factSource.LoadAsync(actor.TenantId, projectId, cancellationToken);
        var baseline = await dbContext.PlanningBaselines
            .AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                item.Status == PlanningBaselineStatus.Approved)
            .OrderByDescending(item => item.ReviewedAt)
            .FirstOrDefaultAsync(cancellationToken);
        IReadOnlyCollection<MilestoneProgressUpdate> milestoneUpdates = baseline is null
            ? Array.Empty<MilestoneProgressUpdate>()
            : await dbContext.MilestoneProgressUpdates
                .AsNoTracking()
                .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.BaselineId == baseline.Id && item.Status == MilestoneProgressStatus.Approved)
                .ToListAsync(cancellationToken);
        var calculation = ProgressLedgerCalculator.Calculate(
            project.PlanningMode,
            items.Select(item => new MeasurementItemInput(
                item.Id,
                item.Code,
                item.Title,
                item.Unit,
                item.TargetQuantity,
                item.Status == MeasurementItemStatus.Active)).ToArray(),
            facts.Select(fact => new ProgressObservationInput(
                fact.FactId,
                fact.MeasurementItemId,
                fact.ReportDate,
                fact.ReviewState == ProgressFactReviewState.Approved
                    ? ProgressReviewState.Approved
                    : ProgressReviewState.Provisional,
                fact.Quantity)).ToArray(),
            baseline is null
                ? null
                : new ApprovedPlanningBaselineInput(
                    baseline.Id,
                    baseline.VersionCode,
                    baseline.Kind,
                    baseline.Entries.Select(entry => new PlanningBaselineEntryInput(
                        entry.Id,
                        entry.Code,
                        entry.Title,
                        entry.Kind,
                        entry.MeasurementMethod,
                        entry.MeasurementItemId,
                        entry.PlannedStart,
                        entry.PlannedFinish,
                        entry.WeightPercent,
                        entry.SortOrder)).ToArray()),
            milestoneUpdates.Select(item => new ApprovedMilestoneProgressInput(
                item.BaselineId,
                item.BaselineEntryId,
                item.StatusDate,
                item.ProgressPercent,
                item.ReviewedAt!.Value)).ToArray(),
            ProjectLocalDate(clock.UtcNow, project.TimeZone),
            project.Calendar.WorkingDaysMask);
        return Results.Ok(ProgressLedgerResponse.From(calculation));
    }

    private static async Task PersistAsync<TResponse>(
        PlanningDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        MeasurementItem item,
        string eventType,
        TResponse response,
        IdempotencyContext idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    item.ProjectId,
                    actor.UserId,
                    eventType,
                    "MeasurementItem",
                    item.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["code"] = item.Code,
                        ["title"] = item.Title,
                        ["unit"] = item.Unit,
                        ["targetQuantity"] = item.TargetQuantity,
                        ["status"] = item.Status.ToString(),
                        ["revision"] = item.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, item.ProjectId, $"Planning.{eventType}", 1,
                    clock.UtcNow, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, idempotency.Key, idempotency.Operation, idempotency.Hash,
                    statusCode, responseJson, clock.UtcNow, clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<IdempotencyContext> GetReplayAsync<TRequest>(
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
            return new IdempotencyContext(
                string.Empty,
                string.Empty,
                operation,
                Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Idempotency-Key is required.",
                    extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }

        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await store.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return new IdempotencyContext(
            key,
            hash,
            operation,
            replay is null
                ? null
                : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static Task<MeasurementItem?> FindItemAsync(
        PlanningDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid itemId,
        CancellationToken cancellationToken) =>
        dbContext.MeasurementItems.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == itemId,
            cancellationToken);

    private static Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, projectId, permission, cancellationToken);

    private static IResult RevisionConflict(long currentRevision) => Results.Conflict(new
    {
        code = "measurement_item.revision.conflict",
        currentRevision
    });

    private static DateOnly ProjectLocalDate(DateTimeOffset instant, string timeZone)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record IdempotencyContext(string Key, string Hash, string Operation, IResult? Result);
}
