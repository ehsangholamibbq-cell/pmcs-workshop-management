using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceInitialMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 600;
    public string Version => "20260910-001";
    public string Description => "Create durable advisory insight requests and reviewable outputs";

    public string Sql => """
        create schema if not exists intelligence;

        create table if not exists intelligence.generation_requests (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            requested_by uuid not null,
            status varchar(40) not null,
            snapshot_id uuid null,
            insight_id uuid null,
            requested_at timestamptz not null,
            started_at timestamptz null,
            completed_at timestamptz null,
            next_attempt_at timestamptz null,
            attempts integer not null,
            last_error_code varchar(120) null,
            revision bigint not null,
            constraint ck_intelligence_request_attempts check (attempts >= 0),
            constraint ck_intelligence_request_status check (status in ('Pending', 'Processing', 'Succeeded', 'Failed'))
        );
        create index if not exists ix_intelligence_request_project
            on intelligence.generation_requests(tenant_id, project_id, requested_at desc);
        create index if not exists ix_intelligence_request_queue
            on intelligence.generation_requests(status, requested_at);
        create unique index if not exists ux_intelligence_active_request
            on intelligence.generation_requests(tenant_id, project_id, requested_by)
            where status in ('Pending', 'Processing');

        create table if not exists intelligence.advisory_insights (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            generation_request_id uuid not null unique references intelligence.generation_requests(id),
            snapshot_id uuid not null,
            requested_by uuid not null,
            output_json jsonb not null,
            insight_type varchar(40) not null,
            statement varchar(2000) not null,
            confidence_band varchar(40) not null,
            potential_impact varchar(1000) not null,
            suggested_owner_role varchar(120) not null,
            context_hash varchar(64) not null,
            includes_financial_data boolean not null,
            includes_commercial_data boolean not null,
            includes_action_data boolean not null,
            provider varchar(80) not null,
            model varchar(120) not null,
            provider_response_id varchar(200) null,
            prompt_version varchar(80) not null,
            policy_version varchar(80) not null,
            generated_at timestamptz not null,
            expires_at timestamptz not null,
            review_status varchar(40) not null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null,
            constraint ck_advisory_review_status check (review_status in ('NeedsReview', 'Accepted', 'Dismissed')),
            constraint ck_advisory_expiration check (expires_at > generated_at)
        );
        create index if not exists ix_advisory_insight_project
            on intelligence.advisory_insights(tenant_id, project_id, generated_at desc);
        """;
}
