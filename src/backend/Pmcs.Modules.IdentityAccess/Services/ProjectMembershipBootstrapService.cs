using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ProjectMembershipBootstrapService(
    IdentityAccessDbContext dbContext,
    IProjectPermissionService permissionService,
    IClock clock,
    ITransactionalSideEffectWriter sideEffects,
    ITransactionalNotificationWriter notifications,
    IIdempotencyStore idempotencyStore) : IProjectMembershipBootstrapService
{
    public const string ContributorId = "identity.project-memberships";
    public const string ContributorVersion = "1.0.0";
    private const string AccessScope = "Project";
    private const string Permission = "projects.bootstrap.members_copy";
    private const string Operation = "identity.project-memberships.bootstrap-copy";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public Task<ProjectMembershipBootstrapPreview> PreviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid sourceProjectId,
        Guid targetProjectId,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> selections,
        CancellationToken cancellationToken = default) =>
        BuildPreviewAsync(
            tenantId,
            actorUserId,
            sourceProjectId,
            targetProjectId,
            selections,
            cancellationToken);

    public async Task<ProjectMembershipBootstrapExecution> ExecuteAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid planId,
        Guid sourceProjectId,
        Guid targetProjectId,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> selections,
        string expectedSnapshotToken,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentities(tenantId, actorUserId, sourceProjectId, targetProjectId);
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("A bootstrap plan id is required.", nameof(planId));
        }
        if (string.IsNullOrWhiteSpace(expectedSnapshotToken))
        {
            throw new ArgumentException("A membership preview snapshot is required.", nameof(expectedSnapshotToken));
        }
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("A correlation id is required.", nameof(correlationId));
        }

        var requestMaterial = JsonSerializer.Serialize(new
        {
            planId,
            sourceProjectId,
            targetProjectId,
            expectedSnapshotToken,
            selections = NormalizeSelections(selections)
        }, SerializerOptions);
        var requestHash = RequestHash.Create(requestMaterial);
        var idempotencyKey = $"project-bootstrap-members:{planId:N}";
        var replay = await idempotencyStore.FindAsync(
            tenantId,
            idempotencyKey,
            Operation,
            requestHash,
            cancellationToken);
        if (replay is not null)
        {
            return JsonSerializer.Deserialize<ProjectMembershipBootstrapExecution>(
                replay.ResponseBody,
                SerializerOptions) ?? throw new InvalidOperationException("Stored membership bootstrap response is invalid.");
        }

        var preview = await BuildPreviewAsync(
            tenantId,
            actorUserId,
            sourceProjectId,
            targetProjectId,
            selections,
            cancellationToken);
        if (!string.Equals(preview.SnapshotToken, expectedSnapshotToken, StringComparison.Ordinal))
        {
            throw new ProjectMembershipBootstrapChangedException(
                "Project memberships changed after the bootstrap preview was issued.");
        }
        if (preview.Items.Any(item => item.Disposition == ProjectMembershipBootstrapDisposition.Blocked))
        {
            throw new ProjectMembershipBootstrapPermissionException(
                "At least one selected membership is blocked by identity or permission policy.");
        }

        var toAdd = preview.Items
            .Where(item => item.Disposition == ProjectMembershipBootstrapDisposition.Added)
            .OrderBy(item => item.UserId)
            .ToArray();
        var now = clock.UtcNow;
        var execution = new ProjectMembershipBootstrapExecution(
            ContributorId,
            ContributorVersion,
            preview.SnapshotToken,
            preview.Items);
        var responseJson = JsonSerializer.Serialize(execution, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        foreach (var item in toAdd)
        {
            var alreadyExists = await dbContext.ProjectMemberships.AnyAsync(candidate =>
                candidate.TenantId == tenantId &&
                candidate.ProjectId == targetProjectId &&
                candidate.UserId == item.UserId,
                cancellationToken);
            if (alreadyExists)
            {
                throw new ProjectMembershipBootstrapChangedException(
                    "A destination membership was created after the bootstrap preview was issued.");
            }

            dbContext.ProjectMemberships.Add(ProjectMembership.Assign(
                Guid.NewGuid(),
                tenantId,
                targetProjectId,
                item.UserId,
                item.RequestedRoleCode,
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await notifications.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            toAdd.Select(item => new InAppNotificationDraft(
                Guid.NewGuid(),
                tenantId,
                targetProjectId,
                item.UserId,
                $"project-bootstrap:{planId:N}:membership:{item.UserId:N}",
                "ProjectMembership",
                "عضویت در پروژه جدید",
                "عضویت شما پس از تأیید ساخت پروژه جدید ثبت شد.",
                "ProjectBootstrap",
                planId,
                now)).ToArray(),
            cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    tenantId,
                    targetProjectId,
                    actorUserId,
                    "ProjectBootstrapMembershipsCopied",
                    "ProjectBootstrapPlan",
                    planId.ToString(),
                    now,
                    new Dictionary<string, object?>
                    {
                        ["sourceProjectId"] = sourceProjectId,
                        ["targetProjectId"] = targetProjectId,
                        ["addedCount"] = execution.AddedCount,
                        ["skippedCount"] = execution.SkippedCount,
                        ["conflictCount"] = execution.ConflictCount,
                        ["snapshotToken"] = preview.SnapshotToken
                    },
                    correlationId),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    tenantId,
                    targetProjectId,
                    "identity.project-memberships.copied.v1",
                    1,
                    now,
                    JsonSerializer.Serialize(new
                    {
                        planId,
                        sourceProjectId,
                        targetProjectId,
                        addedCount = execution.AddedCount,
                        occurredAt = now,
                        correlationId
                    }, SerializerOptions),
                    correlationId),
                new IdempotencyReceipt(
                    tenantId,
                    idempotencyKey,
                    Operation,
                    requestHash,
                    200,
                    responseJson,
                    now,
                    now.AddDays(30))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return execution;
    }

    private async Task<ProjectMembershipBootstrapPreview> BuildPreviewAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid sourceProjectId,
        Guid targetProjectId,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> selections,
        CancellationToken cancellationToken)
    {
        ValidateIdentities(tenantId, actorUserId, sourceProjectId, targetProjectId);
        var normalizedSelections = NormalizeSelections(selections);
        var sourceAllowed = await permissionService.HasProjectPermissionAsync(
            tenantId, actorUserId, sourceProjectId, Permission, cancellationToken);
        var targetAllowed = await permissionService.HasProjectPermissionAsync(
            tenantId, actorUserId, targetProjectId, Permission, cancellationToken);
        if (!sourceAllowed || !targetAllowed)
        {
            throw new ProjectMembershipBootstrapPermissionException(
                "The actor cannot read source memberships and grant the selected roles in the destination project.");
        }

        var selectedUserIds = normalizedSelections.Select(item => item.UserId).ToArray();
        var sourceRows = await (
            from membership in dbContext.ProjectMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on new { membership.TenantId, membership.UserId }
                equals new { user.TenantId, UserId = user.Id }
            where membership.TenantId == tenantId &&
                membership.ProjectId == sourceProjectId &&
                selectedUserIds.Contains(membership.UserId)
            select new MembershipRow(
                membership.UserId,
                user.DisplayName,
                user.Status,
                membership.RoleCode,
                membership.Status,
                membership.StartsAt,
                membership.EndsAt))
            .ToListAsync(cancellationToken);
        var targetRows = await dbContext.ProjectMemberships.AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                item.ProjectId == targetProjectId &&
                selectedUserIds.Contains(item.UserId))
            .Select(item => new TargetMembershipRow(item.UserId, item.RoleCode, item.Status, item.Revision))
            .ToListAsync(cancellationToken);

        var sources = sourceRows.ToDictionary(item => item.UserId);
        var targets = targetRows.ToDictionary(item => item.UserId);
        var now = clock.UtcNow;
        var items = normalizedSelections.Select(selection =>
        {
            if (!sources.TryGetValue(selection.UserId, out var source))
            {
                return Item(selection, null, ProjectMembershipBootstrapDisposition.Blocked,
                    "bootstrap.member.source_missing", "عضویت انتخاب‌شده در پروژه مبدأ وجود ندارد.");
            }
            if (!string.Equals(selection.AccessScope, AccessScope, StringComparison.Ordinal))
            {
                return Item(selection, source, ProjectMembershipBootstrapDisposition.Blocked,
                    "bootstrap.member.scope_unsupported", "فقط دامنه دسترسی پروژه‌ای قابل انتقال است.");
            }
            if (!ProjectRoleCatalog.IsSupported(selection.RoleCode))
            {
                return Item(selection, source, ProjectMembershipBootstrapDisposition.Blocked,
                    "bootstrap.member.role_unsupported", "نقش انتخاب‌شده در مقصد پشتیبانی نمی‌شود.");
            }
            if (source.UserStatus != UserAccountStatus.Active)
            {
                return Item(selection, source, ProjectMembershipBootstrapDisposition.Skipped,
                    "bootstrap.member.account_inactive", "حساب کاربر فعال نیست و منتقل نمی‌شود.");
            }
            if (source.MembershipStatus != MembershipStatus.Active ||
                source.StartsAt > now ||
                (source.EndsAt.HasValue && source.EndsAt.Value <= now))
            {
                return Item(selection, source, ProjectMembershipBootstrapDisposition.Skipped,
                    "bootstrap.member.membership_inactive", "عضویت مبدأ فعال و معتبر نیست.");
            }
            if (targets.TryGetValue(selection.UserId, out var target))
            {
                return string.Equals(target.RoleCode, selection.RoleCode, StringComparison.OrdinalIgnoreCase) &&
                    target.Status == MembershipStatus.Active
                    ? Item(selection, source, ProjectMembershipBootstrapDisposition.Skipped,
                        "bootstrap.member.already_exists", "عضویت هم‌ارز از قبل در مقصد وجود دارد.")
                    : Item(selection, source, ProjectMembershipBootstrapDisposition.Conflict,
                        "bootstrap.member.target_conflict", "عضویت متفاوتی برای این کاربر در مقصد وجود دارد.");
            }

            return Item(selection, source, ProjectMembershipBootstrapDisposition.Added,
                "bootstrap.member.add", "عضویت جدید با ارجاع به همان حساب کاربری ساخته می‌شود.");
        }).ToArray();

        var snapshotToken = HashSnapshot(
            tenantId,
            actorUserId,
            sourceProjectId,
            targetProjectId,
            normalizedSelections,
            sourceRows,
            targetRows,
            now,
            sourceAllowed,
            targetAllowed);
        return new ProjectMembershipBootstrapPreview(
            ContributorId,
            ContributorVersion,
            snapshotToken,
            items);
    }

    private static ProjectMembershipBootstrapItem Item(
        ProjectMembershipBootstrapSelection selection,
        MembershipRow? source,
        ProjectMembershipBootstrapDisposition disposition,
        string code,
        string detail) => new(
        selection.UserId,
        source?.DisplayName ?? "عضو ناشناخته",
        source?.RoleCode ?? string.Empty,
        selection.RoleCode,
        selection.AccessScope,
        source?.UserStatus.ToString() ?? "NotFound",
        source?.MembershipStatus.ToString() ?? "NotFound",
        disposition,
        code,
        detail);

    private static ProjectMembershipBootstrapSelection[] NormalizeSelections(
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        var normalized = selections.Select(selection =>
        {
            if (selection.UserId == Guid.Empty)
            {
                throw new ArgumentException("Selected member identities are required.", nameof(selections));
            }
            return new ProjectMembershipBootstrapSelection(
                selection.UserId,
                selection.RoleCode?.Trim() ?? string.Empty,
                selection.AccessScope?.Trim() ?? string.Empty);
        }).OrderBy(item => item.UserId).ToArray();
        if (normalized.Select(item => item.UserId).Distinct().Count() != normalized.Length)
        {
            throw new ArgumentException("A member can only be selected once.", nameof(selections));
        }
        return normalized;
    }

    private static void ValidateIdentities(
        Guid tenantId,
        Guid actorUserId,
        Guid sourceProjectId,
        Guid targetProjectId)
    {
        if (tenantId == Guid.Empty || actorUserId == Guid.Empty ||
            sourceProjectId == Guid.Empty || targetProjectId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, actor, source and target identities are required.");
        }
        if (sourceProjectId == targetProjectId)
        {
            throw new ArgumentException("Source and target projects must be different.");
        }
    }

    private static string HashSnapshot(
        Guid tenantId,
        Guid actorUserId,
        Guid sourceProjectId,
        Guid targetProjectId,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> selections,
        IReadOnlyCollection<MembershipRow> sourceRows,
        IReadOnlyCollection<TargetMembershipRow> targetRows,
        DateTimeOffset evaluatedAt,
        bool sourceAllowed,
        bool targetAllowed)
    {
        var material = JsonSerializer.Serialize(new
        {
            contributorId = ContributorId,
            contributorVersion = ContributorVersion,
            tenantId,
            actorUserId,
            sourceProjectId,
            targetProjectId,
            sourceAllowed,
            targetAllowed,
            selections = selections.OrderBy(item => item.UserId),
            sourceRows = sourceRows.OrderBy(item => item.UserId).Select(item => new
            {
                item.UserId,
                item.DisplayName,
                item.UserStatus,
                item.RoleCode,
                item.MembershipStatus,
                item.StartsAt,
                item.EndsAt,
                IsEffective = item.UserStatus == UserAccountStatus.Active &&
                    item.MembershipStatus == MembershipStatus.Active &&
                    item.StartsAt <= evaluatedAt &&
                    (!item.EndsAt.HasValue || item.EndsAt.Value > evaluatedAt)
            }),
            targetRows = targetRows.OrderBy(item => item.UserId)
        }, SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record MembershipRow(
        Guid UserId,
        string DisplayName,
        UserAccountStatus UserStatus,
        string RoleCode,
        MembershipStatus MembershipStatus,
        DateTimeOffset StartsAt,
        DateTimeOffset? EndsAt);

    private sealed record TargetMembershipRow(
        Guid UserId,
        string RoleCode,
        MembershipStatus Status,
        long Revision);
}
