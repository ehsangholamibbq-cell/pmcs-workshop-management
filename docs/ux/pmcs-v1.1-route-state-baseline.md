# PMCS V1.1 — Active Route and State Screenshot Baseline

- شناسه: `PMCS-UX-VX-G1-BASELINE-001`
- مرحله: `UX2-MS34`؛ source correction Run 386 موفق، documentation CI شرط اعتبار `PMCS-V1.1-UX2-MS34-C1`
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
| `/login` | Identity | ورود پیش از OIDC، برند، Motion کاهش‌یافته | 01 |
| `/` | Project Registry | فهرست پروژه | 02 |
| `/portfolio` | Portfolio | عادی Desktop/Mobile، فیلتر خالی، Loading، خطای سرویس | 03، 20، 25–27 |
| `/portfolio/reports` | Portfolio Reporting | خاموش پیش‌فرض، ممنوع، catalog مجاز | 24، 37–38 |
| `/projects/[projectId]` | Project Command Center | نمای اصلی و ۱۱ بخش عملیاتی، Mobile/Tablet، تقویم، اعتبارسنجی آفلاین، صفحهٔ بازگشت آفلاین، انتهای ناوبری با keyboard focus | 04–15، 21، 28–31، 39 |
| `/projects/[projectId]/collaboration` | گفت‌وگوی گروهی پروژه | خاموش پیش‌فرض، ممنوع، پیام مجاز فقط در همان پروژه | 22، 33–34 |
| `/projects/[projectId]/reports` | Reporting Center | خاموش پیش‌فرض، ممنوع، catalog مجاز | 23، 35–36 |
| `/profile` | پروفایل شخصی | اطلاعات عضو | 16 |
| `/admin/users` | مدیریت کاربران | فهرست/دعوت/عضویت | 17 |
| `/admin/login-experience` | مدیریت ظاهر ورود | نسخه و گزینه‌های ظاهر | 18 |
| `/project-bootstraps` | تکثیر پروژه | انتخاب پروژهٔ مبدأ | 19 |

Capture 32 صفحهٔ 404 را پوشش می‌دهد. Capture 39 انتهای Sidebar دسکتاپ را پس از
focus آخرین لینک با نشان غیرفشرده و اسکرول مستقل ثبت می‌کند. Inventory دقیق ID، نام state، viewport و
فایل E2E مالک هر تصویر در JSON ثبت شده است. Stateهای ممنوع و مجاز Chat/Reporting
با پاسخ‌های کنترل‌شدهٔ API در E2E شبیه‌سازی می‌شوند؛ Feature Flagهای Production
هنوز خاموش‌اند و تصویر مجاز به‌معنای فعال‌سازی Feature در defaults نیست.
Chat فقط گروه پروژه است؛ هیچ DM/صوت/تصویر در این inventory وجود ندارد.

## دستور بازتولید و Evidence

CI با Commit مشخص، Docker Compose مجزا، Fixture OIDC، دادهٔ QA، Chromium،
viewportهای `1440×900` / `820×1180` / `390×844` و کنترل responsive `320×720`، locale فارسی، timezone تهران،
Light و reduced motion اجرا می‌شود. تصاویر Command Center در هر سه viewport
پس از تغییر اندازه و آماده‌شدن فونت صریحاً به ابتدای صفحه برمی‌گردند. Helper هر
تصویر را پس از Assertion state،
آماده‌شدن فونت، کنترل URL و RTL می‌گیرد؛ انیمیشن و caret غیرفعال‌اند.

پس از `ui-e2e`، فرمان `node tools/qa/visual-baseline.mjs verify` دقیقاً ۳۹ PNG
اعلام‌شده را از نظر حضور، عدم فایل اضافی، ساختار PNG و اندازهٔ viewport کنترل
می‌کند. `index.json` برای هر تصویر SHA-256، اندازه، Route، state و viewport و
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
Artifact ۳۹تایی correction Run 386 با index، ابعاد و SHA-256 همهٔ تصویرها
تطبیق شد؛ قاب‌های 04/39/21 بازبینی شدند.
پوشش Gapهای conflict، preview تکثیر، خطای Login و چاپ در Micro-Stepهای بعدی
نیازمند screenshot/qualification مستقل است؛ `VX-G1` هنوز بسته نیست.
تأیید طرح و فونت فارسی تازه، مهاجرت تمام Componentها، آزمون visual diff، کنتراست،
keyboard/screen-reader، چاپ و Performance در Gateهای مستقل UX2 باقی می‌مانند.
