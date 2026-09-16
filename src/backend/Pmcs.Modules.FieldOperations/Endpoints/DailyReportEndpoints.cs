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
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.FieldOperations.Endpoints;

internal static class DailyReportEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapDailyReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/daily-reports").WithTags("Daily Reports");

        group.MapGet("/", ListAsync);
        group.MapGet("/inbox", InboxAsync);
        group.MapGet("/{reportId:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{reportId:guid}/facts", AddFactAsync);
        group.MapPost("/{reportId:guid}/submit", SubmitAsync);
        group.MapPost("/{reportId:guid}/return", ReturnAsync);
        group.MapPost("/{reportId:guid}/approve", ApproveAsync);
        group.MapPost("/{reportId:guid}/corrections", StartCorrectionAsync);
        group.MapPost("/{reportId:guid}/details", ReviseDetailsAsync);
        group.MapPost("/{reportId:guid}/facts/{factId:guid}/remove", RemoveFactAsync);
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var reports = await dbContext.DailyReports
            .AsNoTracking()
            .Include(report => report.Facts)
            .Where(report => report.TenantId == actor.TenantId && report.ProjectId == projectId)
            .OrderByDescending(report => report.ReportDate)
            .ThenByDescending(report => report.VersionNumber)
            .Take(60)
            .ToListAsync(cancellationToken);

        return Results.Ok(reports.Select(report => DailyReportResponse.From(report)).ToArray());
    }

    private static async Task<IResult> InboxAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var reports = await dbContext.DailyReports
            .AsNoTracking()
            .Include(report => report.Facts)
            .Where(report => report.TenantId == actor.TenantId &&
                report.ProjectId == projectId &&
                report.Status == DailyReportStatus.Submitted)
            .OrderBy(report => report.SubmittedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Results.Ok(reports.Select(report => DailyReportResponse.From(report)).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid projectId,
        Guid reportId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var report = await dbContext.DailyReports
            .AsNoTracking()
            .Include(item => item.Facts)
            .SingleOrDefaultAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId && item.Id == reportId,
                cancellationToken);

        return report is null ? Results.NotFound() : Results.Ok(DailyReportResponse.From(report, includeFacts: true));
    }

    private static async Task<IResult> CreateAsync(
        Guid projectId,
        CreateDailyReportRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!await projectDirectory.ExistsAsync(actor.TenantId, projectId, cancellationToken))
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "daily-reports.create",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (await dbContext.DailyReports.AnyAsync(
                report => report.TenantId == actor.TenantId && report.ProjectId == projectId &&
                    report.ReportDate == request.ReportDate && report.SupersedesReportId == null,
                cancellationToken))
        {
            return Results.Conflict(new { code = "daily_report.date.duplicate" });
        }

        var report = DailyReport.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.ReportDate,
            request.LocationName,
            request.Narrative,
            actor.UserId,
            clock.UtcNow);

        dbContext.DailyReports.Add(report);
        var response = DailyReportResponse.From(report);
        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            report,
            "DailyReportCreated",
            response,
            idempotency,
            StatusCodes.Status201Created,
            sideEffectWriter,
            clock,
            cancellationToken);

        return Results.Created($"/api/v1/projects/{projectId}/daily-reports/{report.Id}", response);
    }

    private static async Task<IResult> AddFactAsync(
        Guid projectId,
        Guid reportId,
        AddDailyFactRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IMeasurementItemDirectory measurementItemDirectory,
        IProjectLocationDirectory projectLocationDirectory,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "daily-reports.add-fact",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var report = await dbContext.DailyReports
            .Include(item => item.Facts)
            .SingleOrDefaultAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId && item.Id == reportId,
                cancellationToken);

        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.Revision != request.BaseRevision)
        {
            return RevisionConflict(report);
        }

        var measurementValidation = await ValidateMeasurementItemAsync(
            measurementItemDirectory,
            actor,
            projectId,
            request.Kind,
            request.MeasurementItemId,
            request.Unit,
            cancellationToken);
        if (measurementValidation is not null)
        {
            return measurementValidation;
        }

        if (!request.LocationId.HasValue)
        {
            return Results.UnprocessableEntity(new { code = "project.location.required" });
        }

        var location = await projectLocationDirectory.FindActiveAsync(
            actor.TenantId,
            projectId,
            request.LocationId.Value,
            cancellationToken);
        if (location is null)
        {
            return Results.UnprocessableEntity(new { code = "project.location.not_active" });
        }

        report.AddFact(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            new DailyFactInput(
                request.Kind,
                request.Description,
                request.Category,
                location.Name,
                request.Quantity,
                request.Unit,
                request.ResourceCount,
                request.Hours,
                request.ImpactLevel,
                request.ReferenceCode,
                request.MeasurementItemId,
                location.Id),
            actor.UserId,
            clock.UtcNow);

        var response = DailyReportResponse.From(report, includeFacts: true);

        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            report,
            "DailyReportFactAdded",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> SubmitAsync(
        Guid projectId,
        Guid reportId,
        SubmitDailyReportRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectPermissionRecipientDirectory recipientDirectory,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        ITransactionalNotificationWriter notificationWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.submit", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "daily-reports.submit",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var report = await dbContext.DailyReports
            .Include(item => item.Facts)
            .SingleOrDefaultAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId && item.Id == reportId,
                cancellationToken);

        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.Revision != request.BaseRevision)
        {
            return RevisionConflict(report);
        }

        report.Submit(request.BaseRevision, clock.UtcNow);
        var response = DailyReportResponse.From(report, includeFacts: true);
        var recipients = await recipientDirectory.ListAsync(
            actor.TenantId,
            projectId,
            "field.daily-reports.review",
            cancellationToken);
        var notifications = recipients.Select(recipient => CreateNotification(
            actor.TenantId,
            projectId,
            recipient.UserId,
            $"daily-report:{report.Id}:submitted:{report.Revision}",
            "DailyReportReview",
            "گزارش روزانه جدید برای بازبینی",
            "یک گزارش رسمی برای بررسی و تصمیم شما ارسال شده است.",
            report.Id,
            clock.UtcNow)).ToArray();

        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            report,
            "DailyReportSubmitted",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken,
            notificationWriter,
            notifications);

        return Results.Ok(response);
    }

    private static async Task<IResult> ReturnAsync(
        Guid projectId,
        Guid reportId,
        ReviewDailyReportRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        ITransactionalNotificationWriter notificationWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "daily-reports.return",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var report = await LoadReportAsync(dbContext, actor.TenantId, projectId, reportId, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.Revision != request.BaseRevision)
        {
            return RevisionConflict(report);
        }

        report.ReturnForCorrection(request.BaseRevision, request.Comment ?? string.Empty, actor.UserId, clock.UtcNow);
        var response = DailyReportResponse.From(report, includeFacts: true);
        InAppNotificationDraft[] notifications = report.CreatedBy == actor.UserId
            ? []
            : new[]
            {
                CreateNotification(
                    actor.TenantId,
                    projectId,
                    report.CreatedBy,
                    $"daily-report:{report.Id}:returned:{report.Revision}",
                    "DailyReportCorrection",
                    "گزارش روزانه برای اصلاح عودت شد",
                    report.ReviewComment ?? "گزارش برای اصلاح عودت شده است.",
                    report.Id,
                    clock.UtcNow)
            };
        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            report,
            "DailyReportReturned",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken,
            notificationWriter,
            notifications);

        return Results.Ok(response);
    }

    private static async Task<IResult> ApproveAsync(
        Guid projectId,
        Guid reportId,
        ReviewDailyReportRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        ITransactionalNotificationWriter notificationWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "field.daily-reports.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "daily-reports.approve",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var report = await LoadReportAsync(dbContext, actor.TenantId, projectId, reportId, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.Revision != request.BaseRevision)
        {
            return RevisionConflict(report);
        }

        var now = clock.UtcNow;
        report.Approve(request.BaseRevision, request.Comment, actor.UserId, now);
        if (report.SupersedesReportId.HasValue)
        {
            var predecessor = await LoadReportAsync(
                dbContext,
                actor.TenantId,
                projectId,
                report.SupersedesReportId.Value,
                cancellationToken);
            if (predecessor is null || predecessor.RootReportId != report.RootReportId ||
                predecessor.VersionNumber + 1 != report.VersionNumber)
            {
                throw new DomainRuleException(
                    "daily_report.correction.lineage.invalid",
                    "The correction predecessor is unavailable or inconsistent.");
            }

            predecessor.SupersedeWith(report.Id, report.CorrectionReason ?? string.Empty, now);
        }
        var response = DailyReportResponse.From(report, includeFacts: true);
        InAppNotificationDraft[] notifications = report.CreatedBy == actor.UserId
            ? []
            : new[]
            {
                CreateNotification(
                    actor.TenantId,
                    projectId,
                    report.CreatedBy,
                    $"daily-report:{report.Id}:approved:{report.Revision}",
                    "DailyReportApproved",
                    "گزارش روزانه تأیید شد",
                    report.ReviewComment ?? "گزارش روزانه پس از بازبینی تأیید شد.",
                    report.Id,
                    now)
            };
        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            report,
            "DailyReportApproved",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken,
            notificationWriter,
            notifications);

        return Results.Ok(response);
    }

    private static async Task<IResult> StartCorrectionAsync(
        Guid projectId,
        Guid reportId,
        StartDailyReportCorrectionRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        ITransactionalNotificationWriter notificationWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(
                permissionService, actor, projectId, "field.daily-reports.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext,
            actor,
            idempotencyStore,
            "daily-reports.start-correction",
            request,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var source = await LoadReportAsync(dbContext, actor.TenantId, projectId, reportId, cancellationToken);
        if (source is null)
        {
            return Results.NotFound();
        }

        if (source.Revision != request.BaseRevision)
        {
            return RevisionConflict(source);
        }

        if (await dbContext.DailyReports.AnyAsync(report =>
                report.TenantId == actor.TenantId && report.ProjectId == projectId &&
                report.SupersedesReportId == source.Id &&
                (report.Status == DailyReportStatus.Draft ||
                    report.Status == DailyReportStatus.Submitted ||
                    report.Status == DailyReportStatus.Returned),
                cancellationToken))
        {
            return Results.Conflict(new { code = "daily_report.correction.already_active" });
        }

        var correction = source.CreateCorrection(
            request.ClientGeneratedId,
            request.BaseRevision,
            request.Reason,
            actor.UserId,
            clock.UtcNow);
        dbContext.DailyReports.Add(correction);
        var response = DailyReportResponse.From(correction, includeFacts: true);
        InAppNotificationDraft[] notifications = correction.CreatedBy == actor.UserId
            ? []
            : new[]
            {
                CreateNotification(
                    actor.TenantId,
                    projectId,
                    correction.CreatedBy,
                    $"daily-report:{source.Id}:correction:{correction.Id}",
                    "DailyReportCorrection",
                    "نسخه اصلاحی گزارش روزانه ایجاد شد",
                    correction.CorrectionReason ?? "نسخه اصلاحی نیازمند تکمیل است.",
                    correction.Id,
                    clock.UtcNow)
            };
        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            correction,
            "DailyReportCorrectionStarted",
            response,
            idempotency,
            StatusCodes.Status201Created,
            sideEffectWriter,
            clock,
            cancellationToken,
            notificationWriter,
            notifications);
        return Results.Created(
            $"/api/v1/projects/{projectId}/daily-reports/{correction.Id}",
            response);
    }

    private static async Task<IResult> ReviseDetailsAsync(
        Guid projectId,
        Guid reportId,
        ReviseDailyReportDetailsRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var report = await LoadReportAsync(dbContext, actor.TenantId, projectId, reportId, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (!await CanEditAsync(permissionService, actor, projectId, report, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "daily-reports.revise-details", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (report.Revision != request.BaseRevision)
        {
            return RevisionConflict(report);
        }

        report.ReviseDetails(request.BaseRevision, request.LocationName, request.Narrative, clock.UtcNow);
        var response = DailyReportResponse.From(report, includeFacts: true);
        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            report,
            "DailyReportDetailsRevised",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> RemoveFactAsync(
        Guid projectId,
        Guid reportId,
        Guid factId,
        RemoveDailyFactRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FieldOperationsDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        var report = await LoadReportAsync(dbContext, actor.TenantId, projectId, reportId, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (!await CanEditAsync(permissionService, actor, projectId, report, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "daily-reports.remove-fact", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        if (report.Revision != request.BaseRevision)
        {
            return RevisionConflict(report);
        }

        report.RemoveFact(factId, request.BaseRevision, clock.UtcNow);
        var response = DailyReportResponse.From(report, includeFacts: true);
        await PersistOperationAsync(
            dbContext,
            httpContext,
            actor,
            report,
            "DailyReportFactRemoved",
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static Task<DailyReport?> LoadReportAsync(
        FieldOperationsDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid reportId,
        CancellationToken cancellationToken) =>
        dbContext.DailyReports
            .Include(item => item.Facts)
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == reportId,
                cancellationToken);

    private static async Task<(string Key, string Hash, string Operation, IResult? Result)> GetReplayAsync<TRequest>(
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
        var result = replay is null
            ? null
            : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        return (key, hash, operation, result);
    }

    private static async Task PersistOperationAsync(
        FieldOperationsDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        DailyReport report,
        string eventType,
        DailyReportResponse response,
        (string Key, string Hash, string Operation, IResult? Result) idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken,
        ITransactionalNotificationWriter? notificationWriter = null,
        IReadOnlyCollection<InAppNotificationDraft>? notifications = null)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    report.ProjectId,
                    actor.UserId,
                    eventType,
                    "DailyReport",
                    report.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["status"] = report.Status.ToString(),
                        ["revision"] = report.Revision,
                        ["rootReportId"] = report.RootReportId,
                        ["versionNumber"] = report.VersionNumber,
                        ["supersedesReportId"] = report.SupersedesReportId,
                        ["supersededByReportId"] = report.SupersededByReportId,
                        ["correctionReason"] = report.CorrectionReason,
                        ["correctionInitiatedBy"] = report.CorrectionInitiatedBy,
                        ["factCount"] = report.Facts.Count,
                        ["copiedFactCount"] = report.Facts.Count(fact => fact.CopiedFromFactId.HasValue),
                        ["reviewedBy"] = report.ReviewedBy,
                        ["reviewComment"] = report.ReviewComment,
                        ["notificationCount"] = notifications?.Count ?? 0,
                        ["notificationRecipientIds"] = notifications is null
                            ? Array.Empty<Guid>()
                            : notifications.Select(notification => notification.RecipientUserId).Distinct().ToArray()
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    report.ProjectId,
                    $"FieldOperations.{eventType}",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    idempotency.Operation,
                    idempotency.Hash,
                    statusCode,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        if (notificationWriter is not null && notifications is { Count: > 0 })
        {
            await notificationWriter.WriteAsync(
                dbContext.Database.GetDbConnection(),
                transaction.GetDbTransaction(),
                notifications,
                cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<bool> CanEditAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        DailyReport report,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(
                permissionService, actor, projectId, "field.daily-reports.capture", cancellationToken))
        {
            return false;
        }

        return report.CreatedBy == actor.UserId || await HasPermissionAsync(
            permissionService, actor, projectId, "field.daily-reports.review", cancellationToken);
    }

    private static InAppNotificationDraft CreateNotification(
        Guid tenantId,
        Guid projectId,
        Guid recipientUserId,
        string deduplicationKey,
        string category,
        string title,
        string body,
        Guid reportId,
        DateTimeOffset occurredAt) => new(
            Guid.NewGuid(),
            tenantId,
            projectId,
            recipientUserId,
            deduplicationKey,
            category,
            title,
            body,
            "DailyReport",
            reportId,
            occurredAt);

    private static Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            permission,
            cancellationToken);

    private static IResult RevisionConflict(DailyReport report) => Results.Conflict(new
    {
        code = "daily_report.revision.conflict",
        currentRevision = report.Revision
    });

    private static async Task<IResult?> ValidateMeasurementItemAsync(
        IMeasurementItemDirectory directory,
        ICurrentActor actor,
        Guid projectId,
        DailyFactKind kind,
        Guid? measurementItemId,
        string? unit,
        CancellationToken cancellationToken)
    {
        if (!measurementItemId.HasValue)
        {
            return null;
        }

        if (kind != DailyFactKind.WorkProgress)
        {
            return Results.UnprocessableEntity(new { code = "daily_fact.measurement_item.kind.invalid" });
        }

        var validation = await directory.ValidateAsync(
            actor.TenantId,
            projectId,
            measurementItemId.Value,
            unit,
            cancellationToken);
        return validation.IsValid
            ? null
            : Results.UnprocessableEntity(new { code = validation.ErrorCode });
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
