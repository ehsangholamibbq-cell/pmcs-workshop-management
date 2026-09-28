# PMCS V1.1 — Active Route and State Screenshot Baseline

- شناسه: `PMCS-UX-VX-G1-BASELINE-001`
- مرحله: `UX2-MS46`؛ MS45 Runهای 414/415 موفق و Safe؛ ماتریس Component و قاب 45 Candidate است
- مرجع اجرایی ماشین‌خوان: `src/web/e2e/visual-baseline.json`
- محدوده: Source فعال Web، tenant و پروژهٔ QA مجزا، Chromium، فارسی/RTL، `Asia/Tehran`
- Runtime business rule، Feature Flag و Migration: بدون تغییر

## Inventory مسیرهای فعال

هر ۱۱ فایل `app/**/page.tsx` به یک Route و حداقل یک تصویر متصل است. ابزار
`node tools/qa/visual-baseline.mjs check-source` این رابطه را با کشف واقعی
Routeها کنترل می‌کند؛ اضافه‌شدن صفحهٔ جدید بدون capture، Gate معماری را رد
می‌کند. مسیر 404 یک state عمدی و خارج از Routeهای ثبت‌شده است.

| Route فعال | سطح | Stateهای ثبت‌شده | Capture |
| --- | --- | --- | --- |
| `/login` | Identity | ورود پیش از OIDC، برند، Motion کاهش‌یافته، خطای callback هویت و تلاش دوباره | 01، 42 |
| `/` | Project Registry | فهرست پروژه | 02 |
| `/portfolio` | Portfolio | عادی Desktop/Mobile، فیلتر خالی، Loading، خطای سرویس | 03، 20، 25–27 |
| `/portfolio/reports` | Portfolio Reporting | خاموش پیش‌فرض، ممنوع، catalog مجاز | 24، 37–38 |
| `/projects/[projectId]` | Project Command Center | نمای اصلی و ۱۱ بخش عملیاتی، Mobile/Tablet، تقویم، اعتبارسنجی آفلاین، صفحهٔ بازگشت آفلاین، انتهای ناوبری با keyboard focus، ممیزی media چاپ مرورگر | 04–15، 21، 28–31، 39، 43 |
| `/projects/[projectId]/collaboration` | گفت‌وگوی گروهی پروژه | خاموش پیش‌فرض، ممنوع، پیام مجاز فقط در همان پروژه، تعارض Revision ویرایش خود با حفظ پیش‌نویس | 22، 33–34، 40 |
| `/projects/[projectId]/reports` | Reporting Center | خاموش پیش‌فرض، ممنوع، catalog مجاز | 23، 35–36 |
| `/profile` | پروفایل شخصی | اطلاعات عضو | 16 |
| `/admin/users` | مدیریت کاربران | فهرست/دعوت/عضویت | 17 |
| `/admin/login-experience` | مدیریت ظاهر ورود | نسخه و گزینه‌های ظاهر، انتخاب فایل محلی با نام فارسی و حذف انتخاب | 18، 44 |
| `/project-bootstraps` | تکثیر پروژه | انتخاب پروژهٔ مبدأ، پیش‌نمایش با تعارض و مانع اجرا | 19، 41 |

Capture 32 صفحهٔ 404 را پوشش می‌دهد. Capture 39 انتهای Sidebar دسکتاپ را پس از
focus آخرین لینک با نشان غیرفشرده و اسکرول مستقل ثبت می‌کند. Inventory دقیق ID، نام state، viewport و
فایل E2E مالک هر تصویر در JSON ثبت شده است. Stateهای ممنوع و مجاز Chat/Reporting
با پاسخ‌های کنترل‌شدهٔ API در E2E شبیه‌سازی می‌شوند؛ Feature Flagهای Production
هنوز خاموش‌اند و تصویر مجاز به‌معنای فعال‌سازی Feature در defaults نیست.
Chat فقط گروه پروژه است؛ هیچ DM/صوت/تصویر در این inventory وجود ندارد.
Capture 40 در همان سناریوی E2E تعارض ویرایش پیام خود، نسخهٔ فعلی سرور و
پیش‌نویس حفظ‌شدهٔ نویسنده را پیش از تأیید مجدد ثبت می‌کند.
Capture 41 پیش‌نمایش نسخه‌دار تکثیر را با یک ردیف افزودنی، یک تعارض و یک مانع
ثبت می‌کند. Fixture مرورگر فقط پاسخ ساخت برنامه را شبیه‌سازی می‌کند؛ آزمون
Payload و کلید Idempotency را کنترل می‌کند و حتی پس از تیک تأیید، دکمهٔ اجرا
غیرفعال و تعداد درخواست اجرا صفر می‌ماند. هیچ مقصدی در API واقعی ساخته نمی‌شود.
Capture 42 پیش از ورود OIDC و با پارامتر `error=identity`، پیام فارسی خطای
callback را با `role=alert` و دکمهٔ «ورود امن» قابل تلاش دوباره ثبت می‌کند.
Credential در صفحهٔ PMCS وارد نمی‌شود و Setup پس از Capture ورود واقعی را ادامه می‌دهد.
Capture 43 صفحهٔ مرکز فرمان را با `media=print` در viewport دسکتاپ ثبت می‌کند و
PDF A4 همان مرورگر را در فایل `43-project-print-preview.pdf` همراه دارد. این
فایل نمونهٔ ممیزی وضعیت فعلی است، نه Print System یا خروجی رسمی Reporting.
در MS43 همین Capture برگهٔ محدود چاپ مرورگر را با نشان رسمی، تصویر وضعیت
مجاز یا نبود آن و اعلان غیررسمی‌بودن ثبت می‌کند. E2E پنهان‌شدن Sidebar و
Workspace تعاملی، نبود Input/Select/Button در برگه و بارگذاری نشان را
کنترل می‌کند. PDF همراه همچنان خروجی رسمی Reporting نیست؛ صفحه‌بندی و
Golden نهایی در `VX-G5` باید جداگانه Qualified شوند. Run 408 PDF را به یک
صفحه رساند اما پیام منبع برای نبود Snapshot ناسازگار بود؛ correction Run
409 هشت Job سبز و متن/PDF را دوباره تأیید کرد.
Capture 44 انتخاب فایل نمونه در مدیریت ظاهر Login را بدون Upload ثبت می‌کند؛
نام فایل در UI دیده می‌شود و E2E پس از Capture فوکوس و حذف انتخاب را کنترل
می‌کند. سیاست امن Upload/Release و Publish تغییر نمی‌کند.
در MS41، قاب‌های 20/21/22/23 Navigation افقی موبایل را همراه با راهنمای
فارسی پیمایش لمسی و کلید تب در شش Shell فعال نشان می‌دهند. آزمون مرورگر
فوکوس پیوند دورتر و نبود overflow کل سند در عرض ۳۲۰ پیکسل را نیز حفظ می‌کند.
تعداد Captureها ۴۴ و Routeها ۱۱ باقی می‌ماند.
در MS42، قاب 26 پیش از پاسخ سرویس، کارت‌ها و Panelهای Placeholder خنثی
Portfolio را ثبت می‌کند؛ متن Loading واقعی باقی است و هیچ عدد ساختگی در
Skeleton نیست. پس از خطا، E2E حذف Placeholder را پیش از قاب 27 کنترل
می‌کند. تعداد مسیرها و Captureها تغییر نمی‌کند.

## دستور بازتولید و Evidence

CI با Commit مشخص، Docker Compose مجزا، Fixture OIDC، دادهٔ QA، Chromium،
viewportهای `1440×900` / `820×1180` / `390×844` و کنترل responsive `320×720`، locale فارسی، timezone تهران،
Light و reduced motion اجرا می‌شود. تصاویر Command Center در هر سه viewport
پس از تغییر اندازه و آماده‌شدن فونت صریحاً به ابتدای صفحه برمی‌گردند. Helper هر
تصویر را پس از Assertion state،
آماده‌شدن فونت، کنترل URL و RTL می‌گیرد؛ انیمیشن و caret غیرفعال‌اند.

پس از `ui-e2e`، فرمان `node tools/qa/visual-baseline.mjs verify` دقیقاً ۴۵ PNG
اعلام‌شده را از نظر حضور، عدم فایل اضافی، ساختار PNG و اندازهٔ viewport کنترل
می‌کند و تنها PDF اعلام‌شده را از نظر حضور، header/footer و SHA-256 می‌سنجد.
`index.json` برای هر تصویر SHA-256، اندازه، Route، state و viewport و
برای PDF همراه اندازه و SHA-256 و
برای کل بسته Manifest SHA-256، Commit و Run/Attempt GitHub را ثبت می‌کند. Artifact
`pmcs-current-ui-tour` شامل PNGها و index است. تغییر Source، Fixture یا Font
باید با Run و index تازه سنجیده شود؛ SHA هویت Evidence همان Run است و جایگزین
بازبینی بصری/آزمون اختلاف پیکسل در `VX-G5` نیست.

در محیط محلی همسان با CI و پس از آماده‌سازی Fixture:

```bash
node tools/qa/visual-baseline.mjs check-source
node tools/qa/regression-runner.mjs run ui-e2e
node tools/qa/visual-baseline.mjs verify
```

## مرز Gate

این Candidate مسیرها و stateهای بحرانی را با screenshot قابل تکرار به هم وصل
می‌کند. Artifact ۳۸تایی Run 380 و سه قاب اصلاحی Command Center بازبینی شدند؛
Artifact ۳۹تایی Run 386 با index، ابعاد و SHA-256 همهٔ تصویرها تطبیق شد؛
قاب‌های 04/39/21 بازبینی شدند. Artifact ۴۰تایی Run 388 با index و SHA/ابعاد
همهٔ فایل‌ها تطبیق شد؛ تصویر 40 نسخهٔ فعلی سرور، پیش‌نویس نویسنده و مسیر
ویرایش دوباره را هم‌زمان در viewport نشان می‌دهد. Artifact ۴۱تایی Run 393
نیز با index و SHA/ابعاد و Source/Run تطبیق شد؛ تصویر 41 Preview مسدود،
تأیید و دکمهٔ اجرای غیرفعال را در قاب نشان می‌دهد.
Artifact ۴۲تایی Run 396 نیز با index و SHA/ابعاد و Source/Run تطبیق شد؛
تصویر 42 پیام خطای callback و دکمهٔ ورود دوباره را واضح نشان می‌دهد.
Artifact ۴۳تصویری Run 398 با index و SHA/ابعاد و Source/Run تطبیق شد؛ PDF همراه
هم از نظر digest و ساختار معتبر است. قاب 43 همان UI تعاملی را در media چاپ نشان
می‌دهد. PDF A4 در ۲۲ صفحه تولید می‌شود؛ صفحهٔ نخست ناوبری بریده و کنترل‌های
فرم را چاپ می‌کند. در MS38، Print System و بازبینی نهایی `VX-G1` باز بودند.

Run 399 مستندات MS38 هشت Job سبز شد. Source MS39 در Run 400 نیز هر هشت Job
را پاس کرد؛ Artifact `10992023150` تمام ۴۳ PNG و PDF را با SHA/ابعاد و
Source/Run معتبر دارد. قاب 19 کنتراست کارت حساب Wizard را پس از اصلاح و
قاب 41 همان مانع Preview را نشان می‌دهد. جمع‌بندی ممیزی، پوشش ۱۱ Route و
۳۰ State و Gapهای باقی‌مانده در `pmcs-v1.1-vx-g1-audit-review.md` است.
پذیرش `VX-G1` فقط پس از CI مستندات MS39 انجام می‌شود؛ `VX-G3/G4/G5`
برای سیستم طراحی، مهاجرت و Qualification بازند.

Run 401 مستندات MS39 هشت Job سبز شد و `VX-G1` به‌عنوان Gate ممیزی پذیرفته
شد. Source MS40 در Run 402 هر هشت Job سبز دارد؛ Artifact `10993615536`
با digest `sha256:22d894acbc312fc26251f5aafb3aaff4c7bc8a548a6291e78adb4a94afbe429d`
شامل ۴۴ PNG، PDF و index است. Hash/ابعاد و Source/Run همهٔ فایل‌ها تطبیق
شدند؛ قاب 18 حالت بدون فایل و قاب 44 نام فایل انتخابی را فارسی و خوانا
نشان می‌دهند. `VX-G3/G4/G5` و Qualification فونت/چاپ بازند.
Run 403 مستندات MS40 نیز هشت Job سبز شد و MS40 Safe است. Source MS41 در
Run 404 هر هشت Job را پاس کرد؛ Artifact `10993184623` با digest
`sha256:2d75917180d3f2d5474426afcec050e6175dcdec85491594f14acec299747edc`
از نظر Source/Run، SHA و ابعاد همهٔ ۴۴ تصویر و PDF همراه معتبر است. قاب‌های
20/21/22/23 راهنمای Navigation موبایل را نشان می‌دهند. پذیرش MS41 به CI
مستندات وابسته می‌ماند؛ `VX-G3/G4/G5` بازند.
تأیید طرح و فونت فارسی تازه، مهاجرت تمام Componentها، آزمون visual diff، کنتراست،
keyboard/screen-reader، چاپ و Performance در Gateهای مستقل UX2 باقی می‌مانند.

Run 405 مستندات MS41 هشت Job سبز شد و MS41 Safe است. Source MS42 در Run 406
هر هشت Job را پاس کرد؛ Artifact `10995736502` با digest
`sha256:a6cfa48ae684a52278c92bbf41124d67b893f99c628bb7b44009ec0e464a97b7`
شامل ۴۴ PNG، PDF و index معتبر از نظر Source/Run، SHA و ابعاد است. قاب 26
Skeleton خنثی را پیش از پاسخ و قاب 27 پیام خطای واقعی را بدون Placeholder
نشان می‌دهد. CI مستندات شرط پذیرش MS42 و `VX-G3/G4/G5` بازند.

MS42 documentation در Run 407 هشت Job سبز شد و Safe است. MS43 correction
در Run 409 نیز هر هشت Job را پاس کرد؛ Artifact `10996804117` با digest
`sha256:636f371259b6a888a09bbbdce5d52825d06983b4271bc2ab98a87a1b8a4c9761`
شامل ۴۴ PNG، PDF و index معتبر از نظر Source/Run، SHA و ابعاد است. قاب 43
و PDF یک‌صفحه‌ای A4 با Poppler بازبینی شدند؛ فرم/Navigation چاپ نشده و متن
NoSnapshot صادق است. CI مستندات شرط پذیرش MS43، Golden چاپ و `VX-G3/G4/G5`
بازند.

MS45 documentation در Run 415 هر هشت Job را پاس کرد و Safe است. Source
MS46 `4d320c13e3a6f66c6c7d7d836891fcbbe14311b3` / tree
`2984d1db45a9a466b7ea3cee31e4afea3c5fa326` قاب 45
`45-bootstrap-disabled-hover` را به همان Preview مسدود می‌افزاید.
E2E با Hover واقعی باید ثابت‌ماندن رنگ دکمهٔ Disabled و صفرماندن درخواست
Execute را کنترل کند. این تصویر مکمل قاب 41 و State سی‌ودوم inventory
است. Run 416 هشت Job سبز و Artifact `11001141954` با digest
`sha256:903dcb4bd7710b82a8c96acccb6c268bf0fb148c3e6502cbc257022a57de19b6`
از نظر ۴۵ PNG/PDF، Source/Manifest/Hash و ابعاد معتبر است. قاب‌های 41
و 45 با Hover Disabled پیکسل‌به‌پیکسل برابر و پیام مانع و Action غیرفعال
خوانا هستند؛ E2E نبود Execute را Assert کرد. CI مستندات شرط پذیرش است.
