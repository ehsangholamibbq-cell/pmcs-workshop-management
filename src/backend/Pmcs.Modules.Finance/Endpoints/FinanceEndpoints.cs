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
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Finance.Persistence;
using Pmcs.Modules.Finance.Services;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Endpoints;

internal static class FinanceEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapFinanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var finance = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/finance").WithTags("Finance Lite");
        finance.MapGet("/records", ListRecordsAsync);
        finance.MapPost("/records", CreateRecordAsync);
        finance.MapPut("/records/{recordId:guid}", AmendRecordAsync);
        finance.MapPost("/records/{recordId:guid}/submit", SubmitRecordAsync);
        finance.MapPost("/records/{recordId:guid}/post", PostRecordAsync);
        finance.MapPost("/records/{recordId:guid}/return", ReturnRecordAsync);
        finance.MapGet("/state", GetFinancialStateAsync);

        finance.MapGet("/budget-baselines", ListBudgetBaselinesAsync);
        finance.MapPost("/budget-baselines", CreateBudgetBaselineAsync);
        finance.MapPut("/budget-baselines/{baselineId:guid}", AmendBudgetBaselineAsync);
        finance.MapPost("/budget-baselines/{baselineId:guid}/submit", SubmitBudgetBaselineAsync);
        finance.MapPost("/budget-baselines/{baselineId:guid}/approve", ApproveBudgetBaselineAsync);
        finance.MapPost("/budget-baselines/{baselineId:guid}/return", ReturnBudgetBaselineAsync);
    }

    private static async Task<IResult> ListRecordsAsync(
        Guid projectId,
        FinancialRecordStatus? status,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FinanceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "finance.records.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = dbContext.FinancialRecords.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        var records = await query
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return Results.Ok(records.Select(FinancialRecordResponse.From).ToArray());
    }

    private static async Task<IResult> GetFinancialStateAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IFinancialStateSource source,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "financial-state.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var state = await source.GetCurrentAsync(actor.TenantId, projectId, cancellationToken);
        return state is null
            ? Results.NotFound(new { code = "project.not_found" })
            : Results.Ok(FinancialStateResponse.From(state));
    }

    private static async Task<IResult> CreateRecordAsync(
        Guid projectId,
        CreateFinancialRecordRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        IProjectLocationDirectory locationDirectory,
        ICommercialReferenceDirectory commercialReferenceDirectory,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "finance.records.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "finance.records.create", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (project.Finance != ProjectFeatureState.Active)
        {
            return Results.UnprocessableEntity(new { code = "finance.not_active", state = project.Finance });
        }

        if (request.TransactionDate > ResolveLocalDate(clock.UtcNow, project.TimeZone))
        {
            throw new DomainRuleException("finance.record.date.future", "A financial record cannot have a future local date.");
        }

        var currency = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? project.BaseCurrencyCode
            : request.CurrencyCode;
        if (!string.Equals(currency.Trim(), project.BaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return Results.UnprocessableEntity(new
            {
                code = "finance.record.currency.outside_base",
                projectCurrency = project.BaseCurrencyCode
            });
        }

        var commercialReference = await commercialReferenceDirectory.ValidateAsync(
            actor.TenantId,
            projectId,
            request.ContractId,
            request.CommitmentId,
            request.PartyId,
            cancellationToken);
        if (!commercialReference.IsValid)
        {
            return Results.UnprocessableEntity(new { code = commercialReference.ErrorCode });
        }

        var location = await ResolveLocationAsync(
            locationDirectory, actor.TenantId, projectId, request.LocationId, cancellationToken);
        if (request.LocationId.HasValue && location is null)
        {
            return Results.UnprocessableEntity(new { code = "finance.location.not_active" });
        }

        var record = FinancialRecord.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.Type,
            request.TransactionDate,
            request.Amount,
            project.BaseCurrencyCode,
            request.Description,
            request.Counterparty,
            request.DocumentNumber,
            request.ContractReference,
            request.ContractId,
            request.CommitmentId,
            request.CostCenterCode,
            actor.UserId,
            clock.UtcNow,
            request.PartyId,
            request.LocationId,
            location?.Code,
            request.WbsReference);
        dbContext.FinancialRecords.Add(record);
        var response = FinancialRecordResponse.From(record);
        await PersistSimpleAsync(
            dbContext,
            httpContext,
            actor,
            projectId,
            record.Id,
            "FinancialRecord",
            "FinancialRecordCreated",
            RecordAuditData(record),
            response,
            idempotency,
            StatusCodes.Status201Created,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/finance/records/{record.Id}", response);
    }

    private static async Task<IResult> SubmitRecordAsync(
        Guid projectId,
        Guid recordId,
        SubmitFinanceItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "finance.records.submit", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "finance.records.submit", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var record = await FindRecordAsync(dbContext, actor.TenantId, projectId, recordId, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        if (record.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.record.revision.conflict", record.Revision);
        }

        record.Submit(request.BaseRevision, clock.UtcNow);
        var response = FinancialRecordResponse.From(record);
        await PersistSimpleAsync(
            dbContext, httpContext, actor, projectId, record.Id, "FinancialRecord", "FinancialRecordSubmitted",
            RecordAuditData(record), response, idempotency, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> AmendRecordAsync(
        Guid projectId,
        Guid recordId,
        AmendFinancialRecordRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        IProjectLocationDirectory locationDirectory,
        ICommercialReferenceDirectory commercialReferenceDirectory,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "finance.records.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "finance.records.amend", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (request.TransactionDate > ResolveLocalDate(clock.UtcNow, project.TimeZone))
        {
            throw new DomainRuleException("finance.record.date.future", "A financial record cannot have a future local date.");
        }

        var currency = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? project.BaseCurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(currency, project.BaseCurrencyCode, StringComparison.Ordinal))
        {
            return Results.UnprocessableEntity(new
            {
                code = "finance.record.currency.outside_base",
                projectCurrency = project.BaseCurrencyCode
            });
        }

        var commercialReference = await commercialReferenceDirectory.ValidateAsync(
            actor.TenantId,
            projectId,
            request.ContractId,
            request.CommitmentId,
            request.PartyId,
            cancellationToken);
        if (!commercialReference.IsValid)
        {
            return Results.UnprocessableEntity(new { code = commercialReference.ErrorCode });
        }

        var location = await ResolveLocationAsync(
            locationDirectory, actor.TenantId, projectId, request.LocationId, cancellationToken);
        if (request.LocationId.HasValue && location is null)
        {
            return Results.UnprocessableEntity(new { code = "finance.location.not_active" });
        }

        var record = await FindRecordAsync(dbContext, actor.TenantId, projectId, recordId, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        if (record.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.record.revision.conflict", record.Revision);
        }

        record.Amend(
            request.BaseRevision,
            request.Type,
            request.TransactionDate,
            request.Amount,
            project.BaseCurrencyCode,
            request.Description,
            request.Counterparty,
            request.DocumentNumber,
            request.ContractReference,
            request.ContractId,
            request.CommitmentId,
            request.CostCenterCode,
            request.PartyId,
            request.LocationId,
            location?.Code,
            request.WbsReference);
        var response = FinancialRecordResponse.From(record);
        await PersistSimpleAsync(
            dbContext, httpContext, actor, projectId, record.Id, "FinancialRecord", "FinancialRecordAmended",
            RecordAuditData(record), response, idempotency, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> PostRecordAsync(
        Guid projectId,
        Guid recordId,
        ReviewFinanceItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "finance.records.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "finance.records.post", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var record = await FindRecordAsync(dbContext, actor.TenantId, projectId, recordId, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        if (record.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.record.revision.conflict", record.Revision);
        }

        record.Post(request.BaseRevision, request.Comment, actor.UserId, clock.UtcNow);
        var response = FinancialRecordResponse.From(record);
        await PersistWithSnapshotAsync(
            dbContext, project, httpContext, actor, record.Id, "FinancialRecord", "FinancialRecordPosted",
            RecordAuditData(record), response, idempotency, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ReturnRecordAsync(
        Guid projectId,
        Guid recordId,
        ReviewFinanceItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "finance.records.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "finance.records.return", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var record = await FindRecordAsync(dbContext, actor.TenantId, projectId, recordId, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        if (record.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.record.revision.conflict", record.Revision);
        }

        record.ReturnForCorrection(request.BaseRevision, request.Comment ?? string.Empty, actor.UserId, clock.UtcNow);
        var response = FinancialRecordResponse.From(record);
        await PersistSimpleAsync(
            dbContext, httpContext, actor, projectId, record.Id, "FinancialRecord", "FinancialRecordReturned",
            RecordAuditData(record), response, idempotency, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListBudgetBaselinesAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FinanceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "budget.baselines.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var baselines = await dbContext.BudgetBaselines.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Results.Ok(baselines.Select(BudgetBaselineResponse.From).ToArray());
    }

    private static async Task<IResult> CreateBudgetBaselineAsync(
        Guid projectId,
        CreateBudgetBaselineRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "budget.baselines.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "budget.baselines.create", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (project.Budget is ProjectFeatureState.NotEnabled or ProjectFeatureState.Suspended)
        {
            return Results.UnprocessableEntity(new { code = "budget.not_available", state = project.Budget });
        }

        var currency = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? project.BaseCurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(currency, project.BaseCurrencyCode, StringComparison.Ordinal))
        {
            return Results.UnprocessableEntity(new
            {
                code = "budget.baseline.currency.outside_base",
                projectCurrency = project.BaseCurrencyCode
            });
        }

        var baseline = BudgetBaseline.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.Title,
            request.Amount,
            project.BaseCurrencyCode,
            request.Notes,
            actor.UserId,
            clock.UtcNow);
        dbContext.BudgetBaselines.Add(baseline);
        var response = BudgetBaselineResponse.From(baseline);
        await PersistSimpleAsync(
            dbContext, httpContext, actor, projectId, baseline.Id, "BudgetBaseline", "BudgetBaselineCreated",
            BaselineAuditData(baseline), response, idempotency, StatusCodes.Status201Created,
            sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/finance/budget-baselines/{baseline.Id}", response);
    }

    private static async Task<IResult> SubmitBudgetBaselineAsync(
        Guid projectId,
        Guid baselineId,
        SubmitFinanceItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "budget.baselines.submit", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "budget.baselines.submit", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return RevisionConflict("budget.baseline.revision.conflict", baseline.Revision);
        }

        baseline.Submit(request.BaseRevision, clock.UtcNow);
        var response = BudgetBaselineResponse.From(baseline);
        await PersistSimpleAsync(
            dbContext, httpContext, actor, projectId, baseline.Id, "BudgetBaseline", "BudgetBaselineSubmitted",
            BaselineAuditData(baseline), response, idempotency, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> AmendBudgetBaselineAsync(
        Guid projectId,
        Guid baselineId,
        AmendBudgetBaselineRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "budget.baselines.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "budget.baselines.amend", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var currency = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? project.BaseCurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(currency, project.BaseCurrencyCode, StringComparison.Ordinal))
        {
            return Results.UnprocessableEntity(new
            {
                code = "budget.baseline.currency.outside_base",
                projectCurrency = project.BaseCurrencyCode
            });
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return RevisionConflict("budget.baseline.revision.conflict", baseline.Revision);
        }

        baseline.Amend(request.BaseRevision, request.Title, request.Amount, project.BaseCurrencyCode, request.Notes);
        var response = BudgetBaselineResponse.From(baseline);
        await PersistSimpleAsync(
            dbContext, httpContext, actor, projectId, baseline.Id, "BudgetBaseline", "BudgetBaselineAmended",
            BaselineAuditData(baseline), response, idempotency, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ApproveBudgetBaselineAsync(
        Guid projectId,
        Guid baselineId,
        ReviewFinanceItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "budget.baselines.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "budget.baselines.approve", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (project.Budget is ProjectFeatureState.NotEnabled or ProjectFeatureState.Suspended)
        {
            return Results.UnprocessableEntity(new { code = "budget.not_available", state = project.Budget });
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return RevisionConflict("budget.baseline.revision.conflict", baseline.Revision);
        }

        var response = await PersistBudgetApprovalWithSnapshotAsync(
            dbContext,
            project,
            baseline,
            request,
            httpContext,
            actor,
            idempotency,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<BudgetBaselineResponse> PersistBudgetApprovalWithSnapshotAsync(
        FinanceDbContext dbContext,
        ProjectControlProfile project,
        BudgetBaseline baseline,
        ReviewFinanceItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IdempotencyContext idempotency,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var previous = await dbContext.BudgetBaselines
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == project.Id &&
                item.Status == BudgetBaselineStatus.Approved && item.Id != baseline.Id)
            .ToListAsync(cancellationToken);
        foreach (var item in previous)
        {
            item.Supersede(actor.UserId, clock.UtcNow);
        }

        if (previous.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        baseline.Approve(request.BaseRevision, request.Comment, actor.UserId, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        var response = BudgetBaselineResponse.From(baseline);
        var snapshot = await CreateCurrentSnapshotAsync(dbContext, project, clock.UtcNow, cancellationToken);
        dbContext.FinancialStateSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
        var audit = BaselineAuditData(baseline);
        audit["supersededBaselineIds"] = previous.Select(item => item.Id).ToArray();
        audit["financialStateSnapshotId"] = snapshot.Id;
        audit["budgetComparisonState"] = snapshot.BudgetComparisonState.ToString();
        await WriteSideEffectsAsync(
            dbContext,
            transaction,
            httpContext,
            actor,
            project.Id,
            baseline.Id,
            "BudgetBaseline",
            "BudgetBaselineApproved",
            audit,
            response,
            idempotency,
            StatusCodes.Status200OK,
            sideEffectWriter,
            clock,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    private static async Task<IResult> ReturnBudgetBaselineAsync(
        Guid projectId,
        Guid baselineId,
        ReviewFinanceItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        FinanceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "budget.baselines.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, "budget.baselines.return", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return RevisionConflict("budget.baseline.revision.conflict", baseline.Revision);
        }

        baseline.ReturnForCorrection(request.BaseRevision, request.Comment ?? string.Empty, actor.UserId, clock.UtcNow);
        var response = BudgetBaselineResponse.From(baseline);
        await PersistSimpleAsync(
            dbContext, httpContext, actor, projectId, baseline.Id, "BudgetBaseline", "BudgetBaselineReturned",
            BaselineAuditData(baseline), response, idempotency, StatusCodes.Status200OK,
            sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task PersistWithSnapshotAsync<TResponse>(
        FinanceDbContext dbContext,
        ProjectControlProfile project,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid resourceId,
        string resourceType,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        IdempotencyContext idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var snapshot = await CreateCurrentSnapshotAsync(dbContext, project, clock.UtcNow, cancellationToken);
        dbContext.FinancialStateSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        var enrichedAudit = new Dictionary<string, object?>(auditData)
        {
            ["financialStateSnapshotId"] = snapshot.Id,
            ["financialStateStatus"] = snapshot.Status.ToString(),
            ["budgetComparisonState"] = snapshot.BudgetComparisonState.ToString()
        };
        await WriteSideEffectsAsync(
            dbContext, transaction, httpContext, actor, project.Id, resourceId, resourceType, eventType,
            enrichedAudit, response, idempotency, statusCode, sideEffectWriter, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<FinancialStateSnapshot> CreateCurrentSnapshotAsync(
        FinanceDbContext dbContext,
        ProjectControlProfile project,
        DateTimeOffset calculatedAt,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.FinancialRecords.AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == project.Id &&
                item.Status == FinancialRecordStatus.Posted)
            .Select(item => new PostedFinancialEntry(
                item.Id,
                item.Type,
                item.TransactionDate,
                item.Amount,
                item.CurrencyCode,
                item.ReviewedAt!.Value))
            .ToListAsync(cancellationToken);
        var baseline = await dbContext.BudgetBaselines.AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == project.Id &&
                item.Status == BudgetBaselineStatus.Approved)
            .OrderByDescending(item => item.ReviewedAt)
            .Select(item => new ApprovedBudget(
                item.Id,
                item.Amount,
                item.CurrencyCode,
                item.ReviewedAt!.Value))
            .FirstOrDefaultAsync(cancellationToken);
        var calculation = FinancialStateCalculator.Calculate(
            project,
            entries,
            baseline,
            ResolveLocalDate(calculatedAt, project.TimeZone),
            calculatedAt);
        return FinancialStateSnapshot.Create(Guid.NewGuid(), calculation);
    }

    private static async Task PersistSimpleAsync<TResponse>(
        FinanceDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid projectId,
        Guid resourceId,
        string resourceType,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        IdempotencyContext idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteSideEffectsAsync(
            dbContext, transaction, httpContext, actor, projectId, resourceId, resourceType, eventType,
            auditData, response, idempotency, statusCode, sideEffectWriter, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task WriteSideEffectsAsync<TResponse>(
        FinanceDbContext dbContext,
        IDbContextTransaction transaction,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid projectId,
        Guid resourceId,
        string resourceType,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        IdempotencyContext idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    projectId,
                    actor.UserId,
                    eventType,
                    resourceType,
                    resourceId.ToString(),
                    clock.UtcNow,
                    auditData,
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, projectId, $"Finance.{eventType}", 1,
                    clock.UtcNow, responseJson, httpContext.TraceIdentifier),
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
    }

    private static async Task<IdempotencyContext> GetReplayAsync<TRequest>(
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
            return new IdempotencyContext(
                string.Empty,
                string.Empty,
                operation,
                Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Idempotency-Key is required.",
                    extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" }));
        }

        var hash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await store.FindAsync(actor.TenantId, key, operation, hash, cancellationToken);
        return new IdempotencyContext(
            key,
            hash,
            operation,
            replay is null
                ? null
                : Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode));
    }

    private static Task<FinancialRecord?> FindRecordAsync(
        FinanceDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid recordId,
        CancellationToken cancellationToken) =>
        dbContext.FinancialRecords.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == recordId,
            cancellationToken);

    private static Task<BudgetBaseline?> FindBaselineAsync(
        FinanceDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid baselineId,
        CancellationToken cancellationToken) =>
        dbContext.BudgetBaselines.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == baselineId,
            cancellationToken);

    private static Task<bool> HasPermissionAsync(
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissionService.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, projectId, permission, cancellationToken);

    private static Dictionary<string, object?> RecordAuditData(FinancialRecord item) => new()
    {
        ["type"] = item.Type.ToString(),
        ["transactionDate"] = item.TransactionDate,
        ["amount"] = item.Amount,
        ["currencyCode"] = item.CurrencyCode,
        ["status"] = item.Status.ToString(),
        ["documentNumber"] = item.DocumentNumber,
        ["contractReference"] = item.ContractReference,
        ["contractId"] = item.ContractId,
        ["commitmentId"] = item.CommitmentId,
        ["costCenterCode"] = item.CostCenterCode,
        ["partyId"] = item.PartyId,
        ["locationId"] = item.LocationId,
        ["locationCode"] = item.LocationCode,
        ["wbsReference"] = item.WbsReference,
        ["revision"] = item.Revision
    };

    private static async Task<ProjectLocationReference?> ResolveLocationAsync(
        IProjectLocationDirectory directory,
        Guid tenantId,
        Guid projectId,
        Guid? locationId,
        CancellationToken cancellationToken) => locationId.HasValue
        ? await directory.FindActiveAsync(tenantId, projectId, locationId.Value, cancellationToken)
        : null;

    private static Dictionary<string, object?> BaselineAuditData(BudgetBaseline item) => new()
    {
        ["title"] = item.Title,
        ["amount"] = item.Amount,
        ["currencyCode"] = item.CurrencyCode,
        ["status"] = item.Status.ToString(),
        ["revision"] = item.Revision
    };

    private static IResult RevisionConflict(string code, long currentRevision) =>
        Results.Conflict(new { code, currentRevision });

    private static DateOnly ResolveLocalDate(DateTimeOffset now, string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new DomainRuleException("project.time_zone.unavailable", exception.Message);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new DomainRuleException("project.time_zone.invalid", exception.Message);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record IdempotencyContext(string Key, string Hash, string Operation, IResult? Result);
}
