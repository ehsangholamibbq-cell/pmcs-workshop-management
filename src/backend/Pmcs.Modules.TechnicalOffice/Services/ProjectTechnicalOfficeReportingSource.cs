using System.Data;
using System.Text.Json;
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
        var rfiHistories = rfis.Select(RfiHistory).ToArray();
        var submittalHistories = submittals.Select(SubmittalHistory).ToArray();
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
            rfis.Select((item, index) => new TechnicalReportingRfi(
                item.Id, item.TenantId, item.ProjectId, item.Number, item.RaisedDate,
                item.RequiredByDate, item.IsBlocking, item.PotentialImpact,
                item.RelatedRevisionIds, item.CreatedAt, rfiHistories[index] ?? [])).ToArray(),
            submittals.Select((item, index) => new TechnicalReportingSubmittal(
                item.Id, item.TenantId, item.ProjectId, item.Number, item.Type,
                item.Discipline, item.ReviewDueDate, item.ResubmissionNumber,
                item.SupersedesSubmittalId, item.RevisionIds, item.CreatedAt, submittalHistories[index] ?? [])).ToArray(),
            TechnicalReportingCompleteness.Complete, TechnicalReportingCompleteness.Complete,
            rfiHistories.All(item => item is not null) ? TechnicalReportingCompleteness.Complete : TechnicalReportingCompleteness.Incomplete,
            submittalHistories.All(item => item is not null) ? TechnicalReportingCompleteness.Complete : TechnicalReportingCompleteness.Incomplete);
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

    // A null ledger is pre-migration evidence, not a draft. Never infer missing events
    // from mutable status or response rows, even when their timestamps appear plausible.
    internal static TechnicalReportingEvent[]? RfiHistory(TechnicalRfi item)
    {
        try
        {
            var events = item.ReportingHistory?.ToArray();
            if (events is null || events.Length != item.Revision - 1) return null;
            var state = events.LastOrDefault()?.Type;
            var status = state switch
            {
                null or TechnicalReportingEventType.ReturnToDraft => RfiStatus.Draft,
                TechnicalReportingEventType.InternalReview => RfiStatus.InternalReview,
                TechnicalReportingEventType.Issued => RfiStatus.Submitted,
                TechnicalReportingEventType.ResponseReceived => RfiStatus.Answered,
                TechnicalReportingEventType.ResponseAccepted => RfiStatus.ResponseAccepted,
                TechnicalReportingEventType.ClarificationRequired => RfiStatus.ClarificationRequired,
                TechnicalReportingEventType.Closed => RfiStatus.Closed,
                _ => (RfiStatus)0
            };
            var issued = events.SingleOrDefault(e => e.Type == TechnicalReportingEventType.Issued);
            var closed = events.LastOrDefault(e => e.Type == TechnicalReportingEventType.Closed);
            var responseEvents = events.Where(e => e.Type is TechnicalReportingEventType.ResponseReceived
                or TechnicalReportingEventType.ReturnToDraft).ToArray();
            var responses = item.Responses.ToArray();
            if (status != item.Status || !SameInstant(issued?.AtUtc, item.SubmittedAt) ||
                !SameInstant(closed?.AtUtc, item.ClosedAt) ||
                responseEvents.Length != responses.Length) return null;
            for (var index = 0; index < responses.Length; index++)
            {
                var response = responses[index];
                var evidence = responseEvents[index];
                if (response.Sequence != index + 1 || !SameInstant(response.RespondedAt, evidence.AtUtc) ||
                    (response.Source == "ExternalResponse") != (evidence.Type == TechnicalReportingEventType.ResponseReceived) ||
                    response.Source == "ExternalResponse" &&
                    (response.Classification != evidence.ResponseClassification || response.ChangePotential != evidence.ChangePotential))
                    return null;
            }
            return events;
        }
        catch (JsonException) { return null; }
    }

    internal static TechnicalReportingEvent[]? SubmittalHistory(TechnicalSubmittal item)
    {
        try
        {
            var events = item.ReportingHistory?.ToArray();
            if (events is null || events.Length != item.Revision - 1) return null;
            var state = events.LastOrDefault()?.Type;
            var status = state switch
            {
                null => SubmittalStatus.Draft,
                TechnicalReportingEventType.Submitted => SubmittalStatus.Submitted,
                TechnicalReportingEventType.UnderReview => SubmittalStatus.UnderReview,
                TechnicalReportingEventType.Closed => SubmittalStatus.Closed,
                TechnicalReportingEventType.Reviewed => item.ReviewOutcome switch
                {
                    SubmittalReviewOutcome.Approved => SubmittalStatus.Approved,
                    SubmittalReviewOutcome.ApprovedAsNoted or SubmittalReviewOutcome.ForInformation => SubmittalStatus.ApprovedAsNoted,
                    SubmittalReviewOutcome.ReviseAndResubmit => SubmittalStatus.ReviseAndResubmit,
                    SubmittalReviewOutcome.Rejected => SubmittalStatus.Rejected,
                    _ => (SubmittalStatus)0
                },
                _ => (SubmittalStatus)0
            };
            if (status != item.Status ||
                !SameInstant(events.SingleOrDefault(e => e.Type == TechnicalReportingEventType.Submitted)?.AtUtc, item.SubmittedAt) ||
                !SameInstant(events.SingleOrDefault(e => e.Type == TechnicalReportingEventType.Reviewed)?.AtUtc, item.ReviewedAt) ||
                events.SingleOrDefault(e => e.Type == TechnicalReportingEventType.Reviewed)?.ReviewOutcome != item.ReviewOutcome ||
                !SameInstant(events.SingleOrDefault(e => e.Type == TechnicalReportingEventType.Closed)?.AtUtc, item.ClosedAt))
                return null;
            return events;
        }
        catch (JsonException) { return null; }
    }

    // PostgreSQL timestamptz persists microseconds; the JSON ledger may retain .NET's
    // seventh fractional digit. Compare only representable UTC precision, never dates alone.
    private static bool SameInstant(DateTimeOffset? left, DateTimeOffset? right) =>
        left.HasValue == right.HasValue && (!left.HasValue ||
            left.Value.ToUniversalTime().Ticks / 10 == right!.Value.ToUniversalTime().Ticks / 10);

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
