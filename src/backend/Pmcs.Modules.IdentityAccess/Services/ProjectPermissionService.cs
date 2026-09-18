using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ProjectPermissionService(
    IdentityAccessDbContext dbContext,
    IClock clock) : IProjectPermissionService
{
    private const string PolicyVersion = "pmcs-rbac-v1";

    private static readonly HashSet<string> AdministratorOnlyPermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "identity.manage",
            "identity.users.manage",
            "projects.create",
            "projects.activate",
            "projects.locations.manage",
            "projects.calendar.configure",
            "projects.setup.configure",
            "projects.setup.configure-sensitive",
            "sync.devices.manage",
            "documents.release_quarantine"
        };

    private static readonly HashSet<string> OperationalRoles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProjectManager", "SiteSupervisor", "TechnicalOffice", "ProjectController",
            "FinanceOperator", "FinanceManager", "ContractAdministrator", "ProcurementOperator",
            "ProcurementManager", "QualityController", "HseOfficer"
        };
    private static readonly Dictionary<string, IReadOnlySet<string>> ProjectRolePermissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProjectManager"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "*" },
            ["SiteSupervisor"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "field.daily-reports.read",
                "field.daily-reports.capture",
                "field.daily-reports.submit",
                "planning.measurement-items.read",
                "planning.progress.read",
                "planning.baselines.read",
                "planning.milestones.read",
                "planning.milestones.capture",
                "planning.milestones.submit",
                "technical.read",
                "technical.documents.create",
                "technical.documents.create-revision",
                "technical.documents.submit",
                "technical.rfis.create",
                "technical.rfis.submit",
                "evidence.read",
                "evidence.upload",
                "documents.read",
                "documents.upload",
                "actions.read",
                "actions.update",
                "governance.read",
                "issues.create",
                "issues.manage",
                "risks.create",
                "decision-requests.create",
                "decision-requests.submit",
                "escalations.acknowledge",
                "quality.read",
                "quality.intake.capture",
                "quality.defects.capture",
                "quality.defects.manage",
                "hse.read",
                "hse.intake.capture",
                "hse.incidents.report",
                "hse.permits.create",
                "hse.toolbox.capture",
                "quality.actions.update",
                "hse.actions.update",
                "commercial.parties.read",
                "procurement.requests.read",
                "procurement.requests.capture",
                "procurement.requests.submit",
                "procurement.orders.read",
                "supply.read",
                "supply.receipts.capture",
                "supply.inventory.issue",
                "supply.inventory.acknowledge",
                "supply.inventory.reconcile",
                "supply.inventory.count",
                "project-state.read"
            },
            ["Observer"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "field.daily-reports.read",
                "planning.measurement-items.read",
                "planning.progress.read",
                "planning.baselines.read",
                "planning.milestones.read",
                "technical.read",
                "evidence.read",
                "documents.read",
                "actions.read",
                "governance.read",
                "quality.read",
                "hse.read",
                "supply.read",
                "project-state.read"
            },
            ["TechnicalOffice"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "field.daily-reports.read",
                "field.daily-reports.capture",
                "field.daily-reports.review",
                "planning.measurement-items.read",
                "planning.measurement-items.manage",
                "planning.progress.read",
                "planning.baselines.read",
                "planning.baselines.capture",
                "planning.baselines.submit",
                "planning.baselines.review",
                "planning.milestones.read",
                "planning.milestones.capture",
                "planning.milestones.submit",
                "planning.milestones.review",
                "technical.read",
                "technical.documents.create",
                "technical.documents.create-revision",
                "technical.documents.submit",
                "technical.documents.review",
                "technical.transmittals.create",
                "technical.transmittals.issue",
                "technical.transmittals.acknowledge",
                "technical.rfis.create",
                "technical.rfis.submit",
                "technical.rfis.issue",
                "technical.rfis.respond",
                "technical.rfis.accept",
                "technical.rfis.close",
                "technical.submittals.create",
                "technical.submittals.submit",
                "technical.submittals.review",
                "technical.submittals.close",
                "evidence.read",
                "evidence.upload",
                "documents.read",
                "documents.upload",
                "documents.classify",
                "actions.read",
                "actions.create",
                "actions.update",
                "attention.triage",
                "governance.read",
                "issues.create",
                "issues.manage",
                "issues.verify-close",
                "risks.create",
                "risks.assess",
                "risks.manage",
                "risks.review",
                "decision-requests.create",
                "decision-requests.submit",
                "decisions.review-effect",
                "escalations.acknowledge",
                "quality.read",
                "quality.intake.capture",
                "quality.intake.triage",
                "quality.inspections.capture",
                "quality.inspections.result",
                "quality.ncr.create",
                "quality.ncr.manage",
                "quality.tests.capture",
                "quality.defects.capture",
                "quality.defects.manage",
                "quality.actions.create",
                "quality.actions.update",
                "commercial.parties.read",
                "contracts.read",
                "procurement.requests.read",
                "procurement.requests.capture",
                "procurement.requests.submit",
                "procurement.orders.read",
                "supply.read",
                "supply.items.manage",
                "supply.inspections.review",
                "supply.services.accept",
                "commercial-state.read",
                "project-state.read"
            },
            ["FinanceOperator"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "governance.read",
                "issues.create",
                "risks.create",
                "decision-requests.create",
                "decision-requests.submit",
                "finance.records.read",
                "documents.read",
                "documents.upload",
                "finance.records.capture",
                "finance.records.submit",
                "financial-state.read",
                "finance.control.read",
                "finance.obligations.read",
                "finance.obligations.capture",
                "finance.obligations.submit",
                "finance.petty-cash.read",
                "finance.petty-cash.capture",
                "finance.petty-cash.submit",
                "finance.petty-cash.advance",
                "finance.petty-cash.reconcile",
                "finance.management-fees.read",
                "finance.management-fees.capture",
                "finance.management-fees.submit",
                "budget.baselines.read",
                "budget.baselines.capture",
                "budget.baselines.submit",
                "commercial.parties.read",
                "contracts.read",
                "procurement.orders.read",
                "supply.read",
                "commercial-state.read"
            },
            ["FinanceManager"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "governance.read",
                "governance.sensitive.read",
                "governance.sensitive.write",
                "issues.create",
                "issues.manage",
                "risks.create",
                "risks.assess",
                "risks.manage",
                "risks.review",
                "decision-requests.create",
                "decision-requests.submit",
                "decisions.decide",
                "decisions.implement",
                "decisions.review-effect",
                "escalations.acknowledge",
                "finance.records.read",
                "documents.read",
                "documents.upload",
                "documents.classify",
                "finance.records.capture",
                "finance.records.submit",
                "finance.records.review",
                "financial-state.read",
                "finance.control.read",
                "finance.verification.read",
                "finance.obligations.read",
                "finance.obligations.capture",
                "finance.obligations.submit",
                "finance.obligations.review",
                "finance.obligations.settle",
                "finance.petty-cash.read",
                "finance.petty-cash.capture",
                "finance.petty-cash.submit",
                "finance.petty-cash.review",
                "finance.petty-cash.advance",
                "finance.petty-cash.reconcile",
                "finance.management-fees.read",
                "finance.management-fees.capture",
                "finance.management-fees.submit",
                "finance.management-fees.review",
                "budget.baselines.read",
                "budget.baselines.capture",
                "budget.baselines.submit",
                "budget.baselines.review",
                "commercial.parties.read",
                "contracts.read",
                "procurement.orders.read",
                "supply.read",
                "commercial-state.read",
                "project-state.read"
            },
            ["ContractAdministrator"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "governance.read",
                "governance.sensitive.read",
                "governance.sensitive.write",
                "issues.create",
                "issues.manage",
                "risks.create",
                "risks.assess",
                "risks.manage",
                "risks.review",
                "decision-requests.create",
                "decision-requests.submit",
                "decisions.decide",
                "decisions.implement",
                "decisions.review-effect",
                "escalations.acknowledge",
                "commercial.parties.read",
                "commercial.parties.manage",
                "contracts.read",
                "documents.read",
                "documents.upload",
                "documents.classify",
                "contracts.capture",
                "contracts.submit",
                "contracts.review",
                "technical.read",
                "technical.documents.create",
                "technical.documents.create-revision",
                "technical.documents.submit",
                "technical.documents.review",
                "technical.confidential.read",
                "technical.confidential.manage",
                "technical.transmittals.create",
                "technical.transmittals.issue",
                "technical.transmittals.acknowledge",
                "technical.rfis.create",
                "technical.rfis.submit",
                "technical.rfis.issue",
                "technical.rfis.respond",
                "technical.rfis.accept",
                "technical.rfis.close",
                "technical.submittals.create",
                "technical.submittals.submit",
                "technical.submittals.review",
                "technical.submittals.close",
                "contracts.amendments.capture",
                "contracts.amendments.submit",
                "contracts.amendments.review",
                "supply.read",
                "supply.services.accept",
                "commercial-state.read",
                "project-state.read"
            },
            ["ProcurementOperator"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "governance.read",
                "issues.create",
                "risks.create",
                "decision-requests.create",
                "decision-requests.submit",
                "commercial.parties.read",
                "contracts.read",
                "documents.read",
                "documents.upload",
                "procurement.requests.read",
                "procurement.requests.capture",
                "procurement.requests.submit",
                "procurement.orders.read",
                "supply.read",
                "supply.items.manage",
                "supply.locations.manage",
                "supply.receipts.capture",
                "commercial-state.read",
                "project-state.read"
            },
            ["ProcurementManager"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read",
                "governance.read",
                "issues.create",
                "issues.manage",
                "risks.create",
                "risks.assess",
                "risks.manage",
                "risks.review",
                "decision-requests.create",
                "decision-requests.submit",
                "decisions.decide",
                "decisions.implement",
                "decisions.review-effect",
                "escalations.acknowledge",
                "commercial.parties.read",
                "commercial.parties.manage",
                "contracts.read",
                "documents.read",
                "documents.upload",
                "documents.classify",
                "procurement.requests.read",
                "procurement.requests.capture",
                "procurement.requests.submit",
                "procurement.requests.review",
                "procurement.orders.read",
                "procurement.orders.issue",
                "procurement.orders.manage",
                "supply.read",
                "supply.items.manage",
                "supply.locations.manage",
                "supply.receipts.capture",
                "supply.receipts.override-excess",
                "supply.inspections.review",
                "supply.inventory.post",
                "supply.inventory.issue",
                "supply.inventory.acknowledge",
                "supply.inventory.reconcile",
                "supply.inventory.transfer",
                "supply.inventory.count",
                "supply.inventory.adjust",
                "supply.services.accept",
                "commercial-state.read",
                "project-state.read"
            },
            ["QualityController"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read", "quality.read", "quality.intake.capture", "quality.intake.triage",
                "quality.inspections.capture", "quality.inspections.result", "quality.ncr.create",
                "quality.ncr.manage", "quality.ncr.verify", "quality.defects.capture",
                "quality.defects.manage", "quality.defects.verify", "quality.actions.create",
                "quality.standards.manage", "quality.tests.capture",
                "quality.actions.update", "quality.actions.verify", "quality.actions.extend", "evidence.read",
                "documents.read", "documents.upload",
                "governance.read", "issues.create", "issues.manage", "issues.verify-close",
                "risks.create", "risks.assess", "risks.manage", "risks.review",
                "decision-requests.create", "decision-requests.submit", "decisions.review-effect",
                "escalations.acknowledge",
                "evidence.upload", "supply.read", "project-state.read"
            },
            ["HseOfficer"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read", "hse.read", "hse.confidential.read", "hse.intake.capture",
                "hse.intake.triage", "hse.incidents.report", "hse.incidents.investigate",
                "hse.incidents.verify", "hse.actions.create", "hse.permits.create",
                "hse.permits.manage", "hse.permits.issue", "hse.toolbox.capture",
                "hse.competencies.manage", "hse.exposure.approve",
                "hse.actions.update", "hse.actions.verify", "hse.actions.extend",
                "governance.read", "governance.sensitive.read", "governance.sensitive.write",
                "issues.create", "issues.manage", "issues.verify-close", "risks.create",
                "risks.assess", "risks.manage", "risks.review", "decision-requests.create",
                "decision-requests.submit", "decisions.review-effect", "escalations.acknowledge",
                "evidence.read", "evidence.upload", "documents.read", "documents.upload", "project-state.read"
            },
            ["ProjectController"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read", "project-state.read", "planning.measurement-items.read",
                "planning.progress.read", "planning.baselines.read", "planning.milestones.read",
                "technical.read", "financial-state.read", "commercial-state.read", "supply.read",
                "quality.read", "hse.read", "evidence.read", "documents.read", "documents.upload",
                "documents.classify", "actions.read", "actions.create",
                "actions.update", "actions.manage", "attention.triage", "governance.read",
                "projects.planning.configure", "sync.conflicts.manage",
                "governance.configure", "governance.escalate", "governance.sensitive.read",
                "governance.sensitive.write", "issues.create", "issues.manage", "issues.verify-close",
                "risks.create", "risks.assess", "risks.manage", "risks.review",
                "decision-requests.create", "decision-requests.submit", "decisions.decide",
                "decisions.implement", "decisions.review-effect",
                "escalations.acknowledge", "escalations.manage"
            }
        };

    public async Task<bool> HasTenantPermissionAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var role = await ActiveTenantRoleAsync(tenantId, userId, cancellationToken);
        return role.HasValue && GrantsTenant(role.Value, permission);
    }

    public async Task<bool> HasProjectPermissionAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var tenantRole = await ActiveTenantRoleAsync(tenantId, userId, cancellationToken);
        if (!tenantRole.HasValue)
        {
            return false;
        }

        if (GrantsTenant(tenantRole.Value, permission))
        {
            return true;
        }

        var now = clock.UtcNow;
        var roleCodes = await dbContext.ProjectMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.ProjectId == projectId &&
                membership.Status == MembershipStatus.Active &&
                membership.StartsAt <= now &&
                (membership.EndsAt == null || membership.EndsAt > now))
            .Select(membership => membership.RoleCode)
            .ToListAsync(cancellationToken);

        return roleCodes.Any(roleCode => GrantsRole(roleCode, permission));
    }

    public async Task<ProjectPermissionScope> GetProjectScopeAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var tenantRole = await ActiveTenantRoleAsync(tenantId, userId, cancellationToken);
        if (!tenantRole.HasValue)
        {
            return new ProjectPermissionScope(false, new HashSet<Guid>());
        }

        if (GrantsTenant(tenantRole.Value, permission))
        {
            return new ProjectPermissionScope(true, new HashSet<Guid>());
        }

        var now = clock.UtcNow;
        var memberships = await dbContext.ProjectMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Status == MembershipStatus.Active &&
                membership.StartsAt <= now &&
                (membership.EndsAt == null || membership.EndsAt > now))
            .Select(membership => new { membership.ProjectId, membership.RoleCode })
            .ToListAsync(cancellationToken);

        var projectIds = memberships
            .Where(membership => GrantsRole(membership.RoleCode, permission))
            .Select(membership => membership.ProjectId)
            .ToHashSet();
        return new ProjectPermissionScope(false, projectIds);
    }

    public async Task<ProjectAccessReadiness> GetProjectAccessReadinessAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var tenantIsActive = await dbContext.Tenants.AsNoTracking().AnyAsync(
            tenant => tenant.Id == tenantId && tenant.Status == TenantStatus.Active,
            cancellationToken);
        if (!tenantIsActive)
        {
            return new ProjectAccessReadiness(0, 0, 0);
        }

        var administratorCount = await dbContext.Users.AsNoTracking().CountAsync(
            user => user.TenantId == tenantId &&
                user.Status == UserAccountStatus.Active &&
                user.TenantRole == TenantRole.TenantAdministrator,
            cancellationToken);

        var now = clock.UtcNow;
        var activeMemberships =
            from membership in dbContext.ProjectMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on new { membership.TenantId, membership.UserId }
                equals new { user.TenantId, UserId = user.Id }
            where membership.TenantId == tenantId &&
                membership.ProjectId == projectId &&
                membership.Status == MembershipStatus.Active &&
                membership.StartsAt <= now &&
                (membership.EndsAt == null || membership.EndsAt > now) &&
                user.Status == UserAccountStatus.Active
            select new { membership.UserId, membership.RoleCode };

        var rows = await activeMemberships.ToListAsync(cancellationToken);
        return new ProjectAccessReadiness(
            administratorCount,
            rows.Count(item => string.Equals(item.RoleCode, "ProjectManager", StringComparison.OrdinalIgnoreCase)),
            rows.Where(item => OperationalRoles.Contains(item.RoleCode)).Select(item => item.UserId).Distinct().Count());
    }

    public async Task<EffectivePermissionPreview> PreviewProjectPermissionsAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string? proposedProjectRoleCode = null,
        IReadOnlyCollection<string>? operations = null,
        CancellationToken cancellationToken = default)
    {
        var evaluatedAt = clock.UtcNow;
        var context = await (
            from user in dbContext.Users.AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
            where user.TenantId == tenantId && user.Id == userId
            select new { UserStatus = user.Status, user.TenantRole, TenantStatus = tenant.Status })
            .SingleOrDefaultAsync(cancellationToken);

        var membership = await dbContext.ProjectMemberships.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.ProjectId == projectId)
            .Select(item => new { item.RoleCode, item.Status, item.StartsAt, item.EndsAt })
            .SingleOrDefaultAsync(cancellationToken);

        var requestedOperations = (operations is { Count: > 0 }
                ? operations
                : KnownOperations())
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(operation => operation, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var accountActive = context is not null &&
            context.UserStatus == UserAccountStatus.Active &&
            context.TenantStatus == TenantStatus.Active;
        var membershipActive = membership is not null &&
            membership.Status == MembershipStatus.Active &&
            membership.StartsAt <= evaluatedAt &&
            (membership.EndsAt is null || membership.EndsAt > evaluatedAt);
        var proposedRole = string.IsNullOrWhiteSpace(proposedProjectRoleCode)
            ? null
            : proposedProjectRoleCode.Trim();
        var effectiveRole = proposedRole ?? membership?.RoleCode;
        var roleIsEffective = proposedRole is not null || membershipActive;

        var decisions = requestedOperations.Select(operation =>
        {
            if (!accountActive)
            {
                return new EffectivePermissionDecision(
                    operation, false, "DefaultDeny", "None", "active tenant and account required",
                    context is null ? "account.not_found" :
                        context.TenantStatus != TenantStatus.Active ? "tenant.inactive" : "account.inactive",
                    null, "NotConfigured");
            }

            if (GrantsTenant(context!.TenantRole, operation))
            {
                return new EffectivePermissionDecision(
                    operation, true, $"TenantRole:{context.TenantRole}", "Tenant",
                    "active tenant and account", null, null, "NotConfigured");
            }

            if (roleIsEffective && effectiveRole is not null && GrantsRole(effectiveRole, operation))
            {
                return new EffectivePermissionDecision(
                    operation, true,
                    proposedRole is null ? $"ProjectRole:{effectiveRole}" : $"ProposedProjectRole:{effectiveRole}",
                    "Project",
                    proposedRole is null ? "active date-bounded membership" : "proposed role simulation; not persisted",
                    null,
                    proposedRole is null ? membership?.EndsAt : null,
                    "NotConfigured");
            }

            var denyReason = proposedRole is not null
                ? "permission.no_matching_grant"
                : membership is null
                ? "membership.missing"
                : !membershipActive
                    ? "membership.inactive_or_expired"
                    : "permission.no_matching_grant";
            return new EffectivePermissionDecision(
                operation, false, "DefaultDeny", "Project", "matching active grant required",
                denyReason, membership?.EndsAt, "NotConfigured");
        }).ToArray();

        return new EffectivePermissionPreview(
            userId,
            projectId,
            context is null ? "NotFound" :
                context.TenantStatus != TenantStatus.Active ? "TenantInactive" : context.UserStatus.ToString(),
            context?.TenantRole.ToString(),
            effectiveRole,
            PolicyVersion,
            evaluatedAt,
            decisions);
    }

    private static string[] KnownOperations() => ProjectRolePermissions.Values
        .SelectMany(permissions => permissions)
        .Where(permission => permission != "*")
        .Concat(AdministratorOnlyPermissions)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private async Task<TenantRole?> ActiveTenantRoleAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from user in dbContext.Users.AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
            where user.TenantId == tenantId &&
                user.Id == userId &&
                user.Status == UserAccountStatus.Active &&
                tenant.Status == TenantStatus.Active
            select (TenantRole?)user.TenantRole)
            .SingleOrDefaultAsync(cancellationToken);

    internal static bool GrantsTenant(TenantRole role, string permission) =>
        role == TenantRole.TenantAdministrator ||
        (role == TenantRole.PortfolioViewer &&
            (string.Equals(permission, "portfolio.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "projects.read-all", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "projects.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "actions.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "project-state.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "financial-state.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "commercial-state.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "supply.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "planning.measurement-items.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "planning.progress.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "planning.baselines.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "planning.milestones.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "technical.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "quality.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "hse.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "governance.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "platform.modules.read", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, "insights.view", StringComparison.OrdinalIgnoreCase)));

    internal static bool GrantsRole(string roleCode, string permission) =>
        ProjectRolePermissions.TryGetValue(roleCode, out var permissions) &&
        (permissions.Contains("*") || permissions.Contains(permission));
}
