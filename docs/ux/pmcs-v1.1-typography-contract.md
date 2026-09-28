# PMCS V1.1 — قرارداد فونت نسخه‌دار

- شناسه: `PMCS-UX-TYPOGRAPHY-001`
- نسخه: `1.0.0`
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
