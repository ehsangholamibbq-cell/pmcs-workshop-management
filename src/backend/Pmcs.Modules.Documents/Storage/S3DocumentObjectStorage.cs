using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Pmcs.Modules.Documents.Storage;

internal sealed class S3DocumentObjectStorage : IDocumentObjectStorage, IDisposable
{
    private readonly DocumentObjectStorageOptions options;
    private readonly AmazonS3Client client;
    private readonly SemaphoreSlim bucketGate = new(1, 1);
    private bool bucketReady;

    public S3DocumentObjectStorage(IOptions<DocumentObjectStorageOptions> configuredOptions)
    {
        options = configuredOptions.Value;
        Validate(options);
        client = new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region
            });
    }

    public async Task<DocumentStoredObjectReceipt> PutAsync(
        string objectKey,
        string contentType,
        string sha256,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        var response = await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.BucketName,
            Key = objectKey,
            ContentType = contentType,
            InputStream = content,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            Metadata = { ["sha256"] = sha256 }
        }, cancellationToken);

        var metadata = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = options.BucketName,
            Key = objectKey
        }, cancellationToken);
        return new DocumentStoredObjectReceipt(
            response.ETag ?? metadata.ETag ?? string.Empty,
            metadata.ContentLength);
    }

    public async Task<DocumentStoredObjectContent?> ReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        try
        {
            using var response = await client.GetObjectAsync(options.BucketName, objectKey, cancellationToken);
            await using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return new DocumentStoredObjectContent(
                buffer.ToArray(),
                response.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        await client.DeleteObjectAsync(options.BucketName, objectKey, cancellationToken);
    }

    public void Dispose()
    {
        client.Dispose();
        bucketGate.Dispose();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (bucketReady)
        {
            return;
        }

        await bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (bucketReady)
            {
                return;
            }

            var buckets = await client.ListBucketsAsync(cancellationToken);
            if (buckets.Buckets?.Any(
                    bucket => string.Equals(bucket.BucketName, options.BucketName, StringComparison.Ordinal)) != true)
            {
                if (!options.CreateBucketIfMissing)
                {
                    throw new InvalidOperationException(
                        $"Object storage bucket '{options.BucketName}' does not exist and automatic creation is disabled.");
                }

                await client.PutBucketAsync(
                    new PutBucketRequest { BucketName = options.BucketName },
                    cancellationToken);
            }

            bucketReady = true;
        }
        finally
        {
            bucketGate.Release();
        }
    }

    private static void Validate(DocumentObjectStorageOptions value)
    {
        if (string.IsNullOrWhiteSpace(value.ServiceUrl) ||
            string.IsNullOrWhiteSpace(value.AccessKey) ||
            string.IsNullOrWhiteSpace(value.SecretKey) ||
            string.IsNullOrWhiteSpace(value.BucketName))
        {
            throw new InvalidOperationException(
                "ObjectStorage ServiceUrl, credentials and BucketName are required.");
        }
    }
}
