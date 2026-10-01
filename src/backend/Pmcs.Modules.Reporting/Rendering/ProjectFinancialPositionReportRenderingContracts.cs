using System.Text.Json;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectFinancialPositionReportRenderSnapshot
{
    public static ProjectFinancialPositionReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectFinancialPositionReportSemanticSnapshot>(
                payloadJson,
                CanonicalJson.SerializerOptions)
                ?? throw Invalid("Project Financial Position snapshot payload is empty.");
            ProjectFinancialPositionReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid("Project Financial Position snapshot payload cannot be rendered.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw Invalid("Project Financial Position snapshot payload uses an unsupported value.", exception);
        }
    }

    private static ReportRenderingException Invalid(string message, Exception? innerException = null) => new(
        "reporting.project_financial_position.snapshot.payload_invalid",
        transient: false,
        message,
        innerException);
}

internal sealed record ProjectFinancialPositionReportRenderRequest(
    Guid RunId,
    Guid OutputId,
    Guid SnapshotId,
    Guid TemplateVersionId,
    string DefinitionCode,
    string DefinitionVersion,
    string TemplateVersion,
    string TemplateContentDigest,
    string RendererContractVersion,
    string LayoutContractVersion,
    ReportFormat Format,
    string FileName,
    string VerificationCode,
    string ManifestSha256,
    string SnapshotSha256,
    string SourceManifestSha256,
    DateTimeOffset SourceCutoffUtc,
    ProjectFinancialPositionReportSemanticSnapshot Snapshot);

internal interface IProjectFinancialPositionReportRenderer
{
    ReportFormat Format { get; }

    RenderedReportArtifact Render(ProjectFinancialPositionReportRenderRequest request);
}

internal sealed class ProjectFinancialPositionReportRenderModel
{
    private ProjectFinancialPositionReportRenderModel(
        ProjectFinancialPositionReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        ReasonCodes = Snapshot.ReasonCodes.OrderBy(item => item).ToArray();
        Aging = Snapshot.Aging
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Bucket)
            .ToArray();
        OpenObligations = Snapshot.OpenObligations
            .OrderBy(item => item.Type)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.NumberSnapshot, StringComparer.Ordinal)
            .ThenBy(item => item.ObligationId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
    }

    public ProjectFinancialPositionReportRenderRequest Request { get; }

    public ProjectFinancialPositionReportSemanticSnapshot Snapshot { get; }

    public IReadOnlyList<ProjectFinancialPositionReasonCode> ReasonCodes { get; }

    public IReadOnlyList<ProjectFinancialPositionReportAgingSummary> Aging { get; }

    public IReadOnlyList<ProjectFinancialPositionReportOpenObligation> OpenObligations { get; }

    public static ProjectFinancialPositionReportRenderModel Create(
        ProjectFinancialPositionReportRenderRequest request)
    {
        ProjectFinancialPositionReportRenderingContract.ValidateRequest(request);
        return new ProjectFinancialPositionReportRenderModel(request);
    }
}

internal static class ProjectFinancialPositionReportRenderingContract
{
    public const int MaximumProjectCodeLength = 160;
    public const int MaximumProjectNameLength = 400;
    public const int MaximumObligationNumberLength = 80;
    public const int MaximumCounterpartyLength = 200;

    public static void ValidateRequest(ProjectFinancialPositionReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        var extension = request.Format switch
        {
            ReportFormat.Pdf => ".pdf",
            ReportFormat.Xlsx => ".xlsx",
            _ => throw InvalidRequest(
                "The Project Financial Position output format is unsupported.")
        };
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            !string.Equals(
                request.DefinitionCode,
                ProjectFinancialPositionReportRuntimeContract.DefinitionCode,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.DefinitionVersion,
                ProjectFinancialPositionReportRuntimeContract.DefinitionVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.TemplateVersion,
                ProjectFinancialPositionReportRuntimeContract.TemplateVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.TemplateContentDigest,
                ProjectFinancialPositionReportRuntimeContract.TemplateContentDigest,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.RendererContractVersion,
                ProjectFinancialPositionReportRuntimeContract.RendererContractVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.LayoutContractVersion,
                ProjectFinancialPositionReportRuntimeContract.LayoutContractVersion,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            !request.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                request.FileName,
                ReportArtifactIdentity.FileName(request.Snapshot, request.Format),
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !IsSha256(request.TemplateContentDigest) || !IsSha256(request.ManifestSha256) ||
            !IsSha256(request.SnapshotSha256) || !IsSha256(request.SourceManifestSha256))
        {
            throw InvalidRequest(
                "The Project Financial Position render request violates its pinned contract.");
        }

        var canonicalSnapshot = CanonicalJson.Serialize(request.Snapshot);
        if (!string.Equals(
                CanonicalJson.Sha256(canonicalSnapshot),
                request.SnapshotSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.SourceManifestSha256,
                request.Snapshot.SourceManifestSha256,
                StringComparison.Ordinal) ||
            request.SourceCutoffUtc.ToUniversalTime() !=
                request.Snapshot.Cutoff.SourceCutoffUtc.ToUniversalTime())
        {
            throw InvalidRequest(
                "The render request does not match its immutable financial snapshot identity.");
        }
    }

    public static void ValidateSnapshot(ProjectFinancialPositionReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(
                snapshot.SchemaVersion,
                ProjectFinancialPositionReportRuntimeContract.SnapshotSchemaVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.SemanticContractId,
                ProjectFinancialPositionReportRuntimeContract.SemanticContractId,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.DefinitionCode,
                ProjectFinancialPositionReportRuntimeContract.DefinitionCode,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.DefinitionVersion,
                ProjectFinancialPositionReportRuntimeContract.DefinitionVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.PolicyVersion,
                ProjectFinancialPositionReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            !Enum.IsDefined(snapshot.DataStatus) || snapshot.DataStatus == ReportDataStatus.Pending ||
            !Enum.IsDefined(snapshot.Classification) ||
            snapshot.Classification < ReportClassification.Confidential ||
            snapshot.ReasonCodes is null || snapshot.Parameters is null || snapshot.Project is null ||
            snapshot.Cutoff is null || snapshot.Cash is null || snapshot.Aging is null ||
            snapshot.OpenObligations is null || snapshot.SourceCounts is null ||
            !Enum.IsDefined(snapshot.CashStatus) || !Enum.IsDefined(snapshot.ObligationStatus) ||
            !Enum.IsDefined(snapshot.BudgetStatus) ||
            !Enum.IsDefined(snapshot.BudgetComparisonStatus) ||
            !IsSha256(snapshot.SourceManifestSha256))
        {
            throw InvalidSnapshot(
                "Project Financial Position snapshot identity or required collections are invalid.");
        }

        ValidateProject(snapshot.Project, snapshot.Cutoff);
        ValidateCutoff(snapshot.Cutoff);
        ValidateReasons(snapshot.ReasonCodes);
        ValidateConfiguration(snapshot.Configuration, snapshot);
        ValidateCash(snapshot.CashStatus, snapshot.Cash);
        ValidateObligations(snapshot);
        ValidateBudget(snapshot);
        ValidateStatus(snapshot);
        ValidateCounts(snapshot.SourceCounts, snapshot.OpenObligations.Count);
        if (snapshot.SourceMaxChangedAt.HasValue &&
            (snapshot.SourceMaxChangedAt.Value.Offset != TimeSpan.Zero ||
                snapshot.SourceMaxChangedAt.Value > snapshot.Cutoff.SourceCutoffUtc))
        {
            throw InvalidSnapshot("Project Financial Position source change time is invalid.");
        }
    }

    private static void ValidateProject(
        ProjectFinancialPositionReportProjectIdentity project,
        ProjectFinancialPositionReportCutoffIdentity cutoff)
    {
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(project.Code) || project.Code.Length > MaximumProjectCodeLength ||
            string.IsNullOrWhiteSpace(project.Name) || project.Name.Length > MaximumProjectNameLength ||
            string.IsNullOrWhiteSpace(project.TimeZone) || !IsCurrency(project.CapturedBaseCurrencyCode) ||
            project.Revision <= 0 || project.ConfigurationVersion <= 0 ||
            project.ConfigurationChangedAt == default ||
            project.ConfigurationChangedAt.Offset != TimeSpan.Zero ||
            project.ProfileCapturedAtUtc == default ||
            project.ProfileCapturedAtUtc.Offset != TimeSpan.Zero ||
            project.ConfigurationChangedAt > project.ProfileCapturedAtUtc ||
            cutoff.SourceCutoffUtc > project.ProfileCapturedAtUtc)
        {
            throw InvalidSnapshot("Project Financial Position project identity is invalid.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw InvalidSnapshot("Project Financial Position time zone is invalid.", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw InvalidSnapshot("Project Financial Position time zone is invalid.", exception);
        }
    }

    private static void ValidateCutoff(ProjectFinancialPositionReportCutoffIdentity cutoff)
    {
        if (cutoff.SourceCutoffUtc == default || cutoff.SourceCutoffUtc.Offset != TimeSpan.Zero ||
            cutoff.CutoffLocalDate == default)
        {
            throw InvalidSnapshot("Project Financial Position cutoff identity is invalid.");
        }
    }

    private static void ValidateReasons(
        IReadOnlyCollection<ProjectFinancialPositionReasonCode> reasons)
    {
        var canonical = reasons.OrderBy(item => item).ToArray();
        if (reasons.Any(reason => !Enum.IsDefined(reason)) ||
            reasons.Distinct().Count() != reasons.Count || !reasons.SequenceEqual(canonical))
        {
            throw InvalidSnapshot(
                "Project Financial Position reason codes are invalid or non-canonical.");
        }
    }

    private static void ValidateConfiguration(
        ProjectFinancialPositionReportConfiguration? configuration,
        ProjectFinancialPositionReportSemanticSnapshot snapshot)
    {
        if (configuration is null)
        {
            return;
        }

        if (configuration.ConfigurationVersion <= 0 || configuration.ProjectRevision <= 0 ||
            configuration.ConfigurationVersion != snapshot.Project.ConfigurationVersion ||
            configuration.ProjectRevision != snapshot.Project.Revision ||
            !Enum.IsDefined(configuration.FinanceState) ||
            !Enum.IsDefined(configuration.BudgetState) ||
            !IsCurrency(configuration.BaseCurrencyCode) ||
            !string.Equals(
                configuration.BaseCurrencyCode,
                snapshot.Project.CapturedBaseCurrencyCode,
                StringComparison.Ordinal) ||
            configuration.EffectiveFromUtc == default ||
            configuration.EffectiveFromUtc.Offset != TimeSpan.Zero ||
            configuration.EffectiveFromUtc > snapshot.Cutoff.SourceCutoffUtc ||
            configuration.EffectiveToUtc.HasValue &&
                (configuration.EffectiveToUtc.Value.Offset != TimeSpan.Zero ||
                    configuration.EffectiveToUtc.Value <= snapshot.Cutoff.SourceCutoffUtc))
        {
            throw InvalidSnapshot(
                "Project Financial Position effective configuration is invalid.");
        }
    }

    private static void ValidateCash(
        ProjectFinancialPositionSectionStatus status,
        ProjectFinancialPositionReportCashSummary cash)
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
        if ((status == ProjectFinancialPositionSectionStatus.Available &&
                values.Any(item => !item.HasValue)) ||
            (status != ProjectFinancialPositionSectionStatus.Available &&
                values.Any(item => item.HasValue)) ||
            values.Where(item => item.HasValue).Any(item => !IsMoney(item!.Value)))
        {
            throw InvalidSnapshot(
                "Project Financial Position cash metrics do not match their explicit status.");
        }
        if (status != ProjectFinancialPositionSectionStatus.Available)
        {
            return;
        }

        var receipts = cash.TotalReceipts!.Value;
        var payments = cash.DirectPayments!.Value;
        var funding = cash.PettyCashFunding!.Value;
        var expenses = cash.PettyCashExpenses!.Value;
        if (receipts < 0 || payments < 0 || funding < 0 || expenses < 0 ||
            cash.RecognizedSpend!.Value < 0 ||
            cash.ExternalNetCash!.Value != receipts - payments - funding ||
            cash.RecognizedSpend.Value != payments + expenses ||
            cash.PettyCashBalance!.Value != funding - expenses)
        {
            throw InvalidSnapshot("Project Financial Position cash formulas are invalid.");
        }
    }

    private static void ValidateObligations(
        ProjectFinancialPositionReportSemanticSnapshot snapshot)
    {
        if (snapshot.ObligationStatus != ProjectFinancialPositionSectionStatus.Available)
        {
            if (snapshot.PayableSummary is not null || snapshot.ReceivableSummary is not null ||
                snapshot.Aging.Count > 0 || snapshot.OpenObligations.Count > 0)
            {
                throw InvalidSnapshot(
                    "Unavailable obligations cannot expose partial summary or Aging data.");
            }
            return;
        }

        if (snapshot.PayableSummary is null || snapshot.ReceivableSummary is null ||
            snapshot.PayableSummary.Type != FinancialObligationType.Payable ||
            snapshot.ReceivableSummary.Type != FinancialObligationType.Receivable ||
            snapshot.Aging.Count != 8 || snapshot.Aging.Any(item => item is null) ||
            snapshot.OpenObligations.Any(item => item is null))
        {
            throw InvalidSnapshot(
                "Project Financial Position obligation summaries are incomplete.");
        }

        var canonicalRows = snapshot.OpenObligations
            .OrderBy(item => item.Type)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.NumberSnapshot, StringComparer.Ordinal)
            .ThenBy(item => item.ObligationId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (!snapshot.OpenObligations.SequenceEqual(canonicalRows) ||
            snapshot.OpenObligations.Select(item => item.ObligationId).Distinct().Count() !=
                snapshot.OpenObligations.Count)
        {
            throw InvalidSnapshot(
                "Project Financial Position obligations are duplicated or non-canonical.");
        }

        foreach (var item in snapshot.OpenObligations)
        {
            if (item is null || item.ObligationId == Guid.Empty || !Enum.IsDefined(item.Type) ||
                !Enum.IsDefined(item.Bucket) || string.IsNullOrWhiteSpace(item.NumberSnapshot) ||
                item.NumberSnapshot.Length > MaximumObligationNumberLength ||
                item.CounterpartySnapshot?.Length > MaximumCounterpartyLength ||
                item.IssueDate == default || item.DueDate < item.IssueDate ||
                !IsPositiveMoney(item.Amount) || !IsMoneyOrZero(item.SettledAmountAtCutoff) ||
                !IsPositiveMoney(item.OutstandingAmount) ||
                item.Amount - item.SettledAmountAtCutoff != item.OutstandingAmount ||
                item.Bucket != Bucket(item.DueDate, snapshot.Cutoff.CutoffLocalDate))
            {
                throw InvalidSnapshot(
                    "A Project Financial Position open obligation is invalid.");
            }
        }

        ValidateSummary(snapshot.PayableSummary, snapshot.OpenObligations);
        ValidateSummary(snapshot.ReceivableSummary, snapshot.OpenObligations);
        ValidateAging(snapshot.Aging, snapshot.OpenObligations);
    }

    private static void ValidateSummary(
        ProjectFinancialPositionReportObligationSummary summary,
        IReadOnlyCollection<ProjectFinancialPositionReportOpenObligation> rows)
    {
        if (!Enum.IsDefined(summary.Type) || summary.OpenCount < 0 ||
            summary.OverdueCount < 0 || summary.OverdueCount > summary.OpenCount ||
            !IsMoneyOrZero(summary.OpenAmount) || !IsMoneyOrZero(summary.OverdueAmount) ||
            summary.OverdueAmount > summary.OpenAmount)
        {
            throw InvalidSnapshot("A Project Financial Position obligation summary is invalid.");
        }

        var matching = rows.Where(item => item.Type == summary.Type).ToArray();
        var overdue = matching.Where(item =>
            item.Bucket != ProjectFinancialPositionAgingBucket.NotDue).ToArray();
        if (summary.OpenCount != matching.Length ||
            summary.OpenAmount != matching.Sum(item => item.OutstandingAmount) ||
            summary.OverdueCount != overdue.Length ||
            summary.OverdueAmount != overdue.Sum(item => item.OutstandingAmount))
        {
            throw InvalidSnapshot(
                "A Project Financial Position obligation summary does not match its rows.");
        }
    }

    private static void ValidateAging(
        IReadOnlyCollection<ProjectFinancialPositionReportAgingSummary> aging,
        IReadOnlyCollection<ProjectFinancialPositionReportOpenObligation> rows)
    {
        var canonical = aging
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Bucket)
            .ToArray();
        if (!aging.SequenceEqual(canonical) ||
            aging.Select(item => (item.Type, item.Bucket)).Distinct().Count() != 8)
        {
            throw InvalidSnapshot(
                "Project Financial Position Aging rows are duplicated or non-canonical.");
        }

        foreach (var item in aging)
        {
            var matching = rows.Where(row => row.Type == item.Type && row.Bucket == item.Bucket)
                .ToArray();
            if (item is null || !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.Bucket) ||
                item.Count < 0 || !IsMoneyOrZero(item.Amount) || item.Count != matching.Length ||
                item.Amount != matching.Sum(row => row.OutstandingAmount))
            {
                throw InvalidSnapshot("A Project Financial Position Aging row is invalid.");
            }
        }
    }

    private static void ValidateBudget(ProjectFinancialPositionReportSemanticSnapshot snapshot)
    {
        if (snapshot.BudgetStatus != ProjectFinancialPositionSectionStatus.Available)
        {
            if (snapshot.Budget is not null || snapshot.BudgetComparison is not null)
            {
                throw InvalidSnapshot(
                    "Unavailable Budget data cannot expose a baseline or comparison.");
            }
            return;
        }

        var budget = snapshot.Budget;
        var comparison = snapshot.BudgetComparison;
        if (budget is null || comparison is null || budget.BaselineId == Guid.Empty ||
            budget.Revision <= 0 || !IsPositiveMoney(budget.Amount) ||
            !IsCurrency(budget.CurrencyCode) ||
            !string.Equals(
                budget.CurrencyCode,
                snapshot.Project.CapturedBaseCurrencyCode,
                StringComparison.Ordinal) ||
            budget.ApprovedAt == default || budget.ApprovedAt.Offset != TimeSpan.Zero ||
            budget.ApprovedAt > snapshot.Cutoff.SourceCutoffUtc ||
            budget.SupersededAt.HasValue &&
                (budget.SupersededAt.Value.Offset != TimeSpan.Zero ||
                    budget.SupersededAt.Value <= snapshot.Cutoff.SourceCutoffUtc) ||
            comparison.ApprovedBudgetAmount != budget.Amount)
        {
            throw InvalidSnapshot(
                "Project Financial Position Budget identity or comparison is invalid.");
        }

        var valuesPresent = comparison.BudgetRemainingAmount.HasValue &&
            comparison.BudgetConsumedPercent.HasValue;
        if ((snapshot.BudgetComparisonStatus == ProjectFinancialPositionSectionStatus.Available &&
                !valuesPresent) ||
            (snapshot.BudgetComparisonStatus != ProjectFinancialPositionSectionStatus.Available &&
                valuesPresent) ||
            comparison.BudgetRemainingAmount.HasValue &&
                !IsMoney(comparison.BudgetRemainingAmount.Value) ||
            comparison.BudgetConsumedPercent.HasValue &&
                (comparison.BudgetConsumedPercent.Value < 0 ||
                    decimal.Round(
                        comparison.BudgetConsumedPercent.Value,
                        1,
                        MidpointRounding.AwayFromZero) !=
                    comparison.BudgetConsumedPercent.Value))
        {
            throw InvalidSnapshot(
                "Project Financial Position Budget comparison status is invalid.");
        }

        if (snapshot.BudgetComparisonStatus == ProjectFinancialPositionSectionStatus.Available)
        {
            var recognizedSpend = snapshot.Cash.RecognizedSpend;
            var expectedPercent = recognizedSpend.HasValue
                ? decimal.Round(
                    recognizedSpend.Value * 100m / budget.Amount,
                    1,
                    MidpointRounding.AwayFromZero)
                : (decimal?)null;
            if (!recognizedSpend.HasValue ||
                comparison.BudgetRemainingAmount != budget.Amount - recognizedSpend.Value ||
                comparison.BudgetConsumedPercent != expectedPercent)
            {
                throw InvalidSnapshot(
                    "Project Financial Position Budget comparison formulas are invalid.");
            }
        }
    }

    private static void ValidateStatus(ProjectFinancialPositionReportSemanticSnapshot snapshot)
    {
        var valid = snapshot.DataStatus switch
        {
            ReportDataStatus.NotConfigured =>
                snapshot.CashStatus == ProjectFinancialPositionSectionStatus.NotConfigured &&
                snapshot.ObligationStatus == ProjectFinancialPositionSectionStatus.NotConfigured,
            ReportDataStatus.NoData =>
                snapshot.CashStatus == ProjectFinancialPositionSectionStatus.NoData &&
                snapshot.ObligationStatus == ProjectFinancialPositionSectionStatus.NoData,
            ReportDataStatus.InsufficientData =>
                snapshot.CashStatus == ProjectFinancialPositionSectionStatus.InsufficientData ||
                snapshot.ObligationStatus == ProjectFinancialPositionSectionStatus.InsufficientData,
            ReportDataStatus.Available =>
                snapshot.CashStatus == ProjectFinancialPositionSectionStatus.Available ||
                snapshot.ObligationStatus == ProjectFinancialPositionSectionStatus.Available,
            _ => false
        };
        if (!valid)
        {
            throw InvalidSnapshot(
                "Project Financial Position data status conflicts with section statuses.");
        }
    }

    private static void ValidateCounts(ProjectFinancialSourceCounts counts, int openRowCount)
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
            counts.OpenObligationCount != openRowCount ||
            counts.OpenObligationCount > counts.OfficialObligationCount)
        {
            throw InvalidSnapshot("Project Financial Position source counts are inconsistent.");
        }
    }

    private static ProjectFinancialPositionAgingBucket Bucket(
        DateOnly dueDate,
        DateOnly cutoffLocalDate)
    {
        var days = cutoffLocalDate.DayNumber - dueDate.DayNumber;
        return days <= 0
            ? ProjectFinancialPositionAgingBucket.NotDue
            : days <= 30
                ? ProjectFinancialPositionAgingBucket.Overdue1To30
                : days <= 60
                    ? ProjectFinancialPositionAgingBucket.Overdue31To60
                    : ProjectFinancialPositionAgingBucket.Overdue61Plus;
    }

    private static bool IsCurrency(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 3 &&
        value.All(char.IsAsciiLetterUpper);

    private static bool IsPositiveMoney(decimal value) => value > 0 && IsMoney(value);

    private static bool IsMoneyOrZero(decimal value) => value >= 0 && IsMoney(value);

    private static bool IsMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static ReportRenderingException InvalidSnapshot(
        string message,
        Exception? innerException = null) => new(
        "reporting.project_financial_position.snapshot.payload_invalid",
        transient: false,
        message,
        innerException);

    private static ReportRenderingException InvalidRequest(string message) => new(
        "reporting.project_financial_position.render_request.invalid",
        transient: false,
        message);
}
