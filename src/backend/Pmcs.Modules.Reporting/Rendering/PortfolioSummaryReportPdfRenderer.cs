using System.Globalization;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class PortfolioSummaryReportPdfRenderer(
    ReportingRendererOptions options, ReportingExecutionOptions execution)
    : IPortfolioSummaryReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(PortfolioSummaryReportRenderRequest request)
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
            throw new ReportRenderingException("reporting.portfolio.renderer.pdf_failed",
                transient: false, "The certified F10 PDF could not be rendered.", exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(PortfolioSummaryReportRenderRequest request)
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
            throw new ReportRenderingException("reporting.portfolio.renderer.pdf_visual_failed",
                transient: false, "F10 qualification images could not be rendered.", exception);
        }
    }

    private PortfolioSummaryReportRenderModel Prepare(PortfolioSummaryReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = PortfolioSummaryReportRenderModel.Create(request);
        if (model.Snapshot.Projects.Count > execution.MaximumPdfFacts)
            throw new ReportRenderingException("reporting.output.page_limit_exceeded",
                transient: false, "The F10 PDF row limit was exceeded.");
        return model;
    }

    private static Document CreateDocument(PortfolioSummaryReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page => Configure(page, model, "خلاصهٔ مجموعه", content => Overview(content, model)));
            container.Page(page => Configure(page, model, "وضعیت پروژه‌های مجاز", content => Projects(content, model)));
            container.Page(page => Configure(page, model, "ابعاد مالی و تجاری", content => Dimensions(content, model)));
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش رسمی خلاصهٔ مجموعه پروژه‌ها",
            Author = "PMCS Certified Reporting",
            Subject = model.Request.VerificationCode,
            Keywords = $"PMCS,{model.Request.DefinitionCode},{model.Request.TemplateVersion}",
            Creator = "PMCS Certified Reporting",
            Producer = $"PMCS renderer {model.Request.RendererContractVersion}",
            Language = "fa-IR",
            CreationDate = model.Request.SourceCutoffUtc.UtcDateTime,
            ModifiedDate = model.Request.SourceCutoffUtc.UtcDateTime
        });

    private static void Configure(PageDescriptor page, PortfolioSummaryReportRenderModel model,
        string title, Action<IContainer> content)
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
                column.Item().Text("گزارش رسمی خلاصهٔ مجموعه پروژه‌ها").FontSize(13.5f).Bold()
                    .FontColor("#0B4F8A");
                column.Item().Text(title).FontSize(8).Bold();
            });
        content(page.Content().PaddingVertical(8));
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

    private static void Overview(IContainer content, PortfolioSummaryReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        content.Column(column =>
        {
            column.Spacing(8);
            column.Item().Text($"برش UTC: {snapshot.AsOfUtc:yyyy-MM-dd HH:mm}  |  " +
                $"طبقه‌بندی: {PersianReportFormatting.Classification(model.Request.Classification)}");
            column.Item().Background("#EFF4FA").Border(1).BorderColor("#B7C9E2")
                .Padding(8).Text($"شمار پروژه‌های مجاز: {snapshot.AuthorizedProjectCount}  |  " +
                    $"وضعیت داده: {PersianReportFormatting.DataStatus(snapshot.DataStatus)}").Bold();
            column.Item().Text("ارزها مستقل‌اند؛ جمع تبدیل‌شده یا سلامت کلی مجموعه وجود ندارد.");
            var groups = snapshot.CurrencyGroups.ToArray();
            if (groups.Length == 0)
                column.Item().Text("گروه ارزی رسمی قابل نمایش نیست.");
            else
                column.Item().Table(table =>
                {
                    Columns(table, 7);
                    Headers(table, ["ارز", "مشارکت مالی", "هزینه شناسایی‌شده", "خالص نقد خارجی",
                        "مشارکت تجاری", "تعهد کل", "تعهد باز"]);
                    foreach (var group in groups)
                        Cells(table, [group.CurrencyCode,
                            group.FinancialContributorCount.ToString(CultureInfo.InvariantCulture),
                            PortfolioSummaryReportFormatting.Amount(group.RecognizedSpendSubtotal),
                            PortfolioSummaryReportFormatting.Amount(group.ExternalNetCashSubtotal),
                            group.CommercialContributorCount.ToString(CultureInfo.InvariantCulture),
                            PortfolioSummaryReportFormatting.Amount(group.TotalCommittedSubtotal),
                            PortfolioSummaryReportFormatting.Amount(group.OpenCommitmentSubtotal)]);
                });
            column.Item().Text($"Snapshot SHA-256: {model.Request.SnapshotSha256}").FontSize(6);
            column.Item().Text($"Source Manifest SHA-256: {snapshot.SourceManifestSha256}").FontSize(6);
        });
    }

    private static void Projects(IContainer content, PortfolioSummaryReportRenderModel model)
    {
        content.Column(column =>
        {
            column.Spacing(6);
            if (model.Snapshot.Projects.Count == 0)
                column.Item().Text("پروژهٔ مجاز در برش انتخاب‌شده وجود ندارد.");
            else
                column.Item().Table(table =>
                {
                    Columns(table, 10);
                    Headers(table, ["کد", "پروژه", "چرخه", "تاریخ محلی", "عملیات", "ارزیابی",
                        "پوشش", "تازگی", "اطمینان", "جزئی"]);
                    foreach (var project in model.Snapshot.Projects)
                        Cells(table, [PortfolioSummaryReportFormatting.Code(project),
                            PortfolioSummaryReportFormatting.Name(project), project.Lifecycle.ToString(),
                            PersianReportFormatting.FormatDate(project.CutoffLocalDate),
                            PortfolioSummaryReportFormatting.Status(project.OperationalStatus),
                            project.OperationalAssessment.HasValue ? PersianReportFormatting.OperationalStatus(
                                project.OperationalAssessment.Value) : "—",
                            project.Coverage.HasValue ? PersianReportFormatting.CoverageStatus(project.Coverage.Value) : "—",
                            project.Freshness.HasValue ? PersianReportFormatting.FreshnessStatus(project.Freshness.Value) : "—",
                            project.Confidence.HasValue ? PersianReportFormatting.ConfidenceStatus(project.Confidence.Value) : "—",
                            project.IsPartial.HasValue ? (project.IsPartial.Value ? "بله" : "خیر") : "—"]);
                });
        });
    }

    private static void Dimensions(IContainer content, PortfolioSummaryReportRenderModel model)
    {
        content.Column(column =>
        {
            column.Spacing(6);
            if (model.Snapshot.Projects.Count == 0)
                column.Item().Text("بعد مالی یا تجاری مجاز در این برش وجود ندارد.");
            else
                column.Item().Table(table =>
                {
                    Columns(table, 10);
                    Headers(table, ["پروژه", "ارز", "مالی", "هزینه", "خالص نقد", "تجاری",
                        "تعهد کل", "تعهد باز", "علت مالی", "علت تجاری"]);
                    foreach (var project in model.Snapshot.Projects)
                        Cells(table, [PortfolioSummaryReportFormatting.Code(project),
                            project.BaseCurrencyCode ?? "—",
                            PortfolioSummaryReportFormatting.Status(project.Financial.Status),
                            project.Financial.Status == PortfolioDimensionStatus.NotAuthorized ? "—" :
                                PortfolioSummaryReportFormatting.Amount(project.Financial.RecognizedSpend),
                            project.Financial.Status == PortfolioDimensionStatus.NotAuthorized ? "—" :
                                PortfolioSummaryReportFormatting.Amount(project.Financial.ExternalNetCash),
                            PortfolioSummaryReportFormatting.Status(project.Commercial.Status),
                            project.Commercial.Status == PortfolioDimensionStatus.NotAuthorized ? "—" :
                                PortfolioSummaryReportFormatting.Amount(project.Commercial.TotalCommittedAmount),
                            project.Commercial.Status == PortfolioDimensionStatus.NotAuthorized ? "—" :
                                PortfolioSummaryReportFormatting.Amount(project.Commercial.OpenCommitmentAmount),
                            PortfolioSummaryReportFormatting.Reason(project.Financial.ReasonCode),
                            PortfolioSummaryReportFormatting.Reason(project.Commercial.ReasonCode)]);
                });
        });
    }

    private static void Columns(TableDescriptor table, int count) =>
        table.ColumnsDefinition(columns =>
        {
            for (var index = 0; index < count; index++) columns.RelativeColumn();
        });

    private static void Headers(TableDescriptor table, IReadOnlyList<string> labels) =>
        table.Header(header =>
        {
            foreach (var label in labels)
                header.Cell().Background("#DCE8F5").Border(0.5f).BorderColor("#AFC3DE")
                    .Padding(3).Text(label).SemiBold();
        });

    private static void Cells(TableDescriptor table, IReadOnlyList<string> values)
    {
        foreach (var value in values)
            table.Cell().BorderBottom(0.5f).BorderColor("#D7E0EB").Padding(3)
                .Text(PersianReportFormatting.SafeText(value, 180));
    }

    private static ReportRenderingException UnsupportedFormat() => new(
        "reporting.format.unsupported", transient: false,
        "The F10 PDF renderer received another format.");
}
