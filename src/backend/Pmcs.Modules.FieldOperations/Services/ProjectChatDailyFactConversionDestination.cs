using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class ProjectChatDailyFactConversionDestination(
    IProjectPermissionService permissions, IProjectLocationDirectory locations,
    ITransactionalSideEffectWriter effects) : IProjectMessageConversionDestination
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public bool Supports(string destinationType) => destinationType == "DailyFact";

    public async Task<ProjectMessageConversionResult> ExecuteAsync(
        ProjectMessageConversionCommand command, CancellationToken cancellationToken = default)
    {
        if (!await CanAsync(command, cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.fact_permission.denied", 403);
        if (command.Documents.Count != 0)
            throw new ProjectMessageConversionException("collaboration.conversion.fact.documents_not_supported", 422);
        DailyFactDetails details;
        try { details = command.Details.Deserialize<DailyFactDetails>(JsonOptions) ?? throw new JsonException(); }
        catch (JsonException)
        {
            throw new ProjectMessageConversionException("collaboration.conversion.details.invalid", 422);
        }
        if (details.ReportId == Guid.Empty || details.LocationId == Guid.Empty ||
            details.BaseReportRevision < 1)
            throw new ProjectMessageConversionException("collaboration.conversion.fact.target.invalid", 422);
        var location = await locations.FindActiveAsync(command.TenantId,
            command.ProjectId, details.LocationId, cancellationToken);
        if (location is null)
            throw new ProjectMessageConversionException("collaboration.conversion.fact.location.not_active", 422);
        await using var db = new FieldOperationsDbContext(new DbContextOptionsBuilder<FieldOperationsDbContext>()
            .UseNpgsql(command.Connection).Options);
        await db.Database.UseTransactionAsync(command.Transaction, cancellationToken);
        var report = await db.DailyReports.Include(item => item.Facts).SingleOrDefaultAsync(item =>
            item.TenantId == command.TenantId && item.ProjectId == command.ProjectId &&
            item.Id == details.ReportId, cancellationToken);
        if (report is null)
            throw new ProjectMessageConversionException("collaboration.conversion.fact.report.not_found", 404);
        if (report.Status != DailyReportStatus.Draft ||
            report.Revision != details.BaseReportRevision)
            throw new ProjectMessageConversionException("collaboration.conversion.fact.report.conflict", 409);
        var fact = report.AddFact(command.DestinationId,
            new DailyFactInput(details.Kind, details.Description ?? command.MessageBody,
                details.Category, location.Name, details.Quantity, details.Unit,
                details.ResourceCount, details.Hours, details.ImpactLevel,
                $"chat:{command.MessageId:N}:v{command.MessageRevision}",
                null, location.Id), command.ActorUserId, command.At);
        if (!await CanAsync(command, cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.fact_permission.denied", 403);
        await db.SaveChangesAsync(cancellationToken);
        await effects.WriteEventAsync(command.Connection, command.Transaction,
            new TransactionalEventBatch(
                new AuditEntry(command.TenantId, command.ProjectId, command.ActorUserId,
                    "ProjectChatConvertedToDailyFact", "DailyReportFact", fact.Id.ToString(),
                    command.At, new Dictionary<string, object?>
                    {
                        ["sourceMessageId"] = command.MessageId,
                        ["sourceRevision"] = command.MessageRevision,
                        ["dailyReportId"] = report.Id
                    }, command.CorrelationId),
                new OutboxEnvelope(Guid.NewGuid(), command.TenantId, command.ProjectId,
                    "FieldOperations.ProjectChatConvertedToDailyFact", 1, command.At,
                    JsonSerializer.Serialize(new { fact.Id, reportId = report.Id,
                        command.MessageId, command.MessageRevision }, JsonOptions),
                    command.CorrelationId)), cancellationToken);
        return new ProjectMessageConversionResult(fact.Id, "DailyFact",
            $"{report.Id:N}/{fact.Id:N}");
    }

    private Task<bool> CanAsync(ProjectMessageConversionCommand command,
        CancellationToken cancellationToken) =>
        permissions.HasProjectPermissionAsync(command.TenantId, command.ActorUserId,
            command.ProjectId, "field.daily-reports.capture", cancellationToken);

    private sealed record DailyFactDetails(Guid ReportId, long BaseReportRevision,
        DailyFactKind Kind, string? Description, Guid LocationId, string? Category,
        decimal? Quantity, string? Unit, int? ResourceCount, decimal? Hours,
        DailyImpactLevel? ImpactLevel);
}
