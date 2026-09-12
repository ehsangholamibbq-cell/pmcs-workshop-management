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

internal static class ContractEndpoints
{
    public static void MapContractEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/contracts", ListContractsAsync);
        group.MapPost("/contracts", CreateContractAsync);
        group.MapPut("/contracts/{contractId:guid}", AmendContractAsync);
        group.MapPost("/contracts/{contractId:guid}/submit", SubmitContractAsync);
        group.MapPost("/contracts/{contractId:guid}/activate", ActivateContractAsync);
        group.MapPost("/contracts/{contractId:guid}/return", ReturnContractAsync);
        group.MapPost("/contracts/{contractId:guid}/close", CloseContractAsync);

        group.MapGet("/contract-amendments", ListAmendmentsAsync);
        group.MapPost("/contract-amendments", CreateAmendmentAsync);
        group.MapPut("/contract-amendments/{amendmentId:guid}", AmendAmendmentAsync);
        group.MapPost("/contract-amendments/{amendmentId:guid}/submit", SubmitAmendmentAsync);
        group.MapPost("/contract-amendments/{amendmentId:guid}/approve", ApproveAmendmentAsync);
        group.MapPost("/contract-amendments/{amendmentId:guid}/return", ReturnAmendmentAsync);
    }

    private static async Task<IResult> ListContractsAsync(
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

        if (!await CanAsync(permissionService, actor, projectId, "contracts.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var items = await dbContext.Contracts.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.ChangedAt)
            .Take(300)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(ProjectContractResponse.From).ToArray());
    }

    private static async Task<IResult> CreateContractAsync(
        Guid projectId,
        CreateProjectContractRequest request,
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

        if (!await CanAsync(permissionService, actor, projectId, "contracts.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        var projectError = ValidateProject(project, contractRequired: true);
        if (projectError is not null)
        {
            return projectError;
        }

        if (!await ActivePartyExistsAsync(dbContext, actor.TenantId, projectId, request.PartyId, cancellationToken))
        {
            return Results.UnprocessableEntity(new { code = "commercial.party.not_active" });
        }

        var currencyError = ValidateCurrency(project!, request.CurrencyCode);
        if (currencyError is not null)
        {
            return currencyError;
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.contracts.create", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var contract = ProjectContract.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.PartyId,
            request.Number,
            request.Title,
            request.Type,
            request.OriginalApprovedAmount,
            project!.BaseCurrencyCode,
            request.StartDate,
            request.EndDate,
            request.Notes,
            actor.UserId,
            clock.UtcNow);
        if (await dbContext.Contracts.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId && item.Number == contract.Number,
                cancellationToken))
        {
            return Results.Conflict(new { code = "commercial.contract.number.duplicate" });
        }

        dbContext.Contracts.Add(contract);
        var response = ProjectContractResponse.From(contract);
        await PersistContractAsync(
            dbContext, stateFactory, httpContext, actor, contract, "ContractCreated", response, command!,
            StatusCodes.Status201Created, sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/contracts/{contract.Id}", response);
    }

    private static async Task<IResult> AmendContractAsync(
        Guid projectId,
        Guid contractId,
        AmendProjectContractRequest request,
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

        if (!await CanAsync(permissionService, actor, projectId, "contracts.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        var projectError = ValidateProject(project, contractRequired: true);
        if (projectError is not null)
        {
            return projectError;
        }

        if (!await ActivePartyExistsAsync(dbContext, actor.TenantId, projectId, request.PartyId, cancellationToken))
        {
            return Results.UnprocessableEntity(new { code = "commercial.party.not_active" });
        }

        var currencyError = ValidateCurrency(project!, request.CurrencyCode);
        if (currencyError is not null)
        {
            return currencyError;
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.contracts.amend", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var contract = await FindContractAsync(dbContext, actor.TenantId, projectId, contractId, cancellationToken);
        if (contract is null)
        {
            return Results.NotFound();
        }

        if (contract.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.contract.revision.conflict", contract.Revision);
        }

        contract.Amend(
            request.BaseRevision,
            request.PartyId,
            request.Number,
            request.Title,
            request.Type,
            request.OriginalApprovedAmount,
            project!.BaseCurrencyCode,
            request.StartDate,
            request.EndDate,
            request.Notes,
            clock.UtcNow);
        var response = ProjectContractResponse.From(contract);
        await PersistContractAsync(
            dbContext, stateFactory, httpContext, actor, contract, "ContractAmended", response, command!,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> SubmitContractAsync(
        Guid projectId,
        Guid contractId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionContractAsync(
            projectId, contractId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "submit", "contracts.submit", cancellationToken);

    private static Task<IResult> ActivateContractAsync(
        Guid projectId,
        Guid contractId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionContractAsync(
            projectId, contractId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "activate", "contracts.review", cancellationToken);

    private static Task<IResult> ReturnContractAsync(
        Guid projectId,
        Guid contractId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionContractAsync(
            projectId, contractId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "return", "contracts.review", cancellationToken);

    private static Task<IResult> CloseContractAsync(
        Guid projectId,
        Guid contractId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionContractAsync(
            projectId, contractId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "close", "contracts.review", cancellationToken);

    private static async Task<IResult> TransitionContractAsync(
        Guid projectId,
        Guid contractId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        string action,
        string permission,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CanAsync(permissionService, actor, projectId, permission, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var operation = $"commercial.contracts.{action}";
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, operation, request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var contract = await FindContractAsync(dbContext, actor.TenantId, projectId, contractId, cancellationToken);
        if (contract is null)
        {
            return Results.NotFound();
        }

        if (contract.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.contract.revision.conflict", contract.Revision);
        }

        var eventName = action switch
        {
            "submit" => Apply(() => contract.Submit(request.BaseRevision, clock.UtcNow), "ContractSubmitted"),
            "activate" => Apply(() => contract.Activate(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "ContractActivated"),
            "return" => Apply(() => contract.Return(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "ContractReturned"),
            "close" => Apply(() => contract.Close(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "ContractClosed"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported contract action.")
        };
        var response = ProjectContractResponse.From(contract);
        await PersistContractAsync(
            dbContext, stateFactory, httpContext, actor, contract, eventName, response, command!,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListAmendmentsAsync(
        Guid projectId,
        Guid? contractId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CanAsync(permissionService, actor, projectId, "contracts.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = dbContext.ContractAmendments.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (contractId.HasValue)
        {
            query = query.Where(item => item.ContractId == contractId);
        }

        var items = await query.OrderByDescending(item => item.ChangedAt).Take(300).ToListAsync(cancellationToken);
        return Results.Ok(items.Select(ContractAmendmentResponse.From).ToArray());
    }

    private static async Task<IResult> CreateAmendmentAsync(
        Guid projectId,
        CreateContractAmendmentRequest request,
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

        if (!await CanAsync(permissionService, actor, projectId, "contracts.amendments.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        var projectError = ValidateProject(project, contractRequired: true);
        if (projectError is not null)
        {
            return projectError;
        }

        var contract = await FindContractAsync(dbContext, actor.TenantId, projectId, request.ContractId, cancellationToken);
        if (contract is null)
        {
            return Results.UnprocessableEntity(new { code = "commercial.contract.not_found" });
        }

        if (contract.Status != ProjectContractStatus.Active)
        {
            return Results.UnprocessableEntity(new { code = "commercial.contract.not_active" });
        }

        var currencyError = ValidateCurrency(project!, request.CurrencyCode);
        if (currencyError is not null)
        {
            return currencyError;
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.amendments.create", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var amendment = ContractAmendment.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.ContractId,
            request.Number,
            request.Title,
            request.Type,
            request.AmountDelta,
            project!.BaseCurrencyCode,
            request.ExtensionDays,
            request.Notes,
            actor.UserId,
            clock.UtcNow);
        if (await dbContext.ContractAmendments.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.ContractId == request.ContractId && item.Number == amendment.Number,
                cancellationToken))
        {
            return Results.Conflict(new { code = "commercial.amendment.number.duplicate" });
        }

        dbContext.ContractAmendments.Add(amendment);
        var response = ContractAmendmentResponse.From(amendment);
        await PersistAmendmentAsync(
            dbContext, stateFactory, httpContext, actor, amendment, "ContractAmendmentCreated", response, command!,
            StatusCodes.Status201Created, sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/contract-amendments/{amendment.Id}", response);
    }

    private static async Task<IResult> AmendAmendmentAsync(
        Guid projectId,
        Guid amendmentId,
        AmendContractAmendmentRequest request,
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

        if (!await CanAsync(permissionService, actor, projectId, "contracts.amendments.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var currencyError = ValidateCurrency(project, request.CurrencyCode);
        if (currencyError is not null)
        {
            return currencyError;
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.amendments.amend", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var amendment = await FindAmendmentAsync(dbContext, actor.TenantId, projectId, amendmentId, cancellationToken);
        if (amendment is null)
        {
            return Results.NotFound();
        }

        if (amendment.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.amendment.revision.conflict", amendment.Revision);
        }

        amendment.Amend(
            request.BaseRevision,
            request.Number,
            request.Title,
            request.Type,
            request.AmountDelta,
            project.BaseCurrencyCode,
            request.ExtensionDays,
            request.Notes,
            clock.UtcNow);
        var response = ContractAmendmentResponse.From(amendment);
        await PersistAmendmentAsync(
            dbContext, stateFactory, httpContext, actor, amendment, "ContractAmendmentAmended", response, command!,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> SubmitAmendmentAsync(
        Guid projectId,
        Guid amendmentId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionAmendmentAsync(
            projectId, amendmentId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "submit", "contracts.amendments.submit", cancellationToken);

    private static Task<IResult> ApproveAmendmentAsync(
        Guid projectId,
        Guid amendmentId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionAmendmentAsync(
            projectId, amendmentId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "approve", "contracts.amendments.review", cancellationToken);

    private static Task<IResult> ReturnAmendmentAsync(
        Guid projectId,
        Guid amendmentId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionAmendmentAsync(
            projectId, amendmentId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "return", "contracts.amendments.review", cancellationToken);

    private static async Task<IResult> TransitionAmendmentAsync(
        Guid projectId,
        Guid amendmentId,
        CommercialTransitionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        string action,
        string permission,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CanAsync(permissionService, actor, projectId, permission, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var operation = $"commercial.amendments.{action}";
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, operation, request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var amendment = await FindAmendmentAsync(dbContext, actor.TenantId, projectId, amendmentId, cancellationToken);
        if (amendment is null)
        {
            return Results.NotFound();
        }

        if (amendment.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.amendment.revision.conflict", amendment.Revision);
        }

        if (action == "approve")
        {
            var ceilingError = await ValidateApprovedCeilingAsync(dbContext, amendment, cancellationToken);
            if (ceilingError is not null)
            {
                return ceilingError;
            }
        }

        var eventName = action switch
        {
            "submit" => Apply(() => amendment.Submit(request.BaseRevision, clock.UtcNow), "ContractAmendmentSubmitted"),
            "approve" => Apply(() => amendment.Approve(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "ContractAmendmentApproved"),
            "return" => Apply(() => amendment.Return(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "ContractAmendmentReturned"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported amendment action.")
        };
        var response = ContractAmendmentResponse.From(amendment);
        await PersistAmendmentAsync(
            dbContext, stateFactory, httpContext, actor, amendment, eventName, response, command!,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult?> ValidateApprovedCeilingAsync(
        CommercialDbContext dbContext,
        ContractAmendment amendment,
        CancellationToken cancellationToken)
    {
        if (!amendment.AmountDelta.HasValue)
        {
            return null;
        }

        var contract = await dbContext.Contracts.AsNoTracking().SingleAsync(
            item => item.Id == amendment.ContractId && item.TenantId == amendment.TenantId && item.ProjectId == amendment.ProjectId,
            cancellationToken);
        if (!contract.OriginalApprovedAmount.HasValue)
        {
            return null;
        }

        var approvedDelta = await dbContext.ContractAmendments.AsNoTracking()
            .Where(item => item.TenantId == amendment.TenantId && item.ProjectId == amendment.ProjectId &&
                item.ContractId == amendment.ContractId && item.Status == ContractAmendmentStatus.Approved)
            .SumAsync(item => item.AmountDelta ?? 0m, cancellationToken);
        return contract.OriginalApprovedAmount.Value + approvedDelta + amendment.AmountDelta.Value < 0
            ? Results.UnprocessableEntity(new { code = "commercial.contract.ceiling.negative" })
            : null;
    }

    private static Task<ProjectContract?> FindContractAsync(
        CommercialDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid contractId,
        CancellationToken cancellationToken) =>
        dbContext.Contracts.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == contractId,
            cancellationToken);

    private static Task<ContractAmendment?> FindAmendmentAsync(
        CommercialDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid amendmentId,
        CancellationToken cancellationToken) =>
        dbContext.ContractAmendments.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == amendmentId,
            cancellationToken);

    private static Task<bool> ActivePartyExistsAsync(
        CommercialDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid partyId,
        CancellationToken cancellationToken) =>
        dbContext.Parties.AsNoTracking().AnyAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.Id == partyId && item.Status == PartyStatus.Active,
            cancellationToken);

    private static IResult? ValidateProject(ProjectControlProfile? project, bool contractRequired)
    {
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        return contractRequired && project.Contract is not ProjectFeatureState.Active and not ProjectFeatureState.SetupRequired
            ? Results.UnprocessableEntity(new { code = "commercial.contract.not_active", state = project.Contract })
            : null;
    }

    private static IResult? ValidateCurrency(ProjectControlProfile project, string? requestedCurrency)
    {
        if (string.IsNullOrWhiteSpace(requestedCurrency) ||
            string.Equals(requestedCurrency.Trim(), project.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Results.UnprocessableEntity(new
        {
            code = "commercial.currency.outside_base",
            projectCurrency = project.BaseCurrencyCode
        });
    }

    private static Task<bool> CanAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        CommercialEndpointSupport.HasPermissionAsync(permissionService, actor, projectId, permission, cancellationToken);

    private static string Apply(Action action, string eventName)
    {
        action();
        return eventName;
    }

    private static Task PersistContractAsync(
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        HttpContext httpContext,
        ICurrentActor actor,
        ProjectContract item,
        string eventName,
        ProjectContractResponse response,
        CommandIdentity command,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken) =>
        CommercialEndpointSupport.PersistAsync(
            dbContext, stateFactory, httpContext, actor, item.ProjectId, item.Id, "ProjectContract", eventName,
            new Dictionary<string, object?>
            {
                ["number"] = item.Number,
                ["partyId"] = item.PartyId,
                ["status"] = item.Status.ToString(),
                ["originalApprovedAmount"] = item.OriginalApprovedAmount,
                ["revision"] = item.Revision
            },
            response, command, statusCode, sideEffectWriter, clock, cancellationToken);

    private static Task PersistAmendmentAsync(
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        HttpContext httpContext,
        ICurrentActor actor,
        ContractAmendment item,
        string eventName,
        ContractAmendmentResponse response,
        CommandIdentity command,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken) =>
        CommercialEndpointSupport.PersistAsync(
            dbContext, stateFactory, httpContext, actor, item.ProjectId, item.Id, "ContractAmendment", eventName,
            new Dictionary<string, object?>
            {
                ["number"] = item.Number,
                ["contractId"] = item.ContractId,
                ["status"] = item.Status.ToString(),
                ["amountDelta"] = item.AmountDelta,
                ["extensionDays"] = item.ExtensionDays,
                ["revision"] = item.Revision
            },
            response, command, statusCode, sideEffectWriter, clock, cancellationToken);
}
