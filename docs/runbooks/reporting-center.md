# Reporting Center — Operations and Recovery Runbook

- Checkpoint: `V1.1-RPT1`
- Contract version: `pmcs.reporting/v1`
- Status: Connected core + cancel/security candidate passed؛ extended recovery/observability qualification open

## Health و Metrics

اپراتور باید این وضعیت‌ها را ببیند:

- Worker enabled/disabled و آخرین heartbeat؛
- Queue depth و سن قدیمی‌ترین Run؛
- Processing count و duration؛
- success/failure/retry/cancel count بر اساس Definition و format؛
- object publish latency و integrity failures؛
- permission revocation failures؛
- poison run count و retry budget exhaustion.

Label شامل Tenant/Project/User/filename یا محتوای گزارش نمی‌شود.

این فهرست target عملیاتی RPT1 است. در Source Candidate Slice 02، Audit/diagnostic/correlation
پیاده شده‌اند اما metrics exporter و Worker heartbeat هنوز Evidence اجرایی ندارند؛ نبود آن‌ها نباید
به‌عنوان Healthy تفسیر شود.

## تشخیص Run گیرکرده

1. Correlation ID، Run ID، Definition، Template version و status را بخوانید؛
2. Worker heartbeat و queue age را بررسی کنید؛
3. attempt، claimed/started time، safe diagnostic code و next retry را بررسی کنید؛
4. Audit و Outbox lineage را بدون خواندن Source payload تطبیق دهید؛
5. وجود Snapshot/Output partial و Generated Document را با verification command کنترل کنید؛
6. retry فقط اگر error code retryable و budget باقی است انجام شود؛
7. Run موفق یا Output immutable هرگز به Queued بازگردانده نشود.

## Crash recovery

- claim منقضی فقط پس از lease/timeout رسمی آزاد می‌شود؛
- Worker جدید همان stable output identity را استفاده می‌کند؛
- اگر Document منتشر ولی transaction نهایی نشده، retry با stable identity همان Document و bytes را
  دوباره verify می‌کند و Document دوم نمی‌سازد؛ automated orphan sweeper هنوز در این Candidate
  پیاده نشده و تا Slice recovery باید inventory آن به‌صورت عملیاتی ثبت شود؛
- اگر database commit شده ولی response قطع شده، Idempotency replay همان Run را برمی‌گرداند؛
- Snapshot/hash موجود پیش از render مجدد verify می‌شود.

## Object Storage failure

- public ACL یا لینک عمومی ایجاد نکنید؛
- mismatch hash/size/content type را retry ساده تلقی نکنید؛ Incident integrity ثبت شود؛
- object key یا signed credential در Log قرار نگیرد؛
- restore گزارش فقط با Database و Object Storage هم‌نسخه معتبر است؛
- missing object خروجی را `Valid` نشان نمی‌دهد.

## Permission incident

اگر Actor پس از Queue دسترسی را از دست دهد:

- Run با `reporting.permission.revoked` fail-closed می‌شود؛
- Snapshot یا Output تازه منتشر نمی‌شود؛
- دادهٔ partial در پاسخ API یا Log ظاهر نمی‌شود؛
- پس از اعاده دسترسی، Retry یک تصمیم صریح و audited است.

اگر Permission پس از success لغو شود، Output حفظ می‌شود ولی View/Download/Verify برای Actor رد می‌شود.

## Template rollback

- Run همیشه Template version pin‌شده دارد؛
- Retire کردن Template، Output تاریخی را تغییر نمی‌دهد؛
- rollback Catalog فقط current version را به نسخه Certified قبلی تغییر می‌دهد؛
- Template file/code یا digest موجود بازنویسی نمی‌شود؛
- Run Queued با نسخه retired طبق policy fail می‌شود و خودکار روی نسخه دیگر منتقل نمی‌شود.

## PDF license و فونت Certified

- `ReportingCenter:PdfLicense` به‌طور پیش‌فرض `Unconfigured` است؛ در این حالت PDF با
  `reporting.renderer.license_unconfigured` fail-closed می‌شود؛
- انتخاب `Community`، `Professional` یا `Enterprise` فقط پس از تأیید حقوقی/تجاری سازمان مجاز است؛
- انتخاب tier در code یا image به‌صورت ضمنی ممنوع است؛
- image فعلی `fonts-dejavu-core` را نصب می‌کند و مسیر Regular/Bold صریح است؛ نبود فایل با
  `reporting.renderer.font_missing` متوقف می‌شود؛
- قبل از enable کردن PDF، version/digest image و فونت باید در Golden evidence ثبت شود؛
- qualification باید دو رندر یک Run را byte-for-byte مقایسه کند؛ تا آن زمان deterministic بودن PDF
  اثبات‌شده نیست.

## Feature rollback

1. `ReportingCenter:Phase1Enabled=false` را برای Catalog/Create/Run mutation اعمال کنید؛
2. `ReportingCenter:WorkerEnabled=false` را اعمال و Worker را graceful drain کنید؛ Worker بدون
   Phase 1 نیز fail-closed است؛
3. فقط در صورت نبود Incident امنیتی، `ReportingCenter:OutputAccessEnabled=true` را برای
   Download/Verify خروجی موجود نگه دارید؛ در Incident integrity آن را نیز false کنید؛
4. Runهای Processing را Cancel/Retry خودکار نکنید؛ inventory ثبت کنید؛
5. Schema یا Output را حذف نکنید؛
6. پس از fix، migration forward و regression اجرا شود؛
7. rollback موفق را با queue، hash و permission smoke ثبت کنید.

## Backup/Restore drill

- Database backup باید تمام جدول‌های `reporting` و ledger کامل ۴۳ Migration را داشته باشد؛
- Object Storage version/reference متناظر ثبت شود؛
- restore روی Database ایزوله انجام شود؛
- Catalog/template digest، Run/Snapshot/Output counts و hash نمونه تطبیق داده شوند؛
- حداقل یک PDF و یک XLSX از restored reference خوانده و hash آن verify شود؛
- Restore هیچ Notification یا Outbox خارجی را دوباره publish نکند مگر با replay policy صریح.

## Alertهای لازم

- queue age بیش از budget؛
- failure rate یا retry rate غیرعادی؛
- worker heartbeat missing؛
- integrity mismatch؛
- permission-denial spike؛
- object storage unavailable؛
- orphan Generated Document؛
- migration/catalog digest mismatch؛
- output size/page/row limit breach.

## داده‌ای که نباید در Ticket یا Log قرار گیرد

- Snapshot JSON یا Narrative کامل؛
- Fact مالی/HSE/قراردادی؛
- binary PDF/XLSX؛
- object key یا signed URL؛
- access token، cookie یا API key؛
- query string دارای پارامتر حساس؛
- stack trace خام در پاسخ کاربر.

## Qualification command فعلی

`tools/qa/seed-diagnostics.sh` در محیط ایزوله، Phase 1 و Worker را فقط برای QA روشن می‌کند و
`verify-reporting` را اجرا می‌کند. سناریوی فعلی XLSX را از Catalog تا Queue، Snapshot، Worker،
Generated Document، Download و Verify دنبال می‌کند و PDF با license تنظیم‌نشده را fail-closed و
Retry محدود می‌سنجد. سپس `verify-database.sh` وضعیت Run/Snapshot/Output، Migration 43، Retention،
Audit، Outbox و Idempotency را کنترل می‌کند.

این دستور در Run 99 (`35381177208`) روی PostgreSQL/Object Storage ایزوله اجرا شد و هر `13/13`
assertion Reporting، QA database verification و Restore Drill ۴۳ Migration را پاس کرد. این نتیجه
جایگزین crash/concurrency/revocation/tamper/load یا PDF Golden نیست؛ نبود آن Evidenceها نباید
به‌عنوان Healthy/Qualified تفسیر شود.

Run 102 (`35383686315`) مسیر عملیاتی دوم را نیز اجرا کرد: API با Worker خاموش بالا آمد، Run پیش از
claim لغو شد و replay/final-state بدون side effect تکراری ماند. سپس tenant/auth isolation، قطع
Download پس از suspend شدن Membership و metadata tamper/restore آزموده شد. این رویه فقط در QA
database ایزوله مجاز است؛ دستکاری مستقیم fixture هرگز Runbook تولید نیست.

Run 104 (`35390054888`) مسیر `tools/qa/verify-reporting-recovery.sh` را نیز پاس کرد. این مسیر دو
Process مستقل Worker را با application name و instance ID جدا اجرا می‌کند، lock اولین Run را پشت
pause `AfterSnapshotRowLock` نگه می‌دارد، عبور Worker دوم با `SKIP LOCKED` را می‌سنجد و سپس
`SIGKILL`، stale lease و crashهای `BeforeStorage`/`AfterStorage` را بازیابی می‌کند. preparation و
harness نهایی هر `5/5` و orchestration هر `15/15` assertion را پاس کردند؛ attemptهای نهایی
`1,1,2,2,2` و تعداد Output/Document برای هر Run دقیقاً یک بود.

این pauseها ابزار عملیاتی Production نیستند. فعال‌سازی آن‌ها بدون `PMCS_QA_GATEWAY_ENABLED=true`،
target Run ID و Worker instance ID معتبر fail-closed است. در رخداد واقعی Production، اپراتور فقط
از retry/recovery رسمی و telemetry مصوب استفاده می‌کند و process را برای ساختن crash window دستکاری
نمی‌کند. metrics/heartbeat/queue-age/alert هنوز Gate باز این Runbook است.

Run 108 (`35393509764`) دو مسیر دیگر را پاس کرد. در
`tools/qa/verify-reporting-worker-revocation.sh`، Worker در pause محدود
`BeforeStoragePermissionRecheck` نگه داشته شد، Membership تعلیق و Run پیش از هر انتشار Storage با
`reporting.permission.revoked` متوقف شد. در `tools/qa/verify-reporting-object-security.sh`، byteهای
شیء MinIO دستکاری، حذف و با payload malformed جایگزین شدند؛ Verify/Download در هر حالت fail-closed
و سپس byte اصلی در `finally` restore شد. recovery regression نیز orphan پنجرهٔ after-storage را یک
و پس از recovery صفر شمرد و اکنون `17/17` assertion دارد.

این فرمان‌ها فقط Qualification ایزوله‌اند. در Production تغییر مستقیم Membership/Database/Object
برای شبیه‌سازی Incident مجاز نیست. orphan inventory فعلی Evidence تشخیصی است، نه sweeper یا مجوز
حذف؛ remediation آینده باید dry-run، retention، legal-hold، idempotency و Audit مستقل داشته باشد.
