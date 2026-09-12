using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Pmcs.Modules.Commercial.Endpoints;

internal static partial class SupplyEndpoints
{
    public static void MapSupplyEndpoints(this RouteGroupBuilder commercial)
    {
        var group = commercial.MapGroup("/supply");
        group.MapGet("/state", GetStateAsync);
        MapCatalogEndpoints(group);
        MapReceiptEndpoints(group);
        MapInventoryEndpoints(group);
    }
}
