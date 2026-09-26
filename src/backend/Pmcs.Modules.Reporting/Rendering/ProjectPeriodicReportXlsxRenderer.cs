using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class ProjectPeriodicReportXlsxRenderer(ReportingExecutionOptions execution)
    : IProjectPeriodicReportRenderer
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

    public RenderedReportArtifact Render(ProjectPeriodicReportRenderRequest request)
    {
        if (request.Format != Format)
        {
            throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                "The project-periodic XLSX renderer received another output format.");
        }

        try
        {
            var model = ProjectPeriodicReportRenderModel.Create(request);
            if (model.Facts.Count > execution.MaximumXlsxRows)
            {
                throw new ReportRenderingException(
                    "reporting.output.row_limit_exceeded",
                    transient: false,
                    "The certified project-periodic workbook row limit was exceeded.");
            }

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
                "reporting.periodic.renderer.xlsx_failed",
                transient: false,
                "The certified project-periodic workbook could not be rendered.",
                exception);
        }
    }

    private static WorkbookEntry[] BuildEntries(
        ProjectPeriodicReportRenderModel model,
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

    private static WorkbookSheet[] BuildSheets(ProjectPeriodicReportRenderModel model) =>
    [
        MetadataSheet(model),
        CoverageSheet(model),
        ReportsSheet(model),
        FactsSheet(model),
        FactCountsSheet(model),
        QuantitiesSheet(model),
        ResourcesSheet(model),
        HighImpactSheet(model)
    ];

    private static WorkbookSheet MetadataSheet(ProjectPeriodicReportRenderModel model)
    {
        var snapshot = model.Snapshot;
        var request = model.Request;
        var metricsAvailable = snapshot.DataStatus is ReportDataStatus.Available or ReportDataStatus.InsufficientData;
        var reasonText = model.ReasonCodes.Count == 0
            ? "—"
            : string.Join("؛ ", model.ReasonCodes.Select(PersianReportFormatting.ReasonCode));
        var rows = new (string Key, string Value, bool LeftToRight)[]
        {
            ("عنوان", $"گزارش {PersianReportFormatting.PeriodKind(snapshot.Period.Kind)} رسمی پروژه", false),
            ("کد پروژه", snapshot.Project.Code, false),
            ("نام پروژه", snapshot.Project.Name, false),
            ("منطقه زمانی", snapshot.Project.TimeZone, true),
            ("نوع دوره", PersianReportFormatting.PeriodKind(snapshot.Period.Kind), false),
            ("شروع دوره شمسی", PersianReportFormatting.FormatDate(snapshot.Period.StartLocalDate), false),
            ("پایان دوره شمسی", PersianReportFormatting.FormatDate(snapshot.Period.EndLocalDateExclusive.AddDays(-1)), false),
            ("شروع دوره ISO", snapshot.Period.StartLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), true),
            ("پایان انحصاری دوره ISO", snapshot.Period.EndLocalDateExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), true),
            ("زمان برش محلی", PersianReportFormatting.FormatInstant(request.SourceCutoffUtc, snapshot.Project.TimeZone), false),
            ("زمان برش UTC", request.SourceCutoffUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), true),
            ("دوره در زمان برش بسته است", PersianReportFormatting.YesNo(snapshot.Period.ClosedAtCutoff), false),
            ("وضعیت داده", PersianReportFormatting.DataStatus(snapshot.DataStatus), false),
            ("علت‌های وضعیت", reasonText, false),
            ("نوبت مورد انتظار", metricsAvailable ? FormatNullable(snapshot.Coverage.ExpectedSlotCount) : "—", false),
            ("نوبت پوشش‌داده‌شده", metricsAvailable ? FormatNullable(snapshot.Coverage.CoveredSlotCount) : "—", false),
            ("تعداد گزارش رسمی", metricsAvailable ? model.OfficialReports.Count.ToString(CultureInfo.InvariantCulture) : "—", false),
            ("تعداد Fact رسمی", metricsAvailable ? model.Facts.Count.ToString(CultureInfo.InvariantCulture) : "—", false),
            ("نسخه پروژه", snapshot.Project.Revision.ToString(CultureInfo.InvariantCulture), true),
            ("نسخه پیکربندی پروژه", snapshot.Project.ConfigurationVersion.ToString(CultureInfo.InvariantCulture), true),
            ("کد تعریف", request.DefinitionCode, true),
            ("نسخه تعریف", request.DefinitionVersion, true),
            ("نسخه قالب", request.TemplateVersion, true),
            ("قرارداد Renderer", request.RendererContractVersion, true),
            ("قرارداد Layout", request.LayoutContractVersion, true),
            ("نسخه Snapshot", snapshot.SchemaVersion, true),
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
            [30d, 82d],
            ["کلید", "مقدار"],
            rows.Select(item => new[]
            {
                TextCell(item.Key),
                TextCell(item.Value, item.LeftToRight)
            }).ToArray());
    }

    private static WorkbookSheet CoverageSheet(ProjectPeriodicReportRenderModel model) => new(
        "Coverage",
        [15d, 14d, 14d, 14d, 24d, 12d, 44d],
        ["نوبت شمسی", "نوبت ISO", "شروع ISO", "پایان انحصاری ISO", "موعد محلی", "پوشش", "تاریخ‌های گزارش رسمی"],
        model.CoverageSlots.Select(slot => new[]
        {
            TextCell(PersianReportFormatting.FormatDate(slot.SlotDate)),
            TextCell(slot.SlotDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), leftToRight: true),
            TextCell(slot.StartLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), leftToRight: true),
            TextCell(slot.EndLocalDateExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), leftToRight: true),
            TextCell(PersianReportFormatting.FormatInstant(slot.DueAtUtc, model.Snapshot.Project.TimeZone)),
            TextCell(slot.Covered ? "پوشش داده شد" : "فاقد پوشش"),
            TextCell(FormatDates(slot.OfficialReportDates))
        }).ToArray());

    private static WorkbookSheet ReportsSheet(ProjectPeriodicReportRenderModel model) => new(
        "Reports",
        [14d, 14d, 12d, 18d, 22d, 48d, 16d, 18d, 20d, 38d, 38d, 38d],
        ["تاریخ شمسی", "تاریخ ISO", "نسخه", "طبقه‌بندی", "محل", "شرح روزانه", "وضعیت نسخه", "تعداد Fact", "علت اصلاح", "Root Report ID", "Report ID", "Supersedes Report ID"],
        model.OfficialReports.Select(report => new[]
        {
            TextCell(PersianReportFormatting.FormatDate(report.Version.ReportDate)),
            TextCell(report.Version.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), leftToRight: true),
            NumberCell(report.Version.VersionNumber),
            TextCell(PersianReportFormatting.Classification(report.Classification)),
            TextCell(report.Version.LocationName),
            TextCell(report.Version.Narrative),
            TextCell(PersianReportFormatting.VersionState(report.Version.State)),
            NumberCell(report.Version.Facts.Count),
            TextCell(report.Version.CorrectionReason),
            TextCell(report.Version.RootReportId.ToString(), leftToRight: true),
            TextCell(report.Version.ReportId.ToString(), leftToRight: true),
            TextCell(report.Version.SupersedesReportId?.ToString(), leftToRight: true)
        }).ToArray());

    private static WorkbookSheet FactsSheet(ProjectPeriodicReportRenderModel model) => new(
        "Facts",
        [14d, 14d, 18d, 18d, 24d, 18d, 54d, 20d, 14d, 12d, 14d, 14d, 14d, 18d, 38d, 38d, 38d, 38d],
        ["تاریخ شمسی", "تاریخ ISO", "طبقه‌بندی", "نوع Fact", "محل", "دسته", "شرح", "کد مرجع", "مقدار", "واحد", "تعداد منبع", "ساعت", "اثر", "نسخه", "Measurement Item ID", "Fact ID", "Copied From Fact ID", "Report ID"],
        model.Facts.Select(item => new[]
        {
            TextCell(PersianReportFormatting.FormatDate(item.Version.ReportDate)),
            TextCell(item.Version.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), leftToRight: true),
            TextCell(PersianReportFormatting.Classification(item.Classification)),
            TextCell(PersianReportFormatting.FactKind(item.Fact.Kind)),
            TextCell(item.Fact.LocationName ?? item.Version.LocationName),
            TextCell(item.Fact.Category),
            TextCell(item.Fact.Description, maximumLength: 4_000),
            TextCell(item.Fact.ReferenceCode),
            NumberCell(item.Fact.Quantity),
            TextCell(item.Fact.Unit),
            NumberCell(item.Fact.ResourceCount),
            NumberCell(item.Fact.Hours),
            TextCell(PersianReportFormatting.Impact(item.Fact.ImpactLevel)),
            NumberCell(item.Version.VersionNumber),
            TextCell(item.Fact.MeasurementItemId?.ToString(), leftToRight: true),
            TextCell(item.Fact.FactId.ToString(), leftToRight: true),
            TextCell(item.Fact.CopiedFromFactId?.ToString(), leftToRight: true),
            TextCell(item.Version.ReportId.ToString(), leftToRight: true)
        }).ToArray());

    private static WorkbookSheet FactCountsSheet(ProjectPeriodicReportRenderModel model) => new(
        "Fact Counts",
        [24d, 16d],
        ["نوع Fact", "تعداد"],
        model.FactCounts.Select(item => new[]
        {
            TextCell(PersianReportFormatting.FactKind(item.Kind)),
            NumberCell(item.Count)
        }).ToArray());

    private static WorkbookSheet QuantitiesSheet(ProjectPeriodicReportRenderModel model) => new(
        "Quantities",
        [24d, 18d, 18d, 18d],
        ["نوع Fact", "مقدار", "واحد منبع", "وضعیت واحد"],
        model.QuantityTotals.Select(item => new[]
        {
            TextCell(PersianReportFormatting.FactKind(item.Kind)),
            NumberCell(item.Quantity),
            TextCell(item.SourceUnit),
            TextCell(PersianReportFormatting.UnitState(item.UnitState))
        }).ToArray());

    private static WorkbookSheet ResourcesSheet(ProjectPeriodicReportRenderModel model) => new(
        "Resources",
        [24d, 18d, 18d],
        ["نوع Fact", "تعداد منبع", "ساعت"],
        model.ResourceObservationTotals.Select(item => new[]
        {
            TextCell(PersianReportFormatting.FactKind(item.Kind)),
            NumberCell(item.ResourceCount),
            NumberCell(item.Hours)
        }).ToArray());

    private static WorkbookSheet HighImpactSheet(ProjectPeriodicReportRenderModel model) => new(
        "High Impact",
        [14d, 14d, 18d, 14d, 54d, 20d, 24d, 38d, 38d],
        ["تاریخ شمسی", "تاریخ ISO", "نوع Fact", "اثر", "شرح", "کد مرجع", "محل", "Fact ID", "Report ID"],
        model.HighImpactFacts.Select(item => new[]
        {
            TextCell(PersianReportFormatting.FormatDate(item.ReportDate)),
            TextCell(item.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), leftToRight: true),
            TextCell(PersianReportFormatting.FactKind(item.Fact.Kind)),
            TextCell(PersianReportFormatting.Impact(item.Fact.ImpactLevel)),
            TextCell(item.Fact.Description),
            TextCell(item.Fact.ReferenceCode),
            TextCell(item.Fact.LocationName),
            TextCell(item.Fact.FactId.ToString(), leftToRight: true),
            TextCell(item.ReportId.ToString(), leftToRight: true)
        }).ToArray());

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
            WriteRelationship(
                writer,
                $"rId{index}",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet",
                $"worksheets/sheet{index}.xml");
        }
        WriteRelationship(
            writer,
            $"rId{sheetCount + 1}",
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles",
            "styles.xml");
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

    private static byte[] CoreProperties(ProjectPeriodicReportRenderModel model) => Xml(writer =>
    {
        writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
        writer.WriteAttributeString("xmlns", "dc", null, DublinCoreNamespace);
        writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteElementString(
            "dc",
            "title",
            DublinCoreNamespace,
            $"گزارش {PersianReportFormatting.PeriodKind(model.Snapshot.Period.Kind)} رسمی پروژه");
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

    private static string FormatNullable(int? value) => value.HasValue
        ? value.Value.ToString(CultureInfo.InvariantCulture)
        : "—";

    private static string FormatDates(IEnumerable<DateOnly> dates)
    {
        var values = dates.OrderBy(item => item)
            .Select(item => PersianReportFormatting.FormatDate(item))
            .ToArray();
        return values.Length == 0 ? "—" : string.Join("، ", values);
    }

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

    private static void WriteStringCell(XmlWriter writer, int row, int column, string value, int style)
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

    private static void WriteContentType(XmlWriter writer, string element, string key, string contentType)
    {
        writer.WriteStartElement(element, ContentTypesNamespace);
        writer.WriteAttributeString(element == "Default" ? "Extension" : "PartName", key);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
    {
        writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
        writer.WriteAttributeString("Id", id);
        writer.WriteAttributeString("Type", type);
        writer.WriteAttributeString("Target", target);
        writer.WriteEndElement();
    }

    private static void WriteSheet(XmlWriter writer, string name, int sheetId, string relationshipId)
    {
        writer.WriteStartElement("sheet", SpreadsheetNamespace);
        writer.WriteAttributeString("name", name);
        writer.WriteAttributeString("sheetId", sheetId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", relationshipId);
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
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "dcterms:W3CDTF");
        writer.WriteString(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
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

    private sealed record WorkbookEntry(string Name, byte[] Content);

    private sealed record WorkbookSheet(
        string Name,
        double[] Widths,
        string[] Headers,
        WorkbookCell[][] Rows);

    private sealed record WorkbookCell(string? Text, decimal? Number, bool LeftToRight);
}
