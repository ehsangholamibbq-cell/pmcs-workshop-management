using System.Data.Common;
using System.Text.Json;

namespace Pmcs.BuildingBlocks.Application;

// Owner modules implement this command. The caller owns the transaction and
// lineage; the destination owns official validation, permissions and persistence.
public interface IProjectMessageConversionDestination
{
    bool Supports(string destinationType);

    Task<ProjectMessageConversionResult> ExecuteAsync(
        ProjectMessageConversionCommand command, CancellationToken cancellationToken = default);
}

public sealed record ProjectMessageConversionCommand(
    Guid TenantId, Guid ProjectId, Guid ActorUserId, Guid MessageId,
    long MessageRevision, string MessageBody, string DestinationType,
    Guid DestinationId, JsonElement Details,
    IReadOnlyList<ProjectMessageDocumentReference> Documents,
    DateTimeOffset At, string CorrelationId,
    DbConnection Connection, DbTransaction Transaction);

public sealed record ProjectMessageDocumentReference(Guid Id, string Sha256,
    int VersionNumber, string FileName, string ContentType, long SizeBytes);

public sealed record ProjectMessageConversionResult(Guid DestinationId,
    string DestinationType, string Reference);

public sealed class ProjectMessageConversionException(string code, int statusCode)
    : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
