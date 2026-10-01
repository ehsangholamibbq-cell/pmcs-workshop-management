namespace Pmcs.Modules.Documents.Storage;

internal interface IDocumentObjectStorage
{
    Task<DocumentStoredObjectReceipt> PutAsync(
        string objectKey,
        string contentType,
        string sha256,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<DocumentStoredObjectContent?> ReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}

internal sealed record DocumentStoredObjectReceipt(string ETag, long SizeBytes);

internal sealed record DocumentStoredObjectContent(byte[] Bytes, string ContentType);
