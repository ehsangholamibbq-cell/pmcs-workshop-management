using System.Text.Json;

namespace Pmcs.Modules.FieldOperations.Contracts;

public sealed record OfflineFieldOperationContext(
    Guid TenantId,
    Guid UserId,
    string DeviceId,
    string CorrelationId);

public sealed record OfflineFieldOperation(
    string OperationId,
    Guid ProjectId,
    string EntityType,
    Guid EntityId,
    string CommandType,
    long? BaseRevision,
    int PayloadSchemaVersion,
    DateTimeOffset CreatedAtDevice,
    JsonElement Payload);

public sealed record OfflineFieldOperationResult(
    string OperationId,
    OfflineFieldOperationStatus Status,
    Guid? EntityId,
    long? ServerRevision,
    string? Code = null,
    string? Message = null,
    bool WasReplay = false,
    string? ServerProjectionJson = null);

public enum OfflineFieldOperationStatus
{
    Applied = 1,
    Conflict = 2,
    Rejected = 3,
    Unsupported = 4
}

public interface IOfflineFieldOperationHandler
{
    Task<OfflineFieldOperationResult> HandleAsync(
        OfflineFieldOperationContext context,
        OfflineFieldOperation operation,
        CancellationToken cancellationToken = default);
}
