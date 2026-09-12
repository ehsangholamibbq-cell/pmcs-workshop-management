using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Modules.Commercial.Persistence;

internal sealed partial class CommercialDbContext(DbContextOptions<CommercialDbContext> options) : DbContext(options)
{
    public DbSet<Party> Parties => Set<Party>();

    public DbSet<ProjectContract> Contracts => Set<ProjectContract>();

    public DbSet<ContractAmendment> ContractAmendments => Set<ContractAmendment>();

    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<CommercialStateSnapshot> CommercialStateSnapshots => Set<CommercialStateSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("commercial");

        modelBuilder.Entity<Party>(builder =>
        {
            builder.ToTable("parties");
            ConfigureAggregate(builder);
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32);
            builder.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(200);
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.NationalId).HasColumnName("national_id").HasMaxLength(50);
            builder.Property(x => x.ContactName).HasColumnName("contact_name").HasMaxLength(160);
            builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ProjectContract>(builder =>
        {
            builder.ToTable("contracts");
            ConfigureAggregate(builder);
            builder.Property(x => x.PartyId).HasColumnName("party_id");
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.OriginalApprovedAmount).HasColumnName("original_approved_amount").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.StartDate).HasColumnName("start_date");
            builder.Property(x => x.EndDate).HasColumnName("end_date");
            builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2_000);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<ContractAmendment>(builder =>
        {
            builder.ToTable("contract_amendments");
            ConfigureAggregate(builder);
            builder.Property(x => x.ContractId).HasColumnName("contract_id");
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.AmountDelta).HasColumnName("amount_delta").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.ExtensionDays).HasColumnName("extension_days");
            builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2_000);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ContractId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<PurchaseRequest>(builder =>
        {
            builder.ToTable("purchase_requests");
            ConfigureAggregate(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2_000);
            builder.Property(x => x.EstimatedAmount).HasColumnName("estimated_amount").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.NeededByDate).HasColumnName("needed_by_date");
            builder.Property(x => x.SupplyItemId).HasColumnName("supply_item_id");
            builder.Property(x => x.RequestedQuantity).HasColumnName("requested_quantity").HasPrecision(24, 6);
            builder.Property(x => x.UnitCode).HasColumnName("unit_code").HasMaxLength(24);
            builder.Property(x => x.DeliveryLocation).HasColumnName("delivery_location").HasMaxLength(240);
            builder.Property(x => x.WorkItemReference).HasColumnName("work_item_reference").HasMaxLength(240);
            builder.Property(x => x.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(x => x.BudgetLineReference).HasColumnName("budget_line_reference").HasMaxLength(240);
            builder.Property(x => x.BudgetCheckStatus).HasColumnName("budget_check_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Criticality).HasColumnName("criticality").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.RequestedBy).HasColumnName("requested_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<PurchaseOrder>(builder =>
        {
            builder.ToTable("purchase_orders");
            ConfigureAggregate(builder);
            builder.Property(x => x.PurchaseRequestId).HasColumnName("purchase_request_id");
            builder.Property(x => x.PartyId).HasColumnName("party_id");
            builder.Property(x => x.ContractId).HasColumnName("contract_id");
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.DeliveryDueDate).HasColumnName("delivery_due_date");
            builder.Property(x => x.SupplyItemId).HasColumnName("supply_item_id");
            builder.Property(x => x.OrderedQuantity).HasColumnName("ordered_quantity").HasPrecision(24, 6);
            builder.Property(x => x.UnitCode).HasColumnName("unit_code").HasMaxLength(24);
            builder.Property(x => x.DeliveryLocation).HasColumnName("delivery_location").HasMaxLength(240);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.IssuedBy).HasColumnName("issued_by");
            builder.Property(x => x.IssuedAt).HasColumnName("issued_at");
            builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
            builder.Property(x => x.ClosedBy).HasColumnName("closed_by");
            builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
            builder.Property(x => x.ClosureComment).HasColumnName("closure_comment").HasMaxLength(1_000);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.PurchaseRequestId }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<CommercialStateSnapshot>(builder =>
        {
            builder.ToTable("commercial_state_snapshots");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.CalculationVersion).HasColumnName("calculation_version").HasMaxLength(80);
            builder.Property(x => x.AsOfDate).HasColumnName("as_of_date");
            builder.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.ContractState).HasColumnName("contract_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ContractDataQualityStatus).HasColumnName("contract_data_quality_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ProcurementState).HasColumnName("procurement_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ProcurementDataQualityStatus).HasColumnName("procurement_data_quality_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ActivePartyCount).HasColumnName("active_party_count");
            builder.Property(x => x.RegisteredContractCount).HasColumnName("registered_contract_count");
            builder.Property(x => x.ActiveContractCount).HasColumnName("active_contract_count");
            builder.Property(x => x.ContractsWithoutCeilingCount).HasColumnName("contracts_without_ceiling_count");
            builder.Property(x => x.PendingContractApprovalCount).HasColumnName("pending_contract_approval_count");
            builder.Property(x => x.ExpiredActiveContractCount).HasColumnName("expired_active_contract_count");
            builder.Property(x => x.ApprovedAmendmentCount).HasColumnName("approved_amendment_count");
            builder.Property(x => x.ApprovedAmendmentDelta).HasColumnName("approved_amendment_delta").HasPrecision(24, 2);
            builder.Property(x => x.ApprovedContractCeilingAmount).HasColumnName("approved_contract_ceiling_amount").HasPrecision(24, 2);
            builder.Property(x => x.PurchaseRequestCount).HasColumnName("purchase_request_count");
            builder.Property(x => x.PendingProcurementApprovalCount).HasColumnName("pending_procurement_approval_count");
            builder.Property(x => x.ApprovedRequestsAwaitingOrderCount).HasColumnName("approved_requests_awaiting_order_count");
            builder.Property(x => x.OpenCommitmentCount).HasColumnName("open_commitment_count");
            builder.Property(x => x.OverdueCommitmentCount).HasColumnName("overdue_commitment_count");
            builder.Property(x => x.TotalCommittedAmount).HasColumnName("total_committed_amount").HasPrecision(24, 2);
            builder.Property(x => x.OpenCommitmentAmount).HasColumnName("open_commitment_amount").HasPrecision(24, 2);
            builder.Property(x => x.SourceMaxChangedAt).HasColumnName("source_max_changed_at");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.CalculatedAt });
        });

        ConfigureSupplyReality(modelBuilder);
    }

    private static void ConfigureAggregate<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : Pmcs.BuildingBlocks.Domain.AggregateRoot
    {
        builder.HasKey("Id");
        builder.Property<Guid>("Id").HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("TenantId").HasColumnName("tenant_id");
        builder.Property<Guid>("ProjectId").HasColumnName("project_id");
        builder.Property<long>("Revision").HasColumnName("revision").IsConcurrencyToken();
        builder.Ignore("DomainEvents");
    }
}
