# Foundation checkpoint 05 — Optional Calendar and Finance Control Lite

- Date: 2026-09-09
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.6

## Outcome

تقویم پروژه اختیاری و Finance Lite مستقل پیاده شد. عملیات روزانه بدون WBS و بدون تقویم اختصاصی ادامه دارد؛ امور مالی نیز بدون Budget Baseline قابل ثبت و کنترل است. سیستم نبود پیکربندی یا نبود داده را به صفر یا وضعیت سالم تبدیل نمی‌کند.

## Implemented

- Project Calendar نسخه‌دار با حالت `NotConfigured` یا Working Week؛
- `project-state-v2` با `FallbackSevenCalendarDays` و `ConfiguredWorkingDays`؛
- تشخیص Snapshot قدیمی پس از تغییر Revision پیکربندی پروژه؛
- حفظ خوانایی Snapshotهای immutable نسخه v1؛
- ماژول Finance با schema، migration و مرز وابستگی مستقل؛
- Receipt، Payment، Petty Cash Funding و Petty Cash Expense؛
- Draft/Submit/Post/Return همراه Revision، Permission، Idempotency، Audit و Outbox؛
- اصلاح کنترل‌شده Draft/Returned پیش از Submit مجدد؛
- Contract Reference و Cost Center اختیاری؛
- Budget Baseline اختیاری با Draft/Submit/Approve/Return/Supersede؛
- تضمین یک Approved Baseline برای هر پروژه در دیتابیس؛
- Snapshot قطعی Financial State فقط از رکوردهای Posted؛
- جلوگیری از دوباره‌شماری هزینه تنخواه؛
- Budget Comparison nullable تا زمان Approved Baseline؛
- نمایش Financial State در Command Center بدون Composite Health؛
- نقش‌های `FinanceOperator` و `FinanceManager` و Permissionهای ریزعملیاتی؛
- UI ثبت/کارتابل مالی، کنترل بودجه اختیاری و تنظیم تقویم.

## Verification evidence

- .NET Release build: ۱۰ پروژه محصول + پروژه تست، ۰ warning و ۰ error؛
- Domain tests: ۴۱/۴۱ passed؛
- Frontend unit/API contract tests: ۲۰/۲۰ passed؛
- ESLint، TypeScript و Next.js production build: passed؛
- Repository architecture validation: ۱۱۱ فایل C# ماژول بررسی و passed؛
- `git diff --check`: passed.

## Constraints still open

- اجرای واقعی migration و transactionها در PostgreSQL این محیط ممکن نیست، چون Docker/PostgreSQL runtime نصب نیست.
- Finance Lite فقط ارز پایه پروژه را می‌پذیرد؛ FX، تسعیر و چندارزی خارج از این Slice است.
- دفترکل دوبل، حسابداری مالیاتی، چک، مغایرت بانکی، حقوق و انبار مالی در این Slice نیست.
- Finance UI برای واحد ستادی online-first است؛ الزام Offline-first فعلی فقط Field App کارگاه و Evidence را پوشش می‌دهد.
- Holiday exception و شیفت‌های چندگانه در Project Calendar هنوز پیاده نشده‌اند.

## Next checkpoint

Vertical Slice «Contract & Procurement Control Lite»:

1. Party/Contract register مستقل از WBS؛
2. تعهدات قراردادی، الحاقیه و سقف مصوب؛
3. Purchase Request → Approval → Order/Commitment؛
4. اتصال اختیاری Finance Record به شناسه Contract/Commitment واقعی؛
5. Contract/Procurement State مستقل و بدون Composite Health مصنوعی؛
6. Portfolio-ready summary contract برای پروژه‌های متعدد.
