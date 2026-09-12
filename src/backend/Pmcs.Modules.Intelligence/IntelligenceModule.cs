using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Intelligence.Endpoints;
using Pmcs.Modules.Intelligence.Migrations;
using Pmcs.Modules.Intelligence.Persistence;
using Pmcs.Modules.Intelligence.Services;

namespace Pmcs.Modules.Intelligence;

public sealed class IntelligenceModule : IModule
{
    public string Name => "Intelligence";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<IntelligenceDbContext>(options => options.UseNpgsql(connectionString));

        var endpointValue = configuration["OpenAI:ResponsesEndpoint"] ?? "https://api.openai.com/v1/responses";
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("OpenAI:ResponsesEndpoint must be an absolute HTTPS URI.");
        }

        var timeoutSeconds = int.TryParse(configuration["OpenAI:TimeoutSeconds"], out var parsedTimeout) &&
            parsedTimeout is >= 5 and <= 300
                ? parsedTimeout
                : 60;
        var settings = new OpenAiSettings(
            configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"],
            configuration["OpenAI:Model"] ?? configuration["OPENAI_MODEL"],
            endpoint,
            TimeSpan.FromSeconds(timeoutSeconds));
        services.AddSingleton(settings);
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IAdvisoryModelClient, OpenAiResponsesClient>();
        services.AddScoped<PermissionAwareContextAssembler>();
        services.AddSingleton<IDatabaseMigration, IntelligenceInitialMigration>();
        services.AddHostedService<AdvisoryGenerationWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapIntelligenceEndpoints();
}
