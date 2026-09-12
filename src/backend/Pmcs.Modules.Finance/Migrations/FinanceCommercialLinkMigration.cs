using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Finance.Migrations;

internal sealed class FinanceCommercialLinkMigration : IDatabaseMigration
{
    public string ModuleName => "finance";

    public long Order => 651;

    public string Version => "20260909-002";

    public string Description => "Link financial records to validated commercial contract and commitment identities";

    public string Sql => """
        alter table finance.financial_records
            add column if not exists contract_id uuid null,
            add column if not exists commitment_id uuid null;

        create index if not exists ix_financial_records_project_contract
            on finance.financial_records(tenant_id, project_id, contract_id);
        create index if not exists ix_financial_records_project_commitment
            on finance.financial_records(tenant_id, project_id, commitment_id);
        """;
}
