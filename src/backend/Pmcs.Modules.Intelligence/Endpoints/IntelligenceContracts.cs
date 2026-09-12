using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Modules.Intelligence.Endpoints;

public sealed record CreateInsightGenerationRequest;
public sealed record ReviewInsightRequest(long BaseRevision, string? Comment);

public sealed record InsightGenerationRequestResponse(
    Guid RequestId,
    Guid ProjectId,
    InsightGenerationStatus Status,
    Guid? SnapshotId,
    Guid? InsightId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? NextAttemptAt,
    int Attempts,
    string? LastErrorCode,
    long Revision)
{
    internal static InsightGenerationRequestResponse From(InsightGenerationRequest request) => new(
        request.Id,
        request.ProjectId,
        request.Status,
        request.SnapshotId,
        request.InsightId,
        request.RequestedAt,
        request.StartedAt,
        request.CompletedAt,
        request.NextAttemptAt,
        request.Attempts,
        request.LastErrorCode,
        request.Revision);
}

public sealed record AdvisoryInsightResponse(
    Guid InsightId,
    Guid ProjectId,
    Guid GenerationRequestId,
    Guid SnapshotId,
    AdvisoryInsightOutput Output,
    bool IncludesFinancialData,
    bool IncludesCommercialData,
    bool IncludesActionData,
    string Provider,
    string Model,
    string? ProviderResponseId,
    string PromptVersion,
    string PolicyVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt,
    bool IsStale,
    AdvisoryReviewStatus ReviewStatus,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    long Revision)
{
    internal static AdvisoryInsightResponse From(AdvisoryInsight insight, bool isStale = false) => new(
        insight.Id,
        insight.ProjectId,
        insight.GenerationRequestId,
        insight.SnapshotId,
        insight.Output,
        insight.IncludesFinancialData,
        insight.IncludesCommercialData,
        insight.IncludesActionData,
        insight.Provider,
        insight.Model,
        insight.ProviderResponseId,
        insight.PromptVersion,
        insight.PolicyVersion,
        insight.GeneratedAt,
        insight.ExpiresAt,
        isStale,
        insight.ReviewStatus,
        insight.ReviewedAt,
        insight.ReviewComment,
        insight.Revision);
}

public sealed record AdvisoryInsightListResponse(
    bool ProviderConfigured,
    bool CanGenerate,
    bool CanReview,
    IReadOnlyCollection<AdvisoryInsightResponse> Insights,
    IReadOnlyCollection<InsightGenerationRequestResponse> ActiveRequests,
    IReadOnlyCollection<InsightGenerationRequestResponse> RecentRequests);
