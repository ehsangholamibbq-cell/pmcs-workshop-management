using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Projects.Persistence;
using Pmcs.Modules.Projects.Services;

namespace Pmcs.Modules.Projects.Endpoints;

internal static class ProjectBootstrapEndpoints
{
    private const string PreviewPermission = "projects.bootstrap.preview";
    private const string CreatePermission = "projects.bootstrap.create";
    private const string ActivatePermission = "projects.bootstrap.activate";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapProjectBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/project-bootstraps").WithTags("Project Bootstrap");
        group.MapPost("/", CreateAsync);
        group.MapGet("/{planId:guid}", GetAsync);
        group.MapPost("/{planId:guid}/preview", RefreshPreviewAsync);
        group.MapPost("/{planId:guid}/execute", ExecuteAsync);
        group.MapGet("/{planId:guid}/result", GetResultAsync);
        group.MapPost("/{planId:guid}/activate", ActivateAsync);
    }

    private static async Task<IResult> CreateAsync(
        CreateProjectBootstrapRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        ProjectBootstrapPreviewFactory previewFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }
        if (!await permissions.HasTenantPermissionAsync(
                actor.TenantId, actor.UserId, CreatePermission, cancellationToken) ||
            !await permissions.HasProjectPermissionAsync(
                actor.TenantId, actor.UserId, request.SourceProjectId, PreviewPermission, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetIdempotencyAsync(
            httpContext, actor, idempotencyStore, "projects.bootstrap.create", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var source = await dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == request.SourceProjectId,
            cancellationToken);
        if (source is null)
        {
            return Results.NotFound(new { code = "project.bootstrap.source.not_found" });
        }
        if (await dbContext.Projects.AsNoTracking().AnyAsync(
                item => item.TenantId == actor.TenantId && item.Code == request.Target.Code.Trim().ToUpperInvariant(),
                cancellationToken))
        {
            return Results.Conflict(new { code = "project.code.duplicate" });
        }

        var now = clock.UtcNow;
        var target = CreateTarget(request.Target, actor, now);
        var root = ProjectLocation.Create(
            Guid.NewGuid(), actor.TenantId, target.Id, "ROOT", "کل پروژه", null, actor.UserId, now);
        var sourceLocations = await dbContext.ProjectLocations.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == source.Id)
            .ToListAsync(cancellationToken);
        var categories = ProjectBootstrapPreviewFactory.NormalizeCategories(request.Categories);
        var members = NormalizeMembers(request.Members);
        var preview = await previewFactory.BuildAsync(
            actor.TenantId,
            actor.UserId,
            source,
            target,
            sourceLocations,
            [root],
            categories,
            members,
            cancellationToken);
        var plan = ProjectBootstrapPlan.Create(
            Guid.NewGuid(),
            actor.TenantId,
            source.Id,
            target.Id,
            request.ConflictPolicy,
            JsonSerializer.Serialize(categories, SerializerOptions),
            JsonSerializer.Serialize(members, SerializerOptions),
            actor.UserId,
            now);
        plan.RecordPreview(
            plan.Revision,
            ProjectBootstrapContributorCatalog.Version,
            preview.Digest,
            preview.MembershipSnapshotToken,
            JsonSerializer.Serialize(preview.Document, SerializerOptions),
            source.Revision,
            target.Revision,
            now,
            now.AddMinutes(30));

        dbContext.Projects.Add(target);
        dbContext.ProjectLocations.Add(root);
        dbContext.ProjectBootstrapPlans.Add(plan);
        var response = ToPreviewResponse(plan, target);
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    target.Id,
                    actor.UserId,
                    "ProjectBootstrapPreviewCreated",
                    "ProjectBootstrapPlan",
                    plan.Id.ToString(),
                    now,
                    new Dictionary<string, object?>
                    {
                        ["sourceProjectId"] = source.Id,
                        ["targetProjectId"] = target.Id,
                        ["previewDigest"] = plan.PreviewDigest,
                        ["categories"] = categories.Select(item => item.ToString()).ToArray(),
                        ["conflictPolicy"] = plan.ConflictPolicy.ToString()
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, target.Id,
                    "Projects.ProjectBootstrapPreviewCreated", 1, now, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, idempotency.Key, idempotency.Operation, idempotency.Hash,
                    StatusCodes.Status201Created, responseJson, now, now.AddDays(30))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/v1/project-bootstraps/{plan.Id}", response);
    }

    private static async Task<IResult> GetAsync(
        Guid planId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        var plan = await dbContext.ProjectBootstrapPlans.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == planId,
            cancellationToken);
        if (plan is null)
        {
            return Results.NotFound(new { code = "project.bootstrap.not_found" });
        }
        if (!await CanPreviewAsync(plan, actor, permissions, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var target = await dbContext.Projects.AsNoTracking().SingleAsync(
            item => item.TenantId == actor.TenantId && item.Id == plan.TargetProjectId,
            cancellationToken);
        return Results.Ok(ToPreviewResponse(plan, target));
    }

    private static async Task<IResult> RefreshPreviewAsync(
        Guid planId,
        RefreshProjectBootstrapPreviewRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        ProjectBootstrapPreviewFactory previewFactory,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        var idempotency = await GetIdempotencyAsync(
            httpContext, actor, idempotencyStore, "projects.bootstrap.preview.refresh", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }
        var plan = await dbContext.ProjectBootstrapPlans.SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == planId,
            cancellationToken);
        if (plan is null)
        {
            return Results.NotFound(new { code = "project.bootstrap.not_found" });
        }
        if (!await CanPreviewAsync(plan, actor, permissions, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (plan.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "project.bootstrap.revision.conflict", currentRevision = plan.Revision });
        }

        var source = await dbContext.Projects.AsNoTracking().SingleAsync(
            item => item.TenantId == actor.TenantId && item.Id == plan.SourceProjectId,
            cancellationToken);
        var target = await dbContext.Projects.SingleAsync(
            item => item.TenantId == actor.TenantId && item.Id == plan.TargetProjectId,
            cancellationToken);
        if (target.Status != ProjectStatus.Draft)
        {
            return Results.Conflict(new { code = "project.bootstrap.target.not_draft" });
        }
        var sourceLocations = await LoadLocationsAsync(dbContext, actor.TenantId, source.Id, true, cancellationToken);
        var targetLocations = await LoadLocationsAsync(dbContext, actor.TenantId, target.Id, true, cancellationToken);
        var categories = Deserialize<ProjectBootstrapCategory[]>(plan.SelectedCategoriesJson);
        var members = Deserialize<ProjectMembershipBootstrapSelection[]>(plan.MemberSelectionsJson);
        var preview = await previewFactory.BuildAsync(
            actor.TenantId,
            actor.UserId,
            source,
            target,
            sourceLocations,
            targetLocations,
            categories,
            members,
            cancellationToken);
        var now = clock.UtcNow;
        plan.RecordPreview(
            request.BaseRevision,
            ProjectBootstrapContributorCatalog.Version,
            preview.Digest,
            preview.MembershipSnapshotToken,
            JsonSerializer.Serialize(preview.Document, SerializerOptions),
            source.Revision,
            target.Revision,
            now,
            now.AddMinutes(30));
        var response = ToPreviewResponse(plan, target);
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                Audit(actor, target.Id, "ProjectBootstrapPreviewRefreshed", plan, now, httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, target.Id,
                    "Projects.ProjectBootstrapPreviewRefreshed", 1, now, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, idempotency.Key, idempotency.Operation, idempotency.Hash,
                    StatusCodes.Status200OK, responseJson, now, now.AddDays(30))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ExecuteAsync(
        Guid planId,
        ExecuteProjectBootstrapRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        ProjectBootstrapPreviewFactory previewFactory,
        IProjectMembershipBootstrapService membershipBootstrap,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        if (!await permissions.HasTenantPermissionAsync(
                actor.TenantId, actor.UserId, CreatePermission, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var idempotency = await GetIdempotencyAsync(
            httpContext, actor, idempotencyStore, "projects.bootstrap.execute", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }
        var plan = await dbContext.ProjectBootstrapPlans.SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == planId,
            cancellationToken);
        if (plan is null)
        {
            return Results.NotFound(new { code = "project.bootstrap.not_found" });
        }
        if (!await permissions.HasProjectPermissionAsync(
                actor.TenantId, actor.UserId, plan.SourceProjectId, PreviewPermission, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (plan.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "project.bootstrap.revision.conflict", currentRevision = plan.Revision });
        }

        var now = clock.UtcNow;
        try
        {
            plan.EnsurePreviewUsable(request.PreviewDigest, now);
        }
        catch (Pmcs.BuildingBlocks.Domain.DomainRuleException exception)
        {
            return Results.Conflict(new { code = exception.Code, detail = exception.Message });
        }

        var source = await dbContext.Projects.AsNoTracking().SingleAsync(
            item => item.TenantId == actor.TenantId && item.Id == plan.SourceProjectId,
            cancellationToken);
        var target = await dbContext.Projects.SingleAsync(
            item => item.TenantId == actor.TenantId && item.Id == plan.TargetProjectId,
            cancellationToken);
        if (target.Status != ProjectStatus.Draft)
        {
            return Results.Conflict(new { code = "project.bootstrap.target.not_draft" });
        }
        var sourceLocations = await LoadLocationsAsync(dbContext, actor.TenantId, source.Id, true, cancellationToken);
        var targetLocations = await LoadLocationsAsync(dbContext, actor.TenantId, target.Id, false, cancellationToken);
        var categories = Deserialize<ProjectBootstrapCategory[]>(plan.SelectedCategoriesJson);
        var members = Deserialize<ProjectMembershipBootstrapSelection[]>(plan.MemberSelectionsJson);
        var storedDocument = Deserialize<ProjectBootstrapPreviewDocument>(plan.PreviewJson);
        var storedMemberItems = storedDocument.Items
            .Where(item => item.Category == ProjectBootstrapCategory.Members)
            .ToArray();
        var current = previewFactory.RebuildProjectState(
            actor.TenantId,
            source,
            target,
            sourceLocations,
            targetLocations,
            categories,
            members,
            storedMemberItems,
            plan.MembershipSnapshotToken);
        if (!string.Equals(current.Digest, plan.PreviewDigest, StringComparison.Ordinal) ||
            !string.Equals(current.Digest, request.PreviewDigest, StringComparison.Ordinal))
        {
            return Results.Conflict(new
            {
                code = "project.bootstrap.preview.changed",
                freshPreviewRequired = true,
                currentDigest = current.Digest
            });
        }
        var summary = ProjectBootstrapSummaryResponse.From(current.Document.Items);
        if (summary.Blocked > 0)
        {
            return Results.UnprocessableEntity(new { code = "project.bootstrap.blocked", preview = current.Document });
        }
        if (summary.Conflicts > 0 && plan.ConflictPolicy == ProjectBootstrapConflictPolicy.FailOnConflict)
        {
            return Results.Conflict(new { code = "project.bootstrap.conflicts", preview = current.Document });
        }

        ProjectMembershipBootstrapExecution membershipExecution;
        if (categories.Contains(ProjectBootstrapCategory.Members) && members.Length > 0)
        {
            try
            {
                membershipExecution = await membershipBootstrap.ExecuteAsync(
                    actor.TenantId,
                    actor.UserId,
                    plan.Id,
                    source.Id,
                    target.Id,
                    members,
                    plan.MembershipSnapshotToken,
                    httpContext.TraceIdentifier,
                    cancellationToken);
            }
            catch (ProjectMembershipBootstrapPermissionException exception)
            {
                return Results.Json(
                    new { code = "project.bootstrap.members.permission_denied", detail = exception.Message },
                    statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ProjectMembershipBootstrapChangedException exception)
            {
                return Results.Conflict(new
                {
                    code = "project.bootstrap.members.changed",
                    detail = exception.Message,
                    freshPreviewRequired = true
                });
            }
        }
        else
        {
            membershipExecution = new ProjectMembershipBootstrapExecution(
                "identity.project-memberships", "1.0.0", plan.MembershipSnapshotToken, []);
        }

        ApplySetup(source, target, categories, now);
        var addedLocations = AddLocations(
            sourceLocations,
            targetLocations,
            current.Document.Items,
            actor,
            target,
            now,
            dbContext);
        var resultItems = ReplaceMembershipItems(current.Document.Items, membershipExecution);
        var validation = new[]
        {
            new ProjectBootstrapValidationResponse(
                "destination-draft", target.Status == ProjectStatus.Draft,
                "مقصد پس از اجرا Draft باقی مانده و Activation مستقل است."),
            new ProjectBootstrapValidationResponse(
                "preview-execute-digest", true,
                "همان Digest تأییدشده برای Execute استفاده شد."),
            new ProjectBootstrapValidationResponse(
                "operational-data-excluded", true,
                "هیچ Contributor عملیاتی، مالی، پیام، فایل، Audit، Offline یا Sync اجرا نشد."),
            new ProjectBootstrapValidationResponse(
                "membership-reference-only", membershipExecution.BlockedCount == 0,
                "Membershipها فقط به حساب‌های موجود Tenant ارجاع می‌دهند و User جدید ساخته نشد."),
            new ProjectBootstrapValidationResponse(
                "location-count", addedLocations == resultItems.Count(item =>
                    item.Category == ProjectBootstrapCategory.Locations &&
                    item.Disposition == ProjectMembershipBootstrapDisposition.Added),
                $"{addedLocations} مکان allowlisted با شناسه جدید ساخته شد.")
        };
        if (validation.Any(item => !item.Passed))
        {
            throw new InvalidOperationException("Project bootstrap post-validation failed before commit.");
        }

        var result = new ProjectBootstrapResultResponse(
            plan.Id,
            source.Id,
            target.Id,
            ProjectResponse.From(target),
            ProjectBootstrapStatus.Completed,
            ProjectBootstrapContributorCatalog.Version,
            plan.PreviewDigest,
            resultItems,
            ProjectBootstrapSummaryResponse.From(resultItems),
            validation,
            now,
            plan.Revision + 1);
        var responseJson = JsonSerializer.Serialize(result, SerializerOptions);
        plan.Complete(plan.Revision, request.PreviewDigest, responseJson, target.Revision, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    target.Id,
                    actor.UserId,
                    "ProjectBootstrapCompleted",
                    "ProjectBootstrapPlan",
                    plan.Id.ToString(),
                    now,
                    new Dictionary<string, object?>
                    {
                        ["sourceProjectId"] = source.Id,
                        ["targetProjectId"] = target.Id,
                        ["previewDigest"] = plan.PreviewDigest,
                        ["addedCount"] = result.Summary.Added,
                        ["skippedCount"] = result.Summary.Skipped,
                        ["conflictCount"] = result.Summary.Conflicts,
                        ["membershipAddedCount"] = membershipExecution.AddedCount,
                        ["conflictPolicy"] = plan.ConflictPolicy.ToString()
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    target.Id,
                    "projects.bootstrap.completed.v1",
                    1,
                    now,
                    JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1,
                        planId = plan.Id,
                        sourceProjectId = source.Id,
                        targetProjectId = target.Id,
                        previewDigest = plan.PreviewDigest,
                        occurredAt = now,
                        correlationId = httpContext.TraceIdentifier
                    }, SerializerOptions),
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, idempotency.Key, idempotency.Operation, idempotency.Hash,
                    StatusCodes.Status200OK, responseJson, now, now.AddDays(30))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetResultAsync(
        Guid planId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        var plan = await dbContext.ProjectBootstrapPlans.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == planId,
            cancellationToken);
        if (plan is null)
        {
            return Results.NotFound(new { code = "project.bootstrap.not_found" });
        }
        if (!await CanPreviewAsync(plan, actor, permissions, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (string.IsNullOrWhiteSpace(plan.ResultJson))
        {
            return Results.Conflict(new { code = "project.bootstrap.result.not_ready" });
        }
        var target = await dbContext.Projects.AsNoTracking().SingleAsync(
            item => item.TenantId == actor.TenantId && item.Id == plan.TargetProjectId,
            cancellationToken);
        var result = Deserialize<ProjectBootstrapResultResponse>(plan.ResultJson) with
        {
            TargetProject = ProjectResponse.From(target),
            Status = plan.Status,
            PlanRevision = plan.Revision
        };
        return Results.Json(result, SerializerOptions, contentType: "application/json");
    }

    private static async Task<IResult> ActivateAsync(
        Guid planId,
        ActivateProjectBootstrapRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        ProjectsDbContext dbContext,
        ProjectReadinessEvaluator readinessEvaluator,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        var idempotency = await GetIdempotencyAsync(
            httpContext, actor, idempotencyStore, "projects.bootstrap.activate", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }
        var plan = await dbContext.ProjectBootstrapPlans.SingleOrDefaultAsync(
            item => item.TenantId == actor.TenantId && item.Id == planId,
            cancellationToken);
        if (plan is null)
        {
            return Results.NotFound(new { code = "project.bootstrap.not_found" });
        }
        if (!await permissions.HasProjectPermissionAsync(
                actor.TenantId, actor.UserId, plan.TargetProjectId, ActivatePermission, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        if (plan.Revision != request.BaseRevision)
        {
            return Results.Conflict(new { code = "project.bootstrap.revision.conflict", currentRevision = plan.Revision });
        }
        var target = await dbContext.Projects.SingleAsync(
            item => item.TenantId == actor.TenantId && item.Id == plan.TargetProjectId,
            cancellationToken);
        if (target.Revision != request.TargetBaseRevision)
        {
            return Results.Conflict(new { code = "project.revision.conflict", currentRevision = target.Revision });
        }
        if (plan.Status != ProjectBootstrapStatus.Completed)
        {
            return Results.Conflict(new { code = "project.bootstrap.activate.invalid_state" });
        }
        var readiness = await readinessEvaluator.EvaluateAsync(target, cancellationToken);
        if (!readiness.IsReady)
        {
            return Results.UnprocessableEntity(new { code = "project.activate.readiness_failed", readiness });
        }

        var now = clock.UtcNow;
        target.Activate(request.TargetBaseRevision, actor.UserId, now);
        plan.MarkActivated(request.BaseRevision, now);
        var result = Deserialize<ProjectBootstrapResultResponse>(plan.ResultJson!) with
        {
            TargetProject = ProjectResponse.From(target),
            Status = plan.Status,
            PlanRevision = plan.Revision
        };
        var responseJson = JsonSerializer.Serialize(result, SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                Audit(actor, target.Id, "ProjectBootstrapTargetActivated", plan, now, httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, target.Id,
                    "Projects.ProjectBootstrapTargetActivated", 1, now, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, idempotency.Key, idempotency.Operation, idempotency.Hash,
                    StatusCodes.Status200OK, responseJson, now, now.AddDays(30))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static Project CreateTarget(
        ProjectBootstrapTargetRequest target,
        ICurrentActor actor,
        DateTimeOffset now) => Project.Create(
        Guid.NewGuid(),
        actor.TenantId,
        target.Code,
        target.Name,
        ContractModel.NotConfigured,
        PlanningMode.None,
        CapabilityMode.NotEnabled,
        CapabilityMode.NotEnabled,
        CapabilityMode.NotEnabled,
        target.TimeZone,
        actor.UserId,
        now,
        CapabilityMode.NotEnabled,
        target.BaseCurrencyCode,
        CapabilityMode.NotEnabled,
        target.ProjectType,
        target.ExecutionPhase,
        target.CountryCode,
        target.Region,
        target.StartDate,
        target.PlannedFinishDate,
        target.ShortDescription,
        target.UnitSystem,
        null,
        ReportingFrequency.NotConfigured,
        DailyReportWorkflow.NotConfigured,
        target.OfflinePolicyAccepted);

    private static void ApplySetup(
        Project source,
        Project target,
        IReadOnlyCollection<ProjectBootstrapCategory> categories,
        DateTimeOffset now)
    {
        var baseSettings = categories.Contains(ProjectBootstrapCategory.BaseSettings);
        var calendar = categories.Contains(ProjectBootstrapCategory.Calendar);
        var workflow = categories.Contains(ProjectBootstrapCategory.WorkflowTemplates);
        var reports = categories.Contains(ProjectBootstrapCategory.ReportTemplates);
        if (!baseSettings && !calendar && !workflow && !reports)
        {
            return;
        }

        target.ConfigureSetup(
            target.Revision,
            baseSettings ? source.ContractModel : target.ContractModel,
            baseSettings ? source.PlanningMode : target.PlanningMode,
            baseSettings ? ProjectBootstrapPreviewFactory.NormalizeCapability(source.BudgetMode) : target.BudgetMode,
            baseSettings ? ProjectBootstrapPreviewFactory.NormalizeCapability(source.QualityMode) : target.QualityMode,
            baseSettings ? ProjectBootstrapPreviewFactory.NormalizeCapability(source.HseMode) : target.HseMode,
            baseSettings ? ProjectBootstrapPreviewFactory.NormalizeCapability(source.FinanceMode) : target.FinanceMode,
            baseSettings ? ProjectBootstrapPreviewFactory.NormalizeCapability(source.ProcurementMode) : target.ProcurementMode,
            calendar ? source.CalendarMode : target.CalendarMode,
            calendar ? source.WorkingDaysMask : target.WorkingDaysMask,
            target.ProjectType,
            target.ExecutionPhase,
            target.CountryCode,
            target.Region,
            target.StartDate,
            target.PlannedFinishDate,
            target.ShortDescription,
            target.TimeZone,
            target.BaseCurrencyCode,
            target.UnitSystem,
            workflow ? source.DailyCutoffLocalTime : target.DailyCutoffLocalTime,
            reports ? source.ReportingFrequency : target.ReportingFrequency,
            workflow ? source.DailyReportWorkflow : target.DailyReportWorkflow,
            target.OfflinePolicyAccepted,
            now,
            false,
            null);
    }

    private static int AddLocations(
        IReadOnlyCollection<ProjectLocation> sourceLocations,
        IReadOnlyCollection<ProjectLocation> targetLocations,
        IReadOnlyCollection<ProjectBootstrapItemResponse> previewItems,
        ICurrentActor actor,
        Project target,
        DateTimeOffset now,
        ProjectsDbContext dbContext)
    {
        var addedCodes = previewItems
            .Where(item => item.Category == ProjectBootstrapCategory.Locations &&
                item.Disposition == ProjectMembershipBootstrapDisposition.Added &&
                item.SourceReference is not null)
            .Select(item => item.SourceReference!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (addedCodes.Count == 0)
        {
            return 0;
        }

        var targetByCode = targetLocations.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var source in sourceLocations)
        {
            if (targetByCode.TryGetValue(source.Code, out var existing))
            {
                idMap[source.Id] = existing.Id;
            }
        }
        var pending = sourceLocations
            .Where(item => item.Status == ProjectLocationStatus.Active &&
                item.ParentLocationId.HasValue && addedCodes.Contains(item.Code))
            .ToList();
        var count = 0;
        while (pending.Count > 0)
        {
            var ready = pending
                .Where(item => item.ParentLocationId.HasValue && idMap.ContainsKey(item.ParentLocationId.Value))
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0)
            {
                throw new InvalidOperationException("The allowlisted project location hierarchy cannot be materialized safely.");
            }
            foreach (var source in ready)
            {
                var location = ProjectLocation.Create(
                    Guid.NewGuid(),
                    actor.TenantId,
                    target.Id,
                    source.Code,
                    source.Name,
                    idMap[source.ParentLocationId!.Value],
                    actor.UserId,
                    now);
                dbContext.ProjectLocations.Add(location);
                idMap[source.Id] = location.Id;
                pending.Remove(source);
                count++;
            }
        }
        return count;
    }

    private static IReadOnlyCollection<ProjectBootstrapItemResponse> ReplaceMembershipItems(
        IReadOnlyCollection<ProjectBootstrapItemResponse> previewItems,
        ProjectMembershipBootstrapExecution execution)
    {
        var projectItems = previewItems.Where(item => item.Category != ProjectBootstrapCategory.Members);
        var memberItems = execution.Items.Select(item => new ProjectBootstrapItemResponse(
            execution.ContributorId,
            ProjectBootstrapCategory.Members,
            item.Disposition,
            item.Code,
            item.DisplayName,
            item.Detail,
            item.UserId.ToString(),
            $"{item.RequestedRoleCode}:{item.AccessScope}"));
        return projectItems.Concat(memberItems).ToArray();
    }

    private static async Task<List<ProjectLocation>> LoadLocationsAsync(
        ProjectsDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        bool noTracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProjectLocations.Where(item =>
            item.TenantId == tenantId && item.ProjectId == projectId);
        if (noTracking)
        {
            query = query.AsNoTracking();
        }
        return await query.OrderBy(item => item.Code).ToListAsync(cancellationToken);
    }

    private static ProjectBootstrapPreviewResponse ToPreviewResponse(
        ProjectBootstrapPlan plan,
        Project target)
    {
        var document = Deserialize<ProjectBootstrapPreviewDocument>(plan.PreviewJson);
        return new ProjectBootstrapPreviewResponse(
            plan.Id,
            plan.SourceProjectId,
            document.SourceProjectCode,
            plan.TargetProjectId,
            ProjectResponse.From(target),
            plan.Status,
            plan.ConflictPolicy,
            document.SelectedCategories,
            plan.ContributorCatalogVersion,
            plan.PreviewDigest,
            plan.PreviewedAt ?? plan.CreatedAt,
            plan.PreviewExpiresAt ?? plan.CreatedAt,
            document.Contributors,
            document.Items,
            ProjectBootstrapSummaryResponse.From(document.Items),
            document.AlwaysExcluded,
            plan.Revision);
    }

    private static IResult? ValidateRequest(CreateProjectBootstrapRequest request)
    {
        if (request.SourceProjectId == Guid.Empty || request.Target is null || request.Categories is null ||
            request.Categories.Count == 0 || request.Categories.Any(item => !Enum.IsDefined(item)) ||
            !Enum.IsDefined(request.ConflictPolicy))
        {
            return Results.UnprocessableEntity(new { code = "project.bootstrap.request.invalid" });
        }
        var members = request.Members ?? [];
        if (!request.Categories.Contains(ProjectBootstrapCategory.Members) && members.Count > 0)
        {
            return Results.UnprocessableEntity(new { code = "project.bootstrap.members.category_required" });
        }
        if (members.Count > 500 || members.Any(item => item.UserId == Guid.Empty) ||
            members.Select(item => item.UserId).Distinct().Count() != members.Count)
        {
            return Results.UnprocessableEntity(new { code = "project.bootstrap.members.invalid" });
        }
        return null;
    }

    private static ProjectMembershipBootstrapSelection[] NormalizeMembers(
        IReadOnlyCollection<ProjectBootstrapMemberSelectionRequest>? members) =>
        (members ?? [])
            .Select(item => new ProjectMembershipBootstrapSelection(
                item.UserId,
                item.RoleCode?.Trim() ?? string.Empty,
                item.AccessScope?.Trim() ?? string.Empty))
            .OrderBy(item => item.UserId)
            .ToArray();

    private static async Task<bool> CanPreviewAsync(
        ProjectBootstrapPlan plan,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        CancellationToken cancellationToken) =>
        await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, plan.SourceProjectId, PreviewPermission, cancellationToken) &&
        await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, plan.TargetProjectId, PreviewPermission, cancellationToken);

    private static AuditEntry Audit(
        ICurrentActor actor,
        Guid targetProjectId,
        string eventType,
        ProjectBootstrapPlan plan,
        DateTimeOffset now,
        string correlationId) => new(
        actor.TenantId,
        targetProjectId,
        actor.UserId,
        eventType,
        "ProjectBootstrapPlan",
        plan.Id.ToString(),
        now,
        new Dictionary<string, object?>
        {
            ["sourceProjectId"] = plan.SourceProjectId,
            ["targetProjectId"] = plan.TargetProjectId,
            ["status"] = plan.Status.ToString(),
            ["previewDigest"] = plan.PreviewDigest,
            ["revision"] = plan.Revision
        },
        correlationId);

    private static async Task<(string Key, string Hash, string Operation, IResult? Result)> GetIdempotencyAsync<TRequest>(
        HttpContext httpContext,
        ICurrentActor actor,
        IIdempotencyStore store,
        string operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (string.Empty, string.Empty, operation, Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }
        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await store.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return (key, hash, operation, replay is null
            ? null
            : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, SerializerOptions) ??
        throw new InvalidOperationException($"Stored {typeof(T).Name} payload is invalid.");

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
