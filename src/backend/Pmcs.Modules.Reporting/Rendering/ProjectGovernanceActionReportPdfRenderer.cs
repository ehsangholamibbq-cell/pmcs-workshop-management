using System.Globalization;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectGovernanceActionReportPdfRenderer(
    ReportingRendererOptions options, ReportingExecutionOptions execution)
    : IProjectGovernanceActionReportRenderer
{
    private static readonly string[] FactHeaders =
        ["شماره", "نوع", "وضعیت", "ثبت UTC", "موعد محلی", "SLA UTC", "شدت/امتیاز", "ماتریس"];

    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ProjectGovernanceActionReportRenderRequest request)
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
            throw new ReportRenderingException("reporting.project_governance_action.renderer.pdf_failed",
                transient: false, "The certified F09 PDF could not be rendered.", exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(ProjectGovernanceActionReportRenderRequest request)
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
            throw new ReportRenderingException("reporting.project_governance_action.renderer.pdf_visual_failed",
                transient: false, "The F09 qualification images could not be rendered.", exception);
        }
    }

    private ProjectGovernanceActionReportRenderModel Prepare(ProjectGovernanceActionReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ProjectGovernanceActionReportRenderModel.Create(request);
        if (model.FactCount > execution.MaximumPdfFacts)
            throw new ReportRenderingException("reporting.output.page_limit_exceeded",
                transient: false, "The F09 PDF fact limit was exceeded.");
        return model;
    }

    private static Document CreateDocument(ProjectGovernanceActionReportRenderModel model) =>
        Document.Create(container =>
        {
            foreach (var section in model.Sections)
                container.Page(page => ConfigurePage(page, model, section.Title,
                    section.Section, section.Section.Rows.ToArray()));
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش رسمی راهبری و اقدام پروژه",
            Author = "PMCS Certified Reporting",
            Subject = model.Request.VerificationCode,
            Keywords = $"PMCS,{model.Request.DefinitionCode},{model.Request.TemplateVersion}",
            Creator = "PMCS Certified Reporting",
            Producer = $"PMCS renderer {model.Request.RendererContractVersion}",
            Language = "fa-IR",
            CreationDate = model.Request.SourceCutoffUtc.UtcDateTime,
            ModifiedDate = model.Request.SourceCutoffUtc.UtcDateTime
        });

    private static void ConfigurePage(PageDescriptor page, ProjectGovernanceActionReportRenderModel model,
        string title, ProjectGovernanceActionReportSection section, GovernanceActionReportingFact[] rows)
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
                column.Item().Text("گزارش رسمی راهبری و اقدام پروژه").FontSize(13.5f).Bold()
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
                        $"  |  {title}: {ProjectGovernanceActionReportFormatting.Status(section.Status)}" +
                        $"  |  شمارش رسمی: {ProjectGovernanceActionReportFormatting.Count(section.OfficialCount)}")
                        .Bold();
                    status.Item().Text($"طبقه‌بندی بخش: {PersianReportFormatting.Classification(section.Classification)}");
                    status.Item().Text($"علت‌های بخش: {ProjectGovernanceActionReportFormatting.Reasons(section.Reasons)}");
                });
            if (section.Status != GovernanceActionReportingStatus.Available)
                column.Item().Background("#F7F8FA").Padding(6).Text(
                    section.Status == GovernanceActionReportingStatus.InsufficientData
                        ? "تاریخچه یا پوشش کامل نیست؛ شمارش رسمی نامعلوم است و صفر فرض نمی‌شود."
                        : $"ردیف رسمی قابل نمایش نیست؛ {ProjectGovernanceActionReportFormatting.Status(section.Status)}.");
            else
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (var index = 0; index < FactHeaders.Length; index++)
                            columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var heading in FactHeaders)
                            header.Cell().Background("#DCE8F5").Border(0.5f)
                                .BorderColor("#AFC3DE").Padding(3).Text(heading).SemiBold();
                    });
                    foreach (var row in rows)
                    {
                        foreach (var value in new[]
                        {
                            row.Number, ProjectGovernanceActionReportFormatting.Kind(row.Kind), row.State,
                            row.OfficialAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
                            row.DueLocalDate.HasValue ? PersianReportFormatting.FormatDate(
                                row.DueLocalDate.Value) : "—",
                            row.SlaDueAtUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) ?? "—",
                            row.Rating ?? "—", row.MatrixVersion?.ToString(CultureInfo.InvariantCulture) ?? "—"
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
        "reporting.format.unsupported", transient: false, "The F09 PDF renderer received another format.");
}
