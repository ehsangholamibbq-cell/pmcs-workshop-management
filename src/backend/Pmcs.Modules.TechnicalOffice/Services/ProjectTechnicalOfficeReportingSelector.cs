using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.TechnicalOffice.Contracts;

namespace Pmcs.Modules.TechnicalOffice.Services;

internal static class ProjectTechnicalOfficeReportingSelector
{
    internal static ProjectTechnicalOfficeReportingProjection Select(ProjectTechnicalOfficeReportingProjection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = source.SourceCutoffUtc.ToUniversalTime();
        if (source.ContractVersion != ProjectTechnicalOfficeReportingContract.Version ||
            source.TenantId == Guid.Empty || source.ProjectId == Guid.Empty ||
            source.CutoffLocalDate == default || cutoff == default ||
            cutoff > DateTimeOffset.UtcNow || source.ConfigurationVersion <= 0 ||
            source.ConfigurationEffectiveAtUtc == default || source.ConfigurationEffectiveAtUtc > cutoff ||
            source.Documents is null || source.Revisions is null || source.Transmittals is null ||
            source.Rfis is null || source.Submittals is null ||
            !Enum.IsDefined(source.Classification) ||
            !Enum.IsDefined(source.DocumentCompleteness) ||
            !Enum.IsDefined(source.TransmittalCompleteness) ||
            !Enum.IsDefined(source.RfiCompleteness) ||
            !Enum.IsDefined(source.SubmittalCompleteness))
        {
            throw Invalid("source.invalid", "Technical Office reporting scope or source version is invalid.");
        }
        if (!source.SourceEnabled && (source.Documents.Count != 0 || source.Revisions.Count != 0 ||
            source.Transmittals.Count != 0 || source.Rfis.Count != 0 || source.Submittals.Count != 0))
        {
            throw Invalid("configuration.conflict", "Disabled source cannot claim official facts.");
        }
        Limit(source.Documents.Count, ProjectTechnicalOfficeReportingContract.MaximumDocuments, "documents");
        Limit(source.Revisions.Count, ProjectTechnicalOfficeReportingContract.MaximumRevisions, "revisions");
        Limit(source.Transmittals.Count, ProjectTechnicalOfficeReportingContract.MaximumTransmittals, "transmittals");
        Limit(source.Rfis.Count, ProjectTechnicalOfficeReportingContract.MaximumRfis, "rfis");
        Limit(source.Submittals.Count, ProjectTechnicalOfficeReportingContract.MaximumSubmittals, "submittals");
        var documents = source.Documents.Where(item => item.CreatedAtUtc <= cutoff)
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        var revisions = source.Revisions.Where(item => item.CreatedAtUtc <= cutoff)
            .OrderBy(item => item.DocumentId)
            .ThenBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        var transmittals = source.Transmittals.Where(item => item.CreatedAtUtc <= cutoff)
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        var rfis = source.Rfis.Where(item => item.CreatedAtUtc <= cutoff)
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();
        var submittals = source.Submittals.Where(item => item.CreatedAtUtc <= cutoff)
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.Id).ToArray();

        CheckIdentity(documents.Select(item => (item.Id, item.TenantId, item.ProjectId, item.Number)), source, "document");
        CheckIdentity(revisions.Select(item => (item.Id, item.TenantId, item.ProjectId, item.Code)), source, "revision");
        CheckIdentity(transmittals.Select(item => (item.Id, item.TenantId, item.ProjectId, item.Number)), source, "transmittal");
        CheckIdentity(rfis.Select(item => (item.Id, item.TenantId, item.ProjectId, item.Number)), source, "rfi");
        CheckIdentity(submittals.Select(item => (item.Id, item.TenantId, item.ProjectId, item.Number)), source, "submittal");
        if (documents.Select(item => item.Number).Distinct(StringComparer.Ordinal).Count() != documents.Length ||
            transmittals.Select(item => item.Number).Distinct(StringComparer.Ordinal).Count() != transmittals.Length ||
            rfis.Select(item => item.Number).Distinct(StringComparer.Ordinal).Count() != rfis.Length ||
            submittals.Select(item => item.Number).Distinct(StringComparer.Ordinal).Count() != submittals.Length ||
            revisions.Select(item => (item.DocumentId, item.Code)).Distinct().Count() != revisions.Length)
        {
            throw Invalid("number.duplicate", "Technical Office numbers or revision codes are duplicated.");
        }
        var documentById = documents.ToDictionary(item => item.Id);
        var revisionById = revisions.ToDictionary(item => item.Id);
        var submittalById = submittals.ToDictionary(item => item.Id);
        foreach (var document in documents)
        {
            if (!Enum.IsDefined(document.Type) || !Enum.IsDefined(document.Classification) ||
                document.CreatedAtUtc == default || string.IsNullOrWhiteSpace(document.Discipline))
                throw Invalid("document.invalid", "Document metadata or classification is invalid.");
        }
        var classification = documents.Select(item => item.Classification)
            .Append(source.Classification).Max();
        if (classification == TechnicalReportingClassification.Restricted && !source.RestrictedPublicationApproved)
            throw Invalid("classification.restricted", "Restricted publication policy is not approved.");
        var eventCount = 0;
        foreach (var revision in revisions)
        {
            if (!documentById.ContainsKey(revision.DocumentId) ||
                revision.SupersedesRevisionId == revision.Id ||
                revision.SupersedesRevisionId.HasValue &&
                    (!revisionById.TryGetValue(revision.SupersedesRevisionId.Value, out var old) ||
                     old.DocumentId != revision.DocumentId) ||
                !Enum.IsDefined(revision.Purpose) || revision.RevisionDate == default)
                throw Invalid("revision.lineage.invalid", "Revision document or predecessor is invalid.");
            eventCount += ValidateEvents(revision.Events, revision.CreatedAtUtc, "revision");
        }
        foreach (var revision in revisions)
        {
            var visited = new HashSet<Guid>();
            var cursor = revision;
            while (cursor.SupersedesRevisionId.HasValue)
            {
                if (!visited.Add(cursor.Id) || visited.Count > ProjectTechnicalOfficeReportingContract.MaximumLineageDepth)
                    throw Invalid("revision.lineage.cycle", "Revision supersession chain is cyclic or exceeds budget.");
                cursor = revisionById[cursor.SupersedesRevisionId.Value];
            }
        }
        foreach (var transmittal in transmittals)
        {
            CheckReferences(transmittal.RevisionIds, revisionById, "transmittal.revision");
            if (transmittal.RevisionIds.Count == 0 ||
                transmittal.RevisionIds.Select(id => revisionById[id].DocumentId).Distinct().Count() !=
                    transmittal.RevisionIds.Count)
                throw Invalid("transmittal.document.duplicate", "An issue contains duplicate Documents.");
            eventCount += ValidateEvents(transmittal.Events, transmittal.CreatedAtUtc, "transmittal");
        }
        foreach (var rfi in rfis)
        {
            CheckReferences(rfi.ReferencedRevisionIds, revisionById, "rfi.revision");
            if (rfi.RaisedDate == default || !Enum.IsDefined(rfi.PotentialImpact) ||
                rfi.RequiredByDate < rfi.RaisedDate)
                throw Invalid("rfi.invalid", "RFI date or impact is invalid.");
            eventCount += ValidateEvents(rfi.Events, rfi.CreatedAtUtc, "rfi");
        }
        foreach (var submittal in submittals)
        {
            CheckReferences(submittal.RevisionIds, revisionById, "submittal.revision");
            if (submittal.RevisionIds.Count == 0 || !Enum.IsDefined(submittal.Type) ||
                string.IsNullOrWhiteSpace(submittal.Discipline) || submittal.ResubmissionNumber < 0 ||
                (submittal.ResubmissionNumber == 0) != !submittal.SupersedesSubmittalId.HasValue)
                throw Invalid("submittal.invalid", "Submittal metadata or revision links are invalid.");
            if (submittal.SupersedesSubmittalId.HasValue &&
                (!submittalById.TryGetValue(submittal.SupersedesSubmittalId.Value, out var previous) ||
                 previous.ResubmissionNumber + 1 != submittal.ResubmissionNumber))
                throw Invalid("submittal.lineage.invalid", "Resubmission predecessor is invalid.");
            eventCount += ValidateEvents(submittal.Events, submittal.CreatedAtUtc, "submittal");
        }
        foreach (var submittal in submittals)
        {
            var visited = new HashSet<Guid>();
            var cursor = submittal;
            while (cursor.SupersedesSubmittalId.HasValue)
            {
                if (!visited.Add(cursor.Id) || visited.Count > ProjectTechnicalOfficeReportingContract.MaximumLineageDepth)
                    throw Invalid("submittal.lineage.cycle", "Submittal lineage is cyclic or exceeds budget.");
                cursor = submittalById[cursor.SupersedesSubmittalId.Value];
            }
        }
        Limit(eventCount, ProjectTechnicalOfficeReportingContract.MaximumEvents, "events");
        return source with
        {
            SourceCutoffUtc = cutoff,
            Classification = classification,
            Documents = documents,
            Revisions = revisions.Select(item => item with
            {
                Events = AtCutoff(item.Events, cutoff),
                IssuedThroughTransmittalId = item.Events.Any(e =>
                    e.Type == TechnicalReportingEventType.Issued && e.AtUtc <= cutoff)
                    ? item.IssuedThroughTransmittalId : null
            }).ToArray(),
            Transmittals = transmittals.Select(item => item with
            {
                RevisionIds = item.RevisionIds.Order().ToArray(), Events = AtCutoff(item.Events, cutoff)
            }).ToArray(),
            Rfis = rfis.Select(item => item with
            {
                ReferencedRevisionIds = item.ReferencedRevisionIds.Order().ToArray(),
                Events = AtCutoff(item.Events, cutoff)
            }).ToArray(),
            Submittals = submittals.Select(item => item with
            {
                RevisionIds = item.RevisionIds.Order().ToArray(), Events = AtCutoff(item.Events, cutoff)
            }).ToArray()
        };
    }

    private static TechnicalReportingEvent[] AtCutoff(
        IReadOnlyCollection<TechnicalReportingEvent> events, DateTimeOffset cutoff) =>
        events.Where(item => item.AtUtc <= cutoff).OrderBy(item => item.AtUtc)
            .ThenBy(item => item.Sequence).Select(item => item with
            {
                AtUtc = item.AtUtc.ToUniversalTime()
            }).ToArray();

    private static int ValidateEvents(
        IReadOnlyCollection<TechnicalReportingEvent> events, DateTimeOffset createdAt, string name)
    {
        if (events is null || createdAt == default || events.Count > ProjectTechnicalOfficeReportingContract.MaximumEvents)
            throw Invalid($"{name}.events.invalid", "Technical Office event lineage is missing or oversized.");
        long sequence = 0;
        DateTimeOffset previous = createdAt.ToUniversalTime();
        foreach (var item in events.OrderBy(item => item.Sequence))
        {
            if (item.Sequence != ++sequence || !Enum.IsDefined(item.Type) ||
                item.AtUtc == default || item.AtUtc.ToUniversalTime() < previous ||
                item.ResponseClassification.HasValue && !Enum.IsDefined(item.ResponseClassification.Value) ||
                item.ReviewOutcome.HasValue && !Enum.IsDefined(item.ReviewOutcome.Value))
                throw Invalid($"{name}.events.invalid", "Technical Office events have a gap or invalid chronology.");
            previous = item.AtUtc.ToUniversalTime();
        }
        return events.Count;
    }

    private static void CheckIdentity(
        IEnumerable<(Guid Id, Guid TenantId, Guid ProjectId, string Number)> rows,
        ProjectTechnicalOfficeReportingProjection source, string name)
    {
        var items = rows.ToArray();
        if (items.Any(item => item.Id == Guid.Empty || item.TenantId != source.TenantId ||
                item.ProjectId != source.ProjectId || string.IsNullOrWhiteSpace(item.Number)) ||
            items.Select(item => item.Id).Distinct().Count() != items.Length)
            throw Invalid($"{name}.scope.invalid", "Technical Office identity or Project boundary is invalid.");
    }

    private static void CheckReferences<T>(IReadOnlyCollection<Guid> ids, Dictionary<Guid, T> byId, string name)
    {
        if (ids is null || ids.Count != ids.Distinct().Count() || ids.Any(id => !byId.ContainsKey(id)))
            throw Invalid($"{name}.invalid", "Technical Office reference is missing, duplicated or cross-Project.");
    }

    private static void Limit(int count, int maximum, string name)
    {
        if (count > maximum) throw Invalid($"{name}.budget", "Technical Office source budget exceeded.");
    }

    internal static DomainRuleException Invalid(string code, string message) =>
        new($"technical.project_reporting.{code}", message);
}
