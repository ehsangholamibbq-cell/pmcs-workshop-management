using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Finance.Migrations;

internal sealed class FinanceInitialMigration : IDatabaseMigration
{
    public string ModuleName => "finance";

    public long Order => 650;

    public string Version => "20260909-001";

    public string Description => "Create Finance Lite records, optional budget baselines and financial state snapshots";

    public string Sql => """
        create schema if not exists finance;

        create table if not exists finance.financial_records (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            type varchar(40) not null,
            transaction_date date not null,
            amount numeric(24, 2) not null check (amount > 0),
            currency_code varchar(3) not null,
            description varchar(1000) not null,
            counterparty varchar(200) null,
            document_number varchar(120) null,
            contract_reference varchar(120) null,
            cost_center_code varchar(120) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null
        );
        create index if not exists ix_financial_records_project_date
            on finance.financial_records(tenant_id, project_id, transaction_date desc);
        create index if not exists ix_financial_records_project_status
            on finance.financial_records(tenant_id, project_id, status);

        create table if not exists finance.budget_baselines (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            title varchar(200) not null,
            amount numeric(24, 2) not null check (amount > 0),
            currency_code varchar(3) not null,
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
        create index if not exists ix_budget_baselines_project_status
            on finance.budget_baselines(tenant_id, project_id, status, created_at desc);
        create unique index if not exists ux_budget_baselines_one_approved
            on finance.budget_baselines(tenant_id, project_id)
            where status = 'Approved';

        create table if not exists finance.financial_state_snapshots (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            calculation_version varchar(80) not null,
            as_of_date date not null,
            calculated_at timestamptz not null,
            currency_code varchar(3) not null,
            status varchar(40) not null,
            data_quality_status varchar(40) not null,
            budget_comparison_state varchar(40) not null,
            posted_record_count integer not null,
            total_receipts numeric(24, 2) not null,
            direct_payments numeric(24, 2) not null,
            petty_cash_funding numeric(24, 2) not null,
            petty_cash_expenses numeric(24, 2) not null,
            external_net_cash numeric(24, 2) not null,
            recognized_spend numeric(24, 2) not null,
            petty_cash_balance numeric(24, 2) not null,
            approved_budget_amount numeric(24, 2) null,
            budget_remaining_amount numeric(24, 2) null,
            budget_consumed_percent numeric(9, 1) null,
            source_max_changed_at timestamptz null
        );
        create index if not exists ix_financial_state_project_calculated
            on finance.financial_state_snapshots(tenant_id, project_id, calculated_at desc);
        """;
}
