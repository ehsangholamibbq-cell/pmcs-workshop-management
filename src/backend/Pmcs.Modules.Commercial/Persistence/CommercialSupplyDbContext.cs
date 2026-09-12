using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Modules.Commercial.Persistence;

internal sealed partial class CommercialDbContext
{
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<InventoryLedgerEntry> InventoryLedger => Set<InventoryLedgerEntry>();
    public DbSet<MaterialIssue> MaterialIssues => Set<MaterialIssue>();
    public DbSet<MaterialReconciliationRecord> MaterialReconciliations => Set<MaterialReconciliationRecord>();
    public DbSet<InventoryAdjustment> InventoryAdjustments => Set<InventoryAdjustment>();
    public DbSet<ServiceAcceptance> ServiceAcceptances => Set<ServiceAcceptance>();

    private static void ConfigureSupplyReality(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SupplyItem>(builder =>
        {
            builder.ToTable("supply_items"); ConfigureAggregate(builder);
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(48);
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(240);
            builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(120);
            builder.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(24);
            builder.Property(x => x.UnitConversionsJson).HasColumnName("unit_conversions_json").HasColumnType("jsonb");
            builder.Property(x => x.TechnicalSpecificationReference).HasColumnName("technical_specification_reference").HasMaxLength(500);
            builder.Property(x => x.TrackingPolicy).HasColumnName("tracking_policy").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.InspectionRequired).HasColumnName("inspection_required");
            builder.Property(x => x.StorageCondition).HasColumnName("storage_condition").HasMaxLength(500);
            builder.Property(x => x.AcceptanceCriteria).HasColumnName("acceptance_criteria").HasMaxLength(1_000);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
            builder.Ignore(x => x.UnitConversions);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<InventoryLocation>(builder =>
        {
            builder.ToTable("inventory_locations"); ConfigureAggregate(builder);
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40);
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.AllowsAvailableStock).HasColumnName("allows_available_stock");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<GoodsReceipt>(builder =>
        {
            builder.ToTable("goods_receipts"); ConfigureAggregate(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
            builder.Property(x => x.PartyId).HasColumnName("party_id");
            builder.Property(x => x.ItemId).HasColumnName("item_id");
            builder.Property(x => x.StockLocationId).HasColumnName("stock_location_id");
            builder.Property(x => x.DispatchNote).HasColumnName("dispatch_note").HasMaxLength(120);
            builder.Property(x => x.ArrivedAt).HasColumnName("arrived_at");
            builder.Property(x => x.DeliveryLocation).HasColumnName("delivery_location").HasMaxLength(240);
            builder.Property(x => x.ShippedQuantity).HasColumnName("shipped_quantity").HasPrecision(24, 6);
            builder.Property(x => x.ReceivedQuantity).HasColumnName("received_quantity").HasPrecision(24, 6);
            builder.Property(x => x.BaseReceivedQuantity).HasColumnName("base_received_quantity").HasPrecision(24, 6);
            builder.Property(x => x.DamagedQuantity).HasColumnName("damaged_quantity").HasPrecision(24, 6);
            builder.Property(x => x.UnitCode).HasColumnName("unit_code").HasMaxLength(24);
            builder.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(24);
            builder.Property(x => x.ConversionVersion).HasColumnName("conversion_version");
            builder.Property(x => x.BatchOrLotReference).HasColumnName("batch_or_lot_reference").HasMaxLength(160);
            builder.Property(x => x.PackageCondition).HasColumnName("package_condition").HasMaxLength(500);
            builder.Property(x => x.ExcessApprovalReason).HasColumnName("excess_approval_reason").HasMaxLength(1_000);
            builder.Property(x => x.ExcessApprovedBy).HasColumnName("excess_approved_by");
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.AcceptedBaseQuantity).HasColumnName("accepted_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.RejectedBaseQuantity).HasColumnName("rejected_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.QuarantinedBaseQuantity).HasColumnName("quarantined_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.ReceivedBy).HasColumnName("received_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.InspectedBy).HasColumnName("inspected_by");
            builder.Property(x => x.InspectedAt).HasColumnName("inspected_at");
            builder.Property(x => x.InspectionType).HasColumnName("inspection_type").HasMaxLength(120);
            builder.Property(x => x.InspectionReference).HasColumnName("inspection_reference").HasMaxLength(500);
            builder.Property(x => x.InspectionComment).HasColumnName("inspection_comment").HasMaxLength(2_000);
            builder.Property(x => x.StockPostedAt).HasColumnName("stock_posted_at");
            builder.Ignore(x => x.EvidenceReferences);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.PurchaseOrderId, x.ItemId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<InventoryLedgerEntry>(builder =>
        {
            builder.ToTable("inventory_ledger");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
            builder.Property(x => x.ItemId).HasColumnName("item_id");
            builder.Property(x => x.LocationId).HasColumnName("location_id");
            builder.Property(x => x.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.BaseQuantityDelta).HasColumnName("base_quantity_delta").HasPrecision(24, 6);
            builder.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(24);
            builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(80);
            builder.Property(x => x.SourceId).HasColumnName("source_id");
            builder.Property(x => x.ReversesEntryId).HasColumnName("reverses_entry_id");
            builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1_000);
            builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
            builder.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ItemId, x.LocationId, x.OccurredAt });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.TransactionId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.SourceType, x.SourceId });
        });

        modelBuilder.Entity<MaterialIssue>(builder =>
        {
            builder.ToTable("material_issues"); ConfigureAggregate(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.ItemId).HasColumnName("item_id");
            builder.Property(x => x.SourceLocationId).HasColumnName("source_location_id");
            builder.Property(x => x.IssuedBaseQuantity).HasColumnName("issued_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(24);
            builder.Property(x => x.IssuedTo).HasColumnName("issued_to").HasMaxLength(240);
            builder.Property(x => x.DestinationLocation).HasColumnName("destination_location").HasMaxLength(240);
            builder.Property(x => x.WorkItemReference).HasColumnName("work_item_reference").HasMaxLength(240);
            builder.Property(x => x.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(x => x.PurchaseRequestId).HasColumnName("purchase_request_id");
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.ConsumedBaseQuantity).HasColumnName("consumed_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.ReturnedBaseQuantity).HasColumnName("returned_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.WasteBaseQuantity).HasColumnName("waste_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.IssuedBy).HasColumnName("issued_by");
            builder.Property(x => x.IssuedAt).HasColumnName("issued_at");
            builder.Property(x => x.AcknowledgedBy).HasColumnName("acknowledged_by");
            builder.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
            builder.Property(x => x.AcknowledgmentReference).HasColumnName("acknowledgment_reference").HasMaxLength(500);
            builder.Property(x => x.ReconciledAt).HasColumnName("reconciled_at");
            builder.Ignore(x => x.EvidenceReferences);
            builder.Ignore(x => x.UnreconciledBaseQuantity);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<MaterialReconciliationRecord>(builder =>
        {
            builder.ToTable("material_reconciliations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.MaterialIssueId).HasColumnName("material_issue_id");
            builder.Property(x => x.ConsumedBaseQuantity).HasColumnName("consumed_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.ReturnedBaseQuantity).HasColumnName("returned_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.WasteBaseQuantity).HasColumnName("waste_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(24);
            builder.Property(x => x.WasteReason).HasColumnName("waste_reason").HasMaxLength(1_000);
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
            builder.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            builder.Ignore(x => x.EvidenceReferences);
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.MaterialIssueId, x.RecordedAt });
        });

        modelBuilder.Entity<InventoryAdjustment>(builder =>
        {
            builder.ToTable("inventory_adjustments"); ConfigureAggregate(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.ItemId).HasColumnName("item_id");
            builder.Property(x => x.LocationId).HasColumnName("location_id");
            builder.Property(x => x.CutoffAt).HasColumnName("cutoff_at");
            builder.Property(x => x.SystemBaseQuantity).HasColumnName("system_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.CountedBaseQuantity).HasColumnName("counted_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(24);
            builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1_000);
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ProposedBy).HasColumnName("proposed_by");
            builder.Property(x => x.ProposedAt).HasColumnName("proposed_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(x => x.PostedAt).HasColumnName("posted_at");
            builder.Ignore(x => x.DeltaBaseQuantity);
            builder.Ignore(x => x.EvidenceReferences);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<ServiceAcceptance>(builder =>
        {
            builder.ToTable("service_acceptances"); ConfigureAggregate(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
            builder.Property(x => x.PartyId).HasColumnName("party_id");
            builder.Property(x => x.ItemId).HasColumnName("item_id");
            builder.Property(x => x.PeriodStart).HasColumnName("period_start");
            builder.Property(x => x.PeriodEnd).HasColumnName("period_end");
            builder.Property(x => x.DeliveredBaseQuantity).HasColumnName("delivered_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.AcceptedBaseQuantity).HasColumnName("accepted_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.RejectedBaseQuantity).HasColumnName("rejected_base_quantity").HasPrecision(24, 6);
            builder.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(24);
            builder.Property(x => x.AcceptanceCriteria).HasColumnName("acceptance_criteria").HasMaxLength(1_000);
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(2_000);
            builder.Property(x => x.VerifiedBy).HasColumnName("verified_by");
            builder.Property(x => x.VerifiedAt).HasColumnName("verified_at");
            builder.Ignore(x => x.EvidenceReferences);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.PurchaseOrderId, x.ItemId });
        });
    }
}
