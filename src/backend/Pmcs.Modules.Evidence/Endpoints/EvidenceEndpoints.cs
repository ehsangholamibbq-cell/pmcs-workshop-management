using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Evidence.Domain;
using Pmcs.Modules.Evidence.Persistence;
using Pmcs.Modules.Evidence.Storage;
using Pmcs.Modules.FieldOperations.Contracts;

namespace Pmcs.Modules.Evidence.Endpoints;

internal static class EvidenceEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/evidence").WithTags("Evidence");
        group.MapGet("/", ListAsync);
        group.MapGet("/{evidenceId:guid}", GetAsync);
        group.MapGet("/{evidenceId:guid}/content", DownloadAsync);
        group.MapPost("/upload-sessions", CreateUploadSessionAsync);
        group.MapPut("/{evidenceId:guid}/content", UploadContentAsync)
            .DisableAntiforgery();
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        Guid? dailyReportId,
        Guid? dailyFactId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        EvidenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "evidence.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = dbContext.EvidenceFiles.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (dailyReportId.HasValue)
        {
            query = query.Where(item => item.DailyReportId == dailyReportId.Value);
        }

        if (dailyFactId.HasValue)
        {
            query = query.Where(item => item.DailyFactId == dailyFactId.Value);
        }

        var files = await query.OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return Results.Ok(files.Select(EvidenceFileResponse.From).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid projectId,
        Guid evidenceId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        EvidenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "evidence.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var evidence = await FindAsync(dbContext, actor.TenantId, projectId, evidenceId, cancellationToken);
        return evidence is null ? Results.NotFound() : Results.Ok(EvidenceFileResponse.From(evidence));
    }

    private static async Task<IResult> CreateUploadSessionAsync(
        Guid projectId,
        CreateEvidenceUploadSessionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IDailyFactDirectory dailyFactDirectory,
        EvidenceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "evidence.upload", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "evidence.create-upload-session", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var target = await dailyFactDirectory.FindAsync(
            actor.TenantId, projectId, request.DailyReportId, request.DailyFactId, cancellationToken);
        if (target is null)
        {
            return Results.NotFound(new { code = "evidence.target.not_found" });
        }

        var evidence = await FindAsync(
            dbContext, actor.TenantId, projectId, request.ClientGeneratedId, cancellationToken);
        var eventType = "EvidenceUploadSessionCreated";
        var statusCode = StatusCodes.Status201Created;
        if (evidence is null)
        {
            evidence = EvidenceFile.CreatePending(
                request.ClientGeneratedId,
                actor.TenantId,
                projectId,
                request.DailyReportId,
                request.DailyFactId,
                request.OriginalFileName,
                request.ContentType,
                request.SizeBytes,
                request.Sha256,
                BuildObjectKey(actor.TenantId, projectId, request.ClientGeneratedId, request.ContentType),
                request.CapturedAtDevice,
                actor.UserId,
                clock.UtcNow);
            dbContext.EvidenceFiles.Add(evidence);
        }
        else
        {
            if (!evidence.MatchesUploadIntent(
                    request.DailyReportId,
                    request.DailyFactId,
                    request.OriginalFileName,
                    request.ContentType,
                    request.SizeBytes,
                    request.Sha256))
            {
                return Results.Conflict(new { code = "evidence.client_id.reused" });
            }

            if (evidence.Status == EvidenceFileStatus.PendingUpload)
            {
                evidence.RenewUploadSession(clock.UtcNow);
                eventType = "EvidenceUploadSessionRenewed";
            }
            else
            {
                eventType = "EvidenceUploadAlreadyCompleted";
            }
            statusCode = StatusCodes.Status200OK;
        }

        var response = BuildSessionResponse(evidence);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            evidence,
            eventType,
            response,
            idempotency,
            statusCode,
            sideEffectWriter,
            clock,
            cancellationToken);
        return statusCode == StatusCodes.Status201Created
            ? Results.Created(response.Evidence.ContentUrl, response)
            : Results.Ok(response);
    }

    private static async Task<IResult> UploadContentAsync(
        Guid projectId,
        Guid evidenceId,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        EvidenceDbContext dbContext,
        IObjectStorage objectStorage,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "evidence.upload", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var evidence = await FindAsync(dbContext, actor.TenantId, projectId, evidenceId, cancellationToken);
        if (evidence is null)
        {
            return Results.NotFound();
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "evidence.upload-content",
            new { evidenceId, evidence.Sha256, evidence.SizeBytes },
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (evidence.Status == EvidenceFileStatus.Uploaded)
        {
            return Results.Ok(EvidenceFileResponse.From(evidence));
        }

        if (evidence.UploadExpiresAt < clock.UtcNow)
        {
            return Results.Json(new { code = "evidence.upload_session.expired" }, statusCode: StatusCodes.Status410Gone);
        }

        if (!string.Equals(httpContext.Request.ContentType, evidence.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.UnprocessableEntity(new { code = "evidence.content_type.mismatch" });
        }

        await using var content = await ReadUploadAsync(httpContext.Request.Body, EvidenceFile.MaximumSizeBytes, cancellationToken);
        if (content.Length != evidence.SizeBytes)
        {
            return Results.UnprocessableEntity(new { code = "evidence.size.mismatch", expected = evidence.SizeBytes, actual = content.Length });
        }

        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken)).ToLowerInvariant();
        content.Position = 0;
        if (!string.Equals(actualHash, evidence.Sha256, StringComparison.Ordinal))
        {
            return Results.UnprocessableEntity(new { code = "evidence.sha256.mismatch" });
        }

        if (!EvidenceContentPolicy.MatchesSignature(
                evidence.ContentType,
                content.GetBuffer().AsSpan(0, checked((int)content.Length))))
        {
            return Results.UnprocessableEntity(new { code = "evidence.content_signature.mismatch" });
        }

        var receipt = await objectStorage.PutAsync(
            evidence.ObjectKey, evidence.ContentType, evidence.Sha256, content, cancellationToken);
        if (receipt.SizeBytes != evidence.SizeBytes || string.IsNullOrWhiteSpace(receipt.ETag))
        {
            await objectStorage.DeleteAsync(evidence.ObjectKey, cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Object storage size verification failed.",
                extensions: new Dictionary<string, object?> { ["code"] = "evidence.storage_verification.failed" });
        }

        evidence.MarkUploaded(receipt.ETag, clock.UtcNow);
        var response = EvidenceFileResponse.From(evidence);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            evidence,
            "EvidenceUploaded",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> DownloadAsync(
        Guid projectId,
        Guid evidenceId,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        EvidenceDbContext dbContext,
        IObjectStorage objectStorage,
        IAuditTrail auditTrail,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "evidence.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var evidence = await FindAsync(dbContext, actor.TenantId, projectId, evidenceId, cancellationToken);
        if (evidence is null || evidence.Status != EvidenceFileStatus.Uploaded)
        {
            return Results.NotFound();
        }

        var content = await objectStorage.ReadAsync(evidence.ObjectKey, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        var storedHash = Convert.ToHexString(SHA256.HashData(content.Bytes)).ToLowerInvariant();
        if (content.Bytes.LongLength != evidence.SizeBytes ||
            !string.Equals(storedHash, evidence.Sha256, StringComparison.Ordinal) ||
            !string.Equals(content.ContentType, evidence.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Object storage integrity verification failed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "evidence.storage_integrity.failed"
                });
        }

        await auditTrail.WriteAsync(
            new AuditEntry(
                actor.TenantId,
                evidence.ProjectId,
                actor.UserId,
                "EvidenceDownloaded",
                "EvidenceFile",
                evidence.Id.ToString(),
                clock.UtcNow,
                new Dictionary<string, object?>
                {
                    ["dailyReportId"] = evidence.DailyReportId,
                    ["dailyFactId"] = evidence.DailyFactId,
                    ["contentType"] = evidence.ContentType,
                    ["sizeBytes"] = evidence.SizeBytes,
                    ["sha256"] = evidence.Sha256,
                    ["revision"] = evidence.Revision
                },
                httpContext.TraceIdentifier),
            cancellationToken);
        return Results.File(
            content.Bytes,
            evidence.ContentType,
            evidence.OriginalFileName,
            enableRangeProcessing: true);
    }

    private static async Task<MemoryStream> ReadUploadAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var destination = new MemoryStream();
        var buffer = new byte[81_920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maximumBytes)
            {
                await destination.DisposeAsync();
                throw new BadHttpRequestException("Evidence exceeds the 25 MiB upload limit.", StatusCodes.Status413PayloadTooLarge);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        destination.Position = 0;
        return destination;
    }

    private static EvidenceUploadSessionResponse BuildSessionResponse(EvidenceFile evidence) => new(
        EvidenceFileResponse.From(evidence),
        evidence.Status == EvidenceFileStatus.PendingUpload ? "PUT" : null,
        evidence.Status == EvidenceFileStatus.PendingUpload ? evidence.ContentUrl() : null,
        evidence.Status == EvidenceFileStatus.PendingUpload ? evidence.UploadExpiresAt : null);

    private static string ContentUrl(this EvidenceFile evidence) =>
        $"/api/v1/projects/{evidence.ProjectId}/evidence/{evidence.Id}/content";

    private static string BuildObjectKey(Guid tenantId, Guid projectId, Guid evidenceId, string contentType) =>
        $"tenants/{tenantId:N}/projects/{projectId:N}/evidence/{evidenceId:N}" +
        EvidenceContentPolicy.CanonicalExtension(contentType.Trim().ToLowerInvariant());

    private static Task<EvidenceFile?> FindAsync(
        EvidenceDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid evidenceId,
        CancellationToken cancellationToken) =>
        dbContext.EvidenceFiles.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == evidenceId,
            cancellationToken);

    private static async Task<(string Key, string Hash, string Operation, IResult? Result)> GetReplayAsync<TRequest>(
        HttpContext httpContext,
        ICurrentActor actor,
        IIdempotencyStore store,
        string operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (string.Empty, string.Empty, operation, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }

        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await store.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return (key, hash, operation, replay is null
            ? null
            : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static async Task PersistAsync<TResponse>(
        EvidenceDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        EvidenceFile evidence,
        string eventType,
        TResponse response,
        (string Key, string Hash, string Operation, IResult? Result) idempotency,
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
                    actor.TenantId,
                    evidence.ProjectId,
                    actor.UserId,
                    eventType,
                    "EvidenceFile",
                    evidence.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["dailyReportId"] = evidence.DailyReportId,
                        ["dailyFactId"] = evidence.DailyFactId,
                        ["status"] = evidence.Status.ToString(),
                        ["sizeBytes"] = evidence.SizeBytes,
                        ["sha256"] = evidence.Sha256,
                        ["revision"] = evidence.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, evidence.ProjectId, $"Evidence.{eventType}", 1,
                    clock.UtcNow, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    idempotency.Operation,
                    idempotency.Hash,
                    statusCode,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(actor.TenantId, actor.UserId, projectId, permission, cancellationToken);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
