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
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Endpoints;

internal static class FinanceControlEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapFinanceControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var finance = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/finance").WithTags("Finance Control");
        finance.MapGet("/control-state", GetControlStateAsync);
        finance.MapGet("/verification", GetVerificationAsync);

        finance.MapGet("/obligations", ListObligationsAsync);
        finance.MapPost("/obligations", CreateObligationAsync);
        finance.MapPut("/obligations/{obligationId:guid}", AmendObligationAsync);
        finance.MapPost("/obligations/{obligationId:guid}/submit", SubmitObligationAsync);
        finance.MapPost("/obligations/{obligationId:guid}/approve", ApproveObligationAsync);
        finance.MapPost("/obligations/{obligationId:guid}/return", ReturnObligationAsync);
        finance.MapPost("/obligations/{obligationId:guid}/settlements", SettleObligationAsync);

        finance.MapGet("/petty-cash-requests", ListPettyCashAsync);
        finance.MapPost("/petty-cash-requests", CreatePettyCashAsync);
        finance.MapPut("/petty-cash-requests/{requestId:guid}", AmendPettyCashAsync);
        finance.MapPost("/petty-cash-requests/{requestId:guid}/submit", SubmitPettyCashAsync);
        finance.MapPost("/petty-cash-requests/{requestId:guid}/approve", ApprovePettyCashAsync);
        finance.MapPost("/petty-cash-requests/{requestId:guid}/return", ReturnPettyCashAsync);
        finance.MapPost("/petty-cash-requests/{requestId:guid}/advance", RecordPettyCashAdvanceAsync);
        finance.MapPost("/petty-cash-requests/{requestId:guid}/reconciliation", SubmitPettyCashReconciliationAsync);
        finance.MapPost("/petty-cash-requests/{requestId:guid}/reconciliation/approve", ApprovePettyCashReconciliationAsync);

        finance.MapGet("/management-fee-policies", ListManagementFeePoliciesAsync);
        finance.MapPost("/management-fee-policies", CreateManagementFeePolicyAsync);
        finance.MapPut("/management-fee-policies/{policyId:guid}", AmendManagementFeePolicyAsync);
        finance.MapPost("/management-fee-policies/{policyId:guid}/submit", SubmitManagementFeePolicyAsync);
        finance.MapPost("/management-fee-policies/{policyId:guid}/approve", ApproveManagementFeePolicyAsync);
        finance.MapPost("/management-fee-policies/{policyId:guid}/return", ReturnManagementFeePolicyAsync);
    }

    private static async Task<IResult> GetControlStateAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IFinanceControlReadService service,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.control.read", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var state = await service.GetCurrentAsync(actor.TenantId, projectId, cancellationToken);
        return state is null
            ? Results.NotFound(new { code = "project.not_found" })
            : Results.Ok(FinanceControlStateResponse.From(state));
    }

    private static async Task<IResult> GetVerificationAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IFinanceVerificationService service,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.verification.read", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var verification = await service.VerifyAsync(actor.TenantId, projectId, cancellationToken);
        return verification is null
            ? Results.NotFound(new { code = "project.not_found" })
            : Results.Ok(verification);
    }

    private static async Task<IResult> ListObligationsAsync(
        Guid projectId,
        FinancialObligationStatus? status,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.obligations.read", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var query = db.FinancialObligations.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        var items = await query.OrderBy(item => item.DueDate).ThenBy(item => item.Number)
            .Take(300)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(FinancialObligationResponse.From).ToArray());
    }

    private static async Task<IResult> CreateObligationAsync(
        Guid projectId,
        CreateFinancialObligationRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IProjectDirectory projects,
        IProjectLocationDirectory locations,
        ICommercialReferenceDirectory commercial,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.obligations.capture", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.obligations.create", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var validation = await ValidateReferencesAsync(
            actor.TenantId,
            projectId,
            request.CurrencyCode,
            request.PartyId,
            request.ContractId,
            request.CommitmentId,
            request.LocationId,
            projects,
            locations,
            commercial,
            cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        if (request.IssueDate > LocalDate(clock.UtcNow, validation.Project!.TimeZone))
        {
            throw new DomainRuleException("finance.obligation.issue_date.future", "Obligation issue date cannot be in the future.");
        }

        var item = FinancialObligation.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.Type,
            request.Number,
            request.Description,
            request.IssueDate,
            request.DueDate,
            request.Amount,
            validation.Project.BaseCurrencyCode,
            request.PartyId,
            request.Counterparty,
            request.ContractId,
            request.CommitmentId,
            request.CostCenterCode,
            request.WbsReference,
            request.LocationId,
            validation.Location?.Code,
            actor.UserId,
            clock.UtcNow);
        db.FinancialObligations.Add(item);
        var response = FinancialObligationResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "FinancialObligation", "FinancialObligationCreated",
            ObligationAudit(item), response, replay, StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/finance/obligations/{item.Id}", response);
    }

    private static async Task<IResult> AmendObligationAsync(
        Guid projectId,
        Guid obligationId,
        AmendFinancialObligationRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IProjectDirectory projects,
        IProjectLocationDirectory locations,
        ICommercialReferenceDirectory commercial,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.obligations.capture", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.obligations.amend", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var validation = await ValidateReferencesAsync(
            actor.TenantId, projectId, request.CurrencyCode, request.PartyId, request.ContractId,
            request.CommitmentId, request.LocationId, projects, locations, commercial, cancellationToken);
        if (validation.Result is not null)
        {
            return validation.Result;
        }

        var item = await FindObligationAsync(db, actor.TenantId, projectId, obligationId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.obligation.revision.conflict", item.Revision);
        }

        item.Amend(
            request.BaseRevision, request.Type, request.Number, request.Description, request.IssueDate,
            request.DueDate, request.Amount, validation.Project!.BaseCurrencyCode, request.PartyId,
            request.Counterparty, request.ContractId, request.CommitmentId, request.CostCenterCode,
            request.WbsReference, request.LocationId, validation.Location?.Code);
        var response = FinancialObligationResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "FinancialObligation", "FinancialObligationAmended",
            ObligationAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> SubmitObligationAsync(
        Guid projectId,
        Guid obligationId,
        SubmitFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionObligationAsync(
            projectId, obligationId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.obligations.submit", "FinancialObligationSubmitted",
            (item, at) => item.Submit(request.BaseRevision, at), cancellationToken);

    private static Task<IResult> ApproveObligationAsync(
        Guid projectId,
        Guid obligationId,
        ReviewFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionObligationAsync(
            projectId, obligationId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.obligations.review", "FinancialObligationApproved",
            (item, at) => item.Approve(request.BaseRevision, request.Comment, actor.UserId, at), cancellationToken);

    private static Task<IResult> ReturnObligationAsync(
        Guid projectId,
        Guid obligationId,
        ReviewFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionObligationAsync(
            projectId, obligationId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.obligations.review", "FinancialObligationReturned",
            (item, at) => item.ReturnForCorrection(request.BaseRevision, request.Comment ?? string.Empty, actor.UserId, at),
            cancellationToken);

    private static async Task<IResult> TransitionObligationAsync<TRequest>(
        Guid projectId,
        Guid obligationId,
        TRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        string permission,
        string eventType,
        Action<FinancialObligation, DateTimeOffset> transition,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, permission, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var operation = $"finance.obligations.{eventType}";
        var replay = await GetReplayAsync(context, actor, idempotencyStore, operation, request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindObligationAsync(db, actor.TenantId, projectId, obligationId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        transition(item, clock.UtcNow);
        var response = FinancialObligationResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "FinancialObligation", eventType,
            ObligationAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> SettleObligationAsync(
        Guid projectId,
        Guid obligationId,
        SettleFinancialObligationRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.obligations.settle", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.obligations.settle", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindObligationAsync(db, actor.TenantId, projectId, obligationId, cancellationToken);
        var record = await db.FinancialRecords.SingleOrDefaultAsync(
            candidate => candidate.TenantId == actor.TenantId && candidate.ProjectId == projectId &&
                candidate.Id == request.FinancialRecordId,
            cancellationToken);
        if (item is null || record is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.obligation.revision.conflict", item.Revision);
        }

        var expectedType = item.Type == FinancialObligationType.Payable
            ? FinancialRecordType.Payment
            : FinancialRecordType.Receipt;
        if (record.Status != FinancialRecordStatus.Posted || record.Type != expectedType ||
            !string.Equals(record.CurrencyCode, item.CurrencyCode, StringComparison.Ordinal))
        {
            return Results.UnprocessableEntity(new { code = "finance.obligation.settlement_record.invalid" });
        }

        var allocated = await db.FinancialSettlements.AsNoTracking()
            .Where(settlement => settlement.TenantId == actor.TenantId && settlement.ProjectId == projectId &&
                settlement.FinancialRecordId == record.Id)
            .SumAsync(settlement => settlement.Amount, cancellationToken);
        if (allocated + request.Amount > record.Amount)
        {
            return Results.UnprocessableEntity(new { code = "finance.obligation.settlement_record.overallocated" });
        }

        item.ApplySettlement(request.BaseRevision, request.Amount, clock.UtcNow);
        var settlement = FinancialSettlement.Create(
            Guid.NewGuid(), actor.TenantId, projectId, item.Id, record.Id, request.Amount, clock.UtcNow, actor.UserId);
        db.FinancialSettlements.Add(settlement);
        var response = FinancialObligationResponse.From(item);
        var audit = ObligationAudit(item);
        audit["settlementId"] = settlement.Id;
        audit["financialRecordId"] = record.Id;
        audit["settlementAmount"] = settlement.Amount;
        await PersistAsync(
            db, context, actor, projectId, item.Id, "FinancialObligation", "FinancialObligationSettled",
            audit, response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListPettyCashAsync(
        Guid projectId,
        PettyCashRequestStatus? status,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.petty-cash.read", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var query = db.PettyCashRequests.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        var items = await query.OrderBy(item => item.ReconciliationDueDate).ThenBy(item => item.Number)
            .Take(300)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(PettyCashRequestResponse.From).ToArray());
    }

    private static async Task<IResult> CreatePettyCashAsync(
        Guid projectId,
        CreatePettyCashRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IProjectDirectory projects,
        IProjectLocationDirectory locations,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.petty-cash.capture", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.petty-cash.create", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var project = await projects.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (!ValidCurrency(request.CurrencyCode, project.BaseCurrencyCode))
        {
            return Results.UnprocessableEntity(new { code = "finance.currency.outside_base", projectCurrency = project.BaseCurrencyCode });
        }

        var location = await ResolveLocationAsync(locations, actor.TenantId, projectId, request.LocationId, cancellationToken);
        if (request.LocationId.HasValue && location is null)
        {
            return Results.UnprocessableEntity(new { code = "finance.location.not_active" });
        }

        if (request.RequestDate > LocalDate(clock.UtcNow, project.TimeZone))
        {
            throw new DomainRuleException("finance.petty_cash.request_date.future", "Petty cash request date cannot be in the future.");
        }

        var item = PettyCashRequest.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            request.Number, request.Purpose, request.Custodian, request.RequestDate,
            request.ReconciliationDueDate, request.RequestedAmount, project.BaseCurrencyCode,
            request.LocationId, location?.Code, request.CostCenterCode, request.WbsReference,
            actor.UserId, clock.UtcNow);
        db.PettyCashRequests.Add(item);
        var response = PettyCashRequestResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "PettyCashRequest", "PettyCashRequestCreated",
            PettyCashAudit(item), response, replay, StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/finance/petty-cash-requests/{item.Id}", response);
    }

    private static async Task<IResult> AmendPettyCashAsync(
        Guid projectId,
        Guid requestId,
        AmendPettyCashRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IProjectDirectory projects,
        IProjectLocationDirectory locations,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.petty-cash.capture", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.petty-cash.amend", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var project = await projects.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (!ValidCurrency(request.CurrencyCode, project.BaseCurrencyCode))
        {
            return Results.UnprocessableEntity(new { code = "finance.currency.outside_base", projectCurrency = project.BaseCurrencyCode });
        }

        var location = await ResolveLocationAsync(locations, actor.TenantId, projectId, request.LocationId, cancellationToken);
        if (request.LocationId.HasValue && location is null)
        {
            return Results.UnprocessableEntity(new { code = "finance.location.not_active" });
        }

        var item = await FindPettyCashAsync(db, actor.TenantId, projectId, requestId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.petty_cash.revision.conflict", item.Revision);
        }

        item.Amend(
            request.BaseRevision, request.Number, request.Purpose, request.Custodian, request.RequestDate,
            request.ReconciliationDueDate, request.RequestedAmount, project.BaseCurrencyCode,
            request.LocationId, location?.Code, request.CostCenterCode, request.WbsReference);
        var response = PettyCashRequestResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "PettyCashRequest", "PettyCashRequestAmended",
            PettyCashAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> SubmitPettyCashAsync(
        Guid projectId,
        Guid requestId,
        SubmitFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionPettyCashAsync(
            projectId, requestId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.petty-cash.submit", "PettyCashRequestSubmitted",
            (item, at) => item.Submit(request.BaseRevision, at), cancellationToken);

    private static Task<IResult> ApprovePettyCashAsync(
        Guid projectId,
        Guid requestId,
        ApprovePettyCashRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionPettyCashAsync(
            projectId, requestId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.petty-cash.review", "PettyCashRequestApproved",
            (item, at) => item.Approve(request.BaseRevision, request.ApprovedAmount, request.Comment, actor.UserId, at),
            cancellationToken);

    private static Task<IResult> ReturnPettyCashAsync(
        Guid projectId,
        Guid requestId,
        ReviewFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionPettyCashAsync(
            projectId, requestId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.petty-cash.review", "PettyCashRequestReturned",
            (item, at) => item.ReturnForCorrection(request.BaseRevision, request.Comment ?? string.Empty, actor.UserId, at),
            cancellationToken);

    private static async Task<IResult> RecordPettyCashAdvanceAsync(
        Guid projectId,
        Guid requestId,
        RecordPettyCashAdvanceRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.petty-cash.advance", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.petty-cash.advance", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindPettyCashAsync(db, actor.TenantId, projectId, requestId, cancellationToken);
        var record = await FindPostedRecordAsync(db, actor.TenantId, projectId, request.AdvanceRecordId, cancellationToken);
        if (item is null || record is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.petty_cash.revision.conflict", item.Revision);
        }

        if (record.Type != FinancialRecordType.PettyCashFunding || record.Amount != item.ApprovedAmount ||
            !string.Equals(record.CurrencyCode, item.CurrencyCode, StringComparison.Ordinal))
        {
            return Results.UnprocessableEntity(new { code = "finance.petty_cash.advance_record.invalid" });
        }

        item.RecordAdvance(request.BaseRevision, record.Id, clock.UtcNow);
        var response = PettyCashRequestResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "PettyCashRequest", "PettyCashAdvanceRecorded",
            PettyCashAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> SubmitPettyCashReconciliationAsync(
        Guid projectId,
        Guid requestId,
        SubmitPettyCashReconciliationRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.petty-cash.reconcile", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.petty-cash.reconcile", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindPettyCashAsync(db, actor.TenantId, projectId, requestId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.petty_cash.revision.conflict", item.Revision);
        }

        FinancialRecord? expenseRecord = null;
        if (request.ExpenseAmount > 0)
        {
            expenseRecord = await FindPostedRecordAsync(
                db, actor.TenantId, projectId, request.ExpenseRecordId, cancellationToken);
            if (!Matches(expenseRecord, FinancialRecordType.PettyCashExpense, request.ExpenseAmount, item.CurrencyCode))
            {
                return Results.UnprocessableEntity(new { code = "finance.petty_cash.expense_record.invalid" });
            }
        }

        FinancialRecord? returnRecord = null;
        if (request.ReturnedAmount > 0 && request.ReturnRecordId.HasValue)
        {
            returnRecord = await FindPostedRecordAsync(
                db, actor.TenantId, projectId, request.ReturnRecordId.Value, cancellationToken);
            if (!Matches(returnRecord, FinancialRecordType.Receipt, request.ReturnedAmount, item.CurrencyCode))
            {
                return Results.UnprocessableEntity(new { code = "finance.petty_cash.return_record.invalid" });
            }
        }

        item.SubmitReconciliation(
            request.BaseRevision, request.ExpenseAmount, request.ReturnedAmount,
            expenseRecord?.Id ?? Guid.Empty, returnRecord?.Id, clock.UtcNow);
        var response = PettyCashRequestResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "PettyCashRequest", "PettyCashReconciliationSubmitted",
            PettyCashAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> ApprovePettyCashReconciliationAsync(
        Guid projectId,
        Guid requestId,
        ReviewFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionPettyCashAsync(
            projectId, requestId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.petty-cash.review", "PettyCashReconciliationApproved",
            (item, at) => item.ApproveReconciliation(request.BaseRevision, request.Comment, actor.UserId, at),
            cancellationToken);

    private static async Task<IResult> TransitionPettyCashAsync<TRequest>(
        Guid projectId,
        Guid requestId,
        TRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        string permission,
        string eventType,
        Action<PettyCashRequest, DateTimeOffset> transition,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, permission, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, $"finance.petty-cash.{eventType}", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindPettyCashAsync(db, actor.TenantId, projectId, requestId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        transition(item, clock.UtcNow);
        var response = PettyCashRequestResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "PettyCashRequest", eventType,
            PettyCashAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListManagementFeePoliciesAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.management-fees.read", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var items = await db.ManagementFeePolicies.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(ManagementFeePolicyResponse.From).ToArray());
    }

    private static async Task<IResult> CreateManagementFeePolicyAsync(
        Guid projectId,
        CreateManagementFeePolicyRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        IProjectDirectory projects,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.management-fees.capture", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.management-fees.create", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var project = await projects.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var item = ManagementFeePolicy.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            request.Title, request.RatePercent, request.CalculationBase, request.EffectiveFrom,
            request.Notes, actor.UserId, clock.UtcNow);
        db.ManagementFeePolicies.Add(item);
        var response = ManagementFeePolicyResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "ManagementFeePolicy", "ManagementFeePolicyCreated",
            ManagementFeeAudit(item), response, replay, StatusCodes.Status201Created, sideEffects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/finance/management-fee-policies/{item.Id}", response);
    }

    private static async Task<IResult> AmendManagementFeePolicyAsync(
        Guid projectId,
        Guid policyId,
        AmendManagementFeePolicyRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.management-fees.capture", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.management-fees.amend", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindManagementFeeAsync(db, actor.TenantId, projectId, policyId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.management_fee.revision.conflict", item.Revision);
        }

        item.Amend(
            request.BaseRevision, request.Title, request.RatePercent, request.CalculationBase,
            request.EffectiveFrom, request.Notes);
        var response = ManagementFeePolicyResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "ManagementFeePolicy", "ManagementFeePolicyAmended",
            ManagementFeeAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> SubmitManagementFeePolicyAsync(
        Guid projectId,
        Guid policyId,
        SubmitFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionManagementFeeAsync(
            projectId, policyId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.management-fees.submit", "ManagementFeePolicySubmitted",
            (item, at) => item.Submit(request.BaseRevision, at), cancellationToken);

    private static async Task<IResult> ApproveManagementFeePolicyAsync(
        Guid projectId,
        Guid policyId,
        ReviewFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, "finance.management-fees.review", cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, "finance.management-fees.approve", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindManagementFeeAsync(db, actor.TenantId, projectId, policyId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Revision != request.BaseRevision)
        {
            return RevisionConflict("finance.management_fee.revision.conflict", item.Revision);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prior = await db.ManagementFeePolicies
            .Where(candidate => candidate.TenantId == actor.TenantId && candidate.ProjectId == projectId &&
                candidate.Status == ManagementFeePolicyStatus.Approved && candidate.Id != item.Id)
            .ToListAsync(cancellationToken);
        foreach (var policy in prior)
        {
            policy.Supersede(actor.UserId, clock.UtcNow);
        }

        if (prior.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        item.Approve(request.BaseRevision, request.Comment, actor.UserId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        var response = ManagementFeePolicyResponse.From(item);
        var audit = ManagementFeeAudit(item);
        audit["supersededPolicyIds"] = prior.Select(policy => policy.Id).ToArray();
        await WriteSideEffectsAsync(
            db, transaction, context, actor, projectId, item.Id, "ManagementFeePolicy", "ManagementFeePolicyApproved",
            audit, response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> ReturnManagementFeePolicyAsync(
        Guid projectId,
        Guid policyId,
        ReviewFinanceItemRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) => TransitionManagementFeeAsync(
            projectId, policyId, request, context, actor, permissions, db, clock, sideEffects,
            idempotencyStore, "finance.management-fees.review", "ManagementFeePolicyReturned",
            (item, at) => item.ReturnForCorrection(request.BaseRevision, request.Comment ?? string.Empty, actor.UserId, at),
            cancellationToken);

    private static async Task<IResult> TransitionManagementFeeAsync<TRequest>(
        Guid projectId,
        Guid policyId,
        TRequest request,
        HttpContext context,
        ICurrentActor actor,
        IProjectPermissionService permissions,
        FinanceDbContext db,
        IClock clock,
        ITransactionalSideEffectWriter sideEffects,
        IIdempotencyStore idempotencyStore,
        string permission,
        string eventType,
        Action<ManagementFeePolicy, DateTimeOffset> transition,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(actor, permissions, projectId, permission, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var replay = await GetReplayAsync(context, actor, idempotencyStore, $"finance.management-fees.{eventType}", request, cancellationToken);
        if (replay.Result is not null)
        {
            return replay.Result;
        }

        var item = await FindManagementFeeAsync(db, actor.TenantId, projectId, policyId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        transition(item, clock.UtcNow);
        var response = ManagementFeePolicyResponse.From(item);
        await PersistAsync(
            db, context, actor, projectId, item.Id, "ManagementFeePolicy", eventType,
            ManagementFeeAudit(item), response, replay, StatusCodes.Status200OK, sideEffects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<(ProjectControlProfile? Project, ProjectLocationReference? Location, IResult? Result)> ValidateReferencesAsync(
        Guid tenantId,
        Guid projectId,
        string? currencyCode,
        Guid? partyId,
        Guid? contractId,
        Guid? commitmentId,
        Guid? locationId,
        IProjectDirectory projects,
        IProjectLocationDirectory locations,
        ICommercialReferenceDirectory commercial,
        CancellationToken cancellationToken)
    {
        var project = await projects.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            return (null, null, Results.NotFound(new { code = "project.not_found" }));
        }

        if (project.Finance != ProjectFeatureState.Active)
        {
            return (project, null, Results.UnprocessableEntity(new { code = "finance.not_active", state = project.Finance }));
        }

        if (!ValidCurrency(currencyCode, project.BaseCurrencyCode))
        {
            return (project, null, Results.UnprocessableEntity(new
            {
                code = "finance.currency.outside_base",
                projectCurrency = project.BaseCurrencyCode
            }));
        }

        var commercialValidation = await commercial.ValidateAsync(
            tenantId, projectId, contractId, commitmentId, partyId, cancellationToken);
        if (!commercialValidation.IsValid)
        {
            return (project, null, Results.UnprocessableEntity(new { code = commercialValidation.ErrorCode }));
        }

        var location = await ResolveLocationAsync(locations, tenantId, projectId, locationId, cancellationToken);
        return locationId.HasValue && location is null
            ? (project, null, Results.UnprocessableEntity(new { code = "finance.location.not_active" }))
            : (project, location, null);
    }

    private static bool ValidCurrency(string? requested, string projectCurrency) =>
        string.IsNullOrWhiteSpace(requested) ||
        string.Equals(requested.Trim(), projectCurrency, StringComparison.OrdinalIgnoreCase);

    private static Task<ProjectLocationReference?> ResolveLocationAsync(
        IProjectLocationDirectory directory,
        Guid tenantId,
        Guid projectId,
        Guid? locationId,
        CancellationToken cancellationToken) => locationId.HasValue
        ? directory.FindActiveAsync(tenantId, projectId, locationId.Value, cancellationToken)
        : Task.FromResult<ProjectLocationReference?>(null);

    private static bool Matches(
        FinancialRecord? record,
        FinancialRecordType type,
        decimal amount,
        string currencyCode) => record is not null && record.Type == type && record.Amount == amount &&
        string.Equals(record.CurrencyCode, currencyCode, StringComparison.Ordinal);

    private static DateOnly LocalDate(DateTimeOffset now, string timeZoneId) =>
        FinancialStateSource.ResolveLocalDate(now, timeZoneId);

    private static Task<FinancialObligation?> FindObligationAsync(
        FinanceDbContext db,
        Guid tenantId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) => db.FinancialObligations.SingleOrDefaultAsync(
        item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id,
        cancellationToken);

    private static Task<PettyCashRequest?> FindPettyCashAsync(
        FinanceDbContext db,
        Guid tenantId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) => db.PettyCashRequests.SingleOrDefaultAsync(
        item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id,
        cancellationToken);

    private static Task<ManagementFeePolicy?> FindManagementFeeAsync(
        FinanceDbContext db,
        Guid tenantId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) => db.ManagementFeePolicies.SingleOrDefaultAsync(
        item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id,
        cancellationToken);

    private static Task<FinancialRecord?> FindPostedRecordAsync(
        FinanceDbContext db,
        Guid tenantId,
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken) => db.FinancialRecords.SingleOrDefaultAsync(
        item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id &&
            item.Status == FinancialRecordStatus.Posted,
        cancellationToken);

    private static async Task<IResult?> AuthorizeAsync(
        ICurrentActor actor,
        IProjectPermissionService permissions,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        return await permissions.HasProjectPermissionAsync(
            actor.TenantId, actor.UserId, projectId, permission, cancellationToken)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static async Task<IdempotencyContext> GetReplayAsync<TRequest>(
        HttpContext context,
        ICurrentActor actor,
        IIdempotencyStore store,
        string operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
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

    private static async Task PersistAsync<TResponse>(
        FinanceDbContext db,
        HttpContext context,
        ICurrentActor actor,
        Guid projectId,
        Guid resourceId,
        string resourceType,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        IdempotencyContext idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffects,
        IClock clock,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await WriteSideEffectsAsync(
            db, transaction, context, actor, projectId, resourceId, resourceType, eventType,
            auditData, response, idempotency, statusCode, sideEffects, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task WriteSideEffectsAsync<TResponse>(
        FinanceDbContext db,
        IDbContextTransaction transaction,
        HttpContext context,
        ICurrentActor actor,
        Guid projectId,
        Guid resourceId,
        string resourceType,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        IdempotencyContext idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffects,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
        await sideEffects.WriteAsync(
            db.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId, projectId, actor.UserId, eventType, resourceType, resourceId.ToString(),
                    clock.UtcNow, auditData, context.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, projectId, $"Finance.{eventType}", 1,
                    clock.UtcNow, responseJson, context.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, idempotency.Key, idempotency.Operation, idempotency.Hash,
                    statusCode, responseJson, clock.UtcNow, clock.UtcNow.AddDays(7))),
            cancellationToken);
    }

    private static Dictionary<string, object?> ObligationAudit(FinancialObligation item) => new()
    {
        ["type"] = item.Type.ToString(),
        ["number"] = item.Number,
        ["issueDate"] = item.IssueDate,
        ["dueDate"] = item.DueDate,
        ["amount"] = item.Amount,
        ["settledAmount"] = item.SettledAmount,
        ["outstandingAmount"] = item.OutstandingAmount,
        ["currencyCode"] = item.CurrencyCode,
        ["partyId"] = item.PartyId,
        ["contractId"] = item.ContractId,
        ["commitmentId"] = item.CommitmentId,
        ["locationId"] = item.LocationId,
        ["locationCode"] = item.LocationCode,
        ["status"] = item.Status.ToString(),
        ["revision"] = item.Revision
    };

    private static Dictionary<string, object?> PettyCashAudit(PettyCashRequest item) => new()
    {
        ["number"] = item.Number,
        ["requestDate"] = item.RequestDate,
        ["reconciliationDueDate"] = item.ReconciliationDueDate,
        ["requestedAmount"] = item.RequestedAmount,
        ["approvedAmount"] = item.ApprovedAmount,
        ["expenseAmount"] = item.ReconciledExpenseAmount,
        ["returnedAmount"] = item.ReturnedAmount,
        ["advanceRecordId"] = item.AdvanceRecordId,
        ["expenseRecordId"] = item.ExpenseRecordId,
        ["returnRecordId"] = item.ReturnRecordId,
        ["locationId"] = item.LocationId,
        ["locationCode"] = item.LocationCode,
        ["status"] = item.Status.ToString(),
        ["revision"] = item.Revision
    };

    private static Dictionary<string, object?> ManagementFeeAudit(ManagementFeePolicy item) => new()
    {
        ["title"] = item.Title,
        ["ratePercent"] = item.RatePercent,
        ["calculationBase"] = item.CalculationBase.ToString(),
        ["effectiveFrom"] = item.EffectiveFrom,
        ["status"] = item.Status.ToString(),
        ["revision"] = item.Revision
    };

    private static IResult RevisionConflict(string code, long currentRevision) =>
        Results.Conflict(new { code, currentRevision });

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record IdempotencyContext(string Key, string Hash, string Operation, IResult? Result);
}
