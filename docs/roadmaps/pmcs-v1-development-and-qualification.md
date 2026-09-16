# Roadmap قطعی ادامه توسعه و احراز صلاحیت PMCS V1

- وضعیت: مصوب و لازم‌الاجرا
- تاریخ ثبت در Repository: ۱۴۰۵/۰۶/۲۵ (۲۰۲۶-۰۹-۱۶)
- نوع تصمیم: تکمیل‌کنندهٔ Roadmap و Blueprint قبلی، نه جایگزین یا حذف‌کنندهٔ آن‌ها

## قاعده حاکم

تمام تصمیم‌ها، قابلیت‌ها و مرزهای معماری تصویب‌شدهٔ قبلی PMCS پابرجا هستند. این سند ترتیب ادامهٔ توسعه، الزامات Test-ready/Agent-ready و مسیر رسمی Qualification را به آن‌ها اضافه می‌کند. هیچ قابلیت قبلی نباید برای اجرای این مسیر حذف، ساده‌سازی یا بازتعریف شود. در تعارض احتمالی، تصمیمی پذیرفتنی است که هم قابلیت قبلی را حفظ کند و هم کنترل سخت‌گیرانه‌تر این سند را رعایت کند.

## ترتیب غیرقابل‌تغییر V1

1. Checkpoint 22 — My Work + Notifications + Report Revision
2. Checkpoint 23 — Offline / Sync / Recovery / Conflict Handling
3. Checkpoint 24 — Finance Lite + Financial Integration + Location
4. اعلام وضعیت `PMCS V1 — Feature Complete`؛ این وضعیت به‌معنی Final، Qualified یا Locked نیست.
5. ایجاد `PMCS.TestHarness / PMCS QA Foundation` و QA Gateway مستقل.
6. اجرای Unit، Integration، API/Contract، Permission، Offline، UI/E2E و Agent/Exploratory Tests.
7. رفع خطاها و اجرای Full Regression.
8. اعلام `PMCS V1 Qualified` فقط با Evidence کامل.
9. قفل‌کردن V1 Baseline.
10. آغاز PMCS Intelligence / Agent مدیریتی فقط بعد از Qualification و قفل Baseline.

## کنترل مهندسی Checkpointهای 22 تا 24

- Business Logic نباید در UI یا HTTP Controller/Endpoint قرار گیرد؛ Domain/Application Service مرجع است.
- Application Serviceها باید مستقل، permission-aware، قابل تست و قابل فراخوانی توسط QA Gateway و Toolهای آینده باشند.
- Permissionها مرکزی، fail-closed و مستقیماً قابل تست باقی می‌مانند.
- عملیات مهم باید Audit Trail، Correlation ID، Outbox/Idempotency لازم و Structured Error/Logging داشته باشند.
- Workflow و State Transitionها باید صریح، نسخه‌دار و قابل راستی‌آزمایی باشند.
- Background Job، Sync و پردازش غیرهم‌زمان باید Status و Diagnostics قابل خواندن داشته باشند.
- Read Serviceها باید بدون وابستگی مستقیم به UI طراحی شوند تا بعداً پشت Permission-aware Tool قرار گیرند.
- تمام ورودی، خروجی، نمایش، گزارش، Export و UI تاریخ برای کاربر شمسی است؛ ذخیره و تبادل داخلی همچنان ISO/UTC استاندارد باقی می‌ماند.

## Testability Hooks الزامی

| مرحله | Hookهای لازم |
| --- | --- |
| Checkpoint 22 | Audit verification، Permission testing، شناسه‌های پایدار E2E، Seed/Test Data قطعی، Workflow state verification و Notification verification |
| Checkpoint 23 | شبیه‌سازی قطع اتصال، Reconnect، Retry، Duplicate، Conflict، Recovery، تغییر هم‌زمان دو کاربر، مقایسه Local/Server و Sync diagnostics |
| Checkpoint 24 | Verification مستقل محاسبات مالی، رکورد و Audit مالی، Permissionهای Finance و اعتبارسنجی اتصال بین‌ماژولی |

## QA Foundation پس از Feature Complete

محیط Staging/QA باید کاملاً از Production جدا باشد و حداقل شامل QA Gateway یا MCP-ready API، Test Authentication، QA SuperAdmin، Test Data Factory، Seed/Reset Scenario، Permission/Workflow/Database/Audit Verification، File/Attachment Test، Offline/Sync Test، Log/Diagnostics، Health Check، Regression Runner و Test Report Generator باشد.

در Staging، داده و کاربر آزمایشی، تغییر Permission، Reset و تست مخرب کنترل‌شده مجاز است. در Production هیچ Reset یا destructive test مجاز نیست و QA access محدود و ممیزی‌شده خواهد بود. Impersonation کنترل‌شده باید دید Roleهای Site Supervisor، Finance User، Technical Office، Procurement، Project Manager، Admin و System Admin را قابل آزمون کند.

## Test Suiteهای مستقل

- تقویم شمسی: Leap year، پایان اسفند، گذار فروردین، فیلتر، مرتب‌سازی، ورود، نمایش، serialization، Export، Report و Offline Sync.
- Offline: ایجاد داده و فایل در حالت قطع اتصال، Local persistence، Reconnect، Sync، جلوگیری از Duplicate، Conflict detection/resolution، Audit after sync و تغییر هم‌زمان دو کاربر.
- UI/E2E: Login، Navigation، Form، Validation، RTL، Responsive، Date Picker شمسی، Modal، Table overflow، Empty/Loading/Error state و Mobile/Tablet/Desktop.

## مرز Agent مدیریتی

Agent تا پیش از `PMCS V1 Qualified` اجرا نمی‌شود. مسیر بعدی آن به‌ترتیب Intelligence Foundation، Read-only Project Intelligence، Knowledge/RAG/Evidence/Citations، Draft Actions و Controlled Actions با Human Approval است.

معماری الزامی:

`Agent → Permission-aware Tool → PMCS Application Service → Business Rules → Database`

دسترسی مستقیم Agent/LLM به SQL یا Database ممنوع است. Agent دقیقاً تحت Permission کاربر جاری عمل می‌کند و حق ارتقای دسترسی ندارد. محاسبات قطعی Finance، Progress، Schedule، Contract و Permission در موتورهای PMCS باقی می‌مانند. لایه مدل Provider-independent و پشت Model Gateway خواهد بود؛ UI آینده نیز Executive Intelligence Center است، نه Chat ساده.

## گزارش پایان هر Checkpoint

گزارش بسته‌شدن باید صریحاً Scope تکمیل‌شده، تست‌های اضافه‌شده، Test Hookهای ایجادشده، نتیجه Regression قبلی و آماده‌سازی‌های باز برای QA/Agent را ثبت کند. تا پایان Checkpoint 24 و Qualification کامل، واژه‌های Final، Complete نهایی یا Locked برای V1 ممنوع‌اند.
