using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectFinancialPositionReportSnapshotBuilder
{
    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        Pmcs.Modules.Projects.Contracts.ProjectControlProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectFinancialPositionReportingResult source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt) => Build(
        runId,
        tenantId,
        ProjectFinancialPositionPinnedProjectProfile.Capture(project, validatedAtUtc),
        sourceCutoffUtc,
        source,
        validatedAtUtc,
        builtAt);

    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ProjectFinancialPositionPinnedProjectProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectFinancialPositionReportingResult source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var acceptedAt = validatedAtUtc.ToUniversalTime();
        var timeZone = project.ValidateForRun(tenantId, project.Id, cutoff, acceptedAt);
        var cutoffLocalDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(cutoff, timeZone).DateTime);
        var canonical = ValidateAndCanonicalizeSource(
            tenantId,
            project.Id,
            cutoffLocalDate,
            cutoff,
            source);
        var sourceManifestJson = CanonicalJson.Serialize(canonical.SourceManifest);
        var sourceManifestSha256 = CanonicalJson.Sha256(sourceManifestJson);
        if (!string.Equals(
            sourceManifestSha256,
            canonical.SourceManifestSha256,
            StringComparison.Ordinal))
        {
            throw Invalid(
                "source_manifest.hash_mismatch",
                "The Finance source manifest hash does not match its canonical content.");
        }

        var classification = MapClassification(canonical.Classification);
        var payload = new ProjectFinancialPositionReportSemanticSnapshot(
            ProjectFinancialPositionReportRuntimeContract.SnapshotSchemaVersion,
            ProjectFinancialPositionReportRuntimeContract.SemanticContractId,
            ProjectFinancialPositionReportRuntimeContract.DefinitionCode,
            ProjectFinancialPositionReportRuntimeContract.DefinitionVersion,
            canonical.PolicyVersion,
            MapDataStatus(canonical.DataStatus),
            canonical.ReasonCodes,
            new ProjectFinancialPositionReportParameters(),
            new ProjectFinancialPositionReportProjectIdentity(
                project.Id,
                project.TenantId,
                project.Code,
                project.Name,
                project.TimeZone,
                project.BaseCurrencyCode,
                project.Revision,
                project.ConfigurationVersion,
                project.ConfigurationChangedAt.ToUniversalTime(),
                project.CapturedAtUtc.ToUniversalTime()),
            new ProjectFinancialPositionReportCutoffIdentity(cutoff, cutoffLocalDate),
            classification,
            MapConfiguration(canonical.Configuration),
            canonical.CashStatus,
            new ProjectFinancialPositionReportCashSummary(
                canonical.Cash.TotalReceipts,
                canonical.Cash.DirectPayments,
                canonical.Cash.PettyCashFunding,
                canonical.Cash.PettyCashExpenses,
                canonical.Cash.ExternalNetCash,
                canonical.Cash.RecognizedSpend,
                canonical.Cash.PettyCashBalance),
            canonical.ObligationStatus,
            canonical.BudgetStatus,
            canonical.BudgetComparisonStatus,
            canonical.Budget is null
                ? null
                : new ProjectFinancialPositionReportBudgetIdentity(
                    canonical.Budget.BaselineId,
                    canonical.Budget.Revision,
                    canonical.Budget.Amount,
                    canonical.Budget.CurrencyCode,
                    canonical.Budget.ApprovedAt,
                    canonical.Budget.SupersededAt),
            canonical.BudgetComparison is null
                ? null
                : new ProjectFinancialPositionReportBudgetComparison(
                    canonical.BudgetComparison.ApprovedBudgetAmount,
                    canonical.BudgetComparison.BudgetRemainingAmount,
                    canonical.BudgetComparison.BudgetConsumedPercent),
            MapSummary(canonical.PayableSummary),
            MapSummary(canonical.ReceivableSummary),
            canonical.Aging.Select(item => new ProjectFinancialPositionReportAgingSummary(
                item.Type,
                item.Bucket,
                item.Count,
                item.Amount)).ToArray(),
            canonical.OpenObligations.Select(item => new ProjectFinancialPositionReportOpenObligation(
                item.ObligationId,
                item.Type,
                item.NumberSnapshot,
                item.CounterpartySnapshot,
                item.IssueDate,
                item.DueDate,
                item.Amount,
                item.SettledAmountAtCutoff,
                item.OutstandingAmount,
                item.Bucket)).ToArray(),
            canonical.SourceCounts,
            canonical.SourceMaxChangedAt,
            sourceManifestSha256);

        return ReportSnapshot.Create(
            Guid.NewGuid(),
            runId,
            tenantId,
            project.Id,
            ProjectFinancialPositionReportRuntimeContract.SnapshotSchemaVersion,
            MapDataStatus(canonical.DataStatus),
            CanonicalJson.Serialize(payload),
            sourceManifestJson,
            classification,
            builtAt,
            cutoff);
    }

    private static ProjectFinancialPositionReportingResult ValidateAndCanonicalizeSource(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        ProjectFinancialPositionReportingResult source)
    {
        if (!string.Equals(
                source.ContractVersion,
                ProjectFinancialPositionReportingContract.Version,
                StringComparison.Ordinal) ||
            !string.Equals(
                source.PolicyVersion,
                ProjectFinancialPositionReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            source.TenantId != tenantId || source.ProjectId != projectId ||
            source.CutoffLocalDate != cutoffLocalDate ||
            source.SourceCutoffUtc.ToUniversalTime() != sourceCutoffUtc ||
            !Enum.IsDefined(source.Classification) ||
            source.Classification < ProjectFinancialPositionReportingClassification.Confidential ||
            !Enum.IsDefined(source.DataStatus) || !Enum.IsDefined(source.CashStatus) ||
            !Enum.IsDefined(source.ObligationStatus) || !Enum.IsDefined(source.BudgetStatus) ||
            !Enum.IsDefined(source.BudgetComparisonStatus) || source.ReasonCodes is null ||
            source.Cash is null || source.Aging is null || source.OpenObligations is null ||
            source.SourceCounts is null || source.SourceManifest is null ||
            string.IsNullOrWhiteSpace(source.SourceManifestSha256) ||
            source.SourceMaxChangedAt?.ToUniversalTime() > sourceCutoffUtc)
        {
            throw Invalid("source.invalid", "The Finance source result violates the report contract.");
        }

        ValidateConfiguration(source.Configuration);
        ValidateCash(source.CashStatus, source.Cash);
        ValidateObligations(source);
        ValidateBudget(source);
        ValidateStatus(source);
        ValidateCounts(source.SourceCounts);
        ValidateManifest(source, sourceCutoffUtc);

        var reasons = source.ReasonCodes
            .Select(item => Enum.IsDefined(item)
                ? item
                : throw Invalid("reason.invalid", "The Finance source returned an unknown reason code."))
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        if (reasons.Length != source.ReasonCodes.Count)
        {
            throw Invalid("reason.duplicate", "The Finance source returned duplicate reason codes.");
        }

        var aging = source.Aging
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Bucket)
            .ToArray();
        var rows = source.OpenObligations
            .OrderBy(item => item.Type)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.NumberSnapshot, StringComparer.Ordinal)
            .ThenBy(item => item.ObligationId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        return source with
        {
            SourceCutoffUtc = sourceCutoffUtc,
            ReasonCodes = reasons,
            Aging = aging,
            OpenObligations = rows,
            SourceMaxChangedAt = source.SourceMaxChangedAt?.ToUniversalTime()
        };
    }

    private static void ValidateConfiguration(ProjectFinancialConfigurationVersion? configuration)
    {
        if (configuration is null)
        {
            return;
        }

        if (configuration.ConfigurationVersion <= 0 || configuration.ProjectRevision <= 0 ||
            !Enum.IsDefined(configuration.FinanceState) || !Enum.IsDefined(configuration.BudgetState) ||
            !IsCurrency(configuration.BaseCurrencyCode) || configuration.EffectiveFromUtc == default ||
            configuration.EffectiveToUtc <= configuration.EffectiveFromUtc ||
            !Enum.IsDefined(configuration.Classification) ||
            configuration.Classification < ProjectFinancialPositionReportingClassification.Confidential)
        {
            throw Invalid(
                "configuration.invalid",
                "The effective Finance configuration violates the report contract.");
        }
    }

    private static void ValidateCash(
        ProjectFinancialPositionSectionStatus status,
        ProjectFinancialPositionCashSummary cash)
    {
        var values = new decimal?[]
        {
            cash.TotalReceipts,
            cash.DirectPayments,
            cash.PettyCashFunding,
            cash.PettyCashExpenses,
            cash.ExternalNetCash,
            cash.RecognizedSpend,
            cash.PettyCashBalance
        };
        if ((status == ProjectFinancialPositionSectionStatus.Available && values.Any(item => !item.HasValue)) ||
            (status != ProjectFinancialPositionSectionStatus.Available && values.Any(item => item.HasValue)) ||
            values.Where(item => item.HasValue).Any(item =>
                decimal.Round(item!.Value, 2, MidpointRounding.AwayFromZero) != item.Value))
        {
            throw Invalid("cash.invalid", "Cash metrics do not match their explicit status.");
        }
    }

    private static void ValidateObligations(ProjectFinancialPositionReportingResult source)
    {
        if (source.ObligationStatus != ProjectFinancialPositionSectionStatus.Available)
        {
            if (source.PayableSummary is not null || source.ReceivableSummary is not null ||
                source.Aging.Count > 0 || source.OpenObligations.Count > 0)
            {
                throw Invalid(
                    "obligation.invalid",
                    "Unavailable obligations cannot expose partial summary or Aging data.");
            }
            return;
        }

        if (source.PayableSummary is null || source.ReceivableSummary is null || source.Aging.Count != 8 ||
            source.Aging.Any(item => !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.Bucket) ||
                item.Count < 0 || item.Amount < 0 || !IsMoneyOrZero(item.Amount)) ||
            source.Aging.Select(item => (item.Type, item.Bucket)).Distinct().Count() != 8 ||
            source.OpenObligations.Any(item => item.ObligationId == Guid.Empty ||
                !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.Bucket) ||
                string.IsNullOrWhiteSpace(item.NumberSnapshot) || item.NumberSnapshot.Length > 80 ||
                item.CounterpartySnapshot?.Length > 200 || item.IssueDate == default ||
                item.DueDate < item.IssueDate || !IsPositiveMoney(item.Amount) ||
                !IsMoneyOrZero(item.SettledAmountAtCutoff) || !IsPositiveMoney(item.OutstandingAmount) ||
                item.Amount - item.SettledAmountAtCutoff != item.OutstandingAmount) ||
            source.OpenObligations.Select(item => item.ObligationId).Distinct().Count() !=
                source.OpenObligations.Count)
        {
            throw Invalid("obligation.invalid", "Obligation summary or Aging data is invalid.");
        }

        ValidateSummary(source.PayableSummary);
        ValidateSummary(source.ReceivableSummary);
    }

    private static void ValidateSummary(ProjectFinancialPositionObligationSummary summary)
    {
        if (!Enum.IsDefined(summary.Type) || summary.OpenCount < 0 || summary.OverdueCount < 0 ||
            summary.OverdueCount > summary.OpenCount || !IsMoneyOrZero(summary.OpenAmount) ||
            !IsMoneyOrZero(summary.OverdueAmount) || summary.OverdueAmount > summary.OpenAmount)
        {
            throw Invalid("obligation.summary.invalid", "An obligation summary is invalid.");
        }
    }

    private static void ValidateBudget(ProjectFinancialPositionReportingResult source)
    {
        if (source.BudgetStatus == ProjectFinancialPositionSectionStatus.Available)
        {
            if (source.Budget is null || source.BudgetComparison is null ||
                source.Budget.BaselineId == Guid.Empty || source.Budget.Revision <= 0 ||
                !IsPositiveMoney(source.Budget.Amount) || !IsCurrency(source.Budget.CurrencyCode) ||
                source.Budget.ApprovedAt == default ||
                source.Budget.SupersededAt <= source.Budget.ApprovedAt ||
                source.BudgetComparison.ApprovedBudgetAmount != source.Budget.Amount)
            {
                throw Invalid("budget.invalid", "Budget identity or comparison metadata is invalid.");
            }

            var valuesPresent = source.BudgetComparison.BudgetRemainingAmount.HasValue &&
                source.BudgetComparison.BudgetConsumedPercent.HasValue;
            if ((source.BudgetComparisonStatus == ProjectFinancialPositionSectionStatus.Available &&
                    !valuesPresent) ||
                (source.BudgetComparisonStatus != ProjectFinancialPositionSectionStatus.Available &&
                    valuesPresent))
            {
                throw Invalid(
                    "budget.comparison.invalid",
                    "Budget comparison metrics do not match their explicit status.");
            }
        }
        else if (source.Budget is not null || source.BudgetComparison is not null)
        {
            throw Invalid("budget.invalid", "Unavailable Budget data cannot expose a baseline or comparison.");
        }
    }

    private static void ValidateStatus(ProjectFinancialPositionReportingResult source)
    {
        var valid = source.DataStatus switch
        {
            ProjectFinancialPositionDataStatus.NotConfigured =>
                source.CashStatus == ProjectFinancialPositionSectionStatus.NotConfigured &&
                source.ObligationStatus == ProjectFinancialPositionSectionStatus.NotConfigured,
            ProjectFinancialPositionDataStatus.NoData =>
                source.CashStatus == ProjectFinancialPositionSectionStatus.NoData &&
                source.ObligationStatus == ProjectFinancialPositionSectionStatus.NoData,
            ProjectFinancialPositionDataStatus.InsufficientData =>
                source.CashStatus == ProjectFinancialPositionSectionStatus.InsufficientData ||
                source.ObligationStatus == ProjectFinancialPositionSectionStatus.InsufficientData,
            ProjectFinancialPositionDataStatus.Available =>
                source.CashStatus == ProjectFinancialPositionSectionStatus.Available ||
                source.ObligationStatus == ProjectFinancialPositionSectionStatus.Available,
            _ => false
        };
        if (!valid)
        {
            throw Invalid("status.invalid", "Financial data status conflicts with section statuses.");
        }
    }

    private static void ValidateCounts(ProjectFinancialSourceCounts counts)
    {
        var values = new[]
        {
            counts.FinancialRecordSourceCount,
            counts.OfficialFinancialRecordCount,
            counts.ExcludedFinancialRecordCount,
            counts.ObligationSourceCount,
            counts.OfficialObligationCount,
            counts.OpenObligationCount,
            counts.ExcludedObligationCount,
            counts.SettlementSourceCount,
            counts.EligibleSettlementCount,
            counts.ExcludedSettlementCount,
            counts.BudgetBaselineSourceCount,
            counts.EffectiveBudgetBaselineCount,
            counts.ExcludedBudgetBaselineCount,
            counts.IncompleteCollectionCount
        };
        if (values.Any(item => item < 0) ||
            counts.FinancialRecordSourceCount !=
                counts.OfficialFinancialRecordCount + counts.ExcludedFinancialRecordCount ||
            counts.ObligationSourceCount !=
                counts.OfficialObligationCount + counts.ExcludedObligationCount ||
            counts.SettlementSourceCount !=
                counts.EligibleSettlementCount + counts.ExcludedSettlementCount ||
            counts.BudgetBaselineSourceCount !=
                counts.EffectiveBudgetBaselineCount + counts.ExcludedBudgetBaselineCount ||
            counts.EffectiveBudgetBaselineCount > 1 || counts.IncompleteCollectionCount > 3 ||
            counts.OpenObligationCount > counts.OfficialObligationCount)
        {
            throw Invalid("source_counts.invalid", "Finance source counts are inconsistent.");
        }
    }

    private static void ValidateManifest(
        ProjectFinancialPositionReportingResult source,
        DateTimeOffset cutoff)
    {
        var manifest = source.SourceManifest;
        if (!string.Equals(
                manifest.ManifestVersion,
                ProjectFinancialPositionReportingContract.SourceManifestVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.SourceContractVersion,
                ProjectFinancialPositionReportingContract.Version,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.PolicyVersion,
                ProjectFinancialPositionReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            manifest.TenantId != source.TenantId || manifest.ProjectId != source.ProjectId ||
            manifest.CutoffLocalDate != source.CutoffLocalDate ||
            manifest.SourceCutoffUtc.ToUniversalTime() != cutoff ||
            manifest.FinancialRecords is null || manifest.Obligations is null ||
            manifest.Settlements is null || manifest.BudgetBaselines is null ||
            !Enum.IsDefined(manifest.LedgerCompleteness) ||
            !Enum.IsDefined(manifest.ObligationCompleteness) ||
            !Enum.IsDefined(manifest.SettlementLineageCompleteness))
        {
            throw Invalid("source_manifest.invalid", "The Finance source manifest is invalid.");
        }
    }

    private static ProjectFinancialPositionReportConfiguration? MapConfiguration(
        ProjectFinancialConfigurationVersion? configuration) => configuration is null
        ? null
        : new ProjectFinancialPositionReportConfiguration(
            configuration.ConfigurationVersion,
            configuration.ProjectRevision,
            configuration.FinanceState,
            configuration.BudgetState,
            configuration.BaseCurrencyCode,
            configuration.EffectiveFromUtc,
            configuration.EffectiveToUtc);

    private static ProjectFinancialPositionReportObligationSummary? MapSummary(
        ProjectFinancialPositionObligationSummary? summary) => summary is null
        ? null
        : new ProjectFinancialPositionReportObligationSummary(
            summary.Type,
            summary.OpenCount,
            summary.OpenAmount,
            summary.OverdueCount,
            summary.OverdueAmount);

    private static ReportDataStatus MapDataStatus(ProjectFinancialPositionDataStatus status) => status switch
    {
        ProjectFinancialPositionDataStatus.NotConfigured => ReportDataStatus.NotConfigured,
        ProjectFinancialPositionDataStatus.NoData => ReportDataStatus.NoData,
        ProjectFinancialPositionDataStatus.InsufficientData => ReportDataStatus.InsufficientData,
        ProjectFinancialPositionDataStatus.Available => ReportDataStatus.Available,
        _ => throw Invalid("data_status.invalid", "The Finance source data status is unknown.")
    };

    private static ReportClassification MapClassification(
        ProjectFinancialPositionReportingClassification classification) => classification switch
    {
        ProjectFinancialPositionReportingClassification.Confidential => ReportClassification.Confidential,
        ProjectFinancialPositionReportingClassification.Restricted => ReportClassification.Restricted,
        _ => throw Invalid(
            "classification.invalid",
            "Certified financial output cannot use an unknown or Internal classification.")
    };

    private static bool IsCurrency(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 3 && value.All(char.IsAsciiLetterUpper);

    private static bool IsPositiveMoney(decimal value) => value > 0 && IsMoneyOrZero(value);

    private static bool IsMoneyOrZero(decimal value) =>
        value >= 0 && decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"reporting.project_financial_position.{suffix}", message);
}
