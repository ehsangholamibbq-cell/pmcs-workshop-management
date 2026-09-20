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
