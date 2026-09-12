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

internal static class ProcurementEndpoints
{
    public static void MapProcurementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/purchase-requests", ListRequestsAsync);
        group.MapPost("/purchase-requests", CreateRequestAsync);
        group.MapPut("/purchase-requests/{requestId:guid}", AmendRequestAsync);
        group.MapPost("/purchase-requests/{requestId:guid}/submit", SubmitRequestAsync);
        group.MapPost("/purchase-requests/{requestId:guid}/approve", ApproveRequestAsync);
        group.MapPost("/purchase-requests/{requestId:guid}/return", ReturnRequestAsync);

        group.MapGet("/purchase-orders", ListOrdersAsync);
        group.MapPost("/purchase-orders", IssueOrderAsync);
        group.MapPost("/purchase-orders/{orderId:guid}/close", CloseOrderAsync);
        group.MapPost("/purchase-orders/{orderId:guid}/cancel", CancelOrderAsync);
    }

    private static async Task<IResult> ListRequestsAsync(
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

        if (!await CanAsync(permissionService, actor, projectId, "procurement.requests.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var items = await dbContext.PurchaseRequests.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.ChangedAt)
            .Take(300)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(PurchaseRequestResponse.From).ToArray());
    }

    private static async Task<IResult> CreateRequestAsync(
        Guid projectId,
        CreatePurchaseRequestRequest request,
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

        if (!await CanAsync(permissionService, actor, projectId, "procurement.requests.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        var projectError = ValidateProject(project);
        if (projectError is not null)
        {
            return projectError;
        }

        var currencyError = ValidateCurrency(project!, request.CurrencyCode);
        if (currencyError is not null)
        {
            return currencyError;
        }
        var supplyItemError = await ValidateSupplyItemAsync(
            dbContext, actor.TenantId, projectId, request.SupplyItemId,
            request.RequestedQuantity, request.UnitCode, cancellationToken);
        if (supplyItemError is not null) return supplyItemError;

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.requests.create", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var item = PurchaseRequest.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.Number,
            request.Title,
            request.Description,
            request.EstimatedAmount,
            project!.BaseCurrencyCode,
            request.NeededByDate,
            actor.UserId,
            clock.UtcNow,
            request.SupplyItemId,
            request.RequestedQuantity,
            request.UnitCode,
            request.DeliveryLocation,
            request.WorkItemReference,
            request.WbsReference,
            request.BudgetLineReference,
            request.BudgetCheckStatus,
            request.Criticality);
        if (await dbContext.PurchaseRequests.AnyAsync(
                existing => existing.TenantId == actor.TenantId && existing.ProjectId == projectId && existing.Number == item.Number,
                cancellationToken))
        {
            return Results.Conflict(new { code = "commercial.request.number.duplicate" });
        }

        dbContext.PurchaseRequests.Add(item);
        var response = PurchaseRequestResponse.From(item);
        await PersistRequestAsync(
            dbContext, stateFactory, httpContext, actor, item, "PurchaseRequestCreated", response, command!,
            StatusCodes.Status201Created, sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/purchase-requests/{item.Id}", response);
    }

    private static async Task<IResult> AmendRequestAsync(
        Guid projectId,
        Guid requestId,
        AmendPurchaseRequestRequest request,
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

        if (!await CanAsync(permissionService, actor, projectId, "procurement.requests.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        var projectError = ValidateProject(project);
        if (projectError is not null)
        {
            return projectError;
        }

        var currencyError = ValidateCurrency(project!, request.CurrencyCode);
        if (currencyError is not null)
        {
            return currencyError;
        }
        var supplyItemError = await ValidateSupplyItemAsync(
            dbContext, actor.TenantId, projectId, request.SupplyItemId,
            request.RequestedQuantity, request.UnitCode, cancellationToken);
        if (supplyItemError is not null) return supplyItemError;

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.requests.amend", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var item = await FindRequestAsync(dbContext, actor.TenantId, projectId, requestId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.request.revision.conflict", item.Revision);
        }

        item.Amend(
            request.BaseRevision,
            request.Number,
            request.Title,
            request.Description,
            request.EstimatedAmount,
            project!.BaseCurrencyCode,
            request.NeededByDate,
            clock.UtcNow,
            request.SupplyItemId,
            request.RequestedQuantity,
            request.UnitCode,
            request.DeliveryLocation,
            request.WorkItemReference,
            request.WbsReference,
            request.BudgetLineReference,
            request.BudgetCheckStatus,
            request.Criticality);
        var response = PurchaseRequestResponse.From(item);
        await PersistRequestAsync(
            dbContext, stateFactory, httpContext, actor, item, "PurchaseRequestAmended", response, command!,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> SubmitRequestAsync(
        Guid projectId,
        Guid requestId,
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
        TransitionRequestAsync(
            projectId, requestId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "submit", "procurement.requests.submit", cancellationToken);

    private static Task<IResult> ApproveRequestAsync(
        Guid projectId,
        Guid requestId,
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
        TransitionRequestAsync(
            projectId, requestId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "approve", "procurement.requests.review", cancellationToken);

    private static Task<IResult> ReturnRequestAsync(
        Guid projectId,
        Guid requestId,
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
        TransitionRequestAsync(
            projectId, requestId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "return", "procurement.requests.review", cancellationToken);

    private static async Task<IResult> TransitionRequestAsync(
        Guid projectId,
        Guid requestId,
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

        var operation = $"commercial.requests.{action}";
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, operation, request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var item = await FindRequestAsync(dbContext, actor.TenantId, projectId, requestId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.request.revision.conflict", item.Revision);
        }

        var eventName = action switch
        {
            "submit" => Apply(() => item.Submit(request.BaseRevision, clock.UtcNow), "PurchaseRequestSubmitted"),
            "approve" => Apply(() => item.Approve(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "PurchaseRequestApproved"),
            "return" => Apply(() => item.Return(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "PurchaseRequestReturned"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported request action.")
        };
        var response = PurchaseRequestResponse.From(item);
        await PersistRequestAsync(
            dbContext, stateFactory, httpContext, actor, item, eventName, response, command!,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListOrdersAsync(
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

        if (!await CanAsync(permissionService, actor, projectId, "procurement.orders.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var items = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.ChangedAt)
            .Take(300)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(PurchaseOrderResponse.From).ToArray());
    }

    private static async Task<IResult> IssueOrderAsync(
        Guid projectId,
        IssuePurchaseOrderRequest request,
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

        if (!await CanAsync(permissionService, actor, projectId, "procurement.orders.issue", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        var projectError = ValidateProject(project);
        if (projectError is not null)
        {
            return projectError;
        }

        var currencyError = ValidateCurrency(project!, request.CurrencyCode);
        if (currencyError is not null)
        {
            return currencyError;
        }

        var itemRequest = await FindRequestAsync(
            dbContext, actor.TenantId, projectId, request.PurchaseRequestId, cancellationToken);
        if (itemRequest is null)
        {
            return Results.UnprocessableEntity(new { code = "commercial.request.not_found" });
        }

        if (itemRequest.Revision != request.PurchaseRequestRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.request.revision.conflict", itemRequest.Revision);
        }

        if (itemRequest.Status != PurchaseRequestStatus.Approved)
        {
            return Results.UnprocessableEntity(new { code = "commercial.request.not_approved" });
        }
        var supplyItemError = await ValidateSupplyItemAsync(
            dbContext, actor.TenantId, projectId, itemRequest.SupplyItemId,
            itemRequest.RequestedQuantity, itemRequest.UnitCode, cancellationToken);
        if (supplyItemError is not null) return supplyItemError;

        if (!await dbContext.Parties.AsNoTracking().AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.Id == request.PartyId && item.Status == PartyStatus.Active,
                cancellationToken))
        {
            return Results.UnprocessableEntity(new { code = "commercial.party.not_active" });
        }

        Guid? contractPartyId = null;
        if (request.ContractId.HasValue)
        {
            contractPartyId = await dbContext.Contracts.AsNoTracking()
                .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.Id == request.ContractId && item.Status == ProjectContractStatus.Active)
                .Select(item => (Guid?)item.PartyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!contractPartyId.HasValue)
            {
                return Results.UnprocessableEntity(new { code = "commercial.contract.not_active" });
            }

            if (contractPartyId.Value != request.PartyId)
            {
                return Results.UnprocessableEntity(new { code = "commercial.order.contract_party_mismatch" });
            }
        }

        if (await dbContext.PurchaseOrders.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    (item.PurchaseRequestId == request.PurchaseRequestId || item.Number == request.Number),
                cancellationToken))
        {
            return Results.Conflict(new { code = "commercial.order.duplicate" });
        }

        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, "commercial.orders.issue", request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var order = PurchaseOrder.Issue(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.PurchaseRequestId,
            request.PartyId,
            request.ContractId,
            request.Number,
            request.Title,
            request.Amount,
            project!.BaseCurrencyCode,
            request.DeliveryDueDate,
            actor.UserId,
            clock.UtcNow,
            itemRequest.SupplyItemId,
            itemRequest.RequestedQuantity,
            itemRequest.UnitCode,
            itemRequest.DeliveryLocation);
        itemRequest.MarkOrdered(request.PurchaseRequestRevision, clock.UtcNow);
        dbContext.PurchaseOrders.Add(order);
        var response = PurchaseOrderResponse.From(order);
        await PersistOrderAsync(
            dbContext, stateFactory, httpContext, actor, order, "PurchaseOrderIssued", response, command!,
            StatusCodes.Status201Created, sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/purchase-orders/{order.Id}", response);
    }

    private static Task<IResult> CloseOrderAsync(
        Guid projectId,
        Guid orderId,
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
        TransitionOrderAsync(
            projectId, orderId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "close", cancellationToken);

    private static Task<IResult> CancelOrderAsync(
        Guid projectId,
        Guid orderId,
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
        TransitionOrderAsync(
            projectId, orderId, request, httpContext, actor, permissionService, dbContext, stateFactory,
            clock, sideEffectWriter, idempotencyStore, "cancel", cancellationToken);

    private static async Task<IResult> TransitionOrderAsync(
        Guid projectId,
        Guid orderId,
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
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CanAsync(permissionService, actor, projectId, "procurement.orders.manage", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var operation = $"commercial.orders.{action}";
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            httpContext, actor, idempotencyStore, operation, request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var item = await dbContext.PurchaseOrders.SingleOrDefaultAsync(
            order => order.TenantId == actor.TenantId && order.ProjectId == projectId && order.Id == orderId,
            cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return CommercialEndpointSupport.RevisionConflict("commercial.order.revision.conflict", item.Revision);
        }

        var eventName = action switch
        {
            "close" => Apply(() => item.Close(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "PurchaseOrderClosed"),
            "cancel" => Apply(() => item.Cancel(request.BaseRevision, actor.UserId, clock.UtcNow, request.Comment), "PurchaseOrderCancelled"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported order action.")
        };
        var response = PurchaseOrderResponse.From(item);
        await PersistOrderAsync(
            dbContext, stateFactory, httpContext, actor, item, eventName, response, command!,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<PurchaseRequest?> FindRequestAsync(
        CommercialDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid requestId,
        CancellationToken cancellationToken) =>
        dbContext.PurchaseRequests.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == requestId,
            cancellationToken);

    private static IResult? ValidateProject(ProjectControlProfile? project)
    {
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        return project.Procurement is not ProjectFeatureState.Active and not ProjectFeatureState.SetupRequired
            ? Results.UnprocessableEntity(new { code = "commercial.procurement.not_active", state = project.Procurement })
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

    private static async Task<IResult?> ValidateSupplyItemAsync(
        CommercialDbContext dbContext, Guid tenantId, Guid projectId, Guid? itemId,
        decimal? quantity, string? unitCode, CancellationToken cancellationToken)
    {
        if (!itemId.HasValue) return null;
        var item = await dbContext.SupplyItems.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.TenantId == tenantId && candidate.ProjectId == projectId &&
            candidate.Id == itemId && candidate.Status == SupplyItemStatus.Active, cancellationToken);
        if (item is null) return Results.UnprocessableEntity(new { code = "supply.item.not_active" });
        if (quantity.HasValue) _ = item.ConvertToBase(quantity.Value, unitCode);
        return null;
    }

    private static string Apply(Action action, string eventName)
    {
        action();
        return eventName;
    }

    private static Task PersistRequestAsync(
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        HttpContext httpContext,
        ICurrentActor actor,
        PurchaseRequest item,
        string eventName,
        PurchaseRequestResponse response,
        CommandIdentity command,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken) =>
        CommercialEndpointSupport.PersistAsync(
            dbContext, stateFactory, httpContext, actor, item.ProjectId, item.Id, "PurchaseRequest", eventName,
            new Dictionary<string, object?>
            {
                ["number"] = item.Number,
                ["status"] = item.Status.ToString(),
                ["estimatedAmount"] = item.EstimatedAmount,
                ["revision"] = item.Revision
            },
            response, command, statusCode, sideEffectWriter, clock, cancellationToken);

    private static Task PersistOrderAsync(
        CommercialDbContext dbContext,
        CommercialStateFactory stateFactory,
        HttpContext httpContext,
        ICurrentActor actor,
        PurchaseOrder item,
        string eventName,
        PurchaseOrderResponse response,
        CommandIdentity command,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken) =>
        CommercialEndpointSupport.PersistAsync(
            dbContext, stateFactory, httpContext, actor, item.ProjectId, item.Id, "PurchaseOrder", eventName,
            new Dictionary<string, object?>
            {
                ["number"] = item.Number,
                ["purchaseRequestId"] = item.PurchaseRequestId,
                ["partyId"] = item.PartyId,
                ["contractId"] = item.ContractId,
                ["amount"] = item.Amount,
                ["status"] = item.Status.ToString(),
                ["revision"] = item.Revision
            },
            response, command, statusCode, sideEffectWriter, clock, cancellationToken);
}
