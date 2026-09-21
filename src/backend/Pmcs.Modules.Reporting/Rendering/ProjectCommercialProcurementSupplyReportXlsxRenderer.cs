using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectCommercialProcurementSupplyReportXlsxRenderer(
    ReportingExecutionOptions execution) : IProjectCommercialProcurementSupplyReportRenderer
{
    private const string SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string PackageRelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string ExtendedPropertiesNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
    private const string DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";

    public ReportFormat Format => ReportFormat.Xlsx;

    public RenderedReportArtifact Render(
        ProjectCommercialProcurementSupplyReportRenderRequest request)
    {
        if (request.Format != Format)
        {
            throw UnsupportedFormat();
        }

        try
        {
            var model = ProjectCommercialProcurementSupplyReportRenderModel.Create(request);
            var semanticRows = model.Contracts.Count + model.Amendments.Count +
                model.PurchaseOrders.Count + model.SupplySummaries.Count +
                model.SupplierPerformance.Count + 76;
            if (semanticRows > execution.MaximumXlsxRows)
            {
                throw new ReportRenderingException(
                    "reporting.output.row_limit_exceeded",
                    transient: false,
                    "The certified Commercial procurement and supply workbook row limit was exceeded.");
            }

            var sheets = BuildSheets(model);
            var entries = BuildEntries(model, sheets);
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var item in entries)
                {
                    var entry = archive.CreateEntry(item.Name, CompressionLevel.NoCompression);
                    entry.LastWriteTime =
                        new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var destination = entry.Open();
                    destination.Write(item.Content);
                }
            }

            var bytes = output.ToArray();
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
                "reporting.project_commercial_procurement_supply.renderer.xlsx_failed",
                transient: false,
                "The certified Commercial procurement and supply workbook could not be rendered.",
                exception);
        }
    }

    private static WorkbookEntry[] BuildEntries(
        ProjectCommercialProcurementSupplyReportRenderModel model,
        IReadOnlyList<WorkbookSheet> sheets)
    {
        var entries = new List<WorkbookEntry>
        {
            new("[Content_Types].xml", ContentTypes(sheets.Count)),
            new("_rels/.rels", PackageRelationships()),
            new("docProps/app.xml", AppProperties()),
            new("docProps/core.xml", CoreProperties(model)),
            new("xl/workbook.xml", Workbook(sheets)),
            new("xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Count)),
            new("xl/styles.xml", Styles())
        };
        for (var index = 0; index < sheets.Count; index++)
        {
            entries.Add(new WorkbookEntry(
                $"xl/worksheets/sheet{index + 1}.xml",
                Worksheet(sheets[index])));
        }
        return entries.ToArray();
    }

    private static WorkbookSheet[] BuildSheets(
        ProjectCommercialProcurementSupplyReportRenderModel model) =>
    [
        MetadataSheet(model),
        ContractSummarySheet(model),
        ContractsSheet(model),
        AmendmentsSheet(model),
        ProcurementSheet(model),
        PurchaseOrdersSheet(model),
        SupplySummarySheet(model),
        SuppliersSheet(model),
        SourceCountsSheet(model),
        LineageSheet(model)
    ];

    private static WorkbookSheet MetadataSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        var request = model.Request;
        var reasonText = model.ReasonCodes.Count == 0
            ? "—"
            : string.Join("؛ ", model.ReasonCodes.Select(ReasonCode));
        var rows = new (string Key, string Value, bool LeftToRight)[]
        {
            ("عنوان", "گزارش رسمی قرارداد، خرید و تأمین پروژه", false),
            ("کد پروژه", snapshot.Project.Code, false),
            ("نام پروژه", snapshot.Project.Name, false),
            ("منطقه زمانی", snapshot.Project.TimeZone, true),
            ("ارز پایه", snapshot.Project.CapturedBaseCurrencyCode, true),
            ("تاریخ برش شمسی", PersianReportFormatting.FormatDate(snapshot.Cutoff.CutoffLocalDate), false),
            ("تاریخ برش ISO", snapshot.Cutoff.CutoffLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), true),
            ("زمان برش محلی", PersianReportFormatting.FormatInstant(snapshot.Cutoff.SourceCutoffUtc, snapshot.Project.TimeZone), false),
            ("زمان برش UTC", snapshot.Cutoff.SourceCutoffUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), true),
            ("وضعیت داده", PersianReportFormatting.DataStatus(snapshot.DataStatus), false),
            ("وضعیت قرارداد", SectionStatus(snapshot.ContractStatus), false),
            ("وضعیت خرید", SectionStatus(snapshot.ProcurementStatus), false),
            ("وضعیت تأمین", SectionStatus(snapshot.SupplyStatus), false),
            ("علت‌های وضعیت", reasonText, false),
            ("طبقه‌بندی", PersianReportFormatting.Classification(snapshot.Classification), false),
            ("نسخه پروژه", snapshot.Project.Revision.ToString(CultureInfo.InvariantCulture), true),
            ("نسخه پیکربندی پروژه", snapshot.Project.ConfigurationVersion.ToString(CultureInfo.InvariantCulture), true),
            ("کد تعریف", request.DefinitionCode, true),
            ("نسخه تعریف", request.DefinitionVersion, true),
            ("نسخه قالب", request.TemplateVersion, true),
            ("قرارداد Renderer", request.RendererContractVersion, true),
            ("قرارداد Layout", request.LayoutContractVersion, true),
            ("نسخه Snapshot", snapshot.SchemaVersion, true),
            ("نسخه Policy", snapshot.PolicyVersion, true),
            ("Template Content SHA-256", request.TemplateContentDigest, true),
            ("Snapshot SHA-256", request.SnapshotSha256, true),
            ("Source Manifest SHA-256", request.SourceManifestSha256, true),
            ("Output Manifest SHA-256", request.ManifestSha256, true),
            ("کد راستی‌آزمایی", request.VerificationCode, true),
            ("Run ID", request.RunId.ToString(), true),
            ("Snapshot ID", request.SnapshotId.ToString(), true),
            ("Output ID", request.OutputId.ToString(), true),
            ("Template Version ID", request.TemplateVersionId.ToString(), true)
        };
        return new WorkbookSheet(
            "Metadata",
            [34d, 92d],
            ["کلید", "مقدار"],
            rows.Select(item => new[]
            {
                TextCell(item.Key),
                TextCell(item.Value, item.LeftToRight)
            }).ToArray());
    }

    private static WorkbookSheet ContractSummarySheet(
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var item = model.Snapshot.ContractSummary;
        WorkbookCell[][] rows = item is null
            ? []
            :
            [
                [
                    NumberCell(item.OfficialContractCount),
                    NumberCell(item.ActiveContractCount),
                    NumberCell(item.SuspendedContractCount),
                    NumberCell(item.ClosedContractCount),
                    NumberCell(item.TerminatedContractCount),
                    NumberCell(item.ExpiredActiveContractCount),
                    NumberCell(item.PendingContractWorkflowCount),
                    NumberCell(item.ApprovedAmendmentCount),
                    NumberCell(item.PendingAmendmentWorkflowCount),
                    NumberCell(item.ApprovedAmountDelta),
                    NumberCell(item.ApprovedExtensionDays),
                    NumberCell(item.KnownEffectiveContractCeilingSubtotal),
                    NumberCell(item.EffectiveContractCeilingTotal)
                ]
            ];
        return new WorkbookSheet(
            "Contract Summary",
            [18d, 14d, 14d, 14d, 14d, 14d, 22d, 18d, 22d, 22d, 18d, 26d, 24d],
            ["قرارداد رسمی", "فعال", "تعلیق", "بسته", "خاتمه", "منقضی", "قرارداد در انتظار", "اصلاحیه مصوب", "اصلاحیه در انتظار", "تغییر مبلغ", "تمدید روز", "جمع سقف معلوم", "سقف کامل"],
            rows);
    }

    private static WorkbookSheet ContractsSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model) => new(
        "Contracts",
        [20d, 34d, 16d, 30d, 18d, 20d, 18d, 18d, 18d, 18d, 22d, 22d, 22d, 16d, 16d],
        ["شماره", "عنوان", "کد طرف", "نام طرف", "نوع طرف", "نوع قرارداد", "وضعیت", "شروع", "پایان اصلی", "پایان مؤثر", "مبلغ اصلی", "تغییر مبلغ", "مبلغ مؤثر", "اصلاحیه مصوب", "تمدید روز"],
        model.Contracts.Select(item => new[]
        {
            TextCell(item.Number, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumNumberLength),
            TextCell(item.Title,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumTitleLength),
            TextCell(item.PartyCode, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumPartyCodeLength),
            TextCell(item.PartyName,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumPartyNameLength),
            TextCell(PartyTypeText(item.PartyType)),
            TextCell(ContractType(item.Type)),
            TextCell(ContractState(item.State)),
            TextCell(Date(item.StartDate)),
            TextCell(Date(item.OriginalEndDate)),
            TextCell(Date(item.EffectiveEndDate)),
            NumberCell(item.OriginalApprovedAmount),
            NumberCell(item.ApprovedAmountDelta),
            NumberCell(item.EffectiveApprovedAmount),
            NumberCell(item.ApprovedAmendmentCount),
            NumberCell(item.ApprovedExtensionDays)
        }).ToArray());

    private static WorkbookSheet AmendmentsSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model) => new(
        "Amendments",
        [20d, 20d, 36d, 20d, 22d, 18d, 26d],
        ["شماره قرارداد", "شماره اصلاحیه", "عنوان", "نوع", "تغییر مبلغ", "تمدید روز", "زمان تصویب"],
        model.Amendments.Select(item => new[]
        {
            TextCell(item.ContractNumber, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumNumberLength),
            TextCell(item.Number, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumNumberLength),
            TextCell(item.Title,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumTitleLength),
            TextCell(AmendmentType(item.Type)),
            NumberCell(item.AmountDelta),
            NumberCell(item.ExtensionDays),
            TextCell(PersianReportFormatting.FormatInstant(
                item.ApprovedAt,
                model.Snapshot.Project.TimeZone))
        }).ToArray());

    private static WorkbookSheet ProcurementSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var item = model.Snapshot.ProcurementSummary;
        WorkbookCell[][] rows = item is null
            ? []
            :
            [
                [
                    NumberCell(item.DraftRequestCount),
                    NumberCell(item.SubmittedRequestCount),
                    NumberCell(item.ReturnedRequestCount),
                    NumberCell(item.ApprovedRequestCount),
                    NumberCell(item.OrderedRequestCount),
                    NumberCell(item.CancelledRequestCount),
                    NumberCell(item.ApprovedRequestsAwaitingOrderCount),
                    NumberCell(item.IssuedOrderCount),
                    NumberCell(item.ClosedOrderCount),
                    NumberCell(item.CancelledOrderCount),
                    NumberCell(item.TotalIssuedOrderAmount),
                    NumberCell(item.OpenOrderAmount)
                ]
            ];
        return new WorkbookSheet(
            "Procurement",
            [16d, 16d, 16d, 16d, 16d, 16d, 22d, 16d, 16d, 16d, 24d, 22d],
            ["درخواست Draft", "ارسال‌شده", "برگشتی", "تأییدشده", "سفارش‌شده", "لغوشده", "در انتظار سفارش", "سفارش باز", "سفارش بسته", "سفارش لغوشده", "تعهد صادرشده", "تعهد باز"],
            rows);
    }

    private static WorkbookSheet PurchaseOrdersSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model) => new(
        "Purchase Orders",
        [20d, 34d, 20d, 20d, 16d, 30d, 18d, 22d, 18d, 18d, 18d, 16d, 30d, 18d, 18d, 16d, 20d, 16d, 20d, 20d, 20d, 20d, 20d, 20d, 18d, 18d, 20d, 16d, 16d, 18d, 22d],
        ["شماره سفارش", "عنوان", "درخواست خرید", "قرارداد", "کد تأمین‌کننده", "نام تأمین‌کننده", "نوع طرف", "مبلغ", "سررسید تحویل", "وضعیت سفارش", "کد قلم", "نام قلم", "نوع قلم", "مقدار سفارش", "واحد سفارش", "مقدار پایه سفارش", "واحد پایه", "نسخه تبدیل", "تحویل پایه", "پذیرش پایه", "رد پایه", "قرنطینه پایه", "باقی‌مانده", "پذیرش مازاد", "Fulfillment %", "تاریخ تکمیل", "وضعیت تحویل", "Receipt", "بازرسی در انتظار", "پذیرش خدمت", "شاهد رد/قرنطینه"],
        model.PurchaseOrders.Select(item => new[]
        {
            TextCell(item.Number, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumNumberLength),
            TextCell(item.Title,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumTitleLength),
            TextCell(item.PurchaseRequestNumber, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumNumberLength),
            TextCell(item.ContractNumber, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumNumberLength),
            TextCell(item.PartyCode, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumPartyCodeLength),
            TextCell(item.PartyName,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumPartyNameLength),
            TextCell(PartyTypeText(item.PartyType)),
            NumberCell(item.Amount),
            TextCell(Date(item.DeliveryDueDate)),
            TextCell(OrderState(item.State)),
            TextCell(item.ItemCode, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumItemCodeLength),
            TextCell(item.ItemName,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumItemNameLength),
            TextCell(item.ItemKind.HasValue ? ItemKind(item.ItemKind.Value) : null),
            NumberCell(item.OrderedQuantity),
            TextCell(item.UnitCode, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumUnitLength),
            NumberCell(item.OrderedBaseQuantity),
            TextCell(item.BaseUnit, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumUnitLength),
            NumberCell(item.ConversionVersion),
            NumberCell(item.DeliveredBaseQuantity),
            NumberCell(item.AcceptedBaseQuantity),
            NumberCell(item.RejectedBaseQuantity),
            NumberCell(item.QuarantinedBaseQuantity),
            NumberCell(item.RemainingOrderedQuantity),
            NumberCell(item.AcceptedExcessQuantity),
            NumberCell(item.FulfillmentPercent),
            TextCell(Date(item.CompletionDate)),
            TextCell(DeliveryStatus(item.DeliveryStatus)),
            NumberCell(item.GoodsReceiptCount),
            NumberCell(item.PendingInspectionCount),
            NumberCell(item.ServiceAcceptanceCount),
            NumberCell(item.RejectedOrQuarantinedEvidenceCount)
        }).ToArray());

    private static WorkbookSheet SupplySummarySheet(
        ProjectCommercialProcurementSupplyReportRenderModel model) => new(
        "Supply Summary",
        [18d, 34d, 20d, 18d, 16d, 22d, 22d, 22d, 22d, 22d],
        ["کد قلم", "نام قلم", "نوع", "واحد پایه", "تعداد سفارش", "مقدار سفارش", "تحویل", "پذیرش", "رد", "قرنطینه"],
        model.SupplySummaries.Select(item => new[]
        {
            TextCell(item.ItemCode, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumItemCodeLength),
            TextCell(item.ItemName,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumItemNameLength),
            TextCell(ItemKind(item.ItemKind)),
            TextCell(item.BaseUnit, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumUnitLength),
            NumberCell(item.OrderCount),
            NumberCell(item.OrderedBaseQuantity),
            NumberCell(item.DeliveredBaseQuantity),
            NumberCell(item.AcceptedBaseQuantity),
            NumberCell(item.RejectedBaseQuantity),
            NumberCell(item.QuarantinedBaseQuantity)
        }).ToArray());

    private static WorkbookSheet SuppliersSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model) => new(
        "Suppliers",
        [18d, 34d, 18d, 16d, 16d, 16d, 16d, 20d, 18d, 18d, 18d, 18d, 16d, 18d, 24d, 20d, 22d, 22d, 20d],
        ["کد", "نام", "نوع طرف", "کل سفارش", "باز", "بسته", "لغوشده", "تکمیل قابل ارزیابی", "به‌موقع", "دیرهنگام", "معوق باز", "کسری بسته", "غیرقابل ارزیابی", "Receipt", "بازرسی در انتظار", "Receipt رد/قرنطینه", "پذیرش خدمت", "پذیرش خدمت ردشده", "نرخ به‌موقع %"],
        model.SupplierPerformance.Select(item => new[]
        {
            TextCell(item.PartyCode, leftToRight: true,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumPartyCodeLength),
            TextCell(item.PartyName,
                maximumLength: ProjectCommercialProcurementSupplyReportRenderingContract.MaximumPartyNameLength),
            TextCell(PartyTypeText(item.PartyType)),
            NumberCell(item.IssuedOrderCount),
            NumberCell(item.OpenOrderCount),
            NumberCell(item.ClosedOrderCount),
            NumberCell(item.CancelledOrderCount),
            NumberCell(item.AssessableCompletedOrderCount),
            NumberCell(item.OnTimeFulfilledCount),
            NumberCell(item.LateFulfilledCount),
            NumberCell(item.OverdueOpenCount),
            NumberCell(item.ClosedShortCount),
            NumberCell(item.NotAssessableCount),
            NumberCell(item.GoodsReceiptCount),
            NumberCell(item.PendingInspectionCount),
            NumberCell(item.ReceiptWithRejectedOrQuarantinedCount),
            NumberCell(item.ServiceAcceptanceCount),
            NumberCell(item.ServiceAcceptanceWithRejectedCount),
            NumberCell(item.OnTimeFulfillmentRate)
        }).ToArray());

    private static WorkbookSheet SourceCountsSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var counts = model.Snapshot.SourceCounts;
        var rows = new (string Key, int Value)[]
        {
            ("Party snapshot منبع", counts.PartySnapshotSourceCount),
            ("Party snapshot مؤثر", counts.EffectivePartySnapshotCount),
            ("Item snapshot منبع", counts.ItemSnapshotSourceCount),
            ("Item snapshot مؤثر", counts.EffectiveItemSnapshotCount),
            ("قرارداد منبع", counts.ContractSourceCount),
            ("قرارداد رسمی", counts.OfficialContractCount),
            ("قرارداد در انتظار", counts.PendingContractCount),
            ("اصلاحیه منبع", counts.AmendmentSourceCount),
            ("اصلاحیه مصوب", counts.ApprovedAmendmentCount),
            ("اصلاحیه در انتظار", counts.PendingAmendmentCount),
            ("درخواست خرید منبع", counts.PurchaseRequestSourceCount),
            ("درخواست خرید تا برش", counts.PurchaseRequestAtCutoffCount),
            ("سفارش منبع", counts.PurchaseOrderSourceCount),
            ("سفارش رسمی", counts.OfficialPurchaseOrderCount),
            ("Receipt منبع", counts.GoodsReceiptSourceCount),
            ("Receipt واجد شرایط", counts.EligibleGoodsReceiptCount),
            ("پذیرش خدمت منبع", counts.ServiceAcceptanceSourceCount),
            ("پذیرش خدمت واجد شرایط", counts.EligibleServiceAcceptanceCount),
            ("Collection ناقص", counts.IncompleteCollectionCount)
        };
        return new WorkbookSheet(
            "Source Counts",
            [44d, 18d],
            ["شمارنده", "مقدار"],
            rows.Select(item => new[]
            {
                TextCell(item.Key),
                NumberCell(item.Value)
            }).ToArray());
    }

    private static WorkbookSheet LineageSheet(
        ProjectCommercialProcurementSupplyReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        var configuration = snapshot.Configuration;
        var rows = new (string Key, string Value, bool LeftToRight)[]
        {
            ("مدل قرارداد مؤثر", configuration is null ? "—" : ContractModelText(configuration.ContractModel), false),
            ("Contract state مؤثر", configuration is null ? "—" : PersianReportFormatting.FeatureState(configuration.ContractState), false),
            ("Procurement state مؤثر", configuration is null ? "—" : PersianReportFormatting.FeatureState(configuration.ProcurementState), false),
            ("Base Currency مؤثر", configuration?.BaseCurrencyCode ?? snapshot.Project.CapturedBaseCurrencyCode, true),
            ("نسخه پیکربندی مؤثر", configuration?.ConfigurationVersion.ToString(CultureInfo.InvariantCulture) ?? "—", true),
            ("پروفایل ثبت‌شده UTC", snapshot.Project.ProfileCapturedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), true),
            ("آخرین تغییر منبع UTC", snapshot.SourceMaxChangedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "—", true),
            ("Source Manifest SHA-256", snapshot.SourceManifestSha256, true),
            ("قاعده قرارداد", "فقط قرارداد فعال‌شده و اصلاحیه تصویب‌شده تا زمان برش؛ سقف نامعلوم صفر فرض نمی‌شود", false),
            ("قاعده خرید", "فقط سفارش صادرشده رسمی؛ مبلغ سفارش با پرداخت یا هزینه برابر نیست", false),
            ("قاعده تأمین", "Receipt، بازرسی و پذیرش خدمت مستقل‌اند؛ مقدار مفقود تکمیل‌شده فرض نمی‌شود", false),
            ("قاعده تأمین‌کننده", "فقط شمارنده و نرخ قابل بازتولید؛ بدون امتیاز، رتبه یا توصیه", false),
            ("مرز معنا", "بدون پرداخت، هزینه، پیش‌بینی، مقایسه بودجه، KPI ترکیبی یا Join عملیاتی تازه", false)
        };
        return new WorkbookSheet(
            "Lineage",
            [38d, 96d],
            ["کلید", "مقدار"],
            rows.Select(item => new[]
            {
                TextCell(item.Key),
                TextCell(item.Value, item.LeftToRight)
            }).ToArray());
    }

    private static byte[] ContentTypes(int sheetCount) => Xml(writer =>
    {
        writer.WriteStartElement("Types", ContentTypesNamespace);
        WriteContentType(writer, "Default", "rels", "application/vnd.openxmlformats-package.relationships+xml");
        WriteContentType(writer, "Default", "xml", "application/xml");
        WriteContentType(writer, "Override", "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        for (var index = 1; index <= sheetCount; index++)
        {
            WriteContentType(writer, "Override", $"/xl/worksheets/sheet{index}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        }
        WriteContentType(writer, "Override", "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        WriteContentType(writer, "Override", "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
        WriteContentType(writer, "Override", "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
        writer.WriteEndElement();
    });

    private static byte[] PackageRelationships() => Xml(writer =>
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
        WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
        WriteRelationship(writer, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", "docProps/app.xml");
        writer.WriteEndElement();
    });

    private static byte[] WorkbookRelationships(int sheetCount) => Xml(writer =>
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        for (var index = 1; index <= sheetCount; index++)
        {
            WriteRelationship(writer, $"rId{index}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{index}.xml");
        }
        WriteRelationship(writer, $"rId{sheetCount + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
        writer.WriteEndElement();
    });

    private static byte[] Workbook(IReadOnlyList<WorkbookSheet> sheets) => Xml(writer =>
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        writer.WriteStartElement("bookViews", SpreadsheetNamespace);
        writer.WriteStartElement("workbookView", SpreadsheetNamespace);
        writer.WriteAttributeString("activeTab", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheets", SpreadsheetNamespace);
        for (var index = 0; index < sheets.Count; index++)
        {
            WriteSheet(writer, sheets[index].Name, index + 1, $"rId{index + 1}");
        }
        writer.WriteEndElement();
        writer.WriteStartElement("calcPr", SpreadsheetNamespace);
        writer.WriteAttributeString("calcId", "0");
        writer.WriteAttributeString("calcMode", "manual");
        writer.WriteEndElement();
        writer.WriteEndElement();
    });

    private static byte[] AppProperties() => Xml(writer =>
    {
        writer.WriteStartElement("Properties", ExtendedPropertiesNamespace);
        writer.WriteElementString("Application", ExtendedPropertiesNamespace, "PMCS Certified Reporting");
        writer.WriteElementString("AppVersion", ExtendedPropertiesNamespace, "1.0");
        writer.WriteElementString("Company", ExtendedPropertiesNamespace, "PMCS");
        writer.WriteEndElement();
    });

    private static byte[] CoreProperties(
        ProjectCommercialProcurementSupplyReportRenderModel model) => Xml(writer =>
    {
        writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
        writer.WriteAttributeString("xmlns", "dc", null, DublinCoreNamespace);
        writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteElementString("dc", "title", DublinCoreNamespace, "گزارش رسمی قرارداد، خرید و تأمین پروژه");
        writer.WriteElementString("dc", "creator", DublinCoreNamespace, "PMCS Certified Reporting");
        writer.WriteElementString("dc", "subject", DublinCoreNamespace, model.Request.VerificationCode);
        WriteW3CDate(writer, "created", model.Request.SourceCutoffUtc);
        WriteW3CDate(writer, "modified", model.Request.SourceCutoffUtc);
        writer.WriteEndElement();
    });

    private static byte[] Styles() => Xml(writer =>
    {
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        writer.WriteStartElement("fonts", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WriteFont(writer, bold: false);
        WriteFont(writer, bold: true);
        writer.WriteEndElement();
        writer.WriteStartElement("fills", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "3");
        WritePatternFill(writer, "none", null);
        WritePatternFill(writer, "gray125", null);
        WritePatternFill(writer, "solid", "DCE6F1");
        writer.WriteEndElement();
        writer.WriteStartElement("borders", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WriteBorder(writer, visible: false);
        WriteBorder(writer, visible: true);
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        WriteXf(writer, 0, 0, 0, applyAlignment: false, horizontal: null);
        writer.WriteEndElement();
        writer.WriteStartElement("cellXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "4");
        WriteXf(writer, 0, 0, 0, applyAlignment: true, horizontal: "right");
        WriteXf(writer, 1, 2, 1, applyAlignment: true, horizontal: "center");
        WriteXf(writer, 0, 0, 1, applyAlignment: true, horizontal: "right");
        WriteXf(writer, 0, 0, 1, applyAlignment: true, horizontal: "left");
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyles", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("cellStyle", SpreadsheetNamespace);
        writer.WriteAttributeString("name", "Normal");
        writer.WriteAttributeString("xfId", "0");
        writer.WriteAttributeString("builtinId", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    });

    private static byte[] Worksheet(WorkbookSheet sheet) => Xml(writer =>
    {
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteAttributeString("rightToLeft", "1");
        writer.WriteStartElement("pane", SpreadsheetNamespace);
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        WriteColumns(writer, sheet.Widths);
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        WriteStringRow(writer, 1, sheet.Headers, style: 1);
        for (var index = 0; index < sheet.Rows.Length; index++)
        {
            var rowNumber = index + 2;
            writer.WriteStartElement("row", SpreadsheetNamespace);
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            for (var cellIndex = 0; cellIndex < sheet.Rows[index].Length; cellIndex++)
            {
                WriteCell(writer, rowNumber, cellIndex + 1, sheet.Rows[index][cellIndex]);
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
        writer.WriteAttributeString(
            "ref",
            $"A1:{ColumnName(sheet.Headers.Length)}{Math.Max(1, sheet.Rows.Length + 1)}");
        writer.WriteEndElement();
        writer.WriteEndElement();
    });

    private static WorkbookCell TextCell(
        string? value,
        bool leftToRight = false,
        int maximumLength = 32_000) =>
        new(PersianReportFormatting.SafeSpreadsheetText(value, maximumLength), null, leftToRight);

    private static WorkbookCell NumberCell(decimal? value) => value.HasValue
        ? new(null, value.Value, true)
        : TextCell("—");

    private static WorkbookCell NumberCell(int? value) =>
        NumberCell(value.HasValue ? (decimal?)value.Value : null);

    private static WorkbookCell NumberCell(long? value) =>
        NumberCell(value.HasValue ? (decimal?)value.Value : null);

    private static void WriteCell(XmlWriter writer, int row, int column, WorkbookCell cell)
    {
        if (cell.Number.HasValue)
        {
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", $"{ColumnName(column)}{row}");
            writer.WriteAttributeString("s", "3");
            writer.WriteElementString(
                "v",
                SpreadsheetNamespace,
                cell.Number.Value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            return;
        }
        WriteStringCell(writer, row, column, cell.Text ?? "—", cell.LeftToRight ? 3 : 2);
    }

    private static void WriteColumns(XmlWriter writer, double[] widths)
    {
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var index = 0; index < widths.Length; index++)
        {
            writer.WriteStartElement("col", SpreadsheetNamespace);
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", widths[index].ToString("0.##", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteStringRow(XmlWriter writer, int rowNumber, string[] cells, int style)
    {
        writer.WriteStartElement("row", SpreadsheetNamespace);
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < cells.Length; index++)
        {
            WriteStringCell(writer, rowNumber, index + 1, cells[index], style);
        }
        writer.WriteEndElement();
    }

    private static void WriteStringCell(
        XmlWriter writer,
        int row,
        int column,
        string value,
        int style)
    {
        writer.WriteStartElement("c", SpreadsheetNamespace);
        writer.WriteAttributeString("r", $"{ColumnName(column)}{row}");
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("is", SpreadsheetNamespace);
        writer.WriteStartElement("t", SpreadsheetNamespace);
        writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static string ColumnName(int column)
    {
        var builder = new StringBuilder();
        while (column > 0)
        {
            column--;
            builder.Insert(0, (char)('A' + column % 26));
            column /= 26;
        }
        return builder.ToString();
    }

    private static void WriteContentType(
        XmlWriter writer,
        string element,
        string key,
        string contentType)
    {
        writer.WriteStartElement(element, ContentTypesNamespace);
        writer.WriteAttributeString(element == "Default" ? "Extension" : "PartName", key);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteRelationship(
        XmlWriter writer,
        string id,
        string type,
        string target)
    {
        writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("Type", type);
        writer.WriteAttributeString("Target", target);
        writer.WriteEndElement();
    }

    private static void WriteSheet(
        XmlWriter writer,
        string name,
        int sheetId,
        string relationshipId)
    {
        writer.WriteStartElement("sheet", SpreadsheetNamespace);
        writer.WriteAttributeString("name", name);
        writer.WriteAttributeString("sheetId", sheetId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString(
            "r",
            "id",
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
            relationshipId);
        writer.WriteEndElement();
    }

    private static void WriteFont(XmlWriter writer, bool bold)
    {
        writer.WriteStartElement("font", SpreadsheetNamespace);
        if (bold)
        {
            writer.WriteElementString("b", SpreadsheetNamespace, string.Empty);
        }
        writer.WriteStartElement("sz", SpreadsheetNamespace);
        writer.WriteAttributeString("val", "10");
        writer.WriteEndElement();
        writer.WriteStartElement("name", SpreadsheetNamespace);
        writer.WriteAttributeString("val", "Arial");
        writer.WriteEndElement();
        writer.WriteStartElement("family", SpreadsheetNamespace);
        writer.WriteAttributeString("val", "2");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WritePatternFill(XmlWriter writer, string patternType, string? rgb)
    {
        writer.WriteStartElement("fill", SpreadsheetNamespace);
        writer.WriteStartElement("patternFill", SpreadsheetNamespace);
        writer.WriteAttributeString("patternType", patternType);
        if (rgb is not null)
        {
            writer.WriteStartElement("fgColor", SpreadsheetNamespace);
            writer.WriteAttributeString("rgb", $"FF{rgb}");
            writer.WriteEndElement();
            writer.WriteStartElement("bgColor", SpreadsheetNamespace);
            writer.WriteAttributeString("indexed", "64");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteBorder(XmlWriter writer, bool visible)
    {
        writer.WriteStartElement("border", SpreadsheetNamespace);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            writer.WriteStartElement(side, SpreadsheetNamespace);
            if (visible)
            {
                writer.WriteAttributeString("style", "thin");
                writer.WriteStartElement("color", SpreadsheetNamespace);
                writer.WriteAttributeString("rgb", "FFB7C9E2");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteElementString("diagonal", SpreadsheetNamespace, string.Empty);
        writer.WriteEndElement();
    }

    private static void WriteXf(
        XmlWriter writer,
        int fontId,
        int fillId,
        int borderId,
        bool applyAlignment,
        string? horizontal)
    {
        writer.WriteStartElement("xf", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", "0");
        writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fillId", fillId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("borderId", borderId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("xfId", "0");
        if (applyAlignment)
        {
            writer.WriteAttributeString("applyAlignment", "1");
            writer.WriteStartElement("alignment", SpreadsheetNamespace);
            writer.WriteAttributeString("horizontal", horizontal);
            writer.WriteAttributeString("vertical", "center");
            writer.WriteAttributeString("wrapText", "1");
            writer.WriteAttributeString("readingOrder", horizontal == "left" ? "1" : "2");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteW3CDate(XmlWriter writer, string name, DateTimeOffset value)
    {
        writer.WriteStartElement("dcterms", name, "http://purl.org/dc/terms/");
        writer.WriteAttributeString(
            "xsi",
            "type",
            "http://www.w3.org/2001/XMLSchema-instance",
            "dcterms:W3CDTF");
        writer.WriteString(value.ToUniversalTime().ToString(
            "yyyy-MM-ddTHH:mm:ssZ",
            CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static byte[] Xml(Action<XmlWriter> write)
    {
        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false,
            CloseOutput = false
        };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument();
            write(writer);
            writer.WriteEndDocument();
        }
        return stream.ToArray();
    }

    private static string Date(DateOnly? value) => value.HasValue
        ? PersianReportFormatting.FormatDate(value.Value)
        : "—";

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

    private static string PartyTypeText(PartyType type) => type switch
    {
        PartyType.Supplier => "تأمین‌کننده",
        PartyType.Subcontractor => "پیمانکار جزء",
        PartyType.Consultant => "مشاور",
        PartyType.LaborCrew => "گروه اجرایی",
        PartyType.Client => "کارفرما",
        PartyType.Other => "سایر",
        _ => "نامشخص"
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

    private static ReportRenderingException UnsupportedFormat() => new(
        "reporting.format.unsupported",
        transient: false,
        "The Commercial procurement and supply XLSX renderer received another output format.");

    private sealed record WorkbookEntry(string Name, byte[] Content);

    private sealed record WorkbookSheet(
        string Name,
        double[] Widths,
        string[] Headers,
        WorkbookCell[][] Rows);

    private sealed record WorkbookCell(string? Text, decimal? Number, bool LeftToRight);
}
