using Pmcs.Modules.Documents.Domain;

namespace Pmcs.Modules.Documents.Scanning;

internal interface IContentScanner
{
    Task<ContentScanResult> ScanAsync(
        string originalFileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);
}

internal sealed record ContentScanResult(
    DocumentScanVerdict Verdict,
    string Provider,
    string? Details);
