using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceProviderRegistrationMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 606;
    public string Version => "20261001-006";
    public string Description => "Add explicit default-deny provider activation catalog";

    public string Sql => """
        create table if not exists intelligence.provider_registrations (
            provider varchar(40) primary key,
            version integer not null,
            enabled boolean not null,
            verified_at timestamptz null,
            created_by uuid not null,
            created_at timestamptz not null,
            revision bigint not null,
            constraint ck_intelligence_provider_enabled check (not enabled or verified_at is not null),
            constraint ck_intelligence_provider_version check (version > 0 and revision > 0),
            constraint ck_intelligence_provider_name check
                (provider in ('OpenAI','GoogleGemini','AnthropicClaude'))
        );
        """;
}
