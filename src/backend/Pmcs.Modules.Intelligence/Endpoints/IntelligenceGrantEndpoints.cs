using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Persistence;
using Pmcs.Modules.Intelligence.Services;

namespace Pmcs.Modules.Intelligence.Endpoints;

internal sealed record IssueIntelligenceGrantRequest(
    Guid ActorTenantId, Guid ActorId, Guid? ScopeTenantId, string Permission,
    DateTimeOffset ExpiresAt);

internal sealed record RevokeIntelligenceGrantRequest(long BaseRevision);

internal sealed record IntelligenceGrantResponse(
    Guid Id, Guid ActorTenantId, Guid ActorId, Guid? ScopeTenantId, string Permission,
    DateTimeOffset StartsAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt, long Revision)
{
    internal static IntelligenceGrantResponse From(IntelligenceAdministrationGrant grant) =>
        new(grant.Id, grant.ActorTenantId, grant.ActorId, grant.ScopeTenantId, grant.Permission,
            grant.StartsAt, grant.ExpiresAt, grant.RevokedAt, grant.Revision);
}

internal static class IntelligenceGrantEndpoints
{
    internal static void MapIntelligenceGrantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var grants = endpoints.MapGroup("/api/v1/intelligence/admin/grants")
            .WithTags("Intelligence administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
        grants.MapGet("", ListAsync);
        grants.MapPost("", IssueAsync);
        grants.MapPost("/{grantId:guid}/revoke", RevokeAsync);
    }

    private static async Task<IResult> ListAsync(
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IntelligenceDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var grants = await dbContext.AdministrationGrants.AsNoTracking()
            .OrderByDescending(item => item.StartsAt).Take(100).ToArrayAsync(cancellationToken);
        return Results.Ok(grants.Select(IntelligenceGrantResponse.From).ToArray());
    }

    private static async Task<IResult> IssueAsync(
        IssueIntelligenceGrantRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IProjectPermissionService identity, IntelligenceDbContext dbContext,
        IClock clock, ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var now = clock.UtcNow;
        if (!IntelligenceAdministrationPermissions.IsValidScope(request.Permission, request.ScopeTenantId) ||
            request.ActorTenantId == Guid.Empty || request.ActorId == Guid.Empty ||
            request.ExpiresAt <= now || request.ExpiresAt > now.AddDays(365) ||
            request.ScopeTenantId is not null && request.ScopeTenantId != request.ActorTenantId)
        {
            return Results.BadRequest(new { code = "ai.grant.invalid" });
        }

        if (!await identity.HasTenantPermissionAsync(
                request.ActorTenantId, request.ActorId, "member-profile.read-self", cancellationToken) ||
            request.ScopeTenantId is not null &&
            !await identity.HasTenantPermissionAsync(
                request.ActorTenantId, request.ActorId, "identity.users.manage", cancellationToken))
        {
            return Results.BadRequest(new { code = "ai.grant.target_ineligible" });
        }

        var replay = await ReplayAsync(context, actor, idempotencyStore, "intelligence.grants.issue",
            request, cancellationToken);
        if (replay.Result is not null) return replay.Result;

        if (await dbContext.AdministrationGrants.AsNoTracking().AnyAsync(item =>
            item.ActorTenantId == request.ActorTenantId && item.ActorId == request.ActorId &&
            item.ScopeTenantId == request.ScopeTenantId && item.Permission == request.Permission &&
            item.RevokedAt == null, cancellationToken))
        {
            return Results.Conflict(new { code = "ai.grant.already_active" });
        }

        var grant = IntelligenceAdministrationGrant.Issue(Guid.NewGuid(), request.ActorTenantId,
            request.ActorId, request.ScopeTenantId, request.Permission, actor.UserId, now, request.ExpiresAt);
        dbContext.AdministrationGrants.Add(grant);
        var response = IntelligenceGrantResponse.From(grant);
        var json = JsonSerializer.Serialize(response);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(dbContext.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, null, actor.UserId, "IntelligenceGrantIssued",
                    "IntelligenceAdministrationGrant", grant.Id.ToString(), now,
                    new Dictionary<string, object?>
                    {
                        ["targetTenantId"] = request.ActorTenantId,
                        ["targetActorId"] = request.ActorId,
                        ["scopeTenantId"] = request.ScopeTenantId,
                        ["permission"] = request.Permission,
                        ["expiresAt"] = request.ExpiresAt
                    }, context.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, null,
                    "Intelligence.AdministrationGrantIssued", 1, now, json, context.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, replay.Key, "intelligence.grants.issue",
                    replay.Hash, StatusCodes.Status201Created, json, now, now.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/intelligence/admin/grants/{grant.Id}", response);
    }

    private static async Task<IResult> RevokeAsync(
        Guid grantId, RevokeIntelligenceGrantRequest request, HttpContext context,
        ICurrentActor actor, IntelligenceAdministrationAccess access,
        IntelligenceDbContext dbContext, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await access.CanManageProvidersAsync(actor, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var replay = await ReplayAsync(context, actor, idempotencyStore,
            "intelligence.grants.revoke", new { grantId, request.BaseRevision }, cancellationToken);
        if (replay.Result is not null) return replay.Result;

        var grant = await dbContext.AdministrationGrants.SingleOrDefaultAsync(item => item.Id == grantId,
            cancellationToken);
        if (grant is null) return Results.NotFound(new { code = "ai.grant.not_found" });
        if (request.BaseRevision != grant.Revision || grant.RevokedAt is not null)
            return Results.Conflict(new { code = "ai.grant.revision_conflict" });

        var now = clock.UtcNow;
        grant.Revoke(now);
        var response = IntelligenceGrantResponse.From(grant);
        var json = JsonSerializer.Serialize(response);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(dbContext.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, null, actor.UserId, "IntelligenceGrantRevoked",
                    "IntelligenceAdministrationGrant", grant.Id.ToString(), now,
                    new Dictionary<string, object?> { ["permission"] = grant.Permission,
                        ["revision"] = grant.Revision }, context.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, null,
                    "Intelligence.AdministrationGrantRevoked", 1, now, json, context.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, replay.Key, "intelligence.grants.revoke",
                    replay.Hash, StatusCodes.Status200OK, json, now, now.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<(string Key, string Hash, IResult? Result)> ReplayAsync<T>(
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
                System.Text.Encoding.UTF8, receipt.StatusCode));
    }
}
