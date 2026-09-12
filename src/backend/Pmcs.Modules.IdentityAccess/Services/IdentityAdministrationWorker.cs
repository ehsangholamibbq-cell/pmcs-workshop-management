using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed partial class IdentityAdministrationWorker(
    IServiceScopeFactory scopeFactory,
    IdentityProvisioningOptions options,
    ILogger<IdentityAdministrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        await RecoverInterruptedAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollSeconds));
        do
        {
            try
            {
                await ProcessOneInvitationAsync(stoppingToken);
                await ProcessOneOperationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogCycleFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var invitations = await dbContext.UserInvitations
            .Where(item => item.Status == UserInvitationStatus.Processing)
            .ToListAsync(cancellationToken);
        foreach (var invitation in invitations)
        {
            invitation.ScheduleRetry("identity_provider.interrupted", clock.UtcNow);
        }

        var operations = await dbContext.IdentityProviderOperations
            .Where(item => item.Status == IdentityProviderOperationStatus.Processing)
            .ToListAsync(cancellationToken);
        foreach (var operation in operations)
        {
            operation.ScheduleRetry("identity_provider.interrupted", clock.UtcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessOneInvitationAsync(CancellationToken cancellationToken)
    {
        Guid? invitationId;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;
            var invitation = await dbContext.UserInvitations
                .Where(item =>
                    (item.Status == UserInvitationStatus.Queued || item.Status == UserInvitationStatus.RetryScheduled) &&
                    item.NextAttemptAt <= now)
                .OrderBy(item => item.NextAttemptAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (invitation is null)
            {
                return;
            }

            if (invitation.ExpiresAt <= now)
            {
                invitation.Expire();
                await QueueIdentityCleanupAsync(dbContext, invitation, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            invitation.BeginAttempt(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            invitationId = invitation.Id;
        }

        try
        {
            await CompleteInvitationAsync(invitationId.Value, cancellationToken);
        }
        catch (IdentityProviderAdministrationException exception)
        {
            await RecordInvitationFailureAsync(invitationId.Value, exception.Code, exception.IsTransient, cancellationToken);
        }
        catch (Exception exception)
        {
            LogInvitationFailed(logger, invitationId.Value, exception);
            await RecordInvitationFailureAsync(
                invitationId.Value,
                "identity_provider.processing_failed",
                true,
                cancellationToken);
        }
    }

    private async Task CompleteInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        ProvisionIdentityResult result;
        ProvisionIdentityRequest request;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            var invitation = await dbContext.UserInvitations
                .AsNoTracking()
                .SingleAsync(item => item.Id == invitationId, cancellationToken);
            request = new ProvisionIdentityRequest(
                invitation.Id,
                invitation.TenantId,
                invitation.DisplayName,
                invitation.Email);
            var administration = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdministration>();
            result = await administration.EnsureUserAsync(request, cancellationToken);
        }

        await using (var bindingScope = scopeFactory.CreateAsyncScope())
        {
            var bindingDb = bindingScope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            var bindingClock = bindingScope.ServiceProvider.GetRequiredService<IClock>();
            var invitationToBind = await bindingDb.UserInvitations
                .SingleAsync(item => item.Id == invitationId, cancellationToken);
            if (invitationToBind.Status == UserInvitationStatus.Sent)
            {
                return;
            }

            invitationToBind.BindProviderUser(result.UserId);
            if (invitationToBind.Status is UserInvitationStatus.Revoked or UserInvitationStatus.Expired)
            {
                await QueueIdentityCleanupAsync(bindingDb, invitationToBind, bindingClock.UtcNow, cancellationToken);
                await bindingDb.SaveChangesAsync(cancellationToken);
                return;
            }

            if (invitationToBind.Status != UserInvitationStatus.Processing)
            {
                throw new IdentityProviderAdministrationException(
                    "identity_provider.invitation_state.conflict",
                    true,
                    "Invitation state changed while provisioning was in progress.");
            }

            await bindingDb.SaveChangesAsync(cancellationToken);
        }

        await using (var deliveryScope = scopeFactory.CreateAsyncScope())
        {
            var administration = deliveryScope.ServiceProvider.GetRequiredService<IIdentityProviderAdministration>();
            await administration.SendInvitationAsync(result.UserId, cancellationToken);
        }

        await using var completionScope = scopeFactory.CreateAsyncScope();
        var completionDb = completionScope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
        var clock = completionScope.ServiceProvider.GetRequiredService<IClock>();
        var sideEffects = completionScope.ServiceProvider.GetRequiredService<ITransactionalSideEffectWriter>();
        var invitationToComplete = await completionDb.UserInvitations
            .SingleAsync(item => item.Id == invitationId, cancellationToken);
        if (invitationToComplete.Status == UserInvitationStatus.Sent)
        {
            return;
        }

        if (invitationToComplete.Status is UserInvitationStatus.Revoked or UserInvitationStatus.Expired)
        {
            await QueueIdentityCleanupAsync(completionDb, invitationToComplete, clock.UtcNow, cancellationToken);
            await completionDb.SaveChangesAsync(cancellationToken);
            return;
        }

        if (invitationToComplete.Status != UserInvitationStatus.Processing)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.invitation_state.conflict",
                true,
                "Invitation state changed while provisioning was in progress.");
        }

        var assignments = await completionDb.InvitationProjectAssignments
            .Where(item => item.InvitationId == invitationId)
            .ToListAsync(cancellationToken);
        var existingByEmail = await completionDb.Users.SingleOrDefaultAsync(
            item => item.TenantId == invitationToComplete.TenantId && item.Email == invitationToComplete.Email,
            cancellationToken);
        if (existingByEmail is not null && existingByEmail.Id != result.UserId)
        {
            throw new IdentityProviderAdministrationException(
                "identity_provider.email.already_owned",
                false,
                "Email is already bound to another PMCS account.");
        }

        if (existingByEmail is null)
        {
            completionDb.Users.Add(UserAccount.Create(
                result.UserId,
                invitationToComplete.TenantId,
                invitationToComplete.DisplayName,
                invitationToComplete.Email,
                invitationToComplete.TenantRole,
                clock.UtcNow));
        }

        var projectIds = assignments.Select(item => item.ProjectId).ToArray();
        var existingMemberships = await completionDb.ProjectMemberships
            .Where(item => item.TenantId == invitationToComplete.TenantId &&
                item.UserId == result.UserId && projectIds.Contains(item.ProjectId))
            .Select(item => item.ProjectId)
            .ToListAsync(cancellationToken);
        foreach (var assignment in assignments.Where(item => !existingMemberships.Contains(item.ProjectId)))
        {
            completionDb.ProjectMemberships.Add(ProjectMembership.Assign(
                Guid.NewGuid(),
                invitationToComplete.TenantId,
                assignment.ProjectId,
                result.UserId,
                assignment.RoleCode,
                clock.UtcNow));
        }

        invitationToComplete.MarkSent(result.UserId, clock.UtcNow);
        var response = JsonSerializer.Serialize(new
        {
            invitationId,
            userId = result.UserId,
            invitationToComplete.Email,
            invitationToComplete.TenantRole,
            projectIds
        });
        await using var transaction = await completionDb.Database.BeginTransactionAsync(cancellationToken);
        await completionDb.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            completionDb.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    invitationToComplete.TenantId,
                    null,
                    invitationToComplete.CreatedBy,
                    "UserInvitationProvisioned",
                    "UserInvitation",
                    invitationId.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["userId"] = result.UserId,
                        ["email"] = invitationToComplete.Email,
                        ["projectCount"] = projectIds.Length
                    }),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    invitationToComplete.TenantId,
                    null,
                    "IdentityAccess.UserInvitationProvisioned",
                    1,
                    clock.UtcNow,
                    response,
                    null),
                new IdempotencyReceipt(
                    invitationToComplete.TenantId,
                    $"identity-worker:{invitationId:N}",
                    "identity.invitation.provision",
                    RequestHash.Create(invitationId.ToString()),
                    200,
                    response,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(30))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordInvitationFailureAsync(
        Guid invitationId,
        string code,
        bool isTransient,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var invitation = await dbContext.UserInvitations.SingleAsync(item => item.Id == invitationId, cancellationToken);
        if (invitation.Status is UserInvitationStatus.Revoked or UserInvitationStatus.Expired or UserInvitationStatus.Sent)
        {
            if (invitation.Status is UserInvitationStatus.Revoked or UserInvitationStatus.Expired)
            {
                await QueueIdentityCleanupAsync(dbContext, invitation, clock.UtcNow, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        if (isTransient && invitation.Attempts < options.MaximumAttempts && clock.UtcNow < invitation.ExpiresAt)
        {
            var delayMinutes = Math.Min(30, 1 << Math.Min(invitation.Attempts, 5));
            invitation.ScheduleRetry(code, clock.UtcNow.AddMinutes(delayMinutes));
        }
        else
        {
            invitation.Fail(code);
            await QueueIdentityCleanupAsync(dbContext, invitation, clock.UtcNow, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessOneOperationAsync(CancellationToken cancellationToken)
    {
        Guid? operationId;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var operation = await dbContext.IdentityProviderOperations
                .Where(item =>
                    (item.Status == IdentityProviderOperationStatus.Queued ||
                        item.Status == IdentityProviderOperationStatus.RetryScheduled) &&
                    item.NextAttemptAt <= clock.UtcNow)
                .OrderBy(item => item.NextAttemptAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (operation is null)
            {
                return;
            }

            operation.BeginAttempt();
            await dbContext.SaveChangesAsync(cancellationToken);
            operationId = operation.Id;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var operation = await dbContext.IdentityProviderOperations
                .SingleAsync(item => item.Id == operationId, cancellationToken);
            var desiredOperation = operation.OperationType;
            if (desiredOperation != IdentityProviderOperationType.Delete)
            {
                var userStatus = await dbContext.Users.AsNoTracking()
                    .Where(item => item.Id == operation.UserId && item.TenantId == operation.TenantId)
                    .Select(item => (UserAccountStatus?)item.Status)
                    .SingleOrDefaultAsync(cancellationToken);
                desiredOperation = userStatus == UserAccountStatus.Active
                    ? IdentityProviderOperationType.Enable
                    : IdentityProviderOperationType.DisableAndLogout;
            }
            var administration = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdministration>();
            await administration.ApplyAsync(operation.UserId, desiredOperation, cancellationToken);
            operation.Complete(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (IdentityProviderAdministrationException exception)
        {
            await RecordOperationFailureAsync(operationId.Value, exception.Code, exception.IsTransient, cancellationToken);
        }
        catch (Exception exception)
        {
            LogOperationFailed(logger, operationId.Value, exception);
            await RecordOperationFailureAsync(
                operationId.Value,
                "identity_provider.processing_failed",
                true,
                cancellationToken);
        }
    }

    private async Task RecordOperationFailureAsync(
        Guid operationId,
        string code,
        bool isTransient,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var operation = await dbContext.IdentityProviderOperations.SingleAsync(item => item.Id == operationId, cancellationToken);
        if (isTransient && operation.Attempts < options.MaximumAttempts)
        {
            var delayMinutes = Math.Min(30, 1 << Math.Min(operation.Attempts, 5));
            operation.ScheduleRetry(code, clock.UtcNow.AddMinutes(delayMinutes));
        }
        else
        {
            operation.Fail(code);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task QueueIdentityCleanupAsync(
        IdentityAccessDbContext dbContext,
        UserInvitation invitation,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (!invitation.ProviderUserId.HasValue)
        {
            return;
        }

        var providerUserId = invitation.ProviderUserId.Value;
        var alreadyQueued = await dbContext.IdentityProviderOperations.AnyAsync(item =>
            item.UserId == providerUserId &&
            item.OperationType == IdentityProviderOperationType.Delete &&
            (item.Status == IdentityProviderOperationStatus.Queued ||
                item.Status == IdentityProviderOperationStatus.Processing ||
                item.Status == IdentityProviderOperationStatus.RetryScheduled),
            cancellationToken);
        if (!alreadyQueued)
        {
            dbContext.IdentityProviderOperations.Add(IdentityProviderOperation.Queue(
                Guid.NewGuid(),
                invitation.TenantId,
                providerUserId,
                IdentityProviderOperationType.Delete,
                at));
        }
    }

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Identity administration worker is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "Identity administration worker cycle failed.")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Error, Message = "Identity invitation {InvitationId} failed unexpectedly.")]
    private static partial void LogInvitationFailed(ILogger logger, Guid invitationId, Exception exception);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Error, Message = "Identity operation {OperationId} failed unexpectedly.")]
    private static partial void LogOperationFailed(ILogger logger, Guid operationId, Exception exception);
}
