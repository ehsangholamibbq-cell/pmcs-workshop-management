using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Finance.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Services;

internal sealed class ProjectFinancialPositionReportingSource(
    FinanceDbContext dbContext,
    IProjectDirectory projectDirectory) : IProjectFinancialPositionReportingSource
{
    public async Task<ProjectFinancialPositionReportingResult> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            throw new DomainRuleException(
                "finance.financial_position_reporting.project.not_found",
                "The Project financial position reporting scope does not exist.");
        }

        var records = await dbContext.FinancialRecords
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectFinancialPositionReportingContract.MaximumFinancialRecords + 1)
            .ToArrayAsync(cancellationToken);
        var obligations = await dbContext.FinancialObligations
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectFinancialPositionReportingContract.MaximumObligations + 1)
            .ToArrayAsync(cancellationToken);
        var settlements = await dbContext.FinancialSettlements
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.SettledAt <= cutoff)
            .Take(ProjectFinancialPositionReportingContract.MaximumSettlements + 1)
            .ToArrayAsync(cancellationToken);
        var baselines = await dbContext.BudgetBaselines
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectFinancialPositionReportingContract.MaximumBudgetBaselines + 1)
            .ToArrayAsync(cancellationToken);
        var projection = ProjectFinancialPositionReportingCompatibilityProjection.Create(
            project,
            cutoffLocalDate,
            cutoff,
            records,
            obligations,
            settlements,
            baselines);
        return ProjectFinancialPositionReportingCalculator.Calculate(
            ProjectFinancialPositionReportingSelector.Select(projection));
    }
}

internal static class ProjectFinancialPositionReportingCompatibilityProjection
{
    public static ProjectFinancialPositionReportingProjection Create(
        ProjectControlProfile project,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        IReadOnlyCollection<FinancialRecord> records,
        IReadOnlyCollection<FinancialObligation> obligations,
        IReadOnlyCollection<FinancialSettlement> settlements,
        IReadOnlyCollection<BudgetBaseline> budgetBaselines)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(obligations);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(budgetBaselines);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty || cutoff == default ||
            cutoffLocalDate == default || !project.ConfigurationChangedAt.HasValue ||
            project.ConfigurationVersion <= 0 || project.Revision <= 0 ||
            project.ConfigurationChangedAt.Value.ToUniversalTime() > cutoff)
        {
            throw HistoryUnavailable(
                "configuration_history.unavailable",
                "The current Project profile cannot safely reconstruct Finance configuration at this cutoff.");
        }

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw HistoryUnavailable(
                "time_zone.invalid",
                "The Project time zone is unknown or invalid.");
        }
        catch (InvalidTimeZoneException)
        {
            throw HistoryUnavailable(
                "time_zone.invalid",
                "The Project time zone is unknown or invalid.");
        }

        var derivedLocalDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(cutoff, timeZone).DateTime);
        if (derivedLocalDate != cutoffLocalDate)
        {
            throw HistoryUnavailable(
                "cutoff.invalid",
                "The local cutoff date does not match the versioned Project time zone.");
        }

        var scopedRecords = records.Where(item => item.CreatedAt.ToUniversalTime() <= cutoff).ToArray();
        var scopedObligations = obligations.Where(item => item.CreatedAt.ToUniversalTime() <= cutoff).ToArray();
        var scopedSettlements = settlements.Where(item => item.SettledAt.ToUniversalTime() <= cutoff).ToArray();
        var scopedBaselines = budgetBaselines.Where(item => item.CreatedAt.ToUniversalTime() <= cutoff).ToArray();
        if (scopedBaselines.Any(item => item.Status == BudgetBaselineStatus.Superseded))
        {
            throw HistoryUnavailable(
                "budget_history.unavailable",
                "A legacy superseded Budget Baseline does not retain independent approval and supersession timestamps.");
        }

        var changedAt = project.ConfigurationChangedAt.Value.ToUniversalTime();
        const ProjectFinancialPositionReportingClassification classification =
            ProjectFinancialPositionReportingClassification.Confidential;
        return new ProjectFinancialPositionReportingProjection(
            ProjectFinancialPositionReportingContract.Version,
            project.TenantId,
            project.Id,
            cutoffLocalDate,
            cutoff,
            [new ProjectFinancialConfigurationVersion(
                project.ConfigurationVersion,
                project.Revision,
                project.Finance,
                project.Budget,
                project.BaseCurrencyCode,
                changedAt,
                null,
                classification)],
            scopedRecords.Select(item => Map(item, classification)).ToArray(),
            scopedObligations.Select(item => Map(item, classification)).ToArray(),
            scopedSettlements.Select(item => Map(item, classification)).ToArray(),
            scopedBaselines.Select(item => Map(item, classification)).ToArray(),
            ProjectFinancialSourceCompleteness.Complete,
            ProjectFinancialSourceCompleteness.Complete,
            ProjectFinancialSourceCompleteness.Complete,
            classification);
    }

    private static ProjectFinancialRecordVersion Map(
        FinancialRecord item,
        ProjectFinancialPositionReportingClassification classification)
    {
        var postedAt = item.Status == FinancialRecordStatus.Posted
            ? item.ReviewedAt?.ToUniversalTime()
            : null;
        if (item.Status == FinancialRecordStatus.Posted && !postedAt.HasValue)
        {
            throw HistoryUnavailable(
                "record_history.unavailable",
                "A posted legacy Financial Record does not retain its posting timestamp.");
        }

        return new ProjectFinancialRecordVersion(
            item.Id,
            item.TenantId,
            item.ProjectId,
            item.Revision,
            item.Type,
            item.Status,
            item.TransactionDate,
            item.Amount,
            item.CurrencyCode,
            item.CreatedAt.ToUniversalTime(),
            postedAt,
            classification);
    }

    private static ProjectFinancialObligationVersion Map(
        FinancialObligation item,
        ProjectFinancialPositionReportingClassification classification)
    {
        var official = item.Status is FinancialObligationStatus.Approved or
            FinancialObligationStatus.PartiallySettled or FinancialObligationStatus.Settled;
        var approvedAt = official ? item.ReviewedAt?.ToUniversalTime() : null;
        if (official && !approvedAt.HasValue)
        {
            throw HistoryUnavailable(
                "obligation_history.unavailable",
                "An approved legacy obligation does not retain its approval timestamp.");
        }

        return new ProjectFinancialObligationVersion(
            item.Id,
            item.TenantId,
            item.ProjectId,
            item.Revision,
            item.Type,
            item.Status,
            item.Number,
            item.Counterparty,
            item.IssueDate,
            item.DueDate,
            item.Amount,
            item.CurrencyCode,
            item.CreatedAt.ToUniversalTime(),
            approvedAt,
            classification);
    }

    private static ProjectFinancialSettlementVersion Map(
        FinancialSettlement item,
        ProjectFinancialPositionReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.ObligationId,
        item.FinancialRecordId,
        item.Amount,
        item.SettledAt.ToUniversalTime(),
        classification);

    private static ProjectBudgetBaselineVersion Map(
        BudgetBaseline item,
        ProjectFinancialPositionReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.Status,
        item.Amount,
        item.CurrencyCode,
        item.CreatedAt.ToUniversalTime(),
        item.Status == BudgetBaselineStatus.Approved
            ? item.ReviewedAt?.ToUniversalTime()
            : null,
        null,
        classification);

    private static DomainRuleException HistoryUnavailable(string suffix, string message) =>
        new($"finance.financial_position_reporting.{suffix}", message);
}
