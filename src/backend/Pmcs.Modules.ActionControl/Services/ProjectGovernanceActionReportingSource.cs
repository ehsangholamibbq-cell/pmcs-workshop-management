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
            risks.Any(x => x.LastReviewedAt <= cutoff &&
                x.MaterializedIssueId is { } id && !issueIds.Contains(id)) ||
            risks.Any(x => x.LastReviewedAt <= cutoff &&
                x.MatrixVersionId is { } id && !matrixIds.Contains(id)) ||
            decisions.Any(x => !requestIds.Contains(x.DecisionRequestId) ||
                x.SupersedesDecisionId is { } predecessor && !decisionIds.Contains(predecessor) ||
                x.SupersededByDecisionId is { } successor && !decisionIds.Contains(successor)) ||
            escalations.Any(x => !(x.EntityType switch
            {
                SlaEntityType.Issue => issueIds.Contains(x.EntityId),
                SlaEntityType.Risk => riskIds.Contains(x.EntityId),
                SlaEntityType.DecisionRequest => requestIds.Contains(x.EntityId),
                _ => false
            })) ||
            escalations.Select(x => x.ThreadKey).Distinct(StringComparer.Ordinal).Count() != escalations.Length)
            throw Invalid("linkage.invalid", "An owner link cannot be proved inside the project scope.");
        if (risks.Any(x => x.LastReviewedAt <= cutoff && x.MaterializedIssueId is { } id &&
                issues.Single(y => y.Id == id).MaterializedFromRiskId != x.Id) ||
            issues.Any(x => x.MaterializedFromRiskId is { } id &&
                risks.Single(y => y.Id == id).LastReviewedAt <= cutoff &&
                risks.Single(y => y.Id == id).MaterializedIssueId != x.Id))
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
        if (risks.Any(x => x.LastReviewedAt <= cutoff && x.MatrixVersionId is { } id &&
                (matrixById[id].Version != x.MatrixVersion ||
                 matrixById[id].FormulaVersion != x.FormulaVersion ||
                 matrixById[id].EffectiveFrom > x.LastReviewedAt)))
            throw Invalid("matrix.version", "A Risk assessment does not match its pinned matrix.");
        var decisionById = decisions.ToDictionary(x => x.Id);
        foreach (var record in decisions)
        {
            if (record.SupersedesDecisionId is { } predecessor &&
                (decisionById[predecessor].DecisionRequestId != record.DecisionRequestId ||
                 decisionById[predecessor].SupersededByDecisionId != record.Id) ||
                record.SupersededByDecisionId is { } successor &&
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

        var issue = Section(issues.Any(x => x.LastChangedAt > cutoff),
            issues.Select(x => Fact(x.Id, x.Number, GovernanceActionFactKind.Issue,
                x.Status.ToString(), x.CreatedAt, x.TargetResolutionDate,
                CertifiedSla(x.SlaRuleVersionId, x.SlaDueAt, ruleById, project),
                x.Severity.ToString(), null, Map(x.Confidentiality))).ToArray(),
            GovernanceActionReportingReason.NoOfficialIssue);
        issue = WithSlaReasons(issue, issues.Select(x => x.SlaRuleVersionId), ruleById, project);
        var risk = Section(risks.Any(x => x.LastReviewedAt > cutoff),
            risks.Select(x => Fact(x.Id, x.Number, GovernanceActionFactKind.Risk,
                x.Status.ToString(), x.CreatedAt, x.ReviewDate,
                CertifiedSla(x.SlaRuleVersionId, x.SlaDueAt, ruleById, project),
                x.MatrixVersionId.HasValue ?
                    (x.ResidualRating ?? x.InherentRating)?.ToString() : null,
                x.MatrixVersionId.HasValue ? x.MatrixVersion : null,
                Map(x.Confidentiality))).ToArray(),
            GovernanceActionReportingReason.NoOfficialRisk);
        risk = WithSlaReasons(risk, risks.Select(x => x.SlaRuleVersionId), ruleById, project);
        if (risk.Status == GovernanceActionReportingStatus.Available &&
            risks.Any(x => !x.MatrixVersionId.HasValue && x.InherentRating.HasValue))
            risk = risk with { Reasons = risk.Reasons.Append(
                GovernanceActionReportingReason.MatrixVersionUnavailable).Distinct().OrderBy(x => x).ToArray() };
        if (risks.Length > 0 && risks.All(x => !x.InherentRating.HasValue) &&
            risk.Status == GovernanceActionReportingStatus.Available)
            risk = risk with { Reasons = risk.Reasons.Append(GovernanceActionReportingReason.NoAssessedRisk).ToArray() };
        // Only the first Submit transition (Revision 2) has an independently
        // provable official timestamp in the legacy request aggregate.
        var decision = Section(requests.Any(x => x.LastChangedAt > cutoff ||
                    x.Status != DecisionRequestStatus.Draft &&
                    (x.Revision != 2 || x.Status != DecisionRequestStatus.ReadyForDecision ||
                     x.LastChangedAt is null)) ||
                decisions.Any(x => x.Status != DecisionRecordStatus.Recorded),
            requests.Where(x => x.Status != DecisionRequestStatus.Draft)
                .Select(x => Fact(x.Id, x.Number, GovernanceActionFactKind.DecisionRequest,
                    x.Status.ToString(), x.LastChangedAt ?? x.CreatedAt, x.RequiredBy,
                    CertifiedSla(x.SlaRuleVersionId, x.SlaDueAt, ruleById, project),
                    null, null, Map(x.Confidentiality)))
                .Concat(decisions.Select(x => Fact(x.Id, x.Number,
                    GovernanceActionFactKind.DecisionRecord, x.Status.ToString(),
                    x.RecordedAt, x.EffectiveDate, null, null, null,
                    Map(requests.Single(y => y.Id == x.DecisionRequestId).Confidentiality))))
                .ToArray(), GovernanceActionReportingReason.NoSubmittedDecision);
        decision = WithSlaReasons(decision, requests.Select(x => x.SlaRuleVersionId), ruleById, project);
        var escalation = Section(escalations.Any(x => x.LastRaisedAt > cutoff || x.AcknowledgedAt > cutoff),
            escalations.Select(x => Fact(x.Id, "ESC-" + x.Id.ToString("N"),
                GovernanceActionFactKind.Escalation, x.Status.ToString(),
                x.FirstRaisedAt, null, null, x.Reason.ToString(), null,
                Map(x.Confidentiality))).ToArray(),
            GovernanceActionReportingReason.NoRaisedEscalation);
        // No Action field proves the sensitivity of SourceFactId. The versioned owner
        // publication policy labels every Action Restricted, with no Daily Fact join.
        var action = Section(actions.Any(x => x.LastChangedAt > cutoff),
            actions.Select(x => Fact(x.Id, x.Id.ToString("N"),
                GovernanceActionFactKind.Action, x.Status.ToString(), x.CreatedAt,
                x.DueDate, null, x.Priority.ToString(), null, Restricted)).ToArray(),
            GovernanceActionReportingReason.NoOfficialAction);
        var sections = new[] { issue, risk, decision, escalation, action };
        var classification = sections.Any(x => x.Classification == Restricted) ? Restricted : Confidential;
        var status = sections.Any(x => x.Status == GovernanceActionReportingStatus.InsufficientData)
            ? GovernanceActionReportingStatus.InsufficientData
            : sections.Any(x => x.Status == GovernanceActionReportingStatus.Available)
                ? GovernanceActionReportingStatus.Available : GovernanceActionReportingStatus.NoData;
        var registers = new[]
        {
            Register("issues", issues), Register("risks", risks),
            Register("decision_requests", requests), Register("decisions", decisions),
            Register("escalation_threads", escalations), Register("actions", actions),
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
        IReadOnlyCollection<GovernanceActionReportingFact> facts, GovernanceActionReportingReason emptyReason)
    {
        var classification = facts.Any(x => x.Classification == Restricted) ? Restricted : Confidential;
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

    private static GovernanceActionReportingRegister Register<T>(string name, IReadOnlyCollection<T> items)
        where T : AggregateRoot => new(name, items.Count,
            GovernanceActionReportingHash.Compute(items.Select(x => new
            {
                Id = (Guid)x.GetType().GetProperty("Id")!.GetValue(x)!, x.Revision
            }).ToArray()));

    private static DomainRuleException Invalid(string code, string message) =>
        new($"action_control.project_reporting.{code}", message);
}
