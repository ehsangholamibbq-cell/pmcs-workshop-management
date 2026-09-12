using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;
using Pmcs.Modules.IdentityAccess.Services;

namespace Pmcs.Modules.IdentityAccess.Endpoints;

internal static class IdentityEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/session", SessionAsync);

        var group = endpoints.MapGroup("/api/v1/identity")
            .WithTags("Identity administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
        group.MapGet("/directory", DirectoryAsync);
        group.MapPost("/invitations", InviteAsync);
        group.MapPost("/invitations/{invitationId:guid}/resend", ResendAsync);
        group.MapPost("/invitations/{invitationId:guid}/revoke", RevokeInvitationAsync);
        group.MapPut("/users/{userId:guid}/status", ChangeUserStatusAsync);
        group.MapPut("/users/{userId:guid}/tenant-role", ChangeTenantRoleAsync);
        group.MapPut("/users/{userId:guid}/memberships/{projectId:guid}", UpsertMembershipAsync);
        group.MapDelete("/users/{userId:guid}/memberships/{projectId:guid}", RevokeMembershipAsync);
    }

    private static async Task<IResult> SessionAsync(
        HttpContext context,
        ICurrentActor actor,
        IdentityAccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var account = await (
            from user in dbContext.Users.AsNoTracking()
            join tenant in dbContext.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
            where user.Id == actor.UserId &&
                user.TenantId == actor.TenantId &&
                user.Status == UserAccountStatus.Active &&
                tenant.Status == TenantStatus.Active
            select new
            {
                user.DisplayName,
                user.Email,
                user.TenantRole,
                TenantName = tenant.Name
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return Results.Forbid();
        }

        return Results.Ok(new
        {
            actor.UserId,
            actor.TenantId,
            actor.DeviceId,
            account.DisplayName,
            account.Email,
            account.TenantRole,
            account.TenantName,
            authentication = string.Equals(
                context.User.Identity?.AuthenticationType,
                "PmcsDevelopment",
                StringComparison.Ordinal)
                ? "development-adapter"
                : "oidc-access-token"
        });
    }

    private static async Task<IResult> DirectoryAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        IdentityProvisioningOptions options,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var users = await dbContext.Users.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId)
            .OrderBy(item => item.DisplayName)
            .Select(item => new UserDirectoryResponse(
                item.Id,
                item.DisplayName,
                item.Email,
                item.TenantRole,
                item.Status,
                item.CreatedAt,
                item.Revision,
                Array.Empty<MembershipResponse>(),
                false))
            .ToListAsync(cancellationToken);
        var userIds = users.Select(item => item.Id).ToArray();
        var memberships = await dbContext.ProjectMemberships.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && userIds.Contains(item.UserId))
            .OrderBy(item => item.ProjectId)
            .Select(item => new MembershipDirectoryRecord(
                item.UserId,
                new MembershipResponse(item.Id, item.ProjectId, item.RoleCode, item.Status, item.StartsAt, item.EndsAt, item.Revision)))
            .ToListAsync(cancellationToken);
        var pendingSyncUsers = await dbContext.IdentityProviderOperations.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId &&
                item.Status != IdentityProviderOperationStatus.Completed &&
                item.Status != IdentityProviderOperationStatus.Failed)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var pendingSyncSet = pendingSyncUsers.ToHashSet();
        var byUser = memberships.GroupBy(item => item.UserId).ToDictionary(
            group => group.Key,
            group => group.Select(item => item.Membership).ToArray());
        users = users.Select(user => user with
        {
            Memberships = byUser.GetValueOrDefault(user.Id) ?? [],
            ProviderSyncPending = pendingSyncSet.Contains(user.Id)
        }).ToList();

        var invitations = await dbContext.UserInvitations.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new InvitationResponse(
                item.Id,
                item.DisplayName,
                item.Email,
                item.TenantRole,
                item.Status,
                item.CreatedAt,
                item.ExpiresAt,
                item.SentAt,
                item.NextAttemptAt,
                item.UserId,
                item.Attempts,
                item.LastErrorCode,
                Array.Empty<InvitationAssignmentResponse>()))
            .ToListAsync(cancellationToken);
        var invitationIds = invitations.Select(item => item.Id).ToArray();
        var assignments = await dbContext.InvitationProjectAssignments.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && invitationIds.Contains(item.InvitationId))
            .Select(item => new { item.InvitationId, item.ProjectId, item.RoleCode })
            .ToListAsync(cancellationToken);
        var assignmentsByInvitation = assignments.GroupBy(item => item.InvitationId).ToDictionary(
            group => group.Key,
            group => group.Select(item => new InvitationAssignmentResponse(item.ProjectId, item.RoleCode)).ToArray());
        invitations = invitations.Select(invitation => invitation with
        {
            Projects = assignmentsByInvitation.GetValueOrDefault(invitation.Id) ?? []
        }).ToList();

        return Results.Ok(new IdentityDirectoryResponse(
            options.Enabled,
            Enum.GetValues<TenantRole>(),
            ProjectRoleCatalog.Supported.Order(StringComparer.Ordinal).ToArray(),
            users,
            invitations));
    }

    private static async Task<IResult> InviteAsync(
        InviteUserRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectTenantDirectory projectDirectory,
        IdentityAccessDbContext dbContext,
        IdentityProvisioningOptions options,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        if (!options.Enabled)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Identity provisioning is not configured.",
                extensions: new Dictionary<string, object?> { ["code"] = "identity_provider.not_configured" });
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "identity.invitation.create",
            request,
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (request.Projects.Count > 50 || request.Projects.Select(item => item.ProjectId).Distinct().Count() != request.Projects.Count)
        {
            return Problem("invitation.projects.invalid", "Project assignments are invalid.");
        }

        var requestedProjectIds = request.Projects.Select(item => item.ProjectId).ToArray();
        var existingProjectIds = await projectDirectory.ExistingProjectIdsAsync(
            actor.TenantId,
            requestedProjectIds,
            cancellationToken);
        if (existingProjectIds.Count != requestedProjectIds.Length)
        {
            return Problem("invitation.project.not_found", "One or more projects do not belong to this tenant.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(item => item.TenantId == actor.TenantId && item.Email == email, cancellationToken))
        {
            return Results.Conflict(new { code = "user.email.duplicate" });
        }

        if (await dbContext.UserInvitations.AnyAsync(item =>
                item.TenantId == actor.TenantId &&
                item.Email == email &&
                (item.Status == UserInvitationStatus.Queued ||
                    item.Status == UserInvitationStatus.Processing ||
                    item.Status == UserInvitationStatus.RetryScheduled),
                cancellationToken))
        {
            return Results.Conflict(new { code = "invitation.email.pending" });
        }

        UserInvitation invitation;
        try
        {
            invitation = UserInvitation.Create(
                Guid.NewGuid(),
                actor.TenantId,
                request.DisplayName,
                request.Email,
                request.TenantRole,
                actor.UserId,
                clock.UtcNow,
                clock.UtcNow.AddSeconds(options.InvitationLifespanSeconds));
            dbContext.UserInvitations.Add(invitation);
            foreach (var assignment in request.Projects)
            {
                dbContext.InvitationProjectAssignments.Add(InvitationProjectAssignment.Create(
                    Guid.NewGuid(),
                    invitation.Id,
                    actor.TenantId,
                    assignment.ProjectId,
                    assignment.RoleCode));
            }
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        var response = InvitationResponse.From(invitation, request.Projects.Select(item =>
            new InvitationAssignmentResponse(item.ProjectId, item.RoleCode)).ToArray());
        await SaveMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "UserInvitationQueued",
            "UserInvitation",
            invitation.Id,
            "IdentityAccess.UserInvitationQueued",
            response,
            new Dictionary<string, object?>
            {
                ["email"] = invitation.Email,
                ["tenantRole"] = invitation.TenantRole.ToString(),
                ["projectCount"] = request.Projects.Count
            },
            StatusCodes.Status202Accepted,
            clock,
            cancellationToken);
        return Results.Accepted($"/api/v1/identity/invitations/{invitation.Id}", response);
    }

    private static async Task<IResult> ResendAsync(
        Guid invitationId,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        IdentityProvisioningOptions options,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        if (!options.Enabled)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "identity.invitation.resend",
            new { invitationId },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var invitation = await dbContext.UserInvitations.SingleOrDefaultAsync(
            item => item.Id == invitationId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        if (await dbContext.UserInvitations.AnyAsync(item =>
                item.Id != invitation.Id &&
                item.TenantId == actor.TenantId &&
                item.Email == invitation.Email &&
                (item.Status == UserInvitationStatus.Queued ||
                    item.Status == UserInvitationStatus.Processing ||
                    item.Status == UserInvitationStatus.RetryScheduled),
                cancellationToken))
        {
            return Results.Conflict(new { code = "invitation.email.pending" });
        }

        if (invitation.ProviderUserId.HasValue)
        {
            var providerUserId = invitation.ProviderUserId.Value;
            var cleanupStatuses = await dbContext.IdentityProviderOperations.AsNoTracking()
                .Where(item => item.TenantId == actor.TenantId &&
                    item.UserId == providerUserId &&
                    item.OperationType == IdentityProviderOperationType.Delete)
                .Select(item => item.Status)
                .ToListAsync(cancellationToken);
            if (cleanupStatuses.Any(status => status is
                    IdentityProviderOperationStatus.Queued or
                    IdentityProviderOperationStatus.Processing or
                    IdentityProviderOperationStatus.RetryScheduled))
            {
                return Results.Conflict(new { code = "invitation.cleanup.pending" });
            }

            if (cleanupStatuses.Contains(IdentityProviderOperationStatus.Completed))
            {
                try
                {
                    invitation.ReleaseDeletedProviderUser(providerUserId);
                }
                catch (DomainRuleException exception)
                {
                    return Problem(exception.Code, exception.Message);
                }
            }
        }

        try
        {
            invitation.Requeue(clock.UtcNow, clock.UtcNow.AddSeconds(options.InvitationLifespanSeconds));
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        var projects = await dbContext.InvitationProjectAssignments.AsNoTracking()
            .Where(item => item.InvitationId == invitationId)
            .Select(item => new InvitationAssignmentResponse(item.ProjectId, item.RoleCode))
            .ToArrayAsync(cancellationToken);
        var response = InvitationResponse.From(invitation, projects);
        await SaveMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "UserInvitationRequeued",
            "UserInvitation",
            invitation.Id,
            "IdentityAccess.UserInvitationRequeued",
            response,
            new Dictionary<string, object?> { ["email"] = invitation.Email },
            StatusCodes.Status202Accepted,
            clock,
            cancellationToken);
        return Results.Accepted(value: response);
    }

    private static async Task<IResult> RevokeInvitationAsync(
        Guid invitationId,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "identity.invitation.revoke",
            new { invitationId },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var invitation = await dbContext.UserInvitations.SingleOrDefaultAsync(
            item => item.Id == invitationId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (invitation is null)
        {
            return Results.NotFound();
        }

        try
        {
            invitation.Revoke();
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        Guid? cleanupOperationId = null;
        if (invitation.ProviderUserId.HasValue && !await dbContext.IdentityProviderOperations.AnyAsync(item =>
                item.UserId == invitation.ProviderUserId.Value &&
                item.OperationType == IdentityProviderOperationType.Delete &&
                (item.Status == IdentityProviderOperationStatus.Queued ||
                    item.Status == IdentityProviderOperationStatus.Processing ||
                    item.Status == IdentityProviderOperationStatus.RetryScheduled),
                cancellationToken))
        {
            var cleanup = IdentityProviderOperation.Queue(
                Guid.NewGuid(),
                invitation.TenantId,
                invitation.ProviderUserId.Value,
                IdentityProviderOperationType.Delete,
                clock.UtcNow);
            dbContext.IdentityProviderOperations.Add(cleanup);
            cleanupOperationId = cleanup.Id;
        }

        var response = new { invitation.Id, invitation.Status, invitation.Revision, cleanupOperationId };
        await SaveMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "UserInvitationRevoked",
            "UserInvitation",
            invitation.Id,
            "IdentityAccess.UserInvitationRevoked",
            response,
            new Dictionary<string, object?> { ["email"] = invitation.Email },
            StatusCodes.Status200OK,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ChangeUserStatusAsync(
        Guid userId,
        ChangeUserStatusRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        if (userId == actor.UserId && request.Status is not UserAccountStatus.Active)
        {
            return Problem("user.self_lockout.denied", "Administrators cannot suspend or deactivate their own account.");
        }

        if (request.Status is UserAccountStatus.Invited)
        {
            return Problem("user.status.invalid", "Invited is not an administrative account state.");
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "identity.user.status",
            new { userId, request.Status },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var previousStatus = user.Status;

        if (user.TenantRole == TenantRole.TenantAdministrator && request.Status != UserAccountStatus.Active &&
            !await HasAnotherActiveAdministratorAsync(dbContext, actor.TenantId, userId, cancellationToken))
        {
            return Problem("tenant.last_administrator.required", "The last active administrator cannot be disabled.");
        }

        try
        {
            switch (request.Status)
            {
                case UserAccountStatus.Active:
                    user.Reactivate(clock.UtcNow);
                    break;
                case UserAccountStatus.Suspended:
                    user.Suspend(clock.UtcNow);
                    break;
                case UserAccountStatus.Deactivated:
                    user.Deactivate(clock.UtcNow);
                    break;
                default:
                    return Problem("user.status.invalid", "User status is invalid.");
            }
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        var operation = IdentityProviderOperation.Queue(
            Guid.NewGuid(),
            actor.TenantId,
            user.Id,
            request.Status == UserAccountStatus.Active
                ? IdentityProviderOperationType.Enable
                : IdentityProviderOperationType.DisableAndLogout,
            clock.UtcNow);
        dbContext.IdentityProviderOperations.Add(operation);
        var response = new UserStatusResponse(user.Id, user.Status, user.Revision, true);
        await SaveMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "UserStatusChanged",
            "UserAccount",
            user.Id,
            "IdentityAccess.UserStatusChanged",
            response,
            new Dictionary<string, object?>
            {
                ["previousStatus"] = previousStatus.ToString(),
                ["status"] = user.Status.ToString(),
                ["providerOperationId"] = operation.Id
            },
            StatusCodes.Status202Accepted,
            clock,
            cancellationToken);
        return Results.Accepted(value: response);
    }

    private static async Task<IResult> ChangeTenantRoleAsync(
        Guid userId,
        ChangeTenantRoleRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "identity.user.tenant-role",
            new { userId, request.TenantRole },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        var previousTenantRole = user.TenantRole;

        if (user.TenantRole == TenantRole.TenantAdministrator && request.TenantRole != TenantRole.TenantAdministrator &&
            !await HasAnotherActiveAdministratorAsync(dbContext, actor.TenantId, userId, cancellationToken))
        {
            return Problem("tenant.last_administrator.required", "The last active administrator cannot be demoted.");
        }

        try
        {
            user.ChangeTenantRole(request.TenantRole);
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        var response = new { user.Id, user.TenantRole, user.Revision };
        await SaveMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "UserTenantRoleChanged",
            "UserAccount",
            user.Id,
            "IdentityAccess.UserTenantRoleChanged",
            response,
            new Dictionary<string, object?>
            {
                ["previousTenantRole"] = previousTenantRole.ToString(),
                ["tenantRole"] = user.TenantRole.ToString()
            },
            StatusCodes.Status200OK,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpsertMembershipAsync(
        Guid userId,
        Guid projectId,
        ChangeMembershipRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectTenantDirectory projectDirectory,
        IdentityAccessDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "identity.membership.upsert",
            new { userId, projectId, request.RoleCode },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (!await dbContext.Users.AnyAsync(item =>
                item.Id == userId && item.TenantId == actor.TenantId && item.Status != UserAccountStatus.Deactivated,
                cancellationToken))
        {
            return Results.NotFound();
        }

        var projects = await projectDirectory.ExistingProjectIdsAsync(actor.TenantId, [projectId], cancellationToken);
        if (!projects.Contains(projectId))
        {
            return Problem("membership.project.not_found", "Project does not belong to this tenant.");
        }

        ProjectMembership membership;
        string? previousRoleCode = null;
        MembershipStatus? previousMembershipStatus = null;
        try
        {
            membership = await dbContext.ProjectMemberships.SingleOrDefaultAsync(item =>
                item.TenantId == actor.TenantId && item.UserId == userId && item.ProjectId == projectId,
                cancellationToken) ?? ProjectMembership.Assign(
                    Guid.NewGuid(), actor.TenantId, projectId, userId, request.RoleCode, clock.UtcNow);
            if (dbContext.Entry(membership).State == EntityState.Detached)
            {
                dbContext.ProjectMemberships.Add(membership);
            }
            else
            {
                previousRoleCode = membership.RoleCode;
                previousMembershipStatus = membership.Status;
                membership.ChangeRole(request.RoleCode);
                membership.Activate(clock.UtcNow);
            }
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        var response = new MembershipResponse(
            membership.Id, membership.ProjectId, membership.RoleCode, membership.Status,
            membership.StartsAt, membership.EndsAt, membership.Revision);
        await SaveMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "ProjectMembershipChanged",
            "ProjectMembership",
            membership.Id,
            "IdentityAccess.ProjectMembershipChanged",
            response,
            new Dictionary<string, object?>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["previousRoleCode"] = previousRoleCode,
                ["previousStatus"] = previousMembershipStatus?.ToString(),
                ["roleCode"] = membership.RoleCode
            },
            StatusCodes.Status200OK,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> RevokeMembershipAsync(
        Guid userId,
        Guid projectId,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await RequireAdministratorAsync(actor, permissionService, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "identity.membership.revoke",
            new { userId, projectId },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var membership = await dbContext.ProjectMemberships.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == userId && item.ProjectId == projectId,
            cancellationToken);
        if (membership is null)
        {
            return Results.NotFound();
        }

        membership.Revoke(clock.UtcNow);
        var response = new MembershipResponse(
            membership.Id, membership.ProjectId, membership.RoleCode, membership.Status,
            membership.StartsAt, membership.EndsAt, membership.Revision);
        await SaveMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "ProjectMembershipRevoked",
            "ProjectMembership",
            membership.Id,
            "IdentityAccess.ProjectMembershipRevoked",
            response,
            new Dictionary<string, object?> { ["userId"] = userId, ["projectId"] = projectId },
            StatusCodes.Status200OK,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult?> RequireAdministratorAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        return await permissionService.HasTenantPermissionAsync(
            actor.TenantId,
            actor.UserId,
            "identity.users.manage",
            cancellationToken)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static Task<bool> HasAnotherActiveAdministratorAsync(
        IdentityAccessDbContext dbContext,
        Guid tenantId,
        Guid excludedUserId,
        CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(item =>
            item.TenantId == tenantId &&
            item.Id != excludedUserId &&
            item.TenantRole == TenantRole.TenantAdministrator &&
            item.Status == UserAccountStatus.Active,
            cancellationToken);

    private static async Task<PreparedIdempotency> PrepareIdempotencyAsync<TRequest>(
        HttpContext context,
        Guid tenantId,
        string operation,
        TRequest request,
        IIdempotencyStore store,
        CancellationToken cancellationToken)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new PreparedIdempotency(
                string.Empty,
                string.Empty,
                operation,
                Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Idempotency-Key is required.",
                    extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }

        var requestJson = JsonSerializer.Serialize(request, SerializerOptions);
        var hash = RequestHash.Create(requestJson);
        var replay = await store.FindAsync(tenantId, key, operation, hash, cancellationToken);
        return new PreparedIdempotency(
            key,
            hash,
            operation,
            replay is null
                ? null
                : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static async Task SaveMutationAsync<TResponse>(
        IdentityAccessDbContext dbContext,
        ITransactionalSideEffectWriter sideEffects,
        ICurrentActor actor,
        HttpContext context,
        PreparedIdempotency idempotency,
        string auditEvent,
        string resourceType,
        Guid resourceId,
        string outboxEvent,
        TResponse response,
        IReadOnlyDictionary<string, object?> auditData,
        int statusCode,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    null,
                    actor.UserId,
                    auditEvent,
                    resourceType,
                    resourceId.ToString(),
                    clock.UtcNow,
                    auditData,
                    context.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    null,
                    outboxEvent,
                    1,
                    clock.UtcNow,
                    responseJson,
                    context.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    idempotency.Operation,
                    idempotency.RequestHash,
                    statusCode,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static IResult Problem(string code, string title) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: title,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record PreparedIdempotency(
        string Key,
        string RequestHash,
        string Operation,
        IResult? Result);

    private sealed record MembershipDirectoryRecord(Guid UserId, MembershipResponse Membership);
}

internal sealed record IdentityDirectoryResponse(
    bool ProvisioningEnabled,
    IReadOnlyCollection<TenantRole> TenantRoles,
    IReadOnlyCollection<string> ProjectRoles,
    IReadOnlyCollection<UserDirectoryResponse> Users,
    IReadOnlyCollection<InvitationResponse> Invitations);

internal sealed record UserDirectoryResponse(
    Guid Id,
    string DisplayName,
    string Email,
    TenantRole TenantRole,
    UserAccountStatus Status,
    DateTimeOffset CreatedAt,
    long Revision,
    IReadOnlyCollection<MembershipResponse> Memberships,
    bool ProviderSyncPending);

internal sealed record MembershipResponse(
    Guid Id,
    Guid ProjectId,
    string RoleCode,
    MembershipStatus Status,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    long Revision);

internal sealed record InvitationResponse(
    Guid Id,
    string DisplayName,
    string Email,
    TenantRole TenantRole,
    UserInvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? NextAttemptAt,
    Guid? UserId,
    int Attempts,
    string? LastErrorCode,
    IReadOnlyCollection<InvitationAssignmentResponse> Projects)
{
    public static InvitationResponse From(
        UserInvitation invitation,
        IReadOnlyCollection<InvitationAssignmentResponse> projects) =>
        new(
            invitation.Id,
            invitation.DisplayName,
            invitation.Email,
            invitation.TenantRole,
            invitation.Status,
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.SentAt,
            invitation.NextAttemptAt,
            invitation.UserId,
            invitation.Attempts,
            invitation.LastErrorCode,
            projects);
}

internal sealed record InvitationAssignmentResponse(Guid ProjectId, string RoleCode);

internal sealed record UserStatusResponse(Guid Id, UserAccountStatus Status, long Revision, bool ProviderSyncPending);

internal sealed record InviteUserRequest(
    string DisplayName,
    string Email,
    TenantRole TenantRole,
    IReadOnlyCollection<InvitationProjectRequest> Projects);

internal sealed record InvitationProjectRequest(Guid ProjectId, string RoleCode);

internal sealed record ChangeUserStatusRequest(UserAccountStatus Status);

internal sealed record ChangeTenantRoleRequest(TenantRole TenantRole);

internal sealed record ChangeMembershipRequest(string RoleCode);
