using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class PortfolioSummaryReportSnapshotBuilder
{
    public static ReportSnapshot Build(
        Guid runId, PortfolioSummarySelection selection, DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (runId == Guid.Empty || selection.TenantId == Guid.Empty ||
            selection.RequestedBy == Guid.Empty || selection.SourceCutoffUtc == default ||
            selection.Projects is null ||
            selection.Projects.Count > PortfolioSummaryReportRuntimeContract.MaximumProjects)
        {
            throw Invalid("cohort.invalid");
        }

        var cutoff = selection.SourceCutoffUtc.ToUniversalTime();
        var projects = selection.Projects.OrderBy(item => item.ProjectId).ToArray();
        if (projects.Any(item => item.ProjectId == Guid.Empty) ||
            projects.Select(item => item.ProjectId).Distinct().Count() != projects.Length)
        {
            throw Invalid("cohort.duplicate");
        }
        foreach (var item in projects)
        {
            ValidateProject(item);
        }

        var manifest = new PortfolioSummarySourceManifest(
            PortfolioSummaryReportRuntimeContract.SourceManifestVersion,
            selection.TenantId,
            cutoff,
            projects.Select(item => new PortfolioProjectSourceManifest(
                item.ProjectId,
                item.CutoffLocalDate,
                item.PermissionPolicyVersion,
                item.OperationalSourceSha256,
                item.Financial.SourceManifestSha256,
                item.Commercial.SourceManifestSha256)).ToArray());
        var manifestJson = CanonicalJson.Serialize(manifest);
        var manifestHash = CanonicalJson.Sha256(manifestJson);
        var status = ResolveStatus(projects);
        var payload = new PortfolioSummarySemanticSnapshot(
            PortfolioSummaryReportRuntimeContract.SnapshotSchemaVersion,
            PortfolioSummaryReportRuntimeContract.DefinitionCode,
            cutoff,
            selection.TenantId,
            projects.Length,
            status,
            projects,
            CurrencyGroups(projects),
            manifestHash);
        var classification = projects.Aggregate(
            ReportClassification.Confidential,
            (current, item) => (ReportClassification)Math.Max((int)current, (int)item.Classification));
        return ReportSnapshot.CreatePortfolio(
            Guid.NewGuid(), runId, selection.TenantId,
            PortfolioSummaryReportRuntimeContract.SnapshotSchemaVersion,
            status, CanonicalJson.Serialize(payload), manifestJson, classification,
            builtAt, cutoff);
    }

    private static void ValidateProject(PortfolioProjectSelection item)
    {
        if (!Enum.IsDefined(item.Lifecycle) || !Enum.IsDefined(item.OperationalStatus) ||
            item.OperationalStatus == PortfolioDimensionStatus.NotAuthorized ||
            !Enum.IsDefined(item.Classification) ||
            item.Classification < ReportClassification.Confidential ||
            string.IsNullOrWhiteSpace(item.PermissionPolicyVersion) ||
            string.IsNullOrWhiteSpace(item.TimeZone) ||
            item.Financial is null || item.Commercial is null ||
            !Enum.IsDefined(item.Financial.Status) || !Enum.IsDefined(item.Commercial.Status))
        {
            throw Invalid("project.invalid");
        }

        if (!item.ConfigurationProvenAtCutoff &&
            (item.Code is not null || item.Name is not null || item.BaseCurrencyCode is not null ||
             item.ConfigurationVersion.HasValue ||
             item.OperationalStatus != PortfolioDimensionStatus.InsufficientData ||
             item.Financial.Status is not (PortfolioDimensionStatus.NotAuthorized or PortfolioDimensionStatus.InsufficientData) ||
             item.Commercial.Status is not (PortfolioDimensionStatus.NotAuthorized or PortfolioDimensionStatus.InsufficientData)))
        {
            throw Invalid("project.historical_configuration_invalid");
        }
        if (item.ConfigurationProvenAtCutoff &&
            (string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name) ||
             !Currency(item.BaseCurrencyCode) || item.ConfigurationVersion is null or <= 0))
        {
            throw Invalid("project.configuration_invalid");
        }
        if (item.OperationalStatus == PortfolioDimensionStatus.Available)
        {
            if (!item.OperationalSnapshotId.HasValue ||
                !item.OperationalAssessment.HasValue || !item.Coverage.HasValue ||
                !item.Freshness.HasValue || !item.Confidence.HasValue ||
                !item.IsPartial.HasValue || !Hash(item.OperationalSourceSha256))
            {
                throw Invalid("operational.proof_invalid");
            }
        }
        else if (item.OperationalSnapshotId.HasValue || item.OperationalAssessment.HasValue ||
                 item.Coverage.HasValue || item.Freshness.HasValue ||
                 item.Confidence.HasValue || item.IsPartial.HasValue)
        {
            throw Invalid("operational.not_available");
        }
        ValidateDimension(item.Financial.Status, item.Financial.CurrencyCode,
            item.Financial.RecognizedSpend, item.Financial.ExternalNetCash,
            item.Financial.SourceManifestSha256, item.Financial.ReasonCode,
            item.Financial.Classification, item.BaseCurrencyCode);
        ValidateDimension(item.Commercial.Status, item.Commercial.CurrencyCode,
            item.Commercial.TotalCommittedAmount, item.Commercial.OpenCommitmentAmount,
            item.Commercial.SourceManifestSha256, item.Commercial.ReasonCode,
            item.Commercial.Classification, item.BaseCurrencyCode);
        var maxClassification = new[] {
            ReportClassification.Confidential,
            item.Financial.Status == PortfolioDimensionStatus.NotAuthorized
                ? ReportClassification.Internal : item.Financial.Classification,
            item.Commercial.Status == PortfolioDimensionStatus.NotAuthorized
                ? ReportClassification.Internal : item.Commercial.Classification
        }.Max();
        if (item.Classification < maxClassification)
        {
            throw Invalid("classification.invalid");
        }
    }

    private static void ValidateDimension(
        PortfolioDimensionStatus status, string? currency, decimal? first, decimal? second,
        string? hash, string? reason, ReportClassification classification, string? baseCurrency)
    {
        if (!Enum.IsDefined(classification) ||
            (status == PortfolioDimensionStatus.NotAuthorized &&
                (currency is not null || first.HasValue || second.HasValue || hash is not null ||
                 reason is not null || classification != ReportClassification.Internal)) ||
            (status == PortfolioDimensionStatus.Available &&
                (!Currency(currency) || !first.HasValue || !second.HasValue ||
                 !Hash(hash) || classification < ReportClassification.Confidential)) ||
            (status != PortfolioDimensionStatus.Available && (first.HasValue || second.HasValue)) ||
            (currency is not null && (!Currency(currency) ||
                !string.Equals(currency, baseCurrency, StringComparison.Ordinal))) ||
            (hash is not null && !Hash(hash)))
        {
            throw Invalid("dimension.invalid");
        }
    }

    private static ReportDataStatus ResolveStatus(PortfolioProjectSelection[] projects)
    {
        if (projects.Length == 0) return ReportDataStatus.NoData;
        if (projects.Any(item => !item.ConfigurationProvenAtCutoff ||
            item.OperationalStatus == PortfolioDimensionStatus.InsufficientData ||
            item.Financial.Status == PortfolioDimensionStatus.InsufficientData ||
            item.Commercial.Status == PortfolioDimensionStatus.InsufficientData))
        {
            return ReportDataStatus.InsufficientData;
        }
        return projects.Any(item => item.OperationalStatus == PortfolioDimensionStatus.Available ||
            item.Financial.Status == PortfolioDimensionStatus.Available ||
            item.Commercial.Status == PortfolioDimensionStatus.Available)
            ? ReportDataStatus.Available
            : ReportDataStatus.NoData;
    }

    private static PortfolioCurrencyGroup[] CurrencyGroups(
        PortfolioProjectSelection[] projects)
    {
        var codes = projects.SelectMany(item => new[] {
                item.Financial.CurrencyCode, item.Commercial.CurrencyCode
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal);
        return codes.Select(code => {
            var finance = projects.Select(item => item.Financial)
                .Where(item => item.CurrencyCode == code).ToArray();
            var commercial = projects.Select(item => item.Commercial)
                .Where(item => item.CurrencyCode == code).ToArray();
            var validFinance = finance.Where(item => item.Status == PortfolioDimensionStatus.Available).ToArray();
            var validCommercial = commercial.Where(item => item.Status == PortfolioDimensionStatus.Available).ToArray();
            var financeIncomplete = finance.Any(item => item.Status == PortfolioDimensionStatus.InsufficientData);
            var commercialIncomplete = commercial.Any(item => item.Status == PortfolioDimensionStatus.InsufficientData);
            return new PortfolioCurrencyGroup(code,
                validFinance.Length,
                validFinance.Length == 0 || financeIncomplete ? null : validFinance.Sum(item => item.RecognizedSpend),
                validFinance.Length == 0 || financeIncomplete ? null : validFinance.Sum(item => item.ExternalNetCash),
                validCommercial.Length,
                validCommercial.Length == 0 || commercialIncomplete ? null : validCommercial.Sum(item => item.TotalCommittedAmount),
                validCommercial.Length == 0 || commercialIncomplete ? null : validCommercial.Sum(item => item.OpenCommitmentAmount),
                financeIncomplete, commercialIncomplete);
        }).ToArray();
    }

    private static bool Currency(string? value) =>
        value is { Length: 3 } && value.All(char.IsAsciiLetterUpper);

    private static bool Hash(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static DomainRuleException Invalid(string suffix) => new(
        $"reporting.portfolio.{suffix}", "Portfolio source or snapshot violates its certified contract.");
}
