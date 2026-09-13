using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Projects.Domain;

public sealed class Project : AggregateRoot
{
    private Project()
    {
    }

    private Project(
        Guid id,
        Guid tenantId,
        string code,
        string name,
        ContractModel contractModel,
        PlanningMode planningMode,
        CapabilityMode budgetMode,
        CapabilityMode qualityMode,
        CapabilityMode hseMode,
        string timeZone,
        Guid createdBy,
        DateTimeOffset createdAt,
        CapabilityMode financeMode,
        string baseCurrencyCode,
        CapabilityMode procurementMode)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        Name = name;
        ContractModel = contractModel;
        PlanningMode = planningMode;
        BudgetMode = budgetMode;
        QualityMode = qualityMode;
        HseMode = hseMode;
        FinanceMode = financeMode;
        ProcurementMode = procurementMode;
        BaseCurrencyCode = baseCurrencyCode;
        CalendarMode = ProjectCalendarMode.NotConfigured;
        TimeZone = timeZone;
        Status = ProjectStatus.Draft;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public ContractModel ContractModel { get; private set; }

    public PlanningMode PlanningMode { get; private set; }

    public CapabilityMode BudgetMode { get; private set; }

    public CapabilityMode QualityMode { get; private set; }

    public CapabilityMode HseMode { get; private set; }

    public CapabilityMode FinanceMode { get; private set; }

    public CapabilityMode ProcurementMode { get; private set; }

    public string BaseCurrencyCode { get; private set; } = "IRR";

    public ProjectCalendarMode CalendarMode { get; private set; }

    public int? WorkingDaysMask { get; private set; }

    public DateTimeOffset? ConfigurationChangedAt { get; private set; }

    public string TimeZone { get; private set; } = "UTC";

    public ProjectStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? ActivatedBy { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public static Project Create(
        Guid id,
        Guid tenantId,
        string code,
        string name,
        ContractModel contractModel,
        PlanningMode planningMode,
        CapabilityMode budgetMode,
        CapabilityMode qualityMode,
        CapabilityMode hseMode,
        string timeZone,
        Guid createdBy,
        DateTimeOffset createdAt,
        CapabilityMode financeMode = CapabilityMode.Active,
        string baseCurrencyCode = "IRR",
        CapabilityMode procurementMode = CapabilityMode.Active)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("project.identity.required", "Project, tenant and creator ids are required.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainRuleException("project.code.invalid", "Project code is required.");
        }

        if (!Enum.IsDefined(contractModel) || !Enum.IsDefined(planningMode) ||
            !Enum.IsDefined(budgetMode) || !Enum.IsDefined(qualityMode) || !Enum.IsDefined(hseMode) ||
            !Enum.IsDefined(financeMode) || !Enum.IsDefined(procurementMode))
        {
            throw new DomainRuleException("project.configuration.invalid", "Project capability configuration is invalid.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length is < 2 or > 32 || normalizedCode.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new DomainRuleException("project.code.invalid", "Project code must be 2-32 letters, numbers, dashes or underscores.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new DomainRuleException("project.name.invalid", "Project name is required and must be at most 200 characters.");
        }

        var normalizedTimeZone = timeZone?.Trim() ?? string.Empty;
        if (normalizedTimeZone.Length is 0 or > 100 || !IsKnownTimeZone(normalizedTimeZone))
        {
            throw new DomainRuleException("project.time_zone.invalid", "A valid time-zone identifier is required.");
        }

        var normalizedCurrency = baseCurrencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedCurrency.Length != 3 || normalizedCurrency.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw new DomainRuleException("project.currency.invalid", "Base currency must be a three-letter ISO-style code.");
        }

        return new Project(
            id,
            tenantId,
            normalizedCode,
            name.Trim(),
            contractModel,
            planningMode,
            budgetMode,
            qualityMode,
            hseMode,
            normalizedTimeZone,
            createdBy,
            createdAt,
            financeMode,
            normalizedCurrency,
            procurementMode);
    }

    public void Activate(long baseRevision, Guid activatedBy, DateTimeOffset activatedAt)
    {
        EnsureRevision(baseRevision);
        if (Status != ProjectStatus.Draft)
        {
            throw new DomainRuleException("project.activate.invalid_state", "Only a draft project can be activated.");
        }

        if (activatedBy == Guid.Empty)
        {
            throw new DomainRuleException("project.activate.actor.required", "An activation actor is required.");
        }

        if (ContractModel == ContractModel.NotConfigured)
        {
            throw new DomainRuleException("project.activate.contract_model.required", "A base contract model is required before activation.");
        }

        Status = ProjectStatus.Active;
        ActivatedBy = activatedBy;
        ActivatedAt = activatedAt;
        ConfigurationChangedAt = activatedAt;
        AdvanceRevision();
    }

    private static bool IsKnownTimeZone(string timeZone)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public void ConfigureCalendar(
        long baseRevision,
        ProjectCalendarMode mode,
        int? workingDaysMask,
        DateTimeOffset changedAt)
    {
        EnsureRevision(baseRevision);
        if (!Enum.IsDefined(mode))
        {
            throw new DomainRuleException("project.calendar.mode.invalid", "Project calendar mode is invalid.");
        }

        if (mode == ProjectCalendarMode.NotConfigured)
        {
            if (workingDaysMask.HasValue)
            {
                throw new DomainRuleException("project.calendar.days.unexpected", "Working days must be empty when the calendar is not configured.");
            }

            WorkingDaysMask = null;
        }
        else
        {
            if (workingDaysMask is null or <= 0 or > 127)
            {
                throw new DomainRuleException("project.calendar.days.invalid", "At least one valid working weekday is required.");
            }

            WorkingDaysMask = workingDaysMask;
        }

        CalendarMode = mode;
        ConfigurationChangedAt = changedAt;
        AdvanceRevision();
    }

    public void ConfigurePlanningMode(
        long baseRevision,
        PlanningMode mode,
        DateTimeOffset changedAt)
    {
        EnsureRevision(baseRevision);
        if (!Enum.IsDefined(mode))
        {
            throw new DomainRuleException("project.planning_mode.invalid", "Project planning mode is invalid.");
        }

        PlanningMode = mode;
        ConfigurationChangedAt = changedAt;
        AdvanceRevision();
    }

    public bool IsWorkingDay(DayOfWeek dayOfWeek) =>
        CalendarMode == ProjectCalendarMode.WorkingWeek &&
        WorkingDaysMask.HasValue &&
        (WorkingDaysMask.Value & (1 << (int)dayOfWeek)) != 0;

    private void EnsureRevision(long baseRevision)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException("project.revision.conflict", "The project configuration changed after it was loaded.");
        }
    }
}

public enum ProjectStatus
{
    Draft = 1,
    Active = 2,
    OnHold = 3,
    Closing = 4,
    Closed = 5
}

public enum ContractModel
{
    NotConfigured = 0,
    GeneralContracting = 1,
    ConstructionManagement = 2,
    LaborOnly = 3,
    Hybrid = 4
}

public enum PlanningMode
{
    None = 0,
    SimpleWorkList = 1,
    Milestones = 2,
    WbsBaseline = 3,
    ExternalSchedule = 4
}

public enum CapabilityMode
{
    NotEnabled = 0,
    SetupRequired = 1,
    Active = 2,
    Suspended = 3
}

public enum ProjectCalendarMode
{
    NotConfigured = 0,
    WorkingWeek = 1
}
