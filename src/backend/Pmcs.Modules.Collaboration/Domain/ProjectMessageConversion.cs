using System.Text.Json;
using Pmcs.BuildingBlocks.Application;

namespace Pmcs.Modules.Collaboration.Domain;

public sealed class ProjectMessageConversion
{
    private ProjectMessageConversion() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MessageId { get; private set; }
    public long MessageRevision { get; private set; }
    public string DestinationType { get; private set; } = string.Empty;
    public Guid DestinationId { get; private set; }
    public string DestinationReference { get; private set; } = string.Empty;
    public string DocumentReferencesJson { get; private set; } = "[]";
    public string RequestHash { get; private set; } = string.Empty;
    public Guid ConfirmedBy { get; private set; }
    public DateTimeOffset ConfirmedAt { get; private set; }

    public static ProjectMessageConversion Create(ProjectMessage message,
        ProjectMessageConversionResult result,
        IReadOnlyList<ProjectMessageDocumentReference> documents,
        string requestHash, Guid actorUserId, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(), TenantId = message.TenantId,
        ProjectId = message.ProjectId, MessageId = message.Id,
        MessageRevision = message.Revision,
        DestinationType = result.DestinationType,
        DestinationId = result.DestinationId,
        DestinationReference = result.Reference,
        DocumentReferencesJson = JsonSerializer.Serialize(documents),
        RequestHash = requestHash, ConfirmedBy = actorUserId, ConfirmedAt = at
    };
}
