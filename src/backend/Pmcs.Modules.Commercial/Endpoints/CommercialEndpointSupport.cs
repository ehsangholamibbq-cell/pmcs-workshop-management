using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Commercial.Services;

namespace Pmcs.Modules.Commercial.Endpoints;

internal static class CommercialEndpointSupport
{
    public static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static async Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            permission,
            cancellationToken);

    public static async Task<(CommandIdentity? Command, IResult? Replay)> GetCommandAsync(
        HttpContext httpContext,
        ICurrentActor actor,
        IIdempotencyStore idempotencyStore,
        string operation,
        object request,
        CancellationToken cancellationToken)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }

        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await idempotencyStore.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return replay is null
            ? (new CommandIdentity(key, operation, hash), null)
            : (null, Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    public static async Task PersistAsync(
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid projectId,
        Guid entityId,
        string entityType,
        string eventName,
        IReadOnlyDictionary<string, object?> auditData,
        object response,
        CommandIdentity command,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        var existingTransaction = dbContext.Database.CurrentTransaction;
        if (existingTransaction is not null)
        {
            await PersistCoreAsync(
                dbContext, stateFactory, httpContext, actor, projectId, entityId, entityType,
                eventName, auditData, responseJson, command, statusCode, sideEffectWriter,
                clock, existingTransaction, cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await PersistCoreAsync(
            dbContext, stateFactory, httpContext, actor, projectId, entityId, entityType,
            eventName, auditData, responseJson, command, statusCode, sideEffectWriter,
            clock, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task PersistCoreAsync(
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid projectId,
        Guid entityId,
        string entityType,
        string eventName,
        IReadOnlyDictionary<string, object?> auditData,
        string responseJson,
        CommandIdentity command,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        var snapshot = await stateFactory.CreateSnapshotAsync(actor.TenantId, projectId, cancellationToken);
        if (snapshot is not null)
        {
            dbContext.CommercialStateSnapshots.Add(snapshot);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    projectId,
                    actor.UserId,
                    eventName,
                    entityType,
                    entityId.ToString(),
                    clock.UtcNow,
                    auditData,
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    $"Commercial.{eventName}",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    command.Key,
                    command.Operation,
                    command.RequestHash,
                    statusCode,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
    }

    public static IResult RevisionConflict(string code, long currentRevision) =>
        Results.Conflict(new { code, currentRevision });

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal sealed record CommandIdentity(string Key, string Operation, string RequestHash);
