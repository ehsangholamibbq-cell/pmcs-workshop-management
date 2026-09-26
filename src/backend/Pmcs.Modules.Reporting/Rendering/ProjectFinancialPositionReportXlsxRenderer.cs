using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectFinancialPositionReportXlsxRenderer(ReportingExecutionOptions execution)
    : IProjectFinancialPositionReportRenderer
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

    public RenderedReportArtifact Render(ProjectFinancialPositionReportRenderRequest request)
    {
        if (request.Format != Format)
        {
            throw UnsupportedFormat();
        }

        try
        {
            var model = ProjectFinancialPositionReportRenderModel.Create(request);
            var semanticRows =
                (model.Snapshot.CashStatus == ProjectFinancialPositionSectionStatus.Available ? 7 : 0) +
                (model.Snapshot.Budget is null ? 0 : 1) +
                (model.Snapshot.PayableSummary is null ? 0 : 1) +
                (model.Snapshot.ReceivableSummary is null ? 0 : 1) +
                model.Aging.Count + model.OpenObligations.Count + 14;
            if (semanticRows > execution.MaximumXlsxRows)
            {
                throw new ReportRenderingException(
                    "reporting.output.row_limit_exceeded",
                    transient: false,
                    "The certified Project Financial Position workbook row limit was exceeded.");
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
                "reporting.project_financial_position.renderer.xlsx_failed",
                transient: false,
                "The certified Project Financial Position workbook could not be rendered.",
                exception);
        }
    }

    private static WorkbookEntry[] BuildEntries(
        ProjectFinancialPositionReportRenderModel model,
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

    private static WorkbookSheet[] BuildSheets(ProjectFinancialPositionReportRenderModel model) =>
    [
        MetadataSheet(model),
        CashSheet(model),
        BudgetSheet(model),
        SummariesSheet(model),
        AgingSheet(model),
        OpenObligationsSheet(model),
        SourceCountsSheet(model),
        LineageSheet(model)
    ];

    private static WorkbookSheet MetadataSheet(ProjectFinancialPositionReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        var request = model.Request;
        var reasonText = model.ReasonCodes.Count == 0
            ? "—"
            : string.Join("؛ ", model.ReasonCodes.Select(ReasonCode));
        var rows = new (string Key, string Value, bool LeftToRight)[]
        {
            ("عنوان", "گزارش رسمی وضعیت مالی پروژه", false),
            ("کد پروژه", snapshot.Project.Code, false),
            ("نام پروژه", snapshot.Project.Name, false),
            ("منطقه زمانی", snapshot.Project.TimeZone, true),
            ("ارز پایه", snapshot.Project.CapturedBaseCurrencyCode, true),
            ("تاریخ برش شمسی", PersianReportFormatting.FormatDate(snapshot.Cutoff.CutoffLocalDate), false),
            ("تاریخ برش ISO", snapshot.Cutoff.CutoffLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), true),
            ("زمان برش محلی", PersianReportFormatting.FormatInstant(snapshot.Cutoff.SourceCutoffUtc, snapshot.Project.TimeZone), false),
            ("زمان برش UTC", snapshot.Cutoff.SourceCutoffUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), true),
            ("وضعیت داده", PersianReportFormatting.DataStatus(snapshot.DataStatus), false),
            ("وضعیت Cash", SectionStatus(snapshot.CashStatus), false),
            ("وضعیت تعهدات", SectionStatus(snapshot.ObligationStatus), false),
            ("وضعیت Budget", SectionStatus(snapshot.BudgetStatus), false),
            ("وضعیت مقایسه Budget", SectionStatus(snapshot.BudgetComparisonStatus), false),
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
            [34d, 88d],
            ["کلید", "مقدار"],
            rows.Select(item => new[]
            {
                TextCell(item.Key),
                TextCell(item.Value, item.LeftToRight)
            }).ToArray());
    }

    private static WorkbookSheet CashSheet(ProjectFinancialPositionReportRenderModel model)
    {
        var cash = model.Snapshot.Cash;
        WorkbookCell[][] rows = model.Snapshot.CashStatus ==
                ProjectFinancialPositionSectionStatus.Available
            ?
            [
                [TextCell("دریافت رسمی"), NumberCell(cash.TotalReceipts)],
                [TextCell("پرداخت مستقیم"), NumberCell(cash.DirectPayments)],
                [TextCell("تأمین تنخواه"), NumberCell(cash.PettyCashFunding)],
                [TextCell("هزینه تنخواه"), NumberCell(cash.PettyCashExpenses)],
                [TextCell("جریان نقد بیرونی خالص"), NumberCell(cash.ExternalNetCash)],
                [TextCell("هزینه شناسایی‌شده"), NumberCell(cash.RecognizedSpend)],
                [TextCell("مانده تنخواه"), NumberCell(cash.PettyCashBalance)]
            ]
            : [];
        return new WorkbookSheet(
            "Cash",
            [38d, 24d],
            ["شاخص", $"مبلغ ({model.Snapshot.Project.CapturedBaseCurrencyCode})"],
            rows);
    }

    private static WorkbookSheet BudgetSheet(ProjectFinancialPositionReportRenderModel model)
    {
        var budget = model.Snapshot.Budget;
        var comparison = model.Snapshot.BudgetComparison;
        WorkbookCell[][] rows = budget is null || comparison is null
            ? []
            :
            [
                [
                    NumberCell(budget.Revision),
                    NumberCell(budget.Amount),
                    TextCell(budget.CurrencyCode, leftToRight: true),
                    TextCell(PersianReportFormatting.FormatInstant(
                        budget.ApprovedAt,
                        model.Snapshot.Project.TimeZone)),
                    TextCell(budget.SupersededAt.HasValue
                        ? PersianReportFormatting.FormatInstant(
                            budget.SupersededAt.Value,
                            model.Snapshot.Project.TimeZone)
                        : null),
                    NumberCell(comparison.ApprovedBudgetAmount),
                    NumberCell(comparison.BudgetRemainingAmount),
                    NumberCell(comparison.BudgetConsumedPercent),
                    TextCell(SectionStatus(model.Snapshot.BudgetComparisonStatus))
                ]
            ];
        return new WorkbookSheet(
            "Budget",
            [14d, 22d, 12d, 24d, 24d, 22d, 22d, 18d, 24d],
            ["Revision", "مبلغ Baseline", "ارز", "تصویب", "جایگزینی", "Budget مصوب", "مانده Budget", "مصرف %", "وضعیت مقایسه"],
            rows);
    }

    private static WorkbookSheet SummariesSheet(ProjectFinancialPositionReportRenderModel model)
    {
        var summaries = new[]
        {
            model.Snapshot.PayableSummary,
            model.Snapshot.ReceivableSummary
        }.Where(item => item is not null).Cast<ProjectFinancialPositionReportObligationSummary>();
        return new WorkbookSheet(
            "Summaries",
            [20d, 18d, 24d, 18d, 24d],
            ["نوع", "تعداد باز", "مبلغ باز", "تعداد سررسیدگذشته", "مبلغ سررسیدگذشته"],
            summaries.Select(item => new[]
            {
                TextCell(ObligationType(item.Type)),
                NumberCell(item.OpenCount),
                NumberCell(item.OpenAmount),
                NumberCell(item.OverdueCount),
                NumberCell(item.OverdueAmount)
            }).ToArray());
    }

    private static WorkbookSheet AgingSheet(ProjectFinancialPositionReportRenderModel model) => new(
        "Aging",
        [20d, 24d, 18d, 24d],
        ["نوع", "بازه سررسید", "تعداد", $"مبلغ ({model.Snapshot.Project.CapturedBaseCurrencyCode})"],
        model.Aging.Select(item => new[]
        {
            TextCell(ObligationType(item.Type)),
            TextCell(AgingBucket(item.Bucket)),
            NumberCell(item.Count),
            NumberCell(item.Amount)
        }).ToArray());

    private static WorkbookSheet OpenObligationsSheet(
        ProjectFinancialPositionReportRenderModel model) => new(
        "Open Obligations",
        [18d, 24d, 36d, 17d, 17d, 22d, 22d, 22d, 24d],
        ["نوع", "شماره", "طرف حساب", "تاریخ صدور", "تاریخ سررسید", "مبلغ", "تسویه تا برش", "مانده", "بازه سررسید"],
        model.OpenObligations.Select(item => new[]
        {
            TextCell(ObligationType(item.Type)),
            TextCell(item.NumberSnapshot, leftToRight: true,
                maximumLength: ProjectFinancialPositionReportRenderingContract.MaximumObligationNumberLength),
            TextCell(item.CounterpartySnapshot,
                maximumLength: ProjectFinancialPositionReportRenderingContract.MaximumCounterpartyLength),
            TextCell(PersianReportFormatting.FormatDate(item.IssueDate)),
            TextCell(PersianReportFormatting.FormatDate(item.DueDate)),
            NumberCell(item.Amount),
            NumberCell(item.SettledAmountAtCutoff),
            NumberCell(item.OutstandingAmount),
            TextCell(AgingBucket(item.Bucket))
        }).ToArray());

    private static WorkbookSheet SourceCountsSheet(ProjectFinancialPositionReportRenderModel model)
    {
        var counts = model.Snapshot.SourceCounts;
        var rows = new (string Key, int Value)[]
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
            ("Budget Baseline منبع", counts.BudgetBaselineSourceCount),
            ("Budget Baseline مؤثر", counts.EffectiveBudgetBaselineCount),
            ("Budget Baseline حذف‌شده", counts.ExcludedBudgetBaselineCount),
            ("Collection ناقص", counts.IncompleteCollectionCount)
        };
        return new WorkbookSheet(
            "Source Counts",
            [42d, 18d],
            ["شمارنده", "مقدار"],
            rows.Select(item => new[]
            {
                TextCell(item.Key),
                NumberCell(item.Value)
            }).ToArray());
    }

    private static WorkbookSheet LineageSheet(ProjectFinancialPositionReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        var configuration = snapshot.Configuration;
        var rows = new (string Key, string Value, bool LeftToRight)[]
        {
            ("Finance state مؤثر", configuration is null ? "—" : FeatureState(configuration.FinanceState), false),
            ("Budget state مؤثر", configuration is null ? "—" : FeatureState(configuration.BudgetState), false),
            ("Base Currency مؤثر", configuration?.BaseCurrencyCode ?? snapshot.Project.CapturedBaseCurrencyCode, true),
            ("نسخه پیکربندی مؤثر", configuration?.ConfigurationVersion.ToString(CultureInfo.InvariantCulture) ?? "—", true),
            ("پروفایل ثبت‌شده UTC", snapshot.Project.ProfileCapturedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), true),
            ("آخرین تغییر منبع UTC", snapshot.SourceMaxChangedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "—", true),
            ("Source Manifest SHA-256", snapshot.SourceManifestSha256, true),
            ("قاعده Cash", "دریافت، پرداخت مستقیم، تأمین و هزینه تنخواه بدون ساخت مقدار مفقود", false),
            ("قاعده تعهد", "پرداختنی و دریافتنی جدا نگه داشته می‌شوند و با هم تهاتر نمی‌شوند", false),
            ("قاعده Budget", "مقایسه فقط با Cash رسمی موجود؛ مانده منفی و مصرف بالاتر از صد حفظ می‌شود", false),
            ("مرز معنا", "بدون تسعیر ارز، پیش‌بینی، ارزش کسب‌شده، کارمزد مدیریت یا امتیاز سلامت", false)
        };
        return new WorkbookSheet(
            "Lineage",
            [38d, 88d],
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

    private static byte[] CoreProperties(ProjectFinancialPositionReportRenderModel model) => Xml(writer =>
    {
        writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
        writer.WriteAttributeString("xmlns", "dc", null, DublinCoreNamespace);
        writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteElementString("dc", "title", DublinCoreNamespace, "گزارش رسمی وضعیت مالی پروژه");
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

    private static string FeatureState(ProjectFeatureState state) => state switch
    {
        ProjectFeatureState.Active => "فعال",
        ProjectFeatureState.NotConfigured => "پیکربندی نشده",
        ProjectFeatureState.NotEnabled => "فعال نشده",
        ProjectFeatureState.SetupRequired => "نیازمند راه‌اندازی",
        ProjectFeatureState.Suspended => "تعلیق‌شده",
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

    private static ReportRenderingException UnsupportedFormat() => new(
        "reporting.format.unsupported",
        transient: false,
        "The Project Financial Position XLSX renderer received another output format.");

    private sealed record WorkbookEntry(string Name, byte[] Content);

    private sealed record WorkbookSheet(
        string Name,
        double[] Widths,
        string[] Headers,
        WorkbookCell[][] Rows);

    private sealed record WorkbookCell(string? Text, decimal? Number, bool LeftToRight);
}
