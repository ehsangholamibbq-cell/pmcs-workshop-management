using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.QualityAssurance.Contracts;
using Pmcs.Modules.QualityAssurance.Endpoints;
using Pmcs.Modules.QualityAssurance.Services;

namespace Pmcs.Modules.QualityAssurance;

public sealed class QualityAssuranceModule : IModule
{
    public string Name => "QualityAssurance";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<IQaDiagnosticsService, QaDiagnosticsService>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<QualityAssuranceRuntimeOptions>();
        if (options.Enabled)
        {
            endpoints.MapQualityAssuranceEndpoints();
        }
    }
}
