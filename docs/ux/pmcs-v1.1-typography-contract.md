# PMCS V1.1 — قرارداد فونت نسخه‌دار

- شناسه: `PMCS-UX-TYPOGRAPHY-001`
- نسخه: `1.4.0`
- وضعیت: زیرساخت MS32 متصل؛ انتخاب فونت فارسی و Qualification بصری/چاپی باز
- مرجع: `PMCS-V1.1-UX2-MS32-C1`، source Run 377 با هشت Job سبز

## منبع واحد و سطوح مصرف

`assets/typography/pmcs-fonts.json` تنها محل تعریف `contractVersion`، خانواده و
fallback وب، asset اختیاری WOFF2 خودمیزبان، خانواده و فایل‌های Regular/Bold
PDF با SHA-256 و خانوادهٔ XLSX است. وضعیت فعلی Web برابر Tahoma با fallback
`Segoe UI, Arial, sans-serif`، PDF برابر DejaVu Sans و XLSX برابر Arial است؛
این‌ها baseline فعلی‌اند و انتخاب فونت فارسی آینده تلقی نمی‌شوند.

| سطح | خروجی تولیدشده | مصرف |
| --- | --- | --- |
| App و Print مرورگر | `src/web/app/typography.generated.css` | Import در root layout و متغیر `--pmcs-font-family` در بدنه |
| Offline و Print آفلاین | `src/web/public/typography/pmcs-fonts.css` | Link صفحهٔ آفلاین و cache نسخه‌دار Service Worker |
| PDF | `src/backend/Pmcs.Modules.Reporting/Rendering/PmcsTypographyContract.g.cs` و manifest فونت | Family و دو فایل TTF با digest؛ Docker و QA از همان دارایی استفاده می‌کنند |
| XLSX | `PmcsTypographyContract.XlsxFamily` | همهٔ ده Renderer و خانوادهٔ سبک‌ها |

## روش تغییر کنترل‌شده

1. فایل‌های دارای حق استفاده و مجوز مناسب را در `assets/typography/web/` (WOFF2)
   و `assets/reporting/fonts/` (دو TTF) قرار دهید؛ Familyهای متناظر و SHA-256
   فایل‌ها را در manifest ثبت و `contractVersion` را افزایش دهید. در صورت
   تغییر PDF، metadata، منشأ و مجوز را نیز به‌روز کنید. فونت Web باید Persian
   glyph، ارقام، علائم، وزن‌های قرارداد و رفتار چاپی لازم را داشته باشد.
2. `node tools/typography/sync.mjs --write` را اجرا و تمام فایل‌های تولیدشده،
   از جمله WOFF2 کپی‌شده در `src/web/public/typography/` را همراه manifest
   Commit کنید؛ `node tools/typography/sync.mjs --check` در CI و QA drift یا
   digest نادرست را رد می‌کند. تغییر نسخه، cache آفلاین را invalid می‌کند.
3. Full Web/Backend/Integration/UI-E2E و Goldenهای PDF/XLSX را اجرا کنید؛
   پس از تغییر فونت واقعی، Golden تصویری و متنی PDF، خروجی XLSX، چاپ و صفحه‌های
   Desktop/Tablet/Mobile و حالت‌های RTL/آفلاین/خطا را بازبینی و نسخه‌دار کنید.
   معیارهای Accessibility و Performance در `VX-G5` سنجیده شوند.

تغییر خانواده‌ها Business Rule، دادهٔ ذخیره‌شده و Permission را تغییر نمی‌دهد.
قرارداد، مسیر فنی تعویض کامل را فراهم می‌کند؛ عبور از Gate بصری تنها با انتخاب
فونت واقعی و Evidence رندر همهٔ سطوح پذیرفته می‌شود.

## نمونهٔ مقایسه‌ای UX2-MS44

`docs/ux/prototypes/ms44/` دو WOFF2 خودمیزبان با مجوز OFL و منشأ/Hash
ثابت برای وزیرمتن و استعداد نگه می‌دارد. انتخابگر نمونه، هر قلم را با
Baseline در ۱۰ سناریو و ۷ حالت و Web/Desktop/Tablet/Mobile/Print A4 مقایسه
می‌کند. این آزمایش فقط Browser Print است؛ `assets/typography/pmcs-fonts.json`
همچنان نسخهٔ `1.0.0` با Tahoma/DejaVu/Arial تولیدی است. انتخاب مستند و تمرین
رندر رسمی PDF/XLSX، Golden و Offline، پیش از هر تعویض تولیدی لازم‌اند.

## تمرین ایزولهٔ UX2-MS45

چهار TTF Regular/Bold در `docs/ux/prototypes/ms45/fonts/` از همان Commitهای
رسمی وزیرمتن و استعداد با SHA ثابت و OFL موجود در بستهٔ MS44 نگه‌داری
می‌شوند. برنامهٔ `tools/qa/FontReview` در فرآیند QA جداگانه، QuestPDF را
با فونت هر Candidate ثبت و یک صفحهٔ A4/PNG و نمونهٔ Spreadsheet راست‌به‌چپ
می‌سازد؛ نام خانواده در دو Style XLSX ذخیره می‌شود. Run 414 هر هشت Job
سبز، Artifact `10999743061` با شش خروجی و index معتبر دارد. PDF/PNGهای
هر دو قلم بازبینی و XLSXها با parser مستقل باز شدند. این نمونه از Renderer
رسمی و Golden محصول استفاده نمی‌کند؛ Familyهای تولیدی Tahoma/DejaVu/Arial
و `PdfLicense=Unconfigured` عوض نشده‌اند. نصب فونت روی دستگاه گیرنده برای
نمایش همان قلم در XLSX همچنان شرط خارجی است.

در ۲۰۲۶-۰۹-۲۹ مالک محصول اختیار انتخاب قلم را برای ادامهٔ بدون توقف UX2
واگذار کرد. انتخاب از شواهد Web/Browser Print/تمرین PDF/XLSX با دلیل
ثبت‌شده و Rollback نسخه‌دار انجام می‌شود؛ این اختیار جایگزین
Qualification واقعی Offline، Renderer رسمی، Golden، Accessibility و
چاپ پس از تغییر تولیدی نیست. تا Candidate بعدی، manifest تولیدی
`1.0.0` و خانواده‌های موجود برقرارند.

## انتخاب Candidate برای UX2-MS48

قاب‌های اصلاح‌شدهٔ MS44 در Desktop/Mobile و دو PDF/PNG نمونهٔ MS45
برای وزیرمتن و استعداد کنار هم بررسی شدند. هر دو فارسی، ارقام و
چاپ A4 را بدون برش اصلی نشان می‌دهند. برای متن‌های پرتراکم مدیریتی
و جدول/گزارش، وزیرمتن در وزن معمول خوانایی یکنواخت‌تری دارد؛
استعداد در تیترها پررنگ‌تر است. با اختیار واگذارشدهٔ مالک، `Vazirmatn`
به‌عنوان خانوادهٔ Candidate و `Estedad` به‌عنوان گزینهٔ بازگشت
انتخاب شدند. این قضاوت بصری است، نه نتیجهٔ Golden محصول؛ MS48 باید
فایل‌های pinned WOFF2/TTF را از manifest مرکزی نسخه‌دار، Web/Offline/
PDF/XLSX/Print و آزمون‌های واقعی را هم‌زمان تغییر و کنترل کند.
manifest تولیدی هنوز `1.0.0` است.
