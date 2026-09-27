using System.Data;
using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.TechnicalOffice.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice.Services;

internal sealed class ProjectTechnicalOfficeReportingSource(
    TechnicalOfficeDbContext db, IProjectDirectory projects) : IProjectTechnicalOfficeReportingSource
{
    public async Task<ProjectTechnicalOfficeReportingResult> LoadAsync(
        Guid tenantId, Guid projectId, DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(ProjectTechnicalOfficeReportingContract.MaximumReadSeconds));
        cancellationToken = timeout.Token;
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var project = await projects.FindProfileAsync(tenantId, projectId, cancellationToken)
            ?? throw ProjectTechnicalOfficeReportingSelector.Invalid("project.not_found", "Project scope is missing.");
        if (cutoff == default || cutoff > DateTimeOffset.UtcNow ||
            project.ConfigurationVersion <= 0 || project.ConfigurationChangedAt is null ||
            project.ConfigurationChangedAt > cutoff)
            throw ProjectTechnicalOfficeReportingSelector.Invalid(
                "configuration.history", "Project configuration is not provable at the requested cutoff.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone); }
        catch (TimeZoneNotFoundException)
        {
            throw ProjectTechnicalOfficeReportingSelector.Invalid("time_zone.invalid", "Unknown Project time zone.");
        }
        catch (InvalidTimeZoneException)
        {
            throw ProjectTechnicalOfficeReportingSelector.Invalid("time_zone.invalid", "Invalid Project time zone.");
        }
        if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, zone).DateTime) != cutoffLocalDate)
            throw ProjectTechnicalOfficeReportingSelector.Invalid("cutoff.invalid", "Project local cutoff mismatch.");

        // One repeatable-read view of all five registers. Never use the capped GET /state endpoint.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var documents = await db.Documents.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.CreatedAt <= cutoff)
            .OrderBy(item => item.Id).Take(ProjectTechnicalOfficeReportingContract.MaximumDocuments + 1)
            .ToArrayAsync(cancellationToken);
        var revisions = await db.DocumentRevisions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.CreatedAt <= cutoff)
            .OrderBy(item => item.Id).Take(ProjectTechnicalOfficeReportingContract.MaximumRevisions + 1)
            .ToArrayAsync(cancellationToken);
        var transmittals = await db.Transmittals.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.CreatedAt <= cutoff)
            .OrderBy(item => item.Id).Take(ProjectTechnicalOfficeReportingContract.MaximumTransmittals + 1)
            .ToArrayAsync(cancellationToken);
        var rfis = await db.Rfis.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.CreatedAt <= cutoff)
            .OrderBy(item => item.Id).Take(ProjectTechnicalOfficeReportingContract.MaximumRfis + 1)
            .ToArrayAsync(cancellationToken);
        var submittals = await db.Submittals.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.CreatedAt <= cutoff)
            .OrderBy(item => item.Id).Take(ProjectTechnicalOfficeReportingContract.MaximumSubmittals + 1)
            .ToArrayAsync(cancellationToken);
        var projection = new ProjectTechnicalOfficeReportingProjection(
            ProjectTechnicalOfficeReportingContract.Version, tenantId, projectId, cutoffLocalDate, cutoff,
            project.ConfigurationVersion, project.ConfigurationChangedAt.Value.ToUniversalTime(), true,
            TechnicalReportingClassification.Confidential, false,
            documents.Select(item => new TechnicalReportingDocument(
                item.Id, item.TenantId, item.ProjectId, item.Number, item.Type,
                item.Discipline, item.CreatedAt, Classification(item.Confidentiality))).ToArray(),
            revisions.Select(Revision).ToArray(),
            transmittals.Select(item => new TechnicalReportingTransmittal(
                item.Id, item.TenantId, item.ProjectId, item.Number,
                item.RevisionIds, item.DueResponseDate, item.CreatedAt, TransmittalEvents(item))).ToArray(),
            rfis.Select(item => new TechnicalReportingRfi(
                item.Id, item.TenantId, item.ProjectId, item.Number, item.RaisedDate,
                item.RequiredByDate, item.IsBlocking, item.PotentialImpact,
                item.RelatedRevisionIds, item.CreatedAt, [])).ToArray(),
            submittals.Select(item => new TechnicalReportingSubmittal(
                item.Id, item.TenantId, item.ProjectId, item.Number, item.Type,
                item.Discipline, item.ReviewDueDate, item.ResubmissionNumber,
                item.SupersedesSubmittalId, item.RevisionIds, item.CreatedAt, [])).ToArray(),
            TechnicalReportingCompleteness.Complete, TechnicalReportingCompleteness.Complete,
            rfis.Length == 0 ? TechnicalReportingCompleteness.Complete : TechnicalReportingCompleteness.Incomplete,
            submittals.Length == 0 ? TechnicalReportingCompleteness.Complete : TechnicalReportingCompleteness.Incomplete);
        var result = ProjectTechnicalOfficeReportingCalculator.Calculate(projection);
        await transaction.CommitAsync(cancellationToken);
        var current = await projects.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (current is null || current.Revision != project.Revision ||
            current.ConfigurationVersion != project.ConfigurationVersion ||
            current.TimeZone != project.TimeZone)
            throw ProjectTechnicalOfficeReportingSelector.Invalid(
                "project.changed", "Project profile changed while collecting Technical Office evidence.");
        return result;
    }

    private static TechnicalReportingClassification Classification(string? value) => value switch
    {
        null or "Internal" or "Confidential" => TechnicalReportingClassification.Confidential,
        "Restricted" => TechnicalReportingClassification.Restricted,
        _ => throw ProjectTechnicalOfficeReportingSelector.Invalid(
            "classification.unknown", "Document classification has no approved versioned mapping.")
    };

    private static TechnicalReportingRevision Revision(TechnicalDocumentRevision item)
    {
        var events = new List<TechnicalReportingEvent>();
        if (item.SubmittedAt.HasValue)
            events.Add(new(0, TechnicalReportingEventType.Submitted, item.SubmittedAt.Value));
        if (item.ReviewedAt.HasValue)
            events.Add(new(0,
                item.Status == DocumentRevisionStatus.Returned ?
                    TechnicalReportingEventType.Returned : TechnicalReportingEventType.Approved,
                item.ReviewedAt.Value));
        if (item.IssuedAt.HasValue)
            events.Add(new(0, TechnicalReportingEventType.Issued, item.IssuedAt.Value));
        if (item.SupersededAt.HasValue)
            events.Add(new(0, TechnicalReportingEventType.Superseded, item.SupersededAt.Value));
        return new TechnicalReportingRevision(
            item.Id, item.TenantId, item.ProjectId, item.DocumentId, item.RevisionCode,
            item.RevisionDate, item.Purpose, item.SupersedesRevisionId,
            item.IssuedThroughTransmittalId, item.CreatedAt,
            events.OrderBy(e => e.AtUtc).Select((e, index) => e with { Sequence = index + 1 }).ToArray());
    }

    private static TechnicalReportingEvent[] TransmittalEvents(TechnicalTransmittal item)
    {
        var events = new List<TechnicalReportingEvent>();
        if (item.IssuedAt.HasValue)
            events.Add(new(1, TechnicalReportingEventType.Issued, item.IssuedAt.Value));
        if (item.AcknowledgedAt.HasValue)
            events.Add(new(2, TechnicalReportingEventType.Acknowledged, item.AcknowledgedAt.Value));
        return events.ToArray();
    }
}
