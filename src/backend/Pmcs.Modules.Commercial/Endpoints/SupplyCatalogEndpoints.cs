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

internal static partial class SupplyEndpoints
{
    private const string SupplyCalculationVersion = "supply-reality-v1";

    private static void MapCatalogEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/items", CreateItemAsync);
        group.MapPost("/items/{itemId:guid}/unit-conversions", AddUnitConversionAsync);
        group.MapPost("/items/{itemId:guid}/status", SetItemStatusAsync);
        group.MapPost("/locations", CreateLocationAsync);
        group.MapPost("/locations/{locationId:guid}/deactivate", DeactivateLocationAsync);
    }

    private static async Task<IResult> GetStateAsync(
        Guid projectId, ICurrentActor actor, IProjectPermissionService permissions,
        IProjectDirectory projects, CommercialDbContext db, IClock clock,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.read", cancellationToken);
        if (access is not null) return access;
        var project = await projects.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });

        var items = await db.SupplyItems.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderBy(item => item.Code).Take(500).ToListAsync(cancellationToken);
        var locations = await db.InventoryLocations.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderBy(item => item.Code).Take(300).ToListAsync(cancellationToken);
        var receipts = await db.GoodsReceipts.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt).Take(500).ToListAsync(cancellationToken);
        var issues = await db.MaterialIssues.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.IssuedAt).Take(500).ToListAsync(cancellationToken);
        var reconciliations = await db.MaterialReconciliations.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.RecordedAt).Take(1_000).ToListAsync(cancellationToken);
        var adjustments = await db.InventoryAdjustments.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.ProposedAt).Take(500).ToListAsync(cancellationToken);
        var services = await db.ServiceAcceptances.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.VerifiedAt).Take(500).ToListAsync(cancellationToken);
        var ledger = await db.InventoryLedger.AsNoTracking().Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.OccurredAt).Take(2_000).ToListAsync(cancellationToken);
        var balanceRows = await db.InventoryLedger.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .GroupBy(item => new { item.ItemId, item.LocationId, item.BaseUnit })
            .Select(group => new
            {
                group.Key.ItemId, group.Key.LocationId, group.Key.BaseUnit,
                Quantity = group.Sum(item => item.BaseQuantityDelta),
                LastMovementAt = group.Max(item => item.OccurredAt)
            }).ToListAsync(cancellationToken);
        var positions = balanceRows.Select(group => new StockPositionResponse(
                group.ItemId, group.LocationId, group.BaseUnit,
                group.Quantity, 0m, group.Quantity, group.LastMovementAt))
            .OrderBy(item => item.ItemId).ThenBy(item => item.LocationId).ToArray();

        var orderIds = receipts.Select(item => item.PurchaseOrderId).Distinct().ToArray();
        List<PurchaseOrder> orders = orderIds.Length == 0 ? [] : await db.PurchaseOrders.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId && orderIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var orderMap = orders.ToDictionary(item => item.Id);
        var supplierPerformance = receipts.GroupBy(item => item.PartyId).Select(group =>
        {
            var receiptItems = group.ToArray();
            return new SupplierDeliveryPerformanceResponse(
                group.Key,
                receiptItems.Length,
                receiptItems.Count(item => orderMap.TryGetValue(item.PurchaseOrderId, out var order) &&
                    order.DeliveryDueDate.HasValue && ProjectDate(item.ArrivedAt, project.TimeZone) <= order.DeliveryDueDate.Value),
                receiptItems.Count(item => item.Status == GoodsReceiptStatus.Accepted),
                receiptItems.Count(item => item.RejectedBaseQuantity > 0));
        }).OrderBy(item => item.PartyId).ToArray();

        return Results.Ok(new SupplyStateResponse(
            SupplyCalculationVersion, clock.UtcNow,
            items.Select(SupplyItemResponse.From).ToArray(),
            locations.Select(InventoryLocationResponse.From).ToArray(),
            receipts.Select(GoodsReceiptResponse.From).ToArray(),
            issues.Select(MaterialIssueResponse.From).ToArray(),
            reconciliations.Select(MaterialReconciliationResponse.From).ToArray(),
            adjustments.Select(InventoryAdjustmentResponse.From).ToArray(),
            services.Select(ServiceAcceptanceResponse.From).ToArray(),
            ledger.Select(InventoryLedgerResponse.From).ToArray(), positions, supplierPerformance,
            receipts.Count(item => item.Status is GoodsReceiptStatus.Received or GoodsReceiptStatus.PendingInspection),
            receipts.Count(item => item.Status == GoodsReceiptStatus.Quarantined || item.QuarantinedBaseQuantity > 0),
            receipts.Count(item => item.Status == GoodsReceiptStatus.Rejected || item.RejectedBaseQuantity > 0),
            issues.Count(item => item.Status != MaterialIssueStatus.Reconciled),
            adjustments.Count(item => item.Status == InventoryAdjustmentStatus.Proposed)));
    }

    private static async Task<IResult> CreateItemAsync(
        Guid projectId, CreateSupplyItemRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.items.manage", cancellationToken);
        if (access is not null) return access;
        if (!await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.items.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = SupplyItem.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            request.Code, request.Name, request.Kind, request.Category, request.BaseUnit,
            request.TechnicalSpecificationReference, request.TrackingPolicy, request.InspectionRequired,
            request.StorageCondition, request.AcceptanceCriteria, actor.UserId, clock.UtcNow);
        if (await db.SupplyItems.AnyAsync(candidate => candidate.TenantId == actor.TenantId &&
                candidate.ProjectId == projectId && candidate.Code == item.Code, cancellationToken))
            return Results.Conflict(new { code = "supply.item.code.duplicate" });
        db.SupplyItems.Add(item);
        var response = SupplyItemResponse.From(item);
        await PersistAsync(db, stateFactory, context, actor, projectId, item.Id, "SupplyItem", "SupplyItemCreated",
            new Dictionary<string, object?> { ["code"] = item.Code, ["kind"] = item.Kind.ToString(), ["revision"] = item.Revision },
            response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/supply/items/{item.Id}", response);
    }

    private static async Task<IResult> AddUnitConversionAsync(
        Guid projectId, Guid itemId, AddUnitConversionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.items.manage", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.items.conversion:{itemId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindItemAsync(db, actor.TenantId, projectId, itemId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (item.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.item.revision.conflict", item.Revision);
        item.AddUnitConversion(request.BaseRevision, request.UnitCode, request.FactorToBase, request.Version, clock.UtcNow);
        var response = SupplyItemResponse.From(item);
        await PersistAsync(db, stateFactory, context, actor, projectId, item.Id, "SupplyItem", "SupplyUnitConversionAdded",
            new Dictionary<string, object?> { ["unit"] = request.UnitCode, ["version"] = request.Version, ["revision"] = item.Revision },
            response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> SetItemStatusAsync(
        Guid projectId, Guid itemId, SupplyStatusRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.items.manage", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.items.status:{itemId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindItemAsync(db, actor.TenantId, projectId, itemId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (item.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.item.revision.conflict", item.Revision);
        item.SetStatus(request.BaseRevision, request.Active ? SupplyItemStatus.Active : SupplyItemStatus.Inactive, clock.UtcNow);
        var response = SupplyItemResponse.From(item);
        await PersistAsync(db, stateFactory, context, actor, projectId, item.Id, "SupplyItem", "SupplyItemStatusChanged",
            new Dictionary<string, object?> { ["status"] = item.Status.ToString(), ["revision"] = item.Revision },
            response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateLocationAsync(
        Guid projectId, CreateInventoryLocationRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.locations.manage", cancellationToken);
        if (access is not null) return access;
        if (!await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.locations.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = InventoryLocation.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            request.Code, request.Name, request.Type, request.AllowsAvailableStock, actor.UserId, clock.UtcNow);
        if (await db.InventoryLocations.AnyAsync(candidate => candidate.TenantId == actor.TenantId &&
                candidate.ProjectId == projectId && candidate.Code == item.Code, cancellationToken))
            return Results.Conflict(new { code = "supply.location.code.duplicate" });
        db.InventoryLocations.Add(item);
        var response = InventoryLocationResponse.From(item);
        await PersistAsync(db, stateFactory, context, actor, projectId, item.Id, "InventoryLocation", "InventoryLocationCreated",
            new Dictionary<string, object?> { ["code"] = item.Code, ["type"] = item.Type.ToString(), ["revision"] = item.Revision },
            response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/supply/locations/{item.Id}", response);
    }

    private static async Task<IResult> DeactivateLocationAsync(
        Guid projectId, Guid locationId, SupplyTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.locations.manage", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.locations.deactivate:{locationId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindLocationAsync(db, actor.TenantId, projectId, locationId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (item.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.location.revision.conflict", item.Revision);
        var balances = await db.InventoryLedger.AsNoTracking().Where(entry => entry.TenantId == actor.TenantId &&
                entry.ProjectId == projectId && entry.LocationId == locationId)
            .GroupBy(entry => entry.ItemId)
            .Select(group => group.Sum(entry => entry.BaseQuantityDelta))
            .ToListAsync(cancellationToken);
        if (balances.Any(balance => balance != 0))
            return Results.UnprocessableEntity(new { code = "supply.location.balance.not_zero" });
        item.Deactivate(request.BaseRevision);
        var response = InventoryLocationResponse.From(item);
        await PersistAsync(db, stateFactory, context, actor, projectId, item.Id, "InventoryLocation", "InventoryLocationDeactivated",
            new Dictionary<string, object?> { ["code"] = item.Code, ["revision"] = item.Revision },
            response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult?> RequireAsync(
        ICurrentActor actor, IProjectPermissionService permissions, Guid projectId,
        string permission, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        return await CommercialEndpointSupport.HasPermissionAsync(permissions, actor, projectId, permission, cancellationToken)
            ? null : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static Task<SupplyItem?> FindItemAsync(CommercialDbContext db, Guid tenantId, Guid projectId, Guid id, CancellationToken token) =>
        db.SupplyItems.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, token);

    private static Task<InventoryLocation?> FindLocationAsync(CommercialDbContext db, Guid tenantId, Guid projectId, Guid id, CancellationToken token) =>
        db.InventoryLocations.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, token);

    private static async Task<decimal> BalanceAsync(
        CommercialDbContext db, Guid tenantId, Guid projectId, Guid itemId, Guid locationId,
        CancellationToken token) =>
        await db.InventoryLedger.Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.ItemId == itemId && item.LocationId == locationId)
            .SumAsync(item => (decimal?)item.BaseQuantityDelta, token) ?? 0m;

    private static Task PersistAsync(
        CommercialDbContext db, CommercialStateFactory stateFactory, HttpContext context,
        ICurrentActor actor, Guid projectId, Guid entityId, string entityType, string eventName,
        IReadOnlyDictionary<string, object?> audit, object response, CommandIdentity command,
        int statusCode, ITransactionalSideEffectWriter effects, IClock clock, CancellationToken token) =>
        CommercialEndpointSupport.PersistAsync(
            db, stateFactory, context, actor, projectId, entityId, entityType, eventName,
            audit, response, command, statusCode, effects, clock, token);

    private static DateOnly ProjectDate(DateTimeOffset value, string timeZone)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime); }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(value.UtcDateTime); }
        catch (InvalidTimeZoneException) { return DateOnly.FromDateTime(value.UtcDateTime); }
    }
}
