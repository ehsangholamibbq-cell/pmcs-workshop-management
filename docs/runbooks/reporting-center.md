# Reporting Center — Operations and Recovery Runbook

- Checkpoint: `V1.1-RPT1`
- Contract version: `pmcs.reporting/v1`
- Status: MS06 PDF Community/pinning/Golden implementation candidate؛ CI pending و RPT1 فعال

## Operational Observability

اپراتور باید این وضعیت‌ها را ببیند:

- Worker enabled/disabled و آخرین heartbeat؛
- Queue depth و سن قدیمی‌ترین Run؛
- Processing count و duration؛
- success/failure/retry/cancel count بر اساس Definition و format؛
- object publish latency و integrity failures؛
- permission revocation failures؛
- poison run count و retry budget exhaustion.

Label شامل Tenant/Project/User/filename یا محتوای گزارش نمی‌شود.

در Slice 06 MS03-C1، نه instrument Worker و چهار tag کم‌کاردینالیتی به قرارداد تست‌شده تبدیل
شده‌اند. health check در stale/missing heartbeat یا عبور سن صف از budget وضعیت `Degraded` می‌دهد و
readiness فقط `activeRuns`، `heartbeatAgeSeconds`، `oldestQueueAgeSeconds` و `queuedRuns` عددی را
برای `reporting-worker` نشان می‌دهد. این endpoint نباید برای دریافت identity، Snapshot، object key
یا diagnostic detail استفاده شود. در MS03-C2، composition API فقط با endpoint صریح OTLP را فعال
می‌کند. Collector نسخه‌پین‌شده روی `4317` دریافت و روی `9464` برای Prometheus scrape می‌کند؛
Prometheus سه rule queue-age/heartbeat/failure-retry را ارزیابی و Alertmanager آن‌ها را به receiver
محیط تحویل می‌دهد. endpoint خالی exporter را خاموش نگه می‌دارد و URI دارای credential/query/fragment
در startup رد می‌شود.

configهای `deploy/observability` در QA فقط روی loopback هستند. در Production باید TLS، authentication،
network policy، retention و receiver URL محیط به‌صورت deployment-owned فراهم شوند؛ config QA یا
webhook HTTP آن مجوز rollout تولیدی نیست.

Budgetهای پیش‌فرض: `MaximumAttempts=3`، `RetryBaseDelaySeconds=30`،
`MaximumPdfFacts=2000`، `MaximumXlsxRows=5000`، `MaximumOutputBytes=26214400`،
`ProcessingTimeoutSeconds=120` و `QueueAgeWarningSeconds=120`. مقدار صریح خارج از بازهٔ مجاز در
startup fail-closed است. تغییر Production فقط با load evidence و Change Record مجاز است.

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
  دوباره verify می‌کند و Document دوم نمی‌سازد؛
- remediation orphan فقط با mode صریح و پس از grace انجام می‌شود و با Retry روی همان advisory
  transaction lock سریال است؛
- اگر database commit شده ولی response قطع شده، Idempotency replay همان Run را برمی‌گرداند؛
- Snapshot/hash موجود پیش از render مجدد verify می‌شود.

## Generated Document orphan remediation

- مقدار پیش‌فرض `ReportingCenter:OrphanRemediationMode=Disabled` است؛
- `InventoryOnly` فقط Candidateها را می‌شمارد و هیچ metadata، object یا Audit را تغییر نمی‌دهد؛
- `ApplyEligible` فقط برای `ReportOutput` بدون owner، Run نهایی failed، lineage یکتا، grace سپری‌شده،
  retention شناخته‌شده و منقضی و legal hold خاموش مجاز است؛
- Documents باید state، revision، retention و legal hold را زیر `FOR UPDATE` دوباره بررسی کند؛
- حذف metadata و `GeneratedReportOrphanRemediated` Audit اتمیک‌اند و Audit نباید object key داشته باشد؛
- اجرای دوم باید idempotent باشد؛ Candidate owned، recoverable، ambiguous یا protected حذف نمی‌شود؛
- تغییر به `ApplyEligible` در Production فقط با Change Record، backup/restore معتبر، dry-run بازبینی‌شده
  و approval عملیاتی مجاز است؛ worker جای retention scheduler عمومی یا lifecycle storage نیست.

## Semantic/XLSX Golden verification

- fixture Golden باید تاریخ و شناسه‌های رزروشدهٔ مستقل از workflow و Sync داشته باشد؛
- پیش از correction فقط v1 رسمی با revision تاریخی دیده می‌شود و هیچ state/link/time/reason آینده
  نباید در Snapshot یا Source Manifest نشت کند؛
- پس از correction فقط v1 Superseded و v2 Approved دیده می‌شوند؛ v3 Draft و marker آن حذف می‌مانند؛
- دو twin هر cutoff باید Snapshot/Source Manifest و semantic workbook digest یکسان داشته باشند؛
- replay همان Run/Output باید idempotent و دانلود تکراری باید byte-identical باشد؛
- XLSX بدون اتکا به renderer با ZIP/OpenXML parse می‌شود: ۹ entry allowlist، Metadata/Data، RTL،
  freeze pane، auto-filter، نبود formula/macro/external link و تطبیق ۱۸ ستون؛
- مقدارهای formula-like باید با apostrophe متن امن بمانند و quantity/resource/hour عددی parse شوند؛
- خطای Golden باید نام predicate OpenXML شکست‌خورده را بدون انتشار payload یا دادهٔ حساس گزارش کند.

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
- ADR 0030 تصمیم صریح `QuestPDF Community` را برای Qualification ثبت کرده است؛ tier دیگر با
  `reporting.renderer.license_unapproved` fail می‌شود؛
- eligibility Community باید پیش از Production rollout و حداقل سالانه توسط مالک تجاری/حقوقی
  بازاعتبارسنجی شود؛ تغییر شرایط به ADR و tier جدید نیاز دارد؛
- `QuestPDF 2026.8.0`، imageهای build/runtime با digest کامل و فونت‌های vendored DejaVu Sans 2.37
  pin شده‌اند؛ license فونت کنار فایل‌ها قرار دارد؛
- Renderer SHA-256 دو فونت و digest contract را پیش از render کنترل می‌کند؛ نبود فایل، اختلاف
  bytes یا configuration به‌ترتیب با `font_missing`، `font_integrity_failed` یا
  `configuration_unpinned` متوقف می‌شود؛
- مسیر QA ابتدا fail-closed بودن `Unconfigured` را می‌سنجد، سپس فقط برای PDF Golden با
  `Community` restart می‌شود و پس از آن به حالت غیرفعال برمی‌گردد؛
- Qualification شامل PDF byte-identical، parse مستقل، text/RTL، visual digest در ۹۶ DPI، بازبینی
  Poppler و budgetهای cold/warm برابر ۵۰۰۰/۲۵۰۰ ms است.

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

- queue age بیش از budget؛ rule و delivery متصل در Run 120 پاس؛
- failure rate یا retry rate غیرعادی؛ rule load شده، delivery incident مستقل باز؛
- worker heartbeat missing؛ rule load شده، delivery incident مستقل باز؛
- integrity mismatch؛
- permission-denial spike؛
- object storage unavailable؛
- orphan Generated Document؛ inventory/dry-run/apply متصل در Run 123 پاس و rollout تولیدی مستقل است؛
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
نمی‌کند. exporter/scrape و alert delivery هنوز Gate باز این Runbook است.

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

Run 113 (`35437832281`) مسیرهای `verify-reporting-capacity.sh` و
`verify-reporting-fairness.sh` را نیز پاس کرد. ۲۰ Run سالم با attempt برابر یک و P95 برابر
`5.529s` تکمیل شدند؛ poison پس از دو requeue در attempt سوم بدون Output/Document نهایی شد. سناریوی
fairness با دو Worker و دو row lock مستقل، پروژه B را پیش از Run دوم و سوم پروژه A انتخاب کرد و پس
از `SIGKILL` هر دو transaction بدون residue rollback شدند. Capacity هر `11/11` و fairness هر
`9/9` assertion را پاس کرد.

این نتیجه health signal موجود را اثبات می‌کند، اما exporter/scrape، alert rule و delivery تولیدی
هنوز در `S06-MS03` Gate باز هستند. feature flagها همچنان پیش‌فرض خاموش‌اند.

Run 117 (`35441980440`) Checkpoint میانی `S06-MS03-C1` را پاس کرد. fairness اکنون `10/10`
assertion دارد و در زمان نگه‌داشتن صف aged زیر دو row lock، readiness وضعیت `Degraded` با شرح
queue-age و فقط چهار مقدار عددی allowlist‌شده برگرداند. هارنس کل payload را برای نبود Tenant،
Project، User و Run ID کنترل کرد. MeterListener نیز نام نه instrument و allowlist چهار tag را Unit
qualify کرد. این Evidence برای عیب‌یابی داخلی معتبر است، اما تا زمان C2 هیچ scrape target، exporter
یا alert delivery را Production-ready اعلام نمی‌کند.

Run 120 (`35443563270`) ادامهٔ مستقیم C2 را پاس کرد. OpenTelemetry exporter فقط با
`Observability__OtlpEndpoint` فعال شد؛ target واقعی Collector/Prometheus بالا آمد، metric
`pmcs_reporting_worker_queue_oldest_age_seconds` از صف aged خوانده شد، alert
`PmcsReportingQueueAgeBudgetExceeded` firing شد و Alertmanager آن را به receiver ایزوله تحویل داد.
هارنس هر `5/5` assertion target/metric/privacy/firing/delivery، fairness هر `10/10` و capacity هر
`11/11` assertion را پاس کرد. payloadهای metric و alert برای نبود Tenant/Project/User/Run ID کنترل
شدند. این نتیجه MS03 را می‌بندد؛ remediation orphan، Golden/PDF و UI هنوز Gate باز RPT1 هستند.

Run 123 (`35445497353`) مسیر `tools/qa/verify-reporting-orphan-remediation.sh` را نیز پاس کرد.
dry-run چهار Candidate واقعی را بدون تغییر inventory کرد و apply فقط orphan eligible منقضی و بدون
hold را از PostgreSQL/MinIO حذف کرد؛ retention-protected، legal-hold-protected و owned باقی ماندند.
TestHarness وضعیت چهار object را `4/4`، orchestration remediation را `7/7` و sweep دوم را بدون Audit
یا حذف تکراری تأیید کرد. Audit شامل lineage/revision بود و object key نداشت. این نتیجه MS04 را
می‌بندد، اما مجوز rollout Production یا پاک‌سازی orphan مبهم نیست؛ Golden معنایی/XLSX، PDF قانونی
و UI Reporting همچنان Gate باز RPT1 هستند.

Run 130 (`35449387794`) مسیر `verify-reporting-golden` و assertionهای SQL متناظر را پاس کرد. چهار
Run twin، Snapshot/manifest hashهای cutoff، دانلود byte-identical، چهار workbook معتبر OpenXML و
۸/۱۶ ردیف semantic را هر `13/13` assertion تأیید کردند. projection تاریخی v1 revision `11` را
بدون metadata supersession آینده و projection اصلاح‌شده v1/v2 را با revisionهای `12/5` ثبت کرد؛
v3 Draft در هیچ خروجی نبود. این نتیجه MS05 را می‌بندد، اما license قانونی و PDF
Golden/visual/performance یا UI Reporting را پاس‌شده اعلام نمی‌کند.
