namespace Pmcs.Modules.IdentityAccess.Domain;

public static class ProjectRoleCatalog
{
    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ProjectManager",
        "SiteSupervisor",
        "Observer",
        "TechnicalOffice",
        "FinanceOperator",
        "FinanceManager",
        "ContractAdministrator",
        "ProcurementOperator",
        "ProcurementManager",
        "QualityController",
        "HseOfficer",
        "ProjectController"
    };

    public static bool IsSupported(string? roleCode) =>
        !string.IsNullOrWhiteSpace(roleCode) && Supported.Contains(roleCode.Trim());
}
