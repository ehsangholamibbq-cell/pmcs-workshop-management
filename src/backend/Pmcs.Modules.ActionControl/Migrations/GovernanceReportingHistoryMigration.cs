using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.ActionControl.Migrations;

internal sealed class GovernanceReportingHistoryMigration : IDatabaseMigration
{
    public string ModuleName => "action-control";
    public long Order => 702;
    public string Version => "20260927-003";
    public string Description => "Preserve owner transition chronology for F09 without inferred legacy backfill";

    public string Sql => """
        alter table action_control.issues add column if not exists reporting_history_json jsonb null;
        alter table action_control.risks add column if not exists reporting_history_json jsonb null;
        alter table action_control.decision_requests add column if not exists reporting_history_json jsonb null;
        alter table action_control.decisions add column if not exists reporting_history_json jsonb null;
        alter table action_control.escalation_threads add column if not exists reporting_history_json jsonb null;
        alter table action_control.actions add column if not exists reporting_history_json jsonb null;
        do $$ declare source_table text; begin
            foreach source_table in array array['issues', 'risks', 'decision_requests',
                'decisions', 'escalation_threads', 'actions'] loop
                if not exists (select 1 from pg_constraint where conname =
                    'ck_' || source_table || '_reporting_history_array') then
                    execute format('alter table action_control.%I add constraint %I check
                        (reporting_history_json is null or jsonb_typeof(reporting_history_json) = ''array'')',
                        source_table, 'ck_' || source_table || '_reporting_history_array');
                end if;
            end loop;
        end $$;
        """;
}
