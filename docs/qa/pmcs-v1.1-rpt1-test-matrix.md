# PMCS V1.1 — RPT1 Test Matrix و Qualification Contract

- شناسه: `PMCS-QA-RPT1-001`
- نسخه: `1.5.0`
- وضعیت: Connected core + security/recovery + capacity/fairness + operational observability + safe orphan remediation passed؛ extended RPT1 qualification open
- Parent V1.1 qualification contract: `pmcs-v1.1-test-and-qualification-contract.md`

## ۱. اصل Gate

RPT1 فقط با زیبا بودن PDF یا سبزشدن Build بسته نمی‌شود. داده، Permission، as-of، template،
hash، خروجی binary و بازتولید باید روی یک Candidate commit ثابت اثبات شوند. Missing/Skipped یا
Artifact متعلق به SHA دیگر Gate را fail می‌کند.

## ۲. Unit و Domain

- lifecycle Run و transitionهای نامعتبر؛
- immutable Template/Snapshot/Output؛
- canonical parameter ordering و hash؛
- source manifest ordering و deterministic hash؛
- as-of انتخاب نسخه Approved/Superseded؛
- NoData/InsufficientData/NotConfigured semantics؛
- retry budget و final-state guards؛
- verification code و manifest hash؛
- safe filename و CSV/XLSX formula escaping؛
- Jalali leap year، پایان اسفند، گذار فروردین و Tehran boundary؛
- IRR/Toman label بدون تبدیل پنهان.

## ۳. Contract و Architecture

- Descriptor `reporting.center` و چهار Permission؛
- Navigation fail-closed و feature flag؛
- `reporting.report.completed.v1` schema؛
- منع cross-module Persistence/SQL؛
- FieldOperations فقط DTO read contract صادر کند؛
- Documents فقط Generated Document contract صادر کند؛
- OpenAPI/Problem Details و backward compatibility؛
- generic Documents download برای ReportOutput مسدود باشد؛
- عدم وجود endpoint Template code/SQL upload؛
- عدم وجود dependency به Intelligence/LLM.

## ۴. Integration — PostgreSQL و Object Storage

- Migration 42، Catalog seed و Migration forward شمارهٔ 43 برای non-unique verification lookup؛
- create/queue/claim/process/success transaction؛
- crash پیش و پس از object upload؛
- retry بدون duplicate Run/Output/Document؛
- `FOR UPDATE SKIP LOCKED` با دو Worker؛
- Audit/Outbox/Idempotency/Correlation lineage؛
- snapshot JSON/hash و output manifest/hash؛
- Shared Document owner/type/classification/retention؛
- download bytes، content type، size و SHA-256؛
- backup/restore شامل Schema Reporting و object version reference؛
- rollback با feature flag بدون حذف داده.

## ۵. Permission و Security

- Allow/Deny/No Context/Cross Tenant/Cross Project؛
- suspended actor و revoked membership؛
- revocation میان request/processing/download؛
- Reporting permission بدون Source permission و برعکس؛
- Observer create denial و read/download policy؛
- Portfolio scope بدون دسترسی پنهان؛
- restricted classification leakage؛
- IDOR روی Run/Output/Document؛
- formula injection و text escaping؛
- unbounded parameter/page/row denial؛
- log/diagnostic بدون payload حساس؛
- verification endpoint بدون Auth/Permission؛
- malformed PDF/XLSX یا storage tampering fail-closed.

## ۶. Golden semantic dataset

Dataset ثابت نخست باید این زنجیره را بسازد:

1. Daily Report v1 در تاریخ مشخص با Facts همه Kindهای مجاز؛
2. Approval v1؛
3. Correction v2 با Fact کپی‌شده، Fact حذف‌شده و Fact جدید؛
4. Approval v2 و Supersede اتمیک v1؛
5. یک Draft v3 که هرگز وارد گزارش رسمی نمی‌شود؛
6. Location ID/name snapshot، Measurement link، quantity/unit و Critical issue؛
7. cutoff پیش از v2 و cutoff پس از v2.

Assertions:

- cutoff اول v1 را current official می‌داند؛
- cutoff دوم v2 را current official و v1 را history می‌داند؛
- Draft v3 غایب است؛
- lineage `copiedFromFactId` و `supersedes` حفظ شده است؛
- اجرای دوباره hash یکسان می‌دهد؛
- تغییر یک Fact رسمی hash را تغییر می‌دهد؛
- هیچ Fact از Tenant/Project دیگر وارد نمی‌شود.

## ۷. PDF Golden و Visual

- PDF header معتبر و parseable؛
- فونت فارسی embed، RTL/Bidi و ارقام فارسی؛
- لوگو، عنوان، Project code/name، تاریخ شمسی، revision، cutoff و verification؛
- A4 Portrait و در صورت نیاز A3/Landscape؛
- table header repeat و page break روی dataset چندصفحه‌ای؛
- footer و شماره صفحه؛
- NoData/InsufficientData visual state؛
- watermark policy؛
- deterministic metadata و hash؛
- pixel/structural Golden در Chromium/renderer ثابت؛
- print در Desktop/Tablet/Mobile preview بدون overflow.

## ۸. XLSX Golden

- ZIP/OpenXML معتبر و قابل بازشدن؛
- sheet names و ordering ثابت؛
- metadata sheet و data sheet؛
- نوع عدد/متن، unit/currency و no hidden conversion؛
- date display شمسی و canonical UTC/ISO metadata؛
- Freeze pane/filter/RTL sheet view؛
- هیچ Macro، external link یا formula غیرمجاز؛
- prefix formula به text امن تبدیل شود؛
- deterministic workbook properties و hash؛
- row/column count و content digest با Snapshot برابر باشد.

## ۹. API/E2E

- Catalog فقط Definition مجاز؛
- Create Run و polling `Queued → Processing → Succeeded`؛
- safe failure/Retry/Cancel؛
- status در reload باقی بماند؛
- download و verify؛
- Loading/Empty/Error/No Permission/NoData/Stale states؛
- RTL، Responsive، Keyboard/Focus و Persian date input/display؛
- قطع اتصال Client هنگام polling و resume بدون create duplicate؛
- Login/BFF واقعی؛ Test Authentication در browser qualification ممنوع.

## ۱۰. Load/Soak و Failure

- concurrency حداقل ۲۰ Run روی dataset مرجع؛
- queue fairness میان Projectها؛
- P95 هدف گزارش روزانه دوفرمتی زیر ۳۰ ثانیه؛
- سقف page/row/bytes و timeout؛
- storage slow/down، database reconnect و worker restart؛
- retry storm و poison run isolation؛
- cancellation؛
- metrics cardinality و log volume؛
- restore و replay پس از restart.

## ۱۱. Regression اجباری

- تمام ۷ Suite V1 بدون کاهش پوشش؛
- EXT1 manifest/architecture tests؛
- DOC1 file/quarantine/permission tests؛
- IAM1 login/profile/privacy tests؛
- PRJ1 preview/execute/non-copy tests؛
- Persian UI/calendar audits؛
- migration از Locked V1 و current V1.1 baseline؛
- PostgreSQL/MinIO/Identity container و restore drill؛
- Browser scenarios موجود.

## ۱۲. Evidence لازم برای بستن RPT1

- start/end commit و tree؛
- CI run با هشت Job و Qualification report؛
- شمارش C#/Web/E2E test؛
- Migration count و restore evidence؛
- Golden snapshot/PDF/XLSX digest؛
- Permission matrix result؛
- connected PostgreSQL/Object Storage result؛
- known limitations و Non-Scope؛
- artifact digest و retention؛
- Checkpoint document بدون ادعای V1.1 Feature Complete/Qualified/Locked.

## ۱۳. Coverage موجود در Source Candidate Slice 02

commit `ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173` با tree
`4deccade8899a2438485fb1304cd918115af2654` پوشش Source زیر را اضافه کرده است:

- Domain tests برای retry/cancel guard، stable identity، Jalali/Tehran و formula escaping؛
- deterministic XLSX byte test، OpenXML namespace/RTL/no-formula و verification path؛
- contract test برای مالکیت Documents، render-before-publish، output gates، Migration 43 و default-off؛
- TestHarness متصل برای Observer deny، Create replay/conflict، Queue/Worker/XLSX، Download/Verify،
  cross-project denial و PDF license fail-closed + explicit retry؛
- assertion دیتابیس برای Run/Snapshot/Output، Released Document، Retention، Audit، Outbox و
  Idempotency؛
- رگرسیون محلی اجراشده: ابزارها `40/40`، Web `139/139`، lint، audit فارسی/تقویم و Web build؛
  Repository validator روی ۳۳۴ فایل و system contract audit روی ۲۷۴ endpoint / ۲۰۴ mutation پاس شد.

Run 99 (`35381177208`) روی PR merge commit
`ebbe1090f42cf6bd58928d8844a4b0f86e6abcf6` با همان source tree اجرا شد. هر هشت Job سبز شدند:
`295/295` تست C#، `139/139` تست Web، پنج browser scenario موجود، `13/13` assertion Reporting روی
PostgreSQL/Object Storage، migration/QA verification و Restore Drill با ۴۳ Migration پاس شدند؛
Qualification artifact `10562821142` نیز هفت Suite و صفر failure ثبت کرد.

این موفقیت فقط coverage فعلی CI را اثبات می‌کند. PDF با license واقعی و Golden/pixel، Golden معنایی
کامل، malformed/tamper و revocation گسترده، concurrency دو Worker، crash windows، cancellation
متصل، load/soak، observability و Browser UI اختصاصی Reporting همچنان Gate باز هستند.

## ۱۴. Coverage افزوده‌شده در Qualification Slice 03

Candidate `b4da1e951debf76e1ba3b398bde2ccf60fbde5de` با tree
`aa4063214ad1dea8fac19685a81818623296c24c` در Run 102 (`35383686315`) موارد زیر را متصل پاس کرد:

- `6/6` assertion Cancel با Worker خاموش، replay و final-state/no-output؛
- `6/6` کنترل security برای anonymous/cross-tenant، generic Documents، Membership suspension،
  metadata tamper fail-closed/Audit و restore؛
- assertion دیتابیس Audit/Outbox/Idempotency لغو؛
- حفظ `13/13` هارنس Core Reporting، Restore ۴۳ Migration و هر هفت Suite Regression.

این coverage، worker-time revocation، object-byte tamper، concurrency/crash، load/soak،
observability، Golden و UI اختصاصی Reporting را پاس‌شده اعلام نمی‌کند.

## ۱۵. Coverage افزوده‌شده در Qualification Slice 04

Candidate `e1ac3263df53a245b1aefb338a015be4854d367b` با tree
`f58881f7e0a77bf89f65b872d4f988bd154a809f` در Run 104 (`35390054888`) موارد زیر را متصل پاس کرد:

- دو Worker واقعی با identity مستقل؛ Worker B در زمان lock بودن Run اول، Run دوم را با
  `SKIP LOCKED` تکمیل کرد؛
- rollback و recovery پس از `SIGKILL` Worker A؛
- stale lease در `BuildingSnapshot`؛
- crash-before-storage و crash-after-storage؛
- reuse Document پایدار و نبود Output/Document/Audit/Outbox تکراری؛
- attemptهای bounded برابر `1,1,2,2,2` برای پنج fixture؛
- preparation و harness نهایی هر `5/5` و orchestration مستقل هر `15/15` assertion؛
- حفظ `298/298` تست C#، `139/139` تست Web، پنج browser scenario، Restore ۴۳ Migration و هر هفت
  Suite Qualification.

این coverage، worker-time revocation، object-byte/missing/malformed tamper، orphan inventory،
retry storm/load/soak/fairness/budgets، metrics/heartbeat/alert، Golden معنایی/XLSX، PDF قانونی و
UI اختصاصی Reporting را پاس‌شده اعلام نمی‌کند.

## ۱۶. Coverage افزوده‌شده در Qualification Slice 05

Candidate `167133fc1985c5b57c3dac90535f7a962dfd03b7` با tree
`34fb70aee9a62a434a8446444d7c6d5c6c9819bd` در Run 108 (`35393509764`) موارد زیر را متصل پاس کرد:

- recheck مجوزهای `reporting.run.create` و `field.daily-reports.read` بلافاصله پیش از Storage؛
- تعلیق Membership حین Rendering، شکست با `reporting.permission.revoked`، attempt برابر یک و نبود
  Output، Generated Document، release Audit و Outbox؛
- processing permission snapshot با هر دو تصمیم ردشده و Audit دارای Worker lineage؛
- byte-tamper واقعی MinIO و شکست fail-closed هر دو Verify و Download؛
- missing object و malformed object با پاسخ fail-closed، چهار Audit جدید و restore قطعی byte اصلی؛
- هارنس object security با `6/6` و orchestration آن با `8/8` assertion؛
- preparation/final worker revocation هر `1/1` و orchestration آن با `8/8` assertion؛
- inventory یک orphan در crash-after-storage و صفر orphan پس از recovery؛ در نتیجه recovery
  orchestration به `17/17` assertion رسید؛
- حفظ `302/302` تست C#، `139/139` تست Web، پنج browser scenario، Restore ۴۳ Migration و هر هفت
  Suite Qualification با صفر failure.

این coverage وجود sweeper/remediation تولیدی orphan را ادعا نمی‌کند. retry storm، poison
isolation، load/soak/fairness/budgets، metrics/heartbeat/queue-age/alert، Golden معنایی/XLSX، PDF
قانونی/Golden/performance و UI اختصاصی Reporting همچنان Gate باز هستند.

## ۱۷. Coverage افزوده‌شده در Slice 06 Micro-Step 02

Candidate `346fbb778aa5c4475fd48df3241b700341e96d83` با tree
`98b25e2dcd109356bdea08de138995f271260cfc` در Run 113 (`35437832281`) موارد زیر را متصل پاس کرد:

- preparation هر `21/21` fixture و verification harness هر `22/22` assertion؛
- ۲۰ Run سالم با attempt برابر یک، ۲۰ Output، ۲۰ Generated Document و lineage یکتای Audit/Outbox؛
- poison isolation با دو requeue، failure نهایی در attempt سوم و صفر Output/Document/completion؛
- P95 برابر `5.529s` در برابر budget سی‌ثانیه‌ای؛
- orchestration ظرفیت `11/11` و fairness `9/9` با دو پروژه و دو Worker؛
- دو row lock مستقل، انتخاب پروژه B پیش از backlog بعدی پروژه A، rollback و cleanup کامل پس از
  `SIGKILL`؛
- حفظ `311/311` تست C#، `45/45` تست قراردادی، `139/139` تست Web، پنج browser scenario، Restore
  ۴۳ Migration و هر هفت Suite Qualification با صفر failure.

Run 112 پیش از Candidate نهایی هر دو سناریوی جدید را پاس کرده بود و فقط به‌دلیل تفاوت format
boolean بین `t` و `true` در assertion دیتابیس شکست خورد. Fix نهایی صرفاً expectation را هم‌تراز کرد
و Run 113 کل زنجیره را دوباره پاس کرد. exporter/scrape و alert delivery، remediation orphan،
Golden معنایی/XLSX، PDF قانونی/Golden/performance و UI اختصاصی Reporting همچنان Gate باز هستند.

## ۱۸. Coverage افزوده‌شده در Slice 06 Micro-Step 03 Checkpoint C1

Candidate `83f13cf43679b23a6a169cc0912985b391b1c017` با tree
`1705d184bd494e80e50d8a85b723f0bc63e20abc` در Run 117 (`35441980440`) موارد زیر را پاس کرد:

- Unit qualification نه instrument Meter `Pmcs.Reporting` و allowlist چهار tag کم‌کاردینالیتی؛
- فیلتر fail-closed پاسخ health برای check دقیق `reporting-worker`، چهار مقدار عددی مجاز و حذف
  data سایر checkها؛
- fairness متصل `10/10` با صف aged، دو Worker/دو Project، وضعیت `Degraded`، شرح queue-age و marker
  `health=degraded-queue-age`؛
- negative assertion برای نبود Tenant، Project، User و Run ID در کل readiness payload؛
- حفظ capacity هر `11/11` با ۲۰ Run سالم، poison سه-attemptی و P95 برابر `5.871168s`؛
- `313/313` تست C#، `46/46` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، Restore
  ۴۳ Migration و هر هفت Suite Qualification با صفر failure.

Run 115 فقط روی analyzer `CA1861` و Run 116 فقط پس از اثبات Build/health صحیح روی binding متغیر
shell هارنس متوقف شدند. Fixها به expectation تست و فهرست forbidden identity محدود ماندند. این C1
وجود exporter، scrape pipeline، alert rule یا delivery را ادعا نمی‌کند؛ آن‌ها Gate باز
`PMCS-V1.1-RPT1-S06-MS03-C2` هستند. remediation orphan، Golden معنایی/XLSX، PDF
قانونی/Golden/performance و UI اختصاصی Reporting نیز باز می‌مانند.

## ۱۹. Coverage افزوده‌شده در Slice 06 Micro-Step 03 Checkpoint C2

Candidate `9bb7ede9b89da2078e165cccb2927e0449116909` با tree
`a960cddb5264b3de8857812906b7595db0664ba5` در Run 120 (`35443563270`) موارد زیر را پاس کرد:

- exporter اختیاری OpenTelemetry در composition API، default-off و URI validation fail-closed؛
- subscription محدود به `Pmcs.Api` و `Pmcs.Reporting`، بدون dependency SDK در ماژول Reporting؛
- Collector OTLP/gRPC و scrape Prometheus واقعی با translation نام صریح و scope label خاموش؛
- سه rule queue-age، heartbeat missing/stale و failure/retry در Prometheus نسخه‌پین‌شده؛
- scrape target سالم و مشاهدهٔ `pmcs_reporting_worker_queue_oldest_age_seconds` بالاتر از budget؛
- firing و webhook delivery واقعی `PmcsReportingQueueAgeBudgetExceeded` از Alertmanager؛
- نبود Tenant/Project/User/Run ID و labelهای identity در metric و alert payload؛
- observability delivery هر `5/5`، fairness هر `10/10` و capacity هر `11/11` assertion؛
- ۲۰ Run سالم، poison سه-attemptی و P95 برابر `5.685905s`؛
- `321/321` تست C#، `48/48` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، Restore
  ۴۳ Migration و هر هفت Suite Qualification با صفر failure.

Run 119 فقط compile diagnostic `CS9135` را در scheme pattern آشکار کرد؛ fix نهایی به مقایسهٔ صریح
Ordinal محدود بود. MS03 با Run 120 بسته است. delivery مستقل heartbeat/failure-retry، remediation
تولیدی orphan، Golden معنایی/XLSX، PDF قانونی/Golden/performance و UI اختصاصی Reporting هنوز
Gate باز هستند.

## ۲۰. Coverage افزوده‌شده در Slice 06 Micro-Step 04

Candidate `4ff44c96104ee1df87d267ca9a530d19b9248ba3` با tree
`d4c320e7917121f64a70dea1251169bef4b516ce` در Run 123 (`35445497353`) موارد زیر را پاس کرد:

- modeهای fail-closed و case-sensitive `Disabled|InventoryOnly|ApplyEligible` با default خاموش؛
- حداقل grace بیست‌وچهارساعته، polling، batch و سقف bounded sweep و رد config ناامن؛
- inventory چهار Generated Document روی PostgreSQL/MinIO واقعی و تشخیص سه orphan/یک owned؛
- dry-run بدون تغییر metadata، object یا Audit؛
- apply فقط برای Run نهایی failed، lineage یکتا، owner غایب، retention منقضی و legal hold خاموش؛
- حفظ جداگانهٔ Candidate دارای retention فعال، legal hold و owner موجود؛
- advisory transaction lock مشترک retry/remediation و recheck Documents زیر `FOR UPDATE`؛
- حذف object eligible و حفظ سه object دیگر با `4/4` assertion TestHarness؛
- Audit یکتای `GeneratedReportOrphanRemediated` با lineage/revision و بدون object key؛
- sweep دوم idempotent و کل orchestration remediation هر `7/7` assertion؛
- `328/328` تست C#، `50/50` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، Restore
  ۴۳ Migration و هر هفت Suite Qualification با صفر failure.

Run 122 فقط compile diagnostic `CS1674` را روی lifetime پاسخ metadata نسخهٔ pin‌شده AWS SDK آشکار
کرد؛ fix نهایی به حذف `using` نامعتبر از هارنس محدود بود. MS04 با Run 123 بسته است. این coverage
پاک‌سازی گسترده، bypass retention/legal hold یا rollout Production را مجاز نمی‌کند. Golden معنایی
و XLSX مستقل، PDF قانونی/Golden/performance و UI اختصاصی Reporting هنوز Gate باز هستند.
