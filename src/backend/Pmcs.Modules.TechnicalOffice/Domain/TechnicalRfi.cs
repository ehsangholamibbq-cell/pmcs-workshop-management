using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.TechnicalOffice.Domain;

public sealed class TechnicalRfi : AggregateRoot
{
    private TechnicalRfi() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Question { get; private set; } = string.Empty;
    public string RequestedFrom { get; private set; } = string.Empty;
    public string Discipline { get; private set; } = string.Empty;
    public Guid? ContractId { get; private set; }
    public string? LocationReference { get; private set; }
    public string? WorkItemReference { get; private set; }
    public string? WbsReference { get; private set; }
    public Guid? SourceIssueId { get; private set; }
    public DateOnly RaisedDate { get; private set; }
    public DateOnly? RequiredByDate { get; private set; }
    public PotentialImpact PotentialImpact { get; private set; }
    public bool IsBlocking { get; private set; }
    public string? ProposedSolution { get; private set; }
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public string RelatedRevisionIdsJson { get; private set; } = "[]";
    public string ResponseHistoryJson { get; private set; } = "[]";
    public RfiStatus Status { get; private set; }
    public Guid RaisedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<string> EvidenceReferences =>
        JsonSerializer.Deserialize<string[]>(EvidenceReferencesJson, TechnicalOfficeRules.JsonOptions) ?? [];
    public IReadOnlyCollection<Guid> RelatedRevisionIds => TechnicalOfficeRules.DeserializeIds(RelatedRevisionIdsJson);
    public IReadOnlyCollection<RfiResponseRecord> Responses =>
        JsonSerializer.Deserialize<RfiResponseRecord[]>(ResponseHistoryJson, TechnicalOfficeRules.JsonOptions) ?? [];

    public static TechnicalRfi Create(
        Guid id, Guid tenantId, Guid projectId, string title, string question, string requestedFrom,
        string discipline, Guid? contractId, string? locationReference, string? workItemReference,
        string? wbsReference, Guid? sourceIssueId, DateOnly raisedDate, DateOnly? requiredByDate,
        PotentialImpact potentialImpact, bool isBlocking, string? proposedSolution,
        IReadOnlyCollection<string>? evidenceReferences, IReadOnlyCollection<Guid>? relatedRevisionIds,
        Guid raisedBy, DateTimeOffset createdAt)
    {
        TechnicalOfficeRules.Identity(id, tenantId, projectId, raisedBy);
        const PotentialImpact allowedImpact = PotentialImpact.Time | PotentialImpact.Cost |
            PotentialImpact.Quality | PotentialImpact.Scope | PotentialImpact.Safety;
        if ((potentialImpact & ~allowedImpact) != 0)
        {
            throw new DomainRuleException("technical.rfi.impact.invalid", "Potential impact is invalid.");
        }
        if (requiredByDate.HasValue && requiredByDate < raisedDate)
        {
            throw new DomainRuleException("technical.rfi.required_by.invalid", "Required-by date cannot precede the raised date.");
        }

        var evidence = NormalizeStrings(evidenceReferences, 30, 700, "technical.rfi.evidence.invalid");
        return new TechnicalRfi
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Number = TechnicalOfficeRules.OfficialNumber("RFI", createdAt, id),
            Title = TechnicalOfficeRules.Required(title, 240, "technical.rfi.title.invalid"),
            Question = TechnicalOfficeRules.Required(question, 6_000, "technical.rfi.question.invalid"),
            RequestedFrom = TechnicalOfficeRules.Required(requestedFrom, 240, "technical.rfi.requested_from.invalid"),
            Discipline = TechnicalOfficeRules.Required(discipline, 120, "technical.rfi.discipline.invalid"),
            ContractId = contractId,
            LocationReference = TechnicalOfficeRules.Optional(locationReference, 240, "technical.rfi.location.too_long"),
            WorkItemReference = TechnicalOfficeRules.Optional(workItemReference, 240, "technical.rfi.work_item.too_long"),
            WbsReference = TechnicalOfficeRules.Optional(wbsReference, 240, "technical.rfi.wbs.too_long"),
            SourceIssueId = sourceIssueId,
            RaisedDate = raisedDate,
            RequiredByDate = requiredByDate,
            PotentialImpact = potentialImpact,
            IsBlocking = isBlocking,
            ProposedSolution = TechnicalOfficeRules.Optional(proposedSolution, 4_000, "technical.rfi.solution.too_long"),
            EvidenceReferencesJson = JsonSerializer.Serialize(evidence, TechnicalOfficeRules.JsonOptions),
            RelatedRevisionIdsJson = TechnicalOfficeRules.SerializeIds(relatedRevisionIds, 50, "technical.rfi.revisions.invalid"),
            Status = RfiStatus.Draft,
            RaisedBy = raisedBy,
            CreatedAt = createdAt
        };
    }

    public void SubmitForInternalReview(long baseRevision)
    {
        EnsureRevision(baseRevision);
        if (Status != RfiStatus.Draft)
        {
            throw new DomainRuleException("technical.rfi.internal_review.invalid_state", "Only a draft RFI can enter internal review.");
        }
        if (EvidenceReferences.Count == 0)
        {
            throw new DomainRuleException("technical.rfi.evidence.required", "Evidence is required before review.");
        }
        Status = RfiStatus.InternalReview;
        AdvanceRevision();
    }

    public void ReturnToDraft(long baseRevision, string reason, Guid reviewer, DateTimeOffset at)
    {
        EnsureRevision(baseRevision);
        TechnicalOfficeRules.Identity(reviewer);
        if (Status != RfiStatus.InternalReview)
        {
            throw new DomainRuleException("technical.rfi.return.invalid_state", "Only an internally reviewed RFI can be returned.");
        }
        var normalizedReason = TechnicalOfficeRules.Required(reason, 1_000, "technical.rfi.return.reason.invalid");
        AppendResponse(new RfiResponseRecord(
            Responses.Count + 1, "InternalReviewReturn", normalizedReason, reviewer.ToString(), at, RfiResponseClassification.InformationOnly,
            false, [], reviewer, null, null, null, null));
        Status = RfiStatus.Draft;
        AdvanceRevision();
    }

    public void Issue(long baseRevision, DateTimeOffset at)
    {
        EnsureRevision(baseRevision);
        if (Status != RfiStatus.InternalReview)
        {
            throw new DomainRuleException("technical.rfi.issue.invalid_state", "Only an internally reviewed RFI can be issued.");
        }
        Status = RfiStatus.Submitted;
        SubmittedAt = at;
        AdvanceRevision();
    }

    public void RecordResponse(
        long baseRevision, string responseText, string respondingParty, DateTimeOffset responseAt,
        RfiResponseClassification classification, bool changePotential,
        IReadOnlyCollection<Guid>? referencedRevisionIds, Guid receivedBy)
    {
        EnsureRevision(baseRevision);
        TechnicalOfficeRules.Identity(receivedBy);
        if (Status is not RfiStatus.Submitted and not RfiStatus.ClarificationRequired)
        {
            throw new DomainRuleException("technical.rfi.response.invalid_state", "Only an issued or clarification-pending RFI can receive a response.");
        }
        if (!Enum.IsDefined(classification))
        {
            throw new DomainRuleException("technical.rfi.response.classification.invalid", "Response classification is invalid.");
        }

        var normalizedParty = TechnicalOfficeRules.Required(
            respondingParty, 240, "technical.rfi.response.party.invalid");
        AppendResponse(new RfiResponseRecord(
            Responses.Count + 1,
            "ExternalResponse",
            TechnicalOfficeRules.Required(responseText, 8_000, "technical.rfi.response.text.invalid"),
            normalizedParty,
            responseAt,
            classification,
            changePotential,
            TechnicalOfficeRules.DeserializeIds(TechnicalOfficeRules.SerializeIds(referencedRevisionIds, 50, "technical.rfi.response.revisions.invalid")),
            receivedBy,
            null,
            null,
            null,
            null));
        Status = RfiStatus.Answered;
        AdvanceRevision();
    }

    public void AcceptResponse(long baseRevision, Guid reviewer, DateTimeOffset at, string? comment)
    {
        ReviewCurrentResponse(baseRevision, reviewer, at, true, comment);
        Status = RfiStatus.ResponseAccepted;
        AdvanceRevision();
    }

    public void RequireClarification(long baseRevision, Guid reviewer, DateTimeOffset at, string reason)
    {
        ReviewCurrentResponse(baseRevision, reviewer, at, false,
            TechnicalOfficeRules.Required(reason, 1_000, "technical.rfi.clarification.reason.invalid"));
        Status = RfiStatus.ClarificationRequired;
        AdvanceRevision();
    }

    public void Close(long baseRevision, DateTimeOffset at)
    {
        EnsureRevision(baseRevision);
        if (Status != RfiStatus.ResponseAccepted)
        {
            throw new DomainRuleException("technical.rfi.close.invalid_state", "Only an accepted response can be closed.");
        }
        Status = RfiStatus.Closed;
        ClosedAt = at;
        AdvanceRevision();
    }

    private void ReviewCurrentResponse(long baseRevision, Guid reviewer, DateTimeOffset at, bool accepted, string? comment)
    {
        EnsureRevision(baseRevision);
        TechnicalOfficeRules.Identity(reviewer);
        if (Status != RfiStatus.Answered || Responses.Count == 0)
        {
            throw new DomainRuleException("technical.rfi.response_review.invalid_state", "Only an answered RFI can be reviewed.");
        }

        var items = Responses.ToArray();
        items[^1] = items[^1] with
        {
            Accepted = accepted,
            ReviewedBy = reviewer,
            ReviewedAt = at,
            ReviewComment = TechnicalOfficeRules.Optional(comment, 1_000, "technical.review.comment.too_long")
        };
        ResponseHistoryJson = JsonSerializer.Serialize(items, TechnicalOfficeRules.JsonOptions);
    }

    private void AppendResponse(RfiResponseRecord response)
    {
        var responses = Responses.Append(response).ToArray();
        ResponseHistoryJson = JsonSerializer.Serialize(responses, TechnicalOfficeRules.JsonOptions);
    }

    private void EnsureRevision(long supplied) =>
        TechnicalOfficeRules.Revision(Revision, supplied, "technical.rfi.revision.conflict");

    private static string[] NormalizeStrings(
        IReadOnlyCollection<string>? values, int maximumCount, int maximumLength, string code)
    {
        var normalized = values?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => TechnicalOfficeRules.Required(value, maximumLength, code))
            .Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (normalized.Length > maximumCount)
        {
            throw new DomainRuleException(code, $"At most {maximumCount} references are allowed.");
        }
        return normalized;
    }
}

public sealed record RfiResponseRecord(
    int Sequence,
    string Source,
    string Text,
    string RespondingParty,
    DateTimeOffset RespondedAt,
    RfiResponseClassification Classification,
    bool ChangePotential,
    IReadOnlyCollection<Guid> ReferencedRevisionIds,
    Guid ReceivedBy,
    bool? Accepted,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment);
