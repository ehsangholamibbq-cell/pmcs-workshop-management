using System.Globalization;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectPeriodicReportPdfRenderer(
    ReportingRendererOptions options,
    ReportingExecutionOptions execution) : IProjectPeriodicReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ProjectPeriodicReportRenderRequest request)
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
                "reporting.periodic.renderer.pdf_failed",
                transient: false,
                "The certified project-periodic PDF could not be rendered.",
                exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(ProjectPeriodicReportRenderRequest request)
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
                "reporting.periodic.renderer.pdf_visual_failed",
                transient: false,
                "The project-periodic PDF qualification image could not be rendered.",
                exception);
        }
    }

    private ProjectPeriodicReportRenderModel Prepare(ProjectPeriodicReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ProjectPeriodicReportRenderModel.Create(request);
        if (model.Facts.Count > execution.MaximumPdfFacts)
        {
            throw new ReportRenderingException(
                "reporting.output.page_limit_exceeded",
                transient: false,
                "The certified project-periodic PDF fact limit was exceeded.");
        }
        return model;
    }

    private static Document CreateDocument(ProjectPeriodicReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.MarginHorizontal(26);
                page.MarginVertical(22);
                page.PageColor(Colors.White);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style
                    .FontFamily(CertifiedPdfRuntimeContract.FontFamily)
                    .FontSize(8.2f)
                    .FontColor("#172033"));
                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().PaddingVertical(10).Element(content => ComposeContent(content, model));
                page.Footer().Element(footer => ComposeFooter(footer, model.Request));
            });
        }).WithMetadata(new DocumentMetadata
        {
            Title = Title(model.Snapshot.Period.Kind),
            Author = "PMCS Certified Reporting",
            Subject = model.Request.VerificationCode,
            Keywords = $"PMCS,{model.Request.DefinitionCode},{model.Request.TemplateVersion}",
            Creator = "PMCS Certified Reporting",
            Producer = $"PMCS renderer {model.Request.RendererContractVersion}",
            Language = "fa-IR",
            CreationDate = model.Request.SourceCutoffUtc.UtcDateTime,
            ModifiedDate = model.Request.SourceCutoffUtc.UtcDateTime
        });

    private static void ComposeHeader(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        container.BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(Title(model.Snapshot.Period.Kind))
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

    private static void ComposeContent(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(9);
            column.Item().Element(summary => ComposeSummary(summary, model));
            column.Item().Element(status => ComposeStatus(status, model));

            if (HasMetrics(model.Snapshot.DataStatus))
            {
                column.Item().Element(kpis => ComposeKpis(kpis, model));
            }

            column.Item().Element(section => ComposeCoverage(section, model));
            column.Item().Element(section => ComposeAggregates(section, model));
            column.Item().Element(section => ComposeHighImpact(section, model));
            column.Item().Element(section => ComposeOfficialReports(section, model));
        });
    }

    private static void ComposeSummary(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(84);
                columns.RelativeColumn();
                columns.ConstantColumn(84);
                columns.RelativeColumn();
            });
            SummaryCell(table, "نوع دوره", header: true);
            SummaryCell(table, PersianReportFormatting.PeriodKind(snapshot.Period.Kind));
            SummaryCell(table, "بازه شمسی", header: true);
            SummaryCell(table,
                $"{PersianReportFormatting.FormatDate(snapshot.Period.StartLocalDate)} تا " +
                PersianReportFormatting.FormatDate(snapshot.Period.EndLocalDateExclusive.AddDays(-1)));
            SummaryCell(table, "زمان برش", header: true);
            SummaryCell(table, PersianReportFormatting.FormatInstant(
                model.Request.SourceCutoffUtc,
                snapshot.Project.TimeZone));
            SummaryCell(table, "دوره بسته", header: true);
            SummaryCell(table, PersianReportFormatting.YesNo(snapshot.Period.ClosedAtCutoff));
            SummaryCell(table, "کد راستی‌آزمایی", header: true);
            table.Cell().ColumnSpan(3).Element(SummaryValueCell)
                .ContentFromLeftToRight().Text(model.Request.VerificationCode).FontSize(7.6f).SemiBold();
        });
    }

    private static void ComposeStatus(IContainer container, ProjectPeriodicReportRenderModel model)
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

    private static void ComposeKpis(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(item => Kpi(item, "گزارش رسمی", model.OfficialReports.Count));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(item, "Fact رسمی", model.Facts.Count));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(
                item,
                "پوشش نوبت‌ها",
                model.Snapshot.Coverage.CoveredSlotCount.HasValue && model.Snapshot.Coverage.ExpectedSlotCount.HasValue
                    ? $"{model.Snapshot.Coverage.CoveredSlotCount}/{model.Snapshot.Coverage.ExpectedSlotCount}"
                    : "—"));
            row.ConstantItem(5);
            row.RelativeItem().Element(item => Kpi(item, "موارد مهم", model.HighImpactFacts.Count));
        });
    }

    private static void ComposeCoverage(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "پوشش دوره"));
            if (model.Snapshot.Coverage.ExpectedSlotCount is null)
            {
                column.Item().Element(EmptyBox)
                    .Text("پوشش مورد انتظار به علت نبود پیکربندی معتبر محاسبه نشده است.");
                return;
            }

            column.Item().PaddingTop(4).Text(text =>
            {
                text.Span("تاریخ‌های گزارش رسمی: ").SemiBold();
                text.Span(FormatDates(model.OfficialReportDates));
                text.Span("  |  تاریخ‌های فاقد پوشش: ").SemiBold();
                text.Span(FormatDates(model.MissingExpectedDates));
            });

            if (model.CoverageSlots.Count == 0)
            {
                column.Item().PaddingTop(4).Element(EmptyBox).Text("نوبت قابل ارزیابی تا زمان برش وجود ندارد.");
                return;
            }

            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2.3f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "نوبت");
                    TableHeader(header, "بازه");
                    TableHeader(header, "پوشش");
                    TableHeader(header, "تاریخ گزارش رسمی");
                });
                foreach (var slot in model.CoverageSlots)
                {
                    TableValue(table, PersianReportFormatting.FormatDate(slot.SlotDate));
                    TableValue(table,
                        $"{PersianReportFormatting.FormatDate(slot.StartLocalDate)} تا " +
                        PersianReportFormatting.FormatDate(slot.EndLocalDateExclusive.AddDays(-1)));
                    TableValue(table, slot.Covered ? "پوشش داده شد" : "فاقد پوشش");
                    TableValue(table, FormatDates(slot.OfficialReportDates.OrderBy(item => item)));
                }
            });
        });
    }

    private static void ComposeAggregates(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "جمع‌بندی داده‌های ساختاریافته"));
            if (!HasMetrics(model.Snapshot.DataStatus) || model.Facts.Count == 0)
            {
                column.Item().Element(EmptyBox)
                    .Text("برای این وضعیت، شاخص عددی یا جمع کل ساختگی تولید نشده است.");
                return;
            }

            column.Item().PaddingTop(4).Text("تعداد Fact برحسب نوع").SemiBold().FontColor("#294C71");
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                foreach (var item in model.FactCounts)
                {
                    TableHeaderCell(table, PersianReportFormatting.FactKind(item.Kind));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        item.Count.ToString(CultureInfo.InvariantCulture)));
                }
            });

            column.Item().PaddingTop(6).Text("مقادیر تجمیعی — هر واحد در سطر مستقل").SemiBold()
                .FontColor("#294C71");
            if (model.QuantityTotals.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text("مقدار قابل تجمیعی ثبت نشده است.");
            }
            else
            {
                column.Item().PaddingTop(3).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.2f);
                    });
                    table.Header(header =>
                    {
                        TableHeader(header, "نوع");
                        TableHeader(header, "مقدار");
                        TableHeader(header, "واحد منبع");
                        TableHeader(header, "وضعیت واحد");
                    });
                    foreach (var item in model.QuantityTotals)
                    {
                        TableValue(table, PersianReportFormatting.FactKind(item.Kind));
                        TableValue(table, PersianReportFormatting.FormatDecimal(item.Quantity));
                        TableValue(table, PersianReportFormatting.SafeText(item.SourceUnit, 80));
                        TableValue(table, PersianReportFormatting.UnitState(item.UnitState));
                    }
                });
            }

            column.Item().PaddingTop(6).Text("مشاهده منابع").SemiBold().FontColor("#294C71");
            if (model.ResourceObservationTotals.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text("مشاهده منبع قابل تجمیعی ثبت نشده است.");
            }
            else
            {
                column.Item().PaddingTop(3).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        TableHeader(header, "نوع");
                        TableHeader(header, "تعداد منبع");
                        TableHeader(header, "ساعت");
                    });
                    foreach (var item in model.ResourceObservationTotals)
                    {
                        TableValue(table, PersianReportFormatting.FactKind(item.Kind));
                        TableValue(table, item.ResourceCount.HasValue
                            ? PersianReportFormatting.ToPersianDigits(item.ResourceCount.Value.ToString(CultureInfo.InvariantCulture))
                            : "—");
                        TableValue(table, PersianReportFormatting.FormatDecimal(item.Hours));
                    }
                });
            }
        });
    }

    private static void ComposeHighImpact(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "مسائل و توقف‌های با اثر زیاد یا بحرانی"));
            if (model.HighImpactFacts.Count == 0)
            {
                column.Item().Element(EmptyBox).Text("مورد با اثر زیاد یا بحرانی در داده رسمی این دوره ثبت نشده است.");
                return;
            }

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(3.5f);
                    columns.RelativeColumn(1.2f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "تاریخ");
                    TableHeader(header, "نوع");
                    TableHeader(header, "اثر");
                    TableHeader(header, "شرح");
                    TableHeader(header, "مرجع");
                });
                foreach (var item in model.HighImpactFacts)
                {
                    TableValue(table, PersianReportFormatting.FormatDate(item.ReportDate));
                    TableValue(table, PersianReportFormatting.FactKind(item.Fact.Kind));
                    TableValue(table, PersianReportFormatting.Impact(item.Fact.ImpactLevel));
                    TableValue(table, PersianReportFormatting.SafeText(item.Fact.Description, 800));
                    TableValue(table, PersianReportFormatting.SafeText(item.Fact.ReferenceCode, 100));
                }
            });
        });
    }

    private static void ComposeOfficialReports(IContainer container, ProjectPeriodicReportRenderModel model)
    {
        container.EnsureSpace(170).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "گزارش‌های روزانه رسمی دوره"));
            if (model.OfficialReports.Count == 0)
            {
                column.Item().Element(EmptyBox)
                    .Text("تا زمان برش، گزارش روزانه رسمی قابل ارائه‌ای در این دوره وجود ندارد.");
                return;
            }

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(3.4f);
                    columns.RelativeColumn(0.8f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "تاریخ");
                    TableHeader(header, "نسخه");
                    TableHeader(header, "طبقه‌بندی");
                    TableHeader(header, "محل");
                    TableHeader(header, "شرح روزانه");
                    TableHeader(header, "Fact");
                });
                foreach (var report in model.OfficialReports)
                {
                    TableValue(table, PersianReportFormatting.FormatDate(report.Version.ReportDate));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        report.Version.VersionNumber.ToString(CultureInfo.InvariantCulture)));
                    TableValue(table, PersianReportFormatting.Classification(report.Classification));
                    TableValue(table, PersianReportFormatting.SafeText(report.Version.LocationName, 180));
                    TableValue(table, PersianReportFormatting.SafeText(report.Version.Narrative, 800));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        report.Version.Facts.Count.ToString(CultureInfo.InvariantCulture)));
                }
            });

            column.Item().PaddingTop(6).Text("جزئیات Factهای رسمی").SemiBold().FontColor("#294C71");
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(3.4f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(0.9f);
                    columns.RelativeColumn(0.9f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "تاریخ");
                    TableHeader(header, "نوع");
                    TableHeader(header, "محل");
                    TableHeader(header, "شرح");
                    TableHeader(header, "مقدار");
                    TableHeader(header, "واحد");
                    TableHeader(header, "اثر");
                });
                foreach (var item in model.Facts)
                {
                    TableValue(table, PersianReportFormatting.FormatDate(item.Version.ReportDate));
                    TableValue(table, PersianReportFormatting.FactKind(item.Fact.Kind));
                    TableValue(table, PersianReportFormatting.SafeText(
                        item.Fact.LocationName ?? item.Version.LocationName,
                        180));
                    TableValue(table, PersianReportFormatting.SafeText(item.Fact.Description, 1_000));
                    TableValue(table, PersianReportFormatting.FormatDecimal(item.Fact.Quantity));
                    TableValue(table, PersianReportFormatting.SafeText(item.Fact.Unit, 80));
                    TableValue(table, PersianReportFormatting.Impact(item.Fact.ImpactLevel));
                }
            });

            column.Item().PaddingTop(6).Text("ردیابی نسخه‌های رسمی").SemiBold().FontColor("#294C71");
            foreach (var report in model.OfficialReports)
            {
                var version = report.Version;
                column.Item().PaddingTop(2).ContentFromLeftToRight()
                    .Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(6.1f).FontColor("#748198"));
                        text.Span($"{version.ReportDate:yyyy-MM-dd} | Root: {version.RootReportId} | Report: {version.ReportId} | ");
                        text.Span($"Supersedes: {version.SupersedesReportId?.ToString() ?? "—"} | Revision: {version.Revision}");
                    });
                if (!string.IsNullOrWhiteSpace(version.CorrectionReason))
                {
                    column.Item().PaddingTop(1).Text(
                        $"علت اصلاح: {PersianReportFormatting.SafeText(version.CorrectionReason, 500)}")
                        .FontSize(6.5f).FontColor("#684C20");
                }
            }
        });
    }

    private static void ComposeFooter(IContainer container, ProjectPeriodicReportRenderRequest request)
    {
        container.BorderTop(1).BorderColor("#D1DBE8").PaddingTop(6).Row(row =>
        {
            row.RelativeItem()
                .ContentFromLeftToRight()
                .DefaultTextStyle(style => style.FontSize(6.4f).FontColor("#5D6B80"))
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

    private static void Kpi(IContainer container, string label, int value) =>
        Kpi(container, label, value.ToString(CultureInfo.InvariantCulture));

    private static void Kpi(IContainer container, string label, string value)
    {
        container.Background("#F4F7FB").Border(0.7f).BorderColor("#C8D6E8")
            .PaddingVertical(7).PaddingHorizontal(4).AlignCenter().Column(column =>
            {
                column.Item().AlignCenter().Text(PersianReportFormatting.ToPersianDigits(value))
                    .FontSize(13).Bold().FontColor("#0B4F8A");
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
        .AlignMiddle().Text(value).SemiBold().FontSize(7.2f);

    private static void TableHeaderCell(TableDescriptor table, string value) => table.Cell()
        .Background("#EEF3F9").Border(0.5f).BorderColor("#C8D6E8").Padding(4)
        .AlignMiddle().Text(value).SemiBold().FontSize(7.2f);

    private static void TableValue(TableDescriptor table, string value) => table.Cell()
        .BorderBottom(0.5f).BorderColor("#D7E0EB").Padding(4)
        .AlignMiddle().Text(value).FontSize(7f);

    private static bool HasMetrics(ReportDataStatus status) =>
        status is ReportDataStatus.Available or ReportDataStatus.InsufficientData;

    private static string FormatDates(IEnumerable<DateOnly> dates)
    {
        var values = dates.Select(item => PersianReportFormatting.FormatDate(item)).ToArray();
        return values.Length == 0 ? "—" : string.Join("، ", values);
    }

    private static string Title(ProjectReportPeriodKind kind) =>
        $"گزارش {PersianReportFormatting.PeriodKind(kind)} رسمی پروژه";

    private static string StatusMessage(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "پوشش و داده رسمی دوره برای ارائه کامل گزارش کافی است.",
        ReportDataStatus.NoData => "تا زمان برش، نسخه رسمی تأییدشده‌ای برای این دوره وجود ندارد.",
        ReportDataStatus.InsufficientData => "داده رسمی موجود است، اما پوشش یا محتوای ساختاریافته کامل نیست.",
        ReportDataStatus.NotConfigured => "پیکربندی لازم برای محاسبه قابل اتکای گزارش دوره‌ای کامل نیست.",
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
        "The project-periodic PDF renderer received another output format.");
}
