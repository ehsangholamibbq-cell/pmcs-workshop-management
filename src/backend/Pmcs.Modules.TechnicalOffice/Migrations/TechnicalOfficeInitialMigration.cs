using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Migrations;

internal sealed class TechnicalOfficeInitialMigration : IDatabaseMigration
{
    public string ModuleName => "technical-office";
    public long Order => 675;
    public string Version => "20260911-001";
    public string Description => "Add document revisions, transmittals, RFIs and technical submittals";

    public string Sql => """
        create schema if not exists technical_office;

        create table if not exists technical_office.documents (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, title varchar(240) not null, type varchar(60) not null,
            discipline varchar(120) not null, originator varchar(240) null, contract_id uuid null,
            location_reference varchar(240) null, work_item_reference varchar(240) null,
            wbs_reference varchar(240) null, confidentiality varchar(80) null,
            current_official_revision_id uuid null, created_by uuid not null,
            created_at timestamptz not null, revision bigint not null
        );
        create unique index if not exists ux_technical_documents_project_number
            on technical_office.documents(tenant_id, project_id, number);

        create table if not exists technical_office.document_revisions (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            document_id uuid not null references technical_office.documents(id), revision_code varchar(80) not null,
            revision_date date not null, purpose varchar(60) not null, file_name varchar(255) not null,
            file_reference varchar(700) not null, sha256 varchar(64) not null,
            supersedes_revision_id uuid null references technical_office.document_revisions(id),
            status varchar(40) not null, prepared_by uuid not null, created_at timestamptz not null,
            submitted_at timestamptz null, reviewed_by uuid null, reviewed_at timestamptz null,
            review_comment varchar(1000) null, issued_through_transmittal_id uuid null,
            issued_at timestamptz null, superseded_at timestamptz null, revision bigint not null
        );
        create unique index if not exists ux_technical_document_revision_code
            on technical_office.document_revisions(tenant_id, project_id, document_id, lower(revision_code));
        create index if not exists ix_technical_document_revision_status
            on technical_office.document_revisions(tenant_id, project_id, status);

        create table if not exists technical_office.transmittals (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, sender varchar(240) not null, recipients_json jsonb not null,
            revision_ids_json jsonb not null, purpose varchar(500) not null, delivery_channel varchar(120) not null,
            due_response_date date null, status varchar(40) not null, created_by uuid not null,
            created_at timestamptz not null, issued_at timestamptz null, issued_by uuid null,
            acknowledged_at timestamptz null, acknowledged_by uuid null,
            acknowledgment_reference varchar(500) null, revision bigint not null
        );
        create unique index if not exists ux_technical_transmittal_number
            on technical_office.transmittals(tenant_id, project_id, number);

        create table if not exists technical_office.rfis (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, title varchar(240) not null, question varchar(6000) not null,
            requested_from varchar(240) not null, discipline varchar(120) not null, contract_id uuid null,
            location_reference varchar(240) null, work_item_reference varchar(240) null,
            wbs_reference varchar(240) null, source_issue_id uuid null, raised_date date not null,
            required_by_date date null, potential_impact integer not null, is_blocking boolean not null,
            proposed_solution varchar(4000) null, evidence_references_json jsonb not null,
            related_revision_ids_json jsonb not null, response_history_json jsonb not null,
            status varchar(40) not null, raised_by uuid not null, created_at timestamptz not null,
            submitted_at timestamptz null, closed_at timestamptz null, revision bigint not null
        );
        create unique index if not exists ux_technical_rfi_number
            on technical_office.rfis(tenant_id, project_id, number);
        create index if not exists ix_technical_rfi_status
            on technical_office.rfis(tenant_id, project_id, status, is_blocking);

        create table if not exists technical_office.submittals (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, title varchar(240) not null, type varchar(60) not null,
            discipline varchar(120) not null, submitter varchar(240) not null, reviewer varchar(240) not null,
            contract_id uuid null, commitment_id uuid null, location_reference varchar(240) null,
            work_item_reference varchar(240) null, wbs_reference varchar(240) null,
            required_by_date date null, planned_submission_date date null, review_due_date date null,
            resubmission_number integer not null, supersedes_submittal_id uuid null references technical_office.submittals(id),
            revision_ids_json jsonb not null, required_deliverable_reference varchar(500) null,
            status varchar(40) not null, review_outcome varchar(40) null, created_by uuid not null,
            created_at timestamptz not null, submitted_at timestamptz null, reviewed_by uuid null,
            reviewed_at timestamptz null, review_comment varchar(2000) null,
            closed_at timestamptz null, revision bigint not null
        );
        create unique index if not exists ux_technical_submittal_number
            on technical_office.submittals(tenant_id, project_id, number);
        create index if not exists ix_technical_submittal_status
            on technical_office.submittals(tenant_id, project_id, status);
        """;
}
