using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.WorkManagement.Contracts;
using Pmcs.Modules.WorkManagement.Domain;
using Pmcs.Modules.WorkManagement.Persistence;

namespace Pmcs.Modules.WorkManagement.Endpoints;

internal static class WorkManagementEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapWorkManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var project = endpoints.MapGroup("/api/v1/projects/{projectId:guid}").WithTags("My Work and Notifications");
        project.MapGet("/my-work", MyWorkAsync);
        project.MapGet("/notifications", ListNotificationsAsync);
        project.MapPost("/notifications/{notificationId:guid}/read", MarkReadAsync);
        project.MapPost("/notifications/{notificationId:guid}/acknowledge", AcknowledgeAsync);
    }

    private static async Task<IResult> MyWorkAsync(
        Guid projectId,
        ICurrentActor actor,
        IWorkManagementQueryService queryService,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var result = await queryService.GetMyWorkAsync(
            actor.TenantId, actor.UserId, projectId, cancellationToken);
        return result.Status switch
        {
            WorkManagementQueryStatus.Success => Results.Ok(result.Value),
            WorkManagementQueryStatus.ProjectNotFound => Results.NotFound(new { code = "project.not_found" }),
            _ => Results.StatusCode(StatusCodes.Status403Forbidden)
        };
    }

    private static async Task<IResult> ListNotificationsAsync(
        Guid projectId,
        bool? unreadOnly,
        ICurrentActor actor,
        IWorkManagementQueryService queryService,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var result = await queryService.ListNotificationsAsync(
            actor.TenantId, actor.UserId, projectId, unreadOnly == true, cancellationToken);
        return result.Status == WorkManagementQueryStatus.Success
            ? Results.Ok(result.Value)
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static Task<IResult> MarkReadAsync(
        Guid projectId,
        Guid notificationId,
        NotificationReceiptRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        WorkManagementDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        ChangeReceiptAsync(
            projectId,
            notificationId,
            request,
            httpContext,
            actor,
            permissions,
            dbContext,
            clock,
            sideEffects,
            idempotencyStore,
            false,
            cancellationToken);

    private static Task<IResult> AcknowledgeAsync(
        Guid projectId,
        Guid notificationId,
        NotificationReceiptRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        WorkManagementDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        ChangeReceiptAsync(
            projectId,
            notificationId,
            request,
            httpContext,
            actor,
            permissions,
            dbContext,
            clock,
            sideEffects,
            idempotencyStore,
            true,
            cancellationToken);

    private static async Task<IResult> ChangeReceiptAsync(
        Guid projectId,
        Guid notificationId,
        NotificationReceiptRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        WorkManagementDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        bool acknowledge,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissions, actor, projectId, "projects.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var operation = acknowledge ? "notifications.acknowledge" : "notifications.read";
        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, operation, request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var notification = await dbContext.Notifications.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.RecipientUserId == actor.UserId && item.Id == notificationId,
            cancellationToken);
        if (notification is null)
        {
            return Results.NotFound();
        }

        if (notification.Revision != request.BaseRevision)
        {
            return Results.Conflict(new
            {
                code = "notification.revision.conflict",
                currentRevision = notification.Revision
            });
        }

        if (acknowledge)
        {
            notification.Acknowledge(request.BaseRevision, actor.UserId, clock.UtcNow);
        }
        else
        {
            notification.MarkRead(request.BaseRevision, actor.UserId, clock.UtcNow);
        }

        var response = Map(notification);
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    projectId,
                    actor.UserId,
                    acknowledge ? "NotificationAcknowledged" : "NotificationRead",
                    "InAppNotification",
                    notification.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["recipientUserId"] = actor.UserId,
                        ["readAt"] = notification.ReadAt,
                        ["acknowledgedAt"] = notification.AcknowledgedAt,
                        ["revision"] = notification.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    acknowledge ? "WorkManagement.NotificationAcknowledged" : "WorkManagement.NotificationRead",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    idempotency.Operation,
                    idempotency.Hash,
                    StatusCodes.Status200OK,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static InAppNotificationResponse Map(InAppNotification notification) => new(
        notification.Id,
        notification.ProjectId,
        notification.Category,
        notification.Title,
        notification.Body,
        notification.TargetType,
        notification.TargetId,
        notification.OccurredAt,
        notification.ReadAt,
        notification.AcknowledgedAt,
        notification.Revision);

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
        var result = replay is null
            ? null
            : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        return (key, hash, operation, result);
    }

    private static Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            permission,
            cancellationToken);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
