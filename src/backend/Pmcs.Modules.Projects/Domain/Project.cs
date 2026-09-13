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
        CapabilityMode procurementMode,
        ProjectType projectType,
        ProjectExecutionPhase executionPhase,
        string countryCode,
        string region,
        DateOnly? startDate,
        DateOnly? plannedFinishDate,
        string shortDescription,
        ProjectUnitSystem unitSystem,
        TimeOnly? dailyCutoffLocalTime,
        ReportingFrequency reportingFrequency,
        DailyReportWorkflow dailyReportWorkflow,
        bool offlinePolicyAccepted)
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
        ProjectType = projectType;
        ExecutionPhase = executionPhase;
        CountryCode = countryCode;
        Region = region;
        StartDate = startDate;
        PlannedFinishDate = plannedFinishDate;
        ShortDescription = shortDescription;
        UnitSystem = unitSystem;
        DailyCutoffLocalTime = dailyCutoffLocalTime;
        ReportingFrequency = reportingFrequency;
        DailyReportWorkflow = dailyReportWorkflow;
        OfflinePolicyAccepted = offlinePolicyAccepted;
        ConfigurationVersion = 1;
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

    public ProjectType ProjectType { get; private set; }

    public ProjectExecutionPhase ExecutionPhase { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public string Region { get; private set; } = string.Empty;

    public DateOnly? StartDate { get; private set; }

    public DateOnly? PlannedFinishDate { get; private set; }

    public string ShortDescription { get; private set; } = string.Empty;

    public ProjectUnitSystem UnitSystem { get; private set; }

    public TimeOnly? DailyCutoffLocalTime { get; private set; }

    public ReportingFrequency ReportingFrequency { get; private set; }

    public DailyReportWorkflow DailyReportWorkflow { get; private set; }

    public bool OfflinePolicyAccepted { get; private set; }

    public long ConfigurationVersion { get; private set; } = 1;

    public long? ActivatedConfigurationVersion { get; private set; }

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
        CapabilityMode procurementMode = CapabilityMode.Active,
        ProjectType projectType = ProjectType.NotConfigured,
        ProjectExecutionPhase executionPhase = ProjectExecutionPhase.NotConfigured,
        string? countryCode = null,
        string? region = null,
        DateOnly? startDate = null,
        DateOnly? plannedFinishDate = null,
        string? shortDescription = null,
        ProjectUnitSystem unitSystem = ProjectUnitSystem.NotConfigured,
        TimeOnly? dailyCutoffLocalTime = null,
        ReportingFrequency reportingFrequency = ReportingFrequency.NotConfigured,
        DailyReportWorkflow dailyReportWorkflow = DailyReportWorkflow.NotConfigured,
        bool offlinePolicyAccepted = false)
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
            !Enum.IsDefined(financeMode) || !Enum.IsDefined(procurementMode) ||
            !Enum.IsDefined(projectType) || !Enum.IsDefined(executionPhase) ||
            !Enum.IsDefined(unitSystem) || !Enum.IsDefined(reportingFrequency) ||
            !Enum.IsDefined(dailyReportWorkflow))
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

        var normalizedCountry = NormalizeCountry(countryCode);
        var normalizedRegion = NormalizeOptional(region, 200, "project.region.invalid");
        var normalizedDescription = NormalizeOptional(shortDescription, 1000, "project.description.invalid");
        ValidateDates(startDate, plannedFinishDate);

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
            procurementMode,
            projectType,
            executionPhase,
            normalizedCountry,
            normalizedRegion,
            startDate,
            plannedFinishDate,
            normalizedDescription,
            unitSystem,
            dailyCutoffLocalTime,
            reportingFrequency,
            dailyReportWorkflow,
            offlinePolicyAccepted);
    }

    public void ConfigureSetup(
        long baseRevision,
        ContractModel contractModel,
        PlanningMode planningMode,
        CapabilityMode budgetMode,
        CapabilityMode qualityMode,
        CapabilityMode hseMode,
        CapabilityMode financeMode,
        CapabilityMode procurementMode,
        ProjectCalendarMode calendarMode,
        int? workingDaysMask,
        ProjectType projectType,
        ProjectExecutionPhase executionPhase,
        string? countryCode,
        string? region,
        DateOnly? startDate,
        DateOnly? plannedFinishDate,
        string? shortDescription,
        string timeZone,
        string baseCurrencyCode,
        ProjectUnitSystem unitSystem,
        TimeOnly? dailyCutoffLocalTime,
        ReportingFrequency reportingFrequency,
        DailyReportWorkflow dailyReportWorkflow,
        bool offlinePolicyAccepted,
        DateTimeOffset changedAt,
        bool allowSensitiveChange,
        string? reason)
    {
        EnsureRevision(baseRevision);
        if (Status != ProjectStatus.Draft && (!allowSensitiveChange || string.IsNullOrWhiteSpace(reason)))
        {
            throw new DomainRuleException(
                "project.setup.sensitive_change_requires_reason",
                "Post-activation setup changes require elevated permission and a reason.");
        }

        if (!Enum.IsDefined(contractModel) || !Enum.IsDefined(planningMode) ||
            !Enum.IsDefined(budgetMode) || !Enum.IsDefined(qualityMode) || !Enum.IsDefined(hseMode) ||
            !Enum.IsDefined(financeMode) || !Enum.IsDefined(procurementMode) || !Enum.IsDefined(calendarMode) ||
            !Enum.IsDefined(projectType) || !Enum.IsDefined(executionPhase) ||
            !Enum.IsDefined(unitSystem) || !Enum.IsDefined(reportingFrequency) || !Enum.IsDefined(dailyReportWorkflow))
        {
            throw new DomainRuleException("project.configuration.invalid", "Project setup configuration is invalid.");
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

        ValidateDates(startDate, plannedFinishDate);
        if (calendarMode == ProjectCalendarMode.NotConfigured && workingDaysMask.HasValue)
        {
            throw new DomainRuleException("project.calendar.days.unexpected", "Working days must be empty when the calendar is not configured.");
        }
        if (calendarMode == ProjectCalendarMode.WorkingWeek && (workingDaysMask is null or <= 0 or > 127))
        {
            throw new DomainRuleException("project.calendar.days.invalid", "At least one valid working weekday is required.");
        }

        ContractModel = contractModel;
        PlanningMode = planningMode;
        BudgetMode = budgetMode;
        QualityMode = qualityMode;
        HseMode = hseMode;
        FinanceMode = financeMode;
        ProcurementMode = procurementMode;
        CalendarMode = calendarMode;
        WorkingDaysMask = workingDaysMask;
        ProjectType = projectType;
        ExecutionPhase = executionPhase;
        CountryCode = NormalizeCountry(countryCode);
        Region = NormalizeOptional(region, 200, "project.region.invalid");
        StartDate = startDate;
        PlannedFinishDate = plannedFinishDate;
        ShortDescription = NormalizeOptional(shortDescription, 1000, "project.description.invalid");
        TimeZone = normalizedTimeZone;
        BaseCurrencyCode = normalizedCurrency;
        UnitSystem = unitSystem;
        DailyCutoffLocalTime = dailyCutoffLocalTime;
        ReportingFrequency = reportingFrequency;
        DailyReportWorkflow = dailyReportWorkflow;
        OfflinePolicyAccepted = offlinePolicyAccepted;
        ConfigurationChangedAt = changedAt;
        ConfigurationVersion++;
        AdvanceRevision();
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
        ActivatedConfigurationVersion = ConfigurationVersion;
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
        ConfigurationVersion++;
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
        ConfigurationVersion++;
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

    private static string NormalizeCountry(string? countryCode)
    {
        var normalized = countryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 0 &&
            (normalized.Length != 2 || normalized.Any(character => !char.IsAsciiLetterUpper(character))))
        {
            throw new DomainRuleException("project.country.invalid", "Country must be an ISO-style two-letter code.");
        }

        return normalized;
    }

    private static string NormalizeOptional(string? value, int maximumLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    private static void ValidateDates(DateOnly? startDate, DateOnly? plannedFinishDate)
    {
        if (startDate.HasValue && plannedFinishDate.HasValue && plannedFinishDate.Value < startDate.Value)
        {
            throw new DomainRuleException("project.dates.invalid", "Planned finish date cannot precede start date.");
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

public enum ProjectType
{
    NotConfigured = 0,
    Building = 1,
    Industrial = 2,
    Infrastructure = 3,
    Renovation = 4,
    Landscaping = 5,
    Mixed = 6
}

public enum ProjectExecutionPhase
{
    NotConfigured = 0,
    PreConstruction = 1,
    ActiveExecution = 2,
    OnHold = 3,
    Closing = 4
}

public enum ProjectUnitSystem
{
    NotConfigured = 0,
    Metric = 1
}

public enum ReportingFrequency
{
    NotConfigured = 0,
    Daily = 1,
    WorkingDays = 2,
    Weekly = 3
}

public enum DailyReportWorkflow
{
    NotConfigured = 0,
    OneStepApproval = 1
}
