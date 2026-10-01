using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Projects.Endpoints;
using Pmcs.Modules.Projects.Persistence;

namespace Pmcs.Modules.Projects.Services;

internal sealed class ProjectReadinessEvaluator(
    IProjectPermissionService permissionService,
    ProjectsDbContext dbContext)
{
    public async Task<ProjectReadinessResponse> EvaluateAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        var access = await permissionService.GetProjectAccessReadinessAsync(
            project.TenantId, project.Id, cancellationToken);
        var hasRootLocation = await dbContext.ProjectLocations.AsNoTracking().AnyAsync(
            location => location.TenantId == project.TenantId &&
                location.ProjectId == project.Id &&
                location.ParentLocationId == null &&
                location.Code == "ROOT" &&
                location.Status == ProjectLocationStatus.Active,
            cancellationToken);

        var items = new List<ProjectReadinessItem>();
        Add("identity", "مشخصات پایه پروژه",
            project.ProjectType != ProjectType.NotConfigured &&
            project.ExecutionPhase != ProjectExecutionPhase.NotConfigured &&
            project.CountryCode.Length == 2 &&
            !string.IsNullOrWhiteSpace(project.Region) &&
            !string.IsNullOrWhiteSpace(project.ShortDescription),
            "نوع، مرحله اجرا، کشور، منطقه و شرح کوتاه باید کامل باشد.");
        Add("dates", "تاریخ‌های اصلی",
            project.StartDate.HasValue && project.PlannedFinishDate.HasValue &&
            project.PlannedFinishDate.Value >= project.StartDate.Value,
            "تاریخ شروع و پایان برنامه‌ای معتبر لازم است.");
        Add("contract-model", "مدل قراردادی پایه", project.ContractModel != ContractModel.NotConfigured,
            "انتخاب مدل قراردادی پایه الزامی است؛ ثبت قرارداد رسمی می‌تواند بعداً انجام شود.");
        Add("location-root", "ساختار مکانی پایه", hasRootLocation,
            "ریشهٔ فعال «کل پروژه» باید وجود داشته باشد.");
        Add("project-manager", "مدیر پروژه", access.ActiveProjectManagerCount > 0,
            "حداقل یک عضویت فعال با نقش مدیر پروژه لازم است.");
        Add("tenant-admin", "مدیر سازمان", access.ActiveAdministratorCount > 0,
            "حداقل یک مدیر فعال سازمان برای بازیابی و کنترل دسترسی لازم است.");
        Add("operational-users", "کاربر عملیاتی", access.ActiveOperationalUserCount > 0,
            "حداقل یک کاربر فعال عملیاتی باید به پروژه دسترسی داشته باشد.");
        Add("calendar", "تقویم کاری و منطقه زمانی",
            project.CalendarMode == ProjectCalendarMode.WorkingWeek &&
            project.WorkingDaysMask is > 0 &&
            !string.IsNullOrWhiteSpace(project.TimeZone),
            "روزهای کاری و منطقه زمانی IANA باید مشخص باشند.");
        Add("units", "واحد اندازه‌گیری", project.UnitSystem != ProjectUnitSystem.NotConfigured,
            "سامانه واحدهای پروژه باید تعیین شود.");
        Add("daily-report", "گردش گزارش روزانه",
            project.DailyCutoffLocalTime.HasValue &&
            project.ReportingFrequency != ReportingFrequency.NotConfigured &&
            project.DailyReportWorkflow != DailyReportWorkflow.NotConfigured,
            "زمان قطع، بسامد و گردش تأیید گزارش روزانه باید تعیین شود.");
        Add("offline-policy", "سیاست کار آفلاین", project.OfflinePolicyAccepted,
            "سیاست ثبت آفلاین، همگام‌سازی و تعارض باید پذیرفته شود.");
        items.Add(new ProjectReadinessItem(
            "audit-policy", "ممیزی تغییرات", ProjectReadinessStatus.Passed,
            "فرمان‌های راه‌اندازی فقط همراه ممیزی تراکنشی ثبت می‌شوند."));

        AddModule("planning", "برنامه‌ریزی",
            project.PlanningMode == PlanningMode.None || project.PlanningMode == PlanningMode.SimpleWorkList,
            project.PlanningMode is PlanningMode.Milestones or PlanningMode.WbsBaseline or PlanningMode.ExternalSchedule);
        AddCapability("budget", "بودجه", project.BudgetMode);
        AddCapability("finance", "مالی", project.FinanceMode);
        AddCapability("procurement", "تدارکات", project.ProcurementMode);
        AddCapability("quality", "کیفیت", project.QualityMode);
        AddCapability("hse", "ایمنی", project.HseMode);

        var blocked = items.Count(item => item.Status == ProjectReadinessStatus.Blocked);
        var completion = items.Count == 0
            ? 0
            : (int)Math.Round(100m * (items.Count - blocked) / items.Count, MidpointRounding.AwayFromZero);
        return new ProjectReadinessResponse(
            project.Id, project.ConfigurationVersion, blocked == 0, completion, items);

        void Add(string code, string title, bool passed, string blockedDetail) => items.Add(
            new ProjectReadinessItem(
                code,
                title,
                passed ? ProjectReadinessStatus.Passed : ProjectReadinessStatus.Blocked,
                passed ? "آماده است." : blockedDetail));

        void AddModule(string code, string title, bool ready, bool selectedWithoutEvidence)
        {
            items.Add(new ProjectReadinessItem(
                $"module-{code}",
                $"آمادگی ماژول {title}",
                selectedWithoutEvidence ? ProjectReadinessStatus.Blocked :
                    ready ? ProjectReadinessStatus.Passed : ProjectReadinessStatus.Warning,
                selectedWithoutEvidence
                    ? "ماژول انتخاب شده اما شواهد آماده‌بودن تنظیمات آن ثبت نشده است."
                    : ready ? "برای راه‌اندازی پایه آماده است." : "ماژول در راه‌اندازی پایه الزامی نیست."));
        }

        void AddCapability(string code, string title, CapabilityMode mode)
        {
            items.Add(new ProjectReadinessItem(
                $"module-{code}",
                $"آمادگی ماژول {title}",
                mode == CapabilityMode.Active ? ProjectReadinessStatus.Blocked :
                    mode == CapabilityMode.Suspended ? ProjectReadinessStatus.Warning : ProjectReadinessStatus.Passed,
                mode == CapabilityMode.Active
                    ? "ماژول فعال انتخاب شده اما Gate اختصاصی تنظیمات آن هنوز تأیید نشده است."
                    : mode == CapabilityMode.SetupRequired
                        ? "راه‌اندازی این ماژول به بعد از فعال‌سازی پایه موکول شده است."
                        : mode == CapabilityMode.Suspended
                            ? "ماژول تعلیق شده و در راه‌اندازی پایه وارد نمی‌شود."
                            : "ماژول برای راه‌اندازی پایه غیرفعال است."));
        }
    }
}
