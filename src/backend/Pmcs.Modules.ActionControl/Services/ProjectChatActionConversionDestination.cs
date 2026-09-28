using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.ActionControl.Persistence;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ActionControl.Services;

internal sealed class ProjectChatActionConversionDestination(
    IProjectPermissionService permissions, IProjectAssigneeDirectory assignees,
    IProjectDirectory projects, ITransactionalSideEffectWriter effects)
    : IProjectMessageConversionDestination
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public bool Supports(string destinationType) => destinationType is "Action" or "Issue";

    public async Task<ProjectMessageConversionResult> ExecuteAsync(
        ProjectMessageConversionCommand command, CancellationToken cancellationToken = default)
    {
        var permission = command.DestinationType == "Action" ? "actions.create" : "issues.create";
        if (!await permissions.HasProjectPermissionAsync(command.TenantId,
                command.ActorUserId, command.ProjectId, permission, cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.owner_permission.denied", 403);
        var project = await projects.FindProfileAsync(command.TenantId,
            command.ProjectId, cancellationToken);
        if (project is null)
            throw new ProjectMessageConversionException("collaboration.conversion.project.not_found", 404);

        await using var db = new ActionControlDbContext(new DbContextOptionsBuilder<ActionControlDbContext>()
            .UseNpgsql(command.Connection).Options);
        await db.Database.UseTransactionAsync(command.Transaction, cancellationToken);
        ProjectMessageConversionResult result;
        string auditEvent;
        if (command.DestinationType == "Action")
        {
            var details = Read<ActionDetails>(command.Details);
            if (await db.Actions.AsNoTracking().AnyAsync(item =>
                    item.TenantId == command.TenantId && item.ProjectId == command.ProjectId &&
                    item.SourceMessageId == command.MessageId, cancellationToken))
                throw new ProjectMessageConversionException("collaboration.conversion.action.already_created", 409);
            var assignee = await assignees.FindAssignableAsync(command.TenantId,
                command.ProjectId, details.AssigneeUserId, cancellationToken);
            if (assignee is null)
                throw new ProjectMessageConversionException("collaboration.conversion.assignee.not_assignable", 422);
            if (details.DueDate < LocalDate(command.At, project.TimeZone))
                throw new ProjectMessageConversionException("action.due_date.in_past", 422);
            var action = ManagementAction.CreateFromMessage(command.DestinationId,
                command.TenantId, command.ProjectId, command.MessageId,
                details.Title ?? command.MessageBody[..Math.Min(240, command.MessageBody.Length)],
                details.Description, assignee.UserId, assignee.DisplayName,
                details.DueDate, details.Priority, command.ActorUserId, command.At);
            db.Actions.Add(action);
            result = new ProjectMessageConversionResult(action.Id, "Action", action.Id.ToString("N"));
            auditEvent = "ProjectChatConvertedToAction";
        }
        else
        {
            var details = Read<IssueDetails>(command.Details);
            if (await db.Issues.AsNoTracking().AnyAsync(item =>
                    item.TenantId == command.TenantId && item.ProjectId == command.ProjectId &&
                    item.SourceModule == "collaboration" && item.SourceEntityType == "ProjectMessage" &&
                    item.SourceEntityId == command.MessageId, cancellationToken))
                throw new ProjectMessageConversionException("collaboration.conversion.issue.already_created", 409);
            if (details.Confidentiality != RecordConfidentiality.GeneralProject &&
                !await permissions.HasProjectPermissionAsync(command.TenantId,
                    command.ActorUserId, command.ProjectId, "governance.sensitive.write", cancellationToken))
                throw new ProjectMessageConversionException("collaboration.conversion.sensitive.denied", 403);
            var owner = await assignees.FindAssignableAsync(command.TenantId,
                command.ProjectId, details.OwnerUserId, cancellationToken);
            if (owner is null)
                throw new ProjectMessageConversionException("collaboration.conversion.owner.not_assignable", 422);
            if (details.TargetResolutionDate < LocalDate(command.At, project.TimeZone))
                throw new ProjectMessageConversionException("governance.issue.target.in_past", 422);
            var rules = await db.SlaRules.AsNoTracking().Where(item =>
                item.TenantId == command.TenantId && item.ProjectId == command.ProjectId &&
                item.EntityType == SlaEntityType.Issue && item.EffectiveFrom <= command.At &&
                (item.Severity == details.Severity || item.Severity == null))
                .OrderByDescending(item => item.Severity == details.Severity)
                .ThenByDescending(item => item.Version).Take(2)
                .ToArrayAsync(cancellationToken);
            var deadline = rules.Length == 0 ? null :
                GovernanceDeadlineCalculator.Calculate(command.At, rules[0], project);
            var evidenceReferences = new[]
            {
                $"collaboration:message:{command.MessageId:N}:revision:{command.MessageRevision}"
            }.Concat(command.Documents.Select(item =>
                $"collaboration:document:{item.Id:N}:sha256:{item.Sha256}"))
                .ToArray();
            var issue = ManagementIssue.Create(command.DestinationId,
                command.TenantId, command.ProjectId, details.Title,
                details.ObservedFact ?? command.MessageBody, details.Category,
                details.Severity, details.Urgency, owner.UserId, owner.DisplayName,
                details.TargetResolutionDate, "collaboration", "ProjectMessage",
                command.MessageId, command.MessageRevision,
                command.MessageBody[..Math.Min(1_000, command.MessageBody.Length)],
                null, evidenceReferences,
                details.Confidentiality, deadline?.DueAt, deadline?.RuleVersionId,
                command.ActorUserId, command.At);
            db.Issues.Add(issue);
            result = new ProjectMessageConversionResult(issue.Id, "Issue", issue.Number);
            auditEvent = "ProjectChatConvertedToIssue";
        }
        if (!await permissions.HasProjectPermissionAsync(command.TenantId,
                command.ActorUserId, command.ProjectId, permission, cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.owner_permission.denied", 403);
        if (command.DestinationType == "Issue" &&
            Read<IssueDetails>(command.Details).Confidentiality != RecordConfidentiality.GeneralProject &&
            !await permissions.HasProjectPermissionAsync(command.TenantId, command.ActorUserId,
                command.ProjectId, "governance.sensitive.write", cancellationToken))
            throw new ProjectMessageConversionException("collaboration.conversion.sensitive.denied", 403);
        await db.SaveChangesAsync(cancellationToken);
        await effects.WriteEventAsync(command.Connection, command.Transaction,
            new TransactionalEventBatch(
                new AuditEntry(command.TenantId, command.ProjectId, command.ActorUserId,
                    auditEvent, result.DestinationType == "Action" ? "ManagementAction" : "ManagementIssue",
                    result.DestinationId.ToString(), command.At,
                    new Dictionary<string, object?>
                    {
                        ["sourceMessageId"] = command.MessageId,
                        ["sourceRevision"] = command.MessageRevision,
                        ["documentIds"] = command.Documents.Select(item => item.Id).ToArray()
                    }, command.CorrelationId),
                new OutboxEnvelope(Guid.NewGuid(), command.TenantId, command.ProjectId,
                    $"ActionControl.{auditEvent}", 1, command.At,
                    JsonSerializer.Serialize(new { result.DestinationId, command.MessageId,
                        command.MessageRevision }, JsonOptions), command.CorrelationId)), cancellationToken);
        return result;
    }

    private static T Read<T>(JsonElement value)
    {
        try
        {
            return value.Deserialize<T>(JsonOptions) ??
                throw new JsonException("Conversion details are required.");
        }
        catch (JsonException)
        {
            throw new ProjectMessageConversionException("collaboration.conversion.details.invalid", 422);
        }
    }

    private static DateOnly LocalDate(DateTimeOffset at, string zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at,
            TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);

    private sealed record ActionDetails(Guid AssigneeUserId, DateOnly DueDate,
        ActionPriority Priority, string? Title, string? Description);

    private sealed record IssueDetails(string Title, string? ObservedFact,
        string Category, GovernanceSeverity Severity, GovernanceUrgency Urgency,
        Guid OwnerUserId, DateOnly TargetResolutionDate,
        RecordConfidentiality Confidentiality);
}
