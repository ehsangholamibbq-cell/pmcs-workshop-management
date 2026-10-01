using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.Platform;
using Pmcs.Modules.Projects;

namespace Pmcs.Domain.Tests;

public sealed class ModuleManifestTests
{
    [Fact]
    public void ProjectsPublishesControlledBootstrapContracts()
    {
        var descriptor = new ProjectsModule().Descriptor;

        Assert.Equal(ModuleManifestSchemas.VersionOne, descriptor.SchemaVersion);
        Assert.Equal("projects.core", descriptor.ModuleId);
        Assert.Contains("identity-access.core", descriptor.Dependencies);
        Assert.Contains("projects.controlled-bootstrap", descriptor.Capabilities);
        Assert.Contains(descriptor.Permissions, item => item.Key == "projects.bootstrap.preview");
        Assert.Contains(descriptor.Permissions, item => item.Key == "projects.bootstrap.create");
        Assert.Contains(descriptor.Permissions, item => item.Key == "projects.bootstrap.members_copy");
        Assert.Contains(descriptor.Permissions, item => item.Key == "projects.bootstrap.activate");
        Assert.Contains(descriptor.NavigationItems, item =>
            item.Route == "/project-bootstraps" && item.Permission == "projects.bootstrap.preview");
        Assert.Contains(descriptor.Events, item =>
            item.Name == "projects.bootstrap.completed" && item.Version == 1);
    }

    [Fact]
    public void PlatformReferenceModulePublishesTheCompleteExtensionContract()
    {
        var catalog = ModuleCatalog.Create([new PlatformModule().Descriptor]);

        var descriptor = catalog.GetRequired("platform.foundation");
        Assert.Equal(ModuleManifestSchemas.VersionOne, descriptor.SchemaVersion);
        Assert.False(descriptor.IsLegacy);
        Assert.Contains("platform.module-catalog", descriptor.Capabilities);
        Assert.Contains(descriptor.Permissions, item => item.Key == "platform.modules.read");
        Assert.Contains(descriptor.NavigationItems, item =>
            item.Permission == "platform.modules.read" &&
            item.FeatureFlag == "platform.module-catalog");
        Assert.Contains(descriptor.Tools, item =>
            item.Id == "platform.modules.describe" &&
            item.AccessMode == ToolAccessMode.ReadOnly &&
            item.RiskClass == ManifestRiskClass.Low);
        Assert.Contains(descriptor.Events, item =>
            item.Name == "platform.module-catalog.snapshot" && item.Version == 1);
    }

    [Fact]
    public void UnknownManifestSchemaFailsClosed()
    {
        var descriptor = ValidDescriptor("test.alpha") with { SchemaVersion = "pmcs.module/v99" };

        var exception = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([descriptor]));

        Assert.Contains("unsupported manifest schema", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateModuleIdentifiersFailStartupValidation()
    {
        var descriptor = ValidDescriptor("test.alpha");

        Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([descriptor, descriptor]));
    }

    [Fact]
    public void MissingDependencyFailsStartupValidation()
    {
        var descriptor = ValidDescriptor("test.alpha") with { Dependencies = ["test.missing"] };

        var exception = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([descriptor]));

        Assert.Contains("depends on unknown module", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyCycleFailsStartupValidation()
    {
        var alpha = ValidDescriptor("test.alpha") with { Dependencies = ["test.beta"] };
        var beta = ValidDescriptor("test.beta") with { Dependencies = ["test.alpha"] };

        var exception = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([alpha, beta]));

        Assert.Contains("dependency cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NavigationAndToolsCannotReferenceUndeclaredPermissions()
    {
        var descriptor = ValidDescriptor("test.alpha") with
        {
            NavigationItems =
            [
                new NavigationManifest(
                    "test.alpha.navigation",
                    "/test/alpha",
                    "آلفا",
                    "test.missing.read",
                    "test.alpha",
                    100)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([descriptor]));

        Assert.Contains("undeclared permission", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PermissionSegmentsMayUseTheCataloguedUnderscoreConvention()
    {
        var descriptor = ValidDescriptor("test.alpha") with
        {
            Permissions =
            [
                new PermissionManifest(
                    "test.alpha.members_copy",
                    PermissionScope.Project,
                    ManifestRiskClass.High,
                    "Copy selected memberships.")
            ]
        };

        var catalog = ModuleCatalog.Create([descriptor]);

        Assert.Contains(
            catalog.GetRequired("test.alpha").Permissions,
            item => item.Key == "test.alpha.members_copy");
    }

    [Fact]
    public void AgentToolSchemasMustBeJsonObjects()
    {
        var descriptor = ValidDescriptor("test.alpha") with
        {
            Tools =
            [
                new ToolManifest(
                    "test.alpha.describe",
                    "Describe alpha.",
                    "[]",
                    "{}",
                    "test.alpha.read",
                    ManifestRiskClass.Low,
                    ToolAccessMode.ReadOnly)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([descriptor]));

        Assert.Contains("must be a JSON object", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FuturePlatformRequirementFailsCompatibilityValidation()
    {
        var descriptor = ValidDescriptor("test.alpha") with { MinimumPlatformVersion = "99.0.0" };

        var exception = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([descriptor]));

        Assert.Contains("requires platform", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyAdapterCannotPublishUnvalidatedExtensionContracts()
    {
        var descriptor = ModuleDescriptor.Legacy("Alpha") with { Capabilities = ["test.alpha"] };

        var exception = Assert.Throws<InvalidOperationException>(() => ModuleCatalog.Create([descriptor]));

        Assert.Contains("cannot publish unvalidated", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownModuleLookupFailsClosed()
    {
        var catalog = ModuleCatalog.Create([ValidDescriptor("test.alpha")]);

        Assert.False(catalog.TryGet("test.missing", out var descriptor));
        Assert.Null(descriptor);
        Assert.Throws<KeyNotFoundException>(() => catalog.GetRequired("test.missing"));
    }

    private static ModuleDescriptor ValidDescriptor(string moduleId)
    {
        var suffix = moduleId.Replace('.', '-');
        return new ModuleDescriptor(
            ModuleManifestSchemas.VersionOne,
            moduleId,
            moduleId,
            "1.1.0",
            [$"{moduleId}.capability"],
            [],
            suffix,
            "1.1.0",
            false,
            [
                new PermissionManifest(
                    $"{moduleId}.read",
                    PermissionScope.Tenant,
                    ManifestRiskClass.Low,
                    "Read the test module.")
            ],
            [],
            [],
            []);
    }
}
