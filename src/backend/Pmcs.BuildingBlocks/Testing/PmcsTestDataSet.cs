namespace Pmcs.BuildingBlocks.Testing;

public sealed record PmcsTestActor(
    string Code,
    Guid UserId,
    Guid MembershipId,
    string DisplayName,
    string Email,
    string TenantRole,
    string ProjectRole,
    bool IsQaSuperAdministrator);

public static class PmcsTestDataSet
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid RootLocationId = Guid.Parse("33333333-3333-4333-8333-333333333334");

    public static readonly IReadOnlyList<PmcsTestActor> Actors =
    [
        new(
            "qa-super-admin",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "مدیر ارشد آزمون PMCS",
            "qa.superadmin@pmcs.invalid",
            "TenantAdministrator",
            "ProjectManager",
            true),
        new(
            "site-supervisor",
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            "سرپرست کارگاه آزمون",
            "qa.site-supervisor@pmcs.invalid",
            "Member",
            "SiteSupervisor",
            false),
        CreateActor(1, "observer", "مشاهده‌گر آزمون", "Observer"),
        CreateActor(2, "technical-office", "کارشناس دفتر فنی آزمون", "TechnicalOffice"),
        CreateActor(3, "finance-operator", "کاربر مالی آزمون", "FinanceOperator"),
        CreateActor(4, "finance-manager", "مدیر مالی آزمون", "FinanceManager"),
        CreateActor(5, "contract-administrator", "مسئول قرارداد آزمون", "ContractAdministrator"),
        CreateActor(6, "procurement-operator", "کاربر تدارکات آزمون", "ProcurementOperator"),
        CreateActor(7, "procurement-manager", "مدیر تدارکات آزمون", "ProcurementManager"),
        CreateActor(8, "quality-controller", "کارشناس کنترل کیفیت آزمون", "QualityController"),
        CreateActor(9, "hse-officer", "مسئول HSE آزمون", "HseOfficer"),
        CreateActor(10, "project-controller", "کنترلر پروژه آزمون", "ProjectController")
    ];

    public static PmcsTestActor QaSuperAdministrator => Actors[0];

    private static PmcsTestActor CreateActor(
        int sequence,
        string code,
        string displayName,
        string projectRole) =>
        new(
            code,
            Guid.Parse($"50000000-0000-4000-8000-{sequence:D12}"),
            Guid.Parse($"60000000-0000-4000-8000-{sequence:D12}"),
            displayName,
            $"qa.{code}@pmcs.invalid",
            "Member",
            projectRole,
            false);
}
