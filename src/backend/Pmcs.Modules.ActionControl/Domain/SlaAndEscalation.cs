using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ActionControl.Domain;

public sealed class SlaRuleVersion : AggregateRoot
{
    private SlaRuleVersion() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public SlaEntityType EntityType { get; private set; }
    public GovernanceSeverity? Severity { get; private set; }
    public int Duration { get; private set; }
    public SlaDurationUnit DurationUnit { get; private set; }
    public int WarningLeadMinutes { get; private set; }
    public int EscalationDelayMinutes { get; private set; }
    public Guid EscalationRecipientUserId { get; private set; }
    public string EscalationRecipientDisplayName { get; private set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SlaRuleVersion Create(Guid id, Guid tenantId, Guid projectId, int version,
        string title, SlaEntityType entityType, GovernanceSeverity? severity, int duration,
        SlaDurationUnit durationUnit, int warningLeadMinutes, int escalationDelayMinutes,
        Guid escalationRecipientUserId, string escalationRecipientDisplayName,
        DateTimeOffset effectiveFrom, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Identities(id, tenantId, projectId, escalationRecipientUserId, actor);
        if (version <= 0 || !Enum.IsDefined(entityType) || !Enum.IsDefined(durationUnit) ||
            (severity.HasValue && !Enum.IsDefined(severity.Value)) || duration <= 0 || duration > 365 ||
            (entityType != SlaEntityType.Issue && severity.HasValue) ||
            warningLeadMinutes < 0 || warningLeadMinutes > 43_200 || escalationDelayMinutes < 0 || escalationDelayMinutes > 43_200)
            throw new DomainRuleException("governance.sla.rule.invalid", "Service-level rule is invalid.");

        return new SlaRuleVersion
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Version = version,
            Title = GovernanceRules.Required(title, 200, "governance.sla.title.invalid"),
            EntityType = entityType,
            Severity = severity,
            Duration = duration,
            DurationUnit = durationUnit,
            WarningLeadMinutes = warningLeadMinutes,
            EscalationDelayMinutes = escalationDelayMinutes,
            EscalationRecipientUserId = escalationRecipientUserId,
            EscalationRecipientDisplayName = GovernanceRules.Required(escalationRecipientDisplayName, 200, "governance.sla.recipient.invalid"),
            EffectiveFrom = effectiveFrom,
            CreatedBy = actor,
            CreatedAt = at
        };
    }
}

public sealed record GovernanceDeadline(
    DateTimeOffset DueAt,
    DateTimeOffset WarningAt,
    DateTimeOffset EscalationAt,
    Guid RuleVersionId,
    int RuleVersion);

public static class GovernanceDeadlineCalculator
{
    public static GovernanceDeadline? Calculate(DateTimeOffset createdAt, SlaRuleVersion rule, ProjectControlProfile project)
    {
        DateTimeOffset? dueAt = rule.DurationUnit switch
        {
            SlaDurationUnit.ElapsedHours => createdAt.AddHours(rule.Duration),
            SlaDurationUnit.ProjectWorkingDays => AddWorkingDays(createdAt, rule.Duration, project),
            _ => null
        };
        return dueAt.HasValue
            ? new GovernanceDeadline(dueAt.Value, dueAt.Value.AddMinutes(-rule.WarningLeadMinutes),
                dueAt.Value.AddMinutes(rule.EscalationDelayMinutes), rule.Id, rule.Version)
            : null;
    }

    public static DeadlineSignal Signal(DateTimeOffset now, DateTimeOffset dueAt, int warningLeadMinutes, int escalationDelayMinutes) =>
        now >= dueAt.AddMinutes(escalationDelayMinutes) ? DeadlineSignal.EscalationDue :
        now >= dueAt ? DeadlineSignal.Overdue :
        now >= dueAt.AddMinutes(-warningLeadMinutes) ? DeadlineSignal.DueSoon : DeadlineSignal.OnTrack;

    private static DateTimeOffset? AddWorkingDays(DateTimeOffset createdAt, int duration, ProjectControlProfile project)
    {
        if (project.Calendar.State != ProjectCalendarConfigurationState.Configured || !project.Calendar.WorkingDaysMask.HasValue)
            return null;
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone); }
        catch (TimeZoneNotFoundException exception) { throw new DomainRuleException("project.time_zone.unavailable", exception.Message); }
        catch (InvalidTimeZoneException exception) { throw new DomainRuleException("project.time_zone.invalid", exception.Message); }

        var local = TimeZoneInfo.ConvertTime(createdAt, timeZone).DateTime;
        var remaining = duration;
        while (remaining > 0)
        {
            local = local.AddDays(1);
            if (project.Calendar.IsWorkingDay(local.DayOfWeek)) remaining--;
        }
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone), TimeSpan.Zero);
    }
}

public sealed class EscalationThread : AggregateRoot
{
    private EscalationThread() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string ThreadKey { get; private set; } = string.Empty;
    public SlaEntityType EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public string EntityNumber { get; private set; } = string.Empty;
    public string EntityTitle { get; private set; } = string.Empty;
    public EscalationReason Reason { get; private set; }
    public int Level { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string RecipientDisplayName { get; private set; } = string.Empty;
    public RecordConfidentiality Confidentiality { get; private set; }
    public EscalationStatus Status { get; private set; }
    public int OccurrenceCount { get; private set; }
    public DateTimeOffset FirstRaisedAt { get; private set; }
    public DateTimeOffset LastRaisedAt { get; private set; }
    public Guid? AcknowledgedBy { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public string? AcknowledgementNote { get; private set; }

    public static string Key(SlaEntityType entityType, Guid entityId, EscalationReason reason, int level) =>
        $"{entityType}:{entityId:N}:{reason}:{level}".ToLowerInvariant();

    public static EscalationThread Raise(Guid id, Guid tenantId, Guid projectId, SlaEntityType entityType,
        Guid entityId, string entityNumber, string entityTitle, EscalationReason reason, int level,
        Guid recipientUserId, string recipientDisplayName, RecordConfidentiality confidentiality,
        DateTimeOffset at)
    {
        GovernanceRules.Identities(id, tenantId, projectId, entityId, recipientUserId);
        if (!Enum.IsDefined(entityType) || !Enum.IsDefined(reason) || !Enum.IsDefined(confidentiality) || level is < 1 or > 5)
            throw new DomainRuleException("governance.escalation.invalid", "Escalation classification is invalid.");
        return new EscalationThread
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            ThreadKey = Key(entityType, entityId, reason, level),
            EntityType = entityType,
            EntityId = entityId,
            EntityNumber = GovernanceRules.Required(entityNumber, 80, "governance.escalation.entity_number.invalid"),
            EntityTitle = GovernanceRules.Required(entityTitle, 500, "governance.escalation.title.invalid"),
            Reason = reason,
            Level = level,
            RecipientUserId = recipientUserId,
            RecipientDisplayName = GovernanceRules.Required(recipientDisplayName, 200, "governance.escalation.recipient.invalid"),
            Confidentiality = confidentiality,
            Status = EscalationStatus.Open,
            OccurrenceCount = 1,
            FirstRaisedAt = at,
            LastRaisedAt = at
        };
    }

    public void Touch(DateTimeOffset at)
    {
        LastRaisedAt = at;
        OccurrenceCount++;
        AdvanceRevision();
    }

    public void Acknowledge(long baseRevision, Guid actor, string? note, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.escalation.revision.conflict");
        GovernanceRules.Identities(actor);
        if (Status == EscalationStatus.ClosedBySourceResolution)
            throw new DomainRuleException("governance.escalation.acknowledge.invalid_state", "Closed escalation cannot be acknowledged.");
        Status = EscalationStatus.Acknowledged;
        AcknowledgedBy = actor;
        AcknowledgedAt = at;
        AcknowledgementNote = GovernanceRules.Optional(note, 1_000, "governance.escalation.note.invalid");
        AdvanceRevision();
    }

    public void CloseFromSource(DateTimeOffset at)
    {
        if (Status == EscalationStatus.ClosedBySourceResolution) return;
        Status = EscalationStatus.ClosedBySourceResolution;
        LastRaisedAt = at;
        AdvanceRevision();
    }
}
