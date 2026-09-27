using System.Text.Json;
using Pmcs.Modules.TechnicalOffice.Contracts;

namespace Pmcs.Modules.TechnicalOffice.Services;

internal static class ProjectTechnicalOfficeReportingCalculator
{
    internal static ProjectTechnicalOfficeReportingResult Calculate(
        ProjectTechnicalOfficeReportingProjection projection)
    {
        var source = ProjectTechnicalOfficeReportingSelector.Select(projection);
        var cutoff = source.SourceCutoffUtc;
        var reasons = new SortedSet<TechnicalReportingReason>();
        var revisions = source.Revisions.ToDictionary(item => item.Id);
        var transmittals = source.Transmittals.ToDictionary(item => item.Id);

        var documentCoverage = source.DocumentCompleteness == TechnicalReportingCompleteness.Complete &&
            source.TransmittalCompleteness == TechnicalReportingCompleteness.Complete;
        var documentRows = new List<TechnicalReportingDocumentRow>();
        var transmittalRows = new List<TechnicalReportingTransmittalRow>();
        if (source.SourceEnabled && documentCoverage)
        {
            var issued = new List<(TechnicalReportingRevision Revision, TechnicalReportingEvent Event)>();
            foreach (var revision in source.Revisions)
            {
                var state = RevisionState(revision.Events);
                var issue = revision.Events.SingleOrDefault(item => item.Type == TechnicalReportingEventType.Issued);
                if (issue is null)
                {
                    if (revision.IssuedThroughTransmittalId.HasValue &&
                        !revision.Events.Any(item => item.Type == TechnicalReportingEventType.Approved))
                        throw ProjectTechnicalOfficeReportingSelector.Invalid(
                            "revision.issue.invalid", "A revision has an issue link without an approval history.");
                    continue;
                }
                if (state is not TechnicalReportingEventType.Issued and not TechnicalReportingEventType.Superseded ||
                    !revision.IssuedThroughTransmittalId.HasValue ||
                    !transmittals.TryGetValue(revision.IssuedThroughTransmittalId.Value, out var transmittal) ||
                    !transmittal.RevisionIds.Contains(revision.Id) ||
                    !transmittal.Events.Any(item => item.Type == TechnicalReportingEventType.Issued &&
                        item.AtUtc == issue.AtUtc))
                    throw ProjectTechnicalOfficeReportingSelector.Invalid(
                        "revision.issue.invalid", "Revision issue and Transmittal lineage disagree.");
                issued.Add((revision, issue));
            }

            foreach (var transmittal in source.Transmittals)
            {
                var (issue, ack) = TransmittalState(transmittal.Events);
                if (issue is null) continue;
                foreach (var revisionId in transmittal.RevisionIds)
                {
                    if (!issued.Any(item => item.Revision.Id == revisionId && item.Event.AtUtc == issue.AtUtc))
                        throw ProjectTechnicalOfficeReportingSelector.Invalid(
                            "transmittal.issue.invalid", "Issued Transmittal contains a revision without reciprocal approval/issue.");
                }
                var due = ack is not null ? TechnicalReportingDueState.Acknowledged :
                    transmittal.DueResponseDate is null ? TechnicalReportingDueState.NotAssessable :
                    transmittal.DueResponseDate < source.CutoffLocalDate ? TechnicalReportingDueState.Overdue :
                    TechnicalReportingDueState.NotDue;
                if (due == TechnicalReportingDueState.NotAssessable)
                    reasons.Add(TechnicalReportingReason.DueDateUnavailable);
                transmittalRows.Add(new TechnicalReportingTransmittalRow(
                    transmittal.Id, transmittal.Number, issue.AtUtc,
                    transmittal.RevisionIds.Count, ack?.AtUtc, due));
            }

            foreach (var document in source.Documents)
            {
                var history = issued.Where(item => item.Revision.DocumentId == document.Id)
                    .OrderBy(item => item.Event.AtUtc).ThenBy(item => item.Revision.Id).ToArray();
                (TechnicalReportingRevision Revision, TechnicalReportingEvent Event)? current = null;
                foreach (var item in history)
                {
                    if (current is not null)
                    {
                        var old = current.Value.Revision;
                        if (item.Revision.SupersedesRevisionId != old.Id ||
                            !old.Events.Any(e => e.Type == TechnicalReportingEventType.Superseded &&
                                e.AtUtc == item.Event.AtUtc))
                            throw ProjectTechnicalOfficeReportingSelector.Invalid(
                                "revision.official.invalid", "Official revision supersession is missing or contradictory.");
                    }
                    else if (item.Revision.SupersedesRevisionId.HasValue &&
                        revisions[item.Revision.SupersedesRevisionId.Value].Events
                            .Any(e => e.Type == TechnicalReportingEventType.Issued))
                        throw ProjectTechnicalOfficeReportingSelector.Invalid(
                            "revision.official.invalid", "Official revision predecessor is not current.");
                    current = item;
                }
                if (current is not null)
                {
                    var (revision, issue) = current.Value;
                    if (revision.Events.Any(item => item.Type == TechnicalReportingEventType.Superseded))
                        throw ProjectTechnicalOfficeReportingSelector.Invalid(
                            "revision.official.invalid", "Last issued revision cannot already be superseded.");
                    documentRows.Add(new TechnicalReportingDocumentRow(
                        document.Id, document.Number, document.Type, document.Discipline,
                        revision.Id, revision.Code, revision.RevisionDate, issue.AtUtc));
                }
            }
        }

        var rfiRows = new List<TechnicalReportingRfiRow>();
        if (source.SourceEnabled && source.RfiCompleteness == TechnicalReportingCompleteness.Complete)
        {
            foreach (var rfi in source.Rfis)
            {
                var (state, issuedAt, responseCount, lastClassification) = RfiState(rfi.Events);
                if (!issuedAt.HasValue) continue;
                bool? overdue = state == TechnicalReportingEventType.Closed ? false :
                    rfi.RequiredByDate.HasValue ? rfi.RequiredByDate < source.CutoffLocalDate : null;
                if (overdue is null) reasons.Add(TechnicalReportingReason.DueDateUnavailable);
                rfiRows.Add(new TechnicalReportingRfiRow(
                    rfi.Id, rfi.Number, state, issuedAt.Value, responseCount,
                    lastClassification, rfi.IsBlocking, overdue));
            }
        }

        var submittalRows = new List<TechnicalReportingSubmittalRow>();
        if (source.SourceEnabled && source.SubmittalCompleteness == TechnicalReportingCompleteness.Complete)
        {
            foreach (var submittal in source.Submittals)
            {
                var (state, outcome) = SubmittalState(submittal.Events);
                if (state is null) continue;
                bool? overdue = state is TechnicalReportingSubmittalState.Submitted or
                    TechnicalReportingSubmittalState.UnderReview ?
                    submittal.ReviewDueDate.HasValue ?
                        submittal.ReviewDueDate < source.CutoffLocalDate : null : false;
                if (overdue is null) reasons.Add(TechnicalReportingReason.DueDateUnavailable);
                submittalRows.Add(new TechnicalReportingSubmittalRow(
                    submittal.Id, submittal.Number, submittal.Type, submittal.Discipline,
                    state.Value, outcome, submittal.ResubmissionNumber, overdue));
            }
        }

        var documents = Section(source.SourceEnabled, documentCoverage, documentRows,
            TechnicalReportingReason.NoOfficialDocumentRevision, reasons);
        var transmittalSection = Section(source.SourceEnabled, documentCoverage, transmittalRows,
            TechnicalReportingReason.NoIssuedTransmittal, reasons);
        var rfis = Section(source.SourceEnabled,
            source.RfiCompleteness == TechnicalReportingCompleteness.Complete, rfiRows,
            TechnicalReportingReason.NoIssuedRfi, reasons);
        var submittals = Section(source.SourceEnabled,
            source.SubmittalCompleteness == TechnicalReportingCompleteness.Complete, submittalRows,
            TechnicalReportingReason.NoSubmittedSubmittal, reasons);
        var statuses = new[] { documents.Status, transmittalSection.Status, rfis.Status, submittals.Status };
        var status = statuses.All(item => item == TechnicalReportingStatus.NotConfigured)
            ? TechnicalReportingStatus.NotConfigured
            : statuses.Contains(TechnicalReportingStatus.InsufficientData)
                ? TechnicalReportingStatus.InsufficientData
                : statuses.All(item => item == TechnicalReportingStatus.NoData)
                    ? TechnicalReportingStatus.NoData : TechnicalReportingStatus.Available;
        var manifest = Manifest(source);
        if (JsonSerializer.SerializeToUtf8Bytes(manifest).Length >
            ProjectTechnicalOfficeReportingContract.MaximumManifestBytes)
            throw ProjectTechnicalOfficeReportingSelector.Invalid("manifest.budget", "Source manifest byte budget exceeded.");
        var manifestHash = TechnicalReportingHash.Compute(manifest);
        var orderedReasons = reasons.ToArray();
        var result = new ProjectTechnicalOfficeReportingResult(
            ProjectTechnicalOfficeReportingContract.Version,
            ProjectTechnicalOfficeReportingContract.PolicyVersion,
            source.TenantId, source.ProjectId, source.CutoffLocalDate, cutoff,
            source.Classification, status, orderedReasons, documents,
            transmittalSection, rfis, submittals, manifest, manifestHash, string.Empty);
        return result with { SemanticSha256 = TechnicalReportingHash.ComputeResult(result) };
    }

    private static TechnicalReportingSection<T> Section<T>(
        bool enabled, bool complete, IReadOnlyCollection<T> rows,
        TechnicalReportingReason noDataReason, SortedSet<TechnicalReportingReason> allReasons)
    {
        var reasons = new List<TechnicalReportingReason>();
        var status = !enabled ? TechnicalReportingStatus.NotConfigured :
            !complete ? TechnicalReportingStatus.InsufficientData :
            rows.Count == 0 ? TechnicalReportingStatus.NoData : TechnicalReportingStatus.Available;
        if (status == TechnicalReportingStatus.NotConfigured)
            reasons.Add(TechnicalReportingReason.TechnicalSourceNotConfigured);
        if (status == TechnicalReportingStatus.InsufficientData)
        {
            reasons.Add(TechnicalReportingReason.HistoricalTransitionUnavailable);
            reasons.Add(TechnicalReportingReason.SourceCoverageIncomplete);
        }
        if (status == TechnicalReportingStatus.NoData) reasons.Add(noDataReason);
        foreach (var reason in reasons) allReasons.Add(reason);
        return new TechnicalReportingSection<T>(status,
            complete && enabled ? rows.Count : null,
            complete && enabled ? rows : Array.Empty<T>(), reasons);
    }

    private static ProjectTechnicalOfficeSourceManifest Manifest(ProjectTechnicalOfficeReportingProjection source)
    {
        TechnicalReportingManifestRegister Collection<T>(
            string name, TechnicalReportingCompleteness completeness, IReadOnlyCollection<T> values,
            Func<T, Guid> id, Func<T, int> eventCount)
        {
            var entries = values.Select(item => new TechnicalReportingManifestEntry(
                id(item), TechnicalReportingHash.Compute(item))).OrderBy(item => item.Id).ToArray();
            return new TechnicalReportingManifestRegister(name, completeness,
                entries.Length, values.Sum(eventCount), entries.FirstOrDefault()?.Id,
                entries.LastOrDefault()?.Id, entries);
        }
        return new ProjectTechnicalOfficeSourceManifest(
            ProjectTechnicalOfficeReportingContract.ManifestVersion,
            ProjectTechnicalOfficeReportingContract.Version,
            ProjectTechnicalOfficeReportingContract.PolicyVersion,
            source.TenantId, source.ProjectId, source.CutoffLocalDate, source.SourceCutoffUtc,
            source.ConfigurationVersion, source.ConfigurationEffectiveAtUtc.ToUniversalTime(),
            source.SourceEnabled, source.Classification, source.RestrictedPublicationApproved,
            source.SourceCutoffUtc,
            [
                Collection("documents", source.DocumentCompleteness, source.Documents, item => item.Id, _ => 0),
                Collection("revisions", source.DocumentCompleteness, source.Revisions, item => item.Id, item => item.Events.Count),
                Collection("transmittals", source.TransmittalCompleteness, source.Transmittals, item => item.Id, item => item.Events.Count),
                Collection("rfis", source.RfiCompleteness, source.Rfis, item => item.Id, item => item.Events.Count),
                Collection("submittals", source.SubmittalCompleteness, source.Submittals, item => item.Id, item => item.Events.Count)
            ]);
    }

    private static TechnicalReportingEventType? RevisionState(IReadOnlyCollection<TechnicalReportingEvent> events)
    {
        TechnicalReportingEventType? state = null;
        foreach (var item in events.OrderBy(item => item.Sequence))
        {
            var valid = (state, item.Type) switch
            {
                (null, TechnicalReportingEventType.Submitted) => true,
                (TechnicalReportingEventType.Submitted, TechnicalReportingEventType.Approved or TechnicalReportingEventType.Returned) => true,
                (TechnicalReportingEventType.Approved, TechnicalReportingEventType.Issued) => true,
                (TechnicalReportingEventType.Issued, TechnicalReportingEventType.Superseded) => true,
                _ => false
            };
            if (!valid) throw ProjectTechnicalOfficeReportingSelector.Invalid(
                "revision.events.invalid", "Revision transition is not a valid formal workflow.");
            state = item.Type;
        }
        return state;
    }

    private static (TechnicalReportingEvent? Issue, TechnicalReportingEvent? Ack) TransmittalState(
        IReadOnlyCollection<TechnicalReportingEvent> events)
    {
        TechnicalReportingEvent? issue = null, ack = null;
        foreach (var item in events.OrderBy(item => item.Sequence))
        {
            if (item.Type == TechnicalReportingEventType.Issued && issue is null) issue = item;
            else if (item.Type == TechnicalReportingEventType.Acknowledged && issue is not null && ack is null)
                ack = item;
            else throw ProjectTechnicalOfficeReportingSelector.Invalid(
                "transmittal.events.invalid", "Transmittal issue/ack chronology is invalid.");
        }
        return (issue, ack);
    }

    private static (TechnicalReportingEventType State, DateTimeOffset? IssuedAt, int Responses,
        Pmcs.Modules.TechnicalOffice.Domain.RfiResponseClassification? LastClassification) RfiState(
        IReadOnlyCollection<TechnicalReportingEvent> events)
    {
        var state = TechnicalReportingEventType.ReturnToDraft;
        DateTimeOffset? issue = null;
        int responses = 0;
        Pmcs.Modules.TechnicalOffice.Domain.RfiResponseClassification? last = null;
        foreach (var item in events.OrderBy(item => item.Sequence))
        {
            var valid = (state, item.Type) switch
            {
                (TechnicalReportingEventType.ReturnToDraft, TechnicalReportingEventType.InternalReview) => true,
                (TechnicalReportingEventType.InternalReview, TechnicalReportingEventType.ReturnToDraft or TechnicalReportingEventType.Issued) => true,
                (TechnicalReportingEventType.Issued or TechnicalReportingEventType.ClarificationRequired,
                    TechnicalReportingEventType.ResponseReceived) => item.ResponseClassification.HasValue,
                (TechnicalReportingEventType.ResponseReceived,
                    TechnicalReportingEventType.ResponseAccepted or TechnicalReportingEventType.ClarificationRequired) => true,
                (TechnicalReportingEventType.ResponseAccepted, TechnicalReportingEventType.Closed) => true,
                _ => false
            };
            if (!valid) throw ProjectTechnicalOfficeReportingSelector.Invalid(
                "rfi.events.invalid", "RFI event lineage is incomplete or contradictory.");
            if (item.Type == TechnicalReportingEventType.Issued) issue = item.AtUtc;
            if (item.Type == TechnicalReportingEventType.ResponseReceived)
            {
                responses++;
                last = item.ResponseClassification;
            }
            state = item.Type;
        }
        return (state, issue, responses, last);
    }

    private static (TechnicalReportingSubmittalState? State,
        Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome? Outcome) SubmittalState(
        IReadOnlyCollection<TechnicalReportingEvent> events)
    {
        TechnicalReportingEventType? state = null;
        Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome? outcome = null;
        foreach (var item in events.OrderBy(item => item.Sequence))
        {
            var valid = (state, item.Type) switch
            {
                (null, TechnicalReportingEventType.Submitted) => true,
                (TechnicalReportingEventType.Submitted, TechnicalReportingEventType.UnderReview) => true,
                (TechnicalReportingEventType.UnderReview, TechnicalReportingEventType.Reviewed) =>
                    item.ReviewOutcome.HasValue,
                (TechnicalReportingEventType.Reviewed, TechnicalReportingEventType.Closed) =>
                    outcome is Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.Approved or
                        Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.ApprovedAsNoted or
                        Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.ForInformation or
                        Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.Rejected,
                _ => false
            };
            if (!valid) throw ProjectTechnicalOfficeReportingSelector.Invalid(
                "submittal.events.invalid", "Submittal review lineage is incomplete or contradictory.");
            if (item.Type == TechnicalReportingEventType.Reviewed) outcome = item.ReviewOutcome;
            state = item.Type;
        }
        if (state is null) return (null, null);
        var official = state switch
        {
            TechnicalReportingEventType.Submitted => TechnicalReportingSubmittalState.Submitted,
            TechnicalReportingEventType.UnderReview => TechnicalReportingSubmittalState.UnderReview,
            TechnicalReportingEventType.Closed => TechnicalReportingSubmittalState.Closed,
            TechnicalReportingEventType.Reviewed => outcome switch
            {
                Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.Approved =>
                    TechnicalReportingSubmittalState.Approved,
                Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.ApprovedAsNoted =>
                    TechnicalReportingSubmittalState.ApprovedAsNoted,
                Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.ReviseAndResubmit =>
                    TechnicalReportingSubmittalState.ReviseAndResubmit,
                Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.Rejected =>
                    TechnicalReportingSubmittalState.Rejected,
                Pmcs.Modules.TechnicalOffice.Domain.SubmittalReviewOutcome.ForInformation =>
                    TechnicalReportingSubmittalState.ForInformation,
                _ => throw ProjectTechnicalOfficeReportingSelector.Invalid(
                    "submittal.outcome.invalid", "Submittal review outcome is unknown.")
            },
            _ => throw ProjectTechnicalOfficeReportingSelector.Invalid(
                "submittal.state.invalid", "Submittal status is unknown.")
        };
        return (official, outcome);
    }
}
