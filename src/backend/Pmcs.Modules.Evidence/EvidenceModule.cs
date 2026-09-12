using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Evidence.Endpoints;
using Pmcs.Modules.Evidence.Migrations;
using Pmcs.Modules.Evidence.Persistence;
using Pmcs.Modules.Evidence.Storage;

namespace Pmcs.Modules.Evidence;

public sealed class EvidenceModule : IModule
{
    public string Name => "Evidence";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<EvidenceDbContext>(options => options.UseNpgsql(connectionString));
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddSingleton<IDatabaseMigration, EvidenceInitialMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapEvidenceEndpoints();
}
