using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Projects.Migrations;

internal sealed class ProjectSetupReadinessMigration : IDatabaseMigration
{
    public string ModuleName => "projects";

    public long Order => 305;

    public string Version => "20260913-006";

    public string Description => "Add versioned setup readiness and activation configuration metadata";

    public string Sql => """
        alter table projects.projects
            add column if not exists project_type varchar(40) not null default 'NotConfigured',
            add column if not exists execution_phase varchar(40) not null default 'NotConfigured',
            add column if not exists country_code varchar(2) not null default '',
            add column if not exists region varchar(200) not null default '',
            add column if not exists start_date date null,
            add column if not exists planned_finish_date date null,
            add column if not exists short_description varchar(1000) not null default '',
            add column if not exists unit_system varchar(40) not null default 'NotConfigured',
            add column if not exists daily_cutoff_local_time time null,
            add column if not exists reporting_frequency varchar(40) not null default 'NotConfigured',
            add column if not exists daily_report_workflow varchar(60) not null default 'NotConfigured',
            add column if not exists offline_policy_accepted boolean not null default false,
            add column if not exists configuration_version bigint not null default 1,
            add column if not exists activated_configuration_version bigint null;

        alter table projects.projects
            drop constraint if exists ck_projects_setup_dates;
        alter table projects.projects
            add constraint ck_projects_setup_dates check (
                start_date is null or planned_finish_date is null or planned_finish_date >= start_date);

        alter table projects.projects
            drop constraint if exists ck_projects_activation_configuration_version;
        alter table projects.projects
            add constraint ck_projects_activation_configuration_version check (
                (activated_at is null and activated_configuration_version is null) or
                (activated_at is not null and activated_configuration_version is not null));
        """;
}
