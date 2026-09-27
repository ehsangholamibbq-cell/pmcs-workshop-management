using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class PortfolioSummaryReportingTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("a1000000-0000-4000-8000-000000000001");

    [Fact]
    public void PinnedCohortRejectsDuplicateAndUnsortedIds()
    {
        var first = new PortfolioPinnedProject(Guid.Parse("a2000000-0000-4000-8000-000000000001"),
            true, false, "policy-v1");
        var second = first with { ProjectId = Guid.Parse("a2000000-0000-4000-8000-000000000002") };
        new PortfolioPinnedCohort(PortfolioPinnedCohort.Version, TenantId,
            Guid.NewGuid(), Cutoff, [first, second]).Validate();
        Assert.Equal("reporting.portfolio.cohort.invalid",
            Assert.Throws<DomainRuleException>(() => new PortfolioPinnedCohort(
                PortfolioPinnedCohort.Version, TenantId, Guid.NewGuid(), Cutoff,
                [first, first]).Validate()).Code);
        Assert.Equal("reporting.portfolio.cohort.invalid",
            Assert.Throws<DomainRuleException>(() => new PortfolioPinnedCohort(
                PortfolioPinnedCohort.Version, TenantId, Guid.NewGuid(), Cutoff,
                [second, first]).Validate()).Code);
    }

    [Fact]
    public void EmptyAuthorizedCohortDoesNotExposeTenantProjectCount()
    {
        var snapshot = Build([]);
        var payload = Parse(snapshot);
        Assert.Null(snapshot.ProjectId);
        Assert.Equal(ReportDataStatus.NoData, snapshot.DataStatus);
        Assert.Equal(0, payload.AuthorizedProjectCount);
        Assert.Empty(payload.Projects);
        Assert.Empty(payload.CurrencyGroups);
    }

    [Fact]
    public void CurrenciesRemainSeparateAndOnlyAuthorizedDimensionsContribute()
    {
        var irrA = Project(Guid.Parse("a2000000-0000-4000-8000-000000000001"), "IRR", 10);
        var irrB = Project(Guid.Parse("a2000000-0000-4000-8000-000000000002"), "IRR", 20);
        var usd = Project(Guid.Parse("a2000000-0000-4000-8000-000000000003"), "USD", 30);
        var snapshot = Build([usd, irrB, irrA]);
        var payload = Parse(snapshot);

        Assert.Equal(new[] { irrA.ProjectId, irrB.ProjectId, usd.ProjectId },
            payload.Projects.Select(item => item.ProjectId));
        Assert.Equal(2, payload.CurrencyGroups.Count);
        Assert.Equal("IRR", payload.CurrencyGroups.First().CurrencyCode);
        Assert.Equal("USD", payload.CurrencyGroups.Last().CurrencyCode);
        Assert.Equal(30m, payload.CurrencyGroups.Single(item => item.CurrencyCode == "IRR").RecognizedSpendSubtotal);
        Assert.Equal(30m, payload.CurrencyGroups.Single(item => item.CurrencyCode == "USD").RecognizedSpendSubtotal);
        Assert.All(payload.CurrencyGroups, item => Assert.Null(item.TotalCommittedSubtotal));
        Assert.All(payload.Projects, item => Assert.Equal(
            PortfolioDimensionStatus.NotAuthorized, item.Commercial.Status));
        Assert.Equal(ReportDataStatus.Available, snapshot.DataStatus);
        Assert.Equal(snapshot.SourceManifestSha256, payload.SourceManifestSha256);
    }

    [Fact]
    public void OverflowDuplicateAndCurrencyMismatchFailClosed()
    {
        var item = Project(Guid.NewGuid(), "IRR", 10);
        Assert.Equal("reporting.portfolio.cohort.invalid",
            Assert.Throws<DomainRuleException>(() => Build(Enumerable.Range(0, 201)
                .Select(_ => Project(Guid.NewGuid(), "IRR", 1)).ToArray())).Code);
        Assert.Equal("reporting.portfolio.cohort.duplicate",
            Assert.Throws<DomainRuleException>(() => Build([item, item])).Code);
        Assert.Equal("reporting.portfolio.dimension.invalid",
            Assert.Throws<DomainRuleException>(() => Build([
                item with { Financial = item.Financial with { CurrencyCode = "USD" } }
            ])).Code);
    }

    [Fact]
    public void UnprovenProfileSuppressesIdentityAndAmounts()
    {
        var item = Project(Guid.NewGuid(), "IRR", 10);
        var gap = item with {
            Code = null, Name = null, BaseCurrencyCode = null, ConfigurationVersion = null,
            ConfigurationChangedAtUtc = null,
            ConfigurationProvenAtCutoff = false,
            OperationalStatus = PortfolioDimensionStatus.InsufficientData,
            OperationalAssessment = null, Coverage = null, Freshness = null,
            Confidence = null, IsPartial = null, OperationalSnapshotId = null,
            OperationalWatermarkUtc = null,
            OperationalSourceSha256 = null,
            Financial = new PortfolioFinancialDimension(PortfolioDimensionStatus.InsufficientData,
                null, null, null, null, "project.configuration_history_unavailable",
                ReportClassification.Confidential)
        };
        var snapshot = Build([gap]);
        Assert.Equal(ReportDataStatus.InsufficientData, snapshot.DataStatus);
        Assert.Null(Parse(snapshot).Projects.Single().Name);
        Assert.Equal("reporting.portfolio.project.historical_configuration_invalid",
            Assert.Throws<DomainRuleException>(() => Build([gap with { Name = "current" }])).Code);
    }

    [Fact]
    public void ManifestPinsSourceVersionsPermissionMaskAndCutoffProof()
    {
        var item = Project(Guid.NewGuid(), "IRR", 10);
        var snapshot = Build([item]);
        var manifest = JsonSerializer.Deserialize<PortfolioSummarySourceManifest>(
            snapshot.SourceManifestJson, CanonicalJson.SerializerOptions)!;
        var entry = Assert.Single(manifest.Projects);
        Assert.Equal(item.ProjectId, entry.ProjectId);
        Assert.Equal(item.ConfigurationChangedAtUtc, entry.ConfigurationChangedAtUtc);
        Assert.Equal(item.OperationalWatermarkUtc, entry.OperationalWatermarkUtc);
        Assert.True(entry.FinancialAuthorized);
        Assert.False(entry.CommercialAuthorized);
        Assert.Equal("pmcs.project-intelligence.project-state-reporting/v1",
            manifest.ProjectStateSourceContractVersion);
        Assert.Equal("reporting.portfolio.project.invalid",
            Assert.Throws<DomainRuleException>(() => Build([
                item with { OperationalWatermarkUtc = Cutoff.AddSeconds(1) }
            ])).Code);
    }

    private static ReportSnapshot Build(IReadOnlyCollection<PortfolioProjectSelection> items) =>
        PortfolioSummaryReportSnapshotBuilder.Build(Guid.NewGuid(),
            new PortfolioSummarySelection(TenantId, Guid.NewGuid(), Cutoff, items), Cutoff.AddSeconds(1));

    private static PortfolioSummarySemanticSnapshot Parse(ReportSnapshot snapshot) =>
        JsonSerializer.Deserialize<PortfolioSummarySemanticSnapshot>(
            snapshot.PayloadJson, CanonicalJson.SerializerOptions)!;

    private static PortfolioProjectSelection Project(Guid id, string currency, decimal spend) => new(
        id, id.ToString("N"), "Project", ProjectStatus.Active,
        new DateOnly(2026, 9, 27), "UTC", currency, 1,
        Cutoff.AddDays(-1), true, "policy-v1",
        PortfolioDimensionStatus.Available, ProjectOperationalStatus.Stable,
        DataCoverageStatus.Sufficient, DataFreshnessStatus.Current,
        DataConfidenceStatus.Adequate, false, Guid.NewGuid(), Cutoff.AddHours(-1),
        new string('a', 64), null,
        new PortfolioFinancialDimension(PortfolioDimensionStatus.Available, currency,
            spend, spend - 1, new string('b', 64), null, ReportClassification.Confidential),
        new PortfolioCommercialDimension(PortfolioDimensionStatus.NotAuthorized,
            null, null, null, null, null, ReportClassification.Internal),
        ReportClassification.Confidential);
}
