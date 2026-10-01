using System.Data;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Persistence;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.ActionControl.Services;

internal sealed class ProjectGovernanceActionReportingSource(
    ActionControlDbContext db, IProjectDirectory projects) : IProjectGovernanceActionReportingSource
{
    private static readonly GovernanceActionReportingClassification Confidential =
        GovernanceActionReportingClassification.Confidential;
    private static readonly GovernanceActionReportingClassification Restricted =
        GovernanceActionReportingClassification.Restricted;

    public async Task<ProjectGovernanceActionReportingResult> LoadAsync(
        Guid tenantId, Guid projectId, DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(ProjectGovernanceActionReportingContract.MaximumReadSeconds));
        var token = timeout.Token;
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var project = await projects.FindProfileAsync(tenantId, projectId, token)
            ?? throw Invalid("project.not_found", "Project scope is missing.");
        if (tenantId == Guid.Empty || projectId == Guid.Empty || cutoff == default ||
            cutoff > DateTimeOffset.UtcNow || project.TenantId != tenantId || project.Id != projectId ||
            project.Status != ProjectStatus.Active || project.ConfigurationVersion <= 0 ||
            project.ConfigurationChangedAt is null || project.ConfigurationChangedAt > cutoff)
            throw Invalid("project.invalid", "Project scope/configuration cannot be proved at the cutoff.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone); }
        catch (TimeZoneNotFoundException) { throw Invalid("time_zone.invalid", "Unknown project time zone."); }
        catch (InvalidTimeZoneException) { throw Invalid("time_zone.invalid", "Invalid project time zone."); }
        if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, zone).DateTime) != cutoffLocalDate)
            throw Invalid("cutoff.invalid", "Pinned local date does not match the UTC cutoff.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        var issues = await Read(db.Issues.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var risks = await Read(db.Risks.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var requests = await Read(db.DecisionRequests.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var decisions = await Read(db.Decisions.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.RecordedAt <= cutoff), token);
        var escalations = await Read(db.Escalations.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.FirstRaisedAt <= cutoff), token);
        var actions = await Read(db.Actions.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var matrices = await Read(db.RiskMatrices.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var slaRules = await Read(db.SlaRules.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);

        // The owner ledger is a complete chronology only when its first event and every
        // subsequent revision are present. A legacy null stays incomplete after mutations.
        var historyChars = issues.Sum(x => (long)(x.ReportingHistoryJson?.Length ?? 0)) +
            risks.Sum(x => (long)(x.ReportingHistoryJson?.Length ?? 0)) +
            requests.Sum(x => (long)(x.ReportingHistoryJson?.Length ?? 0)) +
            decisions.Sum(x => (long)(x.ReportingHistoryJson?.Length ?? 0)) +
            escalations.Sum(x => (long)(x.ReportingHistoryJson?.Length ?? 0)) +
            actions.Sum(x => (long)(x.ReportingHistoryJson?.Length ?? 0));
        if (historyChars > ProjectGovernanceActionReportingContract.MaximumSnapshotBytes)
            throw Invalid("history.overflow", "Owner transition ledger exceeds the bounded read budget.");
        var issueAt = SelectAt(issues, x => x.Id, x => x.ReportingHistory,
            x => x.CreatedAt, IssueStatus.Open, x => x.Status, cutoff);
        var riskAt = SelectAt(risks, x => x.Id, x => x.ReportingHistory,
            x => x.CreatedAt, RiskStatus.Proposed, x => x.Status, cutoff);
        var requestAt = SelectAt(requests, x => x.Id, x => x.ReportingHistory,
            x => x.CreatedAt, DecisionRequestStatus.Draft, x => x.Status, cutoff);
        var decisionAt = SelectAt(decisions, x => x.Id, x => x.ReportingHistory,
            x => x.RecordedAt, DecisionRecordStatus.Recorded, x => x.Status, cutoff);
        var escalationAt = SelectAt(escalations, x => x.Id, x => x.ReportingHistory,
            x => x.FirstRaisedAt, EscalationStatus.Open, x => x.Status, cutoff);
        var actionAt = SelectAt(actions, x => x.Id, x => x.ReportingHistory,
            x => x.CreatedAt, ManagementActionStatus.Open, x => x.Status, cutoff);
        var eventCount = issues.Sum(x => (long)(x.ReportingHistory?.Count ?? 0)) +
            risks.Sum(x => (long)(x.ReportingHistory?.Count ?? 0)) +
            requests.Sum(x => (long)(x.ReportingHistory?.Count ?? 0)) +
            decisions.Sum(x => (long)(x.ReportingHistory?.Count ?? 0)) +
            escalations.Sum(x => (long)(x.ReportingHistory?.Count ?? 0)) +
            actions.Sum(x => (long)(x.ReportingHistory?.Count ?? 0));
        if (eventCount > ProjectGovernanceActionReportingContract.MaximumFacts)
            throw Invalid("history.overflow", "Owner transition event count exceeds the read budget.");

        foreach (var x in issues.Where(x => x.ReportingHistory is not null))
        {
            var last = x.ReportingHistory!.Last();
            if (last.DueDate != x.TargetResolutionDate ||
                x.Revision > 1 && !SameInstant(last.AtUtc, x.LastChangedAt))
                throw Invalid("issue.history", "Issue transition ledger disagrees with the current owner row.");
        }
        foreach (var x in risks.Where(x => x.ReportingHistory is not null))
        {
            var last = x.ReportingHistory!.Last();
            if (last.DueDate != x.ReviewDate || last.Rating != (x.ResidualRating ?? x.InherentRating)?.ToString() ||
                last.MatrixVersion != x.MatrixVersion || last.RelatedId != x.MaterializedIssueId ||
                x.Revision > 1 && !SameInstant(last.AtUtc, x.LastReviewedAt))
                throw Invalid("risk.history", "Risk transition ledger disagrees with the current owner row.");
        }
        foreach (var x in requests.Where(x => x.ReportingHistory is not null))
        {
            var last = x.ReportingHistory!.Last();
            if (last.DueDate != x.RequiredBy || last.RelatedId != x.DecisionRecordId ||
                x.Revision > 1 && !SameInstant(last.AtUtc, x.LastChangedAt))
                throw Invalid("decision_request.history", "Request ledger disagrees with the current row.");
        }
        foreach (var x in decisions.Where(x => x.ReportingHistory is not null))
        {
            var last = x.ReportingHistory!.Last();
            if (last.DueDate != x.EffectiveDate ||
                last.RelatedId != (x.Status == DecisionRecordStatus.Superseded
                    ? x.SupersededByDecisionId : x.SupersedesDecisionId))
                throw Invalid("decision.history", "Decision ledger disagrees with the current row.");
        }
        foreach (var x in escalations.Where(x => x.ReportingHistory is not null))
        {
            var last = x.ReportingHistory!.Last();
            var lastWasTouch = x.ReportingHistory!.Count > 1 &&
                last.OccurrenceCount > x.ReportingHistory!.ElementAt(x.ReportingHistory.Count - 2).OccurrenceCount;
            var timestamp = x.Status == EscalationStatus.Acknowledged && !lastWasTouch
                ? x.AcknowledgedAt : x.LastRaisedAt;
            if (last.OccurrenceCount != x.OccurrenceCount || !SameInstant(last.AtUtc, timestamp))
                throw Invalid("escalation.history", "Escalation ledger disagrees with the current row.");
        }
        foreach (var x in actions.Where(x => x.ReportingHistory is not null))
        {
            var last = x.ReportingHistory!.Last();
            if (last.DueDate != x.DueDate || last.Rating != x.Priority.ToString() ||
                x.Revision > 1 && !SameInstant(last.AtUtc, x.LastChangedAt))
                throw Invalid("action.history", "Action ledger disagrees with the current row.");
        }

        var allIds = issues.Select(x => x.Id).Concat(risks.Select(x => x.Id))
            .Concat(requests.Select(x => x.Id)).Concat(decisions.Select(x => x.Id))
            .Concat(escalations.Select(x => x.Id)).Concat(actions.Select(x => x.Id))
            .Concat(matrices.Select(x => x.Id)).Concat(slaRules.Select(x => x.Id)).ToArray();
        if (allIds.Any(x => x == Guid.Empty) || allIds.Distinct().Count() != allIds.Length)
            throw Invalid("identity.duplicate", "Owner source IDs are missing or duplicated.");
        var issueIds = issues.Select(x => x.Id).ToHashSet();
        var riskIds = risks.Select(x => x.Id).ToHashSet();
        var requestIds = requests.Select(x => x.Id).ToHashSet();
        var decisionIds = decisions.Select(x => x.Id).ToHashSet();
        var matrixIds = matrices.Select(x => x.Id).ToHashSet();
        var matrixById = matrices.ToDictionary(x => x.Id);
        var ruleById = slaRules.ToDictionary(x => x.Id);
        if (issues.Any(x => x.MaterializedFromRiskId is { } id && !riskIds.Contains(id)) ||
            risks.Any(x => riskAt[x.Id]?.State == RiskStatus.Materialized.ToString() &&
                riskAt[x.Id]?.RelatedId is { } id && !issueIds.Contains(id)) ||
            risks.Any(x => riskAt[x.Id]?.MatrixVersion is not null &&
                x.MatrixVersionId is { } id && !matrixIds.Contains(id)) ||
            decisions.Any(x => !requestIds.Contains(x.DecisionRequestId) ||
                x.SupersedesDecisionId is { } predecessor && !decisionIds.Contains(predecessor) ||
                decisionAt[x.Id]?.State == DecisionRecordStatus.Superseded.ToString() &&
                decisionAt[x.Id]?.RelatedId is { } successor && !decisionIds.Contains(successor)) ||
            escalations.Any(x => !(x.EntityType switch
            {
                SlaEntityType.Issue => issueIds.Contains(x.EntityId),
                SlaEntityType.Risk => riskIds.Contains(x.EntityId),
                SlaEntityType.DecisionRequest => requestIds.Contains(x.EntityId),
                _ => false
            })) ||
            escalations.Select(x => x.ThreadKey).Distinct(StringComparer.Ordinal).Count() != escalations.Length)
            throw Invalid("linkage.invalid", "An owner link cannot be proved inside the project scope.");
        if (risks.Any(x => riskAt[x.Id]?.State == RiskStatus.Materialized.ToString() &&
                riskAt[x.Id]?.RelatedId is { } id &&
                issues.Single(y => y.Id == id).MaterializedFromRiskId != x.Id) ||
            issues.Any(x => x.MaterializedFromRiskId is { } id &&
                riskAt[id]?.State == RiskStatus.Materialized.ToString() &&
                riskAt[id]?.RelatedId != x.Id))
            throw Invalid("risk_issue.linkage", "Materialized Risk and Issue links disagree.");
        if (issues.Any(x => !Enum.IsDefined(x.Confidentiality)) ||
            risks.Any(x => !Enum.IsDefined(x.Confidentiality)) ||
            requests.Any(x => !Enum.IsDefined(x.Confidentiality)) ||
            escalations.Any(x => !Enum.IsDefined(x.Confidentiality)))
            throw Invalid("classification.unknown", "Owner confidentiality is unknown.");
        if (issues.Any(x => !Enum.IsDefined(x.Status) || !Enum.IsDefined(x.Severity)) ||
            risks.Any(x => !Enum.IsDefined(x.Status) ||
                x.InherentRating.HasValue && !Enum.IsDefined(x.InherentRating.Value) ||
                x.ResidualRating.HasValue && !Enum.IsDefined(x.ResidualRating.Value)) ||
            requests.Any(x => !Enum.IsDefined(x.Status)) ||
            decisions.Any(x => !Enum.IsDefined(x.Status)) ||
            escalations.Any(x => !Enum.IsDefined(x.Status) || !Enum.IsDefined(x.Reason)) ||
            actions.Any(x => !Enum.IsDefined(x.Status) || !Enum.IsDefined(x.Priority)))
            throw Invalid("state.unknown", "An owner state or rating is unknown.");
        if (risks.Any(x => riskAt[x.Id]?.MatrixVersion is not null &&
                (x.MatrixVersionId is not { } id ||
                 matrixById[id].Version != riskAt[x.Id]!.MatrixVersion ||
                 matrixById[id].FormulaVersion != x.FormulaVersion ||
                 matrixById[id].EffectiveFrom > FirstAssessment(x, cutoff))))
            throw Invalid("matrix.version", "A Risk assessment does not match its pinned matrix.");
        var decisionById = decisions.ToDictionary(x => x.Id);
        foreach (var record in decisions)
        {
            if (record.SupersedesDecisionId is { } predecessor &&
                (decisionById[predecessor].DecisionRequestId != record.DecisionRequestId ||
                 decisionAt[predecessor] is not null &&
                 (decisionAt[predecessor]?.State != DecisionRecordStatus.Superseded.ToString() ||
                  decisionAt[predecessor]?.RelatedId != record.Id)) ||
                decisionAt[record.Id]?.State == DecisionRecordStatus.Superseded.ToString() &&
                decisionAt[record.Id]?.RelatedId is { } successor &&
                (decisionById[successor].DecisionRequestId != record.DecisionRequestId ||
                 decisionById[successor].SupersedesDecisionId != record.Id))
                throw Invalid("decision.linkage", "Decision supersession links disagree.");
            var visited = new HashSet<Guid>();
            var cursor = record;
            while (true)
            {
                if (!visited.Add(cursor.Id))
                    throw Invalid("decision.cycle", "Decision supersession contains a cycle.");
                if (cursor.SupersedesDecisionId is not { } previous) break;
                cursor = decisionById[previous];
            }
        }

        var issue = Section(issues.Any(x => issueAt[x.Id] is null),
            issues.Where(x => issueAt[x.Id] is not null).Select(x => Fact(x.Id, x.Number, GovernanceActionFactKind.Issue,
                issueAt[x.Id]!.State, x.CreatedAt, issueAt[x.Id]!.DueDate,
                CertifiedSla(x.SlaRuleVersionId, x.SlaDueAt, ruleById, project),
                x.Severity.ToString(), null, Map(x.Confidentiality))).ToArray(),
            GovernanceActionReportingReason.NoOfficialIssue, Classify(issues.Select(x => x.Confidentiality)));
        issue = WithSlaReasons(issue, issues.Select(x => x.SlaRuleVersionId), ruleById, project);
        var risk = Section(risks.Any(x => riskAt[x.Id] is null),
            risks.Where(x => riskAt[x.Id] is not null).Select(x => Fact(x.Id, x.Number, GovernanceActionFactKind.Risk,
                riskAt[x.Id]!.State, x.CreatedAt, riskAt[x.Id]!.DueDate,
                CertifiedSla(x.SlaRuleVersionId, x.SlaDueAt, ruleById, project),
                riskAt[x.Id]!.MatrixVersion.HasValue ? riskAt[x.Id]!.Rating : null,
                riskAt[x.Id]!.MatrixVersion,
                Map(x.Confidentiality))).ToArray(),
            GovernanceActionReportingReason.NoOfficialRisk, Classify(risks.Select(x => x.Confidentiality)));
        risk = WithSlaReasons(risk, risks.Select(x => x.SlaRuleVersionId), ruleById, project);
        if (risk.Status == GovernanceActionReportingStatus.Available &&
            risks.Any(x => riskAt[x.Id]?.MatrixVersion is null && riskAt[x.Id]?.Rating is not null))
            risk = risk with { Reasons = risk.Reasons.Append(
                GovernanceActionReportingReason.MatrixVersionUnavailable).Distinct().OrderBy(x => x).ToArray() };
        if (risks.Length > 0 && risks.All(x => riskAt[x.Id]?.Rating is null) &&
            risk.Status == GovernanceActionReportingStatus.Available)
            risk = risk with { Reasons = risk.Reasons.Append(GovernanceActionReportingReason.NoAssessedRisk).ToArray() };
        var decision = Section(requests.Any(x => requestAt[x.Id] is null) ||
                decisions.Any(x => decisionAt[x.Id] is null),
            requests.Where(x => requestAt[x.Id] is not null &&
                    requestAt[x.Id]!.State != DecisionRequestStatus.Draft.ToString())
                .Select(x => Fact(x.Id, x.Number, GovernanceActionFactKind.DecisionRequest,
                    requestAt[x.Id]!.State, FirstSubmit(x, cutoff), requestAt[x.Id]!.DueDate,
                    CertifiedSla(x.SlaRuleVersionId, x.SlaDueAt, ruleById, project),
                    null, null, Map(x.Confidentiality)))
                .Concat(decisions.Where(x => decisionAt[x.Id] is not null).Select(x => Fact(x.Id, x.Number,
                    GovernanceActionFactKind.DecisionRecord, decisionAt[x.Id]!.State,
                    x.RecordedAt, x.EffectiveDate, null, null, null,
                    Map(requests.Single(y => y.Id == x.DecisionRequestId).Confidentiality))))
                .ToArray(), GovernanceActionReportingReason.NoSubmittedDecision,
                Classify(requests.Select(x => x.Confidentiality)));
        decision = WithSlaReasons(decision, requests.Select(x => x.SlaRuleVersionId), ruleById, project);
        var escalation = Section(escalations.Any(x => escalationAt[x.Id] is null),
            escalations.Where(x => escalationAt[x.Id] is not null).Select(x => Fact(x.Id, "ESC-" + x.Id.ToString("N"),
                GovernanceActionFactKind.Escalation, escalationAt[x.Id]!.State,
                x.FirstRaisedAt, null, null, x.Reason.ToString(), null,
                Map(x.Confidentiality))).ToArray(),
            GovernanceActionReportingReason.NoRaisedEscalation,
            Classify(escalations.Select(x => x.Confidentiality)));
        // No Action field proves the sensitivity of SourceFactId. The versioned owner
        // publication policy labels every Action Restricted, with no Daily Fact join.
        var action = Section(actions.Any(x => actionAt[x.Id] is null),
            actions.Where(x => actionAt[x.Id] is not null).Select(x => Fact(x.Id, x.Id.ToString("N"),
                GovernanceActionFactKind.Action, actionAt[x.Id]!.State, x.CreatedAt,
                actionAt[x.Id]!.DueDate, null, actionAt[x.Id]!.Rating, null, Restricted)).ToArray(),
            GovernanceActionReportingReason.NoOfficialAction,
            actions.Length > 0 ? Restricted : Confidential);
        var sections = new[] { issue, risk, decision, escalation, action };
        var classification = sections.Any(x => x.Classification == Restricted) ? Restricted : Confidential;
        var status = sections.Any(x => x.Status == GovernanceActionReportingStatus.InsufficientData)
            ? GovernanceActionReportingStatus.InsufficientData
            : sections.Any(x => x.Status == GovernanceActionReportingStatus.Available)
                ? GovernanceActionReportingStatus.Available : GovernanceActionReportingStatus.NoData;
        var registers = new[]
        {
            RegisterProjection("issues", issues.Select(x => new
                { x.Id, x.Number, x.Confidentiality, x.Severity, x.TargetResolutionDate,
                  x.MaterializedFromRiskId, x.SlaRuleVersionId, x.SlaDueAt, Event = issueAt[x.Id] }).ToArray()),
            RegisterProjection("risks", risks.Select(x => new
                { x.Id, x.Number, x.Confidentiality, x.SlaRuleVersionId, x.SlaDueAt,
                  MatrixVersionId = riskAt[x.Id]?.MatrixVersion is null ? null : x.MatrixVersionId,
                  Event = riskAt[x.Id] }).ToArray()),
            RegisterProjection("decision_requests", requests.Select(x => new
                { x.Id, x.Number, x.Confidentiality, x.SlaRuleVersionId, x.SlaDueAt,
                  Event = requestAt[x.Id] }).ToArray()),
            RegisterProjection("decisions", decisions.Select(x => new
                { x.Id, x.Number, x.DecisionRequestId, x.SupersedesDecisionId,
                  Event = decisionAt[x.Id] }).ToArray()),
            RegisterProjection("escalation_threads", escalations.Select(x => new
                { x.Id, x.ThreadKey, x.EntityType, x.EntityId, x.Reason, x.Confidentiality,
                  Event = escalationAt[x.Id] }).ToArray()),
            RegisterProjection("actions", actions.Select(x => new
                { x.Id, x.SourceFactId, x.DueDate, x.Priority, Event = actionAt[x.Id] }).ToArray()),
            Register("risk_matrix_versions", matrices), Register("sla_rule_versions", slaRules)
        };
        var manifest = new ProjectGovernanceActionSourceManifest(
            ProjectGovernanceActionReportingContract.ManifestVersion,
            ProjectGovernanceActionReportingContract.Version,
            ProjectGovernanceActionReportingContract.PolicyVersion,
            ProjectGovernanceActionReportingContract.ActionClassificationPolicy,
            tenantId, projectId, cutoffLocalDate, cutoff, cutoff,
            project.ConfigurationVersion, project.ConfigurationChangedAt.Value.ToUniversalTime(),
            classification, registers);
        var result = new ProjectGovernanceActionReportingResult(
            ProjectGovernanceActionReportingContract.Version,
            ProjectGovernanceActionReportingContract.PolicyVersion,
            tenantId, projectId, cutoffLocalDate, cutoff, classification, status,
            sections.SelectMany(x => x.Reasons).Distinct().OrderBy(x => x).ToArray(),
            issue, risk, decision, escalation, action, manifest,
            GovernanceActionReportingHash.Compute(manifest), "");
        result = result with { SemanticSha256 = GovernanceActionReportingHash.Result(result) };
        await transaction.CommitAsync(token);
        var current = await projects.FindProfileAsync(tenantId, projectId, token);
        if (current is null || current.Revision != project.Revision ||
            current.ConfigurationVersion != project.ConfigurationVersion || current.TimeZone != project.TimeZone)
            throw Invalid("project.changed", "Project profile changed during source collection.");
        return result;
    }

    private static async Task<T[]> Read<T>(IQueryable<T> query, CancellationToken token) where T : AggregateRoot
    {
        var items = await query.AsNoTracking().OrderBy(x => EF.Property<Guid>(x, "Id"))
            .Take(ProjectGovernanceActionReportingContract.MaximumPerRegister + 1).ToArrayAsync(token);
        if (items.Length > ProjectGovernanceActionReportingContract.MaximumPerRegister)
            throw Invalid("source.overflow", "An owner register exceeds its complete read bound.");
        return items;
    }

    private static GovernanceActionReportingFact Fact(Guid id, string number, GovernanceActionFactKind kind,
        string state, DateTimeOffset officialAt, DateOnly? dueDate, DateTimeOffset? slaDueAt,
        string? rating, int? matrixVersion, GovernanceActionReportingClassification classification) =>
        new(id, number, kind, state, officialAt.ToUniversalTime(), dueDate,
            slaDueAt?.ToUniversalTime(), rating, matrixVersion, classification);

    private static GovernanceActionReportingSection Section(bool incomplete,
        IReadOnlyCollection<GovernanceActionReportingFact> facts, GovernanceActionReportingReason emptyReason,
        GovernanceActionReportingClassification? sourceClassification = null)
    {
        var classification = sourceClassification ??
            (facts.Any(x => x.Classification == Restricted) ? Restricted : Confidential);
        if (incomplete)
            return new(GovernanceActionReportingStatus.InsufficientData, null, [],
                [GovernanceActionReportingReason.HistoricalTransitionUnavailable], classification);
        var rows = facts.OrderBy(x => x.Kind).ThenBy(x => x.Number, StringComparer.Ordinal)
            .ThenBy(x => x.Id).ToArray();
        if (rows.Length > ProjectGovernanceActionReportingContract.MaximumFacts)
            throw Invalid("facts.overflow", "Certified semantic fact budget was exceeded.");
        return rows.Length == 0
            ? new(GovernanceActionReportingStatus.NoData, 0, [], [emptyReason], classification)
            : new(GovernanceActionReportingStatus.Available, rows.Length, rows, [], classification);
    }

    private static DateTimeOffset? CertifiedSla(Guid? id, DateTimeOffset? dueAt,
        Dictionary<Guid, SlaRuleVersion> rules, ProjectControlProfile project)
    {
        if (!id.HasValue || !rules.TryGetValue(id.Value, out var rule) ||
            rule.DurationUnit == SlaDurationUnit.ProjectWorkingDays &&
            project.Calendar.State != ProjectCalendarConfigurationState.Configured)
            return null;
        return dueAt;
    }

    private static GovernanceActionReportingSection WithSlaReasons(
        GovernanceActionReportingSection section, IEnumerable<Guid?> pinnedIds,
        Dictionary<Guid, SlaRuleVersion> rules, ProjectControlProfile project)
    {
        if (section.Status == GovernanceActionReportingStatus.InsufficientData) return section;
        var ids = pinnedIds.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var reasons = section.Reasons.ToList();
        if (ids.Any(id => !rules.ContainsKey(id)))
            reasons.Add(GovernanceActionReportingReason.SlaRuleUnavailable);
        if (ids.Any(id => rules.TryGetValue(id, out var rule) &&
                rule.DurationUnit == SlaDurationUnit.ProjectWorkingDays &&
                project.Calendar.State != ProjectCalendarConfigurationState.Configured))
            reasons.Add(GovernanceActionReportingReason.WorkingCalendarUnavailable);
        return section with { Reasons = reasons.Distinct().OrderBy(x => x).ToArray() };
    }

    private static GovernanceActionReportingClassification Map(RecordConfidentiality confidentiality) =>
        confidentiality switch
        {
            RecordConfidentiality.GeneralProject => Confidential,
            RecordConfidentiality.RestrictedManagement or RecordConfidentiality.ConfidentialHse or
                RecordConfidentiality.CommercialSensitive => Restricted,
            _ => throw Invalid("classification.unknown", "Owner confidentiality is unknown.")
        };

    private static GovernanceActionReportingClassification Classify(IEnumerable<RecordConfidentiality> values) =>
        values.Select(Map).Any(x => x == Restricted) ? Restricted : Confidential;

    private static Dictionary<Guid, GovernanceReportingEvent?> SelectAt<T, TState>(
        IEnumerable<T> items, Func<T, Guid> id,
        Func<T, IReadOnlyCollection<GovernanceReportingEvent>?> history,
        Func<T, DateTimeOffset> createdAt, TState initialState,
        Func<T, TState> currentState, DateTimeOffset cutoff)
        where T : AggregateRoot where TState : struct, Enum =>
        items.ToDictionary(id, x => GovernanceReportingHistory.Select(
            history(x), x.Revision, createdAt(x), initialState, currentState(x), cutoff));

    private static DateTimeOffset FirstSubmit(DecisionRequest item, DateTimeOffset cutoff) =>
        item.ReportingHistory?.FirstOrDefault(x =>
            x.State == DecisionRequestStatus.ReadyForDecision.ToString() && x.AtUtc <= cutoff)?.AtUtc
        ?? throw Invalid("decision_request.submit_history", "The first official submission is missing.");

    private static DateTimeOffset FirstAssessment(ProjectRisk item, DateTimeOffset cutoff) =>
        item.ReportingHistory?.FirstOrDefault(x =>
            x.State == RiskStatus.Assessed.ToString() && x.AtUtc <= cutoff)?.AtUtc
        ?? throw Invalid("risk.assessment_history", "The first assessed Risk event is missing.");

    private static bool SameInstant(DateTimeOffset left, DateTimeOffset? right) =>
        right.HasValue && Microseconds(left) == Microseconds(right.Value);

    private static DateTimeOffset Microseconds(DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private static GovernanceActionReportingRegister RegisterProjection<T>(
        string name, IReadOnlyCollection<T> selected) =>
        new(name, selected.Count, GovernanceActionReportingHash.Compute(selected));

    private static GovernanceActionReportingRegister Register<T>(string name, IReadOnlyCollection<T> items)
        where T : AggregateRoot => new(name, items.Count,
            GovernanceActionReportingHash.Compute(items.Select(x => new
            {
                Id = (Guid)x.GetType().GetProperty("Id")!.GetValue(x)!, x.Revision
            }).ToArray()));

    private static DomainRuleException Invalid(string code, string message) =>
        new($"action_control.project_reporting.{code}", message);
}
