using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Evidence.Domain;

namespace Pmcs.Domain.Tests;

public sealed class EvidenceFileTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PendingEvidenceAcceptsSupportedPhotoWithoutPlanningBaselines()
    {
        var evidence = Create();

        Assert.Equal(EvidenceFileStatus.PendingUpload, evidence.Status);
        Assert.Equal(CreatedAt.AddHours(24), evidence.UploadExpiresAt);
        Assert.Null(evidence.DailyFactId);
    }

    [Fact]
    public void EvidenceRejectsUnsupportedBinaryType()
    {
        var exception = Assert.Throws<DomainRuleException>(() => Create(contentType: "application/x-msdownload"));

        Assert.Equal("evidence.content_type.unsupported", exception.Code);
    }

    [Fact]
    public void EvidenceRequiresValidSha256AndBoundedSize()
    {
        Assert.Equal(
            "evidence.sha256.invalid",
            Assert.Throws<DomainRuleException>(() => Create(sha256: "abc")).Code);
        Assert.Equal(
            "evidence.size.invalid",
            Assert.Throws<DomainRuleException>(() => Create(sizeBytes: EvidenceFile.MaximumSizeBytes + 1)).Code);
    }

    [Fact]
    public void UploadCompletionIsIdempotentAndAdvancesRevisionOnce()
    {
        var evidence = Create();

        evidence.MarkUploaded("etag-1", CreatedAt.AddMinutes(5));
        evidence.MarkUploaded("etag-1", CreatedAt.AddMinutes(6));

        Assert.Equal(EvidenceFileStatus.Uploaded, evidence.Status);
        Assert.Equal(2, evidence.Revision);
        Assert.Equal(CreatedAt.AddMinutes(5), evidence.UploadedAt);
    }

    private static EvidenceFile Create(
        string contentType = "image/jpeg",
        long sizeBytes = 1_024,
        string? sha256 = null) =>
        EvidenceFile.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "site-photo.jpg",
            contentType,
            sizeBytes,
            sha256 ?? new string('a', 64),
            $"evidence/{Guid.NewGuid():N}.jpg",
            CreatedAt,
            Guid.NewGuid(),
            CreatedAt);
}
