using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Collaboration.Domain;

namespace Pmcs.Modules.Collaboration.Persistence;

internal sealed class CollaborationDbContext(DbContextOptions<CollaborationDbContext> options) : DbContext(options)
{
    public DbSet<ProjectRoom> Rooms => Set<ProjectRoom>();
    public DbSet<ProjectMessage> Messages => Set<ProjectMessage>();
    public DbSet<ProjectReaction> Reactions => Set<ProjectReaction>();
    public DbSet<ProjectReadCursor> ReadCursors => Set<ProjectReadCursor>();
    public DbSet<ProjectMessageAttachment> Attachments => Set<ProjectMessageAttachment>();
    public DbSet<ProjectMessageRevision> Revisions => Set<ProjectMessageRevision>();
    public DbSet<ProjectModerationRecord> ModerationRecords => Set<ProjectModerationRecord>();
    public DbSet<ProjectMessageConversion> Conversions => Set<ProjectMessageConversion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("collaboration");
        modelBuilder.Entity<ProjectRoom>(builder =>
        {
            builder.ToTable("project_rooms");
            builder.HasKey(item => item.ProjectId);
            builder.Property(item => item.ProjectId).HasColumnName("project_id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.LastSequence).HasColumnName("last_sequence");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        });
        modelBuilder.Entity<ProjectMessage>(builder =>
        {
            builder.ToTable("messages");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Sequence).HasColumnName("sequence");
            builder.Property(item => item.AuthorUserId).HasColumnName("author_user_id");
            builder.Property(item => item.ClientMessageId).HasColumnName("client_message_id");
            builder.Property(item => item.Body).HasColumnName("body").HasMaxLength(4_000);
            builder.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.ReplyToMessageId).HasColumnName("reply_to_message_id");
            builder.Property(item => item.MentionedUserIds).HasColumnName("mentioned_user_ids").HasColumnType("uuid[]");
            builder.Property(item => item.PinnedAt).HasColumnName("pinned_at");
            builder.Property(item => item.PinnedBy).HasColumnName("pinned_by");
            builder.Property(item => item.Revision).HasColumnName("revision");
            builder.Property(item => item.EditedAt).HasColumnName("edited_at");
            builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
            builder.Property(item => item.RedactedAt).HasColumnName("redacted_at");
            builder.Property(item => item.LegalHold).HasColumnName("legal_hold");
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Sequence }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.AuthorUserId, item.ClientMessageId }).IsUnique();
        });
        modelBuilder.Entity<ProjectReaction>(builder =>
        {
            builder.ToTable("reactions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.MessageId).HasColumnName("message_id");
            builder.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
            builder.Property(item => item.Emoji).HasColumnName("emoji").HasMaxLength(16);
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.MessageId,
                item.ActorUserId, item.Emoji }).IsUnique();
        });
        modelBuilder.Entity<ProjectReadCursor>(builder =>
        {
            builder.ToTable("read_cursors");
            builder.HasKey(item => new { item.TenantId, item.ProjectId, item.UserId });
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.UserId).HasColumnName("user_id");
            builder.Property(item => item.LastReadSequence).HasColumnName("last_read_sequence");
            builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });
        modelBuilder.Entity<ProjectMessageAttachment>(builder =>
        {
            builder.ToTable("message_attachments");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.MessageId).HasColumnName("message_id");
            builder.Property(item => item.DocumentId).HasColumnName("document_id");
            builder.Property(item => item.DocumentSha256).HasColumnName("document_sha256").HasMaxLength(64);
            builder.Property(item => item.DocumentVersion).HasColumnName("document_version");
            builder.Property(item => item.AttachedBy).HasColumnName("attached_by");
            builder.Property(item => item.AttachedAt).HasColumnName("attached_at");
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.DocumentId }).IsUnique();
        });
        modelBuilder.Entity<ProjectMessageRevision>(builder =>
        {
            builder.ToTable("message_revisions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.MessageId).HasColumnName("message_id");
            builder.Property(item => item.FromRevision).HasColumnName("from_revision");
            builder.Property(item => item.Body).HasColumnName("body").HasMaxLength(4_000);
            builder.Property(item => item.Action).HasColumnName("action").HasMaxLength(32);
            builder.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
            builder.Property(item => item.OccurredAt).HasColumnName("occurred_at");
            builder.HasIndex(item => new { item.TenantId, item.ProjectId,
                item.MessageId, item.FromRevision }).IsUnique();
        });
        modelBuilder.Entity<ProjectModerationRecord>(builder =>
        {
            builder.ToTable("moderation_records");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.MessageId).HasColumnName("message_id");
            builder.Property(item => item.MessageRevision).HasColumnName("message_revision");
            builder.Property(item => item.Action).HasColumnName("action").HasMaxLength(32);
            builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(500);
            builder.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
            builder.Property(item => item.OccurredAt).HasColumnName("occurred_at");
        });
        modelBuilder.Entity<ProjectMessageConversion>(builder =>
        {
            builder.ToTable("message_conversions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.MessageId).HasColumnName("message_id");
            builder.Property(item => item.MessageRevision).HasColumnName("message_revision");
            builder.Property(item => item.DestinationType).HasColumnName("destination_type").HasMaxLength(40);
            builder.Property(item => item.DestinationId).HasColumnName("destination_id");
            builder.Property(item => item.DestinationReference).HasColumnName("destination_reference").HasMaxLength(500);
            builder.Property(item => item.DocumentReferencesJson).HasColumnName("document_references_json").HasColumnType("jsonb");
            builder.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
            builder.Property(item => item.ConfirmedBy).HasColumnName("confirmed_by");
            builder.Property(item => item.ConfirmedAt).HasColumnName("confirmed_at");
            builder.HasIndex(item => new { item.TenantId, item.ProjectId,
                item.DestinationType, item.DestinationId }).IsUnique();
        });
    }
}
