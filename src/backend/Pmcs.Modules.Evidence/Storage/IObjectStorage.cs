namespace Pmcs.Modules.Evidence.Storage;

internal interface IObjectStorage
{
    Task<StoredObjectReceipt> PutAsync(
        string objectKey,
        string contentType,
        string sha256,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<StoredObjectContent?> ReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}

internal sealed record StoredObjectReceipt(string ETag, long SizeBytes);

internal sealed record StoredObjectContent(byte[] Bytes, string ContentType);
