using System.Globalization;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class DailyReportPdfRenderer(
    ReportingRendererOptions options,
    ReportingExecutionOptions execution) : IReportRenderer
{
    private const string FontFamily = "DejaVu Sans";
    private static readonly object ConfigurationGate = new();
    private static string? configuredSignature;

    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ReportRenderRequest request)
    {
        if (request.Format != Format)
        {
            throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                "The PDF renderer received another output format.");
        }

        EnsureQuestPdfConfigured();
        var orderedVersions = request.Snapshot.Versions
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId)
            .ToArray();
        if (orderedVersions.Sum(version => version.Facts.Count) > execution.MaximumPdfFacts)
        {
            throw new ReportRenderingException(
                "reporting.output.page_limit_exceeded",
                transient: false,
                "The certified PDF fact limit was exceeded.");
        }

        try
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Portrait());
                    page.MarginHorizontal(28);
                    page.MarginVertical(24);
                    page.PageColor(Colors.White);
                    page.ContentFromRightToLeft();
                    page.DefaultTextStyle(style => style
                        .FontFamily(FontFamily)
                        .FontSize(8.5f)
                        .FontColor("#172033"));
                    page.Header().Element(header => ComposeHeader(header, request));
                    page.Content().PaddingVertical(12).Element(content =>
                        ComposeContent(content, request, orderedVersions));
                    page.Footer().Element(footer => ComposeFooter(footer, request));
                });
            }).WithMetadata(new DocumentMetadata
            {
                Title = "گزارش روزانه رسمی",
                Author = "PMCS Certified Reporting",
                Subject = request.VerificationCode,
                Keywords = $"PMCS,{request.DefinitionCode},{request.TemplateVersion}",
                Creator = "PMCS Certified Reporting",
                Producer = $"PMCS renderer {request.RendererContractVersion}",
                Language = "fa-IR",
                CreationDate = request.SourceCutoffUtc.UtcDateTime,
                ModifiedDate = request.SourceCutoffUtc.UtcDateTime
            });

            var bytes = document.GeneratePdf();
            return new RenderedReportArtifact(
                Format,
                request.FileName,
                ReportArtifactIdentity.ContentType(Format),
                bytes,
                ReportArtifactIdentity.Sha256(bytes),
                request.ManifestSha256,
                request.VerificationCode);
        }
        catch (ReportRenderingException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ReportRenderingException(
                "reporting.renderer.pdf_failed",
                transient: false,
                "The certified PDF could not be rendered.",
                exception);
        }
    }

    private void EnsureQuestPdfConfigured()
    {
        var signature = $"{options.PdfLicense}|{options.PdfRegularFontPath}|{options.PdfBoldFontPath}";
        lock (ConfigurationGate)
        {
            if (configuredSignature is not null)
            {
                if (!string.Equals(configuredSignature, signature, StringComparison.Ordinal))
                {
                    throw new ReportRenderingException(
                        "reporting.renderer.configuration_conflict",
                        transient: false,
                        "PDF renderer configuration changed inside one process.");
                }
                return;
            }

            QuestPDF.Settings.License = options.PdfLicense.ToLowerInvariant() switch
            {
                "community" => LicenseType.Community,
                "professional" => LicenseType.Professional,
                "enterprise" => LicenseType.Enterprise,
                _ => throw new ReportRenderingException(
                    "reporting.renderer.license_unconfigured",
                    transient: false,
                    "A reviewed QuestPDF license mode must be configured before PDF rendering is enabled.")
            };
            if (!File.Exists(options.PdfRegularFontPath) || !File.Exists(options.PdfBoldFontPath))
            {
                throw new ReportRenderingException(
                    "reporting.renderer.font_missing",
                    transient: false,
                    "The certified Persian PDF font files are not available.");
            }

            using (var regularFont = File.OpenRead(options.PdfRegularFontPath))
            {
                FontManager.RegisterFont(regularFont);
            }
            using (var boldFont = File.OpenRead(options.PdfBoldFontPath))
            {
                FontManager.RegisterFont(boldFont);
            }
            configuredSignature = signature;
        }
    }

    private static void ComposeHeader(IContainer container, ReportRenderRequest request)
    {
        container.BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("گزارش روزانه رسمی")
                    .FontSize(16).Bold().FontColor("#0B4F8A");
                column.Item().Text(
                    $"{PersianReportFormatting.SafeText(request.Snapshot.Project.Name, 180)} — " +
                    $"{PersianReportFormatting.SafeText(request.Snapshot.Project.Code, 80)}")
                    .FontSize(9).FontColor("#46556D");
            });
            row.ConstantItem(74).AlignLeft().Column(column =>
            {
                column.Item().AlignCenter().Text("PMCS").FontSize(15).Bold().FontColor("#0B4F8A");
                column.Item().AlignCenter().Text($"قالب {PersianReportFormatting.ToPersianDigits(request.TemplateVersion)}")
                    .FontSize(7).FontColor("#62728A");
            });
        });
    }

    private static void ComposeContent(
        IContainer container,
        ReportRenderRequest request,
        IReadOnlyCollection<DailyReportReportingVersion> versions)
    {
        var current = request.Snapshot.CurrentOfficialReportId.HasValue
            ? versions.SingleOrDefault(version => version.ReportId == request.Snapshot.CurrentOfficialReportId.Value)
            : null;
        container.Column(column =>
        {
            column.Spacing(9);
            column.Item().Element(summary => ComposeSummary(summary, request, current));
            column.Item().Background(StatusColor(request.Snapshot.DataStatus))
                .Border(1).BorderColor(StatusBorderColor(request.Snapshot.DataStatus))
                .Padding(8)
                .Text(PersianReportFormatting.DataStatus(request.Snapshot.DataStatus))
                .Bold().FontSize(10);

            if (request.Snapshot.DataStatus != ReportDataStatus.Available)
            {
                column.Item().PaddingVertical(18).AlignCenter().Text(
                    request.Snapshot.DataStatus == ReportDataStatus.NoData
                        ? "تا زمان برش انتخاب‌شده هیچ نسخه رسمی تأییدشده‌ای وجود ندارد."
                        : "نسخه رسمی وجود دارد، اما داده ساختاریافته لازم برای گزارش کامل نیست.")
                    .FontSize(11).FontColor("#5E6675");
            }

            foreach (var version in versions)
            {
                column.Item().PaddingTop(6).Element(section =>
                    ComposeVersion(section, version, request.Snapshot.Project.TimeZone));
            }
        });
    }

    private static void ComposeSummary(
        IContainer container,
        ReportRenderRequest request,
        DailyReportReportingVersion? current)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(82);
                columns.RelativeColumn();
                columns.ConstantColumn(82);
                columns.RelativeColumn();
            });
            SummaryCell(table, "تاریخ گزارش", header: true);
            SummaryCell(table, current is null ? "—" : PersianReportFormatting.FormatDate(current.ReportDate));
            SummaryCell(table, "نسخه جاری", header: true);
            SummaryCell(table, current is null ? "—" : PersianReportFormatting.ToPersianDigits(current.VersionNumber.ToString(CultureInfo.InvariantCulture)));
            SummaryCell(table, "زمان برش", header: true);
            SummaryCell(table, PersianReportFormatting.FormatInstant(request.SourceCutoffUtc, request.Snapshot.Project.TimeZone));
            SummaryCell(table, "وضعیت", header: true);
            SummaryCell(table, PersianReportFormatting.DataStatus(request.Snapshot.DataStatus));
            SummaryCell(table, "کد راستی‌آزمایی", header: true);
            table.Cell().ColumnSpan(3).Element(SummaryValueCell)
                .ContentFromLeftToRight().Text(request.VerificationCode).FontSize(8).SemiBold();
        });
    }

    private static void ComposeVersion(
        IContainer container,
        DailyReportReportingVersion version,
        string projectTimeZone)
    {
        container.Border(1).BorderColor("#C8D6E8").Column(column =>
        {
            column.Item().Background("#EAF1F8").Padding(7).Row(row =>
            {
                row.RelativeItem().Text(
                    $"نسخه {PersianReportFormatting.ToPersianDigits(version.VersionNumber.ToString(CultureInfo.InvariantCulture))} — " +
                    PersianReportFormatting.VersionState(version.State)).Bold().FontColor("#163F68");
                row.AutoItem().Text(PersianReportFormatting.FormatDate(version.ReportDate))
                    .FontColor("#46556D");
            });
            column.Item().PaddingHorizontal(7).PaddingTop(6).Text(text =>
            {
                text.Span("محل: ").SemiBold();
                text.Span(PersianReportFormatting.SafeText(version.LocationName, 200));
                text.Span("  |  تأیید: ").SemiBold();
                text.Span(PersianReportFormatting.FormatInstant(version.ApprovedAt, projectTimeZone));
            });
            column.Item().Padding(7).Text(text =>
            {
                text.Span("شرح روزانه: ").SemiBold();
                text.Span(PersianReportFormatting.SafeText(version.Narrative));
            });

            if (version.Facts.Count == 0)
            {
                column.Item().Padding(7).Text("Fact ساختاریافته‌ای در این نسخه ثبت نشده است.")
                    .FontColor("#7A5660");
                return;
            }

            column.Item().Padding(7).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(3.6f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(0.9f);
                });
                table.Header(header =>
                {
                    FactHeader(header, "نوع");
                    FactHeader(header, "محل");
                    FactHeader(header, "شرح");
                    FactHeader(header, "مقدار");
                    FactHeader(header, "واحد");
                    FactHeader(header, "اثر");
                });
                foreach (var fact in version.Facts.OrderBy(item => item.CreatedAt).ThenBy(item => item.FactId))
                {
                    FactValue(table, PersianReportFormatting.FactKind(fact.Kind));
                    FactValue(table, PersianReportFormatting.SafeText(fact.LocationName ?? version.LocationName, 180));
                    FactValue(table, PersianReportFormatting.SafeText(fact.Description, 1_000));
                    FactValue(table, PersianReportFormatting.FormatDecimal(fact.Quantity));
                    FactValue(table, PersianReportFormatting.SafeText(fact.Unit, 80));
                    FactValue(table, PersianReportFormatting.Impact(fact.ImpactLevel));
                }
            });
            column.Item().PaddingHorizontal(7).PaddingBottom(7)
                .ContentFromLeftToRight()
                .Text($"Report ID: {version.ReportId} | Revision: {version.Revision}")
                .FontSize(6.5f).FontColor("#748198");
        });
    }

    private static void ComposeFooter(IContainer container, ReportRenderRequest request)
    {
        container.BorderTop(1).BorderColor("#D1DBE8").PaddingTop(6).Row(row =>
        {
            row.RelativeItem()
                .ContentFromLeftToRight()
                .DefaultTextStyle(style => style.FontSize(6.5f).FontColor("#5D6B80"))
                .Text(text =>
                {
                    text.Span(request.VerificationCode).SemiBold();
                    text.Span("  |  ");
                    text.Span(request.ManifestSha256[..12]);
                });
            row.AutoItem()
                .DefaultTextStyle(style => style.FontSize(7).FontColor("#5D6B80"))
                .Text(text =>
                {
                    text.Span("صفحه ");
                    text.CurrentPageNumber();
                    text.Span(" از ");
                    text.TotalPages();
                });
        });
    }

    private static void SummaryCell(TableDescriptor table, string value, bool header = false) =>
        table.Cell().Element(header ? SummaryHeaderCell : SummaryValueCell).Text(value);

    private static IContainer SummaryHeaderCell(IContainer container) => container
        .Background("#EEF3F9").Border(0.5f).BorderColor("#C8D6E8").Padding(5);

    private static IContainer SummaryValueCell(IContainer container) => container
        .Border(0.5f).BorderColor("#C8D6E8").Padding(5);

    private static void FactHeader(TableCellDescriptor header, string value) => header.Cell()
        .Background("#DCE8F5").Border(0.5f).BorderColor("#AFC3DE").Padding(4)
        .AlignMiddle().Text(value).SemiBold().FontSize(7.5f);

    private static void FactValue(TableDescriptor table, string value) => table.Cell()
        .BorderBottom(0.5f).BorderColor("#D7E0EB").Padding(4)
        .AlignMiddle().Text(value).FontSize(7.2f);

    private static string StatusColor(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "#E9F6EE",
        ReportDataStatus.NoData => "#F4F5F7",
        _ => "#FFF4E5"
    };

    private static string StatusBorderColor(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "#A9D8B8",
        ReportDataStatus.NoData => "#CFD4DC",
        _ => "#E5C17A"
    };
}
