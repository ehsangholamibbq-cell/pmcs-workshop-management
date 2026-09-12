using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Persistence;
using Pmcs.Modules.Intelligence.Services;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Intelligence.Endpoints;

internal static class IntelligenceEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = AdvisoryJson.Options;

    public static void MapIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/projects/{projectId:guid}/insights", ListAsync).WithTags("Advisory Insights");
        endpoints.MapPost("/api/v1/projects/{projectId:guid}/insight-generation-requests", CreateRequestAsync)
            .WithTags("Advisory Insights")
            .RequireRateLimiting(ApiRateLimitPolicies.InsightGeneration);
        endpoints.MapGet("/api/v1/projects/{projectId:guid}/insight-generation-requests/{requestId:guid}", GetRequestAsync)
            .WithTags("Advisory Insights");
        endpoints.MapPost("/api/v1/projects/{projectId:guid}/insights/{insightId:guid}/accept", AcceptAsync)
            .WithTags("Advisory Insights");
        endpoints.MapPost("/api/v1/projects/{projectId:guid}/insights/{insightId:guid}/dismiss", DismissAsync)
            .WithTags("Advisory Insights");
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IAdvisoryModelClient modelClient,
        IProjectStateContextSource stateSource,
        IntelligenceDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "insights.view", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var canGenerate = await HasPermissionAsync(permissionService, actor, projectId, "insights.generate", cancellationToken);
        var canReview = await HasPermissionAsync(permissionService, actor, projectId, "insights.review", cancellationToken);
        var canReadFinance = await HasPermissionAsync(permissionService, actor, projectId, "financial-state.read", cancellationToken);
        var canReadCommercial = await HasPermissionAsync(permissionService, actor, projectId, "commercial-state.read", cancellationToken);
        var canReadActions = await HasPermissionAsync(permissionService, actor, projectId, "actions.read", cancellationToken);

        var currentSnapshot = await stateSource.GetLatestAsync(actor.TenantId, projectId, cancellationToken);
        var insights = await dbContext.AdvisoryInsights.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                (!item.IncludesFinancialData || canReadFinance) &&
                (!item.IncludesCommercialData || canReadCommercial) &&
                (!item.IncludesActionData || canReadActions))
            .OrderByDescending(item => item.GeneratedAt)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        var recentRequests = await dbContext.GenerationRequests.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                (canReview || item.RequestedBy == actor.UserId))
            .OrderByDescending(item => item.RequestedAt)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        return Results.Ok(new AdvisoryInsightListResponse(
            modelClient.IsConfigured,
            canGenerate,
            canReview,
            insights.Select(item => AdvisoryInsightResponse.From(
                item,
                item.ExpiresAt <= clock.UtcNow || currentSnapshot is null || currentSnapshot.SnapshotId != item.SnapshotId)).ToArray(),
            recentRequests
                .Where(item => item.Status is InsightGenerationStatus.Pending or InsightGenerationStatus.Processing)
                .Select(InsightGenerationRequestResponse.From)
                .ToArray(),
            recentRequests.Select(InsightGenerationRequestResponse.From).ToArray()));
    }

    private static async Task<IResult> CreateRequestAsync(
        Guid projectId,
        CreateInsightGenerationRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        IProjectStateContextSource stateSource,
        IAdvisoryModelClient modelClient,
        IntelligenceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "insights.generate", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!modelClient.IsConfigured)
        {
            return Results.Json(new { code = "ai.provider.not_configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var snapshot = await stateSource.GetLatestAsync(actor.TenantId, projectId, cancellationToken);
        if (snapshot is null)
        {
            return Results.Conflict(new { code = "ai.context.no_snapshot" });
        }
        if (snapshot.ProjectConfigurationRevision != project.Revision)
        {
            return Results.Conflict(new { code = "ai.context.stale_snapshot" });
        }

        var idempotency = await GetIdempotencyAsync(
            httpContext, actor, idempotencyStore, "insights.generate", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var hasActiveRequest = await dbContext.GenerationRequests.AnyAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId && item.RequestedBy == actor.UserId &&
            (item.Status == InsightGenerationStatus.Pending || item.Status == InsightGenerationStatus.Processing),
            cancellationToken);
        if (hasActiveRequest)
        {
            return Results.Conflict(new { code = "ai.request.already_active" });
        }

        var generationRequest = InsightGenerationRequest.Create(
            Guid.NewGuid(), actor.TenantId, projectId, actor.UserId, clock.UtcNow);
        dbContext.GenerationRequests.Add(generationRequest);
        var response = InsightGenerationRequestResponse.From(generationRequest);
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
                    "AdvisoryInsightGenerationRequested",
                    "InsightGenerationRequest",
                    generationRequest.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?> { ["snapshotId"] = snapshot.SnapshotId },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    "Intelligence.AdvisoryInsightGenerationRequested",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    "insights.generate",
                    idempotency.RequestHash,
                    StatusCodes.Status202Accepted,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Accepted(
            $"/api/v1/projects/{projectId}/insight-generation-requests/{generationRequest.Id}",
            response);
    }

    private static async Task<IResult> GetRequestAsync(
        Guid projectId,
        Guid requestId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IntelligenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "insights.view", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var canReview = await HasPermissionAsync(permissionService, actor, projectId, "insights.review", cancellationToken);
        var request = await dbContext.GenerationRequests.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == requestId && item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            (canReview || item.RequestedBy == actor.UserId), cancellationToken);
        return request is null
            ? Results.NotFound(new { code = "ai.request.not_found" })
            : Results.Ok(InsightGenerationRequestResponse.From(request));
    }

    private static Task<IResult> AcceptAsync(
        Guid projectId,
        Guid insightId,
        ReviewInsightRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IntelligenceDbContext dbContext,
        IProjectStateContextSource stateSource,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        ReviewAsync(projectId, insightId, AdvisoryReviewStatus.Accepted, request, httpContext, actor,
            permissionService, dbContext, stateSource, clock, sideEffectWriter, idempotencyStore, cancellationToken);

    private static Task<IResult> DismissAsync(
        Guid projectId,
        Guid insightId,
        ReviewInsightRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IntelligenceDbContext dbContext,
        IProjectStateContextSource stateSource,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        ReviewAsync(projectId, insightId, AdvisoryReviewStatus.Dismissed, request, httpContext, actor,
            permissionService, dbContext, stateSource, clock, sideEffectWriter, idempotencyStore, cancellationToken);

    private static async Task<IResult> ReviewAsync(
        Guid projectId,
        Guid insightId,
        AdvisoryReviewStatus decision,
        ReviewInsightRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IntelligenceDbContext dbContext,
        IProjectStateContextSource stateSource,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "insights.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var operation = decision == AdvisoryReviewStatus.Accepted ? "insights.accept" : "insights.dismiss";
        var idempotency = await GetIdempotencyAsync(
            httpContext, actor, idempotencyStore, operation, request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var insight = await dbContext.AdvisoryInsights.SingleOrDefaultAsync(item =>
            item.Id == insightId && item.TenantId == actor.TenantId && item.ProjectId == projectId,
            cancellationToken);
        if (insight is null)
        {
            return Results.NotFound(new { code = "insight.not_found" });
        }

        var currentSnapshot = await stateSource.GetLatestAsync(actor.TenantId, projectId, cancellationToken);
        if (currentSnapshot is null || currentSnapshot.SnapshotId != insight.SnapshotId)
        {
            return Results.Conflict(new { code = "insight.snapshot.stale" });
        }

        insight.Review(decision, actor.UserId, request.Comment, request.BaseRevision, clock.UtcNow);
        var response = AdvisoryInsightResponse.From(insight);
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
                    decision == AdvisoryReviewStatus.Accepted ? "AdvisoryInsightAccepted" : "AdvisoryInsightDismissed",
                    "AdvisoryInsight",
                    insight.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["decision"] = decision.ToString(),
                        ["snapshotId"] = insight.SnapshotId,
                        ["revision"] = insight.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    decision == AdvisoryReviewStatus.Accepted
                        ? "Intelligence.AdvisoryInsightAccepted"
                        : "Intelligence.AdvisoryInsightDismissed",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    operation,
                    idempotency.RequestHash,
                    StatusCodes.Status200OK,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<(string Key, string RequestHash, IResult? Result)> GetIdempotencyAsync<T>(
        HttpContext httpContext,
        ICurrentActor actor,
        IIdempotencyStore idempotencyStore,
        string operation,
        T request,
        CancellationToken cancellationToken)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (string.Empty, string.Empty, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }

        var requestHash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId, key, operation, requestHash, cancellationToken);
        return replay is null
            ? (key, requestHash, null)
            : (key, requestHash, Results.Content(
                replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, projectId, permission, cancellationToken);
}
