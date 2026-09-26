using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.IdentityAccess.Migrations;

internal sealed class IdentityExperienceMigration : IDatabaseMigration
{
    public string ModuleName => "identity-access";

    public long Order => 204;

    public string Version => "20260918-002";

    public string Description => "Create member profiles and versioned login experiences";

    public string Sql => """
        create unique index if not exists ux_users_id_tenant
            on identity_access.users(id, tenant_id);

        create table if not exists identity_access.member_profiles (
            user_id uuid primary key,
            tenant_id uuid not null,
            job_title varchar(160) null,
            organization_unit varchar(160) null,
            work_phone varchar(40) null,
            avatar_document_id uuid null,
            avatar_crop_x numeric(6,5) null,
            avatar_crop_y numeric(6,5) null,
            avatar_crop_width numeric(6,5) null,
            avatar_crop_height numeric(6,5) null,
            created_at timestamptz not null,
            updated_at timestamptz not null,
            updated_by uuid null,
            revision bigint not null,
            constraint fk_member_profiles_user_tenant
                foreign key (user_id, tenant_id)
                references identity_access.users(id, tenant_id)
                on delete cascade,
            check (
                (avatar_document_id is null and avatar_crop_x is null and avatar_crop_y is null and
                    avatar_crop_width is null and avatar_crop_height is null) or
                (avatar_document_id is not null and avatar_crop_x between 0 and 1 and
                    avatar_crop_y between 0 and 1 and avatar_crop_width > 0 and avatar_crop_width <= 1 and
                    avatar_crop_height > 0 and avatar_crop_height <= 1 and
                    avatar_crop_x + avatar_crop_width <= 1 and
                    avatar_crop_y + avatar_crop_height <= 1)
            )
        );
        create index if not exists ix_member_profiles_tenant
            on identity_access.member_profiles(tenant_id, user_id);

        insert into identity_access.member_profiles (
            user_id, tenant_id, created_at, updated_at, revision)
        select id, tenant_id, created_at, created_at, 1
        from identity_access.users
        on conflict (user_id) do nothing;

        create or replace function identity_access.ensure_member_profile()
        returns trigger
        language plpgsql
        as $function$
        begin
            insert into identity_access.member_profiles (
                user_id, tenant_id, created_at, updated_at, revision)
            values (new.id, new.tenant_id, new.created_at, new.created_at, 1)
            on conflict (user_id) do nothing;
            return new;
        end;
        $function$;

        drop trigger if exists trg_users_ensure_member_profile on identity_access.users;
        create trigger trg_users_ensure_member_profile
        after insert on identity_access.users
        for each row execute function identity_access.ensure_member_profile();

        create table if not exists identity_access.login_experiences (
            id uuid primary key,
            tenant_id uuid not null,
            version_number integer not null check (version_number > 0),
            status varchar(40) not null,
            composition_variant varchar(60) not null,
            surface_tone varchar(60) not null,
            accent_palette varchar(60) not null,
            motion_policy varchar(40) not null,
            eyebrow varchar(80) not null,
            headline varchar(140) not null,
            supporting_text varchar(320) not null,
            logo_document_id uuid null,
            hero_document_id uuid null,
            created_by uuid not null,
            created_at timestamptz not null,
            published_by uuid null,
            published_at timestamptz null,
            revision bigint not null,
            unique (tenant_id, version_number),
            constraint fk_login_experiences_creator_tenant
                foreign key (created_by, tenant_id)
                references identity_access.users(id, tenant_id),
            constraint fk_login_experiences_publisher_tenant
                foreign key (published_by, tenant_id)
                references identity_access.users(id, tenant_id),
            check (status in ('Draft', 'Published', 'Superseded')),
            check (composition_variant in ('BlueprintSplit', 'MonolithFocus', 'WarmMinimal')),
            check (surface_tone in ('WarmStone', 'WarmIvory', 'DeepNavy')),
            check (accent_palette in ('CorporateNavyGreen', 'NavySilver', 'GreenStone')),
            check (motion_policy in ('Calm', 'Balanced', 'Expressive')),
            check (
                (status = 'Draft' and published_by is null and published_at is null) or
                (status in ('Published', 'Superseded') and published_by is not null and published_at is not null)
            )
        );
        create unique index if not exists ux_login_experiences_published_tenant
            on identity_access.login_experiences(tenant_id)
            where status = 'Published';
        create index if not exists ix_login_experiences_tenant_version
            on identity_access.login_experiences(tenant_id, version_number desc);
        """;
}
