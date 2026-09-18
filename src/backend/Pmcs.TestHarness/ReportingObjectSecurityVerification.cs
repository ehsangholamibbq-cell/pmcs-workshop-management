using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Pmcs.BuildingBlocks.Testing;

namespace Pmcs.TestHarness;

internal static partial class Program
{
    private static async Task<int> VerifyReportingObjectSecurityAsync()
    {
        var key = ReadRequiredEnvironment("PMCS_QA_AUTH_KEY");
        var endpoint = ReadRequiredEnvironment("PMCS_QA_S3_ENDPOINT");
        var accessKey = ReadRequiredEnvironment("PMCS_QA_S3_ACCESS_KEY");
        var secretKey = ReadRequiredEnvironment("PMCS_QA_S3_SECRET_KEY");
        var bucket = ReadRequiredEnvironment("PMCS_QA_S3_BUCKET");
        var objectKey = ReadRequiredEnvironment("PMCS_QA_REPORTING_OBJECT_KEY");
        var expectedSha256 = ReadRequiredEnvironment("PMCS_QA_REPORTING_EXPECTED_SHA256");
        var expectedContentType = ReadRequiredEnvironment("PMCS_QA_REPORTING_EXPECTED_CONTENT_TYPE");
        if (!Guid.TryParse(
                ReadRequiredEnvironment("PMCS_QA_REPORTING_OUTPUT_ID"),
                out var outputId) || outputId == Guid.Empty)
        {
            throw new InvalidOperationException("PMCS_QA_REPORTING_OUTPUT_ID must be a non-empty UUID.");
        }
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            (!string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("PMCS_QA_S3_ENDPOINT must be an absolute HTTP or HTTPS URL.");
        }
        var expectedPrefix = $"tenants/{PmcsTestDataSet.TenantId:N}/projects/" +
            $"{PmcsTestDataSet.ProjectId:N}/documents/";
        if (!objectKey.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            objectKey.Contains("..", StringComparison.Ordinal) ||
            expectedSha256.Length != 64 ||
            expectedSha256.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidOperationException("The reporting object fixture identity is invalid.");
        }

        using var storage = new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            });
        var original = await ReadObjectAsync(storage, bucket, objectKey);
        var originalHash = ReportingSha256(original.Bytes);
        var assertions = new List<VerificationAssertion>();
        Record(
            assertions,
            "reporting.object.original.integrity",
            original.Bytes.Length > 32 &&
            string.Equals(originalHash, expectedSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(original.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase),
            $"size={original.Bytes.Length};sha256Matches=" +
            string.Equals(originalHash, expectedSha256, StringComparison.OrdinalIgnoreCase));

        using var client = CreateClient();
        var actor = Actor("technical-office");
        var verifyPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports/outputs/{outputId}/verify";
        var contentPath = $"/api/v1/projects/{PmcsTestDataSet.ProjectId}/reports/outputs/{outputId}/content";

        try
        {
            var tampered = original.Bytes.ToArray();
            tampered[Math.Max(8, tampered.Length / 2)] ^= 0x5A;
            await PutObjectAsync(
                storage,
                bucket,
                objectKey,
                original.ContentType,
                expectedSha256,
                tampered);
            var tamperedVerify = await SendAsync(client, key, actor, HttpMethod.Get, verifyPath);
            RecordIntegrityFailure(
                assertions,
                "reporting.object.byte-tamper.verify-fails-closed",
                tamperedVerify);
            var tamperedContent = await SendAsync(client, key, actor, HttpMethod.Get, contentPath);
            RecordIntegrityFailure(
                assertions,
                "reporting.object.byte-tamper.download-fails-closed",
                tamperedContent);

            await storage.DeleteObjectAsync(bucket, objectKey, CancellationToken.None);
            var missingVerify = await SendAsync(client, key, actor, HttpMethod.Get, verifyPath);
            RecordIntegrityFailure(
                assertions,
                "reporting.object.missing.verify-fails-closed",
                missingVerify);

            var malformed = Encoding.UTF8.GetBytes("invalid-certified-report-object");
            await PutObjectAsync(
                storage,
                bucket,
                objectKey,
                original.ContentType,
                expectedSha256,
                malformed);
            var malformedVerify = await SendAsync(client, key, actor, HttpMethod.Get, verifyPath);
            RecordIntegrityFailure(
                assertions,
                "reporting.object.malformed.verify-fails-closed",
                malformedVerify);
        }
        finally
        {
            await PutObjectAsync(
                storage,
                bucket,
                objectKey,
                original.ContentType,
                expectedSha256,
                original.Bytes);
        }

        var restored = await SendAsync(client, key, actor, HttpMethod.Get, verifyPath);
        Record(
            assertions,
            "reporting.object.restored.verifies",
            restored.StatusCode == HttpStatusCode.OK && HasString(restored.Payload, "status", "Valid"),
            $"http={(int)restored.StatusCode};status={ReadOptionalString(restored.Payload, "status")}");

        var failed = assertions.Count(assertion => !assertion.Passed);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            contractVersion = 1,
            stage = "rpt1-reporting-object-security-verification",
            passed = failed == 0,
            assertionCount = assertions.Count,
            passedCount = assertions.Count - failed,
            failedCount = failed,
            tenantId = PmcsTestDataSet.TenantId,
            projectId = PmcsTestDataSet.ProjectId,
            outputId,
            capturedAt = DateTimeOffset.UtcNow,
            assertions
        }, JsonOptions));
        return failed == 0 ? 0 : 1;
    }

    private static void RecordIntegrityFailure(
        List<VerificationAssertion> assertions,
        string name,
        HarnessHttpResult result) => Record(
            assertions,
            name,
            result.StatusCode == HttpStatusCode.BadGateway &&
            HasString(result.Payload, "code", "reporting.output.integrity_failed"),
            $"http={(int)result.StatusCode};code={ReadOptionalString(result.Payload, "code")}");

    private static async Task<ReportingStoredObject> ReadObjectAsync(
        IAmazonS3 storage,
        string bucket,
        string objectKey)
    {
        using var response = await storage.GetObjectAsync(bucket, objectKey, CancellationToken.None);
        await using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer);
        return new ReportingStoredObject(
            buffer.ToArray(),
            response.Headers.ContentType ?? "application/octet-stream");
    }

    private static async Task PutObjectAsync(
        IAmazonS3 storage,
        string bucket,
        string objectKey,
        string contentType,
        string sha256,
        byte[] bytes)
    {
        await using var content = new MemoryStream(bytes, writable: false);
        await storage.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = objectKey,
            ContentType = contentType,
            InputStream = content,
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            Metadata = { ["sha256"] = sha256 }
        }, CancellationToken.None);
    }

    private static string ReportingSha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record ReportingStoredObject(byte[] Bytes, string ContentType);
}
