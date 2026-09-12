using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.QualitySafety.Domain;
using Pmcs.Modules.QualitySafety.Persistence;

namespace Pmcs.Modules.QualitySafety.Endpoints;

internal static class QualitySafetyEndpointSupport
{
    internal static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    internal static async Task<bool> Allowed(IProjectPermissionService permissions, ICurrentActor actor,
        Guid projectId, string permission, CancellationToken token) => actor.IsAuthenticated &&
        await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId, projectId, permission, token);

    internal static async Task<(QualitySafetyConfiguration? Configuration, IResult? Error)> RequireAreaAsync(
        Guid projectId, ControlArea area, ICurrentActor actor, IProjectDirectory projects,
        QualitySafetyDbContext db, bool requireReady, CancellationToken token)
    {
        var project = await projects.FindProfileAsync(actor.TenantId, projectId, token);
        if (project is null) return (null, Results.NotFound(new { code = "project.not_found" }));
        var feature = area == ControlArea.Quality ? project.Quality : project.Hse;
        if (feature == ProjectFeatureState.NotEnabled)
            return (null, Results.UnprocessableEntity(new { code = area == ControlArea.Quality ? "quality.not_enabled" : "hse.not_enabled" }));
        if (feature == ProjectFeatureState.Suspended)
            return (null, Results.UnprocessableEntity(new { code = area == ControlArea.Quality ? "quality.suspended" : "hse.suspended" }));
        var configuration = await db.Configurations.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId, token);
        if (configuration is null)
            return (null, Results.UnprocessableEntity(new { code = "quality_safety.setup_required" }));
        var enabled = area == ControlArea.Quality
            ? configuration.QualityMode != QualityOperatingMode.Disabled
            : configuration.HseMode != HseOperatingMode.Disabled;
        var ready = area == ControlArea.Quality ? configuration.QualityReady : configuration.HseReady;
        if (!enabled) return (configuration, Results.UnprocessableEntity(new { code = area == ControlArea.Quality ? "quality.not_enabled" : "hse.not_enabled" }));
        if (requireReady && !ready) return (configuration, Results.UnprocessableEntity(new { code = "quality_safety.setup_required" }));
        return (configuration, null);
    }

    internal static async Task<(Command? Command, IResult? Replay)> CommandAsync(
        HttpContext context, ICurrentActor actor, IIdempotencyStore idempotency, string operation,
        object request, CancellationToken token)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key)) return (null, Results.Problem(statusCode: 400,
            title: "Idempotency-Key is required.", extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await idempotency.FindAsync(actor.TenantId, key, operation, hash, token);
        return replay is null ? (new Command(key, operation, hash), null) :
            (null, Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    internal static async Task PersistAsync(QualitySafetyDbContext db, HttpContext context,
        ICurrentActor actor, Guid projectId, Guid entityId, string entityType, string eventName,
        object response, Command command, int statusCode, ITransactionalSideEffectWriter effects,
        IClock clock, CancellationToken token)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.SaveChangesAsync(token);
        await effects.WriteAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, projectId, actor.UserId, eventName, entityType,
                    entityId.ToString(), clock.UtcNow, new Dictionary<string, object?> { ["revision"] = EntityRevision(response) }, context.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, projectId, $"QualitySafety.{eventName}",
                    1, clock.UtcNow, responseJson, context.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, command.Key, command.Operation, command.Hash,
                    statusCode, responseJson, clock.UtcNow, clock.UtcNow.AddDays(7))), token);
        await transaction.CommitAsync(token);
    }

    internal static IResult Conflict(string code, long revision) => Results.Conflict(new { code, currentRevision = revision });
    internal static void EnsureRevision(long current, long supplied, string code)
    { if (current != supplied) throw new DomainRuleException(code, "The record changed after it was loaded."); }
    private static object? EntityRevision(object response) => response.GetType().GetProperty("Revision")?.GetValue(response);
    private static JsonSerializerOptions CreateSerializerOptions()
    { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); options.Converters.Add(new JsonStringEnumConverter()); return options; }
}

internal sealed record Command(string Key, string Operation, string Hash);
