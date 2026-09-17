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
Audit مربوط به Permission Preview علاوه بر Tenant و Actor، شناسه Project هدف را نیز به‌صورت صریح نگه می‌دارد.

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
گیت معماری Repository نیز فهرست Schemaهای Migration را با فهرست Reset مقایسه می‌کند تا Schema جدید به‌صورت خاموش از Reset قطعی جا نماند.

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

این Harness پس از Seed و Diagnostics، دستور `verify` را اجرا می‌کند. این دستور ۲۴ تصمیم مثبت/منفی Permission را برای هر ۱۲ Actor واقعی بررسی می‌کند و سپس یک Workflow واقعی Daily Report را با جداسازی نقش Site Supervisor، Technical Office و Observer اجرا می‌کند. سپس `verify-files` سناریوهای مثبت/منفی فایل، upload/download واقعی MinIO، یکپارچگی محتوا، Permission و Headerهای خصوصی را اجرا می‌کند. دستور `verify-sync` نیز قطع/اتصال را با Lease قبلی و Session جدید، Retry و Duplicate، Operation ID یکسان میان دو کاربر، Conflict هم‌زمان، Resolution، Pull/Checkpoint، Diagnostics و Revocation دستگاه اجرا می‌کند. دستور `verify-exploratory` مرزهای منفی Authentication و QA Gateway را می‌سنجد و تصمیم Permission مرکزی را با رفتار واقعی ۱۲ سطح خواندنی برای هر ۱۲ Persona تطبیق می‌دهد. در پایان `tools/qa/verify-database.sh`، `tools/qa/verify-files-database.sh` و `tools/qa/verify-sync-database.sh` وضعیت Workflow، Migration Ledger، Evidence lineage، Offline Receipt، Actor/Correlation lineage، Audit، Outbox، Idempotency، Change Feed، Device/Lease/Session و Notification را مستقیماً از PostgreSQL مستقل راستی‌آزمایی می‌کنند.

## وضعیت Sliceها

- Slice 1 زیرساخت امن Seed، Reset، Test Authentication، Diagnostics، Permission Preview و Audit را ساخته است.
- Slice 2 ممیزی سراسری و Permission / Workflow / Database / Audit Verification مستقل را اضافه کرده است.
- Slice 3 File / Attachment Verification مستقل، تشخیص امضای محتوا، کنترل یکپارچگی دانلود و Audit دریافت فایل را اضافه کرده است.
- Slice 4 Offline / Sync Verification مستقل، Reconnect با Lease قبلی، Duplicate/Replay، جداسازی Idempotency دو کاربر، Conflict/Resolution، Local/Server checkpoint، Diagnostics و Device revocation را اضافه کرده و با `28/28` assertion در CI متصل تأیید شده است.
- Slice 5 Browser UI/E2E Verification مستقل، Login واقعی Keycloak/BFF، authenticated cold start، RTL/Responsive، تقویم شمسی، Loading/Error/Empty state و چرخه IndexedDB/Service Worker/Browser Restart/Reconnect را اضافه کرده و با Run 64 و بازآزمایی کامل Run 65 بسته شده است.
- Slice 6 Agent/Exploratory Verification یک Explorer قطعی و بدون ارتقای دسترسی برای کل Actorهای Seed، مرزهای منفی Authentication/Tenant/Project، نبود Route مخرب و تطبیق Permission Preview با رفتار واقعی API را اضافه کرده و با `173/173` assertion در Run 67 بسته شده است.
- Slice 7 Regression Runner و Test Report Generator یک Manifest نسخه‌دار برای هفت Suite، Evidence اتمیک وابسته به Commit و گزارش تجمیعی fail-closed اضافه کرده‌اند. Candidate آماده Full Regression متصل است و تا سبزشدن همه Jobها بسته محسوب نمی‌شود.

پس از تأیید متصل Slice 7، فقط Full Regression نهایی، ثبت Evidence، اعلام `PMCS V1 Qualified` و قفل Baseline باقی می‌ماند. Multipart/Malware/Quarantine، Endurance چندروزه دستگاه و چرخه واقعی Object Storage نیز Gate صریح Hardening/Pilot باقی می‌مانند.
