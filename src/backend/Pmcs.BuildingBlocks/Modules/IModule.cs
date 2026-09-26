using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Pmcs.BuildingBlocks.Modules;

public interface IModule
{
    string Name { get; }

    ModuleDescriptor Descriptor => ModuleDescriptor.Legacy(Name);

    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
