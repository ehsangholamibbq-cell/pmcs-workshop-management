using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Migrations;

internal sealed class TechnicalReportingHistoryMigration : IDatabaseMigration
{
    public string ModuleName => "technical-office";
    public long Order => 676;
    public string Version => "20260927-002";
    public string Description => "Preserve provable RFI and submittal transition histories for Technical Office reporting";

    public string Sql => """
        alter table technical_office.rfis add column if not exists reporting_history_json jsonb null;
        alter table technical_office.submittals add column if not exists reporting_history_json jsonb null;
        do $$ begin
            if not exists (select 1 from pg_constraint where conname = 'ck_technical_rfi_reporting_history_array') then
                alter table technical_office.rfis add constraint ck_technical_rfi_reporting_history_array
                    check (reporting_history_json is null or jsonb_typeof(reporting_history_json) = 'array');
            end if;
            if not exists (select 1 from pg_constraint where conname = 'ck_technical_submittal_reporting_history_array') then
                alter table technical_office.submittals add constraint ck_technical_submittal_reporting_history_array
                    check (reporting_history_json is null or jsonb_typeof(reporting_history_json) = 'array');
            end if;
        end $$;
        """;
}
