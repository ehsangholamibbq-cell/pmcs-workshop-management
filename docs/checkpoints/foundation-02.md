# Foundation checkpoint 02 — Structured field reality and review

- Date: 2026-09-09
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.3

## Outcome

Vertical Slice گزارش روزانه از یک یادداشت ساده به مسیر قابل استفاده «Fact ساختاریافته آفلاین → پذیرش سرور → Submit → Return/Approve» ارتقا یافت. Project State هنوز عمداً ساخته نشده است تا فقط از داده رسمی Approved و قواعد قطعی تغذیه شود.

## Implemented

- Factهای Progress، Labor، Equipment، Material، Issue، Stoppage، SiteCondition و Note؛
- Validation متناسب با Kind در Domain و PWA؛
- ثبت Progress بدون WBS و ثبت Material بدون Budget Baseline؛
- ثبت Issue/Stoppage مستقل از فعال‌بودن HSE؛
- Workflow نسخه‌دار Draft/Returned → Submitted → Approved یا Returned؛
- Review Inbox با Permission مستقل و دلیل الزامی برای Return؛
- transaction واحد PostgreSQL برای Aggregate + Audit + Outbox + Idempotency؛
- دو migration قطعی برای فیلدهای ساختاریافته و metadata بازبینی؛
- PWA RTL با فرم پویا، ذخیره IndexedDB، Submit آنلاین پس از Sync و پنل Conflict/Rejected؛
- جلوگیری از نشت مقدار فیلدهای پنهان هنگام تغییر نوع Fact؛
- نگهداری خطاهای Sync برای بررسی انسانی، بدون overwrite یا resolve خودکار.

## Verification evidence

- .NET Release build: ۰ warning و ۰ error؛
- Domain tests: ۱۲/۱۲ passed؛
- Frontend unit/API contract tests: ۱۰/۱۰ passed؛
- ESLint: passed؛
- TypeScript و Next.js production build: passed؛
- Production standalone smoke: صفحه اصلی، Manifest و Service Worker همگی HTTP 200؛
- Repository architecture validation: ۵۴ فایل C# ماژول بررسی و passed؛
- `git diff --check`: passed.

## Constraints still open

- Authentication واقعی/OIDC و UI مدیریت Role/Grant هنوز پیاده نشده‌اند.
- Submit و Review به اتصال سرور نیاز دارند؛ Capture Fact آفلاین است.
- اجرای migration و Integration Test واقعی PostgreSQL در این محیط ممکن نبود، چون Docker/PostgreSQL runtime نصب نبود.
- Photo queue، S3 upload session و مدیریت attachment هنوز پیاده نشده‌اند.
- Project State projection، Coverage engine و Portfolio Dashboard هنوز در Sprint بعدی هستند.
- Policyهای Role فعلاً code-defined هستند و هنوز Versioned Policy Management ندارند.
- AI عمداً وارد Runtime نشده است؛ ورودی آن باید بعد از Project State قطعی و قابل استناد ایجاد شود.

## Next checkpoint

Vertical Slice «Approved Facts → Deterministic Project State»:

1. projection فقط از گزارش‌های Approved؛
2. Data Coverage و Freshness مستقل از Health؛
3. وضعیت `Unknown/Insufficient Data` به‌جای صفر یا سبز مصنوعی؛
4. موتور Issue/Stoppage aging و Action Inbox؛
5. Photo metadata، upload session و S3-compatible object binding؛
6. اولین Command Center read model بدون نیاز اجباری به WBS، Budget Baseline یا HSE.
