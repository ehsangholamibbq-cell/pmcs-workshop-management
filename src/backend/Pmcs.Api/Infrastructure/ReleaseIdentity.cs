using System.Reflection;
using System.Text.RegularExpressions;

namespace Pmcs.Api.Infrastructure;

internal sealed record ReleaseIdentity(
    int SchemaVersion,
    string Artifact,
    string Commit,
    string Version,
    string BuiltAt)
{
    private static readonly Regex CommitPattern = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);
    private static readonly Regex VersionPattern = new(
        "^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$",
        RegexOptions.CultureInvariant);

    public static ReleaseIdentity FromAssembly(Assembly assembly)
    {
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value ?? string.Empty, StringComparer.Ordinal);

        return new ReleaseIdentity(
            1,
            "api",
            Read(metadata, "PMCS.ReleaseCommit", "development"),
            Read(metadata, "PMCS.ReleaseVersion", "0.0.0-dev"),
            Read(metadata, "PMCS.ReleaseBuiltAt", "unknown"));
    }

    public void ValidateForProduction()
    {
        if (!CommitPattern.IsMatch(Commit))
        {
            throw new InvalidOperationException(
                "The API artifact must embed a full lowercase 40-character PMCS release commit outside Development.");
        }

        if (!VersionPattern.IsMatch(Version))
        {
            throw new InvalidOperationException("The API artifact must embed a valid PMCS semantic release version.");
        }

        if (!DateTimeOffset.TryParse(BuiltAt, out var builtAt) || builtAt.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("The API artifact must embed an ISO-8601 UTC PMCS release build time.");
        }
    }

    private static string Read(Dictionary<string, string> metadata, string key, string fallback) =>
        metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
}
