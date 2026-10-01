using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ReportRendererRegistry(IEnumerable<IReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IReportRenderer> byFormat = renderers
        .ToDictionary(renderer => renderer.Format);

    public IReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                $"No certified renderer is registered for {format}.");
}

internal sealed class ProjectPeriodicReportRendererRegistry(
    IEnumerable<IProjectPeriodicReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IProjectPeriodicReportRenderer> byFormat = renderers
        .ToDictionary(renderer => renderer.Format);

    public IProjectPeriodicReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                $"No certified project-periodic renderer is registered for {format}.");
}

internal sealed class ExecutiveProjectStateReportRendererRegistry(
    IEnumerable<IExecutiveProjectStateReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IExecutiveProjectStateReportRenderer> byFormat = renderers
        .ToDictionary(renderer => renderer.Format);

    public IExecutiveProjectStateReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                $"No certified executive-project-state renderer is registered for {format}.");
}

internal sealed class ProjectProgressReportRendererRegistry(
    IEnumerable<IProjectProgressReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IProjectProgressReportRenderer> byFormat = renderers
        .ToDictionary(renderer => renderer.Format);

    public IProjectProgressReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                $"No certified project-progress renderer is registered for {format}.");
}

internal sealed class ProjectFinancialPositionReportRendererRegistry(
    IEnumerable<IProjectFinancialPositionReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IProjectFinancialPositionReportRenderer> byFormat =
        renderers.ToDictionary(renderer => renderer.Format);

    public IProjectFinancialPositionReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                $"No certified project-financial-position renderer is registered for {format}.");
}

internal sealed class ProjectCommercialProcurementSupplyReportRendererRegistry(
    IEnumerable<IProjectCommercialProcurementSupplyReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IProjectCommercialProcurementSupplyReportRenderer>
        byFormat = renderers.ToDictionary(renderer => renderer.Format);

    public IProjectCommercialProcurementSupplyReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                $"No certified project-commercial-procurement-supply renderer is registered for {format}.");
}

internal sealed class ProjectTechnicalOfficeReportRendererRegistry(
    IEnumerable<IProjectTechnicalOfficeReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IProjectTechnicalOfficeReportRenderer>
        byFormat = renderers.ToDictionary(renderer => renderer.Format);

    public IProjectTechnicalOfficeReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported", transient: false,
                $"No certified project-technical-office renderer is registered for {format}.");
}

internal sealed class ProjectQualityHseReportRendererRegistry(
    IEnumerable<IProjectQualityHseReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IProjectQualityHseReportRenderer>
        byFormat = renderers.ToDictionary(renderer => renderer.Format);

    public IProjectQualityHseReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported", transient: false,
                $"No certified project-quality-hse renderer is registered for {format}.");
}

internal sealed class ProjectGovernanceActionReportRendererRegistry(
    IEnumerable<IProjectGovernanceActionReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IProjectGovernanceActionReportRenderer>
        byFormat = renderers.ToDictionary(renderer => renderer.Format);

    public IProjectGovernanceActionReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported", transient: false,
                $"No certified project-governance-action renderer is registered for {format}.");
}

internal sealed class PortfolioSummaryReportRendererRegistry(
    IEnumerable<IPortfolioSummaryReportRenderer> renderers)
{
    private readonly Dictionary<ReportFormat, IPortfolioSummaryReportRenderer> byFormat =
        renderers.ToDictionary(renderer => renderer.Format);

    public IPortfolioSummaryReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported", transient: false,
                $"No certified portfolio-summary renderer is registered for {format}.");
}
