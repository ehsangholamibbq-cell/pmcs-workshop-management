# Foundation checkpoint 06 — Contract and Procurement Control Lite

- Date: 2026-09-09
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.7

## Outcome

کنترل قراردادها و تدارکات به‌صورت bounded context مستقل پیاده شد. هیچ Aggregate این Slice به WBS یا Budget Baseline وابسته نیست؛ سقف اولیه قرارداد و برآورد درخواست خرید می‌توانند نامشخص باشند. سیستم فقط پس از صدور Purchase Order تعهد واقعی می‌سازد و Commercial State را جدا از وضعیت عملیاتی و مالی نمایش می‌دهد.

## Implemented

- ماژول مستقل Commercial با schema، migration و dependency boundary؛
- Party Register پروژه‌محور با وضعیت Active/Inactive؛
- Project Contract با مبلغ اولیه nullable و گردش Draft/Submit/Activate/Return/Close؛
- Contract Amendment نوع Scope/Value/Time/Mixed با گردش Draft/Submit/Approve/Return؛
- Purchase Request با Estimated Amount اختیاری و گردش Draft/Submit/Approve/Return؛
- صدور یک Purchase Order از درخواست Approved و ایجاد Commitment واقعی؛
- Close/Cancel کنترل‌شده سفارش و تشخیص تعهد سررسیدگذشته؛
- Revision optimistic concurrency، Permission، Idempotency، Audit و Outbox برای commandها؛
- Snapshot قطعی `commercial-state-v1` با State و Data Quality مستقل Contract/Procurement؛
- حفظ `null` برای سقف مصوب نامعلوم و جلوگیری از صفر مصنوعی؛
- خروجی portfolio-ready برای پروژه‌های قابل دسترس کاربر؛
- Integration Contract برای اعتبارسنجی Contract/Commitment بدون cross-module table access؛
- اتصال اختیاری Finance Record به `contractId` و `commitmentId` واقعی؛
- نمایش خلاصه مستقل Commercial در Project Command Center بدون Composite Health؛
- نقش‌های `ContractAdministrator`، `ProcurementOperator` و `ProcurementManager`؛
- UI ثبت و گردش Party/Contract/Amendment/Request/Order و انتخاب شناسه واقعی در Finance.

## Verification evidence

- .NET Release build: ۱۱ پروژه محصول + پروژه تست، ۰ warning و ۰ error؛
- Domain tests: ۵۴/۵۴ passed؛
- Frontend unit/API contract tests: ۲۴/۲۴ passed؛
- ESLint، TypeScript و Next.js production build: passed؛
- Next.js standalone smoke: صفحه اصلی، Manifest و Service Worker passed؛
- Repository architecture validation: ۱۳۹ فایل C# ماژول بررسی و passed؛
- `git diff --check`: passed.

## Constraints still open

- اجرای واقعی migration و transactionها در PostgreSQL این محیط ممکن نیست، چون Docker/PostgreSQL runtime نصب نیست.
- Party در V1 project-scoped است؛ Vendor Master سراسری، deduplication و ارزیابی تأمین‌کننده هنوز وجود ندارد.
- Commercial Lite فقط ارز پایه پروژه را می‌پذیرد؛ FX و چندارزی خارج از این Slice است.
- RFQ/Tender، انبار، رسید کالا، سه‌طرفه‌سازی، Invoice، صورت‌وضعیت و Retention خارج از این Slice هستند.
- در V1 هر Purchase Request فقط به یک Purchase Order تبدیل می‌شود؛ Split Order هنوز پشتیبانی نمی‌شود.
- Staff UI این ماژول online-first است؛ الزام Offline-first همچنان برای Field App کارگاه و Evidence است.

## Next checkpoint

Vertical Slice «Portfolio Command Center V1»:

1. Portfolio Project Registry برای تمام پروژه‌های مجاز مدیرعامل؛
2. نمایش هم‌زمان Operational، Financial و Commercial State به‌صورت ابعاد مستقل؛
3. Freshness/Data Quality و State missing/disabled بدون رنگ سبز مصنوعی؛
4. فیلتر، مرتب‌سازی و drill-down به Project Command Center؛
5. Action/Approval exceptions بین‌پروژه‌ای با Owner و Due Date؛
6. Snapshot contract نسخه‌دار برای مصرف UI و هوش مدیریتی بعدی.
