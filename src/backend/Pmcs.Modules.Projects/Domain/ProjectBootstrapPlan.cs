using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Projects.Domain;

public sealed class ProjectBootstrapPlan : AggregateRoot
{
    private ProjectBootstrapPlan()
    {
    }

    private ProjectBootstrapPlan(
        Guid id,
        Guid tenantId,
        Guid sourceProjectId,
        Guid targetProjectId,
        ProjectBootstrapConflictPolicy conflictPolicy,
        string selectedCategoriesJson,
        string memberSelectionsJson,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        SourceProjectId = sourceProjectId;
        TargetProjectId = targetProjectId;
        ConflictPolicy = conflictPolicy;
        SelectedCategoriesJson = selectedCategoriesJson;
        MemberSelectionsJson = memberSelectionsJson;
        Status = ProjectBootstrapStatus.Draft;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SourceProjectId { get; private set; }

    public Guid TargetProjectId { get; private set; }

    public ProjectBootstrapConflictPolicy ConflictPolicy { get; private set; }

    public string SelectedCategoriesJson { get; private set; } = "[]";

    public string MemberSelectionsJson { get; private set; } = "[]";

    public ProjectBootstrapStatus Status { get; private set; }

    public string ContributorCatalogVersion { get; private set; } = string.Empty;

    public string PreviewDigest { get; private set; } = string.Empty;

    public string MembershipSnapshotToken { get; private set; } = string.Empty;

    public string PreviewJson { get; private set; } = "{}";

    public string? ResultJson { get; private set; }

    public long SourceRevision { get; private set; }

    public long TargetRevision { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PreviewedAt { get; private set; }

    public DateTimeOffset? PreviewExpiresAt { get; private set; }

    public DateTimeOffset? ExecutedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public static ProjectBootstrapPlan Create(
        Guid id,
        Guid tenantId,
        Guid sourceProjectId,
        Guid targetProjectId,
        ProjectBootstrapConflictPolicy conflictPolicy,
        string selectedCategoriesJson,
        string memberSelectionsJson,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || sourceProjectId == Guid.Empty ||
            targetProjectId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "project.bootstrap.identity.required",
                "Bootstrap plan, tenant, source, target and actor identities are required.");
        }
        if (sourceProjectId == targetProjectId)
        {
            throw new DomainRuleException(
                "project.bootstrap.source_target.same",
                "Source and target projects must be different.");
        }
        if (!Enum.IsDefined(conflictPolicy))
        {
            throw new DomainRuleException(
                "project.bootstrap.conflict_policy.invalid",
                "A supported bootstrap conflict policy is required.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedCategoriesJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberSelectionsJson);

        return new ProjectBootstrapPlan(
            id,
            tenantId,
            sourceProjectId,
            targetProjectId,
            conflictPolicy,
            selectedCategoriesJson,
            memberSelectionsJson,
            createdBy,
            createdAt);
    }

    public void RecordPreview(
        long baseRevision,
        string contributorCatalogVersion,
        string previewDigest,
        string membershipSnapshotToken,
        string previewJson,
        long sourceRevision,
        long targetRevision,
        DateTimeOffset previewedAt,
        DateTimeOffset expiresAt)
    {
        EnsureRevision(baseRevision);
        if (Status is ProjectBootstrapStatus.Completed or ProjectBootstrapStatus.Activated)
        {
            throw new DomainRuleException(
                "project.bootstrap.preview.finalized",
                "A completed bootstrap plan cannot be previewed again.");
        }
        if (expiresAt <= previewedAt)
        {
            throw new DomainRuleException(
                "project.bootstrap.preview.expiry.invalid",
                "Preview expiry must be later than the preview instant.");
        }

        ContributorCatalogVersion = Required(contributorCatalogVersion, "project.bootstrap.catalog.required");
        PreviewDigest = Required(previewDigest, "project.bootstrap.digest.required");
        MembershipSnapshotToken = membershipSnapshotToken?.Trim() ?? string.Empty;
        PreviewJson = Required(previewJson, "project.bootstrap.preview.required");
        SourceRevision = sourceRevision;
        TargetRevision = targetRevision;
        PreviewedAt = previewedAt;
        PreviewExpiresAt = expiresAt;
        Status = ProjectBootstrapStatus.PreviewReady;
        AdvanceRevision();
    }

    public void Complete(
        long baseRevision,
        string expectedPreviewDigest,
        string resultJson,
        long targetRevision,
        DateTimeOffset executedAt)
    {
        EnsureRevision(baseRevision);
        if (Status != ProjectBootstrapStatus.PreviewReady)
        {
            throw new DomainRuleException(
                "project.bootstrap.execute.invalid_state",
                "Only a preview-ready bootstrap plan can be executed.");
        }
        if (!string.Equals(PreviewDigest, expectedPreviewDigest?.Trim(), StringComparison.Ordinal))
        {
            throw new DomainRuleException(
                "project.bootstrap.preview.changed",
                "The bootstrap preview digest does not match the confirmed preview.");
        }
        if (!PreviewExpiresAt.HasValue || PreviewExpiresAt.Value <= executedAt)
        {
            throw new DomainRuleException(
                "project.bootstrap.preview.expired",
                "The bootstrap preview expired and must be refreshed.");
        }

        ResultJson = Required(resultJson, "project.bootstrap.result.required");
        TargetRevision = targetRevision;
        ExecutedAt = executedAt;
        Status = ProjectBootstrapStatus.Completed;
        AdvanceRevision();
    }

    public void MarkActivated(long baseRevision, DateTimeOffset activatedAt)
    {
        EnsureRevision(baseRevision);
        if (Status != ProjectBootstrapStatus.Completed)
        {
            throw new DomainRuleException(
                "project.bootstrap.activate.invalid_state",
                "Only a completed bootstrap plan can activate its destination project.");
        }

        ActivatedAt = activatedAt;
        Status = ProjectBootstrapStatus.Activated;
        AdvanceRevision();
    }

    public void EnsurePreviewUsable(string expectedDigest, DateTimeOffset now)
    {
        if (Status != ProjectBootstrapStatus.PreviewReady)
        {
            throw new DomainRuleException(
                "project.bootstrap.execute.invalid_state",
                "The bootstrap plan is not ready for execution.");
        }
        if (!string.Equals(PreviewDigest, expectedDigest?.Trim(), StringComparison.Ordinal))
        {
            throw new DomainRuleException(
                "project.bootstrap.preview.changed",
                "The bootstrap preview digest does not match the confirmed preview.");
        }
        if (!PreviewExpiresAt.HasValue || PreviewExpiresAt.Value <= now)
        {
            throw new DomainRuleException(
                "project.bootstrap.preview.expired",
                "The bootstrap preview expired and must be refreshed.");
        }
    }

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException(
                "project.bootstrap.revision.conflict",
                "The bootstrap plan changed after it was loaded.");
        }
    }

    private static string Required(string? value, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new DomainRuleException(code, "A required bootstrap value is missing.");
        }
        return normalized;
    }
}

public enum ProjectBootstrapStatus
{
    Draft = 1,
    PreviewReady = 2,
    Completed = 3,
    Activated = 4
}

public enum ProjectBootstrapConflictPolicy
{
    FailOnConflict = 1,
    SkipConflicts = 2
}
