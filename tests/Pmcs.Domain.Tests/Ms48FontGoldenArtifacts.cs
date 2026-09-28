namespace Pmcs.Domain.Tests;

internal static class Ms48FontGoldenArtifacts
{
    internal static void Save(string fileName, byte[] bytes)
    {
        var root = Environment.GetEnvironmentVariable("PMCS_MS48_FONT_QUALIFICATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, fileName), bytes);
    }
}
