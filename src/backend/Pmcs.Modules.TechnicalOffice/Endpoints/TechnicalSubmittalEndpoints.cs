using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Endpoints;

internal static partial class TechnicalOfficeEndpoints
{
    private static void MapSubmittalEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/submittals", CreateSubmittalAsync);
        group.MapPost("/submittals/{submittalId:guid}/submit", SubmitSubmittalAsync);
        group.MapPost("/submittals/{submittalId:guid}/begin-review", BeginSubmittalReviewAsync);
        group.MapPost("/submittals/{submittalId:guid}/review", ReviewSubmittalAsync);
        group.MapPost("/submittals/{submittalId:guid}/close", CloseSubmittalAsync);
    }

    private static async Task<IResult> CreateSubmittalAsync(
        Guid projectId, CreateSubmittalRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects,
        ICommercialReferenceDirectory commercialDirectory, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, "technical.submittals.create", cancellationToken);
        if (access is not null) return access;
        if (!await projects.ExistsAsync(actor.TenantId, projectId, cancellationToken))
            return Results.NotFound(new { code = "project.not_found" });
        var commercial = await commercialDirectory.ValidateAsync(
            actor.TenantId, projectId, request.ContractId, request.CommitmentId, cancellationToken);
        if (!commercial.IsValid) return Results.UnprocessableEntity(new { code = commercial.ErrorCode });
        var revisionError = await ValidateRevisionReferencesAsync(
            db, actor.TenantId, projectId, request.RevisionIds, cancellationToken);
        if (revisionError is not null) return revisionError;
        if (!await CanReferenceRevisionsAsync(db, permissions, actor, projectId, request.RevisionIds, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (request.SupersedesSubmittalId.HasValue)
        {
            var previous = await FindSubmittalAsync(
                db, actor.TenantId, projectId, request.SupersedesSubmittalId.Value, cancellationToken);
            if (previous is null || previous.Status != SubmittalStatus.ReviseAndResubmit ||
                request.ResubmissionNumber != previous.ResubmissionNumber + 1)
                return Results.UnprocessableEntity(new { code = "technical.submittal.supersedes.invalid" });
        }

        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.submittals.create:{projectId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = TechnicalSubmittal.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()), actor.TenantId, projectId,
            request.Title, request.Type, request.Discipline, request.Submitter, request.Reviewer,
            request.ContractId, request.CommitmentId, request.LocationReference, request.WorkItemReference,
            request.WbsReference, request.RequiredByDate, request.PlannedSubmissionDate, request.ReviewDueDate,
            request.ResubmissionNumber, request.SupersedesSubmittalId, request.RevisionIds,
            request.RequiredDeliverableReference, actor.UserId, clock.UtcNow);
        db.Submittals.Add(item);
        var response = SubmittalResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalSubmittal", item.Id, "SubmittalCreated",
            SubmittalAudit(item), response, command!, StatusCodes.Status201Created, effects, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/technical-office/submittals/{item.Id}", response);
    }

    private static Task<IResult> SubmitSubmittalAsync(
        Guid projectId, Guid submittalId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => TransitionSubmittalAsync(
            projectId, submittalId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.submittals.submit", "submit", (item, at) => item.Submit(request.BaseRevision, at),
            "SubmittalSubmitted", cancellationToken);

    private static Task<IResult> BeginSubmittalReviewAsync(
        Guid projectId, Guid submittalId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => TransitionSubmittalAsync(
            projectId, submittalId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.submittals.review", "begin-review", (item, _) => item.BeginReview(request.BaseRevision),
            "SubmittalReviewStarted", cancellationToken);

    private static async Task<IResult> ReviewSubmittalAsync(
        Guid projectId, Guid submittalId, ReviewSubmittalRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => await TransitionSubmittalAsync(
            projectId, submittalId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.submittals.review", "review",
            (item, at) => item.RecordReview(request.BaseRevision, request.Outcome, actor.UserId, at, request.Comment),
            "SubmittalReviewed", cancellationToken);

    private static Task<IResult> CloseSubmittalAsync(
        Guid projectId, Guid submittalId, TechnicalTransitionRequest request, HttpContext context,
        ICurrentActor actor, IProjectPermissionService permissions, TechnicalOfficeDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken) => TransitionSubmittalAsync(
            projectId, submittalId, request, context, actor, permissions, db, clock, effects, idempotency,
            "technical.submittals.close", "close", (item, at) => item.Close(request.BaseRevision, at),
            "SubmittalClosed", cancellationToken);

    private static async Task<IResult> TransitionSubmittalAsync<TRequest>(
        Guid projectId, Guid submittalId, TRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, TechnicalOfficeDbContext db, IClock clock,
        ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, string permission,
        string operation, Action<TechnicalSubmittal, DateTimeOffset> transition, string eventType,
        CancellationToken cancellationToken)
    {
        var access = await RequireAsync(actor, permissions, projectId, permission, cancellationToken);
        if (access is not null) return access;
        var (command, replay) = await TechnicalOfficeEndpointSupport.CommandAsync(
            context, actor, idempotency, $"technical.submittals.{operation}:{submittalId:N}", request, cancellationToken);
        if (replay is not null) return replay;
        var item = await FindSubmittalAsync(db, actor.TenantId, projectId, submittalId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (!await CanReferenceRevisionsAsync(db, permissions, actor, projectId, item.RevisionIds, cancellationToken))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        transition(item, clock.UtcNow);
        var response = SubmittalResponse.From(item);
        await PersistAsync(db, context, actor, projectId, "TechnicalSubmittal", item.Id, eventType,
            SubmittalAudit(item), response, command!, StatusCodes.Status200OK, effects, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<TechnicalSubmittal?> FindSubmittalAsync(
        TechnicalOfficeDbContext db, Guid tenantId, Guid projectId, Guid id,
        CancellationToken cancellationToken) => db.Submittals.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.ProjectId == projectId && item.Id == id, cancellationToken);

    private static Dictionary<string, object?> SubmittalAudit(TechnicalSubmittal item) => new()
    {
        ["number"] = item.Number, ["title"] = item.Title, ["type"] = item.Type.ToString(),
        ["status"] = item.Status.ToString(), ["outcome"] = item.ReviewOutcome?.ToString(),
        ["contractId"] = item.ContractId, ["commitmentId"] = item.CommitmentId,
        ["revisionCount"] = item.RevisionIds.Count, ["resubmissionNumber"] = item.ResubmissionNumber,
        ["revision"] = item.Revision
    };
}
