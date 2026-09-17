namespace Pmcs.Modules.Evidence.Domain;

internal static class EvidenceContentPolicy
{
    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    public static bool IsAllowedContentType(string contentType) =>
        contentType is "image/jpeg" or "image/png" or "image/webp" or
            "image/heic" or "image/heif" or "application/pdf";

    public static bool MatchesSignature(string contentType, ReadOnlySpan<byte> content) =>
        contentType switch
        {
            "image/jpeg" => content.Length >= 3 &&
                content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff,
            "image/png" => content.StartsWith(PngSignature),
            "image/webp" => content.Length >= 12 &&
                content[..4].SequenceEqual("RIFF"u8) &&
                content.Slice(8, 4).SequenceEqual("WEBP"u8),
            "image/heic" or "image/heif" => MatchesIsoBaseMediaSignature(content),
            "application/pdf" => content.StartsWith("%PDF-"u8),
            _ => false
        };

    public static string CanonicalExtension(string contentType) =>
        contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            "application/pdf" => ".pdf",
            _ => string.Empty
        };

    private static bool MatchesIsoBaseMediaSignature(ReadOnlySpan<byte> content)
    {
        if (content.Length < 12 || !content.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return false;
        }

        var inspectedLength = Math.Min(content.Length, 64);
        for (var offset = 8; offset + 4 <= inspectedLength; offset += 4)
        {
            var brand = content.Slice(offset, 4);
            if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) ||
                brand.SequenceEqual("hevc"u8) || brand.SequenceEqual("hevx"u8) ||
                brand.SequenceEqual("heim"u8) || brand.SequenceEqual("heis"u8) ||
                brand.SequenceEqual("hevm"u8) || brand.SequenceEqual("hevs"u8) ||
                brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8))
            {
                return true;
            }
        }

        return false;
    }
}
