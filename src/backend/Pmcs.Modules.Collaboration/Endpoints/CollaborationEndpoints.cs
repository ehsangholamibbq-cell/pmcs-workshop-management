using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
    private const string SendOperation = "collaboration.message.send";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapCollaborationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/collaboration")
            .WithTags("Project Collaboration");
        group.MapGet("", GetRoomAsync);
        group.MapGet("/messages", ListMessagesAsync);
        group.MapPost("/messages", SendMessageAsync);
        group.MapGet("/messages/search", SearchMessagesAsync);
        group.MapGet("/events", PollEventsAsync);
        group.MapGet("/live", StreamEventsAsync);
        group.MapGet("/unread", GetUnreadAsync);
        group.MapPut("/read-cursor", AdvanceReadCursorAsync);
        group.MapPut("/messages/{messageId:guid}/reactions/{emoji}", AddReactionAsync);
        group.MapDelete("/messages/{messageId:guid}/reactions/{emoji}", RemoveReactionAsync);
        group.MapPut("/messages/{messageId:guid}/pin", PinMessageAsync);
        group.MapDelete("/messages/{messageId:guid}/pin", UnpinMessageAsync);
        group.MapPut("/messages/{messageId:guid}/attachments/{documentId:guid}", AttachDocumentAsync);
        group.MapGet("/messages/{messageId:guid}/attachments", ListAttachmentsAsync);
        group.MapGet("/messages/{messageId:guid}/attachments/{documentId:guid}/content", DownloadAttachmentAsync);
    }

    private static async Task<IResult> GetRoomAsync(
        Guid projectId, CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;

        var last = await db.Rooms.AsNoTracking()
            .Where(room => room.TenantId == actor.TenantId && room.ProjectId == projectId)
            .Select(room => (long?)room.LastSequence)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        return Results.Ok(new ProjectRoomResponse(projectId, projectId, last));
    }

    private static async Task<IResult> ListMessagesAsync(
        Guid projectId, long? after, CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (after is < 0) return Results.BadRequest(new { code = "collaboration.cursor.invalid" });

        var rows = await db.Messages.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                item.Sequence > (after ?? 0))
            .OrderBy(item => item.Sequence)
            .ThenBy(item => item.Id)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return Results.Ok(new MessagePageResponse(rows.Select(ProjectMessageResponse.From).ToArray(),
            rows.Length == 0 ? after ?? 0 : rows[^1].Sequence));
    }

    private static async Task<IResult> SendMessageAsync(
        Guid projectId, SendProjectMessageRequest request, HttpContext http,
        CollaborationRuntimeOptions runtime, ICurrentActor actor,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db, IClock clock,
        IIdempotencyStore idempotency, ITransactionalSideEffectWriter sideEffects,
        ITransactionalNotificationWriter notificationWriter,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.send", runtime,
            actor, membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;

        var key = http.Request.Headers["Idempotency-Key"].ToString().Trim();
        IdempotencyKeyRules.Validate(key);
        if (request.ClientMessageId == Guid.Empty)
            return Results.BadRequest(new { code = "collaboration.client_message_id.required" });

        var body = ProjectMessage.NormalizeBody(request.Body);
        var mentions = ProjectMessage.NormalizeMentions(request.MentionedUserIds, actor.UserId);
        var payloadHash = ProjectMessage.HashRequest(body, request.ReplyToMessageId, mentions);
        var requestHash = RequestHash.Create(JsonSerializer.Serialize(new
        {
            actor.TenantId, projectId, actor.UserId, request.ClientMessageId, payloadHash
        }, JsonOptions));
        var operation = $"{SendOperation}:{projectId:N}:{actor.UserId:N}";
        var replay = await idempotency.FindAsync(actor.TenantId, key, operation,
            requestHash, cancellationToken);
        if (replay is not null)
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);

        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into collaboration.project_rooms(project_id, tenant_id, last_sequence, created_at)
            values ({projectId}, {actor.TenantId}, 0, {now})
            on conflict (project_id) do nothing
            """, cancellationToken);
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            update collaboration.project_rooms set last_sequence = last_sequence + 1
            where project_id = {projectId} and tenant_id = {actor.TenantId}
            """, cancellationToken);
        if (updated != 1)
            return Results.Conflict(new { code = "collaboration.room.scope_conflict" });

        // The room row lock serializes competing sends and retries before any sequence is committed.
        var duplicate = await db.Messages.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.AuthorUserId == actor.UserId && item.ClientMessageId == request.ClientMessageId,
            cancellationToken);
        if (duplicate is not null)
            return duplicate.RequestHash == payloadHash
                ? Results.Ok(ProjectMessageResponse.From(duplicate))
                : Results.Conflict(new { code = "collaboration.client_message_id.reused" });

        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, "collaboration.send", cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        ProjectMessage? reply = null;
        if (request.ReplyToMessageId.HasValue)
        {
            reply = await db.Messages.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == request.ReplyToMessageId.Value && item.TenantId == actor.TenantId &&
                item.ProjectId == projectId, cancellationToken);
            if (reply is null) return Results.NotFound(new { code = "collaboration.reply.not_found" });
        }
        foreach (var mentionedUserId in mentions)
        {
            if (!await membership.IsActiveAsync(actor.TenantId, projectId, mentionedUserId, cancellationToken))
                return Results.BadRequest(new { code = "collaboration.mention.not_active_member" });
        }

        var sequence = await db.Rooms.AsNoTracking()
            .Where(room => room.TenantId == actor.TenantId && room.ProjectId == projectId)
            .Select(room => room.LastSequence).SingleAsync(cancellationToken);
        var message = ProjectMessage.Create(Guid.NewGuid(), actor.TenantId, projectId,
            sequence, actor.UserId, request.ClientMessageId, body, now,
            request.ReplyToMessageId, mentions);
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        var response = ProjectMessageResponse.From(message);
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);
        await sideEffects.WriteAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, projectId, actor.UserId,
                    "ProjectMessageCreated", "ProjectMessage", message.Id.ToString(), now,
                    new Dictionary<string, object?>
                    {
                        ["sequence"] = sequence, ["requestHash"] = payloadHash,
                        ["replyToMessageId"] = request.ReplyToMessageId,
                        ["mentionCount"] = mentions.Length,
                        ["clientMessageId"] = request.ClientMessageId
                    }, http.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, projectId,
                    "collaboration.message.created", 1, now,
                    JsonSerializer.Serialize(new { message.Id, projectId, sequence,
                        message.AuthorUserId, message.CreatedAt, message.ReplyToMessageId,
                        mentionedUserIds = mentions }, JsonOptions),
                    http.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, key, operation, requestHash,
                    StatusCodes.Status201Created, responseJson, now, now.AddDays(7))),
            cancellationToken);
        var recipients = mentions.ToHashSet();
        if (reply is not null && reply.AuthorUserId != actor.UserId &&
            await membership.IsActiveAsync(actor.TenantId, projectId, reply.AuthorUserId, cancellationToken))
            recipients.Add(reply.AuthorUserId);
        var notifications = recipients.Select(recipient => new InAppNotificationDraft(
            Guid.NewGuid(), actor.TenantId, projectId, recipient,
            $"collaboration:{message.Id:N}:mention-reply:{recipient:N}",
            mentions.Contains(recipient) ? "CollaborationMention" : "CollaborationReply",
            "گفت‌وگوی پروژه", "پیام جدیدی در گفت‌وگوی پروژه برای شما ثبت شد.",
            "ProjectMessage", message.Id, now)).ToArray();
        if (notifications.Length > 0)
            await notificationWriter.WriteAsync(db.Database.GetDbConnection(),
                transaction.GetDbTransaction(), notifications, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/collaboration/messages/{message.Id}", response);
    }

    private static async Task<IResult?> GateAsync(
        Guid projectId, string permission, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IProjectCollaborationMembership membership,
        IProjectPermissionService permissions, IProjectDirectory projects,
        CancellationToken cancellationToken)
    {
        if (!runtime.Enabled) return Results.NotFound();
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await membership.IsActiveAsync(actor.TenantId, projectId, actor.UserId, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(actor.TenantId, actor.UserId,
                projectId, permission, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        return await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken)
            ? null : Results.NotFound();
    }
}

internal sealed record SendProjectMessageRequest(Guid ClientMessageId, string? Body,
    Guid? ReplyToMessageId = null, Guid[]? MentionedUserIds = null);
internal sealed record ProjectRoomResponse(Guid Id, Guid ProjectId, long LastSequence);
internal sealed record ProjectMessageResponse(Guid Id, Guid ProjectId, long Sequence,
    Guid AuthorUserId, Guid ClientMessageId, string Body, DateTimeOffset CreatedAt,
    Guid? ReplyToMessageId, IReadOnlyList<Guid> MentionedUserIds,
    DateTimeOffset? PinnedAt, Guid? PinnedBy)
{
    public static ProjectMessageResponse From(ProjectMessage message) => new(
        message.Id, message.ProjectId, message.Sequence, message.AuthorUserId,
        message.ClientMessageId, message.Body, message.CreatedAt,
        message.ReplyToMessageId, message.MentionedUserIds, message.PinnedAt, message.PinnedBy);
}
internal sealed record MessagePageResponse(IReadOnlyList<ProjectMessageResponse> Messages, long NextSequence);
