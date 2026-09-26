namespace Pmcs.Modules.Reporting.Rendering;

internal static class CertifiedPdfRuntimeContract
{
    public const string QuestPdfPackageVersion = "2026.8.0";
    public const string LicenseDecision = "Community";
    public const string RuntimeImageReference =
        "mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c";
    public const string RuntimeImageDigest =
        "sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c";
    public const string BuildImageReference =
        "mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d";
    public const string FontFamily = "DejaVu Sans";
    public const string RegularFontSha256 =
        "ae7b7855e115a5966d8b1b3f80f254ccc117ec86f9965e202ee2940453837280";
    public const string BoldFontSha256 =
        "5c1247acef7f2b8522a31742c76d6adcb5569bacc0be7ceaa4dc39dd252ce895";
    public const int QualificationRasterDpi = 96;
    public const int QualificationColdRenderBudgetMilliseconds = 5_000;
    public const int QualificationWarmRenderBudgetMilliseconds = 2_500;
    public const int QualificationMaximumPdfBytes = 5 * 1024 * 1024;
}
