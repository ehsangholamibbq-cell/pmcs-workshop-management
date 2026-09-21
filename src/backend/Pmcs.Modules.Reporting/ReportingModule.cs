using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Reporting.Endpoints;
using Pmcs.Modules.Reporting.Contracts;
using Pmcs.Modules.Reporting.Migrations;
using Pmcs.Modules.Reporting.Persistence;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Modules.Reporting;

public sealed class ReportingModule : IModule
{
    public string Name => "Reporting";

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleManifestSchemas.VersionOne,
        "reporting.center",
        "Certified Reporting Center",
        "1.1.0",
        [
            "reporting.catalog",
            "reporting.certified-runs",
            "reporting.semantic-snapshots",
            "reporting.generated-outputs"
        ],
        ["documents.shared", "legacy.fieldoperations", "platform.foundation", "projects.core"],
        "reporting",
        "1.1.0",
        false,
        [
            new PermissionManifest(
                "reporting.catalog.read",
                PermissionScope.Project,
                ManifestRiskClass.Low,
                "Read report definitions and authorized run metadata in a project scope."),
            new PermissionManifest(
                "reporting.run.create",
                PermissionScope.Project,
                ManifestRiskClass.Medium,
                "Queue a certified report using an allowlisted definition and source permissions."),
            new PermissionManifest(
                "reporting.output.download",
                PermissionScope.Project,
                ManifestRiskClass.Medium,
                "Download a certified report after current source permissions are re-evaluated."),
            new PermissionManifest(
                "reporting.template.publish",
                PermissionScope.Tenant,
                ManifestRiskClass.High,
                "Publish a reviewed immutable certified report template version.")
        ],
        [
            new NavigationManifest(
                "reporting.center",
                "/reports",
                "مرکز گزارش‌ها",
                "reporting.catalog.read",
                "reporting.phase1",
                1_500)
        ],
        [
            new ToolManifest(
                "reporting.catalog.list",
                "Lists report definitions visible under the current project and source permissions.",
                """{"type":"object","properties":{"projectId":{"type":"string","format":"uuid"}},"required":["projectId"],"additionalProperties":false}""",
                """{"type":"object","required":["definitions"],"additionalProperties":false}""",
                "reporting.catalog.read",
                ManifestRiskClass.Low,
                ToolAccessMode.ReadOnly),
            new ToolManifest(
                "reporting.runs.get",
                "Returns permission-filtered metadata for one certified report run.",
                """{"type":"object","properties":{"projectId":{"type":"string","format":"uuid"},"runId":{"type":"string","format":"uuid"}},"required":["projectId","runId"],"additionalProperties":false}""",
                """{"type":"object","required":["id","status"],"additionalProperties":true}""",
                "reporting.catalog.read",
                ManifestRiskClass.Low,
                ToolAccessMode.ReadOnly),
            new ToolManifest(
                "reporting.outputs.describe",
                "Returns authorized immutable output metadata without file bytes.",
                """{"type":"object","properties":{"projectId":{"type":"string","format":"uuid"},"outputId":{"type":"string","format":"uuid"}},"required":["projectId","outputId"],"additionalProperties":false}""",
                """{"type":"object","required":["id","format","sha256"],"additionalProperties":true}""",
                "reporting.output.download",
                ManifestRiskClass.Medium,
                ToolAccessMode.ReadOnly)
        ],
        [
            new IntegrationEventManifest(
                "reporting.report.completed",
                1,
                IntegrationEventClassification.Confidential)
        ]);

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddSingleton(ReportingRuntimeOptions.Create(configuration));
        services.AddSingleton(ReportingExecutionOptions.Create(configuration));
        services.AddSingleton(ReportingOrphanRemediationOptions.Create(configuration));
        services.AddSingleton(ReportingWorkerQualificationOptions.Create(configuration));
        services.AddSingleton(ReportingRendererOptions.Create(configuration));
        services.AddSingleton<ReportingWorkerTelemetry>();
        services.AddDbContext<ReportingDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IReportingReadService, ReportingReadService>();
        services.AddSingleton<IReportRenderer, DailyReportPdfRenderer>();
        services.AddSingleton<IReportRenderer, DailyReportXlsxRenderer>();
        services.AddSingleton<ReportRendererRegistry>();
        services.AddSingleton<IProjectPeriodicReportRenderer, ProjectPeriodicReportPdfRenderer>();
        services.AddSingleton<IProjectPeriodicReportRenderer, ProjectPeriodicReportXlsxRenderer>();
        services.AddSingleton<ProjectPeriodicReportRendererRegistry>();
        services.AddSingleton<IExecutiveProjectStateReportRenderer, ExecutiveProjectStateReportPdfRenderer>();
        services.AddSingleton<IExecutiveProjectStateReportRenderer, ExecutiveProjectStateReportXlsxRenderer>();
        services.AddSingleton<ExecutiveProjectStateReportRendererRegistry>();
        services.AddSingleton<IProjectProgressReportRenderer, ProjectProgressReportPdfRenderer>();
        services.AddSingleton<IProjectProgressReportRenderer, ProjectProgressReportXlsxRenderer>();
        services.AddSingleton<ProjectProgressReportRendererRegistry>();
        services.AddSingleton<IProjectFinancialPositionReportRenderer, ProjectFinancialPositionReportPdfRenderer>();
        services.AddSingleton<IProjectFinancialPositionReportRenderer, ProjectFinancialPositionReportXlsxRenderer>();
        services.AddSingleton<ProjectFinancialPositionReportRendererRegistry>();
        services.AddSingleton<IDatabaseMigration, ReportingInitialMigration>();
        services.AddSingleton<IDatabaseMigration, ReportingVerificationCodeIndexMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectPeriodicReportCatalogMigration>();
        services.AddSingleton<IDatabaseMigration, ExecutiveProjectStateReportCatalogMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectProgressReportCatalogMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectFinancialPositionReportCatalogMigration>();
        services.AddHostedService<ReportGenerationWorker>();
        services.AddHostedService<ReportOutputOrphanRemediationWorker>();
        services.AddHealthChecks().AddCheck<ReportingWorkerHealthCheck>(
            "reporting-worker",
            tags: ["ready"]);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapReportingEndpoints();
}
