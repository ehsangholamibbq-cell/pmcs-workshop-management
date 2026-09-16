using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Endpoints;
using Pmcs.Modules.FieldOperations.Persistence;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class OfflineDailyReportOperationHandler(
    IProjectPermissionService permissionService,
    IProjectDirectory projectDirectory,
    IProjectLocationDirectory projectLocationDirectory,
    IMeasurementItemDirectory measurementItemDirectory,
    FieldOperationsDbContext dbContext,
    IClock clock,
    ITransactionalSideEffectWriter sideEffectWriter,
    IIdempotencyStore idempotencyStore) : IOfflineFieldOperationHandler
{
    private const string CaptureFactCommand = "CaptureDailyReportFact";
    private const string DailyReportEntity = "DailyReport";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task<OfflineFieldOperationResult> HandleAsync(
        OfflineFieldOperationContext context,
        OfflineFieldOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (operation.PayloadSchemaVersion != 1)
        {
            return Unsupported(operation, "sync.payload.version.unsupported", "Payload schema version is not supported.");
        }

        if (!string.Equals(operation.EntityType, DailyReportEntity, StringComparison.Ordinal) ||
            !string.Equals(operation.CommandType, CaptureFactCommand, StringComparison.Ordinal))
        {
            return Unsupported(operation, "sync.command.unsupported", "Command is not supported by this API version.");
        }

        var idempotencyKey = $"sync:{context.DeviceId}:{operation.OperationId}";
        const string operationName = "sync.daily-report.capture-fact";
        var requestHash = RequestHash.Create(JsonSerializer.Serialize(operation, SerializerOptions));

        try
        {
            var replay = await idempotencyStore.FindAsync(
                context.TenantId,
                idempotencyKey,
                operationName,
                requestHash,
                cancellationToken);
            if (replay is not null)
            {
                var stored = JsonSerializer.Deserialize<OfflineFieldOperationResult>(replay.ResponseBody, SerializerOptions);
                return stored is null
                    ? Rejected(operation, "sync.replay.invalid", "Stored operation response is invalid.")
                    : stored with { WasReplay = true };
            }

            if (!await permissionService.HasProjectPermissionAsync(
                    context.TenantId,
                    context.UserId,
                    operation.ProjectId,
                    "field.daily-reports.capture",
                    cancellationToken))
            {
                return await StoreTerminalAsync(
                    Rejected(operation, "permission.denied", "Permission was not valid when the operation reached the server."),
                    context.TenantId,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
            }

            var project = await projectDirectory.FindProfileAsync(
                context.TenantId,
                operation.ProjectId,
                cancellationToken);
            if (project is null)
            {
                return await StoreTerminalAsync(
                    Rejected(operation, "project.not_found", "Project was not found or is not accessible."),
                    context.TenantId,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
            }

            if (project.Status != ProjectStatus.Active)
            {
                return await StoreTerminalAsync(
                    Rejected(operation, "project.not_operational", "Project is not active for operational changes."),
                    context.TenantId,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
            }

            CaptureDailyReportFactPayload? payload;
            try
            {
                payload = operation.Payload.Deserialize<CaptureDailyReportFactPayload>(SerializerOptions);
            }
            catch (JsonException)
            {
                payload = null;
            }

            if (payload is null || payload.FactId == Guid.Empty)
            {
                return await StoreTerminalAsync(
                    Rejected(operation, "sync.payload.invalid", "Payload is missing required values."),
                    context.TenantId,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
            }

            if (payload.MeasurementItemId.HasValue)
            {
                var measurementError = await ValidateMeasurementItemAsync(
                    context,
                    operation,
                    payload,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
                if (measurementError is not null)
                {
                    return measurementError;
                }
            }

            if (!payload.LocationId.HasValue)
            {
                return await StoreTerminalAsync(
                    Rejected(operation, "project.location.required", "A project location is required."),
                    context.TenantId,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
            }

            var location = await projectLocationDirectory.FindActiveAsync(
                context.TenantId,
                operation.ProjectId,
                payload.LocationId.Value,
                cancellationToken);
            if (location is null)
            {
                return await StoreTerminalAsync(
                    Rejected(operation, "project.location.not_active", "Location is not active in this project."),
                    context.TenantId,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
            }

            var report = await dbContext.DailyReports
                .Include(item => item.Facts)
                .SingleOrDefaultAsync(
                    item => item.TenantId == context.TenantId && item.Id == operation.EntityId,
                    cancellationToken);

            if (report is not null &&
                (report.ProjectId != operation.ProjectId || report.ReportDate != payload.ReportDate))
            {
                return await StoreTerminalAsync(
                    Conflict(
                        operation,
                        report.Id,
                        report.Revision,
                        "daily_report.identity.conflict",
                        "Local report identity does not match server state.",
                        Projection(report)),
                    context.TenantId,
                    idempotencyKey,
                    operationName,
                    requestHash,
                    cancellationToken);
            }

            if (report is null)
            {
                var reportForDate = await dbContext.DailyReports.AsNoTracking().SingleOrDefaultAsync(
                    item => item.TenantId == context.TenantId &&
                        item.ProjectId == operation.ProjectId &&
                        item.ReportDate == payload.ReportDate &&
                        item.SupersedesReportId == null,
                    cancellationToken);
                if (reportForDate is not null)
                {
                    return await StoreTerminalAsync(
                        Conflict(
                            operation,
                            reportForDate.Id,
                            reportForDate.Revision,
                            "daily_report.date.duplicate",
                            "A daily report already exists for this project date.",
                            Projection(reportForDate)),
                        context.TenantId,
                        idempotencyKey,
                        operationName,
                        requestHash,
                        cancellationToken);
                }

                report = DailyReport.Create(
                    operation.EntityId,
                    context.TenantId,
                    operation.ProjectId,
                    payload.ReportDate,
                    payload.LocationName,
                    null,
                    context.UserId,
                    clock.UtcNow);
                dbContext.DailyReports.Add(report);
            }

            if (report.Facts.All(fact => fact.Id != payload.FactId))
            {
                report.AddFact(
                    payload.FactId,
                    new DailyFactInput(
                        payload.Kind,
                        payload.Description,
                        payload.Category,
                        location.Name,
                        payload.Quantity,
                        payload.Unit,
                        payload.ResourceCount,
                        payload.Hours,
                        payload.ImpactLevel,
                        payload.ReferenceCode,
                        payload.MeasurementItemId,
                        location.Id),
                    context.UserId,
                    clock.UtcNow);
            }

            var response = new OfflineFieldOperationResult(
                operation.OperationId,
                OfflineFieldOperationStatus.Applied,
                report.Id,
                report.Revision,
                ServerProjectionJson: Projection(report));
            var responseJson = JsonSerializer.Serialize(response, SerializerOptions);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await sideEffectWriter.WriteAsync(
                dbContext.Database.GetDbConnection(),
                transaction.GetDbTransaction(),
                new TransactionalSideEffectBatch(
                    new AuditEntry(
                        context.TenantId,
                        operation.ProjectId,
                        context.UserId,
                        "OfflineDailyReportFactApplied",
                        DailyReportEntity,
                        report.Id.ToString(),
                        clock.UtcNow,
                        new Dictionary<string, object?>
                        {
                            ["operationId"] = operation.OperationId,
                            ["deviceId"] = context.DeviceId,
                            ["revision"] = report.Revision,
                            ["createdAtDevice"] = operation.CreatedAtDevice
                        },
                        context.CorrelationId),
                    new OutboxEnvelope(
                        Guid.NewGuid(),
                        context.TenantId,
                        operation.ProjectId,
                        "FieldOperations.OfflineDailyReportFactApplied",
                        2,
                        clock.UtcNow,
                        responseJson,
                        context.CorrelationId),
                    new IdempotencyReceipt(
                        context.TenantId,
                        idempotencyKey,
                        operationName,
                        requestHash,
                        StatusCodes.Status200OK,
                        responseJson,
                        clock.UtcNow,
                        clock.UtcNow.AddDays(14))),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return response;
        }
        catch (IdempotencyKeyReusedException)
        {
            return Rejected(operation, "sync.operation.reused", "Operation id was reused with different content.");
        }
        catch (DomainRuleException exception)
        {
            return await StoreTerminalAsync(
                Rejected(operation, exception.Code, exception.Message),
                context.TenantId,
                idempotencyKey,
                operationName,
                requestHash,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return Conflict(
                operation,
                operation.EntityId,
                null,
                "sync.concurrency.conflict",
                "Server state changed while the operation was applied.",
                null);
        }
    }

    private async Task<OfflineFieldOperationResult?> ValidateMeasurementItemAsync(
        OfflineFieldOperationContext context,
        OfflineFieldOperation operation,
        CaptureDailyReportFactPayload payload,
        string idempotencyKey,
        string operationName,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (payload.Kind != DailyFactKind.WorkProgress)
        {
            return await StoreTerminalAsync(
                Rejected(operation, "daily_fact.measurement_item.kind.invalid", "Only progress facts can reference a measurement item."),
                context.TenantId,
                idempotencyKey,
                operationName,
                requestHash,
                cancellationToken);
        }

        var validation = await measurementItemDirectory.ValidateAsync(
            context.TenantId,
            operation.ProjectId,
            payload.MeasurementItemId!.Value,
            payload.Unit,
            cancellationToken);
        return validation.IsValid
            ? null
            : await StoreTerminalAsync(
                Rejected(
                    operation,
                    validation.ErrorCode ?? "measurement_item.invalid",
                    "Measurement item is not available for this progress fact."),
                context.TenantId,
                idempotencyKey,
                operationName,
                requestHash,
                cancellationToken);
    }

    private async Task<OfflineFieldOperationResult> StoreTerminalAsync(
        OfflineFieldOperationResult response,
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await idempotencyStore.StoreAsync(
            tenantId,
            key,
            operation,
            requestHash,
            StatusCodes.Status200OK,
            JsonSerializer.Serialize(response, SerializerOptions),
            cancellationToken);
        return response;
    }

    private static OfflineFieldOperationResult Rejected(OfflineFieldOperation operation, string code, string message) =>
        new(operation.OperationId, OfflineFieldOperationStatus.Rejected, operation.EntityId, null, code, message);

    private static OfflineFieldOperationResult Unsupported(OfflineFieldOperation operation, string code, string message) =>
        new(operation.OperationId, OfflineFieldOperationStatus.Unsupported, operation.EntityId, null, code, message);

    private static OfflineFieldOperationResult Conflict(
        OfflineFieldOperation operation,
        Guid entityId,
        long? revision,
        string code,
        string message,
        string? projection) =>
        new(
            operation.OperationId,
            OfflineFieldOperationStatus.Conflict,
            entityId,
            revision,
            code,
            message,
            ServerProjectionJson: projection);

    private static string Projection(DailyReport report) => JsonSerializer.Serialize(new
    {
        report.Id,
        report.ProjectId,
        report.ReportDate,
        report.Status,
        report.Revision,
        report.LastModifiedAt
    }, SerializerOptions);

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
