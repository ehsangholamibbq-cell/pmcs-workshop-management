using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceModelProfileMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 602;
    public string Version => "20261001-002";
    public string Description => "Create versioned model catalog and immutable execution profiles";

    public string Sql => """
        create table if not exists intelligence.model_catalog (
            id uuid primary key,
            version integer not null,
            provider varchar(40) not null,
            model varchar(160) not null,
            capabilities integer not null,
            maximum_data_class integer not null,
            enabled boolean not null,
            verified_at timestamptz null,
            created_at timestamptz not null,
            created_by uuid not null,
            revision bigint not null,
            constraint ux_intelligence_model_version unique (provider, model, version),
            constraint ck_intelligence_model_capabilities check (capabilities between 1 and 3),
            constraint ck_intelligence_model_class check (maximum_data_class between 1 and 3),
            constraint ck_intelligence_model_verified check (not enabled or verified_at is not null)
        );

        create table if not exists intelligence.profile_versions (
            id uuid primary key,
            version integer not null,
            use_case varchar(100) not null,
            tenant_id uuid not null,
            project_ids_json jsonb not null,
            default_model_id uuid not null,
            allowed_model_ids_json jsonb not null,
            fallback_model_ids_json jsonb not null,
            allow_fallback boolean not null,
            required_capabilities integer not null,
            maximum_data_class integer not null,
            maximum_input_tokens integer not null,
            maximum_output_tokens integer not null,
            timeout_seconds integer not null,
            maximum_cost_microunits bigint not null,
            prompt_version varchar(100) not null,
            policy_version varchar(100) not null,
            published_by uuid not null,
            published_at timestamptz not null,
            constraint ux_intelligence_profile_version unique (tenant_id, use_case, version),
            constraint ck_intelligence_profile_input check (maximum_input_tokens between 1 and 100000),
            constraint ck_intelligence_profile_output check (maximum_output_tokens between 1 and 20000),
            constraint ck_intelligence_profile_timeout check (timeout_seconds between 5 and 300),
            constraint ck_intelligence_profile_cost check (maximum_cost_microunits between 1 and 1000000000)
        );

        create table if not exists intelligence.profile_selections (
            tenant_id uuid not null,
            use_case varchar(100) not null,
            profile_version_id uuid not null references intelligence.profile_versions(id),
            model_id uuid not null references intelligence.model_catalog(id),
            updated_by uuid not null,
            updated_at timestamptz not null,
            revision bigint not null,
            primary key (tenant_id, use_case),
            constraint ck_intelligence_selection_revision check (revision > 0)
        );
        """;
}
