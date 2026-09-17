using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pmcs.BuildingBlocks.Modules;

public static class ModuleManifestSchemas
{
    public const string VersionOne = "pmcs.module/v1";
    public const string LegacyVersionOne = "pmcs.module/legacy-v1";
}

public enum PermissionScope
{
    Tenant,
    Project
}

public enum ManifestRiskClass
{
    Low,
    Medium,
    High,
    Critical
}

public enum ToolAccessMode
{
    ReadOnly,
    ControlledWrite
}

public enum IntegrationEventClassification
{
    Internal,
    Confidential
}

public sealed record PermissionManifest(
    string Key,
    PermissionScope Scope,
    ManifestRiskClass RiskClass,
    string Description);

public sealed record NavigationManifest(
    string Id,
    string Route,
    string Label,
    string Permission,
    string FeatureFlag,
    int Order);

public sealed record ToolManifest(
    string Id,
    string Description,
    string InputSchema,
    string OutputSchema,
    string Permission,
    ManifestRiskClass RiskClass,
    ToolAccessMode AccessMode);

public sealed record IntegrationEventManifest(
    string Name,
    int Version,
    IntegrationEventClassification Classification);

public sealed record ModuleDescriptor(
    string SchemaVersion,
    string ModuleId,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Dependencies,
    string MigrationOwner,
    string MinimumPlatformVersion,
    bool IsLegacy,
    IReadOnlyList<PermissionManifest> Permissions,
    IReadOnlyList<NavigationManifest> NavigationItems,
    IReadOnlyList<ToolManifest> Tools,
    IReadOnlyList<IntegrationEventManifest> Events)
{
    public static ModuleDescriptor Legacy(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = new string(name
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("A legacy module name must contain at least one letter or digit.", nameof(name));
        }

        return new ModuleDescriptor(
            ModuleManifestSchemas.LegacyVersionOne,
            $"legacy.{normalizedName}",
            name.Trim(),
            "1.0.0",
            [],
            [],
            normalizedName,
            "1.0.0",
            true,
            [],
            [],
            [],
            []);
    }
}

public interface IModuleCatalog
{
    IReadOnlyList<ModuleDescriptor> Modules { get; }

    bool TryGet(string moduleId, out ModuleDescriptor? descriptor);

    ModuleDescriptor GetRequired(string moduleId);
}

public sealed partial class ModuleCatalog : IModuleCatalog
{
    public const string PlatformVersion = "1.1.0";

    private readonly Dictionary<string, ModuleDescriptor> modulesById;

    private ModuleCatalog(IReadOnlyList<ModuleDescriptor> modules)
    {
        Modules = modules;
        modulesById = modules.ToDictionary(item => item.ModuleId, StringComparer.Ordinal);
    }

    public IReadOnlyList<ModuleDescriptor> Modules { get; }

    public static ModuleCatalog Create(IEnumerable<ModuleDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        var modules = descriptors.OrderBy(item => item.ModuleId, StringComparer.Ordinal).ToArray();
        if (modules.Length == 0)
        {
            throw new InvalidOperationException("At least one module descriptor is required.");
        }

        ValidateUnique(modules.Select(item => item.ModuleId), "module id");
        foreach (var module in modules)
        {
            ValidateModule(module);
        }

        var moduleIds = modules.Select(item => item.ModuleId).ToHashSet(StringComparer.Ordinal);
        var permissions = modules.SelectMany(item => item.Permissions).Select(item => item.Key).ToArray();
        ValidateUnique(permissions, "permission key");
        ValidateUnique(modules.SelectMany(item => item.Capabilities), "capability");
        ValidateUnique(modules.SelectMany(item => item.NavigationItems).Select(item => item.Id), "navigation id");
        ValidateUnique(modules.SelectMany(item => item.Tools).Select(item => item.Id), "tool id");
        ValidateUnique(
            modules.SelectMany(item => item.Events).Select(item => $"{item.Name}:v{item.Version}"),
            "event contract");
        ValidateUnique(modules.Select(item => item.MigrationOwner), "migration owner");

        var permissionSet = permissions.ToHashSet(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            foreach (var dependency in module.Dependencies)
            {
                if (!moduleIds.Contains(dependency))
                {
                    throw new InvalidOperationException(
                        $"Module '{module.ModuleId}' depends on unknown module '{dependency}'.");
                }
            }

            foreach (var navigation in module.NavigationItems)
            {
                if (!permissionSet.Contains(navigation.Permission))
                {
                    throw new InvalidOperationException(
                        $"Navigation '{navigation.Id}' references undeclared permission '{navigation.Permission}'.");
                }
            }

            foreach (var tool in module.Tools)
            {
                if (!permissionSet.Contains(tool.Permission))
                {
                    throw new InvalidOperationException(
                        $"Tool '{tool.Id}' references undeclared permission '{tool.Permission}'.");
                }
            }
        }

        ValidateDependencyGraph(modules);
        return new ModuleCatalog(modules);
    }

    public bool TryGet(string moduleId, out ModuleDescriptor? descriptor)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            descriptor = null;
            return false;
        }

        return modulesById.TryGetValue(moduleId, out descriptor);
    }

    public ModuleDescriptor GetRequired(string moduleId) =>
        TryGet(moduleId, out var descriptor)
            ? descriptor!
            : throw new KeyNotFoundException($"Module '{moduleId}' is not registered.");

    private static void ValidateModule(ModuleDescriptor module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (module.SchemaVersion is not ModuleManifestSchemas.VersionOne and
            not ModuleManifestSchemas.LegacyVersionOne)
        {
            throw new InvalidOperationException(
                $"Module '{module.ModuleId}' uses unsupported manifest schema '{module.SchemaVersion}'.");
        }

        ValidateIdentifier(module.ModuleId, "module id");
        ValidateText(module.DisplayName, "display name", 160);
        ValidateSemanticVersion(module.Version, "module version");
        ValidateIdentifier(module.MigrationOwner, "migration owner", allowSingleSegment: true);
        ValidateSemanticVersion(module.MinimumPlatformVersion, "minimum platform version");
        if (CompareVersions(module.MinimumPlatformVersion, PlatformVersion) > 0)
        {
            throw new InvalidOperationException(
                $"Module '{module.ModuleId}' requires platform {module.MinimumPlatformVersion}, " +
                $"but this runtime provides {PlatformVersion}.");
        }

        var isLegacySchema = module.SchemaVersion == ModuleManifestSchemas.LegacyVersionOne;
        if (module.IsLegacy != isLegacySchema)
        {
            throw new InvalidOperationException(
                $"Module '{module.ModuleId}' has inconsistent legacy metadata.");
        }

        if (isLegacySchema)
        {
            if (module.Capabilities.Count > 0 || module.Dependencies.Count > 0 ||
                module.Permissions.Count > 0 || module.NavigationItems.Count > 0 ||
                module.Tools.Count > 0 || module.Events.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Legacy module '{module.ModuleId}' cannot publish unvalidated extension contracts.");
            }

            return;
        }

        foreach (var capability in module.Capabilities)
        {
            ValidateIdentifier(capability, "capability");
        }

        foreach (var dependency in module.Dependencies)
        {
            ValidateIdentifier(dependency, "module dependency");
            if (string.Equals(dependency, module.ModuleId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Module '{module.ModuleId}' cannot depend on itself.");
            }
        }

        ValidateUnique(module.Capabilities, $"capability in module '{module.ModuleId}'");
        ValidateUnique(module.Dependencies, $"dependency in module '{module.ModuleId}'");
        ValidateUnique(module.Permissions.Select(item => item.Key), $"permission in module '{module.ModuleId}'");
        ValidateUnique(module.NavigationItems.Select(item => item.Id), $"navigation in module '{module.ModuleId}'");
        ValidateUnique(module.Tools.Select(item => item.Id), $"tool in module '{module.ModuleId}'");
        ValidateUnique(
            module.Events.Select(item => $"{item.Name}:v{item.Version}"),
            $"event in module '{module.ModuleId}'");

        foreach (var permission in module.Permissions)
        {
            ValidateIdentifier(permission.Key, "permission key");
            ValidateText(permission.Description, "permission description", 300);
        }

        foreach (var navigation in module.NavigationItems)
        {
            ValidateIdentifier(navigation.Id, "navigation id");
            ValidateSafeRoute(navigation.Route);
            ValidateText(navigation.Label, "navigation label", 120);
            ValidateIdentifier(navigation.Permission, "navigation permission");
            ValidateIdentifier(navigation.FeatureFlag, "feature flag");
            if (navigation.Order is < 0 or > 100_000)
            {
                throw new InvalidOperationException(
                    $"Navigation '{navigation.Id}' has an invalid order.");
            }
        }

        foreach (var tool in module.Tools)
        {
            ValidateIdentifier(tool.Id, "tool id");
            ValidateText(tool.Description, "tool description", 500);
            ValidateIdentifier(tool.Permission, "tool permission");
            ValidateJsonObject(tool.InputSchema, $"input schema for tool '{tool.Id}'");
            ValidateJsonObject(tool.OutputSchema, $"output schema for tool '{tool.Id}'");
            if (tool.AccessMode == ToolAccessMode.ControlledWrite && tool.RiskClass == ManifestRiskClass.Low)
            {
                throw new InvalidOperationException(
                    $"Controlled-write tool '{tool.Id}' cannot be classified as low risk.");
            }
        }

        foreach (var integrationEvent in module.Events)
        {
            ValidateIdentifier(integrationEvent.Name, "event name");
            if (integrationEvent.Version <= 0)
            {
                throw new InvalidOperationException(
                    $"Event '{integrationEvent.Name}' must declare a positive version.");
            }
        }
    }

    private static void ValidateDependencyGraph(IReadOnlyCollection<ModuleDescriptor> modules)
    {
        var dependencies = modules.ToDictionary(
            item => item.ModuleId,
            item => item.Dependencies,
            StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var moduleId in dependencies.Keys)
        {
            Visit(moduleId);
        }

        return;

        void Visit(string moduleId)
        {
            if (visited.Contains(moduleId))
            {
                return;
            }

            if (!visiting.Add(moduleId))
            {
                throw new InvalidOperationException($"Module dependency cycle detected at '{moduleId}'.");
            }

            foreach (var dependency in dependencies[moduleId])
            {
                Visit(dependency);
            }

            visiting.Remove(moduleId);
            visited.Add(moduleId);
        }
    }

    private static void ValidateUnique(IEnumerable<string> values, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!seen.Add(value))
            {
                throw new InvalidOperationException($"Duplicate {kind} '{value}'.");
            }
        }
    }

    private static void ValidateIdentifier(string value, string kind, bool allowSingleSegment = false)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160 ||
            !(allowSingleSegment ? SingleSegmentIdentifierRegex() : IdentifierRegex()).IsMatch(value))
        {
            throw new InvalidOperationException($"Invalid {kind} '{value}'.");
        }
    }

    private static void ValidateSemanticVersion(string value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || !SemanticVersionRegex().IsMatch(value))
        {
            throw new InvalidOperationException($"Invalid {kind} '{value}'.");
        }
    }

    private static int CompareVersions(string left, string right)
    {
        var leftVersion = Version.Parse(left.Split('-', 2)[0]);
        var rightVersion = Version.Parse(right.Split('-', 2)[0]);
        return leftVersion.CompareTo(rightVersion);
    }

    private static void ValidateText(string value, string kind, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw new InvalidOperationException($"Invalid {kind}.");
        }
    }

    private static void ValidateSafeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route) || route.Length > 240 || !route.StartsWith('/') ||
            route.StartsWith("//", StringComparison.Ordinal) || route.Contains("..", StringComparison.Ordinal) ||
            route.Contains('\\') || route.Contains("://", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsafe navigation route '{route}'.");
        }
    }

    private static void ValidateJsonObject(string value, string kind)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"The {kind} must be a JSON object.");
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The {kind} is not valid JSON.", exception);
        }
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SingleSegmentIdentifierRegex();

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();
}
