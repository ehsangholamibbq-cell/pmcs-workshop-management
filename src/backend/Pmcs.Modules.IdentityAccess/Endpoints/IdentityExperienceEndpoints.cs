using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.BuildingBlocks.Web;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Endpoints;

internal static partial class IdentityEndpoints
{
    private static void MapIdentityExperienceEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/public/login-experience", GetPublicLoginExperienceAsync)
            .WithTags("Login experience")
            .AllowAnonymous();
        endpoints.MapGet(
                "/api/v1/public/login-experience/assets/{slot}",
                GetPublicLoginAssetAsync)
            .WithTags("Login experience")
            .AllowAnonymous();

        endpoints.MapGet("/api/v1/member-profile", GetSelfProfileAsync)
            .WithTags("Member profiles");
        endpoints.MapPut("/api/v1/member-profile", UpdateSelfProfileAsync)
            .WithTags("Member profiles");
        endpoints.MapGet("/api/v1/member-profiles/{userId:guid}", GetMemberProfileAsync)
            .WithTags("Member profiles");
        endpoints.MapGet("/api/v1/member-profiles/{userId:guid}/avatar", GetMemberAvatarAsync)
            .WithTags("Member profiles");
        endpoints.MapPut(
                "/api/v1/member-profiles/{userId:guid}/directory",
                UpdateMemberDirectoryProfileAsync)
            .WithTags("Member profiles")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);

        var loginAdministration = endpoints.MapGroup("/api/v1/identity/login-experiences")
            .WithTags("Login experience administration")
            .RequireRateLimiting(ApiRateLimitPolicies.IdentityAdministration);
        loginAdministration.MapGet("", ListLoginExperiencesAsync);
        loginAdministration.MapPost("", CreateLoginExperienceDraftAsync);
        loginAdministration.MapPost("/{versionNumber:int}/publish", PublishLoginExperienceAsync);
        loginAdministration.MapPost("/{versionNumber:int}/rollback", RollbackLoginExperienceAsync);
    }

    private static async Task<IResult> GetPublicLoginExperienceAsync(
        Guid tenantId,
        HttpContext context,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return Results.BadRequest(new { code = "login-experience.tenant.required" });
        }

        context.Response.Headers.CacheControl = "no-store";
        var experience = await dbContext.LoginExperiences.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.TenantId == tenantId && item.Status == LoginExperienceStatus.Published,
                cancellationToken);
        if (experience is null)
        {
            return Results.Ok(PublicLoginExperienceResponse.Fallback());
        }

        var documentIds = new[] { experience.LogoDocumentId, experience.HeroDocumentId }
            .OfType<Guid>()
            .Where(id => id != Guid.Empty)
            .ToArray();
        var released = await documentDirectory.FindReleasedAsync(
            tenantId,
            documentIds,
            cancellationToken);
        var usable = released
            .Where(item => item.OwnerType == DocumentOwnerType.LoginExperience &&
                item.OwnerId == experience.Id && item.ContentType.StartsWith("image/", StringComparison.Ordinal))
            .Select(item => item.Id)
            .ToHashSet();

        return Results.Ok(PublicLoginExperienceResponse.From(
            experience,
            experience.LogoDocumentId.HasValue && usable.Contains(experience.LogoDocumentId.Value),
            experience.HeroDocumentId.HasValue && usable.Contains(experience.HeroDocumentId.Value)));
    }

    private static async Task<IResult> GetPublicLoginAssetAsync(
        string slot,
        Guid tenantId,
        int version,
        HttpContext context,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || version <= 0 ||
            !string.Equals(slot, "logo", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(slot, "hero", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { code = "login-experience.asset.request.invalid" });
        }

        var experience = await dbContext.LoginExperiences.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.VersionNumber == version &&
                item.Status == LoginExperienceStatus.Published,
                cancellationToken);
        if (experience is null)
        {
            return Results.NotFound();
        }

        var documentId = string.Equals(slot, "logo", StringComparison.OrdinalIgnoreCase)
            ? experience.LogoDocumentId
            : experience.HeroDocumentId;
        if (!documentId.HasValue)
        {
            return Results.NotFound();
        }

        ReleasedDocumentContent? content;
        try
        {
            content = await documentDirectory.ReadReleasedAsync(
                tenantId,
                documentId.Value,
                DocumentOwnerType.LoginExperience,
                experience.Id,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Login asset integrity verification failed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "login-experience.asset.integrity_failed"
                });
        }

        if (content is null || !content.Document.ContentType.StartsWith("image/", StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        context.Response.Headers.ETag = $"\"{content.Document.Sha256}\"";
        return Results.File(content.Bytes, content.Document.ContentType, enableRangeProcessing: false);
    }

    private static async Task<IResult> GetSelfProfileAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "member-profile.read-self",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var profile = await LoadProfileResponseAsync(
            dbContext,
            actor.TenantId,
            actor.UserId,
            cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static async Task<IResult> GetMemberProfileAsync(
        Guid userId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var profile = await LoadProfileResponseAsync(
            dbContext,
            actor.TenantId,
            userId,
            cancellationToken);
        if (profile is null)
        {
            return Results.NotFound();
        }

        if (!await CanReadProfileAsync(
                actor,
                userId,
                permissionService,
                dbContext,
                clock.UtcNow,
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return Results.Ok(profile);
    }

    private static async Task<IResult> GetMemberAvatarAsync(
        Guid userId,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await CanReadProfileAsync(
                actor,
                userId,
                permissionService,
                dbContext,
                clock.UtcNow,
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var documentId = await dbContext.MemberProfiles.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.UserId == userId)
            .Select(item => item.AvatarDocumentId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!documentId.HasValue)
        {
            return Results.NotFound();
        }

        ReleasedDocumentContent? content;
        try
        {
            content = await documentDirectory.ReadReleasedAsync(
                actor.TenantId,
                documentId.Value,
                DocumentOwnerType.MemberProfile,
                userId,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Profile image integrity verification failed.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "member-profile.avatar.integrity_failed"
                });
        }

        if (content is null || content.Document.Classification != DocumentClassification.Confidential ||
            !content.Document.ContentType.StartsWith("image/", StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "private, max-age=300";
        context.Response.Headers.ETag = $"\"{content.Document.Sha256}\"";
        return Results.File(content.Bytes, content.Document.ContentType, enableRangeProcessing: false);
    }

    private static async Task<IResult> UpdateSelfProfileAsync(
        UpdateSelfProfileRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "member-profile.update-self",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "member-profile.update-self",
            request,
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.Id == actor.UserId,
            cancellationToken);
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == actor.UserId,
            cancellationToken);
        if (user is null || profile is null)
        {
            return Results.NotFound();
        }

        var avatarError = await ValidateProfileAvatarAsync(
            documentDirectory,
            actor.TenantId,
            actor.UserId,
            request.AvatarDocumentId,
            cancellationToken);
        if (avatarError is not null)
        {
            return Results.UnprocessableEntity(new { code = avatarError });
        }

        if (user.Revision != request.BaseUserRevision || profile.Revision != request.BaseProfileRevision)
        {
            return Results.Conflict(new
            {
                code = "member-profile.revision.conflict",
                currentUserRevision = user.Revision,
                currentProfileRevision = profile.Revision
            });
        }

        var changedAt = clock.UtcNow;
        try
        {
            user.ChangeDisplayName(request.BaseUserRevision, request.DisplayName);
            profile.UpdateSelf(
                request.BaseProfileRevision,
                request.JobTitle,
                request.WorkPhone,
                request.AvatarDocumentId,
                request.AvatarCrop?.ToDomain(),
                actor.UserId,
                changedAt);
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        var response = MemberProfileResponse.From(user, profile);
        await SaveExperienceMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "MemberProfileUpdatedByMember",
            "MemberProfile",
            profile.UserId,
            "identity.member-profile.updated.v1",
            response,
            new { userId = profile.UserId, profileRevision = profile.Revision, userRevision = user.Revision },
            new Dictionary<string, object?>
            {
                ["selfService"] = true,
                ["avatarAssociated"] = profile.AvatarDocumentId.HasValue,
                ["profileRevision"] = profile.Revision,
                ["userRevision"] = user.Revision
            },
            StatusCodes.Status200OK,
            changedAt,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateMemberDirectoryProfileAsync(
        Guid userId,
        UpdateDirectoryProfileRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "member-profile.manage-directory",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "member-profile.update-directory",
            new { userId, request },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.Id == userId,
            cancellationToken);
        var profile = await dbContext.MemberProfiles.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.UserId == userId,
            cancellationToken);
        if (user is null || profile is null)
        {
            return Results.NotFound();
        }

        var avatarError = await ValidateProfileAvatarAsync(
            documentDirectory,
            actor.TenantId,
            userId,
            request.AvatarDocumentId,
            cancellationToken);
        if (avatarError is not null)
        {
            return Results.UnprocessableEntity(new { code = avatarError });
        }

        if (user.Revision != request.BaseUserRevision || profile.Revision != request.BaseProfileRevision)
        {
            return Results.Conflict(new
            {
                code = "member-profile.revision.conflict",
                currentUserRevision = user.Revision,
                currentProfileRevision = profile.Revision
            });
        }

        var changedAt = clock.UtcNow;
        try
        {
            user.ChangeDisplayName(request.BaseUserRevision, request.DisplayName);
            profile.UpdateDirectory(
                request.BaseProfileRevision,
                request.JobTitle,
                request.OrganizationUnit,
                request.WorkPhone,
                request.AvatarDocumentId,
                request.AvatarCrop?.ToDomain(),
                actor.UserId,
                changedAt);
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        var response = MemberProfileResponse.From(user, profile);
        await SaveExperienceMutationAsync(
            dbContext,
            sideEffects,
            actor,
            context,
            idempotency,
            "MemberProfileUpdatedByAdministrator",
            "MemberProfile",
            profile.UserId,
            "identity.member-profile.updated.v1",
            response,
            new { userId = profile.UserId, profileRevision = profile.Revision, userRevision = user.Revision },
            new Dictionary<string, object?>
            {
                ["selfService"] = false,
                ["avatarAssociated"] = profile.AvatarDocumentId.HasValue,
                ["profileRevision"] = profile.Revision,
                ["userRevision"] = user.Revision
            },
            StatusCodes.Status200OK,
            changedAt,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListLoginExperiencesAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var denied = await RequireTenantPermissionAsync(
            actor,
            permissionService,
            "login-experience.manage",
            cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var experiences = await dbContext.LoginExperiences.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId)
            .OrderByDescending(item => item.VersionNumber)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Results.Ok(experiences.Select(LoginExperienceResponse.From).ToArray());
    }

    private static async Task<IResult> CreateLoginExperienceDraftAsync(
        CreateLoginExperienceRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var denied = await RequireTenantPermissionAsync(
            actor,
            permissionService,
            "login-experience.manage",
            cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            "login-experience.create-draft",
            request,
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (request.ClientGeneratedId == Guid.Empty)
        {
            return Problem("login-experience.id.required", "A client-generated descriptor id is required.");
        }

        var assetError = await ValidateLoginAssetsAsync(
            documentDirectory,
            actor.TenantId,
            request.ClientGeneratedId,
            request.LogoDocumentId,
            request.HeroDocumentId,
            cancellationToken);
        if (assetError is not null)
        {
            return Results.UnprocessableEntity(new { code = assetError });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLoginExperienceMutationLockAsync(dbContext, actor.TenantId, cancellationToken);
        if (await dbContext.LoginExperiences.AnyAsync(item =>
                item.TenantId == actor.TenantId && item.Id == request.ClientGeneratedId,
                cancellationToken))
        {
            return Results.Conflict(new { code = "login-experience.client-id.reused" });
        }

        var latestVersion = await dbContext.LoginExperiences.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        LoginExperience experience;
        var createdAt = clock.UtcNow;
        try
        {
            experience = LoginExperience.CreateDraft(
                request.ClientGeneratedId,
                actor.TenantId,
                checked(latestVersion + 1),
                request.CompositionVariant,
                request.SurfaceTone,
                request.AccentPalette,
                request.MotionPolicy,
                request.Eyebrow,
                request.Headline,
                request.SupportingText,
                request.LogoDocumentId,
                request.HeroDocumentId,
                actor.UserId,
                createdAt);
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        dbContext.LoginExperiences.Add(experience);
        var response = LoginExperienceResponse.From(experience);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteExperienceSideEffectsAsync(
            dbContext,
            transaction,
            sideEffects,
            actor,
            context,
            idempotency,
            "LoginExperienceDraftCreated",
            "LoginExperience",
            experience.Id,
            "IdentityAccess.LoginExperienceDraftCreated",
            response,
            new { experienceId = experience.Id, experience.VersionNumber },
            new Dictionary<string, object?>
            {
                ["versionNumber"] = experience.VersionNumber,
                ["hasLogo"] = experience.LogoDocumentId.HasValue,
                ["hasHero"] = experience.HeroDocumentId.HasValue
            },
            StatusCodes.Status201Created,
            createdAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created(
            $"/api/v1/identity/login-experiences/{experience.VersionNumber}",
            response);
    }

    private static Task<IResult> PublishLoginExperienceAsync(
        int versionNumber,
        ActivateLoginExperienceRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        IClock clock,
        CancellationToken cancellationToken) =>
        ActivateLoginExperienceAsync(
            versionNumber,
            request,
            false,
            context,
            actor,
            permissionService,
            dbContext,
            documentDirectory,
            sideEffects,
            idempotencyStore,
            clock,
            cancellationToken);

    private static Task<IResult> RollbackLoginExperienceAsync(
        int versionNumber,
        ActivateLoginExperienceRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        IClock clock,
        CancellationToken cancellationToken) =>
        ActivateLoginExperienceAsync(
            versionNumber,
            request,
            true,
            context,
            actor,
            permissionService,
            dbContext,
            documentDirectory,
            sideEffects,
            idempotencyStore,
            clock,
            cancellationToken);

    private static async Task<IResult> ActivateLoginExperienceAsync(
        int versionNumber,
        ActivateLoginExperienceRequest request,
        bool rollback,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var denied = await RequireTenantPermissionAsync(
            actor,
            permissionService,
            "login-experience.manage",
            cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var operation = rollback ? "login-experience.rollback" : "login-experience.publish";
        var idempotency = await PrepareIdempotencyAsync(
            context,
            actor.TenantId,
            operation,
            new { versionNumber, request.BaseRevision },
            idempotencyStore,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireLoginExperienceMutationLockAsync(dbContext, actor.TenantId, cancellationToken);
        var candidate = await dbContext.LoginExperiences.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId && item.VersionNumber == versionNumber,
            cancellationToken);
        if (candidate is null)
        {
            return Results.NotFound();
        }

        var expectedStatus = rollback ? LoginExperienceStatus.Superseded : LoginExperienceStatus.Draft;
        if (candidate.Status != expectedStatus)
        {
            return Results.Conflict(new { code = "login-experience.activation.invalid-state" });
        }

        if (candidate.Revision != request.BaseRevision)
        {
            return Results.Conflict(new
            {
                code = "login-experience.revision.conflict",
                currentRevision = candidate.Revision
            });
        }

        var assetError = await ValidateLoginAssetsAsync(
            documentDirectory,
            actor.TenantId,
            candidate.Id,
            candidate.LogoDocumentId,
            candidate.HeroDocumentId,
            cancellationToken);
        if (assetError is not null)
        {
            return Results.UnprocessableEntity(new { code = assetError });
        }

        var changedAt = clock.UtcNow;
        var current = await dbContext.LoginExperiences.SingleOrDefaultAsync(item =>
            item.TenantId == actor.TenantId &&
            item.Status == LoginExperienceStatus.Published &&
            item.Id != candidate.Id,
            cancellationToken);
        if (current is not null)
        {
            current.MarkSuperseded(actor.UserId, changedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        try
        {
            candidate.Publish(request.BaseRevision, actor.UserId, changedAt);
        }
        catch (DomainRuleException exception)
        {
            return Problem(exception.Code, exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = LoginExperienceResponse.From(candidate);
        await WriteExperienceSideEffectsAsync(
            dbContext,
            transaction,
            sideEffects,
            actor,
            context,
            idempotency,
            rollback ? "LoginExperienceRolledBack" : "LoginExperiencePublished",
            "LoginExperience",
            candidate.Id,
            "identity.login-experience.published.v1",
            response,
            new
            {
                experienceId = candidate.Id,
                candidate.VersionNumber,
                activation = rollback ? "Rollback" : "Publish"
            },
            new Dictionary<string, object?>
            {
                ["versionNumber"] = candidate.VersionNumber,
                ["activation"] = rollback ? "Rollback" : "Publish",
                ["supersededVersion"] = current?.VersionNumber
            },
            StatusCodes.Status200OK,
            changedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task AcquireLoginExperienceMutationLockAsync(
        IdentityAccessDbContext dbContext,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenantKey = tenantId.ToString("D");
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock(hashtextextended({tenantKey}, 0));",
            cancellationToken);
    }

    private static async Task<MemberProfileResponse?> LoadProfileResponseAsync(
        IdentityAccessDbContext dbContext,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == userId,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        var profile = await dbContext.MemberProfiles.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.UserId == userId,
            cancellationToken);
        return profile is null ? null : MemberProfileResponse.From(user, profile);
    }

    private static async Task<bool> CanReadProfileAsync(
        ICurrentActor actor,
        Guid targetUserId,
        IProjectPermissionService permissionService,
        IdentityAccessDbContext dbContext,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        if (targetUserId == Guid.Empty)
        {
            return false;
        }

        if (targetUserId == actor.UserId)
        {
            return await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "member-profile.read-self",
                cancellationToken);
        }

        if (await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "member-profile.read-directory",
                cancellationToken))
        {
            return true;
        }

        var sharedProjectIds = await (
            from requester in dbContext.ProjectMemberships.AsNoTracking()
            join target in dbContext.ProjectMemberships.AsNoTracking()
                on new { requester.TenantId, requester.ProjectId }
                equals new { target.TenantId, target.ProjectId }
            where requester.TenantId == actor.TenantId &&
                requester.UserId == actor.UserId && target.UserId == targetUserId &&
                requester.Status == MembershipStatus.Active &&
                target.Status == MembershipStatus.Active &&
                requester.StartsAt <= evaluatedAt && target.StartsAt <= evaluatedAt &&
                (!requester.EndsAt.HasValue || requester.EndsAt > evaluatedAt) &&
                (!target.EndsAt.HasValue || target.EndsAt > evaluatedAt)
            select requester.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var projectId in sharedProjectIds)
        {
            if (await permissionService.HasProjectPermissionAsync(
                    actor.TenantId,
                    actor.UserId,
                    projectId,
                    "member-profile.read-directory",
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string?> ValidateProfileAvatarAsync(
        ISharedDocumentDirectory documentDirectory,
        Guid tenantId,
        Guid userId,
        Guid? avatarDocumentId,
        CancellationToken cancellationToken)
    {
        if (!avatarDocumentId.HasValue)
        {
            return null;
        }

        try
        {
            var content = await documentDirectory.ReadReleasedAsync(
                tenantId,
                avatarDocumentId.Value,
                DocumentOwnerType.MemberProfile,
                userId,
                cancellationToken);
            return content is null || content.Document.Classification != DocumentClassification.Confidential ||
                !content.Document.ContentType.StartsWith("image/", StringComparison.Ordinal)
                ? "member-profile.avatar.not-released"
                : null;
        }
        catch (InvalidOperationException)
        {
            return "member-profile.avatar.integrity-failed";
        }
    }

    private static async Task<string?> ValidateLoginAssetsAsync(
        ISharedDocumentDirectory documentDirectory,
        Guid tenantId,
        Guid experienceId,
        Guid? logoDocumentId,
        Guid? heroDocumentId,
        CancellationToken cancellationToken)
    {
        foreach (var documentId in new[] { logoDocumentId, heroDocumentId }.OfType<Guid>())
        {
            try
            {
                var content = await documentDirectory.ReadReleasedAsync(
                    tenantId,
                    documentId,
                    DocumentOwnerType.LoginExperience,
                    experienceId,
                    cancellationToken);
                if (content is null || content.Document.Classification != DocumentClassification.Internal ||
                    !content.Document.ContentType.StartsWith("image/", StringComparison.Ordinal))
                {
                    return "login-experience.asset.not-released";
                }
            }
            catch (InvalidOperationException)
            {
                return "login-experience.asset.integrity-failed";
            }
        }

        return null;
    }

    private static async Task<IResult?> RequireTenantPermissionAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        string permission,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        return await permissionService.HasTenantPermissionAsync(
            actor.TenantId,
            actor.UserId,
            permission,
            cancellationToken)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task SaveExperienceMutationAsync<TResponse, TEvent>(
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
        TEvent eventPayload,
        Dictionary<string, object?> auditData,
        int statusCode,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteExperienceSideEffectsAsync(
            dbContext,
            transaction,
            sideEffects,
            actor,
            context,
            idempotency,
            auditEvent,
            resourceType,
            resourceId,
            outboxEvent,
            response,
            eventPayload,
            auditData,
            statusCode,
            occurredAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static Task WriteExperienceSideEffectsAsync<TResponse, TEvent>(
        IdentityAccessDbContext dbContext,
        IDbContextTransaction transaction,
        ITransactionalSideEffectWriter sideEffects,
        ICurrentActor actor,
        HttpContext context,
        PreparedIdempotency idempotency,
        string auditEvent,
        string resourceType,
        Guid resourceId,
        string outboxEvent,
        TResponse response,
        TEvent eventPayload,
        Dictionary<string, object?> auditData,
        int statusCode,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        var payloadJson = JsonSerializer.Serialize(eventPayload, SerializerOptions);
        return sideEffects.WriteAsync(
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
                    occurredAt,
                    auditData,
                    context.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    null,
                    outboxEvent,
                    1,
                    occurredAt,
                    payloadJson,
                    context.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    idempotency.Operation,
                    idempotency.RequestHash,
                    statusCode,
                    responseJson,
                    occurredAt,
                    occurredAt.AddDays(7))),
            cancellationToken);
    }
}

internal sealed record ProfileAvatarCropRequest(decimal X, decimal Y, decimal Width, decimal Height)
{
    public ProfileAvatarCrop ToDomain() => new(X, Y, Width, Height);
}

internal sealed record UpdateSelfProfileRequest(
    long BaseUserRevision,
    long BaseProfileRevision,
    string DisplayName,
    string? JobTitle,
    string? WorkPhone,
    Guid? AvatarDocumentId,
    ProfileAvatarCropRequest? AvatarCrop);

internal sealed record UpdateDirectoryProfileRequest(
    long BaseUserRevision,
    long BaseProfileRevision,
    string DisplayName,
    string? JobTitle,
    string? OrganizationUnit,
    string? WorkPhone,
    Guid? AvatarDocumentId,
    ProfileAvatarCropRequest? AvatarCrop);

internal sealed record MemberProfileResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    string? JobTitle,
    string? OrganizationUnit,
    string? WorkPhone,
    Guid? AvatarDocumentId,
    ProfileAvatarCropResponse? AvatarCrop,
    string? AvatarUrl,
    long UserRevision,
    long ProfileRevision,
    DateTimeOffset UpdatedAt)
{
    public static MemberProfileResponse From(UserAccount user, MemberProfile profile) => new(
        user.Id,
        user.DisplayName,
        user.Email,
        profile.JobTitle,
        profile.OrganizationUnit,
        profile.WorkPhone,
        profile.AvatarDocumentId,
        profile.AvatarCropX.HasValue && profile.AvatarCropY.HasValue &&
            profile.AvatarCropWidth.HasValue && profile.AvatarCropHeight.HasValue
            ? new ProfileAvatarCropResponse(
                profile.AvatarCropX.Value,
                profile.AvatarCropY.Value,
                profile.AvatarCropWidth.Value,
                profile.AvatarCropHeight.Value)
            : null,
        profile.AvatarDocumentId.HasValue
            ? $"/api/v1/member-profiles/{user.Id}/avatar?v={profile.AvatarDocumentId.Value:N}-{profile.Revision}"
            : null,
        user.Revision,
        profile.Revision,
        profile.UpdatedAt);
}

internal sealed record ProfileAvatarCropResponse(decimal X, decimal Y, decimal Width, decimal Height);

internal sealed record CreateLoginExperienceRequest(
    Guid ClientGeneratedId,
    LoginCompositionVariant CompositionVariant,
    LoginSurfaceTone SurfaceTone,
    LoginAccentPalette AccentPalette,
    LoginMotionPolicy MotionPolicy,
    string Eyebrow,
    string Headline,
    string SupportingText,
    Guid? LogoDocumentId,
    Guid? HeroDocumentId);

internal sealed record ActivateLoginExperienceRequest(long BaseRevision);

internal sealed record LoginExperienceResponse(
    Guid Id,
    int VersionNumber,
    LoginExperienceStatus Status,
    LoginCompositionVariant CompositionVariant,
    LoginSurfaceTone SurfaceTone,
    LoginAccentPalette AccentPalette,
    LoginMotionPolicy MotionPolicy,
    string Eyebrow,
    string Headline,
    string SupportingText,
    Guid? LogoDocumentId,
    Guid? HeroDocumentId,
    string? LogoPreviewUrl,
    string? HeroPreviewUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    long Revision)
{
    public static LoginExperienceResponse From(LoginExperience experience) => new(
        experience.Id,
        experience.VersionNumber,
        experience.Status,
        experience.CompositionVariant,
        experience.SurfaceTone,
        experience.AccentPalette,
        experience.MotionPolicy,
        experience.Eyebrow,
        experience.Headline,
        experience.SupportingText,
        experience.LogoDocumentId,
        experience.HeroDocumentId,
        experience.LogoDocumentId.HasValue
            ? $"/api/v1/documents/{experience.LogoDocumentId.Value}/content"
            : null,
        experience.HeroDocumentId.HasValue
            ? $"/api/v1/documents/{experience.HeroDocumentId.Value}/content"
            : null,
        experience.CreatedAt,
        experience.PublishedAt,
        experience.Revision);
}

internal sealed record PublicLoginExperienceResponse(
    int Version,
    bool FallbackUsed,
    LoginCompositionVariant CompositionVariant,
    LoginSurfaceTone SurfaceTone,
    LoginAccentPalette AccentPalette,
    LoginMotionPolicy MotionPolicy,
    string Eyebrow,
    string Headline,
    string SupportingText,
    string? LogoUrl,
    string? HeroUrl)
{
    public static PublicLoginExperienceResponse Fallback() => new(
        0,
        true,
        LoginCompositionVariant.BlueprintSplit,
        LoginSurfaceTone.WarmStone,
        LoginAccentPalette.CorporateNavyGreen,
        LoginMotionPolicy.Balanced,
        "سامانه جامع مدیریت پروژه",
        "ساختن، فراتر از امروز",
        "مرکز فرمان یکپارچه برای تصمیم‌های دقیق، قابل ردیابی و مبتنی بر واقعیت پروژه.",
        null,
        null);

    public static PublicLoginExperienceResponse From(
        LoginExperience experience,
        bool logoAvailable,
        bool heroAvailable) => new(
        experience.VersionNumber,
        false,
        experience.CompositionVariant,
        experience.SurfaceTone,
        experience.AccentPalette,
        experience.MotionPolicy,
        experience.Eyebrow,
        experience.Headline,
        experience.SupportingText,
        logoAvailable
            ? $"/api/v1/public/login-experience/assets/logo?tenantId={experience.TenantId:D}&version={experience.VersionNumber}"
            : null,
        heroAvailable
            ? $"/api/v1/public/login-experience/assets/hero?tenantId={experience.TenantId:D}&version={experience.VersionNumber}"
            : null);
}
