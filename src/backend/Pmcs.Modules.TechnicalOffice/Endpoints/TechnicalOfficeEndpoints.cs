using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Endpoints;

internal static partial class TechnicalOfficeEndpoints
{
    public static void MapTechnicalOfficeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/technical-office")
            .WithTags("Technical Office and Document Control");
        group.MapGet("/state", GetStateAsync);
        MapDocumentEndpoints(group);
        MapRfiEndpoints(group);
        MapSubmittalEndpoints(group);
    }

    private static async Task<IResult> GetStateAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        TechnicalOfficeDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        if (!await CanAsync(permissionService, actor, projectId, "technical.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var documents = await dbContext.Documents.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderBy(item => item.Number).Take(500).ToListAsync(cancellationToken);
        var revisions = await dbContext.DocumentRevisions.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt).Take(1_000).ToListAsync(cancellationToken);
        var transmittals = await dbContext.Transmittals.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt).Take(500).ToListAsync(cancellationToken);
        var rfis = await dbContext.Rfis.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt).Take(500).ToListAsync(cancellationToken);
        var submittals = await dbContext.Submittals.AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt).Take(500).ToListAsync(cancellationToken);
        var today = ProjectLocalDate(clock.UtcNow, project.TimeZone);
        var canReadConfidential = await CanAsync(
            permissionService, actor, projectId, "technical.confidential.read", cancellationToken);
        if (!canReadConfidential)
        {
            documents = documents.Where(item => string.IsNullOrWhiteSpace(item.Confidentiality)).ToList();
            var visibleDocumentIds = documents.Select(item => item.Id).ToHashSet();
            revisions = revisions.Where(item => visibleDocumentIds.Contains(item.DocumentId)).ToList();
            var visibleRevisionIds = revisions.Select(item => item.Id).ToHashSet();
            transmittals = transmittals.Where(item => item.RevisionIds.All(visibleRevisionIds.Contains)).ToList();
            rfis = rfis.Where(item => item.RelatedRevisionIds.All(visibleRevisionIds.Contains) &&
                item.Responses.All(response => response.ReferencedRevisionIds.All(visibleRevisionIds.Contains))).ToList();
            submittals = submittals.Where(item => item.RevisionIds.All(visibleRevisionIds.Contains)).ToList();
        }

        return Results.Ok(new TechnicalOfficeStateResponse(
            documents.Select(TechnicalDocumentResponse.From).ToArray(),
            revisions.Select(DocumentRevisionResponse.From).ToArray(),
            transmittals.Select(TransmittalResponse.From).ToArray(),
            rfis.Select(RfiResponse.From).ToArray(),
            submittals.Select(SubmittalResponse.From).ToArray(),
            rfis.Count(item => item.IsBlocking && item.Status != RfiStatus.Closed),
            rfis.Count(item => item.Status != RfiStatus.Closed && item.RequiredByDate.HasValue && item.RequiredByDate < today),
            submittals.Count(item => (item.Status is SubmittalStatus.Submitted or SubmittalStatus.UnderReview) &&
                item.ReviewDueDate.HasValue && item.ReviewDueDate < today),
            revisions.Count(item => item.Status == DocumentRevisionStatus.Superseded)));
    }

    private static Task<bool> CanAsync(
        IProjectPermissionService service,
        ICurrentActor actor,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        service.HasProjectPermissionAsync(actor.TenantId, actor.UserId, projectId, permission, cancellationToken);

    private static async Task<bool> CanReferenceRevisionsAsync(
        TechnicalOfficeDbContext dbContext,
        IProjectPermissionService permissionService,
        ICurrentActor actor,
        Guid projectId,
        IReadOnlyCollection<Guid>? revisionIds,
        CancellationToken cancellationToken)
    {
        var ids = revisionIds?.Distinct().ToArray() ?? [];
        if (ids.Length == 0)
        {
            return true;
        }

        var containsConfidential = await (
            from revision in dbContext.DocumentRevisions.AsNoTracking()
            join document in dbContext.Documents.AsNoTracking() on revision.DocumentId equals document.Id
            where revision.TenantId == actor.TenantId && revision.ProjectId == projectId &&
                ids.Contains(revision.Id) && document.Confidentiality != null
            select revision.Id).AnyAsync(cancellationToken);
        return !containsConfidential || await CanAsync(
            permissionService, actor, projectId, "technical.confidential.read", cancellationToken);
    }

    private static DateOnly ProjectLocalDate(DateTimeOffset instant, string timeZone)
    {
        try
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
    }
}
