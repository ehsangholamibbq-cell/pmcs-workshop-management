using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Services;

internal sealed class ProjectChatTechnicalConversionDestination(
    IProjectPermissionService permissions, IProjectDirectory projects,
    ITransactionalSideEffectWriter effects) : IProjectMessageConversionDestination
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public bool Supports(string destinationType) => destinationType is "RFI" or "TechnicalDocument";

    public async Task<ProjectMessageConversionResult> ExecuteAsync(
        ProjectMessageConversionCommand command, CancellationToken cancellationToken = default)
    {
        var permission = command.DestinationType == "RFI"
            ? "technical.rfis.create" : "technical.documents.create";
        if (!await CanAsync(command, permission, cancellationToken) ||
            command.DestinationType == "TechnicalDocument" &&
            !await CanAsync(command, "technical.documents.create-revision", cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.technical_permission.denied", 403);
        var project = await projects.FindProfileAsync(command.TenantId,
            command.ProjectId, cancellationToken);
        if (project is null)
            throw new ProjectMessageConversionException("collaboration.conversion.project.not_found", 404);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(command.At,
            TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone)).DateTime);
        await using var db = new TechnicalOfficeDbContext(new DbContextOptionsBuilder<TechnicalOfficeDbContext>()
            .UseNpgsql(command.Connection).Options);
        await db.Database.UseTransactionAsync(command.Transaction, cancellationToken);
        ProjectMessageConversionResult result;
        string auditEvent;
        if (command.DestinationType == "RFI")
        {
            var details = Read<RfiDetails>(command.Details);
            var evidence = new[] { $"collaboration:message:{command.MessageId:N}:revision:{command.MessageRevision}" }
                .Concat(command.Documents.Select(item =>
                    $"pmcs:chat-document:{command.MessageId:N}:{item.Id:N}:v{item.VersionNumber}:sha256:{item.Sha256}"))
                .ToArray();
            var rfi = TechnicalRfi.Create(command.DestinationId, command.TenantId,
                command.ProjectId, details.Title, details.Question ?? command.MessageBody,
                details.RequestedFrom, details.Discipline, null, null, null, null,
                null, localDate, details.RequiredByDate, details.PotentialImpact,
                details.IsBlocking, details.ProposedSolution, evidence, [],
                command.ActorUserId, command.At);
            db.Rfis.Add(rfi);
            result = new ProjectMessageConversionResult(rfi.Id, "RFI", rfi.Number);
            auditEvent = "ProjectChatConvertedToRfi";
        }
        else
        {
            if (command.Documents.Count != 1)
                throw new ProjectMessageConversionException("collaboration.conversion.document.one_released_file_required", 422);
            var details = Read<DocumentDetails>(command.Details);
            var source = command.Documents[0];
            var document = TechnicalDocument.Create(command.DestinationId,
                command.TenantId, command.ProjectId, details.Title, details.Type,
                details.Discipline, details.Originator, null, null, null, null,
                null, command.ActorUserId, command.At);
            var revision = TechnicalDocumentRevision.Create(Guid.NewGuid(),
                command.TenantId, command.ProjectId, document.Id,
                details.RevisionCode, localDate, DocumentRevisionPurpose.WorkInProgress,
                source.FileName,
                $"pmcs:chat-document:{command.MessageId:N}:{source.Id:N}:v{source.VersionNumber}",
                source.Sha256, null, command.ActorUserId, command.At);
            db.Documents.Add(document);
            db.DocumentRevisions.Add(revision);
            result = new ProjectMessageConversionResult(document.Id,
                "TechnicalDocument", document.Number);
            auditEvent = "ProjectChatConvertedToTechnicalDocument";
        }
        if (!await CanAsync(command, permission, cancellationToken) ||
            command.DestinationType == "TechnicalDocument" &&
            !await CanAsync(command, "technical.documents.create-revision", cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.technical_permission.denied", 403);
        await db.SaveChangesAsync(cancellationToken);
        await effects.WriteEventAsync(command.Connection, command.Transaction,
            new TransactionalEventBatch(
                new AuditEntry(command.TenantId, command.ProjectId, command.ActorUserId,
                    auditEvent, command.DestinationType == "RFI" ? "TechnicalRfi" : "TechnicalDocument",
                    result.DestinationId.ToString(), command.At,
                    new Dictionary<string, object?>
                    {
                        ["sourceMessageId"] = command.MessageId,
                        ["sourceRevision"] = command.MessageRevision,
                        ["documentIds"] = command.Documents.Select(item => item.Id).ToArray(),
                        ["documentHashes"] = command.Documents.Select(item => item.Sha256).ToArray()
                    }, command.CorrelationId),
                new OutboxEnvelope(Guid.NewGuid(), command.TenantId, command.ProjectId,
                    $"TechnicalOffice.{auditEvent}", 1, command.At,
                    JsonSerializer.Serialize(new { result.DestinationId, command.MessageId,
                        command.MessageRevision }, JsonOptions), command.CorrelationId)), cancellationToken);
        return result;
    }

    private Task<bool> CanAsync(ProjectMessageConversionCommand command,
        string permission, CancellationToken cancellationToken) =>
        permissions.HasProjectPermissionAsync(command.TenantId, command.ActorUserId,
            command.ProjectId, permission, cancellationToken);

    private static T Read<T>(JsonElement value)
    {
        try
        {
            return value.Deserialize<T>(JsonOptions) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new ProjectMessageConversionException("collaboration.conversion.details.invalid", 422);
        }
    }

    private sealed record RfiDetails(string Title, string? Question,
        string RequestedFrom, string Discipline, DateOnly? RequiredByDate,
        PotentialImpact PotentialImpact, bool IsBlocking, string? ProposedSolution);

    private sealed record DocumentDetails(string Title, TechnicalDocumentType Type,
        string Discipline, string? Originator, string RevisionCode);
}
