using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Collaboration.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Collaboration.Endpoints;

internal static partial class CollaborationEndpoints
{
    private static async Task<IResult> PollEventsAsync(
        Guid projectId, long? after, int? waitSeconds, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IActorAccessValidator access,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        if (after is < 0 || waitSeconds is < 0 or > 20)
            return Results.BadRequest(new { code = "collaboration.events.cursor.invalid" });
        var deadline = DateTimeOffset.UtcNow.AddSeconds(waitSeconds ?? 0);
        do
        {
            var gate = await LiveGateAsync(projectId, runtime, actor, access, membership,
                permissions, projects, cancellationToken);
            if (gate is not null) return gate;
            var events = await ReadEventsAsync(db, actor.TenantId, projectId, after ?? 0,
                cancellationToken);
            if (events.Length > 0 || DateTimeOffset.UtcNow >= deadline)
            {
                // A membership revocation during the query must not release event identities.
                gate = await LiveGateAsync(projectId, runtime, actor, access, membership,
                    permissions, projects, cancellationToken);
                if (gate is not null) return gate;
                return Results.Ok(new CollaborationEventPage(events,
                    events.Length == 0 ? after ?? 0 : events[^1].Sequence));
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        } while (true);
    }

    private static async Task<IResult> StreamEventsAsync(
        Guid projectId, long? after, HttpContext http, CollaborationRuntimeOptions runtime,
        ICurrentActor actor, IActorAccessValidator access,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CollaborationDbContext db,
        CancellationToken cancellationToken)
    {
        if (after is < 0) return Results.BadRequest(new { code = "collaboration.events.cursor.invalid" });
        var gate = await LiveGateAsync(projectId, runtime, actor, access, membership,
            permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        if (!http.WebSockets.IsWebSocketRequest)
            return Results.BadRequest(new { code = "collaboration.websocket.required" });

        using var socket = await http.WebSockets.AcceptWebSocketAsync();
        var cursor = after ?? 0;
        // Each connection is bounded; reconnect resumes from the durable sequence.
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        try
        {
            while (socket.State == WebSocketState.Open &&
                   DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                gate = await LiveGateAsync(projectId, runtime, actor, access, membership,
                    permissions, projects, cancellationToken);
                if (gate is not null)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation,
                        "Project access changed", cancellationToken);
                    break;
                }
                var events = await ReadEventsAsync(db, actor.TenantId, projectId,
                    cursor, cancellationToken);
                // Recheck immediately before a payload is sent, including after a query delay.
                gate = await LiveGateAsync(projectId, runtime, actor, access, membership,
                    permissions, projects, cancellationToken);
                if (gate is not null)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation,
                        "Project access changed", cancellationToken);
                    break;
                }
                foreach (var item in events)
                {
                    await SendFrameAsync(socket, new { type = "message.created", item.MessageId,
                        item.ProjectId, item.Sequence, item.CreatedAt }, cancellationToken);
                    cursor = item.Sequence;
                }
                if (events.Length == 0)
                {
                    await SendFrameAsync(socket, new { type = "heartbeat", projectId,
                        lastSequence = cursor }, cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            if (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,
                    "Reconnect with last sequence", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (WebSocketException) { /* The peer disconnected; durable replay remains available. */ }
        return Results.Empty;
    }

    private static async Task SendFrameAsync(WebSocket socket, object value,
        CancellationToken cancellationToken) => await socket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions).AsMemory(),
            WebSocketMessageType.Text, true, cancellationToken);

    private static async Task<CollaborationEvent[]> ReadEventsAsync(CollaborationDbContext db,
        Guid tenantId, Guid projectId, long after, CancellationToken cancellationToken) =>
        await db.Messages.AsNoTracking()
            .Where(message => message.TenantId == tenantId &&
                message.ProjectId == projectId && message.Sequence > after)
            .OrderBy(message => message.Sequence).ThenBy(message => message.Id)
            .Select(message => new CollaborationEvent(message.Id, message.ProjectId,
                message.Sequence, message.CreatedAt))
            .Take(100).ToArrayAsync(cancellationToken);

    private static async Task<IResult?> LiveGateAsync(Guid projectId,
        CollaborationRuntimeOptions runtime, ICurrentActor actor, IActorAccessValidator access,
        IProjectCollaborationMembership membership, IProjectPermissionService permissions,
        IProjectDirectory projects, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(projectId, "collaboration.read", runtime, actor,
            membership, permissions, projects, cancellationToken);
        if (gate is not null) return gate;
        return actor.TokenIssuedAt.HasValue && await access.HasAccessAsync(
            actor.TenantId, actor.UserId, actor.TokenIssuedAt.Value, cancellationToken)
            ? null : Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}

internal sealed record CollaborationEvent(Guid MessageId, Guid ProjectId,
    long Sequence, DateTimeOffset CreatedAt);
internal sealed record CollaborationEventPage(IReadOnlyList<CollaborationEvent> Events,
    long NextSequence);
