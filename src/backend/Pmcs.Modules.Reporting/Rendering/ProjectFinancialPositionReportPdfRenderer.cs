using System.Globalization;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectFinancialPositionReportPdfRenderer(
    ReportingRendererOptions options,
    ReportingExecutionOptions execution) : IProjectFinancialPositionReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(ProjectFinancialPositionReportRenderRequest request)
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
                "reporting.project_financial_position.renderer.pdf_failed",
                transient: false,
                "The certified Project Financial Position PDF could not be rendered.",
                exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(
        ProjectFinancialPositionReportRenderRequest request)
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
                "reporting.project_financial_position.renderer.pdf_visual_failed",
                transient: false,
                "The Project Financial Position PDF qualification image could not be rendered.",
                exception);
        }
    }

    private ProjectFinancialPositionReportRenderModel Prepare(
        ProjectFinancialPositionReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ProjectFinancialPositionReportRenderModel.Create(request);
        if (model.OpenObligations.Count + model.Aging.Count > execution.MaximumPdfFacts)
        {
            throw new ReportRenderingException(
                "reporting.output.page_limit_exceeded",
                transient: false,
                "The certified Project Financial Position PDF row limit was exceeded.");
        }
        return model;
    }

    private static Document CreateDocument(ProjectFinancialPositionReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page => ConfigurePage(
                page,
                model,
                "Cash و Budget رسمی",
                content => ComposeOverview(content, model)));
            container.Page(page => ConfigurePage(
                page,
                model,
                "تعهدات، Aging و Lineage",
                content => ComposeObligationsAndLineage(content, model)));
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش رسمی وضعیت مالی پروژه",
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
        ProjectFinancialPositionReportRenderModel model,
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
        ProjectFinancialPositionReportRenderModel model,
        string section)
    {
        container.BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(7).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("گزارش رسمی وضعیت مالی پروژه")
                    .FontSize(14).Bold().FontColor("#0B4F8A");
                column.Item().Text(
                    $"{PersianReportFormatting.SafeText(model.Snapshot.Project.Name, 180)} — " +
                    PersianReportFormatting.SafeText(model.Snapshot.Project.Code, 80))
                    .FontSize(8.2f).FontColor("#46556D");
                column.Item().Text(section).FontSize(7).FontColor("#62728A");
            });
            row.ConstantItem(90).AlignLeft().Column(column =>
            {
                column.Item().AlignCenter().Text("PMCS").FontSize(15).Bold().FontColor("#0B4F8A");
                column.Item().AlignCenter().Text(
                    $"قالب {PersianReportFormatting.ToPersianDigits(model.Request.TemplateVersion)}")
                    .FontSize(7).FontColor("#62728A");
            });
        });
    }

    private static void ComposeOverview(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(identity => ComposeIdentity(identity, model));
            column.Item().Element(status => ComposeDataStatus(status, model));
            column.Item().Element(cash => ComposeCash(cash, model));
            column.Item().Element(budget => ComposeBudget(budget, model));
            column.Item().Element(summaries => ComposeSummaries(summaries, model));
            column.Item().Background("#FFF4E5").Border(0.8f).BorderColor("#E5C17A")
                .Padding(6).Text(
                    "تمام مبالغ فقط در ارز پایه ثبت‌شده نمایش داده می‌شوند؛ مقدار مفقود صفر نیست و هیچ تسعیر ارز، تهاتر، پیش‌بینی یا امتیاز سلامت ساخته نمی‌شود.")
                .FontSize(7.1f).FontColor("#684C20");
        });
    }

    private static void ComposeIdentity(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
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
            SummaryCell(table, "ارز پایه", header: true);
            SummaryCell(table, snapshot.Project.CapturedBaseCurrencyCode);
            SummaryCell(table, "طبقه‌بندی", header: true);
            SummaryCell(table, PersianReportFormatting.Classification(snapshot.Classification));
            SummaryCell(table, "کد راستی‌آزمایی", header: true);
            table.Cell().ColumnSpan(3).Element(SummaryValueCell).ContentFromLeftToRight()
                .Text(model.Request.VerificationCode).FontSize(7.2f).SemiBold();
        });
    }

    private static void ComposeDataStatus(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        container.Background(StatusColor(model.Snapshot.DataStatus))
            .Border(1).BorderColor(StatusBorderColor(model.Snapshot.DataStatus))
            .Padding(7).Column(column =>
            {
                column.Item().Text(PersianReportFormatting.DataStatus(model.Snapshot.DataStatus))
                    .Bold().FontSize(9.5f);
                column.Item().PaddingTop(2).Text(StatusMessage(model.Snapshot.DataStatus))
                    .FontColor("#4C596C");
                column.Item().PaddingTop(3).Row(row =>
                {
                    StatusChip(row, "Cash", model.Snapshot.CashStatus);
                    StatusChip(row, "تعهدات", model.Snapshot.ObligationStatus);
                    StatusChip(row, "Budget", model.Snapshot.BudgetStatus);
                    StatusChip(row, "مقایسه Budget", model.Snapshot.BudgetComparisonStatus);
                });
                if (model.ReasonCodes.Count > 0)
                {
                    column.Item().PaddingTop(3).Text(
                        string.Join("؛ ", model.ReasonCodes.Select(ReasonCode)))
                        .FontSize(7.3f).FontColor("#684C20");
                }
            });
    }

    private static void ComposeCash(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        var cash = model.Snapshot.Cash;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Cash Position رسمی"));
            if (model.Snapshot.CashStatus != ProjectFinancialPositionSectionStatus.Available)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "Cash رسمی قابل محاسبه نیست؛ metricهای این بخش عمداً خالی مانده‌اند.");
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
                    columns.RelativeColumn();
                });
                TableHeaderCell(table, "دریافت");
                TableHeaderCell(table, "پرداخت مستقیم");
                TableHeaderCell(table, "تأمین تنخواه");
                TableHeaderCell(table, "هزینه تنخواه");
                TableHeaderCell(table, "جریان بیرونی خالص");
                TableHeaderCell(table, "هزینه شناسایی‌شده");
                TableHeaderCell(table, "مانده تنخواه");
                TableValue(table, Money(cash.TotalReceipts));
                TableValue(table, Money(cash.DirectPayments));
                TableValue(table, Money(cash.PettyCashFunding));
                TableValue(table, Money(cash.PettyCashExpenses));
                TableValue(table, Money(cash.ExternalNetCash));
                TableValue(table, Money(cash.RecognizedSpend));
                TableValue(table, Money(cash.PettyCashBalance));
            });
        });
    }

    private static void ComposeBudget(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        var budget = snapshot.Budget;
        var comparison = snapshot.BudgetComparison;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Budget Baseline رسمی و مقایسه"));
            if (budget is null || comparison is null)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    $"Budget رسمی قابل نمایش نیست؛ وضعیت: {SectionStatus(snapshot.BudgetStatus)}.");
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
                TableHeaderCell(table, "Revision");
                TableHeaderCell(table, "مبلغ مصوب");
                TableHeaderCell(table, "زمان تصویب");
                TableHeaderCell(table, "مانده Budget");
                TableHeaderCell(table, "مصرف٪");
                TableHeaderCell(table, "وضعیت مقایسه");
                TableValue(table, budget.Revision.ToString(CultureInfo.InvariantCulture));
                TableValue(table, Money(comparison.ApprovedBudgetAmount));
                TableValue(table, PersianReportFormatting.FormatInstant(
                    budget.ApprovedAt,
                    snapshot.Project.TimeZone));
                TableValue(table, Money(comparison.BudgetRemainingAmount));
                TableValue(table, Percent(comparison.BudgetConsumedPercent));
                TableValue(table, SectionStatus(snapshot.BudgetComparisonStatus));
            });
        });
    }

    private static void ComposeSummaries(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        var summaries = new[]
        {
            model.Snapshot.PayableSummary,
            model.Snapshot.ReceivableSummary
        }.Where(item => item is not null).Cast<ProjectFinancialPositionReportObligationSummary>()
            .ToArray();
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "خلاصه مستقل تعهدات"));
            if (summaries.Length == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "خلاصه تعهد رسمی قابل محاسبه نیست؛ پرداختنی و دریافتنی با هم تهاتر نشده‌اند.");
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
                });
                table.Header(header =>
                {
                    TableHeader(header, "نوع");
                    TableHeader(header, "تعداد باز");
                    TableHeader(header, "مبلغ باز");
                    TableHeader(header, "تعداد سررسیدگذشته");
                    TableHeader(header, "مبلغ سررسیدگذشته");
                });
                foreach (var summary in summaries)
                {
                    TableValue(table, ObligationType(summary.Type));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        summary.OpenCount.ToString(CultureInfo.InvariantCulture)));
                    TableValue(table, Money(summary.OpenAmount));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        summary.OverdueCount.ToString(CultureInfo.InvariantCulture)));
                    TableValue(table, Money(summary.OverdueAmount));
                }
            });
        });
    }

    private static void ComposeObligationsAndLineage(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(aging => ComposeAging(aging, model));
            column.Item().Element(rows => ComposeOpenObligations(rows, model));
            column.Item().Element(counts => ComposeSourceCounts(counts, model));
            column.Item().Element(lineage => ComposeLineage(lineage, model));
        });
    }

    private static void ComposeAging(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Aging چهار-bucketی جداگانه"));
            if (model.Aging.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "Aging رسمی موجود نیست؛ آرایه خالی به معنی مبلغ صفر نیست.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    TableHeader(header, "نوع");
                    TableHeader(header, "بازه سررسید");
                    TableHeader(header, "تعداد");
                    TableHeader(header, $"مبلغ ({model.Snapshot.Project.CapturedBaseCurrencyCode})");
                });
                foreach (var item in model.Aging)
                {
                    TableValue(table, ObligationType(item.Type));
                    TableValue(table, AgingBucket(item.Bucket));
                    TableValue(table, PersianReportFormatting.ToPersianDigits(
                        item.Count.ToString(CultureInfo.InvariantCulture)));
                    TableValue(table, Money(item.Amount));
                }
            });
        });
    }

    private static void ComposeOpenObligations(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        container.EnsureSpace(100).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "تعهدات باز در زمان برش"));
            if (model.OpenObligations.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "ردیف تعهد باز رسمی وجود ندارد؛ وضعیت بخش تعیین می‌کند که این حالت صفر یا نبود داده است.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(0.8f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.7f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.25f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "نوع");
                    TableHeader(header, "شماره");
                    TableHeader(header, "طرف حساب");
                    TableHeader(header, "صدور");
                    TableHeader(header, "سررسید");
                    TableHeader(header, "مبلغ");
                    TableHeader(header, "تسویه");
                    TableHeader(header, "مانده");
                    TableHeader(header, "بازه");
                });
                foreach (var item in model.OpenObligations)
                {
                    TableValue(table, ObligationType(item.Type));
                    TableValue(table, PersianReportFormatting.SafeText(
                        item.NumberSnapshot,
                        ProjectFinancialPositionReportRenderingContract.MaximumObligationNumberLength));
                    TableValue(table, PersianReportFormatting.SafeText(
                        item.CounterpartySnapshot,
                        ProjectFinancialPositionReportRenderingContract.MaximumCounterpartyLength));
                    TableValue(table, PersianReportFormatting.FormatDate(item.IssueDate));
                    TableValue(table, PersianReportFormatting.FormatDate(item.DueDate));
                    TableValue(table, Money(item.Amount));
                    TableValue(table, Money(item.SettledAmountAtCutoff));
                    TableValue(table, Money(item.OutstandingAmount));
                    TableValue(table, AgingBucket(item.Bucket));
                }
            });
        });
    }

    private static void ComposeSourceCounts(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        var counts = model.Snapshot.SourceCounts;
        var values = new (string Label, int Value)[]
        {
            ("رکورد مالی منبع", counts.FinancialRecordSourceCount),
            ("رکورد مالی رسمی", counts.OfficialFinancialRecordCount),
            ("رکورد مالی حذف‌شده", counts.ExcludedFinancialRecordCount),
            ("تعهد منبع", counts.ObligationSourceCount),
            ("تعهد رسمی", counts.OfficialObligationCount),
            ("تعهد باز", counts.OpenObligationCount),
            ("تعهد حذف‌شده", counts.ExcludedObligationCount),
            ("Settlement منبع", counts.SettlementSourceCount),
            ("Settlement واجد شرایط", counts.EligibleSettlementCount),
            ("Settlement حذف‌شده", counts.ExcludedSettlementCount),
            ("Budget منبع", counts.BudgetBaselineSourceCount),
            ("Budget مؤثر", counts.EffectiveBudgetBaselineCount),
            ("Budget حذف‌شده", counts.ExcludedBudgetBaselineCount),
            ("Collection ناقص", counts.IncompleteCollectionCount)
        };
        container.EnsureSpace(95).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "شمارنده‌های Source و Exclusion"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn();
                });
                for (var index = 0; index < values.Length; index += 2)
                {
                    SummaryCell(table, values[index].Label, header: true);
                    SummaryCell(table, PersianReportFormatting.ToPersianDigits(
                        values[index].Value.ToString(CultureInfo.InvariantCulture)));
                    SummaryCell(table, values[index + 1].Label, header: true);
                    SummaryCell(table, PersianReportFormatting.ToPersianDigits(
                        values[index + 1].Value.ToString(CultureInfo.InvariantCulture)));
                }
            });
        });
    }

    private static void ComposeLineage(
        IContainer container,
        ProjectFinancialPositionReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        container.EnsureSpace(85).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Lineage و قواعد خواندن خروجی"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(130);
                    columns.RelativeColumn();
                });
                SummaryCell(table, "Snapshot schema", header: true);
                SummaryCell(table, snapshot.SchemaVersion);
                SummaryCell(table, "Source Manifest SHA-256", header: true);
                table.Cell().Element(SummaryValueCell).ContentFromLeftToRight()
                    .Text(snapshot.SourceManifestSha256).FontSize(6.5f);
                SummaryCell(table, "آخرین تغییر منبع", header: true);
                SummaryCell(table, snapshot.SourceMaxChangedAt.HasValue
                    ? PersianReportFormatting.FormatInstant(
                        snapshot.SourceMaxChangedAt.Value,
                        snapshot.Project.TimeZone)
                    : "—");
                SummaryCell(table, "مرز معنایی", header: true);
                SummaryCell(table,
                    "فقط Cash رسمی، تعهد باز، Aging و Budget مصوب؛ بدون مقدار ساختگی یا join خانواده دیگر");
            });
        });
    }

    private static void ComposeFooter(
        IContainer container,
        ProjectFinancialPositionReportRenderRequest request)
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

    private static void StatusChip(
        RowDescriptor row,
        string label,
        ProjectFinancialPositionSectionStatus status)
    {
        row.RelativeItem().PaddingHorizontal(2).Background("#FFFFFF")
            .Border(0.5f).BorderColor("#C8D6E8").Padding(3).AlignCenter()
            .Text($"{label}: {SectionStatus(status)}").FontSize(6.7f);
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

    private static string Money(decimal? value) => PersianReportFormatting.FormatDecimal(value);

    private static string Percent(decimal? value) => value.HasValue
        ? $"{PersianReportFormatting.FormatDecimal(value)}٪"
        : "—";

    private static string SectionStatus(ProjectFinancialPositionSectionStatus status) => status switch
    {
        ProjectFinancialPositionSectionStatus.Available => "داده رسمی موجود",
        ProjectFinancialPositionSectionStatus.NoData => "بدون داده رسمی",
        ProjectFinancialPositionSectionStatus.InsufficientData => "داده رسمی ناکافی",
        ProjectFinancialPositionSectionStatus.NotConfigured => "پیکربندی نشده",
        ProjectFinancialPositionSectionStatus.SetupRequired => "نیازمند راه‌اندازی",
        ProjectFinancialPositionSectionStatus.Suspended => "تعلیق‌شده",
        _ => "نامشخص"
    };

    private static string ObligationType(FinancialObligationType type) => type switch
    {
        FinancialObligationType.Payable => "پرداختنی",
        FinancialObligationType.Receivable => "دریافتنی",
        _ => "نامشخص"
    };

    private static string AgingBucket(ProjectFinancialPositionAgingBucket bucket) => bucket switch
    {
        ProjectFinancialPositionAgingBucket.NotDue => "سررسیدنشده",
        ProjectFinancialPositionAgingBucket.Overdue1To30 => "۱ تا ۳۰ روز دیرکرد",
        ProjectFinancialPositionAgingBucket.Overdue31To60 => "۳۱ تا ۶۰ روز دیرکرد",
        ProjectFinancialPositionAgingBucket.Overdue61Plus => "۶۱ روز و بیشتر",
        _ => "نامشخص"
    };

    private static string ReasonCode(ProjectFinancialPositionReasonCode reason) => reason switch
    {
        ProjectFinancialPositionReasonCode.FinanceReportingNotConfigured => "گزارش‌دهی مالی پیکربندی نشده است",
        ProjectFinancialPositionReasonCode.FinanceSetupRequired => "Finance نیازمند راه‌اندازی است",
        ProjectFinancialPositionReasonCode.FinanceSuspended => "Finance تعلیق شده است",
        ProjectFinancialPositionReasonCode.OfficialFinancialRecordsMissing => "رکورد مالی رسمی وجود ندارد",
        ProjectFinancialPositionReasonCode.OfficialObligationsMissing => "تعهد رسمی وجود ندارد",
        ProjectFinancialPositionReasonCode.FinancialSourceIncomplete => "منبع مالی رسمی ناقص است",
        ProjectFinancialPositionReasonCode.ObligationSettlementLineageIncomplete => "Lineage تسویه تعهد ناقص است",
        ProjectFinancialPositionReasonCode.BudgetNotConfigured => "Budget پیکربندی نشده است",
        ProjectFinancialPositionReasonCode.BudgetSetupRequired => "Budget نیازمند راه‌اندازی است",
        ProjectFinancialPositionReasonCode.BudgetSuspended => "Budget تعلیق شده است",
        ProjectFinancialPositionReasonCode.OfficialBudgetBaselineMissing => "Budget Baseline رسمی وجود ندارد",
        ProjectFinancialPositionReasonCode.OfficialCashDataMissingForBudgetComparison => "Cash رسمی برای مقایسه Budget موجود نیست",
        ProjectFinancialPositionReasonCode.NegativePettyCashBalance => "مانده تنخواه منفی است",
        _ => "علت نامشخص"
    };

    private static string StatusMessage(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "حداقل یکی از بخش‌های Cash یا تعهدات دارای داده رسمی کامل است.",
        ReportDataStatus.NoData => "Finance فعال است، اما رکورد مالی و تعهد رسمی واجد شرایط وجود ندارد.",
        ReportDataStatus.InsufficientData => "حداقل یک منبع رسمی completeness یا lineage کافی ندارد.",
        ReportDataStatus.NotConfigured => "Finance در زمان برش پیکربندی یا فعال نبوده است.",
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
        "The Project Financial Position PDF renderer received another output format.");
}
