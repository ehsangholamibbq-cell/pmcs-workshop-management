using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Projects.Migrations;

internal sealed class ProjectCalendarAndFinanceMigration : IDatabaseMigration
{
    public string ModuleName => "projects";

    public long Order => 301;

    public string Version => "20260909-002";

    public string Description => "Add optional calendar and Finance Lite project configuration";

    public string Sql => """
        alter table projects.projects
            add column if not exists finance_mode varchar(40) not null default 'Active',
            add column if not exists base_currency_code varchar(3) not null default 'IRR',
            add column if not exists calendar_mode varchar(40) not null default 'NotConfigured',
            add column if not exists working_days_mask integer null,
            add column if not exists configuration_changed_at timestamptz null;

        alter table projects.projects
            drop constraint if exists ck_projects_working_days_mask;
        alter table projects.projects
            add constraint ck_projects_working_days_mask check (
                (calendar_mode = 'NotConfigured' and working_days_mask is null) or
                (calendar_mode = 'WorkingWeek' and working_days_mask between 1 and 127));
        """;
}
