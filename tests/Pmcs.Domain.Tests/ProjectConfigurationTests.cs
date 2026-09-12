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
}
