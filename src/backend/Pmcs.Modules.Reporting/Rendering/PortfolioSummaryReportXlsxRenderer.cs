using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class PortfolioSummaryReportXlsxRenderer(
    ReportingExecutionOptions execution) : IPortfolioSummaryReportRenderer
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

    public RenderedReportArtifact Render(PortfolioSummaryReportRenderRequest request)
    {
        if (request.Format != Format) throw UnsupportedFormat();
        try
        {
            var model = PortfolioSummaryReportRenderModel.Create(request);
            if (model.Snapshot.Projects.Count * 3 + model.Snapshot.CurrencyGroups.Count + 60 >
                execution.MaximumXlsxRows)
                throw new ReportRenderingException("reporting.output.row_limit_exceeded",
                    transient: false, "The F10 workbook row limit was exceeded.");
            var sheets = BuildSheets(model);
            var entries = BuildEntries(model, sheets);
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var item in entries)
                {
                    var entry = archive.CreateEntry(item.Name, CompressionLevel.NoCompression);
                    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var destination = entry.Open();
                    destination.Write(item.Content);
                }
            }
            var bytes = output.ToArray();
            return new RenderedReportArtifact(Format, request.FileName,
                ReportArtifactIdentity.ContentType(Format), bytes,
                ReportArtifactIdentity.Sha256(bytes), request.ManifestSha256,
                request.VerificationCode);
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception)
        {
            throw new ReportRenderingException("reporting.portfolio.renderer.xlsx_failed",
                transient: false, "The certified F10 workbook could not be rendered.", exception);
        }
    }

    private static WorkbookEntry[] BuildEntries(PortfolioSummaryReportRenderModel model,
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
            entries.Add(new WorkbookEntry($"xl/worksheets/sheet{index + 1}.xml",
                Worksheet(sheets[index])));
        return entries.ToArray();
    }

    private static WorkbookSheet[] BuildSheets(PortfolioSummaryReportRenderModel model) =>
        [MetadataSheet(model), CoverageSheet(model), CurrencySheet(model),
            ProjectSheet(model), FinanceSheet(model), CommercialSheet(model)];

    private static WorkbookSheet MetadataSheet(PortfolioSummaryReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        var request = model.Request;
        var rows = new (string Key, string Value)[]
        {
            ("عنوان", "گزارش رسمی خلاصهٔ مجموعه پروژه‌ها"),
            ("برش UTC", snapshot.AsOfUtc.ToString("O", CultureInfo.InvariantCulture)),
            ("وضعیت داده", PersianReportFormatting.DataStatus(snapshot.DataStatus)),
            ("طبقه‌بندی", PersianReportFormatting.Classification(request.Classification)),
            ("شمار پروژه‌های مجاز", snapshot.AuthorizedProjectCount.ToString(CultureInfo.InvariantCulture)),
            ("ارز", "هر واحد مستقل؛ بدون تبدیل و بدون جمع کلی"),
            ("کد تعریف", request.DefinitionCode),
            ("نسخه تعریف", request.DefinitionVersion),
            ("نسخه قالب", request.TemplateVersion),
            ("قرارداد Renderer", request.RendererContractVersion),
            ("قرارداد Layout", request.LayoutContractVersion),
            ("نسخه Snapshot", snapshot.SchemaVersion),
            ("Template Content SHA-256", request.TemplateContentDigest),
            ("Snapshot SHA-256", request.SnapshotSha256),
            ("Source Manifest SHA-256", request.SourceManifestSha256),
            ("Output Manifest SHA-256", request.ManifestSha256),
            ("کد راستی‌آزمایی", request.VerificationCode),
            ("Run ID", request.RunId.ToString()),
            ("Snapshot ID", request.SnapshotId.ToString()),
            ("Output ID", request.OutputId.ToString()),
            ("Template Version ID", request.TemplateVersionId.ToString())
        };
        return new WorkbookSheet("Metadata", [36d, 110d], ["کلید", "مقدار"],
            rows.Select(item => new[] { TextCell(item.Key), TextCell(item.Value) }).ToArray());
    }

    private static WorkbookSheet CoverageSheet(PortfolioSummaryReportRenderModel model)
    {
        var projects = model.Snapshot.Projects;
        var rows = new[]
        {
            new[] { TextCell("عملیات"), NumberCell(projects.Count(x =>
                x.OperationalStatus == PortfolioDimensionStatus.Available)),
                NumberCell(projects.Count(x => x.OperationalStatus == PortfolioDimensionStatus.InsufficientData)) },
            new[] { TextCell("مالی"), NumberCell(projects.Count(x =>
                x.Financial.Status == PortfolioDimensionStatus.Available)),
                NumberCell(projects.Count(x => x.Financial.Status == PortfolioDimensionStatus.InsufficientData)) },
            new[] { TextCell("تجاری"), NumberCell(projects.Count(x =>
                x.Commercial.Status == PortfolioDimensionStatus.Available)),
                NumberCell(projects.Count(x => x.Commercial.Status == PortfolioDimensionStatus.InsufficientData)) }
        };
        return new WorkbookSheet("Coverage", [30d, 30d, 30d],
            ["بعد", "مشارکت رسمی", "پوشش ناکافی"], rows);
    }

    private static WorkbookSheet CurrencySheet(PortfolioSummaryReportRenderModel model) =>
        new("Currencies", [17d, 20d, 27d, 27d, 20d, 27d, 27d, 22d, 22d],
            ["ارز", "مشارکت مالی", "هزینه شناسایی‌شده", "خالص نقد خارجی",
                "مشارکت تجاری", "تعهد کل", "تعهد باز", "مالی ناقص", "تجاری ناقص"],
            model.Snapshot.CurrencyGroups.Select(group => new[]
            {
                TextCell(group.CurrencyCode, true), NumberCell(group.FinancialContributorCount),
                group.HasIncompleteFinancial ? TextCell("نامعلوم؛ صفر فرض نشود") :
                    NumberOrUnknown(group.RecognizedSpendSubtotal),
                group.HasIncompleteFinancial ? TextCell("نامعلوم؛ صفر فرض نشود") :
                    NumberOrUnknown(group.ExternalNetCashSubtotal),
                NumberCell(group.CommercialContributorCount),
                group.HasIncompleteCommercial ? TextCell("نامعلوم؛ صفر فرض نشود") :
                    NumberOrUnknown(group.TotalCommittedSubtotal),
                group.HasIncompleteCommercial ? TextCell("نامعلوم؛ صفر فرض نشود") :
                    NumberOrUnknown(group.OpenCommitmentSubtotal),
                TextCell(group.HasIncompleteFinancial ? "بله" : "خیر"),
                TextCell(group.HasIncompleteCommercial ? "بله" : "خیر")
            }).ToArray());

    private static WorkbookSheet ProjectSheet(PortfolioSummaryReportRenderModel model) =>
        new("Projects", [25d, 40d, 20d, 24d, 25d, 28d, 22d, 22d, 22d, 16d, 26d, 70d],
            ["کد", "نام", "چرخه", "برش محلی", "منطقه زمانی", "عملیات", "ارزیابی",
                "پوشش", "تازگی", "جزئی", "طبقه‌بندی", "Source SHA-256"],
            model.Snapshot.Projects.Select(project => new[]
            {
                TextCell(PortfolioSummaryReportFormatting.Code(project)),
                TextCell(PortfolioSummaryReportFormatting.Name(project)),
                TextCell(project.Lifecycle.ToString()),
                TextCell(PersianReportFormatting.FormatDate(project.CutoffLocalDate)),
                TextCell(project.TimeZone, true),
                TextCell(PortfolioSummaryReportFormatting.Status(project.OperationalStatus)),
                TextCell(project.OperationalAssessment.HasValue ? PersianReportFormatting.OperationalStatus(
                    project.OperationalAssessment.Value) : "—"),
                TextCell(project.Coverage.HasValue ? PersianReportFormatting.CoverageStatus(
                    project.Coverage.Value) : "—"),
                TextCell(project.Freshness.HasValue ? PersianReportFormatting.FreshnessStatus(
                    project.Freshness.Value) : "—"),
                TextCell(project.IsPartial.HasValue ? (project.IsPartial.Value ? "بله" : "خیر") : "—"),
                TextCell(PersianReportFormatting.Classification(project.Classification)),
                TextCell(project.OperationalSourceSha256, true)
            }).ToArray());

    private static WorkbookSheet FinanceSheet(PortfolioSummaryReportRenderModel model) =>
        new("Finance", [28d, 18d, 34d, 30d, 30d, 65d, 68d],
            ["پروژه", "ارز", "وضعیت", "هزینه شناسایی‌شده", "خالص نقد خارجی", "علت", "Source SHA-256"],
            model.Snapshot.Projects.Select(project => new[]
            {
                TextCell(PortfolioSummaryReportFormatting.Code(project)),
                TextCell(project.Financial.CurrencyCode, true),
                TextCell(PortfolioSummaryReportFormatting.Status(project.Financial.Status)),
                project.Financial.Status == PortfolioDimensionStatus.NotAuthorized ? TextCell("—") :
                    NumberOrUnknown(project.Financial.RecognizedSpend),
                project.Financial.Status == PortfolioDimensionStatus.NotAuthorized ? TextCell("—") :
                    NumberOrUnknown(project.Financial.ExternalNetCash),
                TextCell(project.Financial.ReasonCode),
                TextCell(project.Financial.SourceManifestSha256, true)
            }).ToArray());

    private static WorkbookSheet CommercialSheet(PortfolioSummaryReportRenderModel model) =>
        new("Commercial", [28d, 18d, 34d, 30d, 30d, 65d, 68d],
            ["پروژه", "ارز", "وضعیت", "تعهد کل", "تعهد باز", "علت", "Source SHA-256"],
            model.Snapshot.Projects.Select(project => new[]
            {
                TextCell(PortfolioSummaryReportFormatting.Code(project)),
                TextCell(project.Commercial.CurrencyCode, true),
                TextCell(PortfolioSummaryReportFormatting.Status(project.Commercial.Status)),
                project.Commercial.Status == PortfolioDimensionStatus.NotAuthorized ? TextCell("—") :
                    NumberOrUnknown(project.Commercial.TotalCommittedAmount),
                project.Commercial.Status == PortfolioDimensionStatus.NotAuthorized ? TextCell("—") :
                    NumberOrUnknown(project.Commercial.OpenCommitmentAmount),
                TextCell(project.Commercial.ReasonCode),
                TextCell(project.Commercial.SourceManifestSha256, true)
            }).ToArray());

    private static WorkbookCell NumberOrUnknown(decimal? value) => value.HasValue
        ? NumberCell(value) : TextCell("نامعلوم؛ صفر فرض نشود");

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
        PortfolioSummaryReportRenderModel model) => Xml(writer =>
    {
        writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
        writer.WriteAttributeString("xmlns", "dc", null, DublinCoreNamespace);
        writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteElementString("dc", "title", DublinCoreNamespace, "گزارش رسمی خلاصهٔ مجموعه پروژه‌ها");
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
        writer.WriteAttributeString("val", PmcsTypographyContract.XlsxFamily);
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

    private static ReportRenderingException UnsupportedFormat() => new(
        "reporting.format.unsupported", transient: false,
        "The F10 XLSX renderer received another output format.");

    private sealed record WorkbookEntry(string Name, byte[] Content);
    private sealed record WorkbookSheet(string Name, double[] Widths,
        string[] Headers, WorkbookCell[][] Rows);
    private sealed record WorkbookCell(string? Text, decimal? Number, bool LeftToRight);
}
