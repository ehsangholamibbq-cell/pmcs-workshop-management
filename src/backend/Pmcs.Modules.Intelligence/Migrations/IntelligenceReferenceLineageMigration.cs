using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceReferenceLineageMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 607;
    public string Version => "20261001-007";
    public string Description => "Pin initial and selected provider and model lineage";

    public string Sql => """
        alter table intelligence.reference_runs
            add column if not exists initial_model_catalog_id uuid null,
            add column if not exists initial_model_version integer null,
            add column if not exists initial_provider varchar(40) null,
            add column if not exists initial_provider_version integer null,
            add column if not exists provider_version integer null;
        update intelligence.reference_runs set
            initial_model_catalog_id = coalesce(initial_model_catalog_id, model_catalog_id),
            initial_model_version = coalesce(initial_model_version, model_version),
            initial_provider = coalesce(initial_provider, provider),
            initial_provider_version = coalesce(initial_provider_version, 1),
            provider_version = coalesce(provider_version, 1)
            where initial_model_catalog_id is null or initial_model_version is null or
                initial_provider is null or initial_provider_version is null or
                provider_version is null;
        alter table intelligence.reference_runs
            alter column initial_model_catalog_id set not null,
            alter column initial_model_version set not null,
            alter column initial_provider set not null,
            alter column initial_provider_version set not null,
            alter column provider_version set not null;
        alter table intelligence.reference_runs
            add constraint ck_intelligence_reference_provider_versions
                check (initial_provider_version > 0 and provider_version > 0);
        """;
}
