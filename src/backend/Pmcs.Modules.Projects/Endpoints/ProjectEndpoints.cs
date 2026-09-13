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

internal static class ProjectEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects").WithTags("Projects");

        group.MapGet("/", ListAsync);
        group.MapGet("/{projectId:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{projectId:guid}/activate", ActivateAsync);
        group.MapPut("/{projectId:guid}/calendar", ConfigureCalendarAsync);
        group.MapPut("/{projectId:guid}/planning-mode", ConfigurePlanningModeAsync);
    }

    private static async Task<IResult> ListAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var scope = await permissionService.GetProjectScopeAsync(
            actor.TenantId,
            actor.UserId,
            "projects.read",
            cancellationToken);
        var projectIds = scope.ProjectIds.ToArray();
        var query = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.TenantId == actor.TenantId);
        if (!scope.AllProjects)
        {
            query = query.Where(project => projectIds.Contains(project.Id));
        }

        var projects = await query
            .OrderBy(project => project.Code)
            .ToListAsync(cancellationToken);

        return Results.Ok(projects.Select(ProjectResponse.From).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "projects.read",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == actor.TenantId && item.Id == projectId,
                cancellationToken);

        return project is null ? Results.NotFound() : Results.Ok(ProjectResponse.From(project));
    }

    private static async Task<IResult> CreateAsync(
        CreateProjectRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ProjectsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "projects.create",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" });
        }

        var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
        var requestHash = RequestHash.Create(requestJson);
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId,
            idempotencyKey,
            "projects.create",
            requestHash,
            cancellationToken);

        if (replay is not null)
        {
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        }

        var project = Project.Create(
            Guid.NewGuid(),
            actor.TenantId,
            request.Code,
            request.Name,
            request.ContractModel,
            request.PlanningMode,
            request.BudgetMode,
            request.QualityMode,
            request.HseMode,
            request.TimeZone,
            actor.UserId,
            clock.UtcNow,
            request.FinanceMode ?? CapabilityMode.Active,
            request.BaseCurrencyCode ?? "IRR",
            request.ProcurementMode ?? CapabilityMode.Active);

        if (await dbContext.Projects.AnyAsync(
                item => item.TenantId == actor.TenantId && item.Code == project.Code,
                cancellationToken))
        {
            return Results.Conflict(new { code = "project.code.duplicate" });
        }

        var rootLocation = ProjectLocation.Create(
            Guid.NewGuid(),
            actor.TenantId,
            project.Id,
            "ROOT",
            "کل پروژه",
            null,
            actor.UserId,
            clock.UtcNow);
        dbContext.Projects.Add(project);
        dbContext.ProjectLocations.Add(rootLocation);
        var response = ProjectResponse.From(project);
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    project.Id,
                    actor.UserId,
                    "ProjectCreated",
                    "Project",
                    project.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["code"] = project.Code,
                        ["planningMode"] = project.PlanningMode.ToString(),
                        ["budgetMode"] = project.BudgetMode.ToString(),
                        ["hseMode"] = project.HseMode.ToString(),
                        ["rootLocationId"] = rootLocation.Id
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    project.Id,
                    "Projects.ProjectCreated",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotencyKey,
                    "projects.create",
                    requestHash,
                    StatusCodes.Status201Created,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/v1/projects/{project.Id}", response);
    }

    private static async Task<IResult> ActivateAsync(
        Guid projectId,
        ActivateProjectRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ProjectsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "projects.activate",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" });
        }

        const string operation = "projects.activate";
        var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
        var requestHash = RequestHash.Create(requestJson);
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId,
            idempotencyKey,
            operation,
            requestHash,
            cancellationToken);
        if (replay is not null)
        {
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == projectId,
            cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (project.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "project.revision.conflict", currentRevision = project.Revision });
        }

        var hasRootLocation = await dbContext.ProjectLocations.AsNoTracking().AnyAsync(
            location => location.TenantId == actor.TenantId &&
                location.ProjectId == projectId &&
                location.ParentLocationId == null &&
                location.Code == "ROOT" &&
                location.Status == ProjectLocationStatus.Active,
            cancellationToken);
        if (!hasRootLocation)
        {
            return Results.UnprocessableEntity(new { code = "project.activate.location_root.required" });
        }

        project.Activate(request.BaseRevision, actor.UserId, clock.UtcNow);
        var response = ProjectResponse.From(project);
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    project.Id,
                    actor.UserId,
                    "ProjectActivated",
                    "Project",
                    project.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["status"] = project.Status.ToString(),
                        ["revision"] = project.Revision,
                        ["activatedAt"] = project.ActivatedAt
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    project.Id,
                    "Projects.ProjectActivated",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotencyKey,
                    operation,
                    requestHash,
                    StatusCodes.Status200OK,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> ConfigureCalendarAsync(
        Guid projectId,
        ConfigureProjectCalendarRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ProjectsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "projects.calendar.configure",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" });
        }

        var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
        var requestHash = RequestHash.Create(requestJson);
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId,
            idempotencyKey,
            "projects.configure-calendar",
            requestHash,
            cancellationToken);
        if (replay is not null)
        {
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == projectId,
            cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (project.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "project.revision.conflict", currentRevision = project.Revision });
        }

        project.ConfigureCalendar(
            request.BaseRevision,
            request.Mode,
            ProjectCalendarMask.FromDays(request.Mode, request.WorkingDays),
            clock.UtcNow);
        var response = ProjectResponse.From(project);
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    project.Id,
                    actor.UserId,
                    "ProjectCalendarConfigured",
                    "Project",
                    project.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["calendarMode"] = project.CalendarMode.ToString(),
                        ["workingDays"] = ProjectCalendarMask.ToDays(project.WorkingDaysMask).Select(day => day.ToString()).ToArray(),
                        ["revision"] = project.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    project.Id,
                    "Projects.ProjectCalendarConfigured",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotencyKey,
                    "projects.configure-calendar",
                    requestHash,
                    StatusCodes.Status200OK,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> ConfigurePlanningModeAsync(
        Guid projectId,
        ConfigureProjectPlanningModeRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ProjectsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "projects.planning.configure",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" });
        }

        var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
        var requestHash = RequestHash.Create(requestJson);
        const string operation = "projects.configure-planning-mode";
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId,
            idempotencyKey,
            operation,
            requestHash,
            cancellationToken);
        if (replay is not null)
        {
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        }

        var project = await dbContext.Projects.SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == projectId,
            cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (project.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "project.revision.conflict", currentRevision = project.Revision });
        }

        var previousMode = project.PlanningMode;
        project.ConfigurePlanningMode(request.BaseRevision, request.Mode, clock.UtcNow);
        var response = ProjectResponse.From(project);
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    project.Id,
                    actor.UserId,
                    "ProjectPlanningModeConfigured",
                    "Project",
                    project.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["previousMode"] = previousMode.ToString(),
                        ["planningMode"] = project.PlanningMode.ToString(),
                        ["revision"] = project.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    project.Id,
                    "Projects.ProjectPlanningModeConfigured",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotencyKey,
                    operation,
                    requestHash,
                    StatusCodes.Status200OK,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(response);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
