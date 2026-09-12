using System.Data;
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
    private static void MapReceiptEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/receipts", CreateReceiptAsync);
        group.MapPost("/receipts/{receiptId:guid}/submit-inspection", SubmitReceiptForInspectionAsync);
        group.MapPost("/receipts/{receiptId:guid}/inspect", InspectReceiptAsync);
        group.MapPost("/receipts/{receiptId:guid}/post-stock", PostReceiptToStockAsync);
    }

    private static async Task<IResult> CreateReceiptAsync(
        Guid projectId, CreateGoodsReceiptRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.receipts.capture", cancellationToken);
        if (access is not null) return access;
        if (!await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.receipts.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.ProjectId == projectId &&
            item.Id == request.PurchaseOrderId, cancellationToken);
        if (order is null || order.Status != PurchaseOrderStatus.Issued)
            return Results.UnprocessableEntity(new { code = "supply.receipt.order.not_open" });
        var item = await FindItemAsync(db, actor.TenantId, projectId, request.ItemId, cancellationToken);
        if (item is null || item.Status != SupplyItemStatus.Active || item.Kind != SupplyItemKind.Material)
            return Results.UnprocessableEntity(new { code = "supply.receipt.item.not_material" });
        if (order.SupplyItemId != item.Id || !order.OrderedQuantity.HasValue || order.UnitCode is null)
            return Results.UnprocessableEntity(new { code = "supply.receipt.item.order_mismatch" });
        if ((item.TrackingPolicy == SupplyTrackingPolicy.None) == request.StockLocationId.HasValue)
            return Results.UnprocessableEntity(new { code = "supply.receipt.stock_basis.invalid" });
        InventoryLocation? location = null;
        if (request.StockLocationId.HasValue)
        {
            location = await FindLocationAsync(db, actor.TenantId, projectId, request.StockLocationId.Value, cancellationToken);
            if (location is null || location.Status != InventoryLocationStatus.Active || !location.AllowsAvailableStock)
                return Results.UnprocessableEntity(new { code = "supply.receipt.stock_location.invalid" });
        }

        var shipped = item.ConvertToBase(request.ShippedQuantity, request.UnitCode, request.ConversionVersion);
        var received = item.ConvertToBase(request.ReceivedQuantity, request.UnitCode, request.ConversionVersion);
        Guid? excessApprovedBy = null;
        if (order.OrderedQuantity.HasValue && order.UnitCode is not null)
        {
            var ordered = item.ConvertToBase(order.OrderedQuantity.Value, order.UnitCode);
            var previouslyReceived = await db.GoodsReceipts.Where(candidate =>
                    candidate.TenantId == actor.TenantId && candidate.ProjectId == projectId &&
                    candidate.PurchaseOrderId == order.Id && candidate.ItemId == item.Id)
                .SumAsync(candidate => (decimal?)candidate.BaseReceivedQuantity, cancellationToken) ?? 0m;
            if (previouslyReceived + received.BaseQuantity > ordered.BaseQuantity)
            {
                if (string.IsNullOrWhiteSpace(request.ExcessApprovalReason) ||
                    !await CommercialEndpointSupport.HasPermissionAsync(
                        permissions, actor, projectId, "supply.receipts.override-excess", cancellationToken))
                    return Results.UnprocessableEntity(new
                    {
                        code = "supply.receipt.excess.requires_approval",
                        orderedBaseQuantity = ordered.BaseQuantity,
                        receivedToDateBaseQuantity = previouslyReceived
                    });
                excessApprovedBy = actor.UserId;
            }
        }

        var receipt = GoodsReceipt.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            order.Id, order.PartyId, item.Id, location?.Id, request.DispatchNote, request.ArrivedAt,
            request.DeliveryLocation, shipped, received, request.DamagedQuantity,
            request.BatchOrLotReference, request.PackageCondition, request.ExcessApprovalReason,
            excessApprovedBy, request.EvidenceReferences, actor.UserId, clock.UtcNow);
        db.GoodsReceipts.Add(receipt);
        var response = GoodsReceiptResponse.From(receipt);
        await PersistAsync(db, stateFactory, context, actor, projectId, receipt.Id, "GoodsReceipt", "GoodsPhysicallyReceived",
            ReceiptAudit(receipt), response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/supply/receipts/{receipt.Id}", response);
    }

    private static Task<IResult> SubmitReceiptForInspectionAsync(
        Guid projectId, Guid receiptId, SupplyTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken) =>
        TransitionReceiptAsync(projectId, receiptId, request, context, actor, permissions, db, stateFactory,
            clock, effects, idempotency, "supply.receipts.capture", "submit-inspection",
            item => item.SubmitForInspection(request.BaseRevision), "GoodsReceiptSubmittedForInspection", cancellationToken);

    private static async Task<IResult> InspectReceiptAsync(
        Guid projectId, Guid receiptId, InspectGoodsReceiptRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inspections.review", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.receipts.inspect:{receiptId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var receipt = await FindReceiptAsync(db, actor.TenantId, projectId, receiptId, cancellationToken);
        if (receipt is null) return Results.NotFound();
        if (receipt.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.receipt.revision.conflict", receipt.Revision);
        receipt.RecordInspection(
            request.BaseRevision, request.AcceptedBaseQuantity, request.RejectedBaseQuantity,
            request.QuarantinedBaseQuantity, request.InspectionType, request.InspectionReference,
            request.Comment, actor.UserId, clock.UtcNow);
        var response = GoodsReceiptResponse.From(receipt);
        await PersistAsync(db, stateFactory, context, actor, projectId, receipt.Id, "GoodsReceipt", "GoodsReceiptInspected",
            ReceiptAudit(receipt), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PostReceiptToStockAsync(
        Guid projectId, Guid receiptId, SupplyTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.post", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.receipts.post-stock:{receiptId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var receipt = await FindReceiptAsync(db, actor.TenantId, projectId, receiptId, cancellationToken);
        if (receipt is null) return Results.NotFound();
        if (receipt.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.receipt.revision.conflict", receipt.Revision);
        if (!receipt.StockLocationId.HasValue)
            return Results.UnprocessableEntity(new { code = "supply.receipt.stock_location.required" });
        var item = await FindItemAsync(db, actor.TenantId, projectId, receipt.ItemId, cancellationToken);
        if (item is null || item.TrackingPolicy == SupplyTrackingPolicy.None)
            return Results.UnprocessableEntity(new { code = "supply.receipt.stock_basis.invalid" });
        var location = await FindLocationAsync(db, actor.TenantId, projectId, receipt.StockLocationId.Value, cancellationToken);
        if (location is null || location.Status != InventoryLocationStatus.Active || !location.AllowsAvailableStock)
            return Results.UnprocessableEntity(new { code = "supply.receipt.stock_location.invalid" });
        receipt.MarkStockPosted(request.BaseRevision, clock.UtcNow);
        db.InventoryLedger.Add(InventoryLedgerEntry.Create(
            Guid.NewGuid(), actor.TenantId, projectId, receipt.Id, receipt.ItemId, location.Id,
            InventoryEventType.AcceptedReceipt, receipt.AcceptedBaseQuantity, receipt.BaseUnit,
            "GoodsReceipt", receipt.Id, null, request.Comment, receipt.InspectedAt ?? clock.UtcNow,
            actor.UserId, clock.UtcNow));
        var response = GoodsReceiptResponse.From(receipt);
        await PersistAsync(db, stateFactory, context, actor, projectId, receipt.Id, "GoodsReceipt", "AcceptedGoodsPostedToStock",
            ReceiptAudit(receipt), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> TransitionReceiptAsync(
        Guid projectId, Guid receiptId, SupplyTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, string permission, string operation,
        Action<GoodsReceipt> transition, string eventName, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, permission, cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.receipts.{operation}:{receiptId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var receipt = await FindReceiptAsync(db, actor.TenantId, projectId, receiptId, cancellationToken);
        if (receipt is null) return Results.NotFound();
        if (receipt.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.receipt.revision.conflict", receipt.Revision);
        transition(receipt);
        var response = GoodsReceiptResponse.From(receipt);
        await PersistAsync(db, stateFactory, context, actor, projectId, receipt.Id, "GoodsReceipt", eventName,
            ReceiptAudit(receipt), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<GoodsReceipt?> FindReceiptAsync(
        CommercialDbContext db, Guid tenantId, Guid projectId, Guid id, CancellationToken token) =>
        db.GoodsReceipts.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, token);

    private static Dictionary<string, object?> ReceiptAudit(GoodsReceipt item) => new()
    {
        ["number"] = item.Number, ["purchaseOrderId"] = item.PurchaseOrderId,
        ["partyId"] = item.PartyId, ["itemId"] = item.ItemId, ["status"] = item.Status.ToString(),
        ["receivedBaseQuantity"] = item.BaseReceivedQuantity, ["acceptedBaseQuantity"] = item.AcceptedBaseQuantity,
        ["rejectedBaseQuantity"] = item.RejectedBaseQuantity, ["quarantinedBaseQuantity"] = item.QuarantinedBaseQuantity,
        ["excessApprovedBy"] = item.ExcessApprovedBy, ["stockPostedAt"] = item.StockPostedAt, ["revision"] = item.Revision
    };
}
