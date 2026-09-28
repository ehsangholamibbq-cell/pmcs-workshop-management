using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Collaboration.Endpoints;
using Pmcs.Modules.Collaboration.Migrations;
using Pmcs.Modules.Collaboration.Persistence;
using Pmcs.Modules.Collaboration.Services;
using Pmcs.Modules.Documents.Contracts;

namespace Pmcs.Modules.Collaboration;

public sealed class CollaborationModule : IModule
{
    public string Name => "Collaboration";

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleManifestSchemas.VersionOne,
        "collaboration.project",
        "Project Group Collaboration",
        "1.1.0",
        ["collaboration.project-room", "collaboration.messages"],
        ["identity-access.core", "projects.core", "documents.shared", "platform.foundation"],
        "collaboration",
        "1.1.0",
        false,
        [
            new PermissionManifest("collaboration.read", PermissionScope.Project,
                ManifestRiskClass.Medium, "Read the default room of an active project membership."),
            new PermissionManifest("collaboration.send", PermissionScope.Project,
                ManifestRiskClass.Medium, "Send a message to the default project room."),
            new PermissionManifest("collaboration.upload", PermissionScope.Project,
                ManifestRiskClass.Medium, "Associate a released project-chat document."),
            new PermissionManifest("collaboration.edit-own", PermissionScope.Project,
                ManifestRiskClass.Medium, "Edit the actor's own message with preserved history."),
            new PermissionManifest("collaboration.moderate", PermissionScope.Project,
                ManifestRiskClass.High, "Pin or redact a message with an audited reason."),
            new PermissionManifest("collaboration.convert", PermissionScope.Project,
                ManifestRiskClass.High, "Request explicit human-confirmed conversion by an owner module.")
        ],
        [],
        [
            new ToolManifest("collaboration.messages.list",
                "Returns permission-filtered project room context without document bytes.",
                """{"type":"object","properties":{"projectId":{"type":"string","format":"uuid"}},"required":["projectId"],"additionalProperties":false}""",
                """{"type":"object","required":["messages"],"additionalProperties":false}""",
                "collaboration.read", ManifestRiskClass.Medium, ToolAccessMode.ReadOnly)
        ],
        [new IntegrationEventManifest("collaboration.message.created", 1,
            IntegrationEventClassification.Confidential)]);

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddSingleton(CollaborationRuntimeOptions.Create(configuration));
        services.AddDbContext<CollaborationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectChatDocumentOwner, ProjectChatDocumentOwner>();
        services.AddSingleton<IDatabaseMigration, CollaborationInitialMigration>();
        services.AddSingleton<IDatabaseMigration, CollaborationInteractionMigration>();
        services.AddSingleton<IDatabaseMigration, CollaborationAttachmentMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapCollaborationEndpoints();
}
