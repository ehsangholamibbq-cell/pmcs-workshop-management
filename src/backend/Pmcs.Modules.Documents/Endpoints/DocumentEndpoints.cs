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
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Documents.Persistence;
using Pmcs.Modules.Documents.Scanning;
using Pmcs.Modules.Documents.Storage;

namespace Pmcs.Modules.Documents.Endpoints;

internal static class DocumentEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var documents = endpoints.MapGroup("/api/v1/documents").WithTags("Documents");
        documents.MapGet("", ListAsync);
        documents.MapGet("/{documentId:guid}", GetAsync);
        documents.MapGet("/{documentId:guid}/content", DownloadAsync);
        documents.MapPut("/{documentId:guid}/content", UploadContentAsync)
            .DisableAntiforgery();
        documents.MapPost("/{documentId:guid}/release", ReleaseAsync);
        documents.MapPut("/{documentId:guid}/classification", UpdateGovernanceAsync);

        var uploads = endpoints.MapGroup("/api/v1/upload-sessions").WithTags("Documents");
        uploads.MapPost("", CreateUploadSessionAsync);
    }

    private static async Task<IResult> ListAsync(
        Guid? projectId,
        DocumentOwnerType? ownerType,
        Guid? ownerId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        DocumentsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (ownerType.HasValue && !Enum.IsDefined(ownerType.Value))
        {
            return Results.BadRequest(new { code = "documents.owner_type.invalid" });
        }

        if (ownerId.HasValue && !ownerType.HasValue)
        {
            return Results.BadRequest(new { code = "documents.owner_type.required" });
        }

        var tenantWide = await permissionService.HasTenantPermissionAsync(
            actor.TenantId, actor.UserId, "documents.read", cancellationToken);
        if (projectId.HasValue)
        {
            var projectAllowed = tenantWide || await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId.Value,
                "documents.read",
                cancellationToken);
            if (!projectAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }
        }
        else if (!tenantWide)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = dbContext.Assets.AsNoTracking()
            .Where(asset => asset.TenantId == actor.TenantId && asset.Status != DocumentAssetStatus.Deleted);
        if (projectId.HasValue)
        {
            query = query.Where(asset => asset.ProjectId == projectId.Value);
        }

        if (!tenantWide)
        {
            query = query.Where(asset => asset.Classification != DocumentClassification.Restricted);
        }

        if (ownerType.HasValue)
        {
            query = query.Where(asset => asset.OwnerType == ownerType.Value);
        }

        if (ownerId.HasValue)
        {
            query = query.Where(asset => asset.OwnerId == ownerId.Value);
        }

        var assets = await query
            .OrderByDescending(asset => asset.CreatedAt)
            .ThenByDescending(asset => asset.VersionNumber)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Results.Ok(assets.Select(DocumentAssetResponse.From).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid documentId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        DocumentsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var asset = await FindAsync(dbContext, actor.TenantId, documentId, cancellationToken);
        if (asset is null || asset.Status == DocumentAssetStatus.Deleted)
        {
            return Results.NotFound();
        }

        if (!await HasReadPermissionAsync(permissionService, actor, asset, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Ok(DocumentAssetResponse.From(asset));
    }

    private static async Task<IResult> CreateUploadSessionAsync(
        CreateDocumentUploadSessionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectTenantDirectory projectDirectory,
        DocumentsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!Enum.IsDefined(request.OwnerType) ||
            !Enum.IsDefined(request.Classification) ||
            !Enum.IsDefined(request.RetentionPolicy))
        {
            return Results.UnprocessableEntity(new { code = "documents.metadata.invalid" });
        }

        var projectOwned = DocumentAsset.RequiresProject(request.OwnerType);
        if (projectOwned && (!request.ProjectId.HasValue || request.ProjectId.Value == Guid.Empty))
        {
            return Results.UnprocessableEntity(new { code = "documents.project.required" });
        }

        if (!projectOwned && request.ProjectId.HasValue)
        {
            return Results.UnprocessableEntity(new { code = "documents.project.not_allowed" });
        }

        if (request.ProjectId.HasValue)
        {
            var projects = await projectDirectory.ExistingProjectIdsAsync(
                actor.TenantId,
                [request.ProjectId.Value],
                cancellationToken);
            if (!projects.Contains(request.ProjectId.Value))
            {
                return Results.NotFound(new { code = "documents.project.not_found" });
            }
        }

        if (!await HasScopePermissionAsync(
                permissionService,
                actor,
                request.ProjectId,
                "documents.upload",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var customGovernance = request.Classification != DocumentClassification.Internal ||
            request.RetentionPolicy != DocumentRetentionPolicy.Standard ||
            request.RetainUntil.HasValue ||
            request.LegalHold;
        if (customGovernance && !await HasScopePermissionAsync(
                permissionService,
                actor,
                request.ProjectId,
                "documents.classify",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "documents.create-upload-session",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var asset = await FindAsync(
            dbContext,
            actor.TenantId,
            request.ClientGeneratedId,
            cancellationToken);
        var auditEvent = "DocumentUploadSessionCreated";
        var statusCode = StatusCodes.Status201Created;
        if (asset is null)
        {
            var latestVersion = await dbContext.Assets.AsNoTracking()
                .Where(candidate => candidate.TenantId == actor.TenantId &&
                    candidate.OwnerType == request.OwnerType &&
                    candidate.OwnerId == request.OwnerId)
                .Select(candidate => (int?)candidate.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;
            var nextVersion = checked(latestVersion + 1);
            asset = DocumentAsset.CreatePending(
                request.ClientGeneratedId,
                actor.TenantId,
                request.ProjectId,
                request.OwnerType,
                request.OwnerId,
                nextVersion,
                request.OriginalFileName,
                request.ContentType,
                request.SizeBytes,
                request.Sha256,
                BuildObjectKey(
                    actor.TenantId,
                    request.ProjectId,
                    request.ClientGeneratedId,
                    nextVersion,
                    request.ContentType),
                request.Classification,
                request.RetentionPolicy,
                request.RetainUntil,
                request.LegalHold,
                actor.UserId,
                clock.UtcNow);
            dbContext.Assets.Add(asset);
        }
        else
        {
            if (!asset.MatchesUploadIntent(
                    request.ProjectId,
                    request.OwnerType,
                    request.OwnerId,
                    request.OriginalFileName,
                    request.ContentType,
                    request.SizeBytes,
                    request.Sha256,
                    request.Classification,
                    request.RetentionPolicy,
                    request.RetainUntil,
                    request.LegalHold))
            {
                return Results.Conflict(new { code = "documents.client_id.reused" });
            }

            if (asset.Status == DocumentAssetStatus.PendingUpload)
            {
                asset.RenewUploadSession(clock.UtcNow);
                auditEvent = "DocumentUploadSessionRenewed";
            }
            else
            {
                auditEvent = "DocumentUploadAlreadyCompleted";
            }

            statusCode = StatusCodes.Status200OK;
        }

        var response = BuildSessionResponse(asset);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            asset,
            auditEvent,
            $"Documents.{auditEvent}",
            response,
            idempotency,
            statusCode,
            sideEffectWriter,
            clock,
            cancellationToken);
        return statusCode == StatusCodes.Status201Created
            ? Results.Created($"/api/v1/documents/{asset.Id}", response)
            : Results.Ok(response);
    }

    private static async Task<IResult> UploadContentAsync(
        Guid documentId,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        DocumentsDbContext dbContext,
        IDocumentObjectStorage objectStorage,
        IContentScanner scanner,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var asset = await FindAsync(dbContext, actor.TenantId, documentId, cancellationToken);
        if (asset is null)
        {
            return Results.NotFound();
        }

        if (!await HasScopePermissionAsync(
                permissionService,
                actor,
                asset.ProjectId,
                "documents.upload",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "documents.upload-content",
            new { documentId, asset.Sha256, asset.SizeBytes },
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (asset.Status is DocumentAssetStatus.Quarantined or DocumentAssetStatus.Released)
        {
            var completed = DocumentAssetResponse.From(asset);
            await PersistAsync(
                dbContext,
                httpContext,
                actor,
                asset,
                "DocumentUploadAlreadyCompleted",
                "Documents.DocumentUploadAlreadyCompleted",
                completed,
                idempotency,
                StatusCodes.Status200OK,
                sideEffectWriter,
                clock,
                cancellationToken);
            return Results.Ok(completed);
        }

        if (asset.Status == DocumentAssetStatus.Rejected)
        {
            var rejected = new
            {
                code = "documents.upload.rejected",
                document = DocumentAssetResponse.From(asset)
            };
            await PersistAsync(
                dbContext,
                httpContext,
                actor,
                asset,
                "DocumentUploadAlreadyRejected",
                "Documents.DocumentUploadAlreadyRejected",
                rejected,
                idempotency,
                StatusCodes.Status409Conflict,
                sideEffectWriter,
                clock,
                cancellationToken);
            return Results.Json(rejected, statusCode: StatusCodes.Status409Conflict);
        }

        if (asset.UploadExpiresAt < clock.UtcNow)
        {
            return Results.Json(
                new { code = "documents.upload_session.expired" },
                statusCode: StatusCodes.Status410Gone);
        }

        var requestContentType = NormalizeContentType(httpContext.Request.ContentType);
        if (!string.Equals(requestContentType, asset.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.UnprocessableEntity(new { code = "documents.content_type.mismatch" });
        }

        await using var content = await ReadUploadAsync(
            httpContext.Request.Body,
            DocumentAsset.MaximumSizeBytes,
            cancellationToken);
        if (content.Length != asset.SizeBytes)
        {
            return Results.UnprocessableEntity(new
            {
                code = "documents.size.mismatch",
                expected = asset.SizeBytes,
                actual = content.Length
            });
        }

        var bytes = content.ToArray();
        var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actualHash, asset.Sha256, StringComparison.Ordinal))
        {
            return Results.UnprocessableEntity(new { code = "documents.sha256.mismatch" });
        }

        var scan = await scanner.ScanAsync(
            asset.OriginalFileName,
            asset.ContentType,
            bytes,
            cancellationToken);
        if (scan.Verdict != DocumentScanVerdict.Clean)
        {
            asset.MarkRejected(scan.Verdict, scan.Provider, scan.Details, clock.UtcNow);
            var rejected = new
            {
                code = scan.Verdict == DocumentScanVerdict.Infected
                    ? "documents.scan.infected"
                    : "documents.scan.failed",
                document = DocumentAssetResponse.From(asset)
            };
            await PersistAsync(
                dbContext,
                httpContext,
                actor,
                asset,
                "DocumentUploadRejected",
                "Documents.DocumentUploadRejected",
                rejected,
                idempotency,
                StatusCodes.Status422UnprocessableEntity,
                sideEffectWriter,
                clock,
                cancellationToken);
            return Results.Json(rejected, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        content.Position = 0;
        var receipt = await objectStorage.PutAsync(
            asset.ObjectKey,
            asset.ContentType,
            asset.Sha256,
            content,
            cancellationToken);
        if (receipt.SizeBytes != asset.SizeBytes || string.IsNullOrWhiteSpace(receipt.ETag))
        {
            await objectStorage.DeleteAsync(asset.ObjectKey, cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Object storage size verification failed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "documents.storage_verification.failed"
                });
        }

        asset.MarkQuarantined(receipt.ETag, scan.Provider, scan.Details, clock.UtcNow);
        var response = DocumentAssetResponse.From(asset);
        try
        {
            await PersistAsync(
                dbContext,
                httpContext,
                actor,
                asset,
                "DocumentQuarantined",
                "Documents.DocumentQuarantined",
                response,
                idempotency,
                StatusCodes.Status200OK,
                sideEffectWriter,
                clock,
                cancellationToken);
        }
        catch
        {
            await objectStorage.DeleteAsync(asset.ObjectKey, CancellationToken.None);
            throw;
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> ReleaseAsync(
        Guid documentId,
        ReleaseDocumentRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        DocumentsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "documents.quarantine.release",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var asset = await FindAsync(dbContext, actor.TenantId, documentId, cancellationToken);
        if (asset is null)
        {
            return Results.NotFound();
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "documents.release",
            new { documentId, request.BaseRevision },
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (asset.Revision != request.BaseRevision)
        {
            return Results.Conflict(new
            {
                code = "documents.revision.conflict",
                currentRevision = asset.Revision
            });
        }

        asset.Release(request.BaseRevision, actor.UserId, clock.UtcNow);
        var response = DocumentAssetResponse.From(asset);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            asset,
            "DocumentReleased",
            "documents.asset.released.v1",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateGovernanceAsync(
        Guid documentId,
        UpdateDocumentGovernanceRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        DocumentsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!Enum.IsDefined(request.Classification) || !Enum.IsDefined(request.RetentionPolicy))
        {
            return Results.UnprocessableEntity(new { code = "documents.metadata.invalid" });
        }

        var asset = await FindAsync(dbContext, actor.TenantId, documentId, cancellationToken);
        if (asset is null)
        {
            return Results.NotFound();
        }

        if (!await HasScopePermissionAsync(
                permissionService,
                actor,
                asset.ProjectId,
                "documents.classify",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "documents.update-governance",
            new { documentId, request },
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (asset.Revision != request.BaseRevision)
        {
            return Results.Conflict(new
            {
                code = "documents.revision.conflict",
                currentRevision = asset.Revision
            });
        }

        asset.UpdateGovernance(
            request.BaseRevision,
            request.Classification,
            request.RetentionPolicy,
            request.RetainUntil,
            request.LegalHold,
            actor.UserId,
            clock.UtcNow);
        var response = DocumentAssetResponse.From(asset);
        await PersistAsync(
            dbContext,
            httpContext,
            actor,
            asset,
            "DocumentGovernanceUpdated",
            "Documents.DocumentGovernanceUpdated",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> DownloadAsync(
        Guid documentId,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        DocumentsDbContext dbContext,
        IDocumentObjectStorage objectStorage,
        IAuditTrail auditTrail,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var asset = await FindAsync(dbContext, actor.TenantId, documentId, cancellationToken);
        if (asset is null || asset.Status == DocumentAssetStatus.Deleted)
        {
            return Results.NotFound();
        }

        if (!await HasReadPermissionAsync(permissionService, actor, asset, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (asset.Status != DocumentAssetStatus.Released)
        {
            return Results.Conflict(new { code = "documents.content.not_released" });
        }

        var content = await objectStorage.ReadAsync(asset.ObjectKey, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        var storedHash = Convert.ToHexString(SHA256.HashData(content.Bytes)).ToLowerInvariant();
        if (content.Bytes.LongLength != asset.SizeBytes ||
            !string.Equals(storedHash, asset.Sha256, StringComparison.Ordinal) ||
            !string.Equals(content.ContentType, asset.ContentType, StringComparison.OrdinalIgnoreCase) ||
            !DocumentContentPolicy.MatchesSignature(asset.ContentType, content.Bytes))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Object storage integrity verification failed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "documents.storage_integrity.failed"
                });
        }

        await auditTrail.WriteAsync(
            new AuditEntry(
                actor.TenantId,
                asset.ProjectId,
                actor.UserId,
                "DocumentDownloaded",
                "DocumentAsset",
                asset.Id.ToString(),
                clock.UtcNow,
                new Dictionary<string, object?>
                {
                    ["ownerType"] = asset.OwnerType.ToString(),
                    ["ownerId"] = asset.OwnerId,
                    ["versionNumber"] = asset.VersionNumber,
                    ["classification"] = asset.Classification.ToString(),
                    ["contentType"] = asset.ContentType,
                    ["sizeBytes"] = asset.SizeBytes,
                    ["sha256"] = asset.Sha256,
                    ["revision"] = asset.Revision
                },
                httpContext.TraceIdentifier),
            cancellationToken);
        return Results.File(
            content.Bytes,
            asset.ContentType,
            asset.OriginalFileName,
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
                throw new BadHttpRequestException(
                    "Document exceeds the 25 MiB upload limit.",
                    StatusCodes.Status413PayloadTooLarge);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        destination.Position = 0;
        return destination;
    }

    private static DocumentUploadSessionResponse BuildSessionResponse(DocumentAsset asset) => new(
        DocumentAssetResponse.From(asset),
        asset.Status == DocumentAssetStatus.PendingUpload ? "PUT" : null,
        asset.Status == DocumentAssetStatus.PendingUpload
            ? $"/api/v1/documents/{asset.Id}/content"
            : null,
        asset.Status == DocumentAssetStatus.PendingUpload ? asset.UploadExpiresAt : null);

    private static string BuildObjectKey(
        Guid tenantId,
        Guid? projectId,
        Guid documentId,
        int versionNumber,
        string contentType)
    {
        var scope = projectId.HasValue ? $"projects/{projectId.Value:N}" : "tenant";
        return $"tenants/{tenantId:N}/{scope}/documents/{documentId:N}/v{versionNumber}" +
            DocumentContentPolicy.CanonicalExtension(contentType.Trim().ToLowerInvariant());
    }

    private static Task<DocumentAsset?> FindAsync(
        DocumentsDbContext dbContext,
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Assets.SingleOrDefaultAsync(
            asset => asset.TenantId == tenantId && asset.Id == documentId,
            cancellationToken);

    private static async Task<bool> HasReadPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        DocumentAsset asset,
        CancellationToken cancellationToken)
    {
        if (asset.Classification == DocumentClassification.Restricted || !asset.ProjectId.HasValue)
        {
            return await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "documents.read",
                cancellationToken);
        }

        return await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            asset.ProjectId.Value,
            "documents.read",
            cancellationToken);
    }

    private static Task<bool> HasScopePermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid? projectId,
        string permission,
        CancellationToken cancellationToken) =>
        projectId.HasValue
            ? permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId.Value,
                permission,
                cancellationToken)
            : permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                permission,
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
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "idempotency.key.required"
                }));
        }

        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await store.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return (key, hash, operation, replay is null
            ? null
            : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static async Task PersistAsync<TResponse>(
        DocumentsDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        DocumentAsset asset,
        string auditEventType,
        string outboxEventType,
        TResponse response,
        (string Key, string Hash, string Operation, IResult? Result) idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var occurredAt = clock.UtcNow;
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        var eventPayload = JsonSerializer.Serialize(new
        {
            assetId = asset.Id,
            asset.ProjectId,
            ownerType = asset.OwnerType.ToString(),
            asset.OwnerId,
            asset.VersionNumber,
            status = asset.Status.ToString(),
            classification = asset.Classification.ToString(),
            occurredAt
        }, SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    asset.ProjectId,
                    actor.UserId,
                    auditEventType,
                    "DocumentAsset",
                    asset.Id.ToString(),
                    occurredAt,
                    new Dictionary<string, object?>
                    {
                        ["ownerType"] = asset.OwnerType.ToString(),
                        ["ownerId"] = asset.OwnerId,
                        ["versionNumber"] = asset.VersionNumber,
                        ["status"] = asset.Status.ToString(),
                        ["scanVerdict"] = asset.ScanVerdict.ToString(),
                        ["classification"] = asset.Classification.ToString(),
                        ["retentionPolicy"] = asset.RetentionPolicy.ToString(),
                        ["legalHold"] = asset.LegalHold,
                        ["sizeBytes"] = asset.SizeBytes,
                        ["sha256"] = asset.Sha256,
                        ["revision"] = asset.Revision
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    asset.ProjectId,
                    outboxEventType,
                    1,
                    occurredAt,
                    eventPayload,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    idempotency.Operation,
                    idempotency.Hash,
                    statusCode,
                    responseJson,
                    occurredAt,
                    occurredAt.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string NormalizeContentType(string? contentType) =>
        contentType?.Split(';', 2)[0].Trim().ToLowerInvariant() ?? string.Empty;

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
