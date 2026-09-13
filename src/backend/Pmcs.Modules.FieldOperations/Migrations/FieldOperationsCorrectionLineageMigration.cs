using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.FieldOperations.Migrations;

internal sealed class FieldOperationsCorrectionLineageMigration : IDatabaseMigration
{
    public string ModuleName => "field-operations";

    public long Order => 432;

    public string Version => "20260913-006";

    public string Description => "Add immutable daily report correction and supersession lineage";

    public string Sql => """
        alter table field_operations.daily_reports
            add column if not exists root_report_id uuid null,
            add column if not exists version_number integer null,
            add column if not exists supersedes_report_id uuid null,
            add column if not exists superseded_by_report_id uuid null,
            add column if not exists superseded_at timestamptz null,
            add column if not exists correction_reason varchar(1000) null,
            add column if not exists correction_initiated_by uuid null;

        update field_operations.daily_reports
        set root_report_id = id,
            version_number = 1
        where root_report_id is null or version_number is null;

        alter table field_operations.daily_reports
            alter column root_report_id set not null,
            alter column version_number set not null;

        alter table field_operations.daily_reports
            drop constraint if exists daily_reports_tenant_id_project_id_report_date_key;

        create unique index if not exists ux_daily_reports_root_date
            on field_operations.daily_reports(tenant_id, project_id, report_date)
            where supersedes_report_id is null;
        create unique index if not exists ux_daily_reports_lineage_version
            on field_operations.daily_reports(root_report_id, version_number);
        create unique index if not exists ux_daily_reports_active_correction
            on field_operations.daily_reports(supersedes_report_id)
            where supersedes_report_id is not null and status in ('Draft', 'Submitted', 'Returned');
        create index if not exists ix_daily_reports_lineage
            on field_operations.daily_reports(tenant_id, project_id, root_report_id, version_number desc);

        alter table field_operations.daily_report_facts
            add column if not exists copied_from_fact_id uuid null;
        create index if not exists ix_daily_report_facts_copied_from
            on field_operations.daily_report_facts(copied_from_fact_id)
            where copied_from_fact_id is not null;

        do $$
        begin
            if not exists (
                select 1 from pg_constraint
                where conname = 'fk_daily_reports_supersedes'
                  and conrelid = 'field_operations.daily_reports'::regclass
            ) then
                alter table field_operations.daily_reports
                    add constraint fk_daily_reports_supersedes
                    foreign key (supersedes_report_id) references field_operations.daily_reports(id);
            end if;
            if not exists (
                select 1 from pg_constraint
                where conname = 'fk_daily_reports_superseded_by'
                  and conrelid = 'field_operations.daily_reports'::regclass
            ) then
                alter table field_operations.daily_reports
                    add constraint fk_daily_reports_superseded_by
                    foreign key (superseded_by_report_id) references field_operations.daily_reports(id);
            end if;
            if not exists (
                select 1 from pg_constraint
                where conname = 'fk_daily_report_facts_copied_from'
                  and conrelid = 'field_operations.daily_report_facts'::regclass
            ) then
                alter table field_operations.daily_report_facts
                    add constraint fk_daily_report_facts_copied_from
                    foreign key (copied_from_fact_id) references field_operations.daily_report_facts(id);
            end if;
        end $$;
        """;
}
