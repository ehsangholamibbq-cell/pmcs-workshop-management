using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.WorkManagement.Contracts;
using Pmcs.Modules.WorkManagement.Domain;
using Pmcs.Modules.WorkManagement.Persistence;

namespace Pmcs.Modules.WorkManagement.Services;

internal sealed class WorkManagementQueryService(
    IProjectPermissionService permissions,
    IProjectDirectory projectDirectory,
    IDailyReportWorkSource dailyReports,
    IManagementActionWorkSource actions,
    WorkManagementDbContext dbContext,
    IClock clock) : IWorkManagementQueryService
{
    public async Task<WorkManagementQueryResult<MyWorkResponse>> GetMyWorkAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(tenantId, userId, projectId, "projects.read", cancellationToken))
        {
            return new(WorkManagementQueryStatus.Forbidden, null);
        }

        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            return new(WorkManagementQueryStatus.ProjectNotFound, null);
        }

        var canReadReports = await HasPermissionAsync(
            tenantId, userId, projectId, "field.daily-reports.read", cancellationToken);
        var canReviewReports = canReadReports && await HasPermissionAsync(
            tenantId, userId, projectId, "field.daily-reports.review", cancellationToken);
        var canReadActions = await HasPermissionAsync(
            tenantId, userId, projectId, "actions.read", cancellationToken);

        var reportTask = canReadReports
            ? dailyReports.ListAsync(tenantId, projectId, userId, canReviewReports, cancellationToken)
            : Task.FromResult<IReadOnlyCollection<DailyReportWorkRecord>>([]);
        var actionTask = canReadActions
            ? actions.ListAssignedAsync(tenantId, projectId, userId, cancellationToken)
            : Task.FromResult<IReadOnlyCollection<ManagementActionWorkRecord>>([]);

        await Task.WhenAll(reportTask, actionTask);
        var today = ResolveLocalDate(clock.UtcNow, project.TimeZone);
        var items = reportTask.Result.Select(report => Map(report, today))
            .Concat(actionTask.Result.Select(action => Map(action, today)))
            .OrderByDescending(item => item.IsOverdue)
            .ThenBy(item => item.DueDate ?? item.ReferenceDate)
            .ThenByDescending(item => item.Priority == "Critical")
            .ThenByDescending(item => item.ChangedAt)
            .Take(300)
            .ToArray();

        var unreadCount = await dbContext.Notifications.AsNoTracking().CountAsync(
            notification => notification.TenantId == tenantId &&
                notification.ProjectId == projectId &&
                notification.RecipientUserId == userId &&
                notification.ReadAt == null,
            cancellationToken);

        return new(
            WorkManagementQueryStatus.Success,
            new MyWorkResponse(clock.UtcNow, unreadCount, items));
    }

    public async Task<WorkManagementQueryResult<IReadOnlyCollection<InAppNotificationResponse>>> ListNotificationsAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(tenantId, userId, projectId, "projects.read", cancellationToken))
        {
            return new(WorkManagementQueryStatus.Forbidden, null);
        }

        var query = dbContext.Notifications.AsNoTracking().Where(notification =>
            notification.TenantId == tenantId && notification.ProjectId == projectId &&
            notification.RecipientUserId == userId);
        if (unreadOnly)
        {
            query = query.Where(notification => notification.ReadAt == null);
        }

        var notifications = await query
            .OrderBy(notification => notification.ReadAt != null)
            .ThenByDescending(notification => notification.OccurredAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        return new(
            WorkManagementQueryStatus.Success,
            notifications.Select(Map).ToArray());
    }

    private Task<bool> HasPermissionAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken) =>
        permissions.HasProjectPermissionAsync(tenantId, userId, projectId, permission, cancellationToken);

    private static MyWorkItemResponse Map(DailyReportWorkRecord report, DateOnly today)
    {
        var (kind, title, description, priority, status) = report.Kind switch
        {
            DailyReportWorkKind.Review => (
                "DailyReportReview",
                "بازبینی گزارش روزانه",
                "گزارش ارسال‌شده در انتظار تصمیم شماست.",
                "High",
                "WaitingForReview"),
            DailyReportWorkKind.CorrectReturned => (
                "DailyReportCorrection",
                "اصلاح گزارش عودت‌شده",
                report.CorrectionReason,
                "High",
                "CorrectionRequired"),
            DailyReportWorkKind.CompleteCorrection => (
                "DailyReportCorrection",
                "تکمیل نسخه اصلاحی گزارش",
                report.CorrectionReason,
                "Medium",
                "DraftCorrection"),
            _ => throw new ArgumentOutOfRangeException(nameof(report), report.Kind, "Unsupported report work kind.")
        };
        return new MyWorkItemResponse(
            $"daily-report:{report.ReportId}",
            kind,
            title,
            description,
            report.ReportDate,
            report.ReportDate,
            priority,
            status,
            "DailyReport",
            report.ReportId,
            report.ChangedAt,
            report.Revision,
            report.ReportDate < today);
    }

    private static MyWorkItemResponse Map(ManagementActionWorkRecord action, DateOnly today) => new(
        $"management-action:{action.ActionId}",
        "ManagementAction",
        action.Title,
        action.Description,
        action.DueDate,
        null,
        action.Priority.ToString(),
        action.Status.ToString(),
        "ManagementAction",
        action.ActionId,
        action.ChangedAt,
        action.Revision,
        action.DueDate < today);

    private static InAppNotificationResponse Map(InAppNotification notification) => new(
        notification.Id,
        notification.ProjectId,
        notification.Category,
        notification.Title,
        notification.Body,
        notification.TargetType,
        notification.TargetId,
        notification.OccurredAt,
        notification.ReadAt,
        notification.AcknowledgedAt,
        notification.Revision);

    private static DateOnly ResolveLocalDate(DateTimeOffset now, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
    }
}
