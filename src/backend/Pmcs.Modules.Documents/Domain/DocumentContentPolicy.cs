using System.IO.Compression;
using System.Text;

namespace Pmcs.Modules.Documents.Domain;

internal static class DocumentContentPolicy
{
    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private static readonly Dictionary<string, string[]> Extensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"],
            ["image/heic"] = [".heic"],
            ["image/heif"] = [".heif"],
            ["application/pdf"] = [".pdf"],
            ["text/plain"] = [".txt", ".log"],
            ["text/csv"] = [".csv"],
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = [".pptx"]
        };

    public static bool IsAllowedContentType(string contentType) => Extensions.ContainsKey(contentType);

    public static bool IsFileNameCompatible(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        return Extensions.TryGetValue(contentType, out var allowed) &&
            allowed.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static string CanonicalExtension(string contentType) =>
        Extensions.TryGetValue(contentType, out var allowed) ? allowed[0] : string.Empty;

    public static bool MatchesSignature(string contentType, ReadOnlyMemory<byte> content) =>
        contentType switch
        {
            "image/jpeg" => content.Length >= 3 &&
                content.Span[0] == 0xff && content.Span[1] == 0xd8 && content.Span[2] == 0xff,
            "image/png" => content.Span.StartsWith(PngSignature),
            "image/webp" => content.Length >= 12 &&
                content.Span[..4].SequenceEqual("RIFF"u8) &&
                content.Span.Slice(8, 4).SequenceEqual("WEBP"u8),
            "image/heic" or "image/heif" => MatchesIsoBaseMediaSignature(content.Span),
            "application/pdf" => content.Span.StartsWith("%PDF-"u8),
            "text/plain" or "text/csv" => IsSafeUtf8Text(content.Span),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                MatchesOpenXmlPackage(content, "word/"),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                MatchesOpenXmlPackage(content, "xl/"),
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" =>
                MatchesOpenXmlPackage(content, "ppt/"),
            _ => false
        };

    private static bool IsSafeUtf8Text(ReadOnlySpan<byte> content)
    {
        if (content.Contains((byte)0))
        {
            return false;
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool MatchesOpenXmlPackage(ReadOnlyMemory<byte> content, string requiredPrefix)
    {
        if (content.Length < 4 || content.Span[0] != 0x50 || content.Span[1] != 0x4b)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(content.ToArray(), writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            return archive.GetEntry("[Content_Types].xml") is not null &&
                archive.Entries.Any(entry => entry.FullName.StartsWith(requiredPrefix, StringComparison.Ordinal));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

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
