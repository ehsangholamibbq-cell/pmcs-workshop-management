using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Commercial.Migrations;

internal sealed class CommercialInitialMigration : IDatabaseMigration
{
    public string ModuleName => "commercial";

    public long Order => 625;

    public string Version => "20260909-001";

    public string Description => "Create party, contract, amendment, purchase request, commitment and commercial state registers";

    public string Sql => """
        create schema if not exists commercial;

        create table if not exists commercial.parties (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            code varchar(32) not null,
            legal_name varchar(200) not null,
            type varchar(40) not null,
            national_id varchar(50) null,
            contact_name varchar(160) null,
            phone varchar(50) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            changed_at timestamptz not null,
            revision bigint not null
        );
        create unique index if not exists ux_commercial_parties_project_code
            on commercial.parties(tenant_id, project_id, code);

        create table if not exists commercial.contracts (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            party_id uuid not null references commercial.parties(id),
            number varchar(80) not null,
            title varchar(240) not null,
            type varchar(40) not null,
            original_approved_amount numeric(24, 2) null check (original_approved_amount > 0),
            currency_code varchar(3) not null,
            start_date date null,
            end_date date null,
            notes varchar(2000) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            changed_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null,
            constraint ck_commercial_contract_dates check (end_date is null or start_date is null or end_date >= start_date)
        );
        create unique index if not exists ux_commercial_contracts_project_number
            on commercial.contracts(tenant_id, project_id, number);
        create index if not exists ix_commercial_contracts_project_status
            on commercial.contracts(tenant_id, project_id, status);

        create table if not exists commercial.contract_amendments (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            contract_id uuid not null references commercial.contracts(id),
            number varchar(80) not null,
            title varchar(240) not null,
            type varchar(40) not null,
            amount_delta numeric(24, 2) null,
            currency_code varchar(3) not null,
            extension_days integer null check (extension_days >= 0),
            notes varchar(2000) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            changed_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null
        );
        create unique index if not exists ux_commercial_amendments_contract_number
            on commercial.contract_amendments(tenant_id, project_id, contract_id, number);
        create index if not exists ix_commercial_amendments_project_status
            on commercial.contract_amendments(tenant_id, project_id, status);

        create table if not exists commercial.purchase_requests (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            number varchar(80) not null,
            title varchar(240) not null,
            description varchar(2000) not null,
            estimated_amount numeric(24, 2) null check (estimated_amount > 0),
            currency_code varchar(3) not null,
            needed_by_date date null,
            status varchar(40) not null,
            requested_by uuid not null,
            created_at timestamptz not null,
            changed_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null
        );
        create unique index if not exists ux_commercial_requests_project_number
            on commercial.purchase_requests(tenant_id, project_id, number);
        create index if not exists ix_commercial_requests_project_status
            on commercial.purchase_requests(tenant_id, project_id, status);

        create table if not exists commercial.purchase_orders (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            purchase_request_id uuid not null references commercial.purchase_requests(id),
            party_id uuid not null references commercial.parties(id),
            contract_id uuid null references commercial.contracts(id),
            number varchar(80) not null,
            title varchar(240) not null,
            amount numeric(24, 2) not null check (amount > 0),
            currency_code varchar(3) not null,
            delivery_due_date date null,
            status varchar(40) not null,
            issued_by uuid not null,
            issued_at timestamptz not null,
            changed_at timestamptz not null,
            closed_by uuid null,
            closed_at timestamptz null,
            closure_comment varchar(1000) null,
            revision bigint not null
        );
        create unique index if not exists ux_commercial_orders_project_number
            on commercial.purchase_orders(tenant_id, project_id, number);
        create unique index if not exists ux_commercial_orders_request
            on commercial.purchase_orders(tenant_id, project_id, purchase_request_id);
        create index if not exists ix_commercial_orders_project_status
            on commercial.purchase_orders(tenant_id, project_id, status);

        create table if not exists commercial.commercial_state_snapshots (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            calculation_version varchar(80) not null,
            as_of_date date not null,
            calculated_at timestamptz not null,
            currency_code varchar(3) not null,
            contract_state varchar(40) not null,
            contract_data_quality_status varchar(40) not null,
            procurement_state varchar(40) not null,
            procurement_data_quality_status varchar(40) not null,
            active_party_count integer not null,
            registered_contract_count integer not null,
            active_contract_count integer not null,
            contracts_without_ceiling_count integer not null,
            pending_contract_approval_count integer not null,
            expired_active_contract_count integer not null,
            approved_amendment_count integer not null,
            approved_amendment_delta numeric(24, 2) not null,
            approved_contract_ceiling_amount numeric(24, 2) null,
            purchase_request_count integer not null,
            pending_procurement_approval_count integer not null,
            approved_requests_awaiting_order_count integer not null,
            open_commitment_count integer not null,
            overdue_commitment_count integer not null,
            total_committed_amount numeric(24, 2) not null,
            open_commitment_amount numeric(24, 2) not null,
            source_max_changed_at timestamptz null
        );
        create index if not exists ix_commercial_state_project_calculated
            on commercial.commercial_state_snapshots(tenant_id, project_id, calculated_at desc);
        """;
}
