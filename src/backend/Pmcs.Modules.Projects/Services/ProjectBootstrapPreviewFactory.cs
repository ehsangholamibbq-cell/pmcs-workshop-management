using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Projects.Endpoints;

namespace Pmcs.Modules.Projects.Services;

internal sealed record ProjectBootstrapPreviewBuild(
    ProjectBootstrapPreviewDocument Document,
    string Digest,
    string MembershipSnapshotToken);

internal sealed class ProjectBootstrapPreviewFactory(
    IProjectMembershipBootstrapService membershipBootstrap)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task<ProjectBootstrapPreviewBuild> BuildAsync(
        Guid tenantId,
        Guid actorUserId,
        Project source,
        Project target,
        IReadOnlyCollection<ProjectLocation> sourceLocations,
        IReadOnlyCollection<ProjectLocation> targetLocations,
        IReadOnlyCollection<ProjectBootstrapCategory> categories,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> memberSelections,
        CancellationToken cancellationToken)
    {
        var normalizedCategories = NormalizeCategories(categories);
        var projectItems = BuildProjectItems(source, target, sourceLocations, targetLocations, normalizedCategories);
        var memberItems = Array.Empty<ProjectBootstrapItemResponse>();
        var membershipSnapshotToken = string.Empty;

        if (normalizedCategories.Contains(ProjectBootstrapCategory.Members))
        {
            if (memberSelections.Count == 0)
            {
                memberItems =
                [
                    Item(
                        ProjectBootstrapCategory.Members,
                        ProjectMembershipBootstrapDisposition.Skipped,
                        "bootstrap.members.none_selected",
                        "اعضای پروژه",
                        "هیچ عضوی برای انتقال انتخاب نشده است.")
                ];
            }
            else
            {
                var preview = await membershipBootstrap.PreviewAsync(
                    tenantId,
                    actorUserId,
                    source.Id,
                    target.Id,
                    memberSelections,
                    cancellationToken);
                membershipSnapshotToken = preview.SnapshotToken;
                memberItems = preview.Items.Select(member => new ProjectBootstrapItemResponse(
                    preview.ContributorId,
                    ProjectBootstrapCategory.Members,
                    member.Disposition,
                    member.Code,
                    member.DisplayName,
                    member.Detail,
                    member.UserId.ToString(),
                    $"{member.RequestedRoleCode}:{member.AccessScope}"))
                    .ToArray();
            }
        }

        return Build(
            tenantId,
            source,
            target,
            sourceLocations,
            targetLocations,
            normalizedCategories,
            memberSelections,
            projectItems.Concat(memberItems).ToArray(),
            membershipSnapshotToken);
    }

    public ProjectBootstrapPreviewBuild RebuildProjectState(
        Guid tenantId,
        Project source,
        Project target,
        IReadOnlyCollection<ProjectLocation> sourceLocations,
        IReadOnlyCollection<ProjectLocation> targetLocations,
        IReadOnlyCollection<ProjectBootstrapCategory> categories,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> memberSelections,
        IReadOnlyCollection<ProjectBootstrapItemResponse> storedMemberItems,
        string membershipSnapshotToken)
    {
        var normalizedCategories = NormalizeCategories(categories);
        var items = BuildProjectItems(source, target, sourceLocations, targetLocations, normalizedCategories)
            .Concat(storedMemberItems)
            .ToArray();
        return Build(
            tenantId,
            source,
            target,
            sourceLocations,
            targetLocations,
            normalizedCategories,
            memberSelections,
            items,
            membershipSnapshotToken);
    }

    private static ProjectBootstrapPreviewBuild Build(
        Guid tenantId,
        Project source,
        Project target,
        IReadOnlyCollection<ProjectLocation> sourceLocations,
        IReadOnlyCollection<ProjectLocation> targetLocations,
        IReadOnlyCollection<ProjectBootstrapCategory> categories,
        IReadOnlyCollection<ProjectMembershipBootstrapSelection> memberSelections,
        IReadOnlyCollection<ProjectBootstrapItemResponse> items,
        string membershipSnapshotToken)
    {
        var contributors = categories
            .Select(ProjectBootstrapContributorCatalog.Required)
            .OrderBy(item => item.Order)
            .ToArray();
        var document = new ProjectBootstrapPreviewDocument(
            source.Code,
            categories,
            contributors,
            items,
            ProjectBootstrapContributorCatalog.AlwaysExcluded);
        var digestMaterial = JsonSerializer.Serialize(new
        {
            catalogVersion = ProjectBootstrapContributorCatalog.Version,
            tenantId,
            sourceProjectId = source.Id,
            sourceRevision = source.Revision,
            targetProjectId = target.Id,
            targetRevision = target.Revision,
            categories,
            memberSelections = memberSelections
                .OrderBy(item => item.UserId)
                .Select(item => new { item.UserId, item.RoleCode, item.AccessScope }),
            sourceLocations = sourceLocations
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .Select(item => new
                {
                    item.Id,
                    item.Code,
                    item.Name,
                    item.ParentLocationId,
                    item.Status,
                    item.Revision
                }),
            targetLocations = targetLocations
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .Select(item => new
                {
                    item.Id,
                    item.Code,
                    item.Name,
                    item.ParentLocationId,
                    item.Status,
                    item.Revision
                }),
            membershipSnapshotToken,
            items = items.Select(item => new
            {
                item.ContributorId,
                item.Category,
                item.Disposition,
                item.Code,
                item.SourceReference,
                item.TargetReference
            })
        }, SerializerOptions);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digestMaterial)))
            .ToLowerInvariant();
        return new ProjectBootstrapPreviewBuild(document, digest, membershipSnapshotToken);
    }

    private static IReadOnlyCollection<ProjectBootstrapItemResponse> BuildProjectItems(
        Project source,
        Project target,
        IReadOnlyCollection<ProjectLocation> sourceLocations,
        IReadOnlyCollection<ProjectLocation> targetLocations,
        IReadOnlyCollection<ProjectBootstrapCategory> categories)
    {
        var items = new List<ProjectBootstrapItemResponse>();
        foreach (var category in categories.Where(item => item != ProjectBootstrapCategory.Members))
        {
            switch (category)
            {
                case ProjectBootstrapCategory.BaseSettings:
                    AddBaseSettings(items, source);
                    break;
                case ProjectBootstrapCategory.Calendar:
                    items.Add(source.CalendarMode == ProjectCalendarMode.WorkingWeek && source.WorkingDaysMask is > 0
                        ? Item(category, ProjectMembershipBootstrapDisposition.Added,
                            "bootstrap.calendar.add", "تقویم کاری",
                            "الگوی روزهای کاری منتقل می‌شود؛ منطقه زمانی صریح مقصد حفظ خواهد شد.",
                            source.CalendarMode.ToString(), target.TimeZone)
                        : Item(category, ProjectMembershipBootstrapDisposition.Skipped,
                            "bootstrap.calendar.not_configured", "تقویم کاری",
                            "تقویم مبدأ پیکربندی نشده است."));
                    break;
                case ProjectBootstrapCategory.Locations:
                    AddLocations(items, sourceLocations, targetLocations);
                    break;
                case ProjectBootstrapCategory.RoleTemplates:
                    items.Add(NoOverride(category, "قالب نقش‌ها",
                        "کاتالوگ نقش‌ها مشترک و نسخه‌دار است؛ Override پروژه‌ای برای کپی وجود ندارد."));
                    break;
                case ProjectBootstrapCategory.WorkflowTemplates:
                    items.Add(source.DailyReportWorkflow == DailyReportWorkflow.NotConfigured
                        ? NoOverride(category, "قالب گردش کار", "گردش گزارش روزانه در مبدأ پیکربندی نشده است.")
                        : Item(category, ProjectMembershipBootstrapDisposition.Added,
                            "bootstrap.workflow.add", "قالب گردش گزارش روزانه",
                            "گردش گزارش و زمان قطع روزانه به‌عنوان Default جدید مقصد منتقل می‌شود.",
                            source.DailyReportWorkflow.ToString(), source.DailyCutoffLocalTime?.ToString("HH:mm")));
                    break;
                case ProjectBootstrapCategory.FormTemplates:
                    items.Add(NoOverride(category, "قالب فرم‌ها",
                        "فرم‌های فعلی مشترک‌اند و Override پروژه‌ای Cloneable وجود ندارد."));
                    break;
                case ProjectBootstrapCategory.ReportTemplates:
                    items.Add(source.ReportingFrequency == ReportingFrequency.NotConfigured
                        ? NoOverride(category, "قالب گزارش", "Default گزارش‌گیری در مبدأ پیکربندی نشده است.")
                        : Item(category, ProjectMembershipBootstrapDisposition.Added,
                            "bootstrap.report-default.add", "Default گزارش‌گیری",
                            "فقط بسامد پیش‌فرض گزارش منتقل می‌شود؛ Run و Outputها مستثنا هستند.",
                            source.ReportingFrequency.ToString(), null));
                    break;
                case ProjectBootstrapCategory.Lookups:
                    items.Add(NoOverride(category, "Lookupهای قابل انتقال",
                        "Lookup پروژه‌ای که صریحاً Cloneable اعلام شده باشد در مبدأ وجود ندارد."));
                    break;
                case ProjectBootstrapCategory.NotificationDefaults:
                    items.Add(NoOverride(category, "پیش‌فرض اعلان‌ها",
                        "Default پروژه‌ای ثبت نشده است؛ هیچ سابقه یا اعلان موجود کپی نمی‌شود."));
                    break;
                case ProjectBootstrapCategory.GroupDefaults:
                    items.Add(NoOverride(category, "پیش‌فرض گروه‌ها",
                        "Default پروژه‌ای ثبت نشده است؛ گروه، پیام و تاریخچه کپی نمی‌شوند."));
                    break;
                default:
                    items.Add(Item(category, ProjectMembershipBootstrapDisposition.Blocked,
                        "bootstrap.category.unsupported", "دسته ناشناخته",
                        "Contributor سازگار برای این دسته ثبت نشده است."));
                    break;
            }
        }
        return items;
    }

    private static void AddBaseSettings(List<ProjectBootstrapItemResponse> items, Project source)
    {
        var category = ProjectBootstrapCategory.BaseSettings;
        items.Add(source.ContractModel == ContractModel.NotConfigured
            ? Item(category, ProjectMembershipBootstrapDisposition.Skipped,
                "bootstrap.contract-model.not_configured", "مدل قراردادی پایه",
                "مدل قراردادی پایه در مبدأ پیکربندی نشده است.")
            : Item(category, ProjectMembershipBootstrapDisposition.Added,
                "bootstrap.contract-model.add", "مدل قراردادی پایه",
                "فقط مدل تنظیماتی منتقل می‌شود؛ قرارداد و تاریخچهٔ آن مستثنا هستند.",
                source.ContractModel.ToString(), null));
        items.Add(Item(category, ProjectMembershipBootstrapDisposition.Added,
            "bootstrap.planning-mode.add", "حالت برنامه‌ریزی",
            "حالت برنامه‌ریزی منتقل می‌شود؛ Baseline، WBS و پیشرفت واقعی منتقل نمی‌شوند.",
            source.PlanningMode.ToString(), null));

        AddCapability(items, "بودجه", "budget", source.BudgetMode);
        AddCapability(items, "کیفیت", "quality", source.QualityMode);
        AddCapability(items, "ایمنی", "hse", source.HseMode);
        AddCapability(items, "مالی", "finance", source.FinanceMode);
        AddCapability(items, "تدارکات", "procurement", source.ProcurementMode);
    }

    private static void AddCapability(
        List<ProjectBootstrapItemResponse> items,
        string title,
        string code,
        CapabilityMode sourceMode)
    {
        var targetMode = NormalizeCapability(sourceMode);
        items.Add(Item(
            ProjectBootstrapCategory.BaseSettings,
            ProjectMembershipBootstrapDisposition.Added,
            $"bootstrap.capability.{code}.add",
            $"وضعیت ماژول {title}",
            sourceMode == targetMode
                ? "وضعیت سازگار ماژول به مقصد منتقل می‌شود."
                : "وضعیت عملیاتی مبدأ به SetupRequired/NotEnabled امن مقصد تبدیل می‌شود.",
            sourceMode.ToString(),
            targetMode.ToString()));
    }

    private static void AddLocations(
        List<ProjectBootstrapItemResponse> items,
        IReadOnlyCollection<ProjectLocation> locations,
        IReadOnlyCollection<ProjectLocation> targetLocations)
    {
        var sourceIds = locations.Select(item => item.Id).ToHashSet();
        var activeSourceIds = locations
            .Where(item => item.Status == ProjectLocationStatus.Active)
            .Select(item => item.Id)
            .ToHashSet();
        var targetByCode = targetLocations.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var location in locations.OrderBy(item => item.Code, StringComparer.Ordinal))
        {
            if (location.ParentLocationId is null)
            {
                items.Add(Item(
                    ProjectBootstrapCategory.Locations,
                    ProjectMembershipBootstrapDisposition.Skipped,
                    "bootstrap.location.root.reuse",
                    location.Name,
                    "ریشهٔ مستقل مقصد از قبل ایجاد شده و شناسهٔ مبدأ کپی نمی‌شود.",
                    location.Code,
                    "ROOT"));
            }
            else if (location.Status != ProjectLocationStatus.Active)
            {
                items.Add(Item(
                    ProjectBootstrapCategory.Locations,
                    ProjectMembershipBootstrapDisposition.Skipped,
                    "bootstrap.location.retired",
                    location.Name,
                    "مکان بازنشسته به مقصد منتقل نمی‌شود.",
                    location.Code,
                    null));
            }
            else if (!location.ParentLocationId.HasValue || !sourceIds.Contains(location.ParentLocationId.Value) ||
                !activeSourceIds.Contains(location.ParentLocationId.Value))
            {
                items.Add(Item(
                    ProjectBootstrapCategory.Locations,
                    ProjectMembershipBootstrapDisposition.Blocked,
                    "bootstrap.location.parent_unavailable",
                    location.Name,
                    "والد فعال این مکان در Snapshot مبدأ موجود نیست و ساخت سلسله‌مراتب امن نیست.",
                    location.Code,
                    null));
            }
            else if (targetByCode.ContainsKey(location.Code))
            {
                items.Add(Item(
                    ProjectBootstrapCategory.Locations,
                    ProjectMembershipBootstrapDisposition.Conflict,
                    "bootstrap.location.target_conflict",
                    location.Name,
                    "کد مکان در مقصد از قبل وجود دارد و بازنویسی نمی‌شود.",
                    location.Code,
                    targetByCode[location.Code].Id.ToString()));
            }
            else
            {
                items.Add(Item(
                    ProjectBootstrapCategory.Locations,
                    ProjectMembershipBootstrapDisposition.Added,
                    "bootstrap.location.add",
                    location.Name,
                    "مکان فعال با شناسهٔ جدید و همان جایگاه سلسله‌مراتبی ساخته می‌شود.",
                    location.Code,
                    null));
            }
        }
    }

    internal static CapabilityMode NormalizeCapability(CapabilityMode mode) => mode switch
    {
        CapabilityMode.Active => CapabilityMode.SetupRequired,
        CapabilityMode.Suspended => CapabilityMode.NotEnabled,
        _ => mode
    };

    private static ProjectBootstrapItemResponse NoOverride(
        ProjectBootstrapCategory category,
        string title,
        string detail) => Item(
        category,
        ProjectMembershipBootstrapDisposition.Skipped,
        "bootstrap.project_override.absent",
        title,
        detail);

    private static ProjectBootstrapItemResponse Item(
        ProjectBootstrapCategory category,
        ProjectMembershipBootstrapDisposition disposition,
        string code,
        string title,
        string detail,
        string? sourceReference = null,
        string? targetReference = null) => new(
        ProjectBootstrapContributorCatalog.Required(category).ContributorId,
        category,
        disposition,
        code,
        title,
        detail,
        sourceReference,
        targetReference);

    internal static ProjectBootstrapCategory[] NormalizeCategories(
        IReadOnlyCollection<ProjectBootstrapCategory> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        if (categories.Count == 0 || categories.Any(category => !Enum.IsDefined(category)))
        {
            throw new ArgumentException("At least one supported bootstrap category is required.", nameof(categories));
        }
        return categories.Distinct().OrderBy(item => item).ToArray();
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
