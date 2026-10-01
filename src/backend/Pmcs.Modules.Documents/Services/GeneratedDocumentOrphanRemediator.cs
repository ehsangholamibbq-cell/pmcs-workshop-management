using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Documents.Persistence;
using Pmcs.Modules.Documents.Storage;

namespace Pmcs.Modules.Documents.Services;

internal sealed class GeneratedDocumentOrphanRemediator(
    DocumentsDbContext dbContext,
    IDocumentObjectStorage objectStorage,
    ITransactionalSideEffectWriter sideEffectWriter) : IGeneratedDocumentOrphanRemediator
{
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<IReadOnlyList<GeneratedDocumentOrphanCandidate>> ListReportOutputCandidatesAsync(
        DateTimeOffset releasedBefore,
        Guid? afterDocumentId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Candidate page size must be between 1 and 100.");
        }

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = new NpgsqlCommand(
                """
                select id, tenant_id, project_id, owner_id, retention_policy, retain_until,
                       legal_hold, released_at, revision
                from documents.assets
                where owner_type = 'ReportOutput'
                  and status = 'Released'
                  and released_at is not null
                  and released_at <= @released_before
                  and (@after_document_id is null or id > @after_document_id)
                order by id
                limit @take;
                """,
                connection);
            command.Parameters.AddWithValue("released_before", releasedBefore.ToUniversalTime());
            command.Parameters.Add(new NpgsqlParameter("after_document_id", NpgsqlDbType.Uuid)
            {
                Value = afterDocumentId.HasValue ? afterDocumentId.Value : DBNull.Value
            });
            command.Parameters.AddWithValue("take", take);

            var candidates = new List<GeneratedDocumentOrphanCandidate>(take);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var retentionText = reader.GetString(4);
                DocumentRetentionPolicy? retentionPolicy =
                    Enum.TryParse<DocumentRetentionPolicy>(retentionText, ignoreCase: false, out var parsed) &&
                    Enum.IsDefined(parsed)
                        ? parsed
                        : null;
                candidates.Add(new GeneratedDocumentOrphanCandidate(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetGuid(3),
                    retentionPolicy,
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                    reader.GetBoolean(6),
                    reader.GetFieldValue<DateTimeOffset>(7),
                    reader.GetInt64(8)));
            }
            return candidates;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<GeneratedDocumentOrphanRemediationResult> RemediateReportOutputAsync(
        GeneratedDocumentOrphanRemediationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using (var documentLock = new NpgsqlCommand(
            """
            select id
            from documents.assets
            where id = @document_id
              and tenant_id = @tenant_id
              and project_id = @project_id
              and owner_type = 'ReportOutput'
              and owner_id = @owner_id
            for update;
            """,
            connection,
            postgresTransaction))
        {
            documentLock.Parameters.AddWithValue("document_id", request.DocumentId);
            documentLock.Parameters.AddWithValue("tenant_id", request.TenantId);
            documentLock.Parameters.AddWithValue("project_id", request.ProjectId);
            documentLock.Parameters.AddWithValue("owner_id", request.OwnerId);
            var locked = await documentLock.ExecuteScalarAsync(cancellationToken);
            if (locked is null)
            {
                return GeneratedDocumentOrphanRemediationResult.NotFound;
            }
        }

        var asset = await dbContext.Assets.SingleAsync(candidate =>
            candidate.Id == request.DocumentId &&
            candidate.TenantId == request.TenantId &&
            candidate.ProjectId == request.ProjectId &&
            candidate.OwnerType == DocumentOwnerType.ReportOutput &&
            candidate.OwnerId == request.OwnerId,
            cancellationToken);
        if (asset.Revision != request.ExpectedRevision)
        {
            return GeneratedDocumentOrphanRemediationResult.Changed;
        }
        if (asset.Status != DocumentAssetStatus.Released || !asset.ReleasedAt.HasValue)
        {
            return GeneratedDocumentOrphanRemediationResult.InvalidState;
        }
        if (asset.LegalHold)
        {
            return GeneratedDocumentOrphanRemediationResult.LegalHold;
        }
        if (asset.RetentionPolicy == DocumentRetentionPolicy.Permanent ||
            !asset.RetainUntil.HasValue ||
            asset.RetainUntil.Value > request.EvaluatedAt)
        {
            return GeneratedDocumentOrphanRemediationResult.RetentionActive;
        }

        var observedRevision = asset.Revision;
        var releasedAt = asset.ReleasedAt.Value;
        var retentionPolicy = asset.RetentionPolicy;
        var retainUntil = asset.RetainUntil;
        asset.MarkDeleted(asset.Revision, request.EvaluatedAt);
        var deletionRevision = asset.Revision;
        try
        {
            await objectStorage.DeleteAsync(asset.ObjectKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new GeneratedDocumentOrphanRemediationException(
                "documents.generated.orphan.storage_unavailable",
                transient: true,
                "Generated document orphan storage deletion failed.",
                exception);
        }

        dbContext.Assets.Remove(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAuditAsync(
            connection,
            postgresTransaction,
            new AuditEntry(
                request.TenantId,
                request.ProjectId,
                SystemActorId,
                "GeneratedReportOrphanRemediated",
                "DocumentAsset",
                request.DocumentId.ToString(),
                request.EvaluatedAt,
                new Dictionary<string, object?>
                {
                    ["executor"] = "SystemWorker",
                    ["ownerType"] = DocumentOwnerType.ReportOutput.ToString(),
                    ["ownerId"] = request.OwnerId,
                    ["runId"] = request.RunId,
                    ["retentionPolicy"] = retentionPolicy.ToString(),
                    ["retainUntil"] = retainUntil,
                    ["releasedAt"] = releasedAt,
                    ["observedRevision"] = observedRevision,
                    ["deletionRevision"] = deletionRevision
                },
                request.CorrelationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return GeneratedDocumentOrphanRemediationResult.Remediated;
    }

    private static void ValidateRequest(GeneratedDocumentOrphanRemediationRequest request)
    {
        if (request.DocumentId == Guid.Empty || request.TenantId == Guid.Empty ||
            request.ProjectId == Guid.Empty || request.OwnerId == Guid.Empty ||
            request.RunId == Guid.Empty || request.ExpectedRevision <= 0 ||
            string.IsNullOrWhiteSpace(request.CorrelationId) || request.CorrelationId.Length > 120)
        {
            throw new GeneratedDocumentOrphanRemediationException(
                "documents.generated.orphan.request_invalid",
                transient: false,
                "Generated document orphan remediation request is invalid.");
        }
    }
}
