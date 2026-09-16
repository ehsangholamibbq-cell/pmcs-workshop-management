using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Finance.Domain;

namespace Pmcs.Modules.Finance.Persistence;

internal sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<FinancialRecord> FinancialRecords => Set<FinancialRecord>();

    public DbSet<BudgetBaseline> BudgetBaselines => Set<BudgetBaseline>();

    public DbSet<FinancialStateSnapshot> FinancialStateSnapshots => Set<FinancialStateSnapshot>();

    public DbSet<FinancialObligation> FinancialObligations => Set<FinancialObligation>();

    public DbSet<FinancialSettlement> FinancialSettlements => Set<FinancialSettlement>();

    public DbSet<PettyCashRequest> PettyCashRequests => Set<PettyCashRequest>();

    public DbSet<ManagementFeePolicy> ManagementFeePolicies => Set<ManagementFeePolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.Entity<FinancialRecord>(builder =>
        {
            builder.ToTable("financial_records");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.TransactionDate).HasColumnName("transaction_date");
            builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1_000);
            builder.Property(x => x.Counterparty).HasColumnName("counterparty").HasMaxLength(200);
            builder.Property(x => x.DocumentNumber).HasColumnName("document_number").HasMaxLength(120);
            builder.Property(x => x.ContractReference).HasColumnName("contract_reference").HasMaxLength(120);
            builder.Property(x => x.ContractId).HasColumnName("contract_id");
            builder.Property(x => x.CommitmentId).HasColumnName("commitment_id");
            builder.Property(x => x.CostCenterCode).HasColumnName("cost_center_code").HasMaxLength(120);
            builder.Property(x => x.PartyId).HasColumnName("party_id");
            builder.Property(x => x.LocationId).HasColumnName("location_id");
            builder.Property(x => x.LocationCode).HasColumnName("location_code").HasMaxLength(80);
            builder.Property(x => x.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.TransactionDate });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ContractId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.CommitmentId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.PartyId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.LocationId });
        });

        modelBuilder.Entity<BudgetBaseline>(builder =>
        {
            builder.ToTable("budget_baselines");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
            builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2_000);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<FinancialStateSnapshot>(builder =>
        {
            builder.ToTable("financial_state_snapshots");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.CalculationVersion).HasColumnName("calculation_version").HasMaxLength(80);
            builder.Property(x => x.AsOfDate).HasColumnName("as_of_date");
            builder.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.DataQualityStatus).HasColumnName("data_quality_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.BudgetComparisonState).HasColumnName("budget_comparison_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.PostedRecordCount).HasColumnName("posted_record_count");
            builder.Property(x => x.TotalReceipts).HasColumnName("total_receipts").HasPrecision(24, 2);
            builder.Property(x => x.DirectPayments).HasColumnName("direct_payments").HasPrecision(24, 2);
            builder.Property(x => x.PettyCashFunding).HasColumnName("petty_cash_funding").HasPrecision(24, 2);
            builder.Property(x => x.PettyCashExpenses).HasColumnName("petty_cash_expenses").HasPrecision(24, 2);
            builder.Property(x => x.ExternalNetCash).HasColumnName("external_net_cash").HasPrecision(24, 2);
            builder.Property(x => x.RecognizedSpend).HasColumnName("recognized_spend").HasPrecision(24, 2);
            builder.Property(x => x.PettyCashBalance).HasColumnName("petty_cash_balance").HasPrecision(24, 2);
            builder.Property(x => x.ApprovedBudgetAmount).HasColumnName("approved_budget_amount").HasPrecision(24, 2);
            builder.Property(x => x.BudgetRemainingAmount).HasColumnName("budget_remaining_amount").HasPrecision(24, 2);
            builder.Property(x => x.BudgetConsumedPercent).HasColumnName("budget_consumed_percent").HasPrecision(9, 1);
            builder.Property(x => x.SourceMaxChangedAt).HasColumnName("source_max_changed_at");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.CalculatedAt });
        });

        modelBuilder.Entity<FinancialObligation>(builder =>
        {
            builder.ToTable("financial_obligations");
            ConfigureAggregate(builder);
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1_000);
            builder.Property(x => x.IssueDate).HasColumnName("issue_date");
            builder.Property(x => x.DueDate).HasColumnName("due_date");
            builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(24, 2);
            builder.Property(x => x.SettledAmount).HasColumnName("settled_amount").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.PartyId).HasColumnName("party_id");
            builder.Property(x => x.Counterparty).HasColumnName("counterparty").HasMaxLength(200);
            builder.Property(x => x.ContractId).HasColumnName("contract_id");
            builder.Property(x => x.CommitmentId).HasColumnName("commitment_id");
            builder.Property(x => x.CostCenterCode).HasColumnName("cost_center_code").HasMaxLength(120);
            builder.Property(x => x.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(x => x.LocationId).HasColumnName("location_id");
            builder.Property(x => x.LocationCode).HasColumnName("location_code").HasMaxLength(80);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(x => x.SettledAt).HasColumnName("settled_at");
            builder.Ignore(x => x.OutstandingAmount);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.DueDate });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.LocationId });
        });

        modelBuilder.Entity<FinancialSettlement>(builder =>
        {
            builder.ToTable("financial_settlements");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.ObligationId).HasColumnName("obligation_id");
            builder.Property(x => x.FinancialRecordId).HasColumnName("financial_record_id");
            builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(24, 2);
            builder.Property(x => x.SettledAt).HasColumnName("settled_at");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ObligationId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.FinancialRecordId });
        });

        modelBuilder.Entity<PettyCashRequest>(builder =>
        {
            builder.ToTable("petty_cash_requests");
            ConfigureAggregate(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(1_000);
            builder.Property(x => x.Custodian).HasColumnName("custodian").HasMaxLength(200);
            builder.Property(x => x.RequestDate).HasColumnName("request_date");
            builder.Property(x => x.ReconciliationDueDate).HasColumnName("reconciliation_due_date");
            builder.Property(x => x.RequestedAmount).HasColumnName("requested_amount").HasPrecision(24, 2);
            builder.Property(x => x.ApprovedAmount).HasColumnName("approved_amount").HasPrecision(24, 2);
            builder.Property(x => x.ReconciledExpenseAmount).HasColumnName("reconciled_expense_amount").HasPrecision(24, 2);
            builder.Property(x => x.ReturnedAmount).HasColumnName("returned_amount").HasPrecision(24, 2);
            builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
            builder.Property(x => x.LocationId).HasColumnName("location_id");
            builder.Property(x => x.LocationCode).HasColumnName("location_code").HasMaxLength(80);
            builder.Property(x => x.CostCenterCode).HasColumnName("cost_center_code").HasMaxLength(120);
            builder.Property(x => x.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(x => x.AdvanceRecordId).HasColumnName("advance_record_id");
            builder.Property(x => x.ExpenseRecordId).HasColumnName("expense_record_id");
            builder.Property(x => x.ReturnRecordId).HasColumnName("return_record_id");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(x => x.AdvancedAt).HasColumnName("advanced_at");
            builder.Property(x => x.ReconciliationSubmittedAt).HasColumnName("reconciliation_submitted_at");
            builder.Property(x => x.ReconciledAt).HasColumnName("reconciled_at");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.ReconciliationDueDate });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.LocationId });
        });

        modelBuilder.Entity<ManagementFeePolicy>(builder =>
        {
            builder.ToTable("management_fee_policies");
            ConfigureAggregate(builder);
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
            builder.Property(x => x.RatePercent).HasColumnName("rate_percent").HasPrecision(9, 4);
            builder.Property(x => x.CalculationBase).HasColumnName("calculation_base").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
            builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2_000);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
        });
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
