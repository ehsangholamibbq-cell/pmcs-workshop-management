using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Finance.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Services;

internal sealed class FinanceVerificationService(
    FinanceDbContext dbContext,
    IProjectDirectory projectDirectory,
    IProjectLocationDirectory locationDirectory,
    ICommercialReferenceDirectory commercialReferenceDirectory,
    IClock clock) : IFinanceVerificationService
{
    private const string VerificationVersion = "finance-verification-v1";

    public async Task<FinanceVerificationRecord?> VerifyAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var records = await dbContext.FinancialRecords.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var obligations = await dbContext.FinancialObligations.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var pettyCash = await dbContext.PettyCashRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var feePolicies = await dbContext.ManagementFeePolicies.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var latest = await dbContext.FinancialStateSnapshots.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CalculatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var asOfDate = latest?.AsOfDate ?? FinancialStateSource.ResolveLocalDate(clock.UtcNow, project.TimeZone);
        var entries = records
            .Where(item => item.Status == FinancialRecordStatus.Posted && item.TransactionDate <= asOfDate)
            .Select(item => new PostedFinancialEntry(
                item.Id,
                item.Type,
                item.TransactionDate,
                item.Amount,
                item.CurrencyCode,
                item.ReviewedAt!.Value))
            .ToArray();
        var budget = await dbContext.BudgetBaselines.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.Status == BudgetBaselineStatus.Approved)
            .OrderByDescending(item => item.ReviewedAt)
            .Select(item => new ApprovedBudget(item.Id, item.Amount, item.CurrencyCode, item.ReviewedAt!.Value))
            .FirstOrDefaultAsync(cancellationToken);
        var calculated = FinancialStateCalculator.Calculate(project, entries, budget, asOfDate, clock.UtcNow);
        var mismatches = FinanceVerificationComparer.Compare(latest, calculated);

        var invalidLocationLinks = await CountInvalidLocationsAsync(
            tenantId,
            projectId,
            records.Select(item => item.LocationId)
                .Concat(obligations.Select(item => item.LocationId))
                .Concat(pettyCash.Select(item => item.LocationId)),
            cancellationToken);
        var invalidCommercialLinks = await CountInvalidCommercialLinksAsync(
            tenantId,
            projectId,
            records.Select(item => (item.PartyId, item.ContractId, item.CommitmentId))
                .Concat(obligations.Select(item => (item.PartyId, item.ContractId, item.CommitmentId))),
            cancellationToken);
        var audit = await ReadAuditVerificationAsync(tenantId, projectId, cancellationToken);
        var noSnapshotExpected = latest is null && entries.Length == 0 && budget is null;
        var snapshotMatches = noSnapshotExpected || latest is not null && mismatches.Count == 0;
        var passed = snapshotMatches && audit.RecordsWithoutAuditCount == 0 &&
            audit.AuditEventsWithoutCorrelationCount == 0 && invalidLocationLinks == 0 &&
            invalidCommercialLinks == 0;

        return new FinanceVerificationRecord(
            projectId,
            VerificationVersion,
            clock.UtcNow,
            asOfDate,
            snapshotMatches,
            mismatches,
            records.Count,
            obligations.Count,
            pettyCash.Count,
            feePolicies.Count,
            audit.EventCount,
            audit.RecordsWithoutAuditCount,
            audit.AuditEventsWithoutCorrelationCount,
            invalidLocationLinks,
            invalidCommercialLinks,
            passed);
    }

    private async Task<int> CountInvalidLocationsAsync(
        Guid tenantId,
        Guid projectId,
        IEnumerable<Guid?> locationIds,
        CancellationToken cancellationToken)
    {
        var invalid = 0;
        foreach (var locationId in locationIds.Where(item => item.HasValue).Select(item => item!.Value).Distinct())
        {
            if (await locationDirectory.FindAsync(tenantId, projectId, locationId, cancellationToken) is null)
            {
                invalid++;
            }
        }

        return invalid;
    }

    private async Task<int> CountInvalidCommercialLinksAsync(
        Guid tenantId,
        Guid projectId,
        IEnumerable<(Guid? PartyId, Guid? ContractId, Guid? CommitmentId)> links,
        CancellationToken cancellationToken)
    {
        var invalid = 0;
        foreach (var link in links.Distinct())
        {
            var validation = await commercialReferenceDirectory.ValidateAsync(
                tenantId,
                projectId,
                link.ContractId,
                link.CommitmentId,
                link.PartyId,
                cancellationToken);
            if (!validation.IsValid)
            {
                invalid++;
            }
        }

        return invalid;
    }

    private async Task<AuditVerification> ReadAuditVerificationAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select
                    (select count(*)::int
                     from foundation.audit_events a
                     where a.tenant_id = @tenant_id and a.project_id = @project_id
                       and a.resource_type in ('FinancialRecord', 'FinancialObligation', 'PettyCashRequest', 'ManagementFeePolicy')),
                    (select count(*)::int
                     from foundation.audit_events a
                     where a.tenant_id = @tenant_id and a.project_id = @project_id
                       and a.resource_type in ('FinancialRecord', 'FinancialObligation', 'PettyCashRequest', 'ManagementFeePolicy')
                       and (a.correlation_id is null or btrim(a.correlation_id) = '')),
                    (select count(*)::int from (
                        select r.id::text as resource_id, 'FinancialRecord' as resource_type
                        from finance.financial_records r
                        where r.tenant_id = @tenant_id and r.project_id = @project_id
                        union all
                        select o.id::text, 'FinancialObligation'
                        from finance.financial_obligations o
                        where o.tenant_id = @tenant_id and o.project_id = @project_id
                        union all
                        select p.id::text, 'PettyCashRequest'
                        from finance.petty_cash_requests p
                        where p.tenant_id = @tenant_id and p.project_id = @project_id
                        union all
                        select m.id::text, 'ManagementFeePolicy'
                        from finance.management_fee_policies m
                        where m.tenant_id = @tenant_id and m.project_id = @project_id
                    ) resources
                    where not exists (
                        select 1 from foundation.audit_events a
                        where a.tenant_id = @tenant_id and a.project_id = @project_id
                          and a.resource_type = resources.resource_type
                          and a.resource_id = resources.resource_id));
                """;
            AddParameter(command, "tenant_id", tenantId);
            AddParameter(command, "project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new AuditVerification(0, 0, 0);
            }

            return new AuditVerification(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record AuditVerification(
        int EventCount,
        int AuditEventsWithoutCorrelationCount,
        int RecordsWithoutAuditCount);
}

internal static class FinanceVerificationComparer
{
    public static IReadOnlyCollection<string> Compare(
        FinancialStateSnapshot? snapshot,
        FinancialStateCalculation calculated)
    {
        if (snapshot is null)
        {
            return ["snapshot.missing"];
        }

        var mismatches = new List<string>();
        Compare(mismatches, "calculationVersion", snapshot.CalculationVersion, calculated.CalculationVersion);
        Compare(mismatches, "asOfDate", snapshot.AsOfDate, calculated.AsOfDate);
        Compare(mismatches, "currencyCode", snapshot.CurrencyCode, calculated.CurrencyCode);
        Compare(mismatches, "status", snapshot.Status, calculated.Status);
        Compare(mismatches, "dataQualityStatus", snapshot.DataQualityStatus, calculated.DataQualityStatus);
        Compare(mismatches, "budgetComparisonState", snapshot.BudgetComparisonState, calculated.BudgetComparisonState);
        Compare(mismatches, "postedRecordCount", snapshot.PostedRecordCount, calculated.PostedRecordCount);
        Compare(mismatches, "totalReceipts", snapshot.TotalReceipts, calculated.TotalReceipts);
        Compare(mismatches, "directPayments", snapshot.DirectPayments, calculated.DirectPayments);
        Compare(mismatches, "pettyCashFunding", snapshot.PettyCashFunding, calculated.PettyCashFunding);
        Compare(mismatches, "pettyCashExpenses", snapshot.PettyCashExpenses, calculated.PettyCashExpenses);
        Compare(mismatches, "externalNetCash", snapshot.ExternalNetCash, calculated.ExternalNetCash);
        Compare(mismatches, "recognizedSpend", snapshot.RecognizedSpend, calculated.RecognizedSpend);
        Compare(mismatches, "pettyCashBalance", snapshot.PettyCashBalance, calculated.PettyCashBalance);
        Compare(mismatches, "approvedBudgetAmount", snapshot.ApprovedBudgetAmount, calculated.ApprovedBudgetAmount);
        Compare(mismatches, "budgetRemainingAmount", snapshot.BudgetRemainingAmount, calculated.BudgetRemainingAmount);
        Compare(mismatches, "budgetConsumedPercent", snapshot.BudgetConsumedPercent, calculated.BudgetConsumedPercent);
        Compare(mismatches, "sourceMaxChangedAt", snapshot.SourceMaxChangedAt, calculated.SourceMaxChangedAt);
        return mismatches;
    }

    private static void Compare<T>(ICollection<string> mismatches, string field, T persisted, T calculated)
    {
        if (!EqualityComparer<T>.Default.Equals(persisted, calculated))
        {
            mismatches.Add(field);
        }
    }
}
