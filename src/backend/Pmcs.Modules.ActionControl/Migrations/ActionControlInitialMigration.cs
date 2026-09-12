using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.ActionControl.Migrations;

internal sealed class ActionControlInitialMigration : IDatabaseMigration
{
    public string ModuleName => "action-control";

    public long Order => 700;

    public string Version => "20260909-001";

    public string Description => "Create management actions and attention dispositions";

    public string Sql => """
        create schema if not exists action_control;

        create table if not exists action_control.actions (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            source_fact_id uuid not null,
            title varchar(240) not null,
            description varchar(2000) null,
            assignee_user_id uuid not null,
            assignee_display_name varchar(200) not null,
            due_date date not null,
            priority varchar(40) not null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            last_changed_by uuid null,
            last_changed_at timestamptz null,
            completed_at timestamptz null,
            revision bigint not null
        );
        create index if not exists ix_actions_project_status_due
            on action_control.actions(tenant_id, project_id, status, due_date);
        create index if not exists ix_actions_assignee_status
            on action_control.actions(tenant_id, project_id, assignee_user_id, status);

        create table if not exists action_control.attention_dispositions (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            source_fact_id uuid not null,
            kind varchar(40) not null,
            action_id uuid null references action_control.actions(id),
            reason varchar(1000) null,
            decided_by uuid not null,
            decided_at timestamptz not null,
            constraint ck_attention_disposition_payload check (
                (kind = 'ConvertedToAction' and action_id is not null and reason is null) or
                (kind = 'Dismissed' and action_id is null and reason is not null)),
            unique (tenant_id, project_id, source_fact_id)
        );
        create index if not exists ix_attention_dispositions_project
            on action_control.attention_dispositions(tenant_id, project_id, decided_at desc);
        """;
}
