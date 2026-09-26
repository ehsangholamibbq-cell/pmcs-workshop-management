using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Documents.Persistence;
using Pmcs.Modules.Documents.Storage;

namespace Pmcs.Modules.Documents.Services;

internal sealed class GeneratedDocumentPublisher(
    DocumentsDbContext dbContext,
    IDocumentObjectStorage objectStorage,
    ITransactionalSideEffectWriter sideEffectWriter) : IGeneratedDocumentPublisher
{
    private const int VersionNumber = 1;
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<GeneratedDocumentReference> PublishReportOutputAsync(
        GeneratedDocumentPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var existing = await dbContext.Assets.SingleOrDefaultAsync(
            asset => asset.TenantId == request.TenantId && asset.Id == request.DocumentId,
            cancellationToken);
        if (existing is not null)
        {
            await VerifyExistingAsync(existing, request, cancellationToken);
            return ToReference(existing);
        }

        var objectKey = BuildObjectKey(request);
        await using var content = new MemoryStream(request.Bytes, writable: false);
        DocumentStoredObjectReceipt receipt;
        try
        {
            receipt = await objectStorage.PutAsync(
                objectKey,
                request.ContentType,
                request.Sha256,
                content,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new GeneratedDocumentPublishException(
                "documents.generated.storage_unavailable",
                transient: true,
                "Generated document storage is unavailable.",
                exception);
        }

        if (receipt.SizeBytes != request.Bytes.LongLength || string.IsNullOrWhiteSpace(receipt.ETag))
        {
            throw new GeneratedDocumentPublishException(
                "documents.generated.storage_integrity_failed",
                transient: false,
                "Generated document storage receipt failed integrity verification.");
        }
        await VerifyStoredContentAsync(objectKey, request, cancellationToken);

        var asset = DocumentAsset.CreatePending(
            request.DocumentId,
            request.TenantId,
            request.ProjectId,
            DocumentOwnerType.ReportOutput,
            request.OwnerId,
            VersionNumber,
            request.FileName,
            request.ContentType,
            request.Bytes.LongLength,
            request.Sha256,
            objectKey,
            request.Classification,
            request.RetentionPolicy,
            request.RetainUntil,
            request.LegalHold,
            SystemActorId,
            request.CreatedAt);
        asset.MarkQuarantined(
            receipt.ETag,
            "pmcs-generated-content",
            "Server-rendered artifact passed allowlist, signature, size and SHA-256 validation.",
            request.CreatedAt);
        asset.Release(asset.Revision, SystemActorId, request.CreatedAt);
        dbContext.Assets.Add(asset);

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            var eventPayload = JsonSerializer.Serialize(new
            {
                documentId = asset.Id,
                projectId = asset.ProjectId,
                ownerType = asset.OwnerType.ToString(),
                ownerId = asset.OwnerId,
                contentType = asset.ContentType,
                sizeBytes = asset.SizeBytes,
                sha256 = asset.Sha256,
                classification = asset.Classification.ToString(),
                retentionPolicy = asset.RetentionPolicy.ToString()
            });
            await sideEffectWriter.WriteEventAsync(
                dbContext.Database.GetDbConnection(),
                transaction.GetDbTransaction(),
                new TransactionalEventBatch(
                    new AuditEntry(
                        asset.TenantId,
                        asset.ProjectId,
                        SystemActorId,
                        "GeneratedReportDocumentReleased",
                        "DocumentAsset",
                        asset.Id.ToString(),
                        request.CreatedAt,
                        new Dictionary<string, object?>
                        {
                            ["executor"] = "SystemWorker",
                            ["ownerType"] = asset.OwnerType.ToString(),
                            ["ownerId"] = asset.OwnerId,
                            ["contentType"] = asset.ContentType,
                            ["sizeBytes"] = asset.SizeBytes,
                            ["sha256"] = asset.Sha256,
                            ["classification"] = asset.Classification.ToString(),
                            ["retentionPolicy"] = asset.RetentionPolicy.ToString(),
                            ["revision"] = asset.Revision
                        },
                        request.CorrelationId),
                    new OutboxEnvelope(
                        Guid.NewGuid(),
                        asset.TenantId,
                        asset.ProjectId,
                        "documents.asset.released.v1",
                        1,
                        request.CreatedAt,
                        eventPayload,
                        request.CorrelationId)),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.Assets.SingleOrDefaultAsync(
                candidate => candidate.TenantId == request.TenantId && candidate.Id == request.DocumentId,
                cancellationToken);
            if (winner is null)
            {
                throw new GeneratedDocumentPublishException(
                    "documents.generated.persistence_failed",
                    transient: true,
                    "Generated document metadata could not be persisted.",
                    exception);
            }

            await VerifyExistingAsync(winner, request, cancellationToken);
            return ToReference(winner);
        }

        return ToReference(asset);
    }

    private async Task VerifyExistingAsync(
        DocumentAsset asset,
        GeneratedDocumentPublishRequest request,
        CancellationToken cancellationToken)
    {
        var matches = asset.ProjectId == request.ProjectId &&
            asset.OwnerType == DocumentOwnerType.ReportOutput &&
            asset.OwnerId == request.OwnerId &&
            asset.VersionNumber == VersionNumber &&
            string.Equals(asset.OriginalFileName, request.FileName, StringComparison.Ordinal) &&
            string.Equals(asset.ContentType, request.ContentType, StringComparison.OrdinalIgnoreCase) &&
            asset.SizeBytes == request.Bytes.LongLength &&
            string.Equals(asset.Sha256, request.Sha256, StringComparison.Ordinal) &&
            asset.Classification == request.Classification &&
            asset.RetentionPolicy == request.RetentionPolicy &&
            asset.LegalHold == request.LegalHold &&
            asset.Status == DocumentAssetStatus.Released &&
            asset.ReleasedAt.HasValue;
        if (!matches)
        {
            throw new GeneratedDocumentPublishException(
                "documents.generated.identity_conflict",
                transient: false,
                "Stable generated document identity was reused with different metadata.");
        }

        await VerifyStoredContentAsync(asset.ObjectKey, request, cancellationToken);
    }

    private async Task VerifyStoredContentAsync(
        string objectKey,
        GeneratedDocumentPublishRequest request,
        CancellationToken cancellationToken)
    {
        DocumentStoredObjectContent? stored;
        try
        {
            stored = await objectStorage.ReadAsync(objectKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new GeneratedDocumentPublishException(
                "documents.generated.storage_unavailable",
                transient: true,
                "Generated document storage is unavailable.",
                exception);
        }

        var actualHash = stored is null
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(stored.Bytes)).ToLowerInvariant();
        if (stored is null || stored.Bytes.LongLength != request.Bytes.LongLength ||
            !string.Equals(stored.ContentType, request.ContentType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actualHash, request.Sha256, StringComparison.Ordinal) ||
            !stored.Bytes.AsSpan().SequenceEqual(request.Bytes) ||
            !DocumentContentPolicy.MatchesSignature(request.ContentType, stored.Bytes))
        {
            throw new GeneratedDocumentPublishException(
                "documents.generated.storage_integrity_failed",
                transient: false,
                "Stored generated document failed integrity verification.");
        }
    }

    private static void ValidateRequest(GeneratedDocumentPublishRequest request)
    {
        if (request.DocumentId == Guid.Empty || request.TenantId == Guid.Empty ||
            request.ProjectId == Guid.Empty || request.OwnerId == Guid.Empty ||
            request.Bytes is null || request.Bytes.Length == 0 ||
            string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            throw new GeneratedDocumentPublishException(
                "documents.generated.request_invalid",
                transient: false,
                "Generated document request is incomplete.");
        }

        var normalizedContentType = request.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedHash = request.Sha256?.Trim().ToLowerInvariant() ?? string.Empty;
        var actualHash = Convert.ToHexString(SHA256.HashData(request.Bytes)).ToLowerInvariant();
        if (request.Bytes.LongLength > DocumentAsset.MaximumSizeBytes ||
            !DocumentContentPolicy.IsAllowedContentType(normalizedContentType) ||
            !DocumentContentPolicy.IsFileNameCompatible(request.FileName, normalizedContentType) ||
            !DocumentContentPolicy.MatchesSignature(normalizedContentType, request.Bytes) ||
            !string.Equals(normalizedHash, actualHash, StringComparison.Ordinal))
        {
            throw new GeneratedDocumentPublishException(
                "documents.generated.content_invalid",
                transient: false,
                "Generated document bytes or metadata failed the shared document policy.");
        }
    }

    private static string BuildObjectKey(GeneratedDocumentPublishRequest request)
    {
        var extension = DocumentContentPolicy.CanonicalExtension(
            request.ContentType.Trim().ToLowerInvariant());
        return $"tenants/{request.TenantId:N}/projects/{request.ProjectId:N}/documents/" +
            $"{request.DocumentId:N}/v{VersionNumber}{extension}";
    }

    private static GeneratedDocumentReference ToReference(DocumentAsset asset) => new(
        asset.Id,
        asset.TenantId,
        asset.ProjectId!.Value,
        asset.OwnerId,
        asset.OriginalFileName,
        asset.ContentType,
        asset.SizeBytes,
        asset.Sha256,
        asset.Classification,
        asset.RetentionPolicy,
        asset.ReleasedAt!.Value,
        asset.Revision);
}
