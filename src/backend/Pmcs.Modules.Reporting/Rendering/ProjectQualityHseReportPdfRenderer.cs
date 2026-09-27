using System.Globalization;
using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectQualityHseReportPdfRenderer(
    ReportingRendererOptions options, ReportingExecutionOptions execution)
    : IProjectQualityHseReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ProjectQualityHseReportRenderRequest request)
    {
        if (request.Format != Format) throw UnsupportedFormat();
        try
        {
            var bytes = CreateDocument(Prepare(request)).GeneratePdf();
            return new RenderedReportArtifact(Format, request.FileName,
                ReportArtifactIdentity.ContentType(Format), bytes,
                ReportArtifactIdentity.Sha256(bytes), request.ManifestSha256,
                request.VerificationCode);
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception)
        {
            throw new ReportRenderingException("reporting.project_quality_hse.renderer.pdf_failed",
                transient: false, "The certified F08 PDF could not be rendered.", exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(ProjectQualityHseReportRenderRequest request)
    {
        if (request.Format != Format) throw UnsupportedFormat();
        try
        {
            return CreateDocument(Prepare(request)).GenerateImages(new ImageGenerationSettings
            {
                ImageFormat = ImageFormat.Png,
                ImageCompressionQuality = ImageCompressionQuality.Best,
                RasterDpi = CertifiedPdfRuntimeContract.QualificationRasterDpi,
                UseTransparentBackground = false
            }).ToArray();
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception)
        {
            throw new ReportRenderingException("reporting.project_quality_hse.renderer.pdf_visual_failed",
                transient: false, "The F08 qualification images could not be rendered.", exception);
        }
    }

    private ProjectQualityHseReportRenderModel Prepare(ProjectQualityHseReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ProjectQualityHseReportRenderModel.Create(request);
        if (model.FactCount > execution.MaximumPdfFacts)
            throw new ReportRenderingException("reporting.output.page_limit_exceeded",
                transient: false, "The F08 PDF fact limit was exceeded.");
        return model;
    }

    private static Document CreateDocument(ProjectQualityHseReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page => ConfigurePage(page, model, "کنترل کیفیت", model.Snapshot.Quality, model.Quality));
            container.Page(page => ConfigurePage(page, model, "ایمنی و بهداشت", model.Snapshot.Hse, model.Hse));
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش رسمی کیفیت و ایمنی پروژه",
            Author = "PMCS Certified Reporting",
            Subject = model.Request.VerificationCode,
            Keywords = $"PMCS,{model.Request.DefinitionCode},{model.Request.TemplateVersion}",
            Creator = "PMCS Certified Reporting",
            Producer = $"PMCS renderer {model.Request.RendererContractVersion}",
            Language = "fa-IR",
            CreationDate = model.Request.SourceCutoffUtc.UtcDateTime,
            ModifiedDate = model.Request.SourceCutoffUtc.UtcDateTime
        });

    private static void ConfigurePage(PageDescriptor page, ProjectQualityHseReportRenderModel model,
        string title, ProjectQualityHseReportSection section, QualityHseReportingFact[] rows)
    {
        page.Size(PageSizes.A4.Landscape());
        page.MarginHorizontal(20);
        page.MarginVertical(16);
        page.PageColor(Colors.White);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(style => style.FontFamily(CertifiedPdfRuntimeContract.FontFamily)
            .FontSize(7.2f).FontColor("#172033"));
        page.Header().BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(6)
            .Column(column =>
            {
                column.Item().Text("گزارش رسمی کیفیت و ایمنی پروژه").FontSize(13.5f).Bold()
                    .FontColor("#0B4F8A");
                column.Item().Text($"{Safe(model.Snapshot.Project.Name)} — " +
                    Safe(model.Snapshot.Project.Code)).FontSize(8);
                column.Item().Text(title).FontSize(8).Bold();
            });
        page.Content().PaddingVertical(8).Column(column =>
        {
            column.Spacing(6);
            column.Item().Text($"برش: {PersianReportFormatting.FormatDate(model.Snapshot.Cutoff.CutoffLocalDate)}" +
                $"  |  {PersianReportFormatting.FormatInstant(model.Snapshot.Cutoff.SourceCutoffUtc, model.Snapshot.Project.TimeZone)}" +
                $"  |  {PersianReportFormatting.Classification(model.Snapshot.Classification)}");
            column.Item().Background("#EFF4FA").Border(1).BorderColor("#B7C9E2")
                .Padding(6).Column(status =>
                {
                    status.Item().Text($"وضعیت کل: {PersianReportFormatting.DataStatus(model.Snapshot.DataStatus)}" +
                        $"  |  {title}: {ProjectQualityHseReportFormatting.Status(section.Status)}" +
                        $"  |  شمارش رسمی: {ProjectQualityHseReportFormatting.Count(section.OfficialCount)}")
                        .Bold();
                    status.Item().Text($"طبقه‌بندی بخش: {PersianReportFormatting.Classification(section.Classification)}");
                    status.Item().Text($"علت‌های بخش: {ProjectQualityHseReportFormatting.Reasons(section.Reasons)}");
                });
            if (section.Status != QualityHseReportingStatus.Available)
                column.Item().Background("#F7F8FA").Padding(6).Text(
                    section.Status == QualityHseReportingStatus.InsufficientData
                        ? "تاریخچه یا پوشش کامل نیست؛ شمارش رسمی نامعلوم است و صفر فرض نمی‌شود."
                        : $"ردیف رسمی قابل نمایش نیست؛ {ProjectQualityHseReportFormatting.Status(section.Status)}.");
            else
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                        columns.RelativeColumn(); columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var heading in new[] { "شماره", "نوع", "وضعیت/نتیجه", "زمان UTC", "ساعت تأییدشده" })
                            header.Cell().Background("#DCE8F5").Border(0.5f)
                                .BorderColor("#AFC3DE").Padding(3).Text(heading).SemiBold();
                    });
                    foreach (var row in rows)
                    {
                        foreach (var value in new[]
                        {
                            row.Number, ProjectQualityHseReportFormatting.Kind(row.Kind), row.State,
                            row.OfficialAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
                            row.Hours?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—"
                        })
                            table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EB")
                                .Padding(3).Text(Safe(value));
                    }
                });
        });
        page.Footer().BorderTop(1).BorderColor("#D1DBE8").PaddingTop(5).Row(row =>
        {
            row.RelativeItem().ContentFromLeftToRight().Text(
                $"{model.Request.VerificationCode}  |  {model.Request.ManifestSha256[..12]}").FontSize(6);
            row.AutoItem().Text(text =>
            {
                text.Span("صفحه "); text.CurrentPageNumber();
                text.Span(" از "); text.TotalPages();
            });
        });
    }

    private static string Safe(string value) => PersianReportFormatting.SafeText(value, 180);
    private static ReportRenderingException UnsupportedFormat() => new(
        "reporting.format.unsupported", transient: false, "The F08 PDF renderer received another format.");
}
