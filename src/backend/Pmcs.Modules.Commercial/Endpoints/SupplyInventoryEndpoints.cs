using System.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Commercial.Services;

namespace Pmcs.Modules.Commercial.Endpoints;

internal static partial class SupplyEndpoints
{
    private static void MapInventoryEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/material-issues", CreateMaterialIssueAsync);
        group.MapPost("/material-issues/{issueId:guid}/acknowledge", AcknowledgeMaterialIssueAsync);
        group.MapPost("/material-issues/{issueId:guid}/reconcile", ReconcileMaterialIssueAsync);
        group.MapPost("/transfers", TransferInventoryAsync);
        group.MapPost("/adjustments", ProposeAdjustmentAsync);
        group.MapPost("/adjustments/{adjustmentId:guid}/review", ReviewAdjustmentAsync);
        group.MapPost("/adjustments/{adjustmentId:guid}/post", PostAdjustmentAsync);
        group.MapPost("/service-acceptances", CreateServiceAcceptanceAsync);
    }

    private static async Task<IResult> CreateMaterialIssueAsync(
        Guid projectId, CreateMaterialIssueRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, CommercialDbContext db, CommercialStateFactory stateFactory,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.issue", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.material-issues.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var item = await FindItemAsync(db, actor.TenantId, projectId, request.ItemId, cancellationToken);
        if (item is null || item.Status != SupplyItemStatus.Active || item.Kind != SupplyItemKind.Material ||
            item.TrackingPolicy == SupplyTrackingPolicy.None)
            return Results.UnprocessableEntity(new { code = "supply.issue.item.not_material" });
        var location = await FindLocationAsync(db, actor.TenantId, projectId, request.SourceLocationId, cancellationToken);
        if (location is null || location.Status != InventoryLocationStatus.Active || !location.AllowsAvailableStock)
            return Results.UnprocessableEntity(new { code = "supply.issue.location.invalid" });
        if (request.PurchaseRequestId.HasValue && !await db.PurchaseRequests.AsNoTracking().AnyAsync(candidate =>
                candidate.TenantId == actor.TenantId && candidate.ProjectId == projectId &&
                candidate.Id == request.PurchaseRequestId.Value, cancellationToken))
            return Results.UnprocessableEntity(new { code = "supply.issue.request.invalid" });
        var quantity = item.ConvertToBase(request.Quantity, request.UnitCode, request.ConversionVersion);
        var available = await BalanceAsync(db, actor.TenantId, projectId, item.Id, location.Id, cancellationToken);
        if (available < quantity.BaseQuantity)
            return Results.Conflict(new { code = "supply.inventory.insufficient", availableBaseQuantity = available });

        var issue = MaterialIssue.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            item.Id, location.Id, quantity.BaseQuantity, item.BaseUnit, request.IssuedTo,
            request.DestinationLocation, request.WorkItemReference, request.WbsReference,
            request.PurchaseRequestId, request.EvidenceReferences, actor.UserId, clock.UtcNow);
        db.MaterialIssues.Add(issue);
        db.InventoryLedger.Add(InventoryLedgerEntry.Create(
            Guid.NewGuid(), actor.TenantId, projectId, issue.Id, item.Id, location.Id,
            InventoryEventType.MaterialIssue, -quantity.BaseQuantity, item.BaseUnit,
            "MaterialIssue", issue.Id, null, null, issue.IssuedAt, actor.UserId, clock.UtcNow));
        var response = MaterialIssueResponse.From(issue);
        await PersistAsync(db, stateFactory, context, actor, projectId, issue.Id, "MaterialIssue", "MaterialIssuedFromStore",
            IssueAudit(issue), response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/supply/material-issues/{issue.Id}", response);
    }

    private static async Task<IResult> AcknowledgeMaterialIssueAsync(
        Guid projectId, Guid issueId, AcknowledgeMaterialIssueRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.acknowledge", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.material-issues.acknowledge:{issueId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var issue = await FindIssueAsync(db, actor.TenantId, projectId, issueId, cancellationToken);
        if (issue is null) return Results.NotFound();
        if (issue.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.issue.revision.conflict", issue.Revision);
        issue.Acknowledge(request.BaseRevision, actor.UserId, clock.UtcNow, request.Reference);
        var response = MaterialIssueResponse.From(issue);
        await PersistAsync(db, stateFactory, context, actor, projectId, issue.Id, "MaterialIssue", "MaterialIssueAcknowledged",
            IssueAudit(issue), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ReconcileMaterialIssueAsync(
        Guid projectId, Guid issueId, ReconcileMaterialIssueRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.reconcile", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.material-issues.reconcile:{issueId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var issue = await FindIssueAsync(db, actor.TenantId, projectId, issueId, cancellationToken);
        if (issue is null) return Results.NotFound();
        if (issue.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.issue.revision.conflict", issue.Revision);
        var now = clock.UtcNow;
        issue.Reconcile(
            request.BaseRevision, request.ConsumedBaseQuantity, request.ReturnedBaseQuantity,
            request.WasteBaseQuantity, request.WasteReason, request.EvidenceReferences, now);
        var record = MaterialReconciliationRecord.Create(
            Guid.NewGuid(), actor.TenantId, projectId, issue.Id,
            request.ConsumedBaseQuantity, request.ReturnedBaseQuantity, request.WasteBaseQuantity,
            issue.BaseUnit, request.WasteReason, request.EvidenceReferences, actor.UserId, now);
        db.MaterialReconciliations.Add(record);
        if (request.ReturnedBaseQuantity > 0)
        {
            db.InventoryLedger.Add(InventoryLedgerEntry.Create(
                Guid.NewGuid(), actor.TenantId, projectId, record.Id, issue.ItemId, issue.SourceLocationId,
                InventoryEventType.ReturnFromSite, request.ReturnedBaseQuantity, issue.BaseUnit,
                "MaterialReconciliation", record.Id, null, null, now, actor.UserId, now));
        }
        var response = MaterialIssueResponse.From(issue);
        await PersistAsync(db, stateFactory, context, actor, projectId, issue.Id, "MaterialIssue", "MaterialCustodyReconciled",
            IssueAudit(issue), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> TransferInventoryAsync(
        Guid projectId, TransferInventoryRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, CommercialDbContext db, CommercialStateFactory stateFactory,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.transfer", cancellationToken);
        if (access is not null) return access;
        if (request.SourceLocationId == request.DestinationLocationId)
            return Results.UnprocessableEntity(new { code = "supply.transfer.locations.same" });
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.inventory.transfer:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var item = await FindItemAsync(db, actor.TenantId, projectId, request.ItemId, cancellationToken);
        var source = await FindLocationAsync(db, actor.TenantId, projectId, request.SourceLocationId, cancellationToken);
        var destination = await FindLocationAsync(db, actor.TenantId, projectId, request.DestinationLocationId, cancellationToken);
        if (item is null || item.Status != SupplyItemStatus.Active || item.Kind != SupplyItemKind.Material ||
            item.TrackingPolicy == SupplyTrackingPolicy.None)
            return Results.UnprocessableEntity(new { code = "supply.transfer.item.invalid" });
        if (source is null || destination is null || source.Status != InventoryLocationStatus.Active ||
            destination.Status != InventoryLocationStatus.Active || !source.AllowsAvailableStock || !destination.AllowsAvailableStock)
            return Results.UnprocessableEntity(new { code = "supply.transfer.location.invalid" });
        var quantity = item.ConvertToBase(request.Quantity, request.UnitCode, request.ConversionVersion);
        var available = await BalanceAsync(db, actor.TenantId, projectId, item.Id, source.Id, cancellationToken);
        if (available < quantity.BaseQuantity)
            return Results.Conflict(new { code = "supply.inventory.insufficient", availableBaseQuantity = available });
        var transactionId = request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid());
        var now = clock.UtcNow;
        db.InventoryLedger.AddRange(
            InventoryLedgerEntry.Create(
                Guid.NewGuid(), actor.TenantId, projectId, transactionId, item.Id, source.Id,
                InventoryEventType.TransferOut, -quantity.BaseQuantity, item.BaseUnit,
                "InventoryTransfer", transactionId, null, request.Reason, now, actor.UserId, now),
            InventoryLedgerEntry.Create(
                Guid.NewGuid(), actor.TenantId, projectId, transactionId, item.Id, destination.Id,
                InventoryEventType.TransferIn, quantity.BaseQuantity, item.BaseUnit,
                "InventoryTransfer", transactionId, null, request.Reason, now, actor.UserId, now));
        var response = new InventoryTransferResponse(
            transactionId, item.Id, source.Id, destination.Id, quantity.BaseQuantity, item.BaseUnit, now);
        await PersistAsync(db, stateFactory, context, actor, projectId, transactionId, "InventoryTransfer", "InventoryTransferred",
            new Dictionary<string, object?> { ["itemId"] = item.Id, ["sourceLocationId"] = source.Id,
                ["destinationLocationId"] = destination.Id, ["baseQuantity"] = quantity.BaseQuantity },
            response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/supply/transfers/{transactionId}", response);
    }

    private static async Task<IResult> ProposeAdjustmentAsync(
        Guid projectId, ProposeInventoryAdjustmentRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.count", cancellationToken);
        if (access is not null) return access;
        if (request.CutoffAt > clock.UtcNow)
            return Results.UnprocessableEntity(new { code = "supply.count.cutoff.future" });
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.adjustments.propose:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var item = await FindItemAsync(db, actor.TenantId, projectId, request.ItemId, cancellationToken);
        var location = await FindLocationAsync(db, actor.TenantId, projectId, request.LocationId, cancellationToken);
        if (item is null || item.Kind != SupplyItemKind.Material || item.TrackingPolicy == SupplyTrackingPolicy.None ||
            location is null || location.Status != InventoryLocationStatus.Active)
            return Results.UnprocessableEntity(new { code = "supply.count.scope.invalid" });
        var systemQuantity = await db.InventoryLedger.Where(entry => entry.TenantId == actor.TenantId &&
                entry.ProjectId == projectId && entry.ItemId == item.Id && entry.LocationId == location.Id &&
                entry.OccurredAt <= request.CutoffAt)
            .SumAsync(entry => (decimal?)entry.BaseQuantityDelta, cancellationToken) ?? 0m;
        var adjustment = InventoryAdjustment.Propose(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            item.Id, location.Id, request.CutoffAt, systemQuantity, request.CountedBaseQuantity,
            item.BaseUnit, request.Reason, request.EvidenceReferences, actor.UserId, clock.UtcNow);
        db.InventoryAdjustments.Add(adjustment);
        var response = InventoryAdjustmentResponse.From(adjustment);
        await PersistAsync(db, stateFactory, context, actor, projectId, adjustment.Id, "InventoryAdjustment", "InventoryAdjustmentProposed",
            AdjustmentAudit(adjustment), response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/supply/adjustments/{adjustment.Id}", response);
    }

    private static async Task<IResult> ReviewAdjustmentAsync(
        Guid projectId, Guid adjustmentId, ReviewInventoryAdjustmentRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.adjust", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.adjustments.review:{adjustmentId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var adjustment = await FindAdjustmentAsync(db, actor.TenantId, projectId, adjustmentId, cancellationToken);
        if (adjustment is null) return Results.NotFound();
        if (adjustment.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.adjustment.revision.conflict", adjustment.Revision);
        adjustment.Review(request.BaseRevision, request.Approved, actor.UserId, clock.UtcNow, request.Comment);
        var response = InventoryAdjustmentResponse.From(adjustment);
        await PersistAsync(db, stateFactory, context, actor, projectId, adjustment.Id, "InventoryAdjustment", "InventoryAdjustmentReviewed",
            AdjustmentAudit(adjustment), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PostAdjustmentAsync(
        Guid projectId, Guid adjustmentId, SupplyTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, CommercialDbContext db,
        CommercialStateFactory stateFactory, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.inventory.adjust", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.adjustments.post:{adjustmentId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var adjustment = await FindAdjustmentAsync(db, actor.TenantId, projectId, adjustmentId, cancellationToken);
        if (adjustment is null) return Results.NotFound();
        if (adjustment.Revision != request.BaseRevision)
            return CommercialEndpointSupport.RevisionConflict("supply.adjustment.revision.conflict", adjustment.Revision);
        var current = await BalanceAsync(db, actor.TenantId, projectId, adjustment.ItemId, adjustment.LocationId, cancellationToken);
        if (current + adjustment.DeltaBaseQuantity < 0)
            return Results.Conflict(new { code = "supply.adjustment.would_make_negative", currentBaseQuantity = current });
        adjustment.MarkPosted(request.BaseRevision, clock.UtcNow);
        db.InventoryLedger.Add(InventoryLedgerEntry.Create(
            Guid.NewGuid(), actor.TenantId, projectId, adjustment.Id, adjustment.ItemId, adjustment.LocationId,
            InventoryEventType.Adjustment, adjustment.DeltaBaseQuantity, adjustment.BaseUnit,
            "InventoryAdjustment", adjustment.Id, null, adjustment.Reason, clock.UtcNow, actor.UserId, clock.UtcNow));
        var response = InventoryAdjustmentResponse.From(adjustment);
        await PersistAsync(db, stateFactory, context, actor, projectId, adjustment.Id, "InventoryAdjustment", "InventoryAdjustmentPosted",
            AdjustmentAudit(adjustment), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateServiceAcceptanceAsync(
        Guid projectId, CreateServiceAcceptanceRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, CommercialDbContext db, CommercialStateFactory stateFactory,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "supply.services.accept", cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await CommercialEndpointSupport.GetCommandAsync(
            context, actor, idempotency, $"supply.services.accept:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.TenantId == actor.TenantId && candidate.ProjectId == projectId &&
            candidate.Id == request.PurchaseOrderId && candidate.Status == PurchaseOrderStatus.Issued, cancellationToken);
        var item = await FindItemAsync(db, actor.TenantId, projectId, request.ItemId, cancellationToken);
        if (order is null || item is null || item.Status != SupplyItemStatus.Active || item.Kind == SupplyItemKind.Material)
            return Results.UnprocessableEntity(new { code = "supply.service.order_or_item.invalid" });
        if (order.SupplyItemId != item.Id || !order.OrderedQuantity.HasValue || order.UnitCode is null)
            return Results.UnprocessableEntity(new { code = "supply.service.item.order_mismatch" });
        var delivered = item.ConvertToBase(request.DeliveredQuantity, request.UnitCode, request.ConversionVersion);
        ConvertedQuantity accepted = request.AcceptedQuantity == 0
            ? new ConvertedQuantity(0, delivered.UnitCode, 0, delivered.BaseUnit, delivered.ConversionVersion)
            : item.ConvertToBase(request.AcceptedQuantity, request.UnitCode, request.ConversionVersion);
        ConvertedQuantity rejected = request.RejectedQuantity == 0
            ? new ConvertedQuantity(0, delivered.UnitCode, 0, delivered.BaseUnit, delivered.ConversionVersion)
            : item.ConvertToBase(request.RejectedQuantity, request.UnitCode, request.ConversionVersion);
        if (order.OrderedQuantity.HasValue && order.UnitCode is not null)
        {
            var ordered = item.ConvertToBase(order.OrderedQuantity.Value, order.UnitCode);
            var priorDelivered = await db.ServiceAcceptances.Where(candidate => candidate.TenantId == actor.TenantId &&
                    candidate.ProjectId == projectId && candidate.PurchaseOrderId == order.Id && candidate.ItemId == item.Id)
                .SumAsync(candidate => (decimal?)candidate.DeliveredBaseQuantity, cancellationToken) ?? 0m;
            if (priorDelivered + delivered.BaseQuantity > ordered.BaseQuantity)
                return Results.UnprocessableEntity(new { code = "supply.service.quantity.exceeds_order" });
        }
        var acceptance = ServiceAcceptance.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            order.Id, order.PartyId, item.Id, request.PeriodStart, request.PeriodEnd,
            delivered.BaseQuantity, accepted.BaseQuantity, rejected.BaseQuantity, item.BaseUnit,
            request.AcceptanceCriteria, request.EvidenceReferences, request.Comment, actor.UserId, clock.UtcNow);
        db.ServiceAcceptances.Add(acceptance);
        var response = ServiceAcceptanceResponse.From(acceptance);
        await PersistAsync(db, stateFactory, context, actor, projectId, acceptance.Id, "ServiceAcceptance", "ServiceAccepted",
            new Dictionary<string, object?> { ["number"] = acceptance.Number, ["purchaseOrderId"] = order.Id,
                ["partyId"] = order.PartyId, ["itemId"] = item.Id,
                ["deliveredBaseQuantity"] = acceptance.DeliveredBaseQuantity,
                ["acceptedBaseQuantity"] = acceptance.AcceptedBaseQuantity },
            response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/commercial/supply/service-acceptances/{acceptance.Id}", response);
    }

    private static Task<MaterialIssue?> FindIssueAsync(
        CommercialDbContext db, Guid tenantId, Guid projectId, Guid id, CancellationToken token) =>
        db.MaterialIssues.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, token);

    private static Task<InventoryAdjustment?> FindAdjustmentAsync(
        CommercialDbContext db, Guid tenantId, Guid projectId, Guid id, CancellationToken token) =>
        db.InventoryAdjustments.SingleOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, token);

    private static Dictionary<string, object?> IssueAudit(MaterialIssue item) => new()
    {
        ["number"] = item.Number, ["itemId"] = item.ItemId, ["sourceLocationId"] = item.SourceLocationId,
        ["issuedBaseQuantity"] = item.IssuedBaseQuantity, ["consumedBaseQuantity"] = item.ConsumedBaseQuantity,
        ["returnedBaseQuantity"] = item.ReturnedBaseQuantity, ["wasteBaseQuantity"] = item.WasteBaseQuantity,
        ["unreconciledBaseQuantity"] = item.UnreconciledBaseQuantity, ["status"] = item.Status.ToString(),
        ["revision"] = item.Revision
    };

    private static Dictionary<string, object?> AdjustmentAudit(InventoryAdjustment item) => new()
    {
        ["number"] = item.Number, ["itemId"] = item.ItemId, ["locationId"] = item.LocationId,
        ["cutoffAt"] = item.CutoffAt, ["systemBaseQuantity"] = item.SystemBaseQuantity,
        ["countedBaseQuantity"] = item.CountedBaseQuantity, ["deltaBaseQuantity"] = item.DeltaBaseQuantity,
        ["status"] = item.Status.ToString(), ["revision"] = item.Revision
    };
}
