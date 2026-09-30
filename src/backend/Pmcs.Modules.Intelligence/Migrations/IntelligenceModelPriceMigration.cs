using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceModelPriceMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 604;
    public string Version => "20261001-004";
    public string Description => "Pin conservative per-token rate ceilings to model versions";

    public string Sql => """
        alter table intelligence.model_catalog
            add column if not exists input_microunits_per_token bigint not null default 0;
        alter table intelligence.model_catalog
            add column if not exists output_microunits_per_token bigint not null default 0;
        alter table intelligence.model_catalog
            add constraint ck_intelligence_model_input_rate
                check (input_microunits_per_token between 0 and 1000000);
        alter table intelligence.model_catalog
            add constraint ck_intelligence_model_output_rate
                check (output_microunits_per_token between 0 and 1000000);
        """;
}
