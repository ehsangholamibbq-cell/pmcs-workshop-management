using System.Globalization;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.TechnicalOffice.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectTechnicalOfficeReportPdfRenderer(
    ReportingRendererOptions options, ReportingExecutionOptions execution)
    : IProjectTechnicalOfficeReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ProjectTechnicalOfficeReportRenderRequest request)
    {
        if (request.Format != Format) throw UnsupportedFormat();
        try
        {
            var model = Prepare(request);
            var bytes = CreateDocument(model).GeneratePdf();
            return new RenderedReportArtifact(Format, request.FileName,
                ReportArtifactIdentity.ContentType(Format), bytes,
                ReportArtifactIdentity.Sha256(bytes), request.ManifestSha256,
                request.VerificationCode);
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception)
        {
            throw new ReportRenderingException(
                "reporting.project_technical_office.renderer.pdf_failed", transient: false,
                "The certified F07 PDF could not be rendered.", exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(
        ProjectTechnicalOfficeReportRenderRequest request)
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
            throw new ReportRenderingException(
                "reporting.project_technical_office.renderer.pdf_visual_failed", transient: false,
                "The F07 qualification images could not be rendered.", exception);
        }
    }

    private ProjectTechnicalOfficeReportRenderModel Prepare(
        ProjectTechnicalOfficeReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ProjectTechnicalOfficeReportRenderModel.Create(request);
        if (model.FactCount > execution.MaximumPdfFacts)
            throw new ReportRenderingException("reporting.output.page_limit_exceeded",
                transient: false, "The F07 PDF fact limit was exceeded.");
        return model;
    }

    private static Document CreateDocument(ProjectTechnicalOfficeReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page => ConfigurePage(page, model, "اسناد و بازنگری‌های ابلاغی",
                model.Snapshot.Documents, ["شماره سند", "نوع", "رشته", "بازنگری", "تاریخ بازنگری", "ابلاغ UTC"],
                model.Documents.Select(row => new[]
                {
                    row.Number, row.Type.ToString(), row.Discipline, row.RevisionCode,
                    PersianReportFormatting.FormatDate(row.RevisionDate), Utc(row.IssuedAtUtc)
                })));
            container.Page(page => ConfigurePage(page, model, "ترنسمیتال‌های صادرشده",
                model.Snapshot.Transmittals, ["شماره", "ابلاغ UTC", "بازنگری", "تأیید دریافت UTC", "وضعیت موعد"],
                model.Transmittals.Select(row => new[]
                {
                    row.Number, Utc(row.IssuedAtUtc), Number(row.RevisionCount),
                    row.AcknowledgedAtUtc.HasValue ? Utc(row.AcknowledgedAtUtc.Value) : "—",
                    ProjectTechnicalOfficeReportFormatting.Due(row.DueState)
                })));
            container.Page(page => ConfigurePage(page, model, "RFI صادرشده و پاسخ",
                model.Snapshot.Rfis, ["شماره", "وضعیت", "ابلاغ UTC", "پاسخ", "نوع پاسخ", "مانع", "معوق"],
                model.Rfis.Select(row => new[]
                {
                    row.Number, row.State.ToString(), Utc(row.IssuedAtUtc),
                    Number(row.ResponseCount), row.LastResponseClassification?.ToString() ?? "—",
                    ProjectTechnicalOfficeReportFormatting.YesNo(row.IsBlocking),
                    ProjectTechnicalOfficeReportFormatting.YesNo(row.Overdue)
                })));
            container.Page(page => ConfigurePage(page, model, "سابمیتال و بررسی",
                model.Snapshot.Submittals, ["شماره", "نوع", "رشته", "وضعیت", "نتیجه بررسی", "ارسال مجدد", "معوق"],
                model.Submittals.Select(row => new[]
                {
                    row.Number, row.Type.ToString(), row.Discipline, row.State.ToString(),
                    row.ReviewOutcome?.ToString() ?? "—", Number(row.ResubmissionNumber),
                    ProjectTechnicalOfficeReportFormatting.YesNo(row.ReviewOverdue)
                })));
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش رسمی دفتر فنی پروژه",
            Author = "PMCS Certified Reporting",
            Subject = model.Request.VerificationCode,
            Keywords = $"PMCS,{model.Request.DefinitionCode},{model.Request.TemplateVersion}",
            Creator = "PMCS Certified Reporting",
            Producer = $"PMCS renderer {model.Request.RendererContractVersion}",
            Language = "fa-IR",
            CreationDate = model.Request.SourceCutoffUtc.UtcDateTime,
            ModifiedDate = model.Request.SourceCutoffUtc.UtcDateTime
        });

    private static void ConfigurePage<T>(PageDescriptor page,
        ProjectTechnicalOfficeReportRenderModel model, string title,
        TechnicalReportingSection<T> section, string[] headers, IEnumerable<string[]> rows)
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
                column.Item().Text("گزارش رسمی دفتر فنی پروژه").FontSize(13.5f).Bold()
                    .FontColor("#0B4F8A");
                column.Item().Text($"{Safe(model.Snapshot.Project.Name, 180)} — " +
                    Safe(model.Snapshot.Project.Code, 80)).FontSize(8);
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
                        $"  |  {title}: {ProjectTechnicalOfficeReportFormatting.Status(section.Status)}" +
                        $"  |  شمارش رسمی: {ProjectTechnicalOfficeReportFormatting.Count(section.OfficialCount)}")
                        .Bold();
                    status.Item().Text($"علت‌های بخش: {ProjectTechnicalOfficeReportFormatting.Reasons(section.Reasons)}");
                    status.Item().Text($"علت‌های کل: {ProjectTechnicalOfficeReportFormatting.Reasons(model.Snapshot.Reasons)}");
                });
            if (section.Status != TechnicalReportingStatus.Available)
                column.Item().Background("#F7F8FA").Padding(6).Text(
                    section.Status == TechnicalReportingStatus.InsufficientData
                        ? "تاریخچه تغییر وضعیت کامل نیست؛ ردیف و شمارش رسمی این بخش نامعلوم است و صفر فرض نمی‌شود."
                        : $"ردیف رسمی قابل نمایش نیست؛ {ProjectTechnicalOfficeReportFormatting.Status(section.Status)}.");
            else
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers) columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var item in headers)
                            header.Cell().Background("#DCE8F5").Border(0.5f)
                                .BorderColor("#AFC3DE").Padding(3).Text(item).SemiBold();
                    });
                    foreach (var row in rows)
                        foreach (var value in row)
                            table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EB")
                                .Padding(3).Text(Safe(value, 180));
                });
        });
        page.Footer().BorderTop(1).BorderColor("#D1DBE8").PaddingTop(5).Row(row =>
        {
            row.RelativeItem().ContentFromLeftToRight().Text(
                $"{model.Request.VerificationCode}  |  {model.Request.ManifestSha256[..12]}")
                .FontSize(6);
            row.AutoItem().Text(text =>
            {
                text.Span("صفحه "); text.CurrentPageNumber();
                text.Span(" از "); text.TotalPages();
            });
        });
    }

    private static string Utc(DateTimeOffset instant) =>
        instant.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    private static string Number(int value) => PersianReportFormatting.ToPersianDigits(
        value.ToString(CultureInfo.InvariantCulture));
    private static string Safe(string value, int maximum) =>
        PersianReportFormatting.SafeText(value, maximum);
    private static ReportRenderingException UnsupportedFormat() => new(
        "reporting.format.unsupported", transient: false,
        "The F07 PDF renderer received another output format.");
}
