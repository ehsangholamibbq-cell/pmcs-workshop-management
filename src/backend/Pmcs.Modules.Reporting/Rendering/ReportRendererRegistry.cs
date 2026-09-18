using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ReportRendererRegistry(IEnumerable<IReportRenderer> renderers)
{
    private readonly IReadOnlyDictionary<ReportFormat, IReportRenderer> byFormat = renderers
        .ToDictionary(renderer => renderer.Format);

    public IReportRenderer Require(ReportFormat format) =>
        byFormat.TryGetValue(format, out var renderer)
            ? renderer
            : throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                $"No certified renderer is registered for {format}.");
}
