using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class InvitationProjectAssignment
{
    private InvitationProjectAssignment()
    {
    }

    private InvitationProjectAssignment(Guid id, Guid invitationId, Guid tenantId, Guid projectId, string roleCode)
    {
        Id = id;
        InvitationId = invitationId;
        TenantId = tenantId;
        ProjectId = projectId;
        RoleCode = roleCode;
    }

    public Guid Id { get; private set; }

    public Guid InvitationId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string RoleCode { get; private set; } = string.Empty;

    public static InvitationProjectAssignment Create(
        Guid id,
        Guid invitationId,
        Guid tenantId,
        Guid projectId,
        string roleCode)
    {
        if (id == Guid.Empty || invitationId == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainRuleException("invitation.assignment.identity.required", "Assignment ids are required.");
        }

        if (!ProjectRoleCatalog.IsSupported(roleCode))
        {
            throw new DomainRuleException("membership.role.invalid", "Project role is not supported.");
        }

        return new InvitationProjectAssignment(id, invitationId, tenantId, projectId, roleCode.Trim());
    }
}
