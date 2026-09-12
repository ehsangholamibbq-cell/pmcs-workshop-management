using Pmcs.Modules.Evidence.Domain;

namespace Pmcs.Modules.Evidence.Endpoints;

public sealed record CreateEvidenceUploadSessionRequest(
    Guid ClientGeneratedId,
    Guid DailyReportId,
    Guid? DailyFactId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DateTimeOffset? CapturedAtDevice);

public sealed record EvidenceUploadSessionResponse(
    EvidenceFileResponse Evidence,
    string? UploadMethod,
    string? UploadUrl,
    DateTimeOffset? ExpiresAt);

public sealed record EvidenceFileResponse(
    Guid Id,
    Guid ProjectId,
    Guid DailyReportId,
    Guid? DailyFactId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    EvidenceFileStatus Status,
    DateTimeOffset? CapturedAtDevice,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UploadedAt,
    long Revision,
    string ContentUrl)
{
    public static EvidenceFileResponse From(EvidenceFile evidence) => new(
        evidence.Id,
        evidence.ProjectId,
        evidence.DailyReportId,
        evidence.DailyFactId,
        evidence.OriginalFileName,
        evidence.ContentType,
        evidence.SizeBytes,
        evidence.Sha256,
        evidence.Status,
        evidence.CapturedAtDevice,
        evidence.CreatedAt,
        evidence.UploadedAt,
        evidence.Revision,
        $"/api/v1/projects/{evidence.ProjectId}/evidence/{evidence.Id}/content");
}
