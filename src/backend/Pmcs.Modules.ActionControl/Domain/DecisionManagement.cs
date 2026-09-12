using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

public sealed class DecisionRequest : AggregateRoot
{
    private DecisionRequest() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Question { get; private set; } = string.Empty;
    public string WhyNow { get; private set; } = string.Empty;
    public DateOnly RequiredBy { get; private set; }
    public Guid AuthorityUserId { get; private set; }
    public string AuthorityDisplayName { get; private set; } = string.Empty;
    public string KnownFactsJson { get; private set; } = "[]";
    public string AssumptionsJson { get; private set; } = "[]";
    public string PredictionsJson { get; private set; } = "[]";
    public string OptionsJson { get; private set; } = "[]";
    public string? Recommendation { get; private set; }
    public string ConstraintsJson { get; private set; } = "[]";
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public RecordConfidentiality Confidentiality { get; private set; }
    public string SourceModule { get; private set; } = string.Empty;
    public string SourceEntityType { get; private set; } = string.Empty;
    public Guid? SourceEntityId { get; private set; }
    public long? SourceRevision { get; private set; }
    public string SourceSnapshot { get; private set; } = string.Empty;
    public DecisionRequestStatus Status { get; private set; }
    public string? InformationRequest { get; private set; }
    public Guid? DecisionRecordId { get; private set; }
    public DateTimeOffset? SlaDueAt { get; private set; }
    public Guid? SlaRuleVersionId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? LastChangedBy { get; private set; }
    public DateTimeOffset? LastChangedAt { get; private set; }

    public IReadOnlyCollection<string> KnownFacts => GovernanceRules.ReadList(KnownFactsJson);
    public IReadOnlyCollection<string> Assumptions => GovernanceRules.ReadList(AssumptionsJson);
    public IReadOnlyCollection<string> Predictions => GovernanceRules.ReadList(PredictionsJson);
    public IReadOnlyCollection<string> Options => GovernanceRules.ReadList(OptionsJson);
    public IReadOnlyCollection<string> Constraints => GovernanceRules.ReadList(ConstraintsJson);
    public IReadOnlyCollection<string> EvidenceReferences => GovernanceRules.ReadList(EvidenceReferencesJson);

    public static DecisionRequest Create(Guid id, Guid tenantId, Guid projectId, string question,
        string whyNow, DateOnly requiredBy, Guid authorityUserId, string authorityDisplayName,
        IReadOnlyCollection<string>? knownFacts, IReadOnlyCollection<string>? assumptions,
        IReadOnlyCollection<string>? predictions, IReadOnlyCollection<string>? options,
        string? recommendation, IReadOnlyCollection<string>? constraints,
        IReadOnlyCollection<string>? evidenceReferences, RecordConfidentiality confidentiality,
        string sourceModule, string sourceEntityType, Guid? sourceEntityId, long? sourceRevision,
        string sourceSnapshot, DateTimeOffset? slaDueAt, Guid? slaRuleVersionId,
        Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Identities(id, tenantId, projectId, authorityUserId, actor);
        if (!Enum.IsDefined(confidentiality) || requiredBy == default)
            throw new DomainRuleException("governance.decision_request.classification.invalid", "Decision request classification and required date are required.");

        return new DecisionRequest
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Number = GovernanceRules.Number("DRQ", at, id),
            Question = GovernanceRules.Required(question, 1_000, "governance.decision_request.question.invalid"),
            WhyNow = GovernanceRules.Required(whyNow, 2_000, "governance.decision_request.why_now.invalid"),
            RequiredBy = requiredBy,
            AuthorityUserId = authorityUserId,
            AuthorityDisplayName = GovernanceRules.Required(authorityDisplayName, 200, "governance.decision_request.authority.invalid"),
            KnownFactsJson = GovernanceRules.JsonList(knownFacts, 2_000, "governance.decision_request.facts.invalid", true),
            AssumptionsJson = GovernanceRules.JsonList(assumptions, 2_000, "governance.decision_request.assumptions.invalid"),
            PredictionsJson = GovernanceRules.JsonList(predictions, 2_000, "governance.decision_request.predictions.invalid"),
            OptionsJson = GovernanceRules.JsonList(options, 2_000, "governance.decision_request.options.invalid", true),
            Recommendation = GovernanceRules.Optional(recommendation, 2_000, "governance.decision_request.recommendation.invalid"),
            ConstraintsJson = GovernanceRules.JsonList(constraints, 1_000, "governance.decision_request.constraints.invalid"),
            EvidenceReferencesJson = GovernanceRules.JsonList(evidenceReferences, 700, "governance.decision_request.evidence.invalid", true),
            Confidentiality = confidentiality,
            SourceModule = GovernanceRules.Required(sourceModule, 80, "governance.source.module.invalid"),
            SourceEntityType = GovernanceRules.Required(sourceEntityType, 120, "governance.source.type.invalid"),
            SourceEntityId = sourceEntityId,
            SourceRevision = sourceRevision,
            SourceSnapshot = GovernanceRules.Required(sourceSnapshot, 1_000, "governance.source.snapshot.invalid"),
            Status = DecisionRequestStatus.Draft,
            SlaDueAt = slaDueAt,
            SlaRuleVersionId = slaRuleVersionId,
            CreatedBy = actor,
            CreatedAt = at
        };
    }

    public void Submit(long baseRevision, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision_request.revision.conflict");
        GovernanceRules.Identities(actor);
        if (Status is not (DecisionRequestStatus.Draft or DecisionRequestStatus.MoreInformationRequired))
            throw new DomainRuleException("governance.decision_request.submit.invalid_state", "Decision request cannot be submitted in this state.");
        if (Options.Count < 2)
            throw new DomainRuleException("governance.decision_request.options.insufficient", "At least two explicit options are required before submission.");
        Status = DecisionRequestStatus.ReadyForDecision;
        InformationRequest = null;
        Touch(actor, at);
    }

    public void RequestMoreInformation(long baseRevision, string informationRequest, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision_request.revision.conflict");
        GovernanceRules.Identities(actor);
        if (Status is not (DecisionRequestStatus.ReadyForDecision or DecisionRequestStatus.InDecision))
            throw new DomainRuleException("governance.decision_request.more_information.invalid_state", "More information cannot be requested in this state.");
        InformationRequest = GovernanceRules.Required(informationRequest, 2_000, "governance.decision_request.information.invalid");
        Status = DecisionRequestStatus.MoreInformationRequired;
        Touch(actor, at);
    }

    public void BeginDecision(long baseRevision, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision_request.revision.conflict");
        GovernanceRules.Identities(actor);
        if (Status != DecisionRequestStatus.ReadyForDecision)
            throw new DomainRuleException("governance.decision_request.begin.invalid_state", "Decision request is not ready for decision.");
        Status = DecisionRequestStatus.InDecision;
        Touch(actor, at);
    }

    public void LinkDecision(long baseRevision, Guid decisionRecordId, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision_request.revision.conflict");
        GovernanceRules.Identities(decisionRecordId, actor);
        if (Status is not (DecisionRequestStatus.ReadyForDecision or DecisionRequestStatus.InDecision) || DecisionRecordId.HasValue)
            throw new DomainRuleException("governance.decision_request.decide.invalid_state", "Decision request cannot be decided in this state.");
        DecisionRecordId = decisionRecordId;
        Status = DecisionRequestStatus.Decided;
        Touch(actor, at);
    }

    public void MarkImplementing(long baseRevision, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision_request.revision.conflict");
        GovernanceRules.Identities(actor);
        if (Status != DecisionRequestStatus.Decided)
            throw new DomainRuleException("governance.decision_request.implement.invalid_state", "Only a decided request can enter implementation.");
        Status = DecisionRequestStatus.Implementing;
        Touch(actor, at);
    }

    public void MarkEffectReviewed(long baseRevision, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision_request.revision.conflict");
        GovernanceRules.Identities(actor);
        if (Status is not (DecisionRequestStatus.Decided or DecisionRequestStatus.Implementing))
            throw new DomainRuleException("governance.decision_request.review.invalid_state", "Decision effect cannot be reviewed in this state.");
        Status = DecisionRequestStatus.EffectReviewed;
        Touch(actor, at);
    }

    public void LinkReplacementDecision(long baseRevision, Guid currentDecisionId, Guid replacementDecisionId,
        Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision_request.revision.conflict");
        GovernanceRules.Identities(currentDecisionId, replacementDecisionId, actor);
        if (DecisionRecordId != currentDecisionId || Status is DecisionRequestStatus.Draft or
            DecisionRequestStatus.ReadyForDecision or DecisionRequestStatus.InDecision or
            DecisionRequestStatus.MoreInformationRequired or DecisionRequestStatus.Withdrawn or DecisionRequestStatus.Closed)
            throw new DomainRuleException("governance.decision_request.supersede.invalid_state", "Current decision cannot be replaced for this request.");
        DecisionRecordId = replacementDecisionId;
        Status = DecisionRequestStatus.Decided;
        Touch(actor, at);
    }

    private void Touch(Guid actor, DateTimeOffset at)
    {
        LastChangedBy = actor;
        LastChangedAt = at;
        AdvanceRevision();
    }
}

public sealed class DecisionRecord : AggregateRoot
{
    private DecisionRecord() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid DecisionRequestId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string SelectedOption { get; private set; } = string.Empty;
    public string Rationale { get; private set; } = string.Empty;
    public string ConditionsJson { get; private set; } = "[]";
    public DecisionChannel Channel { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public Guid DecidedBy { get; private set; }
    public string DecidedByDisplayName { get; private set; } = string.Empty;
    public Guid? SupersedesDecisionId { get; private set; }
    public Guid? SupersededByDecisionId { get; private set; }
    public string? EffectReview { get; private set; }
    public string EffectEvidenceJson { get; private set; } = "[]";
    public DecisionRecordStatus Status { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public IReadOnlyCollection<string> Conditions => GovernanceRules.ReadList(ConditionsJson);
    public IReadOnlyCollection<string> EffectEvidence => GovernanceRules.ReadList(EffectEvidenceJson);

    public static DecisionRecord Record(Guid id, DecisionRequest request, string selectedOption,
        string rationale, IReadOnlyCollection<string>? conditions, DecisionChannel channel,
        DateTimeOffset decidedAt, DateOnly? effectiveDate, Guid decidedBy, string decidedByDisplayName,
        Guid? supersedesDecisionId, DateTimeOffset recordedAt)
    {
        GovernanceRules.Identities(id, decidedBy);
        if (!Enum.IsDefined(channel) || decidedAt > recordedAt.AddMinutes(1))
            throw new DomainRuleException("governance.decision.record.invalid", "Decision channel or decision time is invalid.");
        var option = GovernanceRules.Required(selectedOption, 2_000, "governance.decision.option.invalid");
        if (!request.Options.Contains(option, StringComparer.OrdinalIgnoreCase))
            throw new DomainRuleException("governance.decision.option.not_offered", "Selected option must be one of the submitted options.");

        return new DecisionRecord
        {
            Id = id,
            TenantId = request.TenantId,
            ProjectId = request.ProjectId,
            DecisionRequestId = request.Id,
            Number = GovernanceRules.Number("DEC", recordedAt, id),
            SelectedOption = option,
            Rationale = GovernanceRules.Required(rationale, 4_000, "governance.decision.rationale.invalid"),
            ConditionsJson = GovernanceRules.JsonList(conditions, 1_000, "governance.decision.conditions.invalid"),
            Channel = channel,
            DecidedAt = decidedAt,
            EffectiveDate = effectiveDate,
            DecidedBy = decidedBy,
            DecidedByDisplayName = GovernanceRules.Required(decidedByDisplayName, 200, "governance.decision.authority.invalid"),
            SupersedesDecisionId = supersedesDecisionId,
            Status = DecisionRecordStatus.Recorded,
            RecordedAt = recordedAt
        };
    }

    public void Supersede(long baseRevision, Guid replacementDecisionId)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision.revision.conflict");
        GovernanceRules.Identities(replacementDecisionId);
        if (Status == DecisionRecordStatus.Superseded || replacementDecisionId == Id)
            throw new DomainRuleException("governance.decision.supersede.invalid_state", "Decision cannot be superseded in this state.");
        SupersededByDecisionId = replacementDecisionId;
        Status = DecisionRecordStatus.Superseded;
        AdvanceRevision();
    }

    public void ReviewEffect(long baseRevision, string review, IReadOnlyCollection<string>? evidence)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.decision.revision.conflict");
        if (Status != DecisionRecordStatus.Recorded)
            throw new DomainRuleException("governance.decision.effect.invalid_state", "Only the current recorded decision can be reviewed.");
        EffectReview = GovernanceRules.Required(review, 4_000, "governance.decision.effect.invalid");
        EffectEvidenceJson = GovernanceRules.JsonList(evidence, 700, "governance.decision.effect_evidence.invalid", true);
        Status = DecisionRecordStatus.EffectReviewed;
        AdvanceRevision();
    }
}
