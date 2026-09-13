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
        group.MapGet("/{projectId:guid}/readiness", GetReadinessAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{projectId:guid}/activate", ActivateAsync);
        group.MapPut("/{projectId:guid}/setup", ConfigureSetupAsync);
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

    private static async Task<IResult> GetReadinessAsync(
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
                actor.TenantId, actor.UserId, projectId, "projects.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == projectId,
            cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        return Results.Ok(await EvaluateReadinessAsync(project, permissionService, dbContext, cancellationToken));
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
            request.FinanceMode ?? CapabilityMode.SetupRequired,
            request.BaseCurrencyCode ?? "IRR",
            request.ProcurementMode ?? CapabilityMode.SetupRequired,
            request.ProjectType,
            request.ExecutionPhase,
            request.CountryCode,
            request.Region,
            request.StartDate,
            request.PlannedFinishDate,
            request.ShortDescription,
            request.UnitSystem,
            request.DailyCutoffLocalTime,
            request.ReportingFrequency,
            request.DailyReportWorkflow,
            request.OfflinePolicyAccepted);

        if (request.CalendarMode != ProjectCalendarMode.NotConfigured || request.WorkingDays is { Count: > 0 })
        {
            project.ConfigureCalendar(
                project.Revision,
                request.CalendarMode,
                ProjectCalendarMask.FromDays(request.CalendarMode, request.WorkingDays),
                clock.UtcNow);
        }

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
                        ["configurationVersion"] = project.ConfigurationVersion,
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

    private static async Task<IResult> ConfigureSetupAsync(
        Guid projectId,
        ConfigureProjectSetupRequest request,
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
                actor.TenantId, actor.UserId, projectId, "projects.setup.configure", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" });
        }

        const string operation = "projects.configure-setup";
        var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
        var requestHash = RequestHash.Create(requestJson);
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId, key, operation, requestHash, cancellationToken);
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

        var sensitive = project.Status != ProjectStatus.Draft;
        var mayChangeSensitiveSetup = !sensitive || await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            "projects.setup.configure-sensitive",
            cancellationToken);

        project.ConfigureSetup(
            request.BaseRevision,
            request.ContractModel,
            request.PlanningMode,
            request.BudgetMode,
            request.QualityMode,
            request.HseMode,
            request.FinanceMode,
            request.ProcurementMode,
            request.CalendarMode,
            ProjectCalendarMask.FromDays(request.CalendarMode, request.WorkingDays),
            request.ProjectType,
            request.ExecutionPhase,
            request.CountryCode,
            request.Region,
            request.StartDate,
            request.PlannedFinishDate,
            request.ShortDescription,
            request.TimeZone,
            request.BaseCurrencyCode,
            request.UnitSystem,
            request.DailyCutoffLocalTime,
            request.ReportingFrequency,
            request.DailyReportWorkflow,
            request.OfflinePolicyAccepted,
            clock.UtcNow,
            mayChangeSensitiveSetup,
            request.Reason);

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
                    sensitive ? "ProjectSensitiveSetupChanged" : "ProjectSetupConfigured",
                    "Project",
                    project.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["configurationVersion"] = project.ConfigurationVersion,
                        ["revision"] = project.Revision,
                        ["reason"] = request.Reason,
                        ["sensitive"] = sensitive
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, project.Id, "Projects.ProjectSetupConfigured", 1,
                    clock.UtcNow, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, key, operation, requestHash, StatusCodes.Status200OK,
                    responseJson, clock.UtcNow, clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
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

        var readiness = await EvaluateReadinessAsync(project, permissionService, dbContext, cancellationToken);
        if (!readiness.IsReady)
        {
            return Results.UnprocessableEntity(new { code = "project.activate.readiness_failed", readiness });
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
                        ["activatedAt"] = project.ActivatedAt,
                        ["configurationVersion"] = project.ActivatedConfigurationVersion
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
                        ["configurationVersion"] = project.ConfigurationVersion,
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
                        ["configurationVersion"] = project.ConfigurationVersion,
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

    private static async Task<ProjectReadinessResponse> EvaluateReadinessAsync(
        Project project,
        IProjectPermissionService permissionService,
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var access = await permissionService.GetProjectAccessReadinessAsync(
            project.TenantId, project.Id, cancellationToken);
        var hasRootLocation = await dbContext.ProjectLocations.AsNoTracking().AnyAsync(
            location => location.TenantId == project.TenantId &&
                location.ProjectId == project.Id &&
                location.ParentLocationId == null &&
                location.Code == "ROOT" &&
                location.Status == ProjectLocationStatus.Active,
            cancellationToken);

        var items = new List<ProjectReadinessItem>();
        Add("identity", "مشخصات پایه پروژه",
            project.ProjectType != ProjectType.NotConfigured &&
            project.ExecutionPhase != ProjectExecutionPhase.NotConfigured &&
            project.CountryCode.Length == 2 &&
            !string.IsNullOrWhiteSpace(project.Region) &&
            !string.IsNullOrWhiteSpace(project.ShortDescription),
            "نوع، مرحله اجرا، کشور، منطقه و شرح کوتاه باید کامل باشد.");
        Add("dates", "تاریخ‌های اصلی",
            project.StartDate.HasValue && project.PlannedFinishDate.HasValue &&
            project.PlannedFinishDate.Value >= project.StartDate.Value,
            "تاریخ شروع و پایان برنامه‌ای معتبر لازم است.");
        Add("contract-model", "مدل قراردادی پایه", project.ContractModel != ContractModel.NotConfigured,
            "انتخاب مدل قراردادی پایه الزامی است؛ ثبت قرارداد رسمی می‌تواند بعداً انجام شود.");
        Add("location-root", "ساختار مکانی پایه", hasRootLocation,
            "ریشهٔ فعال «کل پروژه» باید وجود داشته باشد.");
        Add("project-manager", "مدیر پروژه", access.ActiveProjectManagerCount > 0,
            "حداقل یک عضویت فعال با نقش مدیر پروژه لازم است.");
        Add("tenant-admin", "مدیر سازمان", access.ActiveAdministratorCount > 0,
            "حداقل یک مدیر فعال سازمان برای بازیابی و کنترل دسترسی لازم است.");
        Add("operational-users", "کاربر عملیاتی", access.ActiveOperationalUserCount > 0,
            "حداقل یک کاربر فعال عملیاتی باید به پروژه دسترسی داشته باشد.");
        Add("calendar", "تقویم کاری و منطقه زمانی",
            project.CalendarMode == ProjectCalendarMode.WorkingWeek &&
            project.WorkingDaysMask is > 0 &&
            !string.IsNullOrWhiteSpace(project.TimeZone),
            "روزهای کاری و منطقه زمانی IANA باید مشخص باشند.");
        Add("units", "واحد اندازه‌گیری", project.UnitSystem != ProjectUnitSystem.NotConfigured,
            "سامانه واحدهای پروژه باید تعیین شود.");
        Add("daily-report", "گردش گزارش روزانه",
            project.DailyCutoffLocalTime.HasValue &&
            project.ReportingFrequency != ReportingFrequency.NotConfigured &&
            project.DailyReportWorkflow != DailyReportWorkflow.NotConfigured,
            "زمان قطع، بسامد و گردش تأیید گزارش روزانه باید تعیین شود.");
        Add("offline-policy", "سیاست کار آفلاین", project.OfflinePolicyAccepted,
            "سیاست ثبت آفلاین، همگام‌سازی و تعارض باید پذیرفته شود.");
        items.Add(new ProjectReadinessItem(
            "audit-policy", "ممیزی تغییرات", ProjectReadinessStatus.Passed,
            "فرمان‌های راه‌اندازی فقط همراه ممیزی تراکنشی ثبت می‌شوند."));

        AddModule("planning", "برنامه‌ریزی", project.PlanningMode == PlanningMode.None || project.PlanningMode == PlanningMode.SimpleWorkList,
            project.PlanningMode is PlanningMode.Milestones or PlanningMode.WbsBaseline or PlanningMode.ExternalSchedule);
        AddCapability("budget", "بودجه", project.BudgetMode);
        AddCapability("finance", "مالی", project.FinanceMode);
        AddCapability("procurement", "تدارکات", project.ProcurementMode);
        AddCapability("quality", "کیفیت", project.QualityMode);
        AddCapability("hse", "ایمنی", project.HseMode);

        var blocked = items.Count(item => item.Status == ProjectReadinessStatus.Blocked);
        var completion = items.Count == 0
            ? 0
            : (int)Math.Round(100m * (items.Count - blocked) / items.Count, MidpointRounding.AwayFromZero);
        return new ProjectReadinessResponse(
            project.Id, project.ConfigurationVersion, blocked == 0, completion, items);

        void Add(string code, string title, bool passed, string blockedDetail) => items.Add(
            new ProjectReadinessItem(
                code,
                title,
                passed ? ProjectReadinessStatus.Passed : ProjectReadinessStatus.Blocked,
                passed ? "آماده است." : blockedDetail));

        void AddModule(string code, string title, bool ready, bool selectedWithoutEvidence)
        {
            items.Add(new ProjectReadinessItem(
                $"module-{code}",
                $"آمادگی ماژول {title}",
                selectedWithoutEvidence ? ProjectReadinessStatus.Blocked :
                    ready ? ProjectReadinessStatus.Passed : ProjectReadinessStatus.Warning,
                selectedWithoutEvidence
                    ? "ماژول انتخاب شده اما شواهد آماده‌بودن تنظیمات آن ثبت نشده است."
                    : ready ? "برای راه‌اندازی پایه آماده است." : "ماژول در راه‌اندازی پایه الزامی نیست."));
        }

        void AddCapability(string code, string title, CapabilityMode mode)
        {
            items.Add(new ProjectReadinessItem(
                $"module-{code}",
                $"آمادگی ماژول {title}",
                mode == CapabilityMode.Active ? ProjectReadinessStatus.Blocked :
                    mode == CapabilityMode.Suspended ? ProjectReadinessStatus.Warning : ProjectReadinessStatus.Passed,
                mode == CapabilityMode.Active
                    ? "ماژول فعال انتخاب شده اما Gate اختصاصی تنظیمات آن هنوز تأیید نشده است."
                    : mode == CapabilityMode.SetupRequired
                        ? "راه‌اندازی این ماژول به بعد از فعال‌سازی پایه موکول شده است."
                        : mode == CapabilityMode.Suspended
                            ? "ماژول تعلیق شده و در راه‌اندازی پایه وارد نمی‌شود."
                            : "ماژول برای راه‌اندازی پایه غیرفعال است."));
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
