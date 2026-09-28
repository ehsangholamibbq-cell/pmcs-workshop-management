using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Isolated review only. It never calls CertifiedPdfRuntime or changes the production font manifest.
if (args.Length != 2 || args[0] != "--output")
    throw new ArgumentException("Use --output <directory>.");

// Resolve from the repository's current directory in CI and in local invocations.
var root = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(root, "assets/typography/pmcs-fonts.json")))
    throw new InvalidOperationException("Run font review from the repository root.");
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
QuestPDF.Settings.License = LicenseType.Community; // Reviewed QA-only Community mode; production default stays Unconfigured.

var candidates = new[]
{
    new Candidate("vazirmatn", "Vazirmatn", "Vazirmatn-Regular.ttf",
        "b69fd4c680b8f3f225feabcc655a2c585d97627b8f5f5c0f9985e894069f3a56",
        "Vazirmatn-Bold.ttf", "f635fdbea28f265de395ba83b4b1570dcf2f58d13c65469e61903b1c2d2ae723"),
    new Candidate("estedad", "Estedad", "Estedad-Regular.ttf",
        "812996efdeff8fd68bb854fbb3db218886661fbfd50ed38a06d50bd14c1f3c7f",
        "Estedad-Bold.ttf", "7fa317abae24c82aef5a5816bba4be8b86b344bd740c600276bcb738e416dd76")
};
var files = new List<OutputFile>();
foreach (var candidate in candidates)
{
    Register(candidate.Regular, candidate.RegularSha256);
    Register(candidate.Bold, candidate.BoldSha256);
    var document = CreateSample(candidate.Family);
    var pdf = document.GeneratePdf();
    if (pdf.Length < 1024 || !pdf.AsSpan().StartsWith("%PDF"u8))
        throw new InvalidOperationException($"Invalid review PDF: {candidate.Id}.");
    Save($"{candidate.Id}-report-review.pdf", pdf);
    var images = document.GenerateImages(new ImageGenerationSettings
    {
        ImageFormat = ImageFormat.Png,
        ImageCompressionQuality = ImageCompressionQuality.Best,
        RasterDpi = 110,
        UseTransparentBackground = false
    }).ToArray();
    if (images.Length != 1 || !images[0].AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 }))
        throw new InvalidOperationException($"Review PDF must render exactly one PNG page: {candidate.Id}.");
    Save($"{candidate.Id}-report-review.png", images[0]);
    var xlsx = CreateSpreadsheet(candidate.Family);
    using (var archive = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
    {
        var styles = archive.GetEntry("xl/styles.xml")
            ?? throw new InvalidOperationException("XLSX styles are missing.");
        using var reader = new StreamReader(styles.Open());
        if (!reader.ReadToEnd().Contains($"name val=\"{candidate.Family}\"", StringComparison.Ordinal))
            throw new InvalidOperationException("XLSX font family differs from the review candidate.");
    }
    Save($"{candidate.Id}-style-review.xlsx", xlsx);
}

var index = new
{
    contractVersion = 1,
    review = "UX2-MS45",
    source = Environment.GetEnvironmentVariable("PMCS_SOURCE_HEAD_SHA"),
    scope = "isolated candidate PDF and XLSX; not a certified reporting output",
    candidates = candidates.Select(c => new { c.Id, c.Family, c.RegularSha256, c.BoldSha256 }),
    files
};
File.WriteAllText(Path.Combine(output, "index.json"),
    JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Font review rendered {files.Count} PDF/PNG/XLSX files in {output}.");

void Register(string filename, string expected)
{
    var path = Path.Combine(root, "docs/ux/prototypes/ms45/fonts", filename);
    var bytes = File.ReadAllBytes(path);
    var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    if (actual != expected) throw new InvalidOperationException($"Font integrity mismatch: {filename}.");
    using var stream = new MemoryStream(bytes);
    FontManager.RegisterFont(stream);
}

void Save(string name, byte[] bytes)
{
    File.WriteAllBytes(Path.Combine(output, name), bytes);
    files.Add(new OutputFile(name, bytes.Length,
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
}

static Document CreateSample(string family) => Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.PageColor(Colors.White);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(style => style.FontFamily(family).FontSize(11).FontColor("#17242E"));
        page.Header().BorderBottom(1).BorderColor("#9BA4A8").PaddingBottom(10)
            .Column(column =>
            {
                column.Item().Text("PMCS · نمونهٔ غیررسمی مقایسهٔ قلم").FontSize(15).Bold();
                column.Item().Text("UX2-MS45 · داده‌های فرضی · فاقد Snapshot و مجوز عملیاتی").FontSize(9);
            });
        page.Content().PaddingVertical(18).Column(column =>
        {
            column.Spacing(14);
            column.Item().Text("مرکز فرمان پروژه").FontSize(20).Bold().FontColor("#0C2036");
            column.Item().Text("تصویر رسمی وضعیت هنوز ساخته نشده است. دادهٔ نمونه، وضعیت پروژه را تأیید نمی‌کند.");
            column.Item().Background("#FBF8F1").Border(1).BorderColor("#E9DFCF").Padding(12)
                .Text("هشدار تازگی: بدون Snapshot رسمی، نسخه و زمان مرجع قابل تعیین نیست.");
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });
                table.Header(header =>
                {
                    header.Cell().Text("عنوان نمونه").Bold();
                    header.Cell().Text("مقدار").Bold();
                    header.Cell().Text("تاریخ شمسی").Bold();
                });
                table.Cell().Text("پروژهٔ نمایشی / بتن‌ریزی آزمایشی");
                table.Cell().Text("۱٬۲۵۰٫۵ متر مکعب");
                table.Cell().Text("۱۴۰۵/۰۷/۰۴");
                table.Cell().Text("وضعیت تصمیم");
                table.Cell().Text("غیرفعال");
                table.Cell().Text("۱۴۰۵/۰۷/۰۵");
            });
            column.Item().Text("آزمایش حروف: پ چ ژ گ ک ی ء آ أ ئ · ارقام ۰۱۲۳۴۵۶۷۸۹ · ٪ ٬ ٫ ﷼")
                .FontSize(10);
        });
        page.Footer().BorderTop(1).BorderColor("#9BA4A8").PaddingTop(8)
            .Text("فقط برای بازبینی بصری و فنی؛ خروجی رسمی Reporting نیست.").FontSize(8);
    });
});

static byte[] CreateSpreadsheet(string family)
{
    using var stream = new MemoryStream();
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
        Entry("[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
            """);
        Entry("_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """);
        Entry("xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="نمونهٔ قلم" sheetId="1" r:id="rId1"/></sheets></workbook>
            """);
        Entry("xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
            """);
        Entry("xl/styles.xml", $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="{{family}}"/></font><font><b/><sz val="12"/><name val="{{family}}"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>
            """);
        Entry("xl/worksheets/sheet1.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0" rightToLeft="1"/></sheetViews><sheetFormatPr defaultRowHeight="20"/><cols><col min="1" max="1" width="44" customWidth="1"/><col min="2" max="2" width="24" customWidth="1"/></cols><sheetData><row r="1"><c r="A1" s="1" t="inlineStr"><is><t>PMCS · نمونهٔ غیررسمی فونت</t></is></c></row><row r="2"><c r="A2" t="inlineStr"><is><t>تصویر رسمی وضعیت هنوز ساخته نشده است</t></is></c></row><row r="3"><c r="A3" t="inlineStr"><is><t>پروژهٔ نمایشی / ۱۴۰۵/۰۷/۰۴</t></is></c><c r="B3" t="inlineStr"><is><t>۱٬۲۵۰٫۵ متر مکعب</t></is></c></row><row r="4"><c r="A4" t="inlineStr"><is><t>پ چ ژ گ ک ی ء آ أ ئ · ٪ ٬ ٫ ﷼</t></is></c></row></sheetData></worksheet>
            """);

        void Entry(string path, string xml)
        {
            using var target = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
            target.Write(Encoding.UTF8.GetBytes(xml));
        }
    }
    return stream.ToArray();
}

internal sealed record Candidate(string Id, string Family, string Regular, string RegularSha256,
    string Bold, string BoldSha256);
internal sealed record OutputFile(string Name, int Bytes, string Sha256);
