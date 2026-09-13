using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ProjectPermissionService(
    IdentityAccessDbContext dbContext,
    IClock clock) : IProjectPermissionService
{
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
                "finance.records.capture",
                "finance.records.submit",
                "financial-state.read",
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
                "finance.records.capture",
                "finance.records.submit",
                "finance.records.review",
                "financial-state.read",
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
                "evidence.read", "evidence.upload", "project-state.read"
            },
            ["ProjectController"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "projects.read", "project-state.read", "planning.measurement-items.read",
                "planning.progress.read", "planning.baselines.read", "planning.milestones.read",
                "technical.read", "financial-state.read", "commercial-state.read", "supply.read",
                "quality.read", "hse.read", "evidence.read", "actions.read", "actions.create",
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
                string.Equals(permission, "insights.view", StringComparison.OrdinalIgnoreCase)));

    internal static bool GrantsRole(string roleCode, string permission) =>
        ProjectRolePermissions.TryGetValue(roleCode, out var permissions) &&
        (permissions.Contains("*") || permissions.Contains(permission));
}
