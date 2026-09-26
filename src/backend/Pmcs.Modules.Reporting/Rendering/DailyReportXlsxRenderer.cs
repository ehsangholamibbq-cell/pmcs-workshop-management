using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed class DailyReportXlsxRenderer(ReportingExecutionOptions execution) : IReportRenderer
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

    public RenderedReportArtifact Render(ReportRenderRequest request)
    {
        if (request.Format != Format)
        {
            throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                "The XLSX renderer received another output format.");
        }

        var orderedVersions = request.Snapshot.Versions
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId)
            .ToArray();
        var factCount = orderedVersions.Sum(version => version.Facts.Count);
        if (factCount > execution.MaximumXlsxRows)
        {
            throw new ReportRenderingException(
                "reporting.output.row_limit_exceeded",
                transient: false,
                "The certified workbook row limit was exceeded.");
        }

        var entries = new (string Name, byte[] Content)[]
        {
            ("[Content_Types].xml", ContentTypes()),
            ("_rels/.rels", PackageRelationships()),
            ("docProps/app.xml", AppProperties()),
            ("docProps/core.xml", CoreProperties(request)),
            ("xl/workbook.xml", Workbook()),
            ("xl/_rels/workbook.xml.rels", WorkbookRelationships()),
            ("xl/styles.xml", Styles()),
            ("xl/worksheets/sheet1.xml", MetadataSheet(request)),
            ("xl/worksheets/sheet2.xml", DataSheet(orderedVersions))
        };

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

    private static byte[] ContentTypes() => Xml(writer =>
    {
        writer.WriteStartElement("Types", ContentTypesNamespace);
        WriteContentType(writer, "Default", "rels", "application/vnd.openxmlformats-package.relationships+xml");
        WriteContentType(writer, "Default", "xml", "application/xml");
        WriteContentType(writer, "Override", "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        WriteContentType(writer, "Override", "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        WriteContentType(writer, "Override", "/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
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

    private static byte[] WorkbookRelationships() => Xml(writer =>
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
        WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet2.xml");
        WriteRelationship(writer, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
        writer.WriteEndElement();
    });

    private static byte[] Workbook() => Xml(writer =>
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        writer.WriteStartElement("bookViews", SpreadsheetNamespace);
        writer.WriteStartElement("workbookView", SpreadsheetNamespace);
        writer.WriteAttributeString("activeTab", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheets", SpreadsheetNamespace);
        WriteSheet(writer, "Metadata", 1, "rId1");
        WriteSheet(writer, "Data", 2, "rId2");
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

    private static byte[] CoreProperties(ReportRenderRequest request) => Xml(writer =>
    {
        writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
        writer.WriteAttributeString("xmlns", "dc", null, DublinCoreNamespace);
        writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteElementString("dc", "title", DublinCoreNamespace, "گزارش روزانه رسمی");
        writer.WriteElementString("dc", "creator", DublinCoreNamespace, "PMCS Certified Reporting");
        writer.WriteElementString("dc", "subject", DublinCoreNamespace, request.VerificationCode);
        WriteW3CDate(writer, "created", request.SourceCutoffUtc);
        WriteW3CDate(writer, "modified", request.SourceCutoffUtc);
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

    private static byte[] MetadataSheet(ReportRenderRequest request)
    {
        var current = request.Snapshot.CurrentOfficialReportId.HasValue
            ? request.Snapshot.Versions.SingleOrDefault(
                version => version.ReportId == request.Snapshot.CurrentOfficialReportId.Value)
            : null;
        var rows = new (string Key, string Value)[]
        {
            ("عنوان", "گزارش روزانه رسمی"),
            ("کد پروژه", request.Snapshot.Project.Code),
            ("نام پروژه", request.Snapshot.Project.Name),
            ("منطقه زمانی", request.Snapshot.Project.TimeZone),
            ("نسخه قالب", request.TemplateVersion),
            ("نسخه Snapshot", request.Snapshot.SchemaVersion),
            ("وضعیت داده", PersianReportFormatting.DataStatus(request.Snapshot.DataStatus)),
            ("تاریخ گزارش شمسی", current is null ? "—" : PersianReportFormatting.FormatDate(current.ReportDate)),
            ("تاریخ گزارش ISO", current?.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—"),
            ("Cutoff محلی", PersianReportFormatting.FormatInstant(request.SourceCutoffUtc, request.Snapshot.Project.TimeZone)),
            ("Cutoff UTC", request.SourceCutoffUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            ("Snapshot SHA-256", request.SnapshotSha256),
            ("Source Manifest SHA-256", request.SourceManifestSha256),
            ("Output Manifest SHA-256", request.ManifestSha256),
            ("کد راستی‌آزمایی", request.VerificationCode),
            ("مسیر راستی‌آزمایی", $"/api/v1/projects/{request.Snapshot.Project.Id}/reports/outputs/{request.OutputId}/verify")
        };

        return Worksheet(writer =>
        {
            WriteColumns(writer, [24d, 78d]);
            writer.WriteStartElement("sheetData", SpreadsheetNamespace);
            WriteStringRow(writer, 1, ["کلید", "مقدار"], style: 1);
            for (var index = 0; index < rows.Length; index++)
            {
                WriteStringRow(
                    writer,
                    index + 2,
                    [rows[index].Key, PersianReportFormatting.SafeSpreadsheetText(rows[index].Value)],
                    style: 2);
            }
            writer.WriteEndElement();
        });
    }

    private static byte[] DataSheet(IReadOnlyCollection<DailyReportReportingVersion> versions)
    {
        var headers = new[]
        {
            "نسخه", "وضعیت نسخه", "تاریخ شمسی", "تاریخ ISO", "محل", "نوع Fact", "شرح",
            "دسته", "مقدار", "واحد", "تعداد منبع", "ساعت", "اثر", "کد مرجع",
            "Measurement Item ID", "Fact ID", "Copied From Fact ID", "Report ID"
        };
        var facts = versions
            .SelectMany(version => version.Facts
                .OrderBy(fact => fact.CreatedAt)
                .ThenBy(fact => fact.FactId)
                .Select(fact => new FactRow(version, fact)))
            .ToArray();
        return Worksheet(writer =>
        {
            WriteColumns(writer, [10d, 22d, 14d, 14d, 24d, 18d, 54d, 20d, 14d, 12d, 14d, 14d, 14d, 18d, 38d, 38d, 38d, 38d]);
            writer.WriteStartElement("sheetData", SpreadsheetNamespace);
            WriteStringRow(writer, 1, headers, style: 1);
            var rowNumber = 2;
            foreach (var item in facts)
            {
                writer.WriteStartElement("row", SpreadsheetNamespace);
                writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
                WriteStringCell(writer, rowNumber, 1, PersianReportFormatting.ToPersianDigits(item.Version.VersionNumber.ToString(CultureInfo.InvariantCulture)), 2);
                WriteStringCell(writer, rowNumber, 2, PersianReportFormatting.VersionState(item.Version.State), 2);
                WriteStringCell(writer, rowNumber, 3, PersianReportFormatting.FormatDate(item.Version.ReportDate), 2);
                WriteStringCell(writer, rowNumber, 4, item.Version.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), 3);
                WriteStringCell(writer, rowNumber, 5, PersianReportFormatting.SafeSpreadsheetText(item.Fact.LocationName ?? item.Version.LocationName), 2);
                WriteStringCell(writer, rowNumber, 6, PersianReportFormatting.FactKind(item.Fact.Kind), 2);
                WriteStringCell(writer, rowNumber, 7, PersianReportFormatting.SafeSpreadsheetText(item.Fact.Description, 4_000), 2);
                WriteStringCell(writer, rowNumber, 8, PersianReportFormatting.SafeSpreadsheetText(item.Fact.Category), 2);
                WriteNumberOrBlank(writer, rowNumber, 9, item.Fact.Quantity);
                WriteStringCell(writer, rowNumber, 10, PersianReportFormatting.SafeSpreadsheetText(item.Fact.Unit), 2);
                WriteNumberOrBlank(writer, rowNumber, 11, item.Fact.ResourceCount);
                WriteNumberOrBlank(writer, rowNumber, 12, item.Fact.Hours);
                WriteStringCell(writer, rowNumber, 13, PersianReportFormatting.Impact(item.Fact.ImpactLevel), 2);
                WriteStringCell(writer, rowNumber, 14, PersianReportFormatting.SafeSpreadsheetText(item.Fact.ReferenceCode), 2);
                WriteStringCell(writer, rowNumber, 15, item.Fact.MeasurementItemId?.ToString() ?? "—", 3);
                WriteStringCell(writer, rowNumber, 16, item.Fact.FactId.ToString(), 3);
                WriteStringCell(writer, rowNumber, 17, item.Fact.CopiedFromFactId?.ToString() ?? "—", 3);
                WriteStringCell(writer, rowNumber, 18, item.Version.ReportId.ToString(), 3);
                writer.WriteEndElement();
                rowNumber++;
            }
            writer.WriteEndElement();
            writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
            writer.WriteAttributeString("ref", $"A1:R{Math.Max(1, rowNumber - 1)}");
            writer.WriteEndElement();
        });
    }

    private static byte[] Worksheet(Action<XmlWriter> content) => Xml(writer =>
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
        content(writer);
        writer.WriteEndElement();
    });

    private static void WriteColumns(XmlWriter writer, IReadOnlyList<double> widths)
    {
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var index = 0; index < widths.Count; index++)
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

    private static void WriteNumberOrBlank(XmlWriter writer, int row, int column, decimal? value)
    {
        if (!value.HasValue)
        {
            WriteStringCell(writer, row, column, "—", 2);
            return;
        }

        writer.WriteStartElement("c", SpreadsheetNamespace);
        writer.WriteAttributeString("r", $"{ColumnName(column)}{row}");
        writer.WriteAttributeString("s", "3");
        writer.WriteElementString("v", SpreadsheetNamespace, value.Value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteNumberOrBlank(XmlWriter writer, int row, int column, int? value) =>
        WriteNumberOrBlank(writer, row, column, value.HasValue ? (decimal?)value.Value : null);

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

    private sealed record FactRow(
        DailyReportReportingVersion Version,
        DailyReportReportingFact Fact);
}
