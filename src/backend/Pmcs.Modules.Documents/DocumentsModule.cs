using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Endpoints;
using Pmcs.Modules.Documents.Migrations;
using Pmcs.Modules.Documents.Persistence;
using Pmcs.Modules.Documents.Scanning;
using Pmcs.Modules.Documents.Services;
using Pmcs.Modules.Documents.Storage;

namespace Pmcs.Modules.Documents;

public sealed class DocumentsModule : IModule
{
    public string Name => "Documents";

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleManifestSchemas.VersionOne,
        "documents.shared",
        "Shared Documents and Attachments",
        "1.1.0",
        [
            "documents.shared-assets",
            "documents.upload-sessions",
            "documents.quarantine",
            "documents.owner-contract"
        ],
        ["platform.foundation"],
        "documents",
        "1.1.0",
        false,
        [
            new PermissionManifest(
                "documents.read",
                PermissionScope.Project,
                ManifestRiskClass.Medium,
                "Read released documents in an authorized owner or project scope."),
            new PermissionManifest(
                "documents.upload",
                PermissionScope.Project,
                ManifestRiskClass.Medium,
                "Create resumable upload sessions in an authorized owner or project scope."),
            new PermissionManifest(
                "documents.classify",
                PermissionScope.Project,
                ManifestRiskClass.High,
                "Change document classification, retention and legal-hold metadata."),
            new PermissionManifest(
                "documents.quarantine.release",
                PermissionScope.Tenant,
                ManifestRiskClass.Critical,
                "Release a clean quarantined document for permission-aware download.")
        ],
        [],
        [],
        [
            new IntegrationEventManifest(
                "documents.asset.released",
                1,
                IntegrationEventClassification.Confidential)
        ]);

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<DocumentsDbContext>(options => options.UseNpgsql(connectionString));
        services.Configure<DocumentObjectStorageOptions>(
            configuration.GetSection(DocumentObjectStorageOptions.SectionName));
        services.AddSingleton<IDocumentObjectStorage, S3DocumentObjectStorage>();
        services.AddSingleton<IContentScanner, DeterministicContentScanner>();
        services.AddScoped<ISharedDocumentDirectory, SharedDocumentDirectory>();
        services.AddSingleton<IDatabaseMigration, DocumentsInitialMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapDocumentEndpoints();
}
