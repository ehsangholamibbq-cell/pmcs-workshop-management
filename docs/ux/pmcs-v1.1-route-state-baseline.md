# PMCS V1.1 — Active Route and State Screenshot Baseline

- شناسه: `PMCS-UX-VX-G1-BASELINE-001`
- مرحله: `UX2-MS38`؛ MS37 Runهای 396/397 موفق و Safe، source MS38 Run 398 موفق، CI مستندات شرط اعتبار
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
| `/admin/login-experience` | مدیریت ظاهر ورود | نسخه و گزینه‌های ظاهر | 18 |
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

## دستور بازتولید و Evidence

CI با Commit مشخص، Docker Compose مجزا، Fixture OIDC، دادهٔ QA، Chromium،
viewportهای `1440×900` / `820×1180` / `390×844` و کنترل responsive `320×720`، locale فارسی، timezone تهران،
Light و reduced motion اجرا می‌شود. تصاویر Command Center در هر سه viewport
پس از تغییر اندازه و آماده‌شدن فونت صریحاً به ابتدای صفحه برمی‌گردند. Helper هر
تصویر را پس از Assertion state،
آماده‌شدن فونت، کنترل URL و RTL می‌گیرد؛ انیمیشن و caret غیرفعال‌اند.

پس از `ui-e2e`، فرمان `node tools/qa/visual-baseline.mjs verify` دقیقاً ۴۳ PNG
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
فرم را چاپ می‌کند. بنابراین Print System و بازبینی نهایی `VX-G1` بازند.
تأیید طرح و فونت فارسی تازه، مهاجرت تمام Componentها، آزمون visual diff، کنتراست،
keyboard/screen-reader، چاپ و Performance در Gateهای مستقل UX2 باقی می‌مانند.
