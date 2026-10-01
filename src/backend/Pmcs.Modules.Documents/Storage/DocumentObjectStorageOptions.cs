namespace Pmcs.Modules.Documents.Storage;

internal sealed class DocumentObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string ServiceUrl { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string BucketName { get; init; } = string.Empty;

    public string Region { get; init; } = "us-east-1";

    public bool ForcePathStyle { get; init; } = true;

    public bool CreateBucketIfMissing { get; init; }
}
