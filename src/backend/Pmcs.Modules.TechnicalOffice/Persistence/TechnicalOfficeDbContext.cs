using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.TechnicalOffice.Domain;

namespace Pmcs.Modules.TechnicalOffice.Persistence;

internal sealed class TechnicalOfficeDbContext(DbContextOptions<TechnicalOfficeDbContext> options) : DbContext(options)
{
    public DbSet<TechnicalDocument> Documents => Set<TechnicalDocument>();
    public DbSet<TechnicalDocumentRevision> DocumentRevisions => Set<TechnicalDocumentRevision>();
    public DbSet<TechnicalTransmittal> Transmittals => Set<TechnicalTransmittal>();
    public DbSet<TechnicalRfi> Rfis => Set<TechnicalRfi>();
    public DbSet<TechnicalSubmittal> Submittals => Set<TechnicalSubmittal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("technical_office");
        ConfigureDocuments(modelBuilder);
        ConfigureTransmittals(modelBuilder);
        ConfigureRfis(modelBuilder);
        ConfigureSubmittals(modelBuilder);
    }

    private static void ConfigureDocuments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TechnicalDocument>(builder =>
        {
            builder.ToTable("documents");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(item => item.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(60);
            builder.Property(item => item.Discipline).HasColumnName("discipline").HasMaxLength(120);
            builder.Property(item => item.Originator).HasColumnName("originator").HasMaxLength(240);
            builder.Property(item => item.ContractId).HasColumnName("contract_id");
            builder.Property(item => item.LocationReference).HasColumnName("location_reference").HasMaxLength(240);
            builder.Property(item => item.WorkItemReference).HasColumnName("work_item_reference").HasMaxLength(240);
            builder.Property(item => item.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(item => item.Confidentiality).HasColumnName("confidentiality").HasMaxLength(80);
            builder.Property(item => item.CurrentOfficialRevisionId).HasColumnName("current_official_revision_id");
            builder.Property(item => item.CreatedBy).HasColumnName("created_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Number }).IsUnique();
        });

        modelBuilder.Entity<TechnicalDocumentRevision>(builder =>
        {
            builder.ToTable("document_revisions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.DocumentId).HasColumnName("document_id");
            builder.Property(item => item.RevisionCode).HasColumnName("revision_code").HasMaxLength(80);
            builder.Property(item => item.RevisionDate).HasColumnName("revision_date");
            builder.Property(item => item.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(60);
            builder.Property(item => item.FileName).HasColumnName("file_name").HasMaxLength(255);
            builder.Property(item => item.FileReference).HasColumnName("file_reference").HasMaxLength(700);
            builder.Property(item => item.Sha256).HasColumnName("sha256").HasMaxLength(64);
            builder.Property(item => item.SupersedesRevisionId).HasColumnName("supersedes_revision_id");
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.PreparedBy).HasColumnName("prepared_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(item => item.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(item => item.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(item => item.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(item => item.IssuedThroughTransmittalId).HasColumnName("issued_through_transmittal_id");
            builder.Property(item => item.IssuedAt).HasColumnName("issued_at");
            builder.Property(item => item.SupersededAt).HasColumnName("superseded_at");
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.DocumentId, item.RevisionCode }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        });
    }

    private static void ConfigureTransmittals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TechnicalTransmittal>(builder =>
        {
            builder.ToTable("transmittals");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(item => item.Sender).HasColumnName("sender").HasMaxLength(240);
            builder.Property(item => item.RecipientsJson).HasColumnName("recipients_json").HasColumnType("jsonb");
            builder.Property(item => item.RevisionIdsJson).HasColumnName("revision_ids_json").HasColumnType("jsonb");
            builder.Property(item => item.Purpose).HasColumnName("purpose").HasMaxLength(500);
            builder.Property(item => item.DeliveryChannel).HasColumnName("delivery_channel").HasMaxLength(120);
            builder.Property(item => item.DueResponseDate).HasColumnName("due_response_date");
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.CreatedBy).HasColumnName("created_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.IssuedAt).HasColumnName("issued_at");
            builder.Property(item => item.IssuedBy).HasColumnName("issued_by");
            builder.Property(item => item.AcknowledgedAt).HasColumnName("acknowledged_at");
            builder.Property(item => item.AcknowledgedBy).HasColumnName("acknowledged_by");
            builder.Property(item => item.AcknowledgmentReference).HasColumnName("acknowledgment_reference").HasMaxLength(500);
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.Recipients);
            builder.Ignore(item => item.RevisionIds);
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Number }).IsUnique();
        });
    }

    private static void ConfigureRfis(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TechnicalRfi>(builder =>
        {
            builder.ToTable("rfis");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(item => item.Question).HasColumnName("question").HasMaxLength(6_000);
            builder.Property(item => item.RequestedFrom).HasColumnName("requested_from").HasMaxLength(240);
            builder.Property(item => item.Discipline).HasColumnName("discipline").HasMaxLength(120);
            builder.Property(item => item.ContractId).HasColumnName("contract_id");
            builder.Property(item => item.LocationReference).HasColumnName("location_reference").HasMaxLength(240);
            builder.Property(item => item.WorkItemReference).HasColumnName("work_item_reference").HasMaxLength(240);
            builder.Property(item => item.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(item => item.SourceIssueId).HasColumnName("source_issue_id");
            builder.Property(item => item.RaisedDate).HasColumnName("raised_date");
            builder.Property(item => item.RequiredByDate).HasColumnName("required_by_date");
            builder.Property(item => item.PotentialImpact).HasColumnName("potential_impact").HasConversion<int>();
            builder.Property(item => item.IsBlocking).HasColumnName("is_blocking");
            builder.Property(item => item.ProposedSolution).HasColumnName("proposed_solution").HasMaxLength(4_000);
            builder.Property(item => item.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(item => item.RelatedRevisionIdsJson).HasColumnName("related_revision_ids_json").HasColumnType("jsonb");
            builder.Property(item => item.ResponseHistoryJson).HasColumnName("response_history_json").HasColumnType("jsonb");
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.RaisedBy).HasColumnName("raised_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(item => item.ClosedAt).HasColumnName("closed_at");
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.EvidenceReferences);
            builder.Ignore(item => item.RelatedRevisionIds);
            builder.Ignore(item => item.Responses);
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Number }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status, item.IsBlocking });
        });
    }

    private static void ConfigureSubmittals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TechnicalSubmittal>(builder =>
        {
            builder.ToTable("submittals");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(item => item.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(60);
            builder.Property(item => item.Discipline).HasColumnName("discipline").HasMaxLength(120);
            builder.Property(item => item.Submitter).HasColumnName("submitter").HasMaxLength(240);
            builder.Property(item => item.Reviewer).HasColumnName("reviewer").HasMaxLength(240);
            builder.Property(item => item.ContractId).HasColumnName("contract_id");
            builder.Property(item => item.CommitmentId).HasColumnName("commitment_id");
            builder.Property(item => item.LocationReference).HasColumnName("location_reference").HasMaxLength(240);
            builder.Property(item => item.WorkItemReference).HasColumnName("work_item_reference").HasMaxLength(240);
            builder.Property(item => item.WbsReference).HasColumnName("wbs_reference").HasMaxLength(240);
            builder.Property(item => item.RequiredByDate).HasColumnName("required_by_date");
            builder.Property(item => item.PlannedSubmissionDate).HasColumnName("planned_submission_date");
            builder.Property(item => item.ReviewDueDate).HasColumnName("review_due_date");
            builder.Property(item => item.ResubmissionNumber).HasColumnName("resubmission_number");
            builder.Property(item => item.SupersedesSubmittalId).HasColumnName("supersedes_submittal_id");
            builder.Property(item => item.RevisionIdsJson).HasColumnName("revision_ids_json").HasColumnType("jsonb");
            builder.Property(item => item.RequiredDeliverableReference).HasColumnName("required_deliverable_reference").HasMaxLength(500);
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.ReviewOutcome).HasColumnName("review_outcome").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.CreatedBy).HasColumnName("created_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(item => item.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(item => item.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(item => item.ReviewComment).HasColumnName("review_comment").HasMaxLength(2_000);
            builder.Property(item => item.ClosedAt).HasColumnName("closed_at");
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.RevisionIds);
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Number }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        });
    }
}
