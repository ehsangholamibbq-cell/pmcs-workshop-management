using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Commercial.Migrations;

internal sealed class CommercialSupplyRealityMigration : IDatabaseMigration
{
    public string ModuleName => "commercial";
    public long Order => 626;
    public string Version => "20260911-002";
    public string Description => "Add item master, receipts, inspections, append-only inventory and custody reconciliation";

    public string Sql => """
        create table if not exists commercial.supply_items (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            code varchar(48) not null, name varchar(240) not null, kind varchar(40) not null,
            category varchar(120) not null, base_unit varchar(24) not null,
            unit_conversions_json jsonb not null, technical_specification_reference varchar(500) null,
            tracking_policy varchar(40) not null, inspection_required boolean not null,
            storage_condition varchar(500) null, acceptance_criteria varchar(1000) null,
            status varchar(40) not null, created_by uuid not null, created_at timestamptz not null,
            changed_at timestamptz not null, revision bigint not null
        );
        create unique index if not exists ux_commercial_supply_items_project_code
            on commercial.supply_items(tenant_id, project_id, code);

        alter table commercial.purchase_requests add column if not exists supply_item_id uuid null;
        alter table commercial.purchase_requests add column if not exists requested_quantity numeric(24, 6) null;
        alter table commercial.purchase_requests add column if not exists unit_code varchar(24) null;
        alter table commercial.purchase_requests add column if not exists delivery_location varchar(240) null;
        alter table commercial.purchase_requests add column if not exists work_item_reference varchar(240) null;
        alter table commercial.purchase_requests add column if not exists wbs_reference varchar(240) null;
        alter table commercial.purchase_requests add column if not exists budget_line_reference varchar(240) null;
        alter table commercial.purchase_requests add column if not exists budget_check_status varchar(40) not null default 'NotConfigured';
        alter table commercial.purchase_requests add column if not exists criticality varchar(40) not null default 'Normal';

        alter table commercial.purchase_orders add column if not exists supply_item_id uuid null;
        alter table commercial.purchase_orders add column if not exists ordered_quantity numeric(24, 6) null;
        alter table commercial.purchase_orders add column if not exists unit_code varchar(24) null;
        alter table commercial.purchase_orders add column if not exists delivery_location varchar(240) null;

        create table if not exists commercial.inventory_locations (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            code varchar(40) not null, name varchar(200) not null, type varchar(40) not null,
            allows_available_stock boolean not null, status varchar(40) not null,
            created_by uuid not null, created_at timestamptz not null, revision bigint not null
        );
        create unique index if not exists ux_commercial_inventory_locations_project_code
            on commercial.inventory_locations(tenant_id, project_id, code);

        create table if not exists commercial.goods_receipts (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, purchase_order_id uuid not null references commercial.purchase_orders(id),
            party_id uuid not null references commercial.parties(id), item_id uuid not null references commercial.supply_items(id),
            stock_location_id uuid null references commercial.inventory_locations(id),
            dispatch_note varchar(120) not null, arrived_at timestamptz not null,
            delivery_location varchar(240) not null, shipped_quantity numeric(24, 6) not null,
            received_quantity numeric(24, 6) not null, base_received_quantity numeric(24, 6) not null,
            damaged_quantity numeric(24, 6) not null, unit_code varchar(24) not null,
            base_unit varchar(24) not null, conversion_version integer not null,
            batch_or_lot_reference varchar(160) null, package_condition varchar(500) null,
            excess_approval_reason varchar(1000) null, excess_approved_by uuid null,
            evidence_references_json jsonb not null, status varchar(40) not null,
            accepted_base_quantity numeric(24, 6) not null, rejected_base_quantity numeric(24, 6) not null,
            quarantined_base_quantity numeric(24, 6) not null, received_by uuid not null,
            created_at timestamptz not null, inspected_by uuid null, inspected_at timestamptz null,
            inspection_type varchar(120) null, inspection_reference varchar(500) null,
            inspection_comment varchar(2000) null, stock_posted_at timestamptz null, revision bigint not null,
            constraint ck_commercial_receipt_quantities check (
                shipped_quantity > 0 and received_quantity > 0 and received_quantity <= shipped_quantity and
                damaged_quantity >= 0 and damaged_quantity <= received_quantity and base_received_quantity > 0),
            constraint ck_commercial_inspection_quantities check (
                accepted_base_quantity >= 0 and rejected_base_quantity >= 0 and quarantined_base_quantity >= 0)
        );
        create unique index if not exists ux_commercial_goods_receipts_project_number
            on commercial.goods_receipts(tenant_id, project_id, number);
        create index if not exists ix_commercial_goods_receipts_order_item
            on commercial.goods_receipts(tenant_id, project_id, purchase_order_id, item_id);
        create index if not exists ix_commercial_goods_receipts_status
            on commercial.goods_receipts(tenant_id, project_id, status);

        create table if not exists commercial.inventory_ledger (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            transaction_id uuid not null, item_id uuid not null references commercial.supply_items(id),
            location_id uuid not null references commercial.inventory_locations(id),
            event_type varchar(40) not null, base_quantity_delta numeric(24, 6) not null,
            base_unit varchar(24) not null, source_type varchar(80) not null, source_id uuid not null,
            reverses_entry_id uuid null references commercial.inventory_ledger(id), reason varchar(1000) null,
            occurred_at timestamptz not null, recorded_by uuid not null, recorded_at timestamptz not null,
            constraint ck_commercial_inventory_ledger_nonzero check (base_quantity_delta <> 0)
        );
        create index if not exists ix_commercial_inventory_ledger_balance
            on commercial.inventory_ledger(tenant_id, project_id, item_id, location_id, occurred_at);
        create index if not exists ix_commercial_inventory_ledger_transaction
            on commercial.inventory_ledger(tenant_id, project_id, transaction_id);
        create index if not exists ix_commercial_inventory_ledger_source
            on commercial.inventory_ledger(tenant_id, project_id, source_type, source_id);
        create unique index if not exists ux_commercial_inventory_ledger_single_reversal
            on commercial.inventory_ledger(reverses_entry_id) where reverses_entry_id is not null;
        create or replace function commercial.reject_inventory_ledger_mutation()
        returns trigger language plpgsql as $$
        begin
            raise exception 'inventory ledger is append-only';
        end;
        $$;
        drop trigger if exists trg_commercial_inventory_ledger_append_only on commercial.inventory_ledger;
        create trigger trg_commercial_inventory_ledger_append_only
            before update or delete on commercial.inventory_ledger
            for each row execute function commercial.reject_inventory_ledger_mutation();

        create table if not exists commercial.material_issues (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, item_id uuid not null references commercial.supply_items(id),
            source_location_id uuid not null references commercial.inventory_locations(id),
            issued_base_quantity numeric(24, 6) not null, base_unit varchar(24) not null,
            issued_to varchar(240) not null, destination_location varchar(240) not null,
            work_item_reference varchar(240) null, wbs_reference varchar(240) null,
            purchase_request_id uuid null references commercial.purchase_requests(id),
            evidence_references_json jsonb not null, consumed_base_quantity numeric(24, 6) not null,
            returned_base_quantity numeric(24, 6) not null, waste_base_quantity numeric(24, 6) not null,
            status varchar(40) not null, issued_by uuid not null, issued_at timestamptz not null,
            acknowledged_by uuid null, acknowledged_at timestamptz null,
            acknowledgment_reference varchar(500) null, reconciled_at timestamptz null, revision bigint not null,
            constraint ck_commercial_material_issue_quantities check (
                issued_base_quantity > 0 and consumed_base_quantity >= 0 and returned_base_quantity >= 0 and
                waste_base_quantity >= 0 and consumed_base_quantity + returned_base_quantity + waste_base_quantity <= issued_base_quantity)
        );
        create unique index if not exists ux_commercial_material_issues_project_number
            on commercial.material_issues(tenant_id, project_id, number);
        create index if not exists ix_commercial_material_issues_status
            on commercial.material_issues(tenant_id, project_id, status);

        create table if not exists commercial.material_reconciliations (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            material_issue_id uuid not null references commercial.material_issues(id),
            consumed_base_quantity numeric(24, 6) not null, returned_base_quantity numeric(24, 6) not null,
            waste_base_quantity numeric(24, 6) not null, base_unit varchar(24) not null,
            waste_reason varchar(1000) null, evidence_references_json jsonb not null,
            recorded_by uuid not null, recorded_at timestamptz not null,
            constraint ck_commercial_reconciliation_quantities check (
                consumed_base_quantity >= 0 and returned_base_quantity >= 0 and waste_base_quantity >= 0 and
                consumed_base_quantity + returned_base_quantity + waste_base_quantity > 0)
        );
        create index if not exists ix_commercial_material_reconciliations_issue
            on commercial.material_reconciliations(tenant_id, project_id, material_issue_id, recorded_at);

        create table if not exists commercial.inventory_adjustments (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, item_id uuid not null references commercial.supply_items(id),
            location_id uuid not null references commercial.inventory_locations(id), cutoff_at timestamptz not null,
            system_base_quantity numeric(24, 6) not null, counted_base_quantity numeric(24, 6) not null,
            base_unit varchar(24) not null, reason varchar(1000) not null, evidence_references_json jsonb not null,
            status varchar(40) not null, proposed_by uuid not null, proposed_at timestamptz not null,
            reviewed_by uuid null, reviewed_at timestamptz null, review_comment varchar(1000) null,
            posted_at timestamptz null, revision bigint not null,
            constraint ck_commercial_adjustment_quantities check (
                system_base_quantity >= 0 and counted_base_quantity >= 0 and system_base_quantity <> counted_base_quantity)
        );
        create unique index if not exists ux_commercial_inventory_adjustments_project_number
            on commercial.inventory_adjustments(tenant_id, project_id, number);
        create index if not exists ix_commercial_inventory_adjustments_status
            on commercial.inventory_adjustments(tenant_id, project_id, status);

        create table if not exists commercial.service_acceptances (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, purchase_order_id uuid not null references commercial.purchase_orders(id),
            party_id uuid not null references commercial.parties(id), item_id uuid not null references commercial.supply_items(id),
            period_start date not null, period_end date not null, delivered_base_quantity numeric(24, 6) not null,
            accepted_base_quantity numeric(24, 6) not null, rejected_base_quantity numeric(24, 6) not null,
            base_unit varchar(24) not null, acceptance_criteria varchar(1000) not null,
            evidence_references_json jsonb not null, comment varchar(2000) null,
            verified_by uuid not null, verified_at timestamptz not null, revision bigint not null,
            constraint ck_commercial_service_period check (period_end >= period_start),
            constraint ck_commercial_service_quantities check (
                delivered_base_quantity > 0 and accepted_base_quantity >= 0 and rejected_base_quantity >= 0 and
                accepted_base_quantity + rejected_base_quantity = delivered_base_quantity)
        );
        create unique index if not exists ux_commercial_service_acceptances_project_number
            on commercial.service_acceptances(tenant_id, project_id, number);
        create index if not exists ix_commercial_service_acceptances_order_item
            on commercial.service_acceptances(tenant_id, project_id, purchase_order_id, item_id);
        """;
}
