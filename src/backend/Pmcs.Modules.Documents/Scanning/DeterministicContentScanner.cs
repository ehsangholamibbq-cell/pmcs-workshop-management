using System.Text;
using Pmcs.Modules.Documents.Domain;

namespace Pmcs.Modules.Documents.Scanning;

internal sealed class DeterministicContentScanner : IContentScanner
{
    internal const string ProviderName = "pmcs-policy-scanner-v1";
    private static readonly byte[] EicarMarker = Encoding.ASCII.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE");

    public Task<ContentScanResult> ScanAsync(
        string originalFileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!DocumentContentPolicy.IsFileNameCompatible(originalFileName, contentType) ||
            !DocumentContentPolicy.MatchesSignature(contentType, content))
        {
            return Task.FromResult(new ContentScanResult(
                DocumentScanVerdict.Failed,
                ProviderName,
                "content-policy-mismatch"));
        }

        if (content.Span.IndexOf(EicarMarker) >= 0)
        {
            return Task.FromResult(new ContentScanResult(
                DocumentScanVerdict.Infected,
                ProviderName,
                "test-malware-signature-detected"));
        }

        return Task.FromResult(new ContentScanResult(
            DocumentScanVerdict.Clean,
            ProviderName,
            "signature-and-policy-clean"));
    }
}
