using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Intelligence.Endpoints;

internal static class IntelligenceAdminMutationSupport
{
    internal static async Task<(string Key, string Hash, IResult? Result)> CheckAsync<T>(
        HttpContext context, ICurrentActor actor, IIdempotencyStore store,
        string operation, T request, CancellationToken cancellationToken)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 160)
            return (string.Empty, string.Empty, Results.BadRequest(new { code = "idempotency.key.required" }));

        var hash = RequestHash.Create(JsonSerializer.Serialize(request));
        var receipt = await store.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return receipt is null
            ? (key, hash, null)
            : (key, hash, Results.Content(receipt.ResponseBody, "application/json",
                Encoding.UTF8, receipt.StatusCode));
    }

    internal static async Task CommitAsync<T>(
        DbContext dbContext, ITransactionalSideEffectWriter writer,
        HttpContext context, ICurrentActor actor, IClock clock,
        string operation, string eventName, string resourceType, string resourceId,
        IReadOnlyDictionary<string, object?> metadata, T response,
        string key, string hash, int statusCode, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var json = JsonSerializer.Serialize(response);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await writer.WriteAsync(dbContext.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, null, actor.UserId, eventName,
                    resourceType, resourceId, now, metadata, context.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, null,
                    $"Intelligence.{eventName}", 1, now, json, context.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, key, operation, hash, statusCode,
                    json, now, now.AddDays(7))), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
