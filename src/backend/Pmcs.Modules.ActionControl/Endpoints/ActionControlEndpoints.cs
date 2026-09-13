using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Persistence;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ActionControl.Endpoints;

internal static partial class ActionControlEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapActionControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var actions = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/actions").WithTags("Action Control");
        actions.MapGet("/", ListAsync);
        actions.MapGet("/{actionId:guid}", GetAsync);
        actions.MapPost("/{actionId:guid}/transition", TransitionAsync);

        var attention = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/attention").WithTags("Action Control");
        attention.MapPost("/{sourceFactId:guid}/actions", CreateFromAttentionAsync);
        attention.MapPost("/{sourceFactId:guid}/dismiss", DismissAttentionAsync);

        MapGovernanceEndpoints(endpoints);
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        ManagementActionStatus? status,
        bool? assignedToMe,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ActionControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "actions.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = dbContext.Actions.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (assignedToMe == true)
        {
            query = query.Where(item => item.AssigneeUserId == actor.UserId);
        }

        var actions = await query
            .OrderBy(item => item.Status == ManagementActionStatus.Done || item.Status == ManagementActionStatus.Cancelled)
            .ThenBy(item => item.DueDate)
            .ThenByDescending(item => item.Priority)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Results.Ok(actions.Select(ManagementActionResponse.From).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid projectId,
        Guid actionId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ActionControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "actions.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var action = await FindActionAsync(dbContext, actor.TenantId, projectId, actionId, cancellationToken);
        return action is null ? Results.NotFound() : Results.Ok(ManagementActionResponse.From(action));
    }

    private static async Task<IResult> CreateFromAttentionAsync(
        Guid projectId,
        Guid sourceFactId,
        CreateActionFromAttentionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IDailyFactDirectory dailyFactDirectory,
        IProjectAssigneeDirectory assigneeDirectory,
        IProjectDirectory projectDirectory,
        ActionControlDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        ITransactionalNotificationWriter notificationWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "attention.triage", cancellationToken) ||
            !await HasPermissionAsync(permissionService, actor, projectId, "actions.create", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "attention.create-action", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (request.DueDate < ResolveLocalDate(clock.UtcNow, project.TimeZone))
        {
            throw new DomainRuleException("action.due_date.in_past", "A new action due date cannot be in the past.");
        }

        var source = await dailyFactDirectory.FindApprovedAttentionFactAsync(
            actor.TenantId, projectId, sourceFactId, cancellationToken);
        if (source is null)
        {
            return Results.NotFound(new { code = "attention.source.not_found_or_not_approved" });
        }

        var assignee = await assigneeDirectory.FindAssignableAsync(
            actor.TenantId, projectId, request.AssigneeUserId, cancellationToken);
        if (assignee is null)
        {
            return Results.UnprocessableEntity(new { code = "action.assignee.not_assignable" });
        }

        if (await dbContext.AttentionDispositions.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.SourceFactId == sourceFactId,
                cancellationToken))
        {
            return Results.Conflict(new { code = "attention.already_triaged" });
        }

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? Truncate(source.Description ?? "اقدام ناشی از مشاهده کارگاه", 240)
            : request.Title;
        var action = ManagementAction.Create(
            request.ClientGeneratedId,
            actor.TenantId,
            projectId,
            sourceFactId,
            title,
            request.Description,
            assignee.UserId,
            assignee.DisplayName,
            request.DueDate,
            request.Priority,
            actor.UserId,
            clock.UtcNow);
        var disposition = AttentionDisposition.Converted(
            Guid.NewGuid(), actor.TenantId, projectId, sourceFactId, action.Id, actor.UserId, clock.UtcNow);
        dbContext.Actions.Add(action);
        dbContext.AttentionDispositions.Add(disposition);
        var response = ManagementActionResponse.From(action);
        InAppNotificationDraft[] notifications = action.AssigneeUserId == actor.UserId
            ? []
            : new[]
            {
                new InAppNotificationDraft(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    action.AssigneeUserId,
                    $"management-action:{action.Id}:assigned:{action.Revision}",
                    "ManagementActionAssigned",
                    "اقدام جدید به شما واگذار شد",
                    action.Title,
                    "ManagementAction",
                    action.Id,
                    clock.UtcNow)
            };
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            projectId,
            action.Id,
            "ManagementAction",
            "AttentionConvertedToAction",
            new Dictionary<string, object?>
            {
                ["sourceFactId"] = sourceFactId,
                ["assigneeUserId"] = action.AssigneeUserId,
                ["dueDate"] = action.DueDate,
                ["priority"] = action.Priority.ToString(),
                ["status"] = action.Status.ToString(),
                ["revision"] = action.Revision
            },
            response,
            idempotency,
            StatusCodes.Status201Created,
            sideEffectWriter,
            clock,
            cancellationToken,
            notificationWriter,
            notifications);
        return Results.Created($"/api/v1/projects/{projectId}/actions/{action.Id}", response);
    }

    private static async Task<IResult> DismissAttentionAsync(
        Guid projectId,
        Guid sourceFactId,
        DismissAttentionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IDailyFactDirectory dailyFactDirectory,
        ActionControlDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "attention.triage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "attention.dismiss", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var source = await dailyFactDirectory.FindApprovedAttentionFactAsync(
            actor.TenantId, projectId, sourceFactId, cancellationToken);
        if (source is null)
        {
            return Results.NotFound(new { code = "attention.source.not_found_or_not_approved" });
        }

        if (await dbContext.AttentionDispositions.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.SourceFactId == sourceFactId,
                cancellationToken))
        {
            return Results.Conflict(new { code = "attention.already_triaged" });
        }

        var disposition = AttentionDisposition.Dismissed(
            Guid.NewGuid(), actor.TenantId, projectId, sourceFactId, request.Reason, actor.UserId, clock.UtcNow);
        dbContext.AttentionDispositions.Add(disposition);
        var response = new AttentionDismissalResponse(
            sourceFactId,
            disposition.Kind,
            disposition.Reason!,
            disposition.DecidedBy,
            disposition.DecidedAt);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            projectId,
            disposition.Id,
            "AttentionDisposition",
            "AttentionDismissed",
            new Dictionary<string, object?>
            {
                ["sourceFactId"] = sourceFactId,
                ["kind"] = disposition.Kind.ToString(),
                ["reason"] = disposition.Reason
            },
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> TransitionAsync(
        Guid projectId,
        Guid actionId,
        TransitionActionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ActionControlDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "actions.update", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "actions.transition", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var action = await FindActionAsync(dbContext, actor.TenantId, projectId, actionId, cancellationToken);
        if (action is null)
        {
            return Results.NotFound();
        }

        if (action.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "action.revision.conflict", currentRevision = action.Revision });
        }

        if (action.AssigneeUserId != actor.UserId && !await HasPermissionAsync(
                permissionService, actor, projectId, "actions.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        action.Transition(request.BaseRevision, request.TargetStatus, actor.UserId, clock.UtcNow);
        var response = ManagementActionResponse.From(action);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            projectId,
            action.Id,
            "ManagementAction",
            "ManagementActionTransitioned",
            new Dictionary<string, object?>
            {
                ["sourceFactId"] = action.SourceFactId,
                ["status"] = action.Status.ToString(),
                ["revision"] = action.Revision,
                ["changedBy"] = actor.UserId
            },
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task PersistAsync<TResponse>(
        ActionControlDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid projectId,
        Guid resourceId,
        string resourceType,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        (string Key, string Hash, string Operation, IResult? Result) idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken,
        ITransactionalNotificationWriter? notificationWriter = null,
        IReadOnlyCollection<InAppNotificationDraft>? notifications = null)
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
                    projectId,
                    actor.UserId,
                    eventType,
                    resourceType,
                    resourceId.ToString(),
                    clock.UtcNow,
                    auditData,
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, projectId, $"ActionControl.{eventType}", 1,
                    clock.UtcNow, responseJson, httpContext.TraceIdentifier),
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
        if (notificationWriter is not null && notifications is { Count: > 0 })
        {
            await notificationWriter.WriteAsync(
                dbContext.Database.GetDbConnection(),
                transaction.GetDbTransaction(),
                notifications,
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static Task<ManagementAction?> FindActionAsync(
        ActionControlDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid actionId,
        CancellationToken cancellationToken) =>
        dbContext.Actions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == actionId,
            cancellationToken);

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

    private static Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(actor.TenantId, actor.UserId, projectId, permission, cancellationToken);

    private static DateOnly ResolveLocalDate(DateTimeOffset now, string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new DomainRuleException("project.time_zone.unavailable", exception.Message);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new DomainRuleException("project.time_zone.invalid", exception.Message);
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
