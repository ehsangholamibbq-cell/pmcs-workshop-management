# Reporting Center — Operations and Recovery Runbook

- Checkpoint: `V1.1-RPT1`
- Contract version: `pmcs.reporting/v1`
- Status: Definition of Ready

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
- اگر Document منتشر ولی transaction نهایی نشده، reconciliation آن را به همان Run متصل یا به‌صورت
  orphan امن علامت‌گذاری می‌کند؛ Document دوم ساخته نمی‌شود؛
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

## Feature rollback

1. feature flag `reporting.phase1` را برای create خاموش کنید؛
2. download/verify خروجی موجود را فقط در صورت نبود Incident امنیتی فعال نگه دارید؛
3. Worker را graceful drain و سپس disable کنید؛
4. Runهای Processing را Cancel/Retry خودکار نکنید؛ inventory ثبت کنید؛
5. Schema یا Output را حذف نکنید؛
6. پس از fix، migration forward و regression اجرا شود؛
7. rollback موفق را با queue، hash و permission smoke ثبت کنید.

## Backup/Restore drill

- Database backup باید تمام جدول‌های `reporting` را داشته باشد؛
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
