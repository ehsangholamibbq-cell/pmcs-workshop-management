using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Pmcs.Modules.Evidence.Storage;

internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly ObjectStorageOptions _options;
    private readonly AmazonS3Client _client;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketReady;

    public S3ObjectStorage(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        Validate(_options);
        _client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                ForcePathStyle = _options.ForcePathStyle,
                AuthenticationRegion = _options.Region
            });
    }

    public async Task<StoredObjectReceipt> PutAsync(
        string objectKey,
        string contentType,
        string sha256,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        var response = await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            ContentType = contentType,
            InputStream = content,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            Metadata = { ["sha256"] = sha256 }
        }, cancellationToken);

        var metadata = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey
        }, cancellationToken);
        return new StoredObjectReceipt(response.ETag ?? metadata.ETag ?? string.Empty, metadata.ContentLength);
    }

    public async Task<StoredObjectContent?> ReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        try
        {
            using var response = await _client.GetObjectAsync(_options.BucketName, objectKey, cancellationToken);
            await using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return new StoredObjectContent(buffer.ToArray(), response.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        await _client.DeleteObjectAsync(_options.BucketName, objectKey, cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
        _bucketGate.Dispose();
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
        {
            return;
        }

        await _bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
            {
                return;
            }

            var buckets = await _client.ListBucketsAsync(cancellationToken);
            if (buckets.Buckets?.Any(
                    bucket => string.Equals(bucket.BucketName, _options.BucketName, StringComparison.Ordinal)) != true)
            {
                if (!_options.CreateBucketIfMissing)
                {
                    throw new InvalidOperationException(
                        $"Object storage bucket '{_options.BucketName}' does not exist and automatic creation is disabled.");
                }

                await _client.PutBucketAsync(new PutBucketRequest { BucketName = _options.BucketName }, cancellationToken);
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }

    private static void Validate(ObjectStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceUrl) ||
            string.IsNullOrWhiteSpace(options.AccessKey) ||
            string.IsNullOrWhiteSpace(options.SecretKey) ||
            string.IsNullOrWhiteSpace(options.BucketName))
        {
            throw new InvalidOperationException("ObjectStorage ServiceUrl, credentials and BucketName are required.");
        }
    }
}

