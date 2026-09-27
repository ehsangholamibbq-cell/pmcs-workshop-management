using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Collaboration.Domain;
using Pmcs.Modules.Collaboration.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Collaboration.Endpoints;

internal static partial class CollaborationEndpoints
{
    private static async Task<IResult> SearchMessagesAsync(
        Guid projectId, string? q, CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        var term = q?.Trim();
        if (term is null || term.Length is < 2 or > 120)
            return Results.BadRequest(new { code = "collaboration.search.query.invalid" });
        var pattern = "%" + term.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal) + "%";
        var matches = await db.Messages.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                EF.Functions.ILike(item.Body, pattern, "\\"))
            .OrderByDescending(item => item.Sequence).ThenBy(item => item.Id)
            .Take(50).ToArrayAsync(cancellationToken);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.Ok(matches.Select(ProjectMessageResponse.From).ToArray());
    }

    private static async Task<IResult> GetUnreadAsync(
        Guid projectId, CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        var cursor = await db.ReadCursors.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                item.UserId == actor.UserId)
            .Select(item => (long?)item.LastReadSequence)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        var count = await db.Messages.AsNoTracking().CountAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.Sequence > cursor && item.AuthorUserId != actor.UserId,
            cancellationToken);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return Results.Ok(new { lastReadSequence = cursor, unreadCount = count });
    }

    private static async Task<IResult> AdvanceReadCursorAsync(
        Guid projectId, AdvanceProjectReadCursorRequest request, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (request.LastReadSequence < 0)
            return Results.BadRequest(new { code = "collaboration.cursor.invalid" });
        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into collaboration.project_rooms(project_id, tenant_id, last_sequence, created_at)
            values ({projectId}, {actor.TenantId}, 0, {now})
            on conflict (project_id) do nothing
            """, cancellationToken);
        var maximum = await db.Rooms.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .Select(item => item.LastSequence).SingleAsync(cancellationToken);
        if (request.LastReadSequence > maximum)
            return Results.Conflict(new { code = "collaboration.cursor.future" });
        var previous = await db.ReadCursors.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                item.UserId == actor.UserId)
            .Select(item => (long?)item.LastReadSequence)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (request.LastReadSequence > previous)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                insert into collaboration.read_cursors(tenant_id, project_id, user_id,
                    last_read_sequence, updated_at)
                values ({actor.TenantId}, {projectId}, {actor.UserId},
                    {request.LastReadSequence}, {now})
                on conflict (tenant_id, project_id, user_id) do update
                set last_read_sequence = greatest(read_cursors.last_read_sequence,
                    excluded.last_read_sequence), updated_at = excluded.updated_at
                """, cancellationToken);
            await sideEffects.WriteAuditAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
                new AuditEntry(actor.TenantId, projectId, actor.UserId, "ProjectReadCursorAdvanced",
                    "ProjectRoom", projectId.ToString(), now,
                    new Dictionary<string, object?> { ["from"] = previous,
                        ["to"] = request.LastReadSequence }, http.TraceIdentifier), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { lastReadSequence = Math.Max(previous, request.LastReadSequence) });
    }

    private static async Task<IResult> AddReactionAsync(
        Guid projectId, Guid messageId, string emoji, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.send", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        ProjectReaction.ValidateEmoji(emoji);
        if (!await MessageExistsAsync(db, actor, projectId, messageId, cancellationToken))
            return Results.NotFound();
        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var reaction = ProjectReaction.Create(Guid.NewGuid(), actor.TenantId, projectId,
            messageId, actor.UserId, emoji, now);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into collaboration.reactions(id, tenant_id, project_id, message_id,
                actor_user_id, emoji, created_at)
            values ({reaction.Id}, {actor.TenantId}, {projectId}, {messageId},
                {actor.UserId}, {emoji}, {now})
            on conflict (tenant_id, project_id, message_id, actor_user_id, emoji) do nothing
            """, cancellationToken);
        if (inserted == 1)
            await WriteInteractionEventAsync(db, transaction, sideEffects, http, actor,
                projectId, messageId, "ProjectMessageReactionAdded", now,
                new Dictionary<string, object?> { ["emoji"] = emoji }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { messageId, emoji, reacted = true });
    }

    private static async Task<IResult> RemoveReactionAsync(
        Guid projectId, Guid messageId, string emoji, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.send", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        ProjectReaction.ValidateEmoji(emoji);
        if (!await MessageExistsAsync(db, actor, projectId, messageId, cancellationToken))
            return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var deleted = await db.Reactions.Where(item => item.TenantId == actor.TenantId &&
            item.ProjectId == projectId && item.MessageId == messageId &&
            item.ActorUserId == actor.UserId && item.Emoji == emoji)
            .ExecuteDeleteAsync(cancellationToken);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (deleted > 0)
            await WriteInteractionEventAsync(db, transaction, sideEffects, http, actor,
                projectId, messageId, "ProjectMessageReactionRemoved", clock.UtcNow,
                new Dictionary<string, object?> { ["emoji"] = emoji }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static Task<IResult> PinMessageAsync(Guid projectId, Guid messageId, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken) =>
        SetPinAsync(projectId, messageId, true, http, runtime, actor, membership,
            permissions, projects, db, clock, sideEffects, cancellationToken);

    private static Task<IResult> UnpinMessageAsync(Guid projectId, Guid messageId, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken) =>
        SetPinAsync(projectId, messageId, false, http, runtime, actor, membership,
            permissions, projects, db, clock, sideEffects, cancellationToken);

    private static async Task<IResult> SetPinAsync(Guid projectId, Guid messageId, bool pin,
        HttpContext http, CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.moderate", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.Messages.SingleOrDefaultAsync(item => item.Id == messageId &&
            item.TenantId == actor.TenantId && item.ProjectId == projectId, cancellationToken);
        if (message is null) return Results.NotFound();
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if ((message.PinnedAt is not null) == pin)
            return Results.Ok(ProjectMessageResponse.From(message));
        var now = clock.UtcNow;
        if (pin) message.SetPin(actor.UserId, now);
        else message.ClearPin();
        await db.SaveChangesAsync(cancellationToken);
        await WriteInteractionEventAsync(db, transaction, sideEffects, http, actor,
            projectId, messageId, pin ? "ProjectMessagePinned" : "ProjectMessageUnpinned", now,
            new Dictionary<string, object?> { ["pinned"] = pin }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(ProjectMessageResponse.From(message));
    }

    private static Task<bool> MessageExistsAsync(CollaborationDbContext db, ICurrentActor actor,
        Guid projectId, Guid messageId, CancellationToken cancellationToken) =>
        db.Messages.AsNoTracking().AnyAsync(item => item.Id == messageId &&
            item.TenantId == actor.TenantId && item.ProjectId == projectId, cancellationToken);

    private static Task WriteInteractionEventAsync(CollaborationDbContext db,
        IDbContextTransaction transaction, ITransactionalSideEffectWriter sideEffects,
        HttpContext http, ICurrentActor actor, Guid projectId, Guid messageId,
        string eventType, DateTimeOffset now, IReadOnlyDictionary<string, object?> data,
        CancellationToken cancellationToken) =>
        sideEffects.WriteEventAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalEventBatch(
                new AuditEntry(actor.TenantId, projectId, actor.UserId, eventType,
                    "ProjectMessage", messageId.ToString(), now, data, http.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, projectId,
                    $"collaboration.{eventType}", 1, now,
                    JsonSerializer.Serialize(new { messageId, projectId, eventType }, JsonOptions),
                    http.TraceIdentifier)), cancellationToken);
}

internal sealed record AdvanceProjectReadCursorRequest(long LastReadSequence);
