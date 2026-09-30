using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Persistence;
using Pmcs.Modules.Intelligence.Services;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Intelligence.Endpoints;

internal sealed record StartReferenceRunRequest(Guid RequestId, string? Question);

internal sealed record ReferenceRunMetadata(Guid Id, Guid ProjectId,
    IntelligenceRunStatus Status, Guid ProfileVersionId, int ProfileVersion,
    Guid ModelCatalogId, int ModelVersion, string Provider, string Model,
    string PromptVersion, string PolicyVersion, string? ToolId, string? ToolDecision,
    bool Fallback, string? FallbackReason, int InputTokens, int OutputTokens,
    long CostMicrounits, string? ErrorCode, DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt)
{
    internal static ReferenceRunMetadata From(IntelligenceReferenceRun run) => new(
        run.Id, run.ProjectId, run.Status, run.ProfileVersionId, run.ProfileVersion,
        run.ModelCatalogId, run.ModelVersion, run.Provider, run.Model,
        run.PromptVersion, run.PolicyVersion, run.ToolId, run.ToolDecision,
        run.Fallback, run.FallbackReason, run.InputTokens, run.OutputTokens,
        run.CostMicrounits, run.ErrorCode, run.RequestedAt, run.CompletedAt);
}

internal static class IntelligenceReferenceRunEndpoints
{
    internal static void MapIntelligenceReferenceRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/intelligence/reference-runs")
            .WithTags("INT1 Reference Gateway");
        group.MapPost("", StartAsync).RequireRateLimiting(ApiRateLimitPolicies.InsightGeneration);
        group.MapGet("/{runId:guid}", GetAsync);
    }

    private static async Task<IResult> GetAsync(Guid projectId, Guid runId,
        IConfiguration configuration, ICurrentActor actor,
        IProjectPermissionService permissions, IntelligenceDbContext db,
        CancellationToken cancellationToken)
    {
        if (!Enabled(configuration)) return Results.NotFound();
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await permissions.HasProjectPermissionAsync(actor.TenantId,
                actor.UserId, projectId, "insights.generate", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var run = await db.ReferenceRuns.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == runId && item.TenantId == actor.TenantId &&
            item.ProjectId == projectId && item.RequestedBy == actor.UserId,
            cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(ReferenceRunMetadata.From(run));
    }

    private static async Task<IResult> StartAsync(Guid projectId,
        StartReferenceRunRequest request, HttpContext http,
        IConfiguration configuration, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects,
        IntelligenceDbContext db, IntelligenceToolRegistry registry,
        IEnumerable<IReferenceModelAdapter> adapters,
        ITransactionalSideEffectWriter sideEffects, IClock clock,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!Enabled(configuration)) return Results.NotFound();
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await permissions.HasProjectPermissionAsync(actor.TenantId,
                actor.UserId, projectId, "insights.generate", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound();
        var question = request.Question?.Trim();
        if (request.RequestId == Guid.Empty || question is not { Length: > 0 and <= 1_000 } ||
            question.Any(char.IsControl))
            return Results.BadRequest(new { code = "ai.run.request_invalid" });
        var operation = $"intelligence.reference.run:{projectId:N}:{actor.UserId:N}";
        var replay = await IntelligenceAdminMutationSupport.CheckAsync(http, actor,
            idempotencyStore, operation, request, cancellationToken);
        if (replay.Result is not null) return replay.Result;
        if (await db.ReferenceRuns.AsNoTracking().AnyAsync(item => item.Id == request.RequestId,
                cancellationToken))
            return Results.Conflict(new { code = "ai.run.request_exists" });

        var selection = await db.ProfileSelections.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UseCase == "int1.reference", cancellationToken);
        if (selection is null) return Results.Conflict(new { code = "ai.profile.selection_missing" });
        var version = await db.ProfileVersions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == selection.ProfileVersionId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (version is null) return Results.Conflict(new { code = "ai.profile.selection_invalid" });
        var profile = version.ToPolicy();
        var allowedIds = profile.AllowedModelIds.ToArray();
        var models = await db.ModelCatalog.AsNoTracking()
            .Where(item => allowedIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        var catalog = models.Select(item => item.ToPolicy()).ToArray();
        var estimatedUnits = profile.MaximumInputTokens + profile.MaximumOutputTokens;
        var decision = ModelSelectionPolicy.Select(profile, catalog, actor.TenantId, projectId,
            IntelligenceDataClass.Confidential, estimatedUnits, selection.ModelId);
        if (!decision.Allowed || decision.Model is null)
            return Results.Conflict(new { code = decision.Code });
        if (profile.MaximumOutputTokens < 64)
            return Results.Conflict(new { code = "ai.profile.limit_exceeded" });
        var available = adapters.SingleOrDefault(item => item.Provider == decision.Model.Provider &&
            item.ConfiguredModel == decision.Model.Model && item.IsConfigured);
        if (available is null)
            return Results.Json(new { code = "ai.provider.not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var run = IntelligenceReferenceRun.Request(request.RequestId, actor.TenantId,
            projectId, actor.UserId, profile, decision.Model,
            RequestHash.Create(JsonSerializer.Serialize(new { projectId, question })), clock.UtcNow);
        db.ReferenceRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        run.Validate(clock.UtcNow);
        run.Start(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        string? answer = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
        try
        {
            var adapter = available;
            var toolSet = registry.RegisteredTools();
            if (toolSet.Count == 0) throw new ReferenceGatewayException("ai.tool.registry_unavailable");
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var toolDecision = await adapter.DecideToolAsync(question, toolSet, timeout.Token);
                    var invoked = await registry.InvokeAsync(actor.TenantId, actor.UserId,
                        projectId, toolDecision.ToolId, toolDecision.Arguments, timeout.Token);
                    if (!invoked.Allowed)
                    {
                        if (run.ToolId is null) run.RecordTool(toolDecision.ToolId, "Denied");
                        throw new ReferenceGatewayException(invoked.Code);
                    }
                    if (run.ToolId is null) run.RecordTool(toolDecision.ToolId, "Allowed");
                    else if (run.ToolId != toolDecision.ToolId || run.ToolDecision != "Allowed")
                        throw new ReferenceGatewayException("ai.tool.fallback_changed_tool");
                    await db.SaveChangesAsync(timeout.Token);
                    var toolJson = JsonSerializer.Serialize(invoked.Data);
                    var reply = await adapter.AnswerAsync(question, toolDecision.ToolId,
                        toolJson, profile.MaximumOutputTokens, timeout.Token);
                    var inputTokens = toolDecision.InputTokens + reply.InputTokens;
                    var outputTokens = toolDecision.OutputTokens + reply.OutputTokens;
                    if (inputTokens < 0 || outputTokens < 0 ||
                        inputTokens > profile.MaximumInputTokens ||
                        outputTokens > profile.MaximumOutputTokens)
                        throw new ReferenceGatewayException("ai.profile.limit_exceeded");
                    // No catalog price is published yet. Zero means unknown billed cost, never a quoted price.
                    run.Complete(inputTokens, outputTokens, 0, clock.UtcNow);
                    answer = reply.Answer;
                    break;
                }
                catch (ReferenceGatewayException error) when (attempt == 0 &&
                    profile.AllowFallback && error.Code is ("ai.provider.timeout" or
                        "ai.provider.unavailable" or "ai.provider.invalid_response"))
                {
                    var alternate = profile.FallbackModelIds
                        .Select(id => ModelSelectionPolicy.Select(profile, catalog, actor.TenantId,
                            projectId, IntelligenceDataClass.Confidential, estimatedUnits,
                            id, fallback: true, failureCode: error.Code))
                        .FirstOrDefault(item => item.Allowed && item.Model is not null);
                    if (alternate?.Model is null) throw;
                    var nextAdapter = adapters.SingleOrDefault(item =>
                        item.Provider == alternate.Model.Provider &&
                        item.ConfiguredModel == alternate.Model.Model && item.IsConfigured);
                    if (nextAdapter is null) throw;
                    run.SelectFallback(alternate.Model, error.Code);
                    await db.SaveChangesAsync(timeout.Token);
                    adapter = nextAdapter;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Cancel(clock.UtcNow);
        }
        catch (OperationCanceledException)
        {
            run.Fail("ai.provider.timeout", clock.UtcNow);
        }
        catch (ReferenceGatewayException error)
        {
            run.Fail(error.Code, clock.UtcNow);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or
            KeyNotFoundException)
        {
            run.Fail("ai.provider.invalid_response", clock.UtcNow);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        var metadata = ReferenceRunMetadata.From(run);
        var responseBody = JsonSerializer.Serialize(new { run = metadata, code = run.ErrorCode });
        var now = clock.UtcNow;
        await sideEffects.WriteAsync(db.Database.GetDbConnection(),
            transaction.GetDbTransaction(), new TransactionalSideEffectBatch(
            new AuditEntry(actor.TenantId, projectId, actor.UserId,
                "IntelligenceReferenceRunFinished", "IntelligenceReferenceRun",
                run.Id.ToString(), now,
                new Dictionary<string, object?>
                {
                    ["profileVersionId"] = run.ProfileVersionId,
                    ["modelCatalogId"] = run.ModelCatalogId,
                    ["status"] = run.Status.ToString(),
                    ["toolId"] = run.ToolId, ["toolDecision"] = run.ToolDecision,
                    ["fallbackReason"] = run.FallbackReason,
                    ["inputTokens"] = run.InputTokens, ["outputTokens"] = run.OutputTokens,
                    ["errorCode"] = run.ErrorCode
                }, http.TraceIdentifier),
            new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, projectId,
                "intelligence.reference-run.finished", 1, now,
                responseBody, http.TraceIdentifier),
            new IdempotencyReceipt(actor.TenantId, replay.Key, operation,
                replay.Hash, run.Status == IntelligenceRunStatus.Completed
                    ? StatusCodes.Status200OK : StatusCodes.Status502BadGateway,
                responseBody, now, now.AddDays(7))), CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        return run.Status == IntelligenceRunStatus.Completed
            ? Results.Ok(new { run = metadata, answer })
            : Results.Json(new { run = metadata, code = run.ErrorCode },
                statusCode: StatusCodes.Status502BadGateway);
    }

    private static bool Enabled(IConfiguration configuration) =>
        bool.TryParse(configuration["Intelligence:INT1ReferenceEnabled"], out var enabled) && enabled;
}
