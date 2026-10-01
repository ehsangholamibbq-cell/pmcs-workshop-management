# ADR 0030 — QuestPDF Community و Runtime قطعی PDF

- وضعیت: Accepted for `V1.1-RPT1` qualification
- تاریخ: ۱۴۰۵/۰۶/۲۸ (۲۰۲۶-۰۹-۱۹)
- Parent checkpoint: `PMCS-V1.1-RPT1-S06-MS05-C1`
- تصمیم‌گیر: Product owner؛ پاسخ صریح `QuestPDF Community`
- تصمیم جایگزین‌شده: حالت «تصمیم حقوقی PDF ثبت نشده» در MS05

## Context

ADR 0029، QuestPDF را فقط به‌عنوان Adapter فنی معرفی کرد و انتخاب license tier را عمداً باز و
`PdfLicense=Unconfigured` نگه داشت. بدون تصمیم صریح، Renderer باید fail-closed می‌ماند و Golden،
visual/pixel و performance PDF قابل Qualification نبود.

مجوز رسمی QuestPDF، Community را برای اشخاص و سازمان‌های واجد شرایط تعریف می‌کند. انتخاب این tier
خوداظهاری مالک محصول است و به‌تنهایی اثبات دائمی eligibility سازمان نیست. مرجع بررسی‌شده در تاریخ
این ADR:

- `https://www.questpdf.com/license/guide.html`
- `https://www.questpdf.com/license/community.html`

## Decision

### ۱. License tier مصوب

Adapter رسمی PDF در RPT1 با `QuestPDF Community` qualify می‌شود. Runtime فقط مقدار `Community`
را مطابق این ADR می‌پذیرد؛ `Unconfigured` همچنان fail-closed است و tier دیگری با
`reporting.renderer.license_unapproved` رد می‌شود.

این تصمیم ادعای مشاورهٔ حقوقی یا تضمین eligibility آینده نیست. مالک تجاری/حقوقی باید پیش از هر
Production rollout و سپس حداقل در بازبینی سالانه، شرایط جاری Community را دوباره تأیید و Evidence
سازمانی را نگه‌داری کند. اگر eligibility از بین رفت، rollout متوقف و tier مناسب با ADR جدید انتخاب
می‌شود.

### ۲. مرز فعال‌سازی

- `ReportingCenter:PdfLicense` در `appsettings.json` برابر `Unconfigured` می‌ماند؛
- `Phase1Enabled`، `OutputAccessEnabled` و `WorkerEnabled` پیش‌فرض `false` می‌مانند؛
- فقط مسیر QA ایزوله، پس از اثبات fail-closed قبلی، API را موقتاً با `Community` اجرا می‌کند؛
- این ADR مجوز Production enablement یا بستن RPT1 نیست.

### ۳. Runtime و dependencyهای pin‌شده

- QuestPDF: `2026.8.0`؛
- build image: `mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d`؛
- runtime image: `mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c`؛
- فونت `DejaVuSans.ttf`: SHA-256
  `ae7b7855e115a5966d8b1b3f80f254ccc117ec86f9965e202ee2940453837280`؛
- فونت `DejaVuSans-Bold.ttf`: SHA-256
  `5c1247acef7f2b8522a31742c76d6adcb5569bacc0be7ceaa4dc39dd252ce895`؛
- خانوادهٔ فونت: `DejaVu Sans 2.37` با متن license همراه artifact.

Renderer پیش از ثبت فونت، SHA-256 فایل را کنترل می‌کند. اختلاف image/font contract با
`reporting.renderer.configuration_unpinned` و اختلاف bytes فونت با
`reporting.renderer.font_integrity_failed` متوقف می‌شود.

### ۴. Qualification contract

PDF برای fixture قطعی گزارش روزانه باید این کنترل‌ها را پاس کند:

- دو رندر مستقل با bytes و SHA-256 یکسان؛
- PDF واقعی A4، media type صحیح، header/footer/page number و متن فارسی قابل استخراج؛
- PNG در ۹۶ DPI با visual digest ثابت و بازبینی مستقل Poppler؛
- cold render حداکثر ۵۰۰۰ ms و warm render حداکثر ۲۵۰۰ ms؛
- حداکثر ۵ MiB برای fixture Qualification؛
- مسیر متصل همان fixture معنایی MS05، replay، download/hash/security headers، حذف Draft و budget
  انتهابه‌انتها را کنترل کند.

## Alternatives Rejected

| گزینه | دلیل رد |
| --- | --- |
| باقی‌ماندن تصمیم در Conversation | غیرقابل ردیابی و ناسازگار با Gate حقوقی MS05 |
| انتخاب ضمنی Community در code | دورزدن self-certification و fail-closed deployment |
| image tag شناور یا فونت سیستم‌عامل | Golden غیرقابل بازتولید و ریسک drift |
| فعال‌سازی پیش‌فرض PDF در Production | خارج از Scope این Qualification و بدون Pilot gate |
| tier دیگر بدون تصمیم جدید | ناسازگار با تصمیم صریح مالک محصول |

## Consequences

- PDF در QA قابل Qualification است، اما Production همچنان با تنظیمات پیش‌فرض خروجی PDF تولید
  نمی‌کند.
- ارتقای QuestPDF، تغییر base image، فونت، DPI یا layout، Golden جدید و بازبینی همین قرارداد را
  لازم دارد.
- eligibility مجوز یک control عملیاتی دوره‌ای است؛ تغییر آن با code inference یا fallback خودکار
  حل نمی‌شود.
- اختلاف ده‌گانهٔ Catalog با پیاده‌سازی تک‌گزارش همچنان Gate مستقل پیش از بستن RPT1 است.
