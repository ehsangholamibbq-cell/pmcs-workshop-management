# PMCS V1.1 — قرارداد فونت نسخه‌دار

- شناسه: `PMCS-UX-TYPOGRAPHY-001`
- نسخه: `1.5.0`
- وضعیت: وزیرمتن در Candidate تولیدی MS48 متصل؛ Gateهای `VX-G3/G4/G5` باز
- مرجع: `PMCS-V1.1-UX2-MS32-C1` و Source Candidate `UX2-MS48`

## منبع واحد و سطوح مصرف

`assets/typography/pmcs-fonts.json` تنها محل تعریف `contractVersion`، خانواده و
fallback وب، asset اختیاری WOFF2 خودمیزبان، خانواده و فایل‌های Regular/Bold
PDF با SHA-256 و خانوادهٔ XLSX است. Candidate MS48 نسخهٔ `2.0.0` این manifest
را به `Vazirmatn` در Web/Offline، PDF، XLSX و Print ارتقا می‌دهد؛ fallback وب
`Tahoma, Segoe UI, Arial, sans-serif` است. نسخهٔ `1.0.0` با Tahoma،
DejaVu Sans و Arial، baseline تاریخی و مسیر بازگشت است.

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
می‌کند. این آزمایش فقط Browser Print بود؛ در زمان MS44،
`assets/typography/pmcs-fonts.json` نسخهٔ `1.0.0` با Tahoma/DejaVu/Arial بود. انتخاب مستند و تمرین
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
و `PdfLicense=Unconfigured` در آن مرحله عوض نشدند. نصب فونت روی دستگاه گیرنده برای
نمایش همان قلم در XLSX همچنان شرط خارجی است.

در ۲۰۲۶-۰۹-۲۹ مالک محصول اختیار انتخاب قلم را برای ادامهٔ بدون توقف UX2
واگذار کرد. انتخاب از شواهد Web/Browser Print/تمرین PDF/XLSX با دلیل
ثبت‌شده و Rollback نسخه‌دار انجام می‌شود؛ این اختیار جایگزین
Qualification واقعی Offline، Renderer رسمی، Golden، Accessibility و
چاپ پس از تغییر تولیدی نیست. این بند ثبت تصمیم پیش از Candidate MS48 است؛
manifest آن هنگام `1.0.0` بود.

## انتخاب Candidate برای UX2-MS48

قاب‌های اصلاح‌شدهٔ MS44 در Desktop/Mobile و دو PDF/PNG نمونهٔ MS45
برای وزیرمتن و استعداد کنار هم بررسی شدند. هر دو فارسی، ارقام و
چاپ A4 را بدون برش اصلی نشان می‌دهند. برای متن‌های پرتراکم مدیریتی
و جدول/گزارش، وزیرمتن در وزن معمول خوانایی یکنواخت‌تری دارد؛
استعداد در تیترها پررنگ‌تر است. با اختیار واگذارشدهٔ مالک، `Vazirmatn`
به‌عنوان خانوادهٔ Candidate و `Estedad` به‌عنوان گزینهٔ مقایسهٔ بعدی
انتخاب شدند. این قضاوت بصری مبنای Candidate است و با Golden واقعی MS48
در بخش بعدی کنترل می‌شود.

## اجرای نسخه‌دار و Qualification محدود UX2-MS48

manifest `2.0.0`، WOFF2 متغیر خودمیزبان با SHA-256
`4e3fa217d38fdafc1fea4414ceb58ca5e662cf0ab5fa735a8c8c20e8b42cad92`،
TTF Regular/Bold با digestهای
`b69fd4c680b8f3f225feabcc655a2c585d97627b8f5f5c0f9985e894069f3a56` و
`f635fdbea28f265de395ba83b4b1570dcf2f58d13c65469e61903b1c2d2ae723`
و مجوز OFL با digest
`17e355067c8284f47743a1ee3b1ef7ff684ff0601eda357f9353b10b3016ab31`
را به commit رسمی upstream `6e553e33489a8f9dfaccc76860a2e3f3c1e66de7`
pin می‌کند. `sync.mjs --check` digest و خروجی‌های تولیدی CSS/C#/manifest/SW
را کنترل می‌کند. Font file در Browser با digest واقعی، `document.fonts`،
cache آفلاین و Print inheritance آزموده می‌شود؛ فاز بارگذاری آفلاین بعد از
قطع شبکه نیز در E2E کنترل شده است. ده خانوادهٔ گزارش، PDFهای rasterized و
XLSXهای راست‌به‌چپ از Artifact رسمی بررسی می‌شوند. در F05 جابه‌جایی بخش
Lineage به فضای صفحهٔ اول، PDF را دوصفحه‌ای و خوانا نگه داشت.
Source Run 425 هر هشت Job را پاس کرد؛ Golden Backend Artifact
`11003123272` با digest
`sha256:07964bb13f02df30b5a94ce96f14a4d46ac8c5663a2bcdbb0c39d0b0db18d460`
و UI Artifact `11003547859` با digest
`sha256:cd9f084c7161653a59ce7358a8275b17ab0c4a64ebd6d83c761da1dbbe1a8722`
است. Index قاب‌های UI به Source Commit و manifest رسمی متصل است.

Rollback از manifest و assetهای قبلی همراه sync و Goldenهای پیشین در
Commit عادی قابل انجام است؛ DejaVu و fallback وب حفظ شده‌اند. XLSX نام
خانواده را در Style دارد اما نصب فونت در دستگاه گیرنده خارج از کنترل فایل
است. Gate `VX-G5` هنوز به ممیزی جامع Accessibility، Responsive،
cross-browser، عملکرد و Golden همهٔ Route/Stateهای مهاجرت‌یافته نیاز دارد؛
این Evidence فونت به‌تنهایی آن Gate را نمی‌بندد.
