using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Endpoints;

internal static class TechnicalOfficeEndpointSupport
{
    internal static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static async Task<(TechnicalCommand? Command, IResult? Replay)> CommandAsync<TRequest>(
        HttpContext context,
        ICurrentActor actor,
        IIdempotencyStore idempotencyStore,
        string operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
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
            ? (new TechnicalCommand(key, hash, operation), null)
            : (null, Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    public static async Task PersistAsync<TResponse>(
        TechnicalOfficeDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid projectId,
        string entityType,
        Guid entityId,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        TechnicalCommand command,
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
                    actor.TenantId, projectId, actor.UserId, eventType, entityType, entityId.ToString(),
                    clock.UtcNow, auditData, httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, projectId, $"TechnicalOffice.{eventType}", 1,
                    clock.UtcNow, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, command.Key, command.Operation, command.Hash, statusCode,
                    responseJson, clock.UtcNow, clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static IResult RevisionConflict(string code, long currentRevision) => Results.Conflict(new
    {
        code,
        currentRevision
    });

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal sealed record TechnicalCommand(string Key, string Hash, string Operation);
