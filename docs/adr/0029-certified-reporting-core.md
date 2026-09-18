# ADR 0029 — Certified Reporting Core و Semantic Snapshot مستقل

- وضعیت: Accepted for `V1.1-RPT1`
- تاریخ: ۱۴۰۵/۰۶/۲۷ (۲۰۲۶-۰۹-۱۸)
- Parent baseline: `PMCS V1.1 / db08eef7477356f164783dc783a4993f24f39a10`
- تصمیم جایگزین‌شده: ندارد

## Context

PMCS برای خروجی رسمی به گزارش قابل بازتولید، نسخه‌دار، permission-aware و قابل ممیزی نیاز
دارد. خواندن مستقیم جدول‌های ماژول‌ها، ساخت PDF از صفحهٔ مرورگر، Query دلخواه کاربر یا تولید
عدد توسط LLM با مالکیت داده، Permission و قابلیت استناد V1 ناسازگار است.

اولین گزارش استاندارد، «گزارش روزانه رسمی و زنجیرهٔ اصلاحات» است. این گزارش باید فقط نسخه‌های
رسمی Daily Report را مصرف کند و بتواند وضعیت رسمی را در یک `asOf` مشخص بازسازی کند.

## Decision

### ۱. Bounded Context مستقل

ماژول `reporting.center` مالک Catalog، Template Version، Report Run، Semantic Snapshot، Output
manifest، Retry/Diagnostics و Verification record است. ماژول Reporting مالک Fact عملیاتی نیست.

### ۲. دسترسی بین‌ماژولی

Reporting فقط Application Contractهای خواندنی و نسخه‌دار را مصرف می‌کند. نخستین Contract،
زنجیرهٔ Approved/Superseded گزارش روزانه را از FieldOperations برمی‌گرداند. Import یا Query
مستقیم `FieldOperationsDbContext` و هر Cross-schema SQL ممنوع است.

### ۳. Semantic Snapshot

هر Run پس از کنترل Permission یک Snapshot canonical و immutable می‌سازد که شامل این موارد است:

- Definition و Template version؛
- پارامترهای canonical؛
- `asOf` و Time Zone پروژه؛
- Project identity و Configuration revision مرتبط؛
- Source IDs، Source revisions، lineage و زمان رسمی‌شدن؛
- وضعیت `Available/NoData/NotConfigured/InsufficientData`؛
- Permission policy version و تصمیم‌های لازم در زمان درخواست و پردازش؛
- Canonical JSON hash با SHA-256.

زمان اجرای Worker، Run ID، Retry count و دادهٔ عملیاتی غیرضروری وارد hash معنایی نمی‌شوند.
برای Snapshot و Template یکسان، دادهٔ معنایی و خروجی Renderer باید deterministic باشد.

### ۴. مدل اجرای غیرهم‌زمان

`Queued → Processing → Succeeded | Failed | Cancelled`

Worker هر Run را با claim اتمیک، timeout، retry محدود و diagnostic code امن پردازش می‌کند.
قبل از خواندن Source، Permission کاربر درخواست‌کننده دوباره ارزیابی می‌شود. Permission revoked،
Project غیرفعال یا Source ناسازگار به Fail-safe منجر می‌شود و با دادهٔ cached دور زده نمی‌شود.

### ۵. Template و Renderer

V1.1 فقط Templateهای Certified و کدنویسی‌شده/نسخه‌دار را می‌پذیرد. Template دلخواه، SQL،
Expression اجرایی یا HTML/JavaScript کاربر مجاز نیست. Renderer از Contract مستقل برای PDF و
XLSX استفاده می‌کند. CSV فقط برای تعریف‌های صریحاً جدولی مجاز است.

انتخاب کتابخانهٔ فنی Renderer یک Adapter detail است؛ Domain، Snapshot و hash به کتابخانهٔ PDF
یا Excel وابسته نمی‌شوند. Adapter باید مجوز مناسب، پشتیبانی RTL/Unicode، deterministic metadata
و تست Golden-file داشته باشد.

### ۶. فایل خروجی

خروجی با owner type برابر `ReportOutput` در Shared Documents ذخیره می‌شود، اما دانلود عمومی
Documents برای این owner type مجاز نیست. دانلود فقط از endpoint Reporting انجام می‌شود و در
همان لحظه این موارد دوباره کنترل می‌شوند:

- `reporting.output.download`؛
- Permissionهای Source تعریف گزارش؛
- Tenant/Project boundary؛
- Classification و Retention؛
- Released/immutable بودن Document و تطابق hash.

Reporting فقط از Application Contract انتشار فایل Generated استفاده می‌کند و به Persistence یا
Object key داخلی Documents دسترسی ندارد.

### ۷. Audit و Verification

Create، claim، success/failure، retry، download و archive دارای Actor/System actor، Correlation ID
و Audit هستند. `reporting.report.completed.v1` فقط شناسه‌ها، version، classification-safe status و
hash را در Outbox منتشر می‌کند؛ Snapshot یا محتوای گزارش وارد Event نمی‌شود.

هر Output دارای verification code مشتق از manifest hash است. QR می‌تواند به endpoint کنترل‌شدهٔ
Verification اشاره کند، اما هیچ لینک عمومی یا دورزنندهٔ Permission ایجاد نمی‌کند.

### ۸. تقویم و اعداد

لحظه‌ها در Database/API به UTC و تاریخ‌های مدنی به ISO ذخیره می‌شوند. PDF/XLSX/Print قابل مشاهده
برای کاربر فارسی باید تاریخ شمسی، ارقام فارسی، جهت RTL و Time Zone پروژه داشته باشد. واحد پول
و مبنای IRR/تومان باید کنار مقدار صریح باشد و تبدیل پنهان ممنوع است.

### ۹. AI boundary

LLM در محاسبه، Snapshot، جمع، KPI، انتخاب Fact یا تولید رقم گزارش نقشی ندارد. Narrative مشورتی
آینده فقط به‌صورت بخش مجزا، cited و قابل حذف است و در RPT1 Scope اولیه نیست.

## نخستین Vertical Slice

Definition ثابت `daily-report-certified` و Template `1.0.0`:

- انتخاب یک Daily Report chain در یک Project؛
- بازسازی نسخهٔ رسمی در `asOf`؛
- نمایش نسخه جاری و زنجیرهٔ اصلاحات رسمی؛
- Factهای ساختاریافته با Location/Quantity/Unit/Impact و lineage؛
- وضعیت صریح NoData/InsufficientData؛
- Snapshot/hash؛
- PDF و XLSX immutable؛
- Status/Retry/Diagnostics، Download و Verification.

## Alternatives Rejected

| گزینه | دلیل رد |
| --- | --- |
| Query مستقیم Reporting به Schemaهای ماژول | coupling، دورزدن Permission و مالکیت داده |
| تولید گزارش از state فعلی UI | غیرقابل بازتولید و وابسته به Client |
| Print مرورگر به‌عنوان PDF رسمی | نبود hash/template/as-of و fidelity کنترل‌شده |
| Report Builder آزاد در RPT1 | Scope و سطح حمله بالا پیش از اثبات Core |
| تولید عدد یا Narrative رسمی توسط LLM | غیرقطعی و غیرقابل ممیزی |
| ذخیره Output فقط در Reporting object key | دورزدن Shared Documents، Retention و Scanner policy |
| مجازکردن generic Documents download برای ReportOutput | امکان دورزدن Source permission جاری |

## Consequences

- یک Snapshot تکراری ممکن است فضای بیشتری مصرف کند؛ در مقابل، بازتولید و Audit قطعی می‌شود.
- Permission در request، processing و download دوباره سنجیده می‌شود و عملیات کمی پرهزینه‌تر است؛
  در مقابل، Revocation به‌درستی اعمال می‌شود.
- افزودن گزارش استاندارد جدید نیازمند Source contract، Template version و Golden dataset مستقل است؛
  در مقابل، Catalog از Queryهای کنترل‌نشده و عدد نادرست مصون می‌ماند.
- Advanced Builder، Scheduled Delivery و Word output به V1.2 منتقل می‌شوند.
