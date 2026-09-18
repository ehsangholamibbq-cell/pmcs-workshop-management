using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.IdentityAccess.Domain;

namespace Pmcs.Modules.IdentityAccess.Persistence;

internal sealed class IdentityAccessDbContext(DbContextOptions<IdentityAccessDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<InvitationProjectAssignment> InvitationProjectAssignments => Set<InvitationProjectAssignment>();

    public DbSet<IdentityProviderOperation> IdentityProviderOperations => Set<IdentityProviderOperation>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();

    public DbSet<LoginExperience> LoginExperiences => Set<LoginExperience>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity_access");

        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.ToTable("tenants");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<UserAccount>(builder =>
        {
            builder.ToTable("users");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            builder.Property(x => x.TenantRole).HasColumnName("tenant_role").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.AccessValidAfter).HasColumnName("access_valid_after");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        });

        modelBuilder.Entity<ProjectMembership>(builder =>
        {
            builder.ToTable("project_memberships");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(100);
            builder.Property(x => x.StartsAt).HasColumnName("starts_at");
            builder.Property(x => x.EndsAt).HasColumnName("ends_at");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<UserInvitation>(builder =>
        {
            builder.ToTable("user_invitations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            builder.Property(x => x.TenantRole).HasColumnName("tenant_role").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.Property(x => x.SentAt).HasColumnName("sent_at");
            builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.ProviderUserId).HasColumnName("provider_user_id");
            builder.Property(x => x.Attempts).HasColumnName("attempts");
            builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(120);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.Email });
            builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
            builder.HasIndex(x => x.ProviderUserId);
        });

        modelBuilder.Entity<InvitationProjectAssignment>(builder =>
        {
            builder.ToTable("invitation_project_assignments");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.InvitationId).HasColumnName("invitation_id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(100);
            builder.HasIndex(x => new { x.InvitationId, x.ProjectId }).IsUnique();
        });

        modelBuilder.Entity<IdentityProviderOperation>(builder =>
        {
            builder.ToTable("identity_provider_operations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.OperationType).HasColumnName("operation_type").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
            builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            builder.Property(x => x.Attempts).HasColumnName("attempts");
            builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(120);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
            builder.HasIndex(x => new { x.TenantId, x.UserId });
        });

        modelBuilder.Entity<MemberProfile>(builder =>
        {
            builder.ToTable("member_profiles");
            builder.HasKey(x => x.UserId);
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.JobTitle).HasColumnName("job_title").HasMaxLength(160);
            builder.Property(x => x.OrganizationUnit).HasColumnName("organization_unit").HasMaxLength(160);
            builder.Property(x => x.WorkPhone).HasColumnName("work_phone").HasMaxLength(40);
            builder.Property(x => x.AvatarDocumentId).HasColumnName("avatar_document_id");
            builder.Property(x => x.AvatarCropX).HasColumnName("avatar_crop_x").HasPrecision(6, 5);
            builder.Property(x => x.AvatarCropY).HasColumnName("avatar_crop_y").HasPrecision(6, 5);
            builder.Property(x => x.AvatarCropWidth).HasColumnName("avatar_crop_width").HasPrecision(6, 5);
            builder.Property(x => x.AvatarCropHeight).HasColumnName("avatar_crop_height").HasPrecision(6, 5);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.UserId });
        });

        modelBuilder.Entity<LoginExperience>(builder =>
        {
            builder.ToTable("login_experiences");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.VersionNumber).HasColumnName("version_number");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CompositionVariant).HasColumnName("composition_variant").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.SurfaceTone).HasColumnName("surface_tone").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.AccentPalette).HasColumnName("accent_palette").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.MotionPolicy).HasColumnName("motion_policy").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Eyebrow).HasColumnName("eyebrow").HasMaxLength(80);
            builder.Property(x => x.Headline).HasColumnName("headline").HasMaxLength(140);
            builder.Property(x => x.SupportingText).HasColumnName("supporting_text").HasMaxLength(320);
            builder.Property(x => x.LogoDocumentId).HasColumnName("logo_document_id");
            builder.Property(x => x.HeroDocumentId).HasColumnName("hero_document_id");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.PublishedBy).HasColumnName("published_by");
            builder.Property(x => x.PublishedAt).HasColumnName("published_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.VersionNumber }).IsUnique();
        });
    }
}
