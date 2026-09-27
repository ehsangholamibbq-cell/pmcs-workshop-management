using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class PortfolioReportScopeMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";
    public long Order => 1210;
    public string Version => "20260927-011";
    public string Description => "Support real portfolio scope on report runs, snapshots and outputs";

    public string Sql => """
        alter table reporting.report_definitions
            drop constraint ck_reporting_definition_scope;
        alter table reporting.report_definitions
            add constraint ck_reporting_definition_scope
                check (scope in ('Project', 'Tenant', 'Portfolio'));

        alter table reporting.report_runs
            add column scope varchar(40) not null default 'Project',
            add column pinned_portfolio_cohort jsonb null,
            alter column project_id drop not null;
        alter table reporting.report_runs
            add constraint ck_reporting_run_scope_project check (
                (scope = 'Project' and project_id is not null and
                    project_time_zone <> '' and pinned_portfolio_cohort is null)
                or
                (scope = 'Portfolio' and project_id is null and
                    project_time_zone = '' and pinned_portfolio_cohort is not null and
                    jsonb_typeof(pinned_portfolio_cohort) = 'object'));

        alter table reporting.report_snapshots
            add column scope varchar(40) not null default 'Project',
            alter column project_id drop not null;
        alter table reporting.report_snapshots
            add constraint ck_reporting_snapshot_scope_project check (
                (scope = 'Project' and project_id is not null) or
                (scope = 'Portfolio' and project_id is null));

        alter table reporting.report_outputs
            add column scope varchar(40) not null default 'Project',
            alter column project_id drop not null;
        alter table reporting.report_outputs
            add constraint ck_reporting_output_scope_project check (
                (scope = 'Project' and project_id is not null) or
                (scope = 'Portfolio' and project_id is null));

        create index ix_reporting_runs_portfolio_created
            on reporting.report_runs(tenant_id, created_at desc, id desc)
            where scope = 'Portfolio';
        create index ix_reporting_snapshots_portfolio_built
            on reporting.report_snapshots(tenant_id, built_at desc, id desc)
            where scope = 'Portfolio';
        create index ix_reporting_outputs_portfolio_created
            on reporting.report_outputs(tenant_id, created_at desc, id desc)
            where scope = 'Portfolio';
        """;
}
