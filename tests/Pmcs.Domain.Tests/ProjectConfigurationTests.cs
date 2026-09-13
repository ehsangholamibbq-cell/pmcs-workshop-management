using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ProjectConfigurationTests
{
    [Fact]
    public void ProjectCanStartWithoutWbsOrBudgetBaselineAndWithoutHse()
    {
        var project = Project.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BLDG-01",
            "Building 01",
            ContractModel.NotConfigured,
            PlanningMode.None,
            CapabilityMode.SetupRequired,
            CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled,
            "Asia/Tehran",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(PlanningMode.None, project.PlanningMode);
        Assert.Equal(CapabilityMode.SetupRequired, project.BudgetMode);
        Assert.Equal(CapabilityMode.NotEnabled, project.HseMode);
        Assert.Equal(CapabilityMode.Active, project.FinanceMode);
        Assert.Equal(CapabilityMode.Active, project.ProcurementMode);
        Assert.Equal(ProjectCalendarMode.NotConfigured, project.CalendarMode);
        Assert.Equal(ProjectStatus.Draft, project.Status);
    }

    [Fact]
    public void OptionalCapabilitiesAreIndependent()
    {
        var project = Project.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BLDG-02",
            "Building 02",
            ContractModel.Hybrid,
            PlanningMode.Milestones,
            CapabilityMode.Active,
            CapabilityMode.NotEnabled,
            CapabilityMode.Active,
            "Asia/Tehran",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(CapabilityMode.Active, project.BudgetMode);
        Assert.Equal(CapabilityMode.NotEnabled, project.QualityMode);
        Assert.Equal(CapabilityMode.Active, project.HseMode);
    }

    [Fact]
    public void CalendarCanRemainAbsentOrUseAnExplicitWorkingWeek()
    {
        var project = Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "CAL-01", "Calendar project",
            ContractModel.NotConfigured, PlanningMode.None, CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled, CapabilityMode.NotEnabled, "Asia/Tehran", Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));
        var workingDays = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

        project.ConfigureCalendar(1, ProjectCalendarMode.WorkingWeek, workingDays, DateTimeOffset.UtcNow);

        Assert.True(project.IsWorkingDay(DayOfWeek.Saturday));
        Assert.False(project.IsWorkingDay(DayOfWeek.Friday));
        Assert.Equal(2, project.Revision);
    }

    [Fact]
    public void ConfiguredCalendarRequiresAtLeastOneWorkingDay()
    {
        var project = Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "CAL-02", "Calendar project",
            ContractModel.NotConfigured, PlanningMode.None, CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled, CapabilityMode.NotEnabled, "Asia/Tehran", Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));

        Assert.Throws<DomainRuleException>(() =>
            project.ConfigureCalendar(1, ProjectCalendarMode.WorkingWeek, 0, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PlanningModeCanChangeWithoutRewritingExistingProjectFacts()
    {
        var project = Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "PRJ-01", "Project",
            ContractModel.NotConfigured, PlanningMode.None, CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled, CapabilityMode.NotEnabled, "Asia/Tehran",
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        project.ConfigurePlanningMode(1, PlanningMode.Milestones, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(PlanningMode.Milestones, project.PlanningMode);
        Assert.Equal(2, project.Revision);
        Assert.NotNull(project.ConfigurationChangedAt);
    }

    [Fact]
    public void ProjectLocationPreservesHierarchyAndCannotRetireTheRoot()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var root = ProjectLocation.Create(
            Guid.NewGuid(), tenantId, projectId, "ROOT", "کل پروژه", null, actorId, DateTimeOffset.UtcNow);
        var floor = ProjectLocation.Create(
            Guid.NewGuid(), tenantId, projectId, "FLOOR-03", "طبقه سوم", root.Id, actorId, DateTimeOffset.UtcNow);

        floor.Retire(floor.Revision, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(root.Id, floor.ParentLocationId);
        Assert.Equal(ProjectLocationStatus.Retired, floor.Status);
        Assert.Throws<DomainRuleException>(() => root.Retire(root.Revision, DateTimeOffset.UtcNow));
        Assert.Throws<DomainRuleException>(() => ProjectLocation.Create(
            Guid.NewGuid(), tenantId, projectId, "ORPHAN", "محل بدون والد", null, actorId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ProjectActivationIsRevisionControlled()
    {
        var project = Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "ACT-01", "Project",
            ContractModel.GeneralContracting, PlanningMode.SimpleWorkList, CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled, CapabilityMode.NotEnabled, "Asia/Tehran",
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleException>(() => project.Activate(0, Guid.NewGuid(), DateTimeOffset.UtcNow));

        var actorId = Guid.NewGuid();
        var activatedAt = DateTimeOffset.UtcNow;
        project.Activate(project.Revision, actorId, activatedAt);

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(actorId, project.ActivatedBy);
        Assert.Equal(activatedAt, project.ActivatedAt);
        Assert.Equal(2, project.Revision);
        Assert.Throws<DomainRuleException>(() => project.Activate(project.Revision, actorId, activatedAt));
    }

    [Fact]
    public void ProjectRejectsUnknownTimeZone()
    {
        var exception = Assert.Throws<DomainRuleException>(() => Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "TZ-01", "Project",
            ContractModel.GeneralContracting, PlanningMode.SimpleWorkList, CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled, CapabilityMode.NotEnabled, "Iran/Unknown-City",
            Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.Equal("project.time_zone.invalid", exception.Code);
    }

    [Fact]
    public void ProjectCannotActivateWithoutABaseContractModel()
    {
        var project = Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "NO-CONTRACT", "Project",
            ContractModel.NotConfigured, PlanningMode.None, CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled, CapabilityMode.NotEnabled, "Asia/Tehran",
            Guid.NewGuid(), DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainRuleException>(() =>
            project.Activate(project.Revision, Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.Equal("project.activate.contract_model.required", exception.Code);
        Assert.Equal(ProjectStatus.Draft, project.Status);
    }

    [Fact]
    public void CompleteSetupIsVersionedAndPreservesDateOrder()
    {
        var project = Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SETUP-01", "Project setup",
            ContractModel.GeneralContracting, PlanningMode.None, CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled, CapabilityMode.NotEnabled, "Asia/Tehran",
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        var workingDays = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);

        project.ConfigureSetup(
            project.Revision,
            ContractModel.ConstructionManagement,
            PlanningMode.SimpleWorkList,
            CapabilityMode.SetupRequired,
            CapabilityMode.SetupRequired,
            CapabilityMode.NotEnabled,
            CapabilityMode.SetupRequired,
            CapabilityMode.SetupRequired,
            ProjectCalendarMode.WorkingWeek,
            workingDays,
            ProjectType.Building,
            ProjectExecutionPhase.PreConstruction,
            "ir",
            "Tehran",
            new DateOnly(2026, 9, 1),
            new DateOnly(2027, 9, 1),
            "Controlled setup",
            "Asia/Tehran",
            "irr",
            ProjectUnitSystem.Metric,
            new TimeOnly(18, 0),
            ReportingFrequency.WorkingDays,
            DailyReportWorkflow.OneStepApproval,
            true,
            DateTimeOffset.UtcNow,
            false,
            null);

        Assert.Equal(2, project.ConfigurationVersion);
        Assert.Equal(2, project.Revision);
        Assert.Equal("IR", project.CountryCode);
        Assert.Equal("IRR", project.BaseCurrencyCode);
        Assert.True(project.OfflinePolicyAccepted);
        Assert.True(project.IsWorkingDay(DayOfWeek.Saturday));
    }

    [Fact]
    public void PostActivationSetupChangeRequiresElevatedPermissionAndReason()
    {
        var project = Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SETUP-02", "Project setup",
            ContractModel.GeneralContracting, PlanningMode.SimpleWorkList, CapabilityMode.SetupRequired,
            CapabilityMode.SetupRequired, CapabilityMode.NotEnabled, "Asia/Tehran",
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        project.Activate(project.Revision, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainRuleException>(() => project.ConfigureSetup(
            project.Revision,
            project.ContractModel,
            project.PlanningMode,
            project.BudgetMode,
            project.QualityMode,
            project.HseMode,
            project.FinanceMode,
            project.ProcurementMode,
            project.CalendarMode,
            project.WorkingDaysMask,
            ProjectType.Building,
            ProjectExecutionPhase.ActiveExecution,
            "IR",
            "Tehran",
            new DateOnly(2026, 9, 1),
            new DateOnly(2027, 9, 1),
            "Controlled setup",
            "Asia/Tehran",
            "IRR",
            ProjectUnitSystem.Metric,
            new TimeOnly(18, 0),
            ReportingFrequency.WorkingDays,
            DailyReportWorkflow.OneStepApproval,
            true,
            DateTimeOffset.UtcNow,
            true,
            null));

        Assert.Equal("project.setup.sensitive_change_requires_reason", exception.Code);
    }

    [Fact]
    public void SetupRejectsPlannedFinishBeforeStart()
    {
        var exception = Assert.Throws<DomainRuleException>(() => Project.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SETUP-03", "Project setup",
            ContractModel.GeneralContracting, PlanningMode.SimpleWorkList, CapabilityMode.SetupRequired,
            CapabilityMode.SetupRequired, CapabilityMode.NotEnabled, "Asia/Tehran",
            Guid.NewGuid(), DateTimeOffset.UtcNow,
            projectType: ProjectType.Building,
            startDate: new DateOnly(2027, 1, 1),
            plannedFinishDate: new DateOnly(2026, 1, 1)));

        Assert.Equal("project.dates.invalid", exception.Code);
    }
}
