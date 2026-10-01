using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceReferenceRunMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 603;
    public string Version => "20261001-003";
    public string Description => "Create metadata-only reference Run lineage";

    public string Sql => """
        create table if not exists intelligence.reference_runs (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            requested_by uuid not null,
            profile_version_id uuid not null references intelligence.profile_versions(id),
            profile_version integer not null,
            model_catalog_id uuid not null references intelligence.model_catalog(id),
            model_version integer not null,
            provider varchar(40) not null,
            model varchar(160) not null,
            prompt_version varchar(100) not null,
            policy_version varchar(100) not null,
            request_hash varchar(64) not null,
            status varchar(20) not null,
            tool_id varchar(100) null,
            tool_decision varchar(20) null,
            fallback boolean not null,
            fallback_reason varchar(120) null,
            input_tokens integer not null,
            output_tokens integer not null,
            cost_microunits bigint not null,
            error_code varchar(120) null,
            requested_at timestamptz not null,
            validated_at timestamptz null,
            started_at timestamptz null,
            completed_at timestamptz null,
            revision bigint not null,
            constraint ck_intelligence_reference_run_status check
                (status in ('Requested','Validated','Running','Completed','Failed','Cancelled')),
            constraint ck_intelligence_reference_run_usage check
                (input_tokens >= 0 and output_tokens >= 0 and cost_microunits >= 0),
            constraint ck_intelligence_reference_run_revision check (revision > 0)
        );
        create index if not exists ix_intelligence_reference_runs_scope
            on intelligence.reference_runs (tenant_id, project_id, requested_at desc);
        """;
}
