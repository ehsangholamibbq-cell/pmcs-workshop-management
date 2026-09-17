# PMCS QA Foundation — مرز اجرایی Seed، Diagnostics و QA Gateway

- وضعیت محصول: `PMCS V1 — Feature Complete`
- وضعیت Qualification: فعال؛ هنوز `Qualified`، `Final` یا `Locked` نیست
- مرجع: Roadmap قطعی توسعه و احراز صلاحیت PMCS V1

## قرارداد امنیتی

QA Gateway فقط با `PMCS_QA_GATEWAY_ENABLED=true` و فقط در محیط‌های `Development` یا `QA` ثبت می‌شود. فعال‌سازی آن روی هر محیط دیگر باعث توقف Startup است. دیتابیس باید نامی با پیشوند `pmcs_qa_` داشته باشد؛ در غیر این صورت برنامه پیش از ثبت Endpointها متوقف می‌شود.

Test Authentication از کلید مستقل `PMCS_QA_AUTH_KEY` استفاده می‌کند. کلید باید حداقل ۳۲ بایت باشد، در Log یا پاسخ API بازگردانده نمی‌شود و با مقایسهٔ زمان‌ثابت کنترل می‌شود. Headerها فقط شناسه Tenant و User را حمل می‌کنند؛ Role از Header یا Token پذیرفته نمی‌شود. Actor باید واقعاً در Identity فعال باشد و Permission از `IProjectPermissionService` محاسبه می‌شود.

مسیر Gateway ثابت و نسخه‌دار است:

- `GET /api/qa/v1/status`
- `GET /api/qa/v1/diagnostics`
- `GET /api/qa/v1/permissions/preview`

هر فراخوانی Gateway نیازمند Permission مرکزی `qa.gateway.use` و دارای Audit Trail با Correlation ID است. Gateway هیچ DbContext یا جدول ماژولی را مستقیم نمی‌خواند.

## Seed قطعی

`PmcsTestDataSet` شناسه‌های پایدار Tenant، Project، Root Location و Actorهای چندنقشی را تعریف می‌کند. QA SuperAdmin یک User واقعی با `TenantAdministrator` است. Actorهای Project Manager، Site Supervisor، Technical Office، Finance، Procurement، Quality، HSE، Observer و Project Controller با Membership واقعی Seed می‌شوند.

Seed فقط با `PMCS_SEED_ENABLED=true` اجرا می‌شود. در حالت عادی Development همان دو Actor قدیمی حفظ می‌شوند؛ مجموعه کامل فقط وقتی QA Gateway فعال است ایجاد می‌شود.

## Reset خارج از API

هیچ Endpoint عمومی یا QA برای Reset وجود ندارد. Reset فقط با Harness خارجی انجام می‌شود:

```bash
PMCS_QA_CONNECTION_STRING='Host=...;Database=pmcs_qa_local;...' \
PMCS_QA_DATABASE_URL='postgresql://.../pmcs_qa_local' \
PMCS_QA_RESET_CONFIRM='RESET:pmcs_qa_local' \
./tools/qa/reset-database.sh
```

Harness نام دیتابیس ADO و URI را مستقل می‌خواند، برابری آن‌ها و پیشوند `pmcs_qa_` را کنترل می‌کند و فقط با عبارت تأیید دقیق Schemaهای شناخته‌شده PMCS را حذف می‌کند. Production و دیتابیس فاقد پیشوند هیچ مسیر Reset ندارند.

پس از Reset، Migration، Seed و Diagnostics با Harness اجرا می‌شود:

```bash
PMCS_QA_CONNECTION_STRING='Host=...;Database=pmcs_qa_local;...' \
PMCS_QA_DATABASE_URL='postgresql://.../pmcs_qa_local' \
PMCS_QA_AUTH_KEY='replace-with-independent-32-byte-secret' \
PMCS_QA_S3_ENDPOINT='http://127.0.0.1:9000' \
PMCS_QA_S3_ACCESS_KEY='...' \
PMCS_QA_S3_SECRET_KEY='...' \
PMCS_QA_S3_BUCKET='pmcs-qa-local' \
./tools/qa/seed-diagnostics.sh
```

## مرز Slice اول

این Slice زیرساخت امن Seed، Reset، Test Authentication، Diagnostics، Permission Preview و Audit را می‌سازد. Full Qualification هنوز شامل Test Suiteهای مستقل API/Contract، Permission matrix، Workflow/Database/Audit verification، File/Attachment، Offline/Sync، UI/E2E، Agent/Exploratory، Regression Runner و Test Report Generator است.
