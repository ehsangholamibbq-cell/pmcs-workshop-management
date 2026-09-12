using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Commercial.Services;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Commercial.Endpoints;

internal static class CommercialQueryEndpoints
{
    public static async Task<IResult> GetStateAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ICommercialStateSource stateSource,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CommercialEndpointSupport.HasPermissionAsync(
                permissionService, actor, projectId, "commercial-state.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var state = await stateSource.GetCurrentAsync(actor.TenantId, projectId, cancellationToken);
        return state is null
            ? Results.NotFound(new { code = "project.not_found" })
            : Results.Ok(CommercialStateResponse.From(state));
    }

    public static async Task<IResult> GetPortfolioAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        CommercialDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var scope = await permissionService.GetProjectScopeAsync(
            actor.TenantId,
            actor.UserId,
            "commercial-state.read",
            cancellationToken);
        var profiles = await projectDirectory.ListProfilesAsync(
            actor.TenantId,
            scope.AllProjects ? null : scope.ProjectIds.ToArray(),
            cancellationToken);
        if (profiles.Count == 0)
        {
            return Results.Ok(Array.Empty<PortfolioCommercialStateResponse>());
        }

        var projectIds = profiles.Select(project => project.Id).ToArray();
        var snapshots = await dbContext.CommercialStateSnapshots.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && projectIds.Contains(item.ProjectId))
            .OrderByDescending(item => item.CalculatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
        var latestByProject = snapshots
            .GroupBy(item => item.ProjectId)
            .ToDictionary(group => group.Key, group => group.First());
        var response = profiles.Select(project =>
        {
            CommercialStateRecord state;
            if (latestByProject.TryGetValue(project.Id, out var snapshot))
            {
                state = CommercialStateSource.From(snapshot);
            }
            else
            {
                var calculation = CommercialStateCalculator.Calculate(
                    project,
                    [],
                    [],
                    [],
                    [],
                    0,
                    CommercialStateFactory.ResolveLocalDate(clock.UtcNow, project.TimeZone),
                    clock.UtcNow);
                state = CommercialStateSource.From(calculation);
            }

            return new PortfolioCommercialStateResponse(
                project.Id,
                project.Code,
                project.Name,
                CommercialStateResponse.From(state));
        }).ToArray();
        return Results.Ok(response);
    }
}
