using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Pmcs.Modules.Commercial.Endpoints;

internal static class CommercialEndpoints
{
    public static void MapCommercialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var commercial = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/commercial")
            .WithTags("Contract & Procurement Control Lite");
        commercial.MapPartyEndpoints();
        commercial.MapContractEndpoints();
        commercial.MapProcurementEndpoints();
        commercial.MapSupplyEndpoints();
        commercial.MapGet("/state", CommercialQueryEndpoints.GetStateAsync);

        endpoints.MapGet("/api/v1/portfolio/commercial-state", CommercialQueryEndpoints.GetPortfolioAsync)
            .WithTags("Portfolio Commercial Control");
    }
}
