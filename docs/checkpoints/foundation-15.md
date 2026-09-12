# Foundation checkpoint 15 — Procurement & Inventory Reality V1

- Date: 2026-09-11
- Status: Implemented; PostgreSQL migration/concurrency and browser UAT remain environment gates
- Baseline: Checkpoint 14 (`74373c3`)

## Scope delivered

- ادامه Purchase Request و Purchase Order موجود در bounded context تجاری، بدون ساخت منبع سفارش موازی؛
- گسترش درخواست و سفارش با Supply Item، Quantity، Unit، Delivery Location و پیوندهای اختیاری Work/WBS/Budget؛
- کاتالوگ قلم Material/Service/EquipmentRental با Base Unit و Conversionهای نسخه‌دار؛
- محل‌های Central/Site/Zone/Laydown/Contractor Custody/Quarantine/Rejected با Availability صریح؛
- Goods Receipt با شماره server-issued، Dispatch Note، Arrival، Damaged Quantity، Evidence و کنترل cumulative over-receipt؛
- گردش مستقل Receipt → Pending Inspection → Accepted/Partial/Rejected/Quarantined؛
- PostStock مستقل که فقط Accepted Quantity را وارد Ledger می‌کند؛
- Inventory Ledger افزایشی با Trigger پایگاه‌داده برای رد Update/Delete؛
- Stock Position محاسبه‌شده از کل Ledger و Drill-down محدودشده جداگانه؛
- Material Issue با Evidence، Recipient، Destination و کاهش موجودی؛
- Acknowledgment و Reconciliation افزایشی برای Consumption/Return/Waste بدون یکی‌گرفتن Issue و Consumption؛
- Return-to-stock، Transfer دوطرفه متوازن و کنترل موجودی منفی تحت تراکنش Serializable؛
- Physical Count با System Quantity محاسبه‌شده در سرور، Review مستقل و Adjustment رخدادمحور؛
- Service/Equipment Rental Acceptance مستقل و بدون Stock side effect؛
- Supplier delivery counters بر اساس Receipt، Due Date و نتیجه بازرسی؛
- Permissionهای مجزا برای خواندن، کاتالوگ، دریافت، اضافه‌تحویل، بازرسی، ورود موجودی، تحویل، تأیید، تسویه، انتقال، شمارش، اصلاح و خدمت؛
- رابط فارسی مرکز فرمان با Cache scope‌شده فقط‌خواندنی و عملیات رسمی Online-only؛
- Migration افزایشی `commercial:20260911-002`، Audit، Outbox، Idempotency و optimistic revision.

## Semantic safeguards

- Approved Purchase Request فقط مجوز ادامه خرید است؛ Receipt یا Stock نیست.
- Issued Purchase Order تعهد است؛ تحویل یا پذیرش نیست.
- Physically Received با Accepted، Available و Consumed برابر نیست.
- فقط Inspection پذیرفته‌شده و PostStock صریح Ledger مثبت می‌سازد.
- Quarantine و Rejected Location موجودی قابل مصرف تولید نمی‌کنند.
- Issue آغاز Custody است؛ Consumption/Return/Waste فقط در Reconciliation ثبت می‌شود.
- Physical Count، رکوردهای Ledger قبلی را Update نمی‌کند.
- پذیرش خدمت هیچ موجودی کالایی نمی‌سازد.
- Submittal Approval دفتر فنی Receipt یا Site Acceptance نیست.
- WBS، Budget، Project Calendar، HSE و Quality همچنان اختیاری و مستقل‌اند.
- AI هیچ Receipt، Inspection، Ledger، Issue، Reconciliation یا Adjustment رسمی را تغییر نمی‌دهد.

## Verification

- Build هدفمند ماژول Commercial: بدون Warning؛
- Build کامل C# با Reference آفلاین سازگار JWT: بدون Warning؛
- ۱۳۸ تست C# شامل conversion lineage، تفکیک Receipt/Acceptance/Stock، Ledger direction، Custody و Adjustment موفق؛
- ۷۰ تست Frontend/API contract، ESLint، TypeScript، ممیزی فارسی و Next.js production build موفق؛
- Architecture Guard روی ۲۵۸ فایل C# و `git diff --check` موفق.

Reference آفلاین JWT فقط برای Compile محلی استفاده و پیش از ثبت نسخه حذف شد؛ Repository همچنان فقط به Package رسمی Microsoft متکی است.

## Remaining environment and product gates

- اجرای Migration و Trigger append-only روی PostgreSQL 17 واقعی؛
- تست هم‌زمانی Issue/Transfer/Adjustment و سیاست retry برای Serialization Failure؛
- UAT مرورگر برای Request → Order → Receipt → Inspection → Stock → Issue → Reconciliation؛
- RFQ، Quote، Technical/Commercial Evaluation، Award و Single-source Waiver؛
- Revision/Amendment سفارش و Expediting milestone؛
- Reservation/Hold، انتقال به قرنطینه پس از ورود، Return-to-vendor و Serial-level workflow؛
- Invoice/Receipt/PO three-way match و اتصال کنترل‌شده به Finance؛
- Draft provisional آفلاین تأمین و Conflict Center اختصاصی؛
- اتصال شمارنده‌های Supply به Project State و Portfolio بدون ساخت Composite Health.
