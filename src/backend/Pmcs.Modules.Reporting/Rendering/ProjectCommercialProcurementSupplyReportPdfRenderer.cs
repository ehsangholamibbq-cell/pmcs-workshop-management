using System.Globalization;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectCommercialProcurementSupplyReportPdfRenderer(
    ReportingRendererOptions options,
    ReportingExecutionOptions execution) : IProjectCommercialProcurementSupplyReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    public RenderedReportArtifact Render(
        ProjectCommercialProcurementSupplyReportRenderRequest request)
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
                "reporting.project_commercial_procurement_supply.renderer.pdf_failed",
                transient: false,
                "The certified Commercial procurement and supply PDF could not be rendered.",
                exception);
        }
    }

    internal IReadOnlyList<byte[]> RenderQualificationImages(
        ProjectCommercialProcurementSupplyReportRenderRequest request)
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
                "reporting.project_commercial_procurement_supply.renderer.pdf_visual_failed",
                transient: false,
                "The Commercial procurement and supply qualification image could not be rendered.",
                exception);
        }
    }

    private ProjectCommercialProcurementSupplyReportRenderModel Prepare(
        ProjectCommercialProcurementSupplyReportRenderRequest request)
    {
        CertifiedPdfRuntime.EnsureConfigured(options);
        var model = ProjectCommercialProcurementSupplyReportRenderModel.Create(request);
        var facts = model.Contracts.Count + model.Amendments.Count + model.PurchaseOrders.Count +
            model.SupplySummaries.Count + model.SupplierPerformance.Count;
        if (facts > execution.MaximumPdfFacts)
        {
            throw new ReportRenderingException(
                "reporting.output.page_limit_exceeded",
                transient: false,
                "The certified Commercial procurement and supply PDF row limit was exceeded.");
        }
        return model;
    }

    private static Document CreateDocument(
        ProjectCommercialProcurementSupplyReportRenderModel model) =>
        Document.Create(container =>
        {
            container.Page(page => ConfigurePage(
                page,
                model,
                "قراردادها و اصلاحیه‌های رسمی",
                content => ComposeContracts(content, model)));
            container.Page(page => ConfigurePage(
                page,
                model,
                "خرید، سفارش و شواهد تأمین",
                content => ComposeProcurement(content, model)));
            container.Page(page => ConfigurePage(
                page,
                model,
                "تأمین‌کنندگان و Lineage",
                content => ComposeSuppliersAndLineage(content, model)));
        }).WithMetadata(new DocumentMetadata
        {
            Title = "گزارش رسمی قرارداد، خرید و تأمین پروژه",
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
        ProjectCommercialProcurementSupplyReportRenderModel model,
        string section,
        Action<IContainer> content)
    {
        page.Size(PageSizes.A4.Landscape());
        page.MarginHorizontal(20);
        page.MarginVertical(16);
        page.PageColor(Colors.White);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(style => style
            .FontFamily(CertifiedPdfRuntimeContract.FontFamily)
            .FontSize(7.2f)
            .FontColor("#172033"));
        page.Header().Element(header => ComposeHeader(header, model, section));
        page.Content().PaddingVertical(7).Element(content);
        page.Footer().Element(footer => ComposeFooter(footer, model.Request));
    }

    private static void ComposeHeader(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model,
        string section)
    {
        container.BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("گزارش رسمی قرارداد، خرید و تأمین پروژه")
                    .FontSize(13.5f).Bold().FontColor("#0B4F8A");
                column.Item().Text(
                    $"{PersianReportFormatting.SafeText(model.Snapshot.Project.Name, 180)} — " +
                    PersianReportFormatting.SafeText(model.Snapshot.Project.Code, 80))
                    .FontSize(8).FontColor("#46556D");
                column.Item().Text(section).FontSize(6.8f).FontColor("#62728A");
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

    private static void ComposeContracts(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Element(identity => ComposeIdentity(identity, model));
            column.Item().Element(status => ComposeStatus(status, model));
            column.Item().Element(configuration => ComposeConfiguration(configuration, model));
            column.Item().Element(summary => ComposeContractSummary(summary, model));
            column.Item().Element(rows => ComposeContractRegister(rows, model));
            column.Item().Element(rows => ComposeAmendments(rows, model));
        });
    }

    private static void ComposeIdentity(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(80);
                columns.RelativeColumn();
                columns.ConstantColumn(80);
                columns.RelativeColumn();
                columns.ConstantColumn(80);
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
                .Text(model.Request.VerificationCode).FontSize(7).SemiBold();
        });
    }

    private static void ComposeStatus(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.Background(StatusColor(model.Snapshot.DataStatus))
            .Border(1).BorderColor(StatusBorderColor(model.Snapshot.DataStatus))
            .Padding(6).Column(column =>
            {
                column.Item().Text(PersianReportFormatting.DataStatus(model.Snapshot.DataStatus))
                    .Bold().FontSize(9.2f);
                column.Item().PaddingTop(2).Text(StatusMessage(model.Snapshot.DataStatus))
                    .FontColor("#4C596C");
                column.Item().PaddingTop(3).Row(row =>
                {
                    StatusChip(row, "قرارداد", model.Snapshot.ContractStatus);
                    StatusChip(row, "خرید", model.Snapshot.ProcurementStatus);
                    StatusChip(row, "تأمین", model.Snapshot.SupplyStatus);
                });
                if (model.ReasonCodes.Count > 0)
                {
                    column.Item().PaddingTop(3).Text(
                        string.Join("؛ ", model.ReasonCodes.Select(ReasonCode)))
                        .FontSize(7).FontColor("#684C20");
                }
            });
    }

    private static void ComposeConfiguration(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var configuration = model.Snapshot.Configuration;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "پیکربندی مؤثر در زمان برش"));
            if (configuration is null)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "پیکربندی رسمی مؤثر موجود نیست؛ هیچ حالت پیش‌فرض یا مقدار حدسی نمایش داده نشده است.");
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
                TableHeaderCell(table, "مدل قرارداد");
                TableHeaderCell(table, "وضعیت قرارداد");
                TableHeaderCell(table, "وضعیت خرید");
                TableHeaderCell(table, "نسخه پیکربندی");
                TableHeaderCell(table, "نسخه پروژه");
                TableValue(table, ContractModelText(configuration.ContractModel));
                TableValue(table, PersianReportFormatting.FeatureState(configuration.ContractState));
                TableValue(table, PersianReportFormatting.FeatureState(configuration.ProcurementState));
                TableValue(table, Number(configuration.ConfigurationVersion));
                TableValue(table, Number(configuration.ProjectRevision));
            });
        });
    }

    private static void ComposeContractSummary(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var summary = model.Snapshot.ContractSummary;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "خلاصه قرارداد و اصلاحیه"));
            if (summary is null)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    $"خلاصه رسمی قرارداد قابل نمایش نیست؛ وضعیت: {SectionStatus(model.Snapshot.ContractStatus)}.");
                return;
            }
            var values = new (string Label, string Value)[]
            {
                ("قرارداد رسمی", Number(summary.OfficialContractCount)),
                ("فعال", Number(summary.ActiveContractCount)),
                ("تعلیق", Number(summary.SuspendedContractCount)),
                ("بسته", Number(summary.ClosedContractCount)),
                ("خاتمه", Number(summary.TerminatedContractCount)),
                ("منقضی", Number(summary.ExpiredActiveContractCount)),
                ("اصلاحیه مصوب", Number(summary.ApprovedAmendmentCount)),
                ("تغییر مبلغ", Money(summary.ApprovedAmountDelta)),
                ("تمدید روز", Number(summary.ApprovedExtensionDays)),
                ("جمع سقف معلوم", Money(summary.KnownEffectiveContractCeilingSubtotal)),
                ("سقف کامل", Money(summary.EffectiveContractCeilingTotal)),
                ("گردش‌کار در انتظار", Number(
                    summary.PendingContractWorkflowCount + summary.PendingAmendmentWorkflowCount))
            };
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn();
                });
                for (var index = 0; index < values.Length; index += 3)
                {
                    for (var offset = 0; offset < 3; offset++)
                    {
                        SummaryCell(table, values[index + offset].Label, header: true);
                        SummaryCell(table, values[index + offset].Value);
                    }
                }
            });
        });
    }

    private static void ComposeContractRegister(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Register قراردادهای رسمی"));
            if (model.Contracts.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "ردیف قرارداد رسمی وجود ندارد؛ وضعیت بخش تعیین می‌کند این حالت نبود داده یا عدم پیکربندی است.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.45f);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(0.75f);
                });
                table.Header(header =>
                {
                    TableHeader(header, "شماره");
                    TableHeader(header, "عنوان");
                    TableHeader(header, "طرف قرارداد");
                    TableHeader(header, "نوع");
                    TableHeader(header, "وضعیت");
                    TableHeader(header, "پایان اصلی");
                    TableHeader(header, "پایان مؤثر");
                    TableHeader(header, "مبلغ مؤثر");
                    TableHeader(header, "اصلاحیه");
                });
                foreach (var item in model.Contracts)
                {
                    TableValue(table, Safe(item.Number, ProjectCommercialProcurementSupplyReportRenderingContract.MaximumNumberLength));
                    TableValue(table, Safe(item.Title, ProjectCommercialProcurementSupplyReportRenderingContract.MaximumTitleLength));
                    TableValue(table, $"{Safe(item.PartyCode, 32)} — {Safe(item.PartyName, 120)}");
                    TableValue(table, ContractType(item.Type));
                    TableValue(table, ContractState(item.State));
                    TableValue(table, Date(item.OriginalEndDate));
                    TableValue(table, Date(item.EffectiveEndDate));
                    TableValue(table, Money(item.EffectiveApprovedAmount));
                    TableValue(table, Number(item.ApprovedAmendmentCount));
                }
            });
        });
    }

    private static void ComposeAmendments(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.EnsureSpace(75).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "اصلاحیه‌های مصوب تا زمان برش"));
            if (model.Amendments.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "اصلاحیه مصوب واجد شرایط وجود ندارد؛ این حالت مبلغ یا مدت ساختگی ایجاد نمی‌کند.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    TableHeader(header, "قرارداد");
                    TableHeader(header, "شماره");
                    TableHeader(header, "عنوان");
                    TableHeader(header, "نوع");
                    TableHeader(header, "تغییر مبلغ");
                    TableHeader(header, "تمدید روز");
                    TableHeader(header, "تصویب");
                });
                foreach (var item in model.Amendments)
                {
                    TableValue(table, Safe(item.ContractNumber, 80));
                    TableValue(table, Safe(item.Number, 80));
                    TableValue(table, Safe(item.Title, 160));
                    TableValue(table, AmendmentType(item.Type));
                    TableValue(table, Money(item.AmountDelta));
                    TableValue(table, item.ExtensionDays.HasValue ? Number(item.ExtensionDays.Value) : "—");
                    TableValue(table, PersianReportFormatting.FormatInstant(
                        item.ApprovedAt,
                        model.Snapshot.Project.TimeZone));
                }
            });
        });
    }

    private static void ComposeProcurement(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Element(summary => ComposeProcurementSummary(summary, model));
            column.Item().Element(rows => ComposePurchaseOrders(rows, model));
            column.Item().Element(rows => ComposeSupplySummaries(rows, model));
            column.Item().Background("#FFF4E5").Border(0.8f).BorderColor("#E5C17A")
                .Padding(6).Text(
                    "مبلغ سفارش فقط تعهد تجاری صادرشده است؛ مقدار تحویل، پذیرش و بازرسی مستقل نمایش داده می‌شوند و مقدار مفقود هرگز صفر یا تکمیل‌شده فرض نمی‌شود.")
                .FontSize(7).FontColor("#684C20");
        });
    }

    private static void ComposeProcurementSummary(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var summary = model.Snapshot.ProcurementSummary;
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "خلاصه درخواست خرید و سفارش"));
            if (summary is null)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    $"خلاصه رسمی خرید قابل نمایش نیست؛ وضعیت: {SectionStatus(model.Snapshot.ProcurementStatus)}.");
                return;
            }
            var values = new (string Label, string Value)[]
            {
                ("درخواست Draft", Number(summary.DraftRequestCount)),
                ("ارسال‌شده", Number(summary.SubmittedRequestCount)),
                ("برگشتی", Number(summary.ReturnedRequestCount)),
                ("تأییدشده", Number(summary.ApprovedRequestCount)),
                ("سفارش‌شده", Number(summary.OrderedRequestCount)),
                ("لغوشده", Number(summary.CancelledRequestCount)),
                ("در انتظار سفارش", Number(summary.ApprovedRequestsAwaitingOrderCount)),
                ("سفارش باز", Number(summary.IssuedOrderCount)),
                ("سفارش بسته", Number(summary.ClosedOrderCount)),
                ("سفارش لغوشده", Number(summary.CancelledOrderCount)),
                ("تعهد صادرشده", Money(summary.TotalIssuedOrderAmount)),
                ("تعهد باز", Money(summary.OpenOrderAmount))
            };
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn();
                });
                for (var index = 0; index < values.Length; index += 3)
                {
                    for (var offset = 0; offset < 3; offset++)
                    {
                        SummaryCell(table, values[index + offset].Label, header: true);
                        SummaryCell(table, values[index + offset].Value);
                    }
                }
            });
        });
    }

    private static void ComposePurchaseOrders(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Register سفارش و Fulfillment"));
            if (model.PurchaseOrders.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "سفارش رسمی واجد شرایط وجود ندارد؛ درخواست تأییدشده بدون سفارش تعهد تجاری نیست.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    TableHeader(header, "سفارش");
                    TableHeader(header, "تأمین‌کننده");
                    TableHeader(header, "درخواست");
                    TableHeader(header, "قلم/واحد پایه");
                    TableHeader(header, "مبلغ");
                    TableHeader(header, "سررسید");
                    TableHeader(header, "وضعیت");
                    TableHeader(header, "پذیرفته");
                    TableHeader(header, "باقی‌مانده");
                    TableHeader(header, "Fulfillment");
                });
                foreach (var item in model.PurchaseOrders)
                {
                    TableValue(table, Safe(item.Number, 80));
                    TableValue(table, $"{Safe(item.PartyCode, 32)} — {Safe(item.PartyName, 100)}");
                    TableValue(table, Safe(item.PurchaseRequestNumber, 80));
                    TableValue(table, item.ItemCode is null
                        ? "—"
                        : $"{Safe(item.ItemCode, 48)} / {Safe(item.BaseUnit, 24)}");
                    TableValue(table, Money(item.Amount));
                    TableValue(table, Date(item.DeliveryDueDate));
                    TableValue(table, $"{OrderState(item.State)} / {DeliveryStatus(item.DeliveryStatus)}");
                    TableValue(table, Quantity(item.AcceptedBaseQuantity));
                    TableValue(table, Quantity(item.RemainingOrderedQuantity));
                    TableValue(table, Percent(item.FulfillmentPercent));
                }
            });
        });
    }

    private static void ComposeSupplySummaries(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.EnsureSpace(90).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "خلاصه تأمین بر مبنای قلم و واحد پایه"));
            if (model.SupplySummaries.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    $"خلاصه مقدار رسمی موجود نیست؛ وضعیت: {SectionStatus(model.Snapshot.SupplyStatus)}.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    TableHeader(header, "کد قلم");
                    TableHeader(header, "نام قلم");
                    TableHeader(header, "نوع");
                    TableHeader(header, "واحد پایه");
                    TableHeader(header, "سفارش");
                    TableHeader(header, "مقدار سفارش");
                    TableHeader(header, "تحویل");
                    TableHeader(header, "پذیرش");
                    TableHeader(header, "رد/قرنطینه");
                });
                foreach (var item in model.SupplySummaries)
                {
                    TableValue(table, Safe(item.ItemCode, 48));
                    TableValue(table, Safe(item.ItemName, 180));
                    TableValue(table, ItemKind(item.ItemKind));
                    TableValue(table, Safe(item.BaseUnit, 24));
                    TableValue(table, Number(item.OrderCount));
                    TableValue(table, Quantity(item.OrderedBaseQuantity));
                    TableValue(table, Quantity(item.DeliveredBaseQuantity));
                    TableValue(table, Quantity(item.AcceptedBaseQuantity));
                    TableValue(table, Quantity(
                        item.RejectedBaseQuantity + item.QuarantinedBaseQuantity));
                }
            });
        });
    }

    private static void ComposeSuppliersAndLineage(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Element(rows => ComposeSupplierPerformance(rows, model));
            column.Item().Element(counts => ComposeSourceCounts(counts, model));
            column.Item().Element(lineage => ComposeLineage(lineage, model));
        });
    }

    private static void ComposeSupplierPerformance(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        container.Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "عملکرد قابل ممیزی تأمین‌کنندگان"));
            if (model.SupplierPerformance.Count == 0)
            {
                column.Item().PaddingTop(3).Element(EmptyBox).Text(
                    "ردیف عملکرد رسمی وجود ندارد؛ امتیاز، رتبه یا توصیه ساخته نشده است.");
                return;
            }
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    TableHeader(header, "کد");
                    TableHeader(header, "نام");
                    TableHeader(header, "کل سفارش");
                    TableHeader(header, "باز");
                    TableHeader(header, "بسته");
                    TableHeader(header, "به‌موقع");
                    TableHeader(header, "دیرهنگام");
                    TableHeader(header, "معوق باز");
                    TableHeader(header, "کسری بسته");
                    TableHeader(header, "غیرقابل ارزیابی");
                    TableHeader(header, "نرخ به‌موقع");
                });
                foreach (var item in model.SupplierPerformance)
                {
                    TableValue(table, Safe(item.PartyCode, 32));
                    TableValue(table, Safe(item.PartyName, 160));
                    TableValue(table, Number(item.IssuedOrderCount));
                    TableValue(table, Number(item.OpenOrderCount));
                    TableValue(table, Number(item.ClosedOrderCount));
                    TableValue(table, Number(item.OnTimeFulfilledCount));
                    TableValue(table, Number(item.LateFulfilledCount));
                    TableValue(table, Number(item.OverdueOpenCount));
                    TableValue(table, Number(item.ClosedShortCount));
                    TableValue(table, Number(item.NotAssessableCount));
                    TableValue(table, Percent(item.OnTimeFulfillmentRate));
                }
            });
        });
    }

    private static void ComposeSourceCounts(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var counts = model.Snapshot.SourceCounts;
        var values = new (string Label, int Value)[]
        {
            ("Party منبع", counts.PartySnapshotSourceCount),
            ("Party مؤثر", counts.EffectivePartySnapshotCount),
            ("Item منبع", counts.ItemSnapshotSourceCount),
            ("Item مؤثر", counts.EffectiveItemSnapshotCount),
            ("قرارداد منبع", counts.ContractSourceCount),
            ("قرارداد رسمی", counts.OfficialContractCount),
            ("اصلاحیه منبع", counts.AmendmentSourceCount),
            ("اصلاحیه مصوب", counts.ApprovedAmendmentCount),
            ("درخواست منبع", counts.PurchaseRequestSourceCount),
            ("درخواست تا برش", counts.PurchaseRequestAtCutoffCount),
            ("سفارش منبع", counts.PurchaseOrderSourceCount),
            ("سفارش رسمی", counts.OfficialPurchaseOrderCount),
            ("Receipt منبع", counts.GoodsReceiptSourceCount),
            ("Receipt واجد شرایط", counts.EligibleGoodsReceiptCount),
            ("پذیرش خدمت منبع", counts.ServiceAcceptanceSourceCount),
            ("پذیرش واجد شرایط", counts.EligibleServiceAcceptanceCount),
            ("Collection ناقص", counts.IncompleteCollectionCount),
            ("گردش‌کار در انتظار", counts.PendingContractCount + counts.PendingAmendmentCount)
        };
        container.EnsureSpace(105).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "شمارنده‌های Source و Eligibility"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.45f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.45f);
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.45f);
                    columns.RelativeColumn();
                });
                for (var index = 0; index < values.Length; index += 3)
                {
                    for (var offset = 0; offset < 3; offset++)
                    {
                        SummaryCell(table, values[index + offset].Label, header: true);
                        SummaryCell(table, Number(values[index + offset].Value));
                    }
                }
            });
        });
    }

    private static void ComposeLineage(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        container.EnsureSpace(120).Column(column =>
        {
            column.Item().Element(title => SectionTitle(title, "Lineage و قواعد خواندن خروجی"));
            column.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(135);
                    columns.RelativeColumn();
                });
                SummaryCell(table, "Snapshot schema", header: true);
                SummaryCell(table, snapshot.SchemaVersion);
                SummaryCell(table, "Policy", header: true);
                SummaryCell(table, snapshot.PolicyVersion);
                SummaryCell(table, "Source Manifest SHA-256", header: true);
                table.Cell().Element(SummaryValueCell).ContentFromLeftToRight()
                    .Text(snapshot.SourceManifestSha256).FontSize(6.3f);
                SummaryCell(table, "آخرین تغییر منبع", header: true);
                SummaryCell(table, snapshot.SourceMaxChangedAt.HasValue
                    ? PersianReportFormatting.FormatInstant(
                        snapshot.SourceMaxChangedAt.Value,
                        snapshot.Project.TimeZone)
                    : "—");
                SummaryCell(table, "مرز معنایی", header: true);
                SummaryCell(table,
                    "فقط Contract/Amendment/Request/Order و شواهد رسمی Receipt/Inspection/Service Acceptance در زمان برش");
            });
            column.Item().PaddingTop(5).Background("#EEF3F9").Border(0.6f)
                .BorderColor("#AFC3DE").Padding(6).Text(
                    "این خروجی Snapshot ممیزی‌شده است؛ مبلغ سفارش با پرداخت یا هزینه یکی نیست، ثبت دریافت با پذیرش یکی نیست و نرخ تأمین‌کننده فقط از شمارنده‌های رسمی همین Snapshot ساخته شده است.")
                .FontSize(7).FontColor("#30465F");
        });
    }

    private static void ComposeFooter(
        IContainer container,
        ProjectCommercialProcurementSupplyReportRenderRequest request)
    {
        container.BorderTop(1).BorderColor("#D1DBE8").PaddingTop(5).Row(row =>
        {
            row.RelativeItem().ContentFromLeftToRight()
                .DefaultTextStyle(style => style.FontSize(6.1f).FontColor("#5D6B80"))
                .Text(text =>
                {
                    text.Span(request.VerificationCode).SemiBold();
                    text.Span("  |  ");
                    text.Span(request.ManifestSha256[..12]);
                });
            row.AutoItem().DefaultTextStyle(style => style.FontSize(6.7f).FontColor("#5D6B80"))
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
        ProjectCommercialReportingSectionStatus status)
    {
        row.RelativeItem().PaddingHorizontal(2).Background("#FFFFFF")
            .Border(0.5f).BorderColor("#C8D6E8").Padding(3).AlignCenter()
            .Text($"{label}: {SectionStatus(status)}").FontSize(6.6f);
    }

    private static void SectionTitle(IContainer container, string title) => container
        .BorderBottom(1).BorderColor("#AFC3DE").PaddingBottom(3)
        .Text(title).FontSize(9.2f).Bold().FontColor("#163F68");

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
        .Background("#DCE8F5").Border(0.5f).BorderColor("#AFC3DE").Padding(2.5f)
        .AlignMiddle().Text(value).SemiBold().FontSize(6.2f);

    private static void TableHeaderCell(TableDescriptor table, string value) => table.Cell()
        .Background("#EEF3F9").Border(0.5f).BorderColor("#C8D6E8").Padding(3)
        .AlignMiddle().Text(value).SemiBold().FontSize(6.4f);

    private static void TableValue(TableDescriptor table, string value) => table.Cell()
        .BorderBottom(0.5f).BorderColor("#D7E0EB").Padding(2.5f)
        .AlignMiddle().Text(value).FontSize(6.1f);

    private static string Safe(string? value, int maximumLength) =>
        PersianReportFormatting.SafeText(value, maximumLength);

    private static string Date(DateOnly? value) => value.HasValue
        ? PersianReportFormatting.FormatDate(value.Value)
        : "—";

    private static string Money(decimal? value) => PersianReportFormatting.FormatDecimal(value);

    private static string Quantity(decimal? value) => PersianReportFormatting.FormatDecimal(value);

    private static string Percent(decimal? value) => value.HasValue
        ? $"{PersianReportFormatting.FormatDecimal(value)}٪"
        : "—";

    private static string Number(long value) => PersianReportFormatting.ToPersianDigits(
        value.ToString(CultureInfo.InvariantCulture));

    private static string SectionStatus(ProjectCommercialReportingSectionStatus status) => status switch
    {
        ProjectCommercialReportingSectionStatus.Available => "داده رسمی موجود",
        ProjectCommercialReportingSectionStatus.NoData => "بدون داده رسمی",
        ProjectCommercialReportingSectionStatus.InsufficientData => "داده رسمی ناکافی",
        ProjectCommercialReportingSectionStatus.NotConfigured => "پیکربندی نشده",
        ProjectCommercialReportingSectionStatus.SetupRequired => "نیازمند راه‌اندازی",
        ProjectCommercialReportingSectionStatus.Suspended => "تعلیق‌شده",
        _ => "نامشخص"
    };

    private static string ContractModelText(ContractModel model) => model switch
    {
        ContractModel.GeneralContracting => "پیمانکاری",
        ContractModel.ConstructionManagement => "مدیریت پیمان",
        ContractModel.LaborOnly => "دستمزدی",
        ContractModel.Hybrid => "ترکیبی",
        _ => "پیکربندی نشده"
    };

    private static string ContractType(ProjectContractType type) => type switch
    {
        ProjectContractType.MainContract => "قرارداد اصلی",
        ProjectContractType.Subcontract => "پیمان جزء",
        ProjectContractType.Supply => "تأمین",
        ProjectContractType.ProfessionalService => "خدمات حرفه‌ای",
        ProjectContractType.Labor => "دستمزدی",
        _ => "سایر"
    };

    private static string ContractState(ProjectCommercialContractState state) => state switch
    {
        ProjectCommercialContractState.Active => "فعال",
        ProjectCommercialContractState.Suspended => "تعلیق‌شده",
        ProjectCommercialContractState.Closed => "بسته",
        ProjectCommercialContractState.Terminated => "خاتمه‌یافته",
        ProjectCommercialContractState.Expired => "منقضی",
        _ => "نامشخص"
    };

    private static string AmendmentType(ContractAmendmentType type) => type switch
    {
        ContractAmendmentType.ScopeChange => "تغییر دامنه",
        ContractAmendmentType.ValueChange => "تغییر مبلغ",
        ContractAmendmentType.TimeExtension => "تمدید مدت",
        ContractAmendmentType.Mixed => "ترکیبی",
        _ => "نامشخص"
    };

    private static string OrderState(ProjectCommercialPurchaseOrderState state) => state switch
    {
        ProjectCommercialPurchaseOrderState.Issued => "صادرشده",
        ProjectCommercialPurchaseOrderState.Closed => "بسته",
        ProjectCommercialPurchaseOrderState.Cancelled => "لغوشده",
        _ => "نامشخص"
    };

    private static string DeliveryStatus(ProjectCommercialDeliveryStatus status) => status switch
    {
        ProjectCommercialDeliveryStatus.NotAssessable => "غیرقابل ارزیابی",
        ProjectCommercialDeliveryStatus.PendingDue => "در انتظار سررسید",
        ProjectCommercialDeliveryStatus.OnTimeFulfilled => "تکمیل به‌موقع",
        ProjectCommercialDeliveryStatus.LateFulfilled => "تکمیل دیرهنگام",
        ProjectCommercialDeliveryStatus.OverdueOpen => "معوق باز",
        ProjectCommercialDeliveryStatus.ClosedShort => "بسته با کسری",
        ProjectCommercialDeliveryStatus.Cancelled => "لغوشده",
        _ => "نامشخص"
    };

    private static string ItemKind(SupplyItemKind kind) => kind switch
    {
        SupplyItemKind.Material => "مصالح",
        SupplyItemKind.Service => "خدمت",
        SupplyItemKind.EquipmentRental => "اجاره تجهیز",
        _ => "نامشخص"
    };

    private static string ReasonCode(ProjectCommercialReportingReasonCode reason) => reason switch
    {
        ProjectCommercialReportingReasonCode.CommercialReportingNotConfigured => "گزارش‌دهی تجاری پیکربندی نشده است",
        ProjectCommercialReportingReasonCode.ContractSetupRequired => "قراردادها نیازمند راه‌اندازی‌اند",
        ProjectCommercialReportingReasonCode.ContractSuspended => "قراردادها تعلیق شده‌اند",
        ProjectCommercialReportingReasonCode.ProcurementSetupRequired => "خرید نیازمند راه‌اندازی است",
        ProjectCommercialReportingReasonCode.ProcurementSuspended => "خرید تعلیق شده است",
        ProjectCommercialReportingReasonCode.OfficialContractsMissing => "قرارداد رسمی وجود ندارد",
        ProjectCommercialReportingReasonCode.OfficialProcurementMissing => "خرید رسمی وجود ندارد",
        ProjectCommercialReportingReasonCode.OfficialSupplyEvidenceMissing => "شواهد رسمی تأمین وجود ندارد",
        ProjectCommercialReportingReasonCode.ContractLifecycleIncomplete => "چرخه قرارداد ناقص است",
        ProjectCommercialReportingReasonCode.AmendmentLifecycleIncomplete => "چرخه اصلاحیه ناقص است",
        ProjectCommercialReportingReasonCode.ProcurementLifecycleIncomplete => "چرخه خرید ناقص است",
        ProjectCommercialReportingReasonCode.SupplyLineageIncomplete => "Lineage تأمین ناقص است",
        ProjectCommercialReportingReasonCode.PartySnapshotUnavailable => "Snapshot طرف قرارداد موجود نیست",
        ProjectCommercialReportingReasonCode.UnitConversionHistoryUnavailable => "تاریخچه تبدیل واحد موجود نیست",
        ProjectCommercialReportingReasonCode.ContractCeilingUnavailable => "سقف قرارداد کامل نیست",
        ProjectCommercialReportingReasonCode.ContractEndDateUnavailable => "تاریخ پایان قرارداد موجود نیست",
        ProjectCommercialReportingReasonCode.OrderQuantityBasisUnavailable => "مبنای مقدار سفارش موجود نیست",
        ProjectCommercialReportingReasonCode.DeliveryDueDateUnavailable => "موعد تحویل موجود نیست",
        ProjectCommercialReportingReasonCode.ClosedOrderSupplyGap => "سفارش بسته دارای کسری تأمین است",
        ProjectCommercialReportingReasonCode.PendingInspection => "بازرسی در انتظار است",
        ProjectCommercialReportingReasonCode.RejectedOrQuarantinedSupply => "تأمین رد یا قرنطینه‌شده وجود دارد",
        _ => "علت نامشخص"
    };

    private static string StatusMessage(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "حداقل یک بخش دارای داده رسمی کامل و قابل ممیزی است.",
        ReportDataStatus.NoData => "بخش فعال است، اما fact رسمی واجد زمان برش وجود ندارد.",
        ReportDataStatus.InsufficientData => "حداقل یک چرخه، snapshot یا lineage رسمی کامل نیست.",
        ReportDataStatus.NotConfigured => "قرارداد و خرید در زمان برش قابل ارزیابی نبوده‌اند.",
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
        "The Commercial procurement and supply PDF renderer received another output format.");
}
