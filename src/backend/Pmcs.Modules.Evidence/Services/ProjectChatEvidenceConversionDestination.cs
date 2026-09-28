using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Evidence.Domain;
using Pmcs.Modules.Evidence.Persistence;
using Pmcs.Modules.FieldOperations.Contracts;

namespace Pmcs.Modules.Evidence.Services;

internal sealed class ProjectChatEvidenceConversionDestination(
    IProjectPermissionService permissions, IDailyFactDirectory facts,
    ISharedDocumentDirectory documents, ITransactionalSideEffectWriter effects)
    : IProjectMessageConversionDestination
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public bool Supports(string destinationType) => destinationType == "Evidence";

    public async Task<ProjectMessageConversionResult> ExecuteAsync(
        ProjectMessageConversionCommand command, CancellationToken cancellationToken = default)
    {
        if (!await CanAsync(command, cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.evidence_permission.denied", 403);
        if (command.Documents.Count != 1)
            throw new ProjectMessageConversionException("collaboration.conversion.evidence.one_released_file_required", 422);
        EvidenceDetails details;
        try { details = command.Details.Deserialize<EvidenceDetails>(JsonOptions) ?? throw new JsonException(); }
        catch (JsonException)
        {
            throw new ProjectMessageConversionException("collaboration.conversion.details.invalid", 422);
        }
        if (details.DailyReportId == Guid.Empty || details.DailyFactId == Guid.Empty ||
            await facts.FindAsync(command.TenantId, command.ProjectId,
                details.DailyReportId, details.DailyFactId, cancellationToken) is null)
            throw new ProjectMessageConversionException("collaboration.conversion.evidence.target.not_found", 404);
        var source = command.Documents[0];
        var released = await documents.ReadReleasedAsync(command.TenantId, source.Id,
            DocumentOwnerType.ProjectChat, command.MessageId, cancellationToken);
        if (released is null || released.Document.ProjectId != command.ProjectId ||
            released.Document.VersionNumber != source.VersionNumber ||
            !string.Equals(released.Document.Sha256, source.Sha256,
                StringComparison.OrdinalIgnoreCase))
            throw new ProjectMessageConversionException("collaboration.conversion.evidence.source.not_released", 404);
        if (released.Document.Classification == DocumentClassification.Restricted &&
            !await permissions.HasTenantPermissionAsync(command.TenantId,
                command.ActorUserId, "documents.read", cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.evidence.classification.denied", 403);
        if (!EvidenceContentPolicy.IsAllowedContentType(source.ContentType) ||
            source.SizeBytes > EvidenceFile.MaximumSizeBytes)
            throw new ProjectMessageConversionException("collaboration.conversion.evidence.file.unsupported", 422);

        await using var db = new EvidenceDbContext(new DbContextOptionsBuilder<EvidenceDbContext>()
            .UseNpgsql(command.Connection).Options);
        await db.Database.UseTransactionAsync(command.Transaction, cancellationToken);
        var evidence = EvidenceFile.CreateFromReleasedChat(command.DestinationId,
            command.TenantId, command.ProjectId, details.DailyReportId,
            details.DailyFactId, command.MessageId, source.Id, source.VersionNumber,
            source.FileName, source.ContentType, source.SizeBytes, source.Sha256,
            command.ActorUserId, command.At);
        db.EvidenceFiles.Add(evidence);
        if (!await CanAsync(command, cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.evidence_permission.denied", 403);
        await db.SaveChangesAsync(cancellationToken);
        await effects.WriteEventAsync(command.Connection, command.Transaction,
            new TransactionalEventBatch(
                new AuditEntry(command.TenantId, command.ProjectId, command.ActorUserId,
                    "ProjectChatConvertedToEvidence", "EvidenceFile", evidence.Id.ToString(),
                    command.At, new Dictionary<string, object?>
                    {
                        ["sourceMessageId"] = command.MessageId,
                        ["sourceRevision"] = command.MessageRevision,
                        ["sourceDocumentId"] = source.Id,
                        ["sourceDocumentVersion"] = source.VersionNumber,
                        ["sourceSha256"] = source.Sha256,
                        ["dailyReportId"] = details.DailyReportId,
                        ["dailyFactId"] = details.DailyFactId
                    }, command.CorrelationId),
                new OutboxEnvelope(Guid.NewGuid(), command.TenantId, command.ProjectId,
                    "Evidence.ProjectChatConvertedToEvidence", 1, command.At,
                    JsonSerializer.Serialize(new { evidence.Id, command.MessageId,
                        command.MessageRevision, sourceDocumentId = source.Id }, JsonOptions),
                    command.CorrelationId)), cancellationToken);
        return new ProjectMessageConversionResult(evidence.Id, "Evidence",
            evidence.Id.ToString("N"));
    }

    private async Task<bool> CanAsync(ProjectMessageConversionCommand command,
        CancellationToken cancellationToken) =>
        await permissions.HasProjectPermissionAsync(command.TenantId, command.ActorUserId,
            command.ProjectId, "evidence.upload", cancellationToken) &&
        await permissions.HasProjectPermissionAsync(command.TenantId, command.ActorUserId,
            command.ProjectId, "documents.read", cancellationToken);

    private sealed record EvidenceDetails(Guid DailyReportId, Guid? DailyFactId);
}
