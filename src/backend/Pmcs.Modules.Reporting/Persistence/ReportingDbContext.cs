using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Persistence;

internal sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<ReportDefinitionRecord> Definitions => Set<ReportDefinitionRecord>();
    public DbSet<ReportTemplateVersionRecord> TemplateVersions => Set<ReportTemplateVersionRecord>();
    public DbSet<ReportRun> Runs => Set<ReportRun>();
    public DbSet<ReportSnapshot> Snapshots => Set<ReportSnapshot>();
    public DbSet<ReportOutput> Outputs => Set<ReportOutput>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("reporting");

        modelBuilder.Entity<ReportDefinitionRecord>(builder =>
        {
            builder.ToTable("report_definitions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(120);
            builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(200);
            builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(1_000);
            builder.Property(item => item.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.SupportedFormatsJson).HasColumnName("supported_formats").HasColumnType("jsonb");
            builder.Property(item => item.RequiredPermissionsJson).HasColumnName("required_permissions").HasColumnType("jsonb");
            builder.Property(item => item.ParameterSchemaVersion).HasColumnName("parameter_schema_version").HasMaxLength(80);
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.CurrentTemplateVersionId).HasColumnName("current_template_version_id");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            builder.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<ReportTemplateVersionRecord>(builder =>
        {
            builder.ToTable("report_template_versions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.DefinitionId).HasColumnName("definition_id");
            builder.Property(item => item.Version).HasColumnName("version").HasMaxLength(40);
            builder.Property(item => item.RendererContractVersion).HasColumnName("renderer_contract_version").HasMaxLength(80);
            builder.Property(item => item.LayoutContractVersion).HasColumnName("layout_contract_version").HasMaxLength(80);
            builder.Property(item => item.ContentDigest).HasColumnName("content_digest").HasMaxLength(64);
            builder.Property(item => item.PublishedAt).HasColumnName("published_at");
            builder.Property(item => item.RetiredAt).HasColumnName("retired_at");
            builder.Property(item => item.PageSize).HasColumnName("page_size").HasMaxLength(20);
            builder.Property(item => item.Orientation).HasColumnName("orientation").HasMaxLength(20);
            builder.Property(item => item.Locale).HasColumnName("locale").HasMaxLength(20);
            builder.Property(item => item.Calendar).HasColumnName("calendar").HasMaxLength(20);
            builder.HasIndex(item => new { item.DefinitionId, item.Version }).IsUnique();
        });

        modelBuilder.Entity<ReportRun>(builder =>
        {
            builder.ToTable("report_runs");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.DefinitionId).HasColumnName("definition_id");
            builder.Property(item => item.DefinitionCode).HasColumnName("definition_code").HasMaxLength(120);
            builder.Property(item => item.TemplateVersionId).HasColumnName("template_version_id");
            builder.Property(item => item.TemplateVersion).HasColumnName("template_version").HasMaxLength(40);
            builder.Property(item => item.ParametersJson).HasColumnName("parameters_json").HasColumnType("jsonb");
            builder.Property(item => item.ParametersHash).HasColumnName("parameters_hash").HasMaxLength(64);
            builder.Property(item => item.RequestedFormatsJson).HasColumnName("requested_formats").HasColumnType("jsonb");
            builder.Property(item => item.AsOfUtc).HasColumnName("as_of_utc");
            builder.Property(item => item.ProjectTimeZone).HasColumnName("project_time_zone").HasMaxLength(120);
            builder.Property(item => item.PinnedProjectProfileJson)
                .HasColumnName("pinned_project_profile")
                .HasColumnType("jsonb");
            builder.Property(item => item.RequestedBy).HasColumnName("requested_by");
            builder.Property(item => item.RequestPermissionSnapshotJson).HasColumnName("request_permission_snapshot").HasColumnType("jsonb");
            builder.Property(item => item.ProcessingPermissionSnapshotJson).HasColumnName("processing_permission_snapshot").HasColumnType("jsonb");
            builder.Property(item => item.CorrelationId).HasColumnName("correlation_id").HasMaxLength(160);
            builder.Property(item => item.IdempotencyKeyHash).HasColumnName("idempotency_key_hash").HasMaxLength(64);
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.PipelineStage).HasColumnName("pipeline_stage").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.AttemptCount).HasColumnName("attempt_count");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.ClaimedAt).HasColumnName("claimed_at");
            builder.Property(item => item.StartedAt).HasColumnName("started_at");
            builder.Property(item => item.CompletedAt).HasColumnName("completed_at");
            builder.Property(item => item.NextAttemptAt).HasColumnName("next_attempt_at");
            builder.Property(item => item.DiagnosticCode).HasColumnName("diagnostic_code").HasMaxLength(120);
            builder.Property(item => item.DiagnosticDetail).HasColumnName("diagnostic_detail").HasMaxLength(500);
            builder.Property(item => item.SnapshotId).HasColumnName("snapshot_id");
            builder.Property(item => item.OutputCount).HasColumnName("output_count");
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.CreatedAt });
            builder.HasIndex(item => new { item.TenantId, item.IdempotencyKeyHash }).IsUnique();
        });

        modelBuilder.Entity<ReportSnapshot>(builder =>
        {
            builder.ToTable("report_snapshots");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.RunId).HasColumnName("run_id");
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.SchemaVersion).HasColumnName("schema_version").HasMaxLength(80);
            builder.Property(item => item.DataStatus).HasColumnName("data_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
            builder.Property(item => item.SourceManifestJson).HasColumnName("source_manifest_json").HasColumnType("jsonb");
            builder.Property(item => item.Sha256).HasColumnName("sha256").HasMaxLength(64);
            builder.Property(item => item.SourceManifestSha256).HasColumnName("source_manifest_sha256").HasMaxLength(64);
            builder.Property(item => item.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.BuiltAt).HasColumnName("built_at");
            builder.Property(item => item.SourceCutoffUtc).HasColumnName("source_cutoff_utc");
            builder.HasIndex(item => item.RunId).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.BuiltAt });
        });

        modelBuilder.Entity<ReportOutput>(builder =>
        {
            builder.ToTable("report_outputs");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.RunId).HasColumnName("run_id");
            builder.Property(item => item.SnapshotId).HasColumnName("snapshot_id");
            builder.Property(item => item.TemplateVersionId).HasColumnName("template_version_id");
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Format).HasColumnName("format").HasConversion<string>().HasMaxLength(20);
            builder.Property(item => item.ContentType).HasColumnName("content_type").HasMaxLength(160);
            builder.Property(item => item.FileName).HasColumnName("file_name").HasMaxLength(255);
            builder.Property(item => item.GeneratedDocumentId).HasColumnName("generated_document_id");
            builder.Property(item => item.SizeBytes).HasColumnName("size_bytes");
            builder.Property(item => item.Sha256).HasColumnName("sha256").HasMaxLength(64);
            builder.Property(item => item.VerificationCode).HasColumnName("verification_code").HasMaxLength(80);
            builder.Property(item => item.ManifestSha256).HasColumnName("manifest_sha256").HasMaxLength(64);
            builder.Property(item => item.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.RetentionPolicy).HasColumnName("retention_policy").HasMaxLength(40);
            builder.Property(item => item.ArchiveState).HasColumnName("archive_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.ArchivedAt).HasColumnName("archived_at");
            builder.HasIndex(item => new { item.RunId, item.Format }).IsUnique();
            builder.HasIndex(item => item.GeneratedDocumentId).IsUnique();
            builder.HasIndex(item => item.VerificationCode);
        });
    }
}
