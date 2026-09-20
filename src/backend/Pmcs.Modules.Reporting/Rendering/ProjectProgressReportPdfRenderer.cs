using System.Globalization;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectProgressReportPdfRenderer(
    ReportingRendererOptions options,
    ReportingExecutionOptions execution) : IProjectProgressReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ProjectProgressReportRenderRequest request)
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
                "reporting.project_progress.renderer.pdf_failed",
                transient: false,
                "The certified Project Progress PDF could not be rendered.",
                exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(ProjectProgressReportRenderRequest request)
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
                "reporting.project_progress.renderer.pdf_visual_failed",
                transient: false,
                "The Project Progress PDF qualification image could not be rendered.",
                exception);
        }
    }

    private ProjectProgressReportRenderModel Prepare(ProjectProgressReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ProjectProgressReportRenderModel.Create(request);
        if (model.Entries.Count + model.Milestones.Count + model.Curve.Count > execution.MaximumPdfFacts)
        {
            throw new ReportRenderingException(
                "reporting.output.page_limit_exceeded",
                transient: false,
                "The certified Project Progress PDF row limit was exceeded.");
        }
        return model;
    }

    private static Document CreateDocument(ProjectProgressReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page => ConfigurePage(
                page,
                model,
                "خلاصه و Baseline رسمی",
                content => ComposeOverview(content, model)));
            container.Page(page => ConfigurePage(
                page,
                model,
                "منحنی S و Lineage",
                content => ComposeCurveAndLineage(content, model)));
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش رسمی پیشرفت فیزیکی و منحنی S",
            Author = "PMCS Certified Reporting",
            Subject = model.Request.VerificationCode,
            Keywords = $"PMCS,{model.Request.DefinitionCode},{model.Request.TemplateVersion}",
            Creator = "PMCS Certified Reporting",
            Producer = $"PMCS renderer {model.Request.RendererContractVersion}",
            Language = "fa-IR",
            CreationDate = model.Request.SourceCutoffUtc.UtcDateTime,
            ModifiedDate = model.Request.SourceCutoffUtc.UtcDateTime
        });

    private static void ConfigurePage(
        PageDescriptor page,
        ProjectProgressReportRenderModel model,
        string section,
        Action<IContainer> content)
    {
        page.Size(PageSizes.A4.Landscape());
        page.MarginHorizontal(22);
        page.MarginVertical(18);
        page.PageColor(Colors.White);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(style => style
            .FontFamily(CertifiedPdfRuntimeContract.FontFamily)
            .FontSize(7.8f)
            .FontColor("#172033"));
        page.Header().Element(header => ComposeHeader(header, model, section));
        page.Content().PaddingVertical(8).Element(content);
        page.Footer().Element(footer => ComposeFooter(footer, model.Request));
    }

    private static void ComposeHeader(
        IContainer container,
        ProjectProgressReportRenderModel model,
        string section)
    {
        container.BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(7).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("گزارش رسمی پیشرفت فیزیکی و منحنی S")
                    .FontSize(14).Bold().FontColor("#0B4F8A");
                column.Item().Text(
                    $"{PersianReportFormatting.SafeText(model.Snapshot.Project.Name, 180)} — " +
                    PersianReportFormatting.SafeText(model.Snapshot.Project.Code, 80))
                    .FontSize(8.2f).FontColor("#46556D");
                column.Item().Text(section).FontSize(7).FontColor("#62728A");
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

    private static void ComposeOverview(IContainer container, ProjectProgressReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(summary => ComposeIdentity(summary, model));
            column.Item().Element(status => ComposeDataStatus(status, model));
            column.Item().Element(kpis => ComposeKpis(kpis, model));
            column.Item().Element(configuration => ComposeConfiguration(configuration, model));
            column.Item().Element(baseline => ComposeBaseline(baseline, model));
            if (model.Snapshot.Baseline is null)
            {
                column.Item().Element(EmptyBox).Text(
                    "تا زمان برش، Baseline رسمی واجد شرایط وجود ندارد؛ هیچ درصد یا منحنی ساختگی تولید نشده است.");
                return;
            }
            column.Item().Element(entries => ComposeEntries(entries, model));
            column.Item().Element(milestones => ComposeMilestones(milestones, model));
        });
    }

    private static void ComposeIdentity(IContainer container, ProjectProgressReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(82);
                columns.RelativeColumn();
                columns.ConstantColumn(82);
                columns.RelativeColumn();
                columns.ConstantColumn(82);
                columns.RelativeColumn();
            });
            SummaryCell(table, "تاریخ برش شمسی", header: true);
            SummaryCell(table, PersianReportFormatting.FormatDate(snapshot.Cutoff.CutoffLocalDate));
            SummaryCell(table, "زمان برش محلی", header: true);
            SummaryCell(table, PersianReportFormatting.FormatInstant(
                snapshot.Cutoff.SourceCutoffUtc,
                snapshot.Project.TimeZone));
            SummaryCell(table, "طبقه‌بندی", header: true);
            SummaryCell(table, PersianReportFormatting.Classification(snapshot.Classification));
            SummaryCell(table, "کد راستی‌آزمایی", header: true);
            table.Cell().ColumnSpan(5).Element(SummaryValueCell).ContentFromLeftToRight()
                .Text(model.Request.VerificationCode).FontSize(7.2f).SemiBold();
        });
    }

    private static void ComposeDataStatus(IContainer container, ProjectProgressReportRenderModel model)
    {
        container.Background(StatusColor(model.Snapshot.DataStatus))
            .Border(1).BorderColor(StatusBorderColor(model.Snapshot.DataStatus))
            .Padding(7).Column(column =>
            {
                column.Item().Text(PersianReportFormatting.DataStatus(model.Snapshot.DataStatus))
                    .Bold().FontSize(9.5f);
                column.Item().PaddingTop(2).Text(StatusMessage(model.Snapshot.DataStatus))
                    .FontColor("#4C596C");
                if (model.ReasonCodes.Count > 0)
                {
                    column.Item().PaddingTop(3).Text(
                        string.Join("؛ ", model.ReasonCodes.Select(PersianReportFormatting.ReasonCode)))
                        .FontSize(7.3f).FontColor("#684C20");
                }
            });
    }

    private static void ComposeKpis(IContainer container, ProjectProgressReportRenderModel model)
    {
        var summary = model.Snapshot.Summary;
        container.Row(row =>
        {
            row.RelativeItem().Element(item => Kpi(item, "Actual رسمی", Percent(summary?.ActualPercent)));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(item, "Planned رسمی", Percent(summary?.PlannedPercent)));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(item, "Variance", PercentagePoint(summary?.VariancePercent)));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(
                item,
                "Actual ناقص",
                summary?.MissingActualEntryCount.ToString(CultureInfo.InvariantCulture) ?? "—"));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(
                item,
                "شواهد خارج Baseline",
                model.Snapshot.ApprovedProgressOutsideBaselineCount.ToString(CultureInfo.InvariantCulture)));
        });
    }

    private static void ComposeConfiguration(IContainer container, ProjectProgressReportRenderModel model)
    {
        var configuration = model.Snapshot.Configuration;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "پیکربندی مؤثر در زمان برش"));
            if (configuration is null)
            {
                column.Item().Element(EmptyBox).Text("پیکربندی مؤثر و قابل اثبات وجود ندارد.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                TableHeaderCell(table, "نسخه");
                TableHeaderCell(table, "حالت برنامه‌ریزی");
                TableHeaderCell(table, "گزارش فعال");
                TableHeaderCell(table, "تقویم");
                TableHeaderCell(table, "مبنای محاسبه");
                TableHeaderCell(table, "نسخه تقویم");
                TableValue(table, configuration.ConfigurationVersion.ToString(CultureInfo.InvariantCulture));
                TableValue(table, PersianReportFormatting.PlanningMode(configuration.PlanningMode));
                TableValue(table, PersianReportFormatting.YesNo(configuration.ProgressReportingEnabled));
                TableValue(table, PersianReportFormatting.CalendarState(configuration.CalendarState));
                TableValue(table, PersianReportFormatting.CalendarBasis(model.Snapshot.CalendarBasis));
                TableValue(table, configuration.CalendarRevision.ToString(CultureInfo.InvariantCulture));
            });
        });
    }

    private static void ComposeBaseline(IContainer container, ProjectProgressReportRenderModel model)
    {
        var baseline = model.Snapshot.Baseline;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Baseline رسمی مؤثر"));
            if (baseline is null)
            {
                column.Item().Element(EmptyBox).Text("Baseline رسمی واجد شرایط موجود نیست.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.4f);
                });
                TableHeaderCell(table, "نسخه");
                TableHeaderCell(table, "عنوان");
                TableHeaderCell(table, "نوع");
                TableHeaderCell(table, "Revision تصویب");
                TableHeaderCell(table, "زمان تصویب");
                TableValue(table, baseline.VersionCode);
                TableValue(table, PersianReportFormatting.SafeText(
                    baseline.Title,
                    ProjectProgressReportRenderingContract.MaximumEntryTitleLength));
                TableValue(table, PersianReportFormatting.BaselineKind(baseline.Kind));
                TableValue(table, baseline.ApprovalRevision.ToString(CultureInfo.InvariantCulture));
                TableValue(table, PersianReportFormatting.FormatInstant(
                    baseline.ApprovedAt,
                    model.Snapshot.Project.TimeZone));
            });
        });
    }

    private static void ComposeEntries(IContainer container, ProjectProgressReportRenderModel model)
    {
        container.EnsureSpace(105).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "ردیف‌های Baseline و پیشرفت در زمان برش"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn(2.3f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(0.75f);
                    columns.RelativeColumn(0.75f);
                    columns.RelativeColumn(0.75f);
                    columns.RelativeColumn(0.85f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "کد");
                    TableHeader(header, "عنوان");
                    TableHeader(header, "نوع");
                    TableHeader(header, "روش");
                    TableHeader(header, "وزن٪");
                    TableHeader(header, "Actual٪");
                    TableHeader(header, "Planned٪");
                    TableHeader(header, "Variance");
                });
                foreach (var item in model.Entries)
                {
                    TableValue(table, item.Code);
                    TableValue(table, PersianReportFormatting.SafeText(
                        item.Title,
                        ProjectProgressReportRenderingContract.MaximumEntryTitleLength));
                    TableValue(table, PersianReportFormatting.EntryKind(item.Kind));
                    TableValue(table, PersianReportFormatting.MeasurementMethod(item.MeasurementMethod));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.WeightPercent));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.ActualPercent));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.PlannedPercent));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.VariancePercent));
                }
            });
        });
    }

    private static void ComposeMilestones(IContainer container, ProjectProgressReportRenderModel model)
    {
        if (model.Milestones.Count == 0)
        {
            return;
        }
        container.EnsureSpace(85).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "مایلستون‌های رسمی"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    TableHeader(header, "کد");
                    TableHeader(header, "عنوان");
                    TableHeader(header, "تاریخ برنامه");
                    TableHeader(header, "آخرین وضعیت");
                    TableHeader(header, "پیشرفت رسمی٪");
                });
                foreach (var item in model.Milestones)
                {
                    TableValue(table, item.Code);
                    TableValue(table, item.Title);
                    TableValue(table, PersianReportFormatting.FormatDate(item.PlannedDate));
                    TableValue(table, item.LatestApprovedStatusDate.HasValue
                        ? PersianReportFormatting.FormatDate(item.LatestApprovedStatusDate.Value)
                        : "—");
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.ApprovedProgressPercent));
                }
            });
        });
    }

    private static void ComposeCurveAndLineage(IContainer container, ProjectProgressReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(sampling => ComposeSampling(sampling, model));
            column.Item().Element(curve => ComposeCurve(curve, model));
            column.Item().Element(lineage => ComposeLineage(lineage, model));
            column.Item().Background("#FFF4E5").Border(0.8f).BorderColor("#E5C17A").Padding(6)
                .Text("نقاط پس از تاریخ برش فقط Planned را نمایش می‌دهند؛ Actual و Variance آن‌ها خالی است و مقدار آینده ساخته نمی‌شود.")
                .FontSize(7.1f).FontColor("#684C20");
        });
    }

    private static void ComposeSampling(IContainer container, ProjectProgressReportRenderModel model)
    {
        var sampling = model.Snapshot.Sampling;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "قرارداد نمونه‌برداری منحنی S"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.5f);
                });
                TableHeaderCell(table, "روش");
                TableHeaderCell(table, "نمونه‌برداری");
                TableHeaderCell(table, "نقاط پایه");
                TableHeaderCell(table, "نقاط خروجی");
                TableHeaderCell(table, "وضعیت Curve");
                TableValue(table, PersianReportFormatting.SamplingKind(sampling.Kind));
                TableValue(table, PersianReportFormatting.YesNo(sampling.IsSampled));
                TableValue(table, sampling.BaseGridPointCount.ToString(CultureInfo.InvariantCulture));
                TableValue(table, sampling.PointCount.ToString(CultureInfo.InvariantCulture));
                TableValue(table, PersianReportFormatting.MetricStatus(model.Snapshot.CurveStatus));
            });
        });
    }

    private static void ComposeCurve(IContainer container, ProjectProgressReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "نقاط قطعی منحنی S"));
            if (model.Curve.Count == 0)
            {
                column.Item().Element(EmptyBox).Text(
                    "برای این Baseline منحنی زمان‌دار پیکربندی نشده است؛ آرایه خالی به معنی صفر نیست.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.25f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "تاریخ شمسی");
                    TableHeader(header, "Planned٪");
                    TableHeader(header, "Actual٪");
                    TableHeader(header, "Variance");
                    TableHeader(header, "Actual ناقص");
                });
                foreach (var item in model.Curve)
                {
                    TableValue(table, PersianReportFormatting.FormatDate(item.PointDate));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.PlannedPercent));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.ActualPercent));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.VariancePercent));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        item.MissingActualEntryCount.ToString(CultureInfo.InvariantCulture)));
                }
            });
        });
    }

    private static void ComposeLineage(IContainer container, ProjectProgressReportRenderModel model)
    {
        container.EnsureSpace(105).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Lineage و قواعد خواندن خروجی"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(122);
                    columns.RelativeColumn();
                });
                SummaryCell(table, "Snapshot schema", header: true);
                SummaryCell(table, model.Snapshot.SchemaVersion);
                SummaryCell(table, "Source Manifest SHA-256", header: true);
                table.Cell().Element(SummaryValueCell).ContentFromLeftToRight()
                    .Text(model.Snapshot.SourceManifestSha256).FontSize(6.5f);
                SummaryCell(table, "آخرین تغییر منبع", header: true);
                SummaryCell(table, model.Snapshot.SourceMaxChangedAt.HasValue
                    ? PersianReportFormatting.FormatInstant(
                        model.Snapshot.SourceMaxChangedAt.Value,
                        model.Snapshot.Project.TimeZone)
                    : "—");
                SummaryCell(table, "علامت Variance", header: true);
                SummaryCell(table, "Actual - Planned؛ مثبت یعنی جلوتر و منفی یعنی عقب‌تر از برنامه");
                SummaryCell(table, "شواهد خارج Baseline", header: true);
                SummaryCell(table, model.Snapshot.ApprovedProgressOutsideBaselineCount.ToString(CultureInfo.InvariantCulture));
            });
        });
    }

    private static void ComposeFooter(IContainer container, ProjectProgressReportRenderRequest request)
    {
        container.BorderTop(1).BorderColor("#D1DBE8").PaddingTop(5).Row(row =>
        {
            row.RelativeItem().ContentFromLeftToRight()
                .DefaultTextStyle(style => style.FontSize(6.2f).FontColor("#5D6B80"))
                .Text(text =>
                {
                    text.Span(request.VerificationCode).SemiBold();
                    text.Span("  |  ");
                    text.Span(request.ManifestSha256[..12]);
                });
            row.AutoItem().DefaultTextStyle(style => style.FontSize(6.8f).FontColor("#5D6B80"))
                .Text(text =>
                {
                    text.Span("صفحه ");
                    text.CurrentPageNumber();
                    text.Span(" از ");
                    text.TotalPages();
                });
        });
    }

    private static string Percent(decimal? value) => value.HasValue
        ? $"{PersianReportFormatting.FormatDecimal(value)}٪"
        : "—";

    private static string PercentagePoint(decimal? value) => value.HasValue
        ? $"{PersianReportFormatting.FormatDecimal(value)} واحد درصد"
        : "—";

    private static void Kpi(IContainer container, string label, string value)
    {
        container.Background("#F4F7FB").Border(0.7f).BorderColor("#C8D6E8")
            .PaddingVertical(6).PaddingHorizontal(3).AlignCenter().Column(column =>
            {
                column.Item().AlignCenter().Text(PersianReportFormatting.ToPersianDigits(value))
                    .FontSize(11.5f).Bold().FontColor("#0B4F8A");
                column.Item().AlignCenter().Text(label).FontSize(6.8f).FontColor("#5D6B80");
            });
    }

    private static void SectionTitle(IContainer container, string title) => container
        .BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(3)
        .Text(title).FontSize(9.5f).Bold().FontColor("#163F68");

    private static IContainer EmptyBox(IContainer container) => container
        .Background("#F7F8FA").Border(0.5f).BorderColor("#D5DCE5").Padding(6)
        .DefaultTextStyle(style => style.FontColor("#5E6675"));

    private static void SummaryCell(TableDescriptor table, string value, bool header = false) =>
        table.Cell().Element(header ? SummaryHeaderCell : SummaryValueCell).Text(value);

    private static IContainer SummaryHeaderCell(IContainer container) => container
        .Background("#EEF3F9").Border(0.5f).BorderColor("#C8D6E8").Padding(4);

    private static IContainer SummaryValueCell(IContainer container) => container
        .Border(0.5f).BorderColor("#C8D6E8").Padding(4);

    private static void TableHeader(TableCellDescriptor header, string value) => header.Cell()
        .Background("#DCE8F5").Border(0.5f).BorderColor("#AFC3DE").Padding(3)
        .AlignMiddle().Text(value).SemiBold().FontSize(6.7f);

    private static void TableHeaderCell(TableDescriptor table, string value) => table.Cell()
        .Background("#EEF3F9").Border(0.5f).BorderColor("#C8D6E8").Padding(3)
        .AlignMiddle().Text(value).SemiBold().FontSize(6.7f);

    private static void TableValue(TableDescriptor table, string value) => table.Cell()
        .BorderBottom(0.5f).BorderColor("#D7E0EB").Padding(3)
        .AlignMiddle().Text(value).FontSize(6.5f);

    private static string StatusMessage(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "Baseline و Actual رسمی برای همه ردیف‌های وزن‌دار در زمان برش موجود است.",
        ReportDataStatus.NoData => "پیکربندی وجود دارد، اما Baseline رسمی واجد شرایط پیدا نشد.",
        ReportDataStatus.InsufficientData => "Baseline رسمی وجود دارد، اما Actual رسمی یا سازگاری منبع کامل نیست.",
        ReportDataStatus.NotConfigured => "گزارش‌دهی پیشرفت یا حالت برنامه‌ریزی برای این تاریخ پیکربندی نشده است.",
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
        "The Project Progress PDF renderer received another output format.");
}
