using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pmcs.Api.Infrastructure;
using Pmcs.BuildingBlocks.Testing;
using Pmcs.Modules.QualityAssurance;

namespace Pmcs.Domain.Tests;

public sealed class QualityAssuranceBoundaryTests
{
    private const string StrongKey = "qa-key-0123456789abcdef-0123456789abcdef";

    [Fact]
    public void DisabledGatewayDoesNotRequireQaCredentials()
    {
        var options = QualityAssuranceRuntimeOptions.Create(
            new QaTestEnvironment("Production"),
            Configuration(new Dictionary<string, string?>()),
            "commit",
            "version",
            "built-at");

        Assert.False(options.Enabled);
        Assert.Empty(options.AuthenticationKey);
        Assert.Empty(options.DatabaseName);
    }

    [Fact]
    public void EnabledGatewayRejectsProduction()
    {
        var values = QaValues("pmcs_qa_test");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            QualityAssuranceRuntimeOptions.Create(
                new QaTestEnvironment("Production"),
                Configuration(values),
                "commit",
                "version",
                "built-at"));

        Assert.Contains("Development or QA", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pmcs")]
    [InlineData("pmcs_test")]
    [InlineData("qa_pmcs")]
    [InlineData("pmcs_qa_bad.name")]
    public void EnabledGatewayRejectsNonIsolatedDatabase(string databaseName)
    {
        var values = QaValues(databaseName);

        Assert.Throws<InvalidOperationException>(() =>
            QualityAssuranceRuntimeOptions.Create(
                new QaTestEnvironment("Development"),
                Configuration(values),
                "commit",
                "version",
                "built-at"));
    }

    [Fact]
    public void EnabledGatewayRequiresIndependentStrongKey()
    {
        var values = QaValues("pmcs_qa_test");
        values["PMCS_QA_AUTH_KEY"] = "short";

        Assert.Throws<InvalidOperationException>(() =>
            QualityAssuranceRuntimeOptions.Create(
                new QaTestEnvironment("Development"),
                Configuration(values),
                "commit",
                "version",
                "built-at"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("QA")]
    public void EnabledGatewayAcceptsOnlyIsolatedTestRuntime(string environmentName)
    {
        var options = QualityAssuranceRuntimeOptions.Create(
            new QaTestEnvironment(environmentName),
            Configuration(QaValues("pmcs_qa_test")),
            "release-commit",
            "1.0.0-qa",
            "2026-09-17T00:00:00Z");

        Assert.True(options.Enabled);
        Assert.Equal("pmcs_qa_test", options.DatabaseName);
        Assert.Equal(StrongKey, options.AuthenticationKey);
    }

    [Fact]
    public void QaAuthenticationKeyComparisonIsExact()
    {
        Assert.True(QualityAssuranceAuthenticationHandler.KeysMatch(StrongKey, StrongKey));
        Assert.False(QualityAssuranceAuthenticationHandler.KeysMatch(StrongKey, $"{StrongKey}x"));
        Assert.False(QualityAssuranceAuthenticationHandler.KeysMatch(StrongKey, null));
    }

    [Fact]
    public void DeterministicQaActorsCoverAllSupportedOperationalViews()
    {
        var roles = PmcsTestDataSet.Actors.Select(actor => actor.ProjectRole).ToHashSet(StringComparer.Ordinal);

        Assert.True(PmcsTestDataSet.QaSuperAdministrator.IsQaSuperAdministrator);
        Assert.Equal("TenantAdministrator", PmcsTestDataSet.QaSuperAdministrator.TenantRole);
        Assert.Equal(PmcsTestDataSet.Actors.Count, PmcsTestDataSet.Actors.Select(actor => actor.UserId).Distinct().Count());
        Assert.Contains("SiteSupervisor", roles);
        Assert.Contains("TechnicalOffice", roles);
        Assert.Contains("FinanceOperator", roles);
        Assert.Contains("ProcurementOperator", roles);
        Assert.Contains("ProjectManager", roles);
        Assert.Contains("ProjectController", roles);
    }

    private static Dictionary<string, string?> QaValues(string databaseName) => new()
    {
        ["PMCS_QA_GATEWAY_ENABLED"] = "true",
        ["PMCS_QA_AUTH_KEY"] = StrongKey,
        ["ConnectionStrings:Pmcs"] =
            $"Host=localhost;Database={databaseName};Username=pmcs;Password=not-used"
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class QaTestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Pmcs.Domain.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
