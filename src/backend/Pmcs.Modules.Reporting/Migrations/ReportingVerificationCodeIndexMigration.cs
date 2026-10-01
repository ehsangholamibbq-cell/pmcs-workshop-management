using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class ReportingVerificationCodeIndexMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";

    public long Order => 1201;

    public string Version => "20260918-002";

    public string Description => "Allow deterministic verification codes across equivalent certified artifacts";

    public string Sql => """
        alter table reporting.report_outputs
            drop constraint if exists report_outputs_verification_code_key;

        create index if not exists ix_reporting_outputs_verification_code
            on reporting.report_outputs(verification_code);
        """;
}
