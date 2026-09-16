using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Finance.Migrations;

internal sealed class FinanceControlAndLocationMigration : IDatabaseMigration
{
    public string ModuleName => "finance";

    public long Order => 652;

    public string Version => "20260916-003";

    public string Description => "Complete Finance Lite controls, deterministic aging and stable project location lineage";

    public string Sql => """
        alter table finance.financial_records
            add column if not exists party_id uuid null,
            add column if not exists location_id uuid null,
            add column if not exists location_code varchar(80) null,
            add column if not exists wbs_reference varchar(240) null;
        create index if not exists ix_financial_records_project_party
            on finance.financial_records(tenant_id, project_id, party_id);
        create index if not exists ix_financial_records_project_location
            on finance.financial_records(tenant_id, project_id, location_id);

        create table if not exists finance.financial_obligations (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            type varchar(40) not null,
            number varchar(80) not null,
            description varchar(1000) not null,
            issue_date date not null,
            due_date date not null,
            amount numeric(24, 2) not null check (amount > 0),
            settled_amount numeric(24, 2) not null default 0 check (settled_amount >= 0 and settled_amount <= amount),
            currency_code varchar(3) not null,
            party_id uuid null,
            counterparty varchar(200) null,
            contract_id uuid null,
            commitment_id uuid null,
            cost_center_code varchar(120) null,
            wbs_reference varchar(240) null,
            location_id uuid null,
            location_code varchar(80) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            settled_at timestamptz null,
            revision bigint not null,
            constraint ck_financial_obligation_dates check (due_date >= issue_date)
        );
        create unique index if not exists ux_financial_obligations_project_number
            on finance.financial_obligations(tenant_id, project_id, number);
        create index if not exists ix_financial_obligations_project_status_due
            on finance.financial_obligations(tenant_id, project_id, status, due_date);
        create index if not exists ix_financial_obligations_project_location
            on finance.financial_obligations(tenant_id, project_id, location_id);

        create table if not exists finance.financial_settlements (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            obligation_id uuid not null,
            financial_record_id uuid not null,
            amount numeric(24, 2) not null check (amount > 0),
            settled_at timestamptz not null,
            created_by uuid not null,
            constraint fk_financial_settlement_obligation foreign key (obligation_id)
                references finance.financial_obligations(id),
            constraint fk_financial_settlement_record foreign key (financial_record_id)
                references finance.financial_records(id)
        );
        create unique index if not exists ux_financial_settlements_obligation_record
            on finance.financial_settlements(tenant_id, project_id, obligation_id, financial_record_id);

        create table if not exists finance.petty_cash_requests (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            number varchar(80) not null,
            purpose varchar(1000) not null,
            custodian varchar(200) not null,
            request_date date not null,
            reconciliation_due_date date not null,
            requested_amount numeric(24, 2) not null check (requested_amount > 0),
            approved_amount numeric(24, 2) null check (approved_amount > 0),
            reconciled_expense_amount numeric(24, 2) null check (reconciled_expense_amount >= 0),
            returned_amount numeric(24, 2) null check (returned_amount >= 0),
            currency_code varchar(3) not null,
            location_id uuid null,
            location_code varchar(80) null,
            cost_center_code varchar(120) null,
            wbs_reference varchar(240) null,
            advance_record_id uuid null,
            expense_record_id uuid null,
            return_record_id uuid null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            advanced_at timestamptz null,
            reconciliation_submitted_at timestamptz null,
            reconciled_at timestamptz null,
            revision bigint not null,
            constraint ck_petty_cash_dates check (reconciliation_due_date >= request_date),
            constraint fk_petty_cash_advance_record foreign key (advance_record_id)
                references finance.financial_records(id),
            constraint fk_petty_cash_expense_record foreign key (expense_record_id)
                references finance.financial_records(id),
            constraint fk_petty_cash_return_record foreign key (return_record_id)
                references finance.financial_records(id)
        );
        create unique index if not exists ux_petty_cash_requests_project_number
            on finance.petty_cash_requests(tenant_id, project_id, number);
        create index if not exists ix_petty_cash_requests_project_status_due
            on finance.petty_cash_requests(tenant_id, project_id, status, reconciliation_due_date);
        create index if not exists ix_petty_cash_requests_project_location
            on finance.petty_cash_requests(tenant_id, project_id, location_id);
        create unique index if not exists ux_petty_cash_requests_advance_record
            on finance.petty_cash_requests(tenant_id, project_id, advance_record_id)
            where advance_record_id is not null;

        create table if not exists finance.management_fee_policies (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            title varchar(200) not null,
            rate_percent numeric(9, 4) not null check (rate_percent > 0 and rate_percent <= 100),
            calculation_base varchar(40) not null,
            effective_from date not null,
            notes varchar(2000) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null
        );
        create index if not exists ix_management_fee_policies_project_status
            on finance.management_fee_policies(tenant_id, project_id, status);
        create unique index if not exists ux_management_fee_policies_one_approved
            on finance.management_fee_policies(tenant_id, project_id)
            where status = 'Approved';
        """;
}
