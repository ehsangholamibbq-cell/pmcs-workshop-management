using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Commercial.Services;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Commercial.Endpoints;

internal static class PartyEndpoints
{
    public static void MapPartyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/parties", ListAsync);
        group.MapPost("/parties", CreateAsync);
        group.MapPut("/parties/{partyId:guid}", AmendAsync);
        group.MapPost("/parties/{partyId:guid}/status", SetStatusAsync);
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CommercialEndpointSupport.HasPermissionAsync(
                permissionService, actor, projectId, "commercial.parties.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var parties = await dbContext.Parties.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderBy(item => item.Code)
            .Take(300)
            .ToListAsync(cancellationToken);
        return Results.Ok(parties.Select(PartyResponse.From).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        Guid projectId,
        CreatePartyRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CommercialEndpointSupport.HasPermissionAsync(
                permissionService, actor, projectId, "commercial.parties.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (!PartyRegisterAvailable(project))
        {
            return Results.UnprocessableEntity(new { code = "commercial.not_active" });
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.parties.create", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var party = Party.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.Code,
            request.LegalName,
            request.Type,
            request.NationalId,
            request.ContactName,
            request.Phone,
            actor.UserId,
            clock.UtcNow);
        if (await dbContext.Parties.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId && item.Code == party.Code,
                cancellationToken))
        {
            return Results.Conflict(new { code = "commercial.party.code.duplicate" });
        }

        dbContext.Parties.Add(party);
        var response = PartyResponse.From(party);
        await CommercialEndpointSupport.PersistAsync(
            dbContext, stateFactory, httpContext, actor, projectId, party.Id, "Party", "PartyCreated",
            Audit(party), response, command!, StatusCodes.Status201Created,
            sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/parties/{party.Id}", response);
    }

    private static async Task<IResult> AmendAsync(
        Guid projectId,
        Guid partyId,
        AmendPartyRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CommercialEndpointSupport.HasPermissionAsync(
                permissionService, actor, projectId, "commercial.parties.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.parties.amend", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var party = await FindAsync(dbContext, actor.TenantId, projectId, partyId, cancellationToken);
        if (party is null)
        {
            return Results.NotFound();
        }

        if (party.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.party.revision.conflict", party.Revision);
        }

        party.Amend(
            request.BaseRevision,
            request.LegalName,
            request.Type,
            request.NationalId,
            request.ContactName,
            request.Phone,
            clock.UtcNow);
        var response = PartyResponse.From(party);
        await CommercialEndpointSupport.PersistAsync(
            dbContext, stateFactory, httpContext, actor, projectId, party.Id, "Party", "PartyAmended",
            Audit(party), response, command!, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> SetStatusAsync(
        Guid projectId,
        Guid partyId,
        SetPartyStatusRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CommercialEndpointSupport.HasPermissionAsync(
                permissionService, actor, projectId, "commercial.parties.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.parties.status", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var party = await FindAsync(dbContext, actor.TenantId, projectId, partyId, cancellationToken);
        if (party is null)
        {
            return Results.NotFound();
        }

        if (party.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.party.revision.conflict", party.Revision);
        }

        party.SetStatus(request.BaseRevision, request.Status, clock.UtcNow);
        var response = PartyResponse.From(party);
        await CommercialEndpointSupport.PersistAsync(
            dbContext, stateFactory, httpContext, actor, projectId, party.Id, "Party", "PartyStatusChanged",
            Audit(party), response, command!, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<Party?> FindAsync(
        CommercialDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid partyId,
        CancellationToken cancellationToken) =>
        dbContext.Parties.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == partyId,
            cancellationToken);

    private static bool PartyRegisterAvailable(ProjectControlProfile project) =>
        project.Contract is ProjectFeatureState.Active or ProjectFeatureState.SetupRequired ||
        project.Procurement is ProjectFeatureState.Active or ProjectFeatureState.SetupRequired;

    private static Dictionary<string, object?> Audit(Party item) =>
        new Dictionary<string, object?>
        {
            ["code"] = item.Code,
            ["type"] = item.Type.ToString(),
            ["status"] = item.Status.ToString(),
            ["revision"] = item.Revision
        };
}
