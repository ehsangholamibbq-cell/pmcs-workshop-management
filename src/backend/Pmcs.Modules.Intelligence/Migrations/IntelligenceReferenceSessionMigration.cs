using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceReferenceSessionMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 605;
    public string Version => "20261001-005";
    public string Description => "Pin one-shot reference session identity and expiry";

    public string Sql => """
        alter table intelligence.reference_runs
            add column if not exists session_id uuid null;
        alter table intelligence.reference_runs
            add column if not exists session_expires_at timestamptz null;
        update intelligence.reference_runs
            set session_id = id,
                session_expires_at = requested_at + interval '10 minutes'
            where session_id is null or session_expires_at is null;
        alter table intelligence.reference_runs
            alter column session_id set not null,
            alter column session_expires_at set not null;
        create unique index if not exists ux_intelligence_reference_run_session
            on intelligence.reference_runs (session_id);
        """;
}
