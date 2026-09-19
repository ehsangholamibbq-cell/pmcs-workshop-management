using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static readonly ReportingOrphanObjectFixture[] ReportingOrphanObjects =
    [
        new(
            Guid.Parse("73000000-0000-4000-8000-000000000201"),
            "qa-orphan-eligible,1",
            ExpectedAfterRemediation: false),
        new(
            Guid.Parse("73000000-0000-4000-8000-000000000202"),
            "qa-orphan-retention,1",
            ExpectedAfterRemediation: true),
        new(
            Guid.Parse("73000000-0000-4000-8000-000000000203"),
            "qa-orphan-legal-hold,1",
            ExpectedAfterRemediation: true),
        new(
            Guid.Parse("73000000-0000-4000-8000-000000000204"),
            "qa-orphan-owned,1",
            ExpectedAfterRemediation: true)
    ];

    private static async Task<int> PrepareReportingOrphanObjectsAsync()
    {
        using var storage = CreateReportingOrphanStorage();
        var bucket = ReadRequiredEnvironment("PMCS_QA_S3_BUCKET");
        foreach (var fixture in ReportingOrphanObjects)
        {
            var bytes = Encoding.UTF8.GetBytes(fixture.Content);
            await using var content = new MemoryStream(bytes, writable: false);
            await storage.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = ReportingOrphanObjectKey(fixture.DocumentId),
                ContentType = "text/csv",
                InputStream = content,
                AutoCloseStream = false,
                AutoResetStreamPosition = false
            }, CancellationToken.None);
        }

        return WriteReportingOrphanObjectResult(
            "rpt1-orphan-object-preparation",
            ReportingOrphanObjects.Length,
            ReportingOrphanObjects.Length);
    }

    private static async Task<int> VerifyReportingOrphanObjectsAsync()
    {
        using var storage = CreateReportingOrphanStorage();
        var bucket = ReadRequiredEnvironment("PMCS_QA_S3_BUCKET");
        var passed = 0;
        foreach (var fixture in ReportingOrphanObjects)
        {
            var exists = await ReportingOrphanObjectExistsAsync(
                storage,
                bucket,
                ReportingOrphanObjectKey(fixture.DocumentId));
            if (exists == fixture.ExpectedAfterRemediation)
            {
                passed++;
            }
        }

        return WriteReportingOrphanObjectResult(
            "rpt1-orphan-object-remediation-verification",
            ReportingOrphanObjects.Length,
            passed);
    }

    private static async Task<int> CleanupReportingOrphanObjectsAsync()
    {
        using var storage = CreateReportingOrphanStorage();
        var bucket = ReadRequiredEnvironment("PMCS_QA_S3_BUCKET");
        foreach (var fixture in ReportingOrphanObjects)
        {
            await storage.DeleteObjectAsync(
                bucket,
                ReportingOrphanObjectKey(fixture.DocumentId),
                CancellationToken.None);
        }

        return WriteReportingOrphanObjectResult(
            "rpt1-orphan-object-cleanup",
            ReportingOrphanObjects.Length,
            ReportingOrphanObjects.Length);
    }

    private static AmazonS3Client CreateReportingOrphanStorage()
    {
        var endpoint = ReadRequiredEnvironment("PMCS_QA_S3_ENDPOINT");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            (!string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("PMCS_QA_S3_ENDPOINT must be an absolute HTTP or HTTPS URL.");
        }

        return new AmazonS3Client(
            new BasicAWSCredentials(
                ReadRequiredEnvironment("PMCS_QA_S3_ACCESS_KEY"),
                ReadRequiredEnvironment("PMCS_QA_S3_SECRET_KEY")),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            });
    }

    private static async Task<bool> ReportingOrphanObjectExistsAsync(
        AmazonS3Client storage,
        string bucket,
        string objectKey)
    {
        try
        {
            using var response = await storage.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = objectKey
            }, CancellationToken.None);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static string ReportingOrphanObjectKey(Guid documentId) =>
        $"tenants/{PmcsTestDataSet.TenantId:N}/projects/{PmcsTestDataSet.ProjectId:N}/documents/" +
        $"{documentId:N}/v1.csv";

    private static int WriteReportingOrphanObjectResult(string stage, int assertions, int passed)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage,
            passed = assertions == passed,
            assertionCount = assertions,
            passedCount = passed,
            failedCount = assertions - passed,
            capturedAt = DateTimeOffset.UtcNow
        }, JsonOptions));
        return assertions == passed ? 0 : 1;
    }

    private sealed record ReportingOrphanObjectFixture(
        Guid DocumentId,
        string Content,
        bool ExpectedAfterRemediation);
}
