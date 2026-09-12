# Foundation checkpoint 11.1 — identity continuity audit

- Date: 2026-09-11
- Status: Implemented; live PostgreSQL/Keycloak/SMTP and browser UAT remain environment gates
- Baseline reviewed: Checkpoints 10 and 11

## Audit scope

- Git history, Blueprint invariants, identity migrations, Keycloak realm and service-account permissions;
- BFF account selection, API authentication pipeline, internal permission authority and account lifecycle;
- invitation restart/retry/revoke/expiry races, provider cleanup and CI integration paths;
- Persian error boundary, architecture guard, backend/frontend tests and production builds.

## Gaps found and closed

- شناسه User ساخته‌شده در Keycloak اکنون پیش از تحویل ایمیل روی دعوت پایدار می‌شود؛ بنابراین Restart یا شکست ارسال، هویت بیرونی را گم نمی‌کند.
- لغو، انقضا یا شکست نهایی دعوت، عملیات `Delete` پایدار و Idempotent ایجاد می‌کند. Retry این عملیات هرگز براساس وضعیت UserAccount به Enable تبدیل نمی‌شود.
- ارسال مجدد تا پایان Delete در حال اجرا متوقف می‌شود تا مسابقه حذف و ساخت مجدد رخ ندهد.
- پس از Delete موفق، اتصال شناسه حذف‌شده فقط برای دعوت Failed/Expired آزاد می‌شود تا ارسال مجدد بتواند حساب تازه را بدون تصاحب یا تعارض ثبت کند.
- هر تغییر وضعیت UserAccount، مقدار `access_valid_after` را جلو می‌برد. Middleware مرکزی API وجود `iat`، فعال‌بودن Tenant/User و جدیدبودن Token را بررسی می‌کند.
- فعال‌سازی مجدد حساب، Token صادرشده پیش از تعلیق را معتبر نمی‌کند؛ Token جدید باید در ثانیه‌ای پس از مرز وضعیت صادر شود.
- تست Integration پیش‌تر Seed توسعه را خاموش و هم‌زمان انتظار User نمونه داشت؛ Seed فقط در پایگاه موقت CI فعال شد تا سناریوی تست واقعاً قابل اجرا باشد.
- Smoke مربوط به Service Account در CI اکنون علاوه بر خواندن، Create/Delete یک User موقت را نیز کنترل می‌کند.
- قرارداد Better Auth نسخه نصب‌شده بررسی شد: `getAccessToken` به‌درستی با id رکورد Account فراخوانی می‌شود و تغییر لازم نبود.

## Verification

- Build کامل Release بدون Warning و ۹۷ تست C# موفق؛
- ۵۵ تست واحد، ESLint، ممیزی فارسی و Build تولیدی Next.js موفق؛
- Architecture Guard، YAML/JSON/shell syntax و `git diff --check` موفق؛
- Smoke خروجی Standalone: صفحه ورود و Manifest پاسخ ۲۰۰ با Headerهای امنیتی؛ مسیر مدیریت بدون پایگاه نشست Fail-closed و بدون نمایش محتوا؛
- Migrationهای جدید به‌صورت افزایشی با ترتیب 202 و 203 افزوده شدند و تاریخچه Migration مرحله 11 تغییر نکرد.

## Remaining environment gates

- اجرای واقعی Migration و صف Restart روی PostgreSQL در Job متصل CI؛
- Create/Delete واقعی Keycloak و مسیر ایمیل/Required Actions با SMTP آزمایشی؛
- Restore بسته رسمی JWT و UAT مرورگر برای Login، تعلیق، Refresh، فعال‌سازی مجدد و Logout.

## Invariants preserved

- Keycloak منبع احراز هویت است؛ Tenant role، Project membership و Permission فقط از PMCS می‌آیند.
- WBS، بودجه اولیه، تقویم، HSE و Quality اختیاری و مستقل باقی مانده‌اند.
- AI هیچ Fact، State، Permission یا اقدام رسمی را تغییر نمی‌دهد.
