using Pmcs.Modules.Projects.Endpoints;

namespace Pmcs.Modules.Projects.Services;

internal static class ProjectBootstrapContributorCatalog
{
    public const string Version = "pmcs.project-bootstrap/v1.0.0";

    public static readonly IReadOnlyCollection<ProjectBootstrapContributorResponse> Contributors =
    [
        Contributor(
            "projects.base-settings", ProjectBootstrapCategory.BaseSettings, "projects.bootstrap.create", 100, [],
            ["contractModel", "planningMode", "budgetMode", "qualityMode", "hseMode", "financeMode", "procurementMode"],
            "Active capabilities become SetupRequired; suspended capabilities become NotEnabled."),
        Contributor(
            "projects.calendar", ProjectBootstrapCategory.Calendar, "projects.bootstrap.create", 200,
            ["projects.base-settings"], ["calendarMode", "workingDaysMask"],
            "An unconfigured source calendar is skipped."),
        Contributor(
            "projects.locations", ProjectBootstrapCategory.Locations, "projects.bootstrap.create", 300,
            ["projects.base-settings"], ["active project location code", "name", "hierarchy"],
            "ROOT is retained in the destination and retired source locations are skipped."),
        Contributor(
            "projects.role-templates", ProjectBootstrapCategory.RoleTemplates, "projects.bootstrap.create", 400,
            ["projects.base-settings"], ["project-scoped role template overrides"],
            "Shared role catalog entries are referenced and project overrides are copied only when present."),
        Contributor(
            "projects.workflow-templates", ProjectBootstrapCategory.WorkflowTemplates, "projects.bootstrap.create", 500,
            ["projects.base-settings"], ["dailyReportWorkflow", "dailyCutoffLocalTime"],
            "Only allowlisted workflow defaults are copied."),
        Contributor(
            "projects.form-templates", ProjectBootstrapCategory.FormTemplates, "projects.bootstrap.create", 600,
            ["projects.workflow-templates"], ["project-scoped form template overrides"],
            "Shared forms are referenced; absent project overrides are skipped."),
        Contributor(
            "projects.report-templates", ProjectBootstrapCategory.ReportTemplates, "projects.bootstrap.create", 700,
            ["projects.base-settings"], ["reportingFrequency"],
            "Only report scheduling defaults are copied; report runs and outputs are excluded."),
        Contributor(
            "projects.lookups", ProjectBootstrapCategory.Lookups, "projects.bootstrap.create", 800,
            ["projects.base-settings"], ["lookups explicitly marked cloneable"],
            "Unknown or non-cloneable lookups fail closed."),
        Contributor(
            "identity.project-memberships", ProjectBootstrapCategory.Members,
            "projects.bootstrap.members_copy", 900, ["projects.role-templates"],
            ["existing user reference", "selected role", "Project access scope"],
            "Inactive memberships are skipped and blocked grants stop execution."),
        Contributor(
            "projects.notification-defaults", ProjectBootstrapCategory.NotificationDefaults,
            "projects.bootstrap.create", 1_000, ["identity.project-memberships"],
            ["project notification defaults"],
            "Notification history is never copied; only new destination defaults are eligible."),
        Contributor(
            "projects.group-defaults", ProjectBootstrapCategory.GroupDefaults,
            "projects.bootstrap.create", 1_100, ["identity.project-memberships"],
            ["project group defaults"],
            "Group history and conversations are never copied; only new destination defaults are eligible.")
    ];

    public static readonly IReadOnlyCollection<string> AlwaysExcluded =
    [
        "شناسه، کد، نام، تاریخ‌ها، قراردادها و مقادیر یکتای پروژه مبدأ",
        "گزارش روزانه، پیشرفت واقعی، Project State و Snapshotها",
        "تراکنش مالی، پرداخت، Voucher، Budget actual و سوابق قرارداد/تدارکات",
        "پیام، فایل، پیوست، سند فنی، RFI، Approval، Issue، Action و Notification history",
        "گفت‌وگوی Agent، Audit، Outbox، Idempotency، Offline queue و Sync state"
    ];

    public static ProjectBootstrapContributorResponse Required(ProjectBootstrapCategory category) =>
        Contributors.Single(item => item.Category == category);

    private static ProjectBootstrapContributorResponse Contributor(
        string id,
        ProjectBootstrapCategory category,
        string permission,
        int order,
        IReadOnlyCollection<string> dependencies,
        IReadOnlyCollection<string> allowlist,
        string conflictPolicy) => new(
        id,
        "1.0.0",
        category,
        permission,
        order,
        dependencies,
        allowlist,
        conflictPolicy,
        "Destination remains Draft; retry is idempotent and no operational data is copied.",
        "Contributor output, destination Draft state and preview digest are verified after execution.");
}
