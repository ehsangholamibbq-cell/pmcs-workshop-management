using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Intelligence.Domain;

public enum AdvisoryInsightType
{
    ExecutiveSummary = 1,
    EmergingRisk = 2,
    LikelyCause = 3,
    MissingDataWarning = 4
}

public enum AdvisoryConfidenceBand
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum AdvisoryReviewStatus
{
    NeedsReview = 1,
    Accepted = 2,
    Dismissed = 3
}

public sealed record AdvisorySuggestedAction(string Title, string Rationale);

public sealed record AdvisoryInsightOutput(
    AdvisoryInsightType InsightType,
    string Statement,
    IReadOnlyCollection<string> EvidenceReferences,
    IReadOnlyCollection<string> FactsUsed,
    IReadOnlyCollection<string> Assumptions,
    IReadOnlyCollection<string> DataGaps,
    AdvisoryConfidenceBand ConfidenceBand,
    string PotentialImpact,
    IReadOnlyCollection<AdvisorySuggestedAction> SuggestedActions,
    string SuggestedOwnerRole);

public sealed class AdvisoryInsight
{
    private AdvisoryInsight()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid GenerationRequestId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public string OutputJson { get; private set; } = string.Empty;
    public AdvisoryInsightType InsightType { get; private set; }
    public string Statement { get; private set; } = string.Empty;
    public AdvisoryConfidenceBand ConfidenceBand { get; private set; }
    public string PotentialImpact { get; private set; } = string.Empty;
    public string SuggestedOwnerRole { get; private set; } = string.Empty;
    public string ContextHash { get; private set; } = string.Empty;
    public bool IncludesFinancialData { get; private set; }
    public bool IncludesCommercialData { get; private set; }
    public bool IncludesActionData { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string? ProviderResponseId { get; private set; }
    public string PromptVersion { get; private set; } = string.Empty;
    public string PolicyVersion { get; private set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public AdvisoryReviewStatus ReviewStatus { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }
    public long Revision { get; private set; }

    public AdvisoryInsightOutput Output => JsonSerializer.Deserialize<AdvisoryInsightOutput>(
        OutputJson,
        AdvisoryJson.Options) ?? throw new InvalidOperationException("Stored advisory output is invalid.");

    public static AdvisoryInsight Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid generationRequestId,
        Guid snapshotId,
        Guid requestedBy,
        AdvisoryInsightOutput output,
        string contextHash,
        bool includesFinancialData,
        bool includesCommercialData,
        bool includesActionData,
        string provider,
        string model,
        string? providerResponseId,
        string promptVersion,
        string policyVersion,
        DateTimeOffset generatedAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            generationRequestId == Guid.Empty || snapshotId == Guid.Empty || requestedBy == Guid.Empty)
        {
            throw new ArgumentException("Advisory insight identifiers are required.");
        }

        if (expiresAt <= generatedAt)
        {
            throw new DomainRuleException("insight.expiration.invalid", "Insight expiration must be after generation.");
        }

        var normalized = AdvisoryOutputValidator.NormalizeAndValidate(output);
        return new AdvisoryInsight
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            GenerationRequestId = generationRequestId,
            SnapshotId = snapshotId,
            RequestedBy = requestedBy,
            OutputJson = JsonSerializer.Serialize(normalized, AdvisoryJson.Options),
            InsightType = normalized.InsightType,
            Statement = normalized.Statement,
            ConfidenceBand = normalized.ConfidenceBand,
            PotentialImpact = normalized.PotentialImpact,
            SuggestedOwnerRole = normalized.SuggestedOwnerRole,
            ContextHash = Required(contextHash, 64, "insight.context_hash.required"),
            IncludesFinancialData = includesFinancialData,
            IncludesCommercialData = includesCommercialData,
            IncludesActionData = includesActionData,
            Provider = Required(provider, 80, "insight.provider.required"),
            Model = Required(model, 120, "insight.model.required"),
            ProviderResponseId = Optional(providerResponseId, 200, "insight.provider_response_id.too_long"),
            PromptVersion = Required(promptVersion, 80, "insight.prompt_version.required"),
            PolicyVersion = Required(policyVersion, 80, "insight.policy_version.required"),
            GeneratedAt = generatedAt,
            ExpiresAt = expiresAt,
            ReviewStatus = AdvisoryReviewStatus.NeedsReview,
            Revision = 1
        };
    }

    public void Review(
        AdvisoryReviewStatus decision,
        Guid reviewerId,
        string? comment,
        long baseRevision,
        DateTimeOffset reviewedAt)
    {
        if (baseRevision != Revision)
        {
            throw new DomainRuleException("insight.revision.conflict", "Insight was changed by another reviewer.");
        }

        if (ReviewStatus != AdvisoryReviewStatus.NeedsReview)
        {
            throw new DomainRuleException("insight.already_reviewed", "Insight has already been reviewed.");
        }

        if (reviewedAt >= ExpiresAt)
        {
            throw new DomainRuleException("insight.expired", "Insight has expired and cannot be reviewed.");
        }

        if (decision is not (AdvisoryReviewStatus.Accepted or AdvisoryReviewStatus.Dismissed))
        {
            throw new DomainRuleException("insight.review.invalid", "Review decision is invalid.");
        }

        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer is required.", nameof(reviewerId));
        }

        ReviewStatus = decision;
        ReviewedBy = reviewerId;
        ReviewedAt = reviewedAt;
        ReviewComment = Optional(comment, 1_000, "insight.review_comment.too_long");
        Revision++;
    }

    private static string Required(string value, int maximumLength, string code)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, "Required value is missing or too long.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string code)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, "Optional value is too long.");
        }

        return normalized;
    }
}

public static class AdvisoryJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
