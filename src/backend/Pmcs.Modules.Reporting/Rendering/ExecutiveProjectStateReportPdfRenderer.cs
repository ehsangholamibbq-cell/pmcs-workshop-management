using System.Globalization;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ExecutiveProjectStateReportPdfRenderer(
    ReportingRendererOptions options,
    ReportingExecutionOptions execution) : IExecutiveProjectStateReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ExecutiveProjectStateReportRenderRequest request)
    {
        if (request.Format != Format)
        {
            throw UnsupportedFormat();
        }

        try
        {
            var model = Prepare(request);
            var bytes = CreateDocument(model).GeneratePdf();
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
                "reporting.executive_state.renderer.pdf_failed",
                transient: false,
                "The certified Executive Project State PDF could not be rendered.",
                exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(
        ExecutiveProjectStateReportRenderRequest request)
    {
        if (request.Format != Format)
        {
            throw UnsupportedFormat();
        }

        try
        {
            var model = Prepare(request);
            return CreateDocument(model)
                .GenerateImages(new ImageGenerationSettings
                {
                    ImageFormat = ImageFormat.Png,
                    ImageCompressionQuality = ImageCompressionQuality.Best,
                    RasterDpi = CertifiedPdfRuntimeContract.QualificationRasterDpi,
                    UseTransparentBackground = false
                })
                .ToArray();
        }
        catch (ReportRenderingException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ReportRenderingException(
                "reporting.executive_state.renderer.pdf_visual_failed",
                transient: false,
                "The Executive Project State PDF qualification image could not be rendered.",
                exception);
        }
    }

    private ExecutiveProjectStateReportRenderModel Prepare(
        ExecutiveProjectStateReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ExecutiveProjectStateReportRenderModel.Create(request);
        if (model.AttentionItems.Count + model.Trend.Count > execution.MaximumPdfFacts)
        {
            throw new ReportRenderingException(
                "reporting.output.page_limit_exceeded",
                transient: false,
                "The certified Executive Project State PDF row limit was exceeded.");
        }
        return model;
    }

    private static Document CreateDocument(ExecutiveProjectStateReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.MarginHorizontal(24);
                page.MarginVertical(20);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style
                    .FontFamily(CertifiedPdfRuntimeContract.FontFamily)
                    .FontSize(8.1f)
                    .FontColor("#172033"));
                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().PaddingVertical(9).Element(content => ComposeContent(content, model));
                page.Footer().Element(footer => ComposeFooter(footer, model.Request));
            });
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش مدیریتی وضعیت رسمی پروژه",
            Author = "PMCS Certified Reporting",
            Subject = model.Request.VerificationCode,
            Keywords = $"PMCS,{model.Request.DefinitionCode},{model.Request.TemplateVersion}",
            Creator = "PMCS Certified Reporting",
            Producer = $"PMCS renderer {model.Request.RendererContractVersion}",
            Language = "fa-IR",
            CreationDate = model.Request.SourceCutoffUtc.UtcDateTime,
            ModifiedDate = model.Request.SourceCutoffUtc.UtcDateTime
        });

    private static void ComposeHeader(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        container.BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("گزارش مدیریتی وضعیت رسمی پروژه")
                    .FontSize(15).Bold().FontColor("#0B4F8A");
                column.Item().Text(
                    $"{PersianReportFormatting.SafeText(model.Snapshot.Project.Name, 180)} — " +
                    PersianReportFormatting.SafeText(model.Snapshot.Project.Code, 80))
                    .FontSize(8.5f).FontColor("#46556D");
            });
            row.ConstantItem(82).AlignLeft().Column(column =>
            {
                column.Item().AlignCenter().Text("PMCS").FontSize(15).Bold().FontColor("#0B4F8A");
                column.Item().AlignCenter().Text(
                    $"قالب {PersianReportFormatting.ToPersianDigits(model.Request.TemplateVersion)}")
                    .FontSize(7).FontColor("#62728A");
            });
        });
    }

    private static void ComposeContent(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Element(summary => ComposeSummary(summary, model));
            column.Item().Element(status => ComposeDataStatus(status, model));
            if (model.Snapshot.OfficialSnapshot is null)
            {
                column.Item().Element(EmptyBox).Text(
                    "تا زمان برش، Snapshot رسمی قابل ارائه‌ای وجود ندارد؛ هیچ وضعیت یا عددی ساخته نشده است.");
                return;
            }

            column.Item().Element(scope => ComposeScope(scope, model));
            column.Item().Element(kpis => ComposeKpis(kpis, model));
            column.Item().Element(section => ComposeIndependentStatuses(section, model));
            column.Item().Element(section => ComposeFactsAndFeatures(section, model));
            column.Item().Element(section => ComposeAttention(section, model));
            column.Item().Element(section => ComposeTrend(section, model));
            column.Item().Element(section => ComposeLineage(section, model));
        });
    }

    private static void ComposeSummary(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(86);
                columns.RelativeColumn();
                columns.ConstantColumn(86);
                columns.RelativeColumn();
            });
            SummaryCell(table, "تاریخ برش شمسی", header: true);
            SummaryCell(table, PersianReportFormatting.FormatDate(snapshot.Cutoff.CutoffLocalDate));
            SummaryCell(table, "زمان برش", header: true);
            SummaryCell(table, PersianReportFormatting.FormatInstant(
                snapshot.Cutoff.SourceCutoffUtc,
                snapshot.Project.TimeZone));
            SummaryCell(table, "ارز پایه پروژه", header: true);
            SummaryCell(table, PersianReportFormatting.SafeText(snapshot.Project.BaseCurrencyCode, 24));
            SummaryCell(table, "منبع وضعیت", header: true);
            SummaryCell(table, PersianReportFormatting.SourceState(snapshot.SourceState));
            SummaryCell(table, "کد راستی‌آزمایی", header: true);
            table.Cell().ColumnSpan(3).Element(SummaryValueCell)
                .ContentFromLeftToRight().Text(model.Request.VerificationCode).FontSize(7.5f).SemiBold();
        });
    }

    private static void ComposeDataStatus(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        container.Background(StatusColor(model.Snapshot.DataStatus))
            .Border(1).BorderColor(StatusBorderColor(model.Snapshot.DataStatus))
            .Padding(8).Column(column =>
            {
                column.Item().Text(PersianReportFormatting.DataStatus(model.Snapshot.DataStatus))
                    .Bold().FontSize(10);
                column.Item().PaddingTop(3).Text(StatusMessage(model.Snapshot.DataStatus))
                    .FontColor("#4C596C");
                if (model.ReasonCodes.Count > 0)
                {
                    column.Item().PaddingTop(4).Text(
                        string.Join("؛ ", model.ReasonCodes.Select(PersianReportFormatting.ReasonCode)))
                        .FontSize(7.7f).FontColor("#684C20");
                }
            });
    }

    private static void ComposeScope(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        var official = model.Snapshot.OfficialSnapshot!;
        container.Background(official.IsPartial ? "#FFF4E5" : "#EEF6FF")
            .Border(0.8f).BorderColor(official.IsPartial ? "#E5C17A" : "#B8D3EC")
            .Padding(7).Text(text =>
            {
                text.Span("محدوده ارزیابی: ").SemiBold();
                text.Span(PersianReportFormatting.AssessmentScope(official.AssessmentScope));
                text.Span(" — ");
                text.Span(official.IsPartial ? "محدود/جزئی" : "کامل در محدوده رسمی").SemiBold();
                text.Span(". وضعیت پایدار فقط به عملیات روزانه رسمی این محدوده اشاره دارد و سلامت کل پروژه نیست.");
            });
    }

    private static void ComposeKpis(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        var official = model.Snapshot.OfficialSnapshot!;
        container.Row(row =>
        {
            row.RelativeItem().Element(item => Kpi(
                item,
                "پوشش رسمی",
                $"{PersianReportFormatting.FormatDecimal(official.CoveragePercent)}٪"));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(
                item,
                "روزهای تأییدشده",
                $"{official.ApprovedReportDays}/{official.ExpectedReportDays}"));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(item, "Fact رسمی", official.FactCounts.Approved));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(
                item,
                "موارد توجه",
                official.AttentionSummary.Issues + official.AttentionSummary.Stoppages));
        });
    }

    private static void ComposeIndependentStatuses(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        var official = model.Snapshot.OfficialSnapshot!;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "وضعیت‌های مستقل Snapshot رسمی"));
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                TableHeaderCell(table, "عملیاتی");
                TableHeaderCell(table, "پوشش");
                TableHeaderCell(table, "تازگی");
                TableHeaderCell(table, "اعتمادپذیری");
                TableValue(table, PersianReportFormatting.OperationalStatus(official.OperationalStatus));
                TableValue(table, PersianReportFormatting.CoverageStatus(official.CoverageStatus));
                TableValue(table, PersianReportFormatting.FreshnessStatus(official.FreshnessStatus));
                TableValue(table, PersianReportFormatting.ConfidenceStatus(official.ConfidenceStatus));
            });
            column.Item().PaddingTop(4).Text(
                $"مبنای پوشش: {PersianReportFormatting.CoverageBasis(official.CoverageBasis)} | " +
                $"آخرین گزارش رسمی: {(official.LastApprovedReportDate.HasValue ? PersianReportFormatting.FormatDate(official.LastApprovedReportDate.Value) : "—")}")
                .FontSize(7.5f).FontColor("#53647B");
        });
    }

    private static void ComposeFactsAndFeatures(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "داده‌های رسمی و وضعیت پیکربندی حوزه‌ها"));
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2f);
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        TableHeader(header, "Fact رسمی");
                        TableHeader(header, "تعداد");
                    });
                    foreach (var item in model.FactCounts)
                    {
                        TableValue(table, item.Kind);
                        TableValue(table, PersianReportFormatting.ToPersianDigits(
                            item.Count.ToString(CultureInfo.InvariantCulture)));
                    }
                });
                row.ConstantItem(7);
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn(1.5f);
                    });
                    table.Header(header =>
                    {
                        TableHeader(header, "حوزه");
                        TableHeader(header, "وضعیت پیکربندی");
                    });
                    foreach (var item in model.FeatureStates)
                    {
                        TableValue(table, item.Domain);
                        TableValue(table, PersianReportFormatting.FeatureState(item.State));
                    }
                });
            });
            column.Item().PaddingTop(3).Text(
                "وضعیت حوزه‌ها فقط پیکربندی را نشان می‌دهد و معیار عملکرد یا سلامت آن حوزه نیست.")
                .FontSize(6.8f).FontColor("#684C20");
        });
    }

    private static void ComposeAttention(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        container.EnsureSpace(130).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "موارد توجه رسمی"));
            if (model.AttentionItems.Count == 0)
            {
                column.Item().Element(EmptyBox).Text(
                    "در Snapshot رسمی این تاریخ، مسئله یا توقفی در lineage رسمی ثبت نشده است.");
                return;
            }

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(3.2f);
                    columns.RelativeColumn(1.2f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "تاریخ");
                    TableHeader(header, "نوع");
                    TableHeader(header, "اولویت");
                    TableHeader(header, "اثر");
                    TableHeader(header, "سن");
                    TableHeader(header, "شرح");
                    TableHeader(header, "محل");
                });
                foreach (var item in model.AttentionItems)
                {
                    TableValue(table, PersianReportFormatting.FormatDate(item.ReportDate));
                    TableValue(table, PersianReportFormatting.AttentionKind(item.Kind));
                    TableValue(table, PersianReportFormatting.AttentionPriority(item.Priority));
                    TableValue(table, PersianReportFormatting.ObservedImpact(item.ObservedImpact));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        item.AgeDays.ToString(CultureInfo.InvariantCulture)));
                    TableValue(table, PersianReportFormatting.SafeText(
                        item.Description,
                        ExecutiveProjectStateReportRenderingContract.MaximumAttentionDescriptionLength));
                    TableValue(table, PersianReportFormatting.SafeText(
                        item.LocationName,
                        ExecutiveProjectStateReportRenderingContract.MaximumAttentionLocationLength));
                }
            });
        });
    }

    private static void ComposeTrend(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        container.EnsureSpace(115).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "روند رسمی — حداکثر ۱۴ تاریخ متمایز"));
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(0.8f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "تاریخ");
                    TableHeader(header, "عملیاتی");
                    TableHeader(header, "پوشش");
                    TableHeader(header, "تازگی");
                    TableHeader(header, "اعتماد");
                    TableHeader(header, "پوشش٪");
                    TableHeader(header, "توجه");
                });
                foreach (var item in model.Trend)
                {
                    TableValue(table, PersianReportFormatting.FormatDate(item.AsOfDate));
                    TableValue(table, PersianReportFormatting.OperationalStatus(item.OperationalStatus));
                    TableValue(table, PersianReportFormatting.CoverageStatus(item.CoverageStatus));
                    TableValue(table, PersianReportFormatting.FreshnessStatus(item.FreshnessStatus));
                    TableValue(table, PersianReportFormatting.ConfidenceStatus(item.ConfidenceStatus));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.CoveragePercent));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        (item.IssueCount + item.StoppageCount).ToString(CultureInfo.InvariantCulture)));
                }
            });
        });
    }

    private static void ComposeLineage(
        IContainer container,
        ExecutiveProjectStateReportRenderModel model)
    {
        var official = model.Snapshot.OfficialSnapshot!;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Lineage و تازگی Snapshot"));
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(112);
                    columns.RelativeColumn();
                });
                SummaryCell(table, "تاریخ Snapshot", header: true);
                SummaryCell(table, PersianReportFormatting.FormatDate(official.AsOfDate));
                SummaryCell(table, "زمان محاسبه", header: true);
                SummaryCell(table, PersianReportFormatting.FormatInstant(
                    official.CalculatedAt,
                    model.Snapshot.Project.TimeZone));
                SummaryCell(table, "نسخه محاسبه", header: true);
                SummaryCell(table, official.CalculationVersion);
                SummaryCell(table, "نسخه پروژه جاری", header: true);
                SummaryCell(table, model.Snapshot.Project.Revision.ToString(CultureInfo.InvariantCulture));
                SummaryCell(table, "نسخه پروژه Snapshot", header: true);
                SummaryCell(table, official.ProjectConfigurationRevision.ToString(CultureInfo.InvariantCulture));
                SummaryCell(table, "پیکربندی پروژه جاری", header: true);
                SummaryCell(table, PersianReportFormatting.YesNoUnknown(
                    model.Snapshot.Currency.ProjectConfigurationCurrent));
                SummaryCell(table, "منبع رسمی جاری", header: true);
                SummaryCell(table, PersianReportFormatting.YesNoUnknown(
                    model.Snapshot.Currency.ApprovedSourceCurrent));
                SummaryCell(table, "Snapshot ID", header: true);
                table.Cell().Element(SummaryValueCell).ContentFromLeftToRight()
                    .Text(official.SnapshotId.ToString()).FontSize(6.8f);
            });
        });
    }

    private static void ComposeFooter(
        IContainer container,
        ExecutiveProjectStateReportRenderRequest request)
    {
        container.BorderTop(1).BorderColor("#D1DBE8").PaddingTop(6).Row(row =>
        {
            row.RelativeItem().ContentFromLeftToRight()
                .DefaultTextStyle(style => style.FontSize(6.4f).FontColor("#5D6B80"))
                .Text(text =>
                {
                    text.Span(request.VerificationCode).SemiBold();
                    text.Span("  |  ");
                    text.Span(request.ManifestSha256[..12]);
                });
            row.AutoItem().DefaultTextStyle(style => style.FontSize(7).FontColor("#5D6B80"))
                .Text(text =>
                {
                    text.Span("صفحه ");
                    text.CurrentPageNumber();
                    text.Span(" از ");
                    text.TotalPages();
                });
        });
    }

    private static void Kpi(IContainer container, string label, int value) =>
        Kpi(container, label, value.ToString(CultureInfo.InvariantCulture));

    private static void Kpi(IContainer container, string label, string value)
    {
        container.Background("#F4F7FB").Border(0.7f).BorderColor("#C8D6E8")
            .PaddingVertical(7).PaddingHorizontal(4).AlignCenter().Column(column =>
            {
                column.Item().AlignCenter().Text(PersianReportFormatting.ToPersianDigits(value))
                    .FontSize(12.5f).Bold().FontColor("#0B4F8A");
                column.Item().AlignCenter().Text(label).FontSize(7).FontColor("#5D6B80");
            });
    }

    private static void SectionTitle(IContainer container, string title) => container
        .BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(3)
        .Text(title).FontSize(10).Bold().FontColor("#163F68");

    private static IContainer EmptyBox(IContainer container) => container
        .Background("#F7F8FA").Border(0.5f).BorderColor("#D5DCE5").Padding(7)
        .DefaultTextStyle(style => style.FontColor("#5E6675"));

    private static void SummaryCell(TableDescriptor table, string value, bool header = false) =>
        table.Cell().Element(header ? SummaryHeaderCell : SummaryValueCell).Text(value);

    private static IContainer SummaryHeaderCell(IContainer container) => container
        .Background("#EEF3F9").Border(0.5f).BorderColor("#C8D6E8").Padding(5);

    private static IContainer SummaryValueCell(IContainer container) => container
        .Border(0.5f).BorderColor("#C8D6E8").Padding(5);

    private static void TableHeader(TableCellDescriptor header, string value) => header.Cell()
        .Background("#DCE8F5").Border(0.5f).BorderColor("#AFC3DE").Padding(4)
        .AlignMiddle().Text(value).SemiBold().FontSize(7f);

    private static void TableHeaderCell(TableDescriptor table, string value) => table.Cell()
        .Background("#EEF3F9").Border(0.5f).BorderColor("#C8D6E8").Padding(4)
        .AlignMiddle().Text(value).SemiBold().FontSize(7f);

    private static void TableValue(TableDescriptor table, string value) => table.Cell()
        .BorderBottom(0.5f).BorderColor("#D7E0EB").Padding(4)
        .AlignMiddle().Text(value).FontSize(6.8f);

    private static string StatusMessage(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "Snapshot رسمی current با lineage کامل در زمان برش موجود است.",
        ReportDataStatus.NoData => "Snapshot رسمی واجد شرایط وجود ندارد یا داده رسمی قابل ارزیابی نیست.",
        ReportDataStatus.InsufficientData => "Snapshot رسمی وجود دارد، اما یک یا چند معیار کیفیت یا تازگی کافی نیست.",
        ReportDataStatus.NotConfigured => "منبع رسمی وضعیت پروژه برای گزارش‌دهی پیکربندی نشده است.",
        _ => "وضعیت داده قابل ارائه نیست."
    };

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

    private static ReportRenderingException UnsupportedFormat() => new(
        "reporting.format.unsupported",
        transient: false,
        "The Executive Project State PDF renderer received another output format.");
}
