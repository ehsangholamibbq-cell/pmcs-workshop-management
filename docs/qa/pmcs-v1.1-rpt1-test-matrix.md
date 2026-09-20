# PMCS V1.1 — RPT1 Test Matrix و Qualification Contract

- شناسه: `PMCS-QA-RPT1-001`
- نسخه: `1.32.0`
- وضعیت: F05 Runtime Core safe checkpoint؛ Renderer/Wiring/F06–F10/UI/Production باز
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

## ۲۱. Coverage افزوده‌شده در Slice 06 Micro-Step 05

Candidate `38a03f33f4747d0b6a76696705877633acd17678` با tree
`eb9369c9e32eb3f523c4faa22487d6428c2d7e34` در Run 130 (`35449387794`) موارد زیر را پاس کرد:

- fixture قطعی v1 Approved، v2 corrected/Approved و v3 Draft روی تاریخ مستقل `2099-12-27`؛
- هر هشت نوع Fact، measurement quantity/unit، Critical impact و formula-like text امن؛
- copy هفت Fact، حذف material کپی‌شده، replacement بدون lineage کپی و حفظ Draft marker فقط در v3؛
- دو twin پیش و دو twin پس از correction با hash یکسان در هر cutoff و hash متفاوت میان cutoffها؛
- projection تاریخی v1 با state/revision `Approved/11` و بدون metadata supersession آینده؛
- projection پس از correction با v1 `Superseded/12` و v2 `Approved/5` و حذف کامل v3؛
- replay idempotent و دانلود دوبارهٔ همان Output با bytes/SHA-256 یکسان؛
- parser مستقل هر چهار XLSX برای ۹ ZIP entry، sheet order، RTL، freeze pane، filter، metadata،
  نبود formula/macro/external link و تطبیق ۱۸ ستون؛
- ۸ ردیف پیش و ۱۶ ردیف پس، semantic digest برابر میان twinها و متفاوت میان cutoffها؛
- assertionهای مستقیم PostgreSQL برای measurement، chain، Fact lineage، چهار Run/Snapshot/Output،
  دو hash و draft exclusion؛
- Golden متصل `13/13`، `329/329` تست C#، `51/51` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario، Restore ۴۳ Migration و هر هفت Suite Qualification با صفر failure.

Runهای 125 تا 129 به‌ترتیب برخورد نام helper، analyzer type، nullable JSON، دقت cutoff ذخیره‌شده و
تداخل تاریخ fixture Sync را آشکار کردند؛ هیچ‌یک به تضعیف assertion یا تغییر Production منجر نشد.
MS05 با Run 130 بسته است. این coverage تصمیم قانونی license یا PDF Golden/visual/performance را
جایگزین نمی‌کند و UI اختصاصی Reporting نیز هنوز Gate باز است.

## ۲۲. Coverage افزوده‌شده در Slice 06 Micro-Step 06

Candidate `b8f21492a4f44c7c412e5b7eda0b164e7f256758` با tree
`e94b6ba3753e67b42ea0ec99e998761fdad0bcc3` در Run 133 (`35463350892`) موارد زیر را پاس کرد:

- ADR 0030 و پذیرش فقط `Community`، همراه با fail-closed ماندن default `Unconfigured`؛
- pin دقیق `QuestPDF 2026.8.0`، imageهای SDK/ASP.NET و SHA-256 دو فونت DejaVu Sans؛
- integrity check فونت و diagnosticهای `license_unapproved`، `font_integrity_failed` و
  `configuration_unpinned`؛
- دو PDF مستقل byte-identical، PNGهای مستقل pixel-identical و visual digest ثابت در ۹۶ DPI؛
- budgetهای cold/warm `5000/2500 ms` و سقف fixture برابر `5 MiB`؛
- PDF Golden متصل روی fixture MS05 با `8/8` assertion create/replay، success، integrity، stored
  bytes deterministic، text/structure، Draft exclusion و end-to-end budget؛
- PDF متصل یک صفحه، `42489` byte، SHA-256 برابر
  `cc188c842ddcced9a24acd5e18f92c4a6511252c40104055c8c38627cdfac863` و زمان
  create-to-success برابر `3079.1417 ms`؛
- `330/330` تست C#، `52/52` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، Restore
  ۴۳ Migration و هر هفت Suite Qualification با صفر failure.

بازبینی مستقل Poppler clipping، overlap یا glyph شکسته نشان نداد. MS06 با Run 133 بسته است؛ این
coverage Production enablement، eligibility دائمی Community، ۹ خانوادهٔ پیاده‌نشدهٔ Catalog یا UI
اختصاصی Reporting را جایگزین نمی‌کند و RPT1 Active باقی می‌ماند.

## ۲۳. Coverage افزوده‌شده در Slice 07 Micro-Step 01

Candidate `d81ecc00762145210e1c688f8f5843f46d62fc04` با tree
`5f40383ad506d94520c741eb69fcd00086283734` در Run 135 (`35466775368`) موارد زیر را پاس کرد:

- وجود ADR 0031 با تصمیم صریح مالک محصول برای حفظ هر ده خانوادهٔ استاندارد RPT1؛
- شمارش قراردادی دقیق `RPT1-F01` تا `RPT1-F10` و ثبت F02 تا F10 به‌عنوان
  `Required / Not Implemented`؛
- منع Done شدن خانواده بر مبنای Foundation مشترک، placeholder یا Catalog seed؛
- الزام Micro-Slice، semantic/source contract، permission/classification و Golden مستقل هر خانواده؛
- تعیین F02 گزارش هفتگی/ماهانه به‌عنوان Micro-Step بعدی بدون قطعی‌کردن زودهنگام Runtime ID؛
- اثبات عدم تغییر API، Migration، Runtime، feature flag و defaults تولید؛
- `330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، validator
  روی `344` فایل C# و audit ثابت `274/204/5`؛
- هر هشت Job CI و Qualification report با صفر failure.

این coverage فقط تصمیم Scope را qualify می‌کند. F02 تا F10 پیاده نشده‌اند، RPT1 Active است و
Micro-Step بعدی ابتدا DoR/semantic contract خانواده F02 خواهد بود.

## ۲۴. قرارداد Qualification خانواده F02 در Slice 07 Micro-Step 02

قرارداد `PMCS-RPT1-F02-SEMANTIC-001 v1.0.0` پیش از Runtime این Gateها را قطعی می‌کند:

- Weekly نیمه‌باز از شنبه تا شنبه بعد و Monthly از روز اول تا روز اول ماه شمسی بعد در Time Zone
  pin‌شده پروژه؛
- پارامترهای بسته `periodKind` و `periodStartLocalDate` و منع end date/Time Zone/Query دلخواه؛
- انتخاب current official Daily Report هر تاریخ با `approvedAt <= asOfUtc` و حذف Draft و correction
  آینده؛
- precedence دقیق `NotConfigured → NoData → InsufficientData → Available` بدون صفر ساختگی؛
- aggregation فقط در bucketهای هم‌Kind/هم‌unit و منع درصد پیشرفت، conversion و unique-count inference؛
- permission کامل F02، propagation Classification و منع redaction خاموش؛
- Golden matrix چهارده‌سناریویی `F02-W01..D01` برای week/month boundary، leap Esfand، open period،
  missing coverage، correction cutoff، unit split، revocation، isolation، classification و determinism.

Candidate `b4a59fa966320a1da4b53759814224e21893c01e` با tree
`6b5b486dace3c07b0b4e0385413bf1add5aee7a3` در Run 137 (`35474388839`) هر هشت Job را پاس کرد:
`330/330` تست C#، `56/56` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، validator
`344` فایل و audit ثابت `274/204/5`. Restore Drill همان ۴۳ Migration و Qualification report هر
`7/7` Suite را با صفر failure حفظ کرد.

این Micro-Step فقط contract/readiness را qualify می‌کند. هیچ Runtime Definition، Template/schema
ID، Source implementation، Catalog seed، Migration، PDF/XLSX یا feature flag اضافه نشده و F02
`Contract Ready / Runtime Not Implemented` است.

## ۲۵. Runtime Core خانواده F02 در Slice 07 Micro-Step 03

Candidate باید بدون ادعای API یا Renderer این Gateها را پاس کند:

- pin شدن Definition identity و parameter/snapshot/source contract versionها؛
- خواندن period فقط در FieldOperations و منع `FieldOperationsDbContext`/SQL در Reporting؛
- حذف rootهای صرفاً Draft/Submitted/Returned/Rejected و metadata correction بعد از cutoff؛
- fail-closed روی Tenant/Project/window mismatch، duplicate root/date، duplicate current official،
  Fact/Version نامعتبر و Time Zone/boundary مبهم؛
- مرز هفتگی شنبه، ماه شمسی ۲۹/۳۰/۳۱ روزه و رد cutoff آینده یا قبل از شروع؛
- coverage قطعی برای `Daily`، `WorkingDays` و `Weekly` با cutoff محلی pin‌شده؛
- precedence چهار data status و reason codeهای allowlist بدون صفر، درصد یا conversion ساختگی؛
- bucket ordinal `(kind, sourceUnit)` شامل `UnitMissing`، جمع resource observation هم‌Kind و انتخاب
  فقط Issue/Stoppage با impact صریح High/Critical؛
- canonical ordering و hash یکسان در تغییر query order/Run ID/build time؛ تفاوت hash پس از correction
  رسمی و propagation بالاترین Classification Source؛
- اثبات عدم تغییر Migration count، endpoint، Catalog/Template seed، Worker dispatch، Renderer، UI و
  Production flags.

Source commit `6fc28cf54a6df820c49a2365eab76e3550ae421a` با tree
`5188dac79fe5187b319e6aa727da89163fa37c1b` در Run 139 (`35477179493`) هر هشت Job را پاس کرد:
`346/346` تست C# بدون warning، شامل ۱۶ case جدید F02 در `ProjectPeriodicReportingTests`؛ `58/58`
تست Node که `27/27` مورد آن در contract مرتبط RPT1 است؛ `139/139` تست Web؛ پنج browser scenario؛
validator روی ۳۵۰ فایل ماژولی و Restore کامل ۴۳ Migration. Checkpoint
`PMCS-V1.1-RPT1-S07-MS03-C1` فقط Runtime Core را می‌بندد، نه Qualification انتهابه‌انتهای F02.

## ۲۶. Renderer/Golden خانواده F02 در Slice 07 Micro-Step 04

Candidate باید پیش از هر wiring این Gateهای محلی را پاس کند:

- parser و render request روی schema، Definition/Template/Renderer/Layout version، Snapshot hash،
  Source Manifest hash و cutoff fail-closed باشند؛
- canonical render model ترتیب reports/facts/coverage/aggregateها را مستقل از ترتیب collection pin کند؛
- PDF هفتگی RTL/Jalali شامل status/reason، coverage، aggregateهای جدا برحسب unit، resource
  observations، High/Critical، narrative و lineage باشد؛
- دو رندر PDF byte-identical و دو raster qualification در ۹۶ DPI pixel-identical باشند و
  cold/warm/size budgetهای runtime گواهی‌شده را پاس کنند؛
- XLSX دارای هشت Sheet ثابت، entry order/timestamp ثابت، RTL، frozen header، numeric cell واقعی،
  text escaping برای `= + - @` و صفر Formula باشد؛
- `m3`، `M3` و `UnitMissing` مستقل بمانند و هیچ `grandTotal` یا conversion پنهان ساخته نشود؛
- `NoData` و `NotConfigured` data sheetهای header-only و reason صریح بدون `<v>0</v>` داشته باشند؛
- Monthly مرز روز اول تا آخر ماه شمسی را در filename/metadata نگه دارد؛
- wrong format، hash mismatch، PDF fact budget و XLSX row budget fail-closed باشند؛
- Golden قبلی F01 پس از استخراج `CertifiedPdfRuntime` همان visual digest را نگه دارد؛
- `ReportingModule`، `ReportGenerationWorker`، endpointها، Migrationها، Catalog seed، settings و UI
  از F02 Renderer نامی نبرند و defaultهای Production خاموش بمانند.

Golden قطعی، XLSX SHA-256 برابر
`83fd80eedaa1024e84eb253bec76591379fe2f088be12c5b322573d63eb1909d`، PDF SHA-256 برابر
`52ec4e80c34e682f6994ef7a674b161b748a772e34b4e04ec12e27e94c98f989` و visual digestهای
`058a3da3045408a1d87dc9e5c942cd38ffdf7da1921b6594e6ee88a0aa22b396` و
`d61a1090d07d5f21a5d57c15b3abb197a341332b124ba3e8a98461996a42b770` را pin می‌کند. Source commit
`4f68f57de2c2a79b654a19128894d9c89878ab65` با tree
`f4b592c72ea65974c00b936ca59c0428eb47f981` در Run 141 (`35495791821`) هر هشت Job را پاس کرد:
`353/353` تست C#، `60/60` تست Node، `139/139` تست Web، پنج browser scenario، validator روی ۳۵۴
فایل ماژولی و Restore کامل ۴۳ Migration. Checkpoint `PMCS-V1.1-RPT1-S07-MS04-C1` فقط
Renderer/Golden را می‌بندد، نه wiring یا Qualification انتهابه‌انتهای F02.

## ۲۷. Catalog/API/Worker wiring متصل خانواده F02 — Slice 07 Micro-Step 05

Checkpoint جاری پوشش‌های زیر را در pipeline متصل تثبیت می‌کند:

- Migration 44 برای Definition/Template قطعی، contract-versionهای ۸۰کاراکتری و Project profile
  pin‌شدهٔ JSONB؛
- Catalog شامل F01/F02 و نمایش `NotConfigured` برای F02؛
- parser بسته برای `periodKind` و `periodStartLocalDate`، field اضافه، boundary نامعتبر و cutoff؛
- deny ساخت Run توسط Observer، replay یکسان و conflict همان idempotency key؛
- Worker dispatch از period source تا semantic Snapshot و هر دو Renderer PDF/XLSX؛
- Download و Verify با SHA-256، headerهای امن و verification code؛
- assertion مستقل PostgreSQL برای migration/catalog/template، pinned profile، Snapshot
  `NotConfigured`، دو Output، Audit/Outbox/Idempotency و governed Documents.

Run 143 (`35498734639`) مسیر Runtime و هارنس متصل F02 را با `13/13` assertion پاس کرد و فقط query
شواهد shell به‌دلیل تقدم عملگر `->>` پس از `||` شکست خورد. commit نهایی دو expression JSON را
پرانتزبندی کرد و هیچ رفتار Runtime را تغییر نداد.

Source `7fc55c167ad2159a31c895b32a52d78f47574df9` با tree
`d665fe4cdf29369f96ec0875bc6f1535db349d55` در Run 144 (`35498990050`) هر هشت Job را پاس کرد:
`355/355` تست C#، `61/61` تست Node، `139/139` تست Web، پنج browser scenario، validator روی ۳۵۵
فایل ماژولی، system contract audit برابر `274/204/5`، هارنس F02 برابر `13/13`، Restore کامل ۴۴
Migration و Qualification برابر `7/7`. artifactهای Qualification، Integration و UI-E2E به‌ترتیب
`10601457941`، `10601880116` و `10601678972` با digestهای ثبت‌شده در Checkpoint هستند.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS05-C1` اتصال F02 را می‌بندد. UI تغییر نکرده و
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled` در production defaults همچنان `false` هستند؛
F03 تا F10 و RPT1 باز می‌مانند.

## ۲۸. قرارداد Qualification خانواده F03 — Slice 07 Micro-Step 06

قرارداد `PMCS-RPT1-F03-SEMANTIC-001 v1.0.0` پیش از هر Runtime این Gateها را قطعی می‌کند:

- پارامتر Client دقیقاً `{}` و منع `snapshotId`، تاریخ، status، include flag یا Source دلخواه؛
- انتخاب Snapshot رسمی با Tenant/Project، `calculatedAt <= cutoff` و `asOfDate <= cutoffLocalDate`؛
- precedence انتخاب `asOfDate → calculatedAt → snapshotId` و trend حداکثر ۱۴ تاریخ متمایز؛
- Source فقط از Application Contract باریک ProjectIntelligence، بدون DbContext/SQL، Command Center
  HTTP یا Recalculate؛
- جدایی `dataStatus` از Operational/Coverage/Freshness/Confidence و حفظ هشدار `isPartial`؛
- currency براساس Project revision و آخرین Approved Source مؤثر تا cutoff؛
- منع Composite Health، AI summary، inference و join پنهان Finance/Commercial/F04 تا F10؛
- permission کامل `project-state.read`، propagation Classification و منع حذف خاموش Source؛
- ordering canonical Attention/Trend، semantic/source-manifest hash و budget fail-closed؛
- Golden matrix هفده‌سناریویی `F03-C01..D01` برای cutoff، selection، NoData/NotConfigured،
  InsufficientData، outdated، Operational state، trend، Attention، security و determinism.

Contract test وجود سند، هر ۱۷ fixture، reason allowlist، منع Runtime و هم‌راستایی معماری، API،
Security، Roadmap و Canonical Reference را کنترل می‌کند. Candidate
`e3218555a38f7ba460558e51b4db3f8bc17fcd9c` با tree
`1fe4cc804fdd078a71ff2633201c8c690447900e` در Run 146 (`35500809115`) هر هشت Job را پاس کرد:
`355/355` تست C#، `63/63` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، validator
`355` فایل و audit ثابت `274/204/5`. Restore Drill همان ۴۴ Migration و Qualification report هر
`7/7` Suite را با صفر failure حفظ کرد.

این Micro-Step فقط contract/readiness را qualify می‌کند. هیچ C# Runtime، Migration، endpoint،
Catalog/Template seed، Renderer، TestHarness یا UI اضافه نشده و F03
`Contract Ready / Runtime Not Implemented` است.

## ۲۹. Runtime Core محدود خانواده F03 — Slice 07 Micro-Step 07

Runtime Core باید بدون API، Migration، Renderer یا Worker wiring موارد زیر را با Unit/contract test
اثبات کند:

- Runtime identity و parameter/snapshot/profile schema نسخه‌دار؛ پارامتر معنایی همچنان `{}`؛
- Source فقط از `IProjectStateReportingSource` و بدون import از Persistence یا SQL بین‌ماژولی؛
- cutoff و tie-break قطعی، حذف Snapshot پس از cutoff و collapse محاسبات تکراری هر تاریخ؛
- trend حداکثر ۱۴ تاریخ متمایز و ordering قدیم به جدید؛
- `NotConfigured/NoData/InsufficientData/Available` و reasonهای مستقل currency؛
- `isPartial` و Aging بدون تبدیل خودکار به Insufficient و حفظ Operational Status؛
- ordering Attention با `Critical → High → Medium → Low → Unassessed` و lineage کامل؛
- propagation Classification و fail-closed برای Tenant/Project/version/classification نامعتبر؛
- hash یکسان twinها مستقل از query order، Run ID و build time؛
- عدم وجود Composite Health، Finance/Commercial join، Recalculate، endpoint، Migration، Renderer و
  Production enablement.

Source `22d5b0f91edf8d733192fae0ba946c8538c63bca` با tree
`eb5ea4253a7b80e8ab3320b9ccea747624f89790` در Run 148 (`35507127968`) هر هشت Job را پاس کرد:
`377/377` تست C#، شامل `22/22` case متمرکز F03؛ `64/64` تست قراردادی Node؛ `139/139` تست Web؛
پنج browser scenario؛ validator روی `362` فایل ماژولی؛ audit ثابت `274/204/5`؛ Restore کامل ۴۴
Migration و Qualification برابر `7/7` با صفر failure.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS07-C1` فقط Runtime Core را می‌بندد. Renderer/Golden،
Catalog/API/Worker wiring، UI و Production enablement بازند و تمام Suiteهای V1/F01/F02 بدون
Regression باقی مانده‌اند.

## ۳۰. Renderer/Golden خانواده F03 — Slice 07 Micro-Step 08

Renderer مستقل باید پیش از هر wiring این Gateها را پاس کند:

- parser/request روی schema، Definition/Template/Renderer/Layout، snapshot/source-manifest hash،
  cutoff، filename و format به‌صورت fail-closed؛
- render model canonical برای reason، Attention، trend، Fact count و feature state مستقل از ترتیب
  collection؛
- PDF دوصفحه‌ای A4، فارسی/RTL و شمسی با وضعیت‌های مستقل، partial-scope warning، Attention، trend و
  lineage و بدون Composite Health یا inference؛
- دو رندر PDF byte-identical، دو raster ۹۶ DPI pixel-identical و عبور cold/warm/size budget؛
- XLSX هشت‌Sheet ثابت با entry order/timestamp قطعی، RTL، frozen header، numeric cell واقعی، text
  escaping برای `= + - @` و صفر Formula/Macro؛
- `NoData` با data sheetهای header-only و reason صریح، بدون صفر یا وضعیت Stable ساختگی؛
- null impact و `Unassessed` مستقل، feature state فقط به‌عنوان configuration و Attention بدون
  truncate خاموش؛
- text/attention/row/page budget، wrong format و hash/identity mismatch به‌صورت non-transient و
  fail-closed؛
- نبود نام F03 در `ReportingModule`، `ReportGenerationWorker`، endpointها و Migration 44 و خاموش
  ماندن تمام defaultهای Production.

Goldenهای قطعی عبارت‌اند از XLSX
`e19809b6c3ffa5ff3443babe683c9f286c3b928986d176f1d515166f336cf5a3`، PDF
`d765dfc98873fbc07e28b7320524fd156cfa5acc80c6f4da2b2d42941c4e09d1` و visual digestهای
`6d18d03ff3e9100ffe0e12c5da05a6f1976c3d7c1d2ba7f27b36656dcc5ff0ba` و
`252a6dd6c8562242a37e4466dfb4d0a0155831abb39c30e2751309eb3acfa205`.

Source `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f` با tree
`58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434` در Run 154 (`35512969648`) هر هشت Job را پاس کرد:
`383/383` تست C#، شامل شش case Renderer/Golden تازه و `28/28` case متمرکز F03؛ `65/65` تست Node؛
`139/139` تست Web؛ پنج browser scenario؛ validator روی `365` فایل ماژولی؛ audit ثابت
`274/204/5`؛ Restore کامل ۴۴ Migration و Qualification برابر `7/7` با صفر failure.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS08-C1` فقط Renderer/Golden را می‌بندد. Catalog/API/Worker
wiring، UI و Production enablement بازند و تمام Suiteها و Goldenهای V1/F01/F02 بدون Regression
باقی مانده‌اند.

## ۳۱. Catalog/API/Worker wiring متصل خانواده F03 — Slice 07 Micro-Step 09 Safe Checkpoint

Qualification متصل باید بدون endpoint جدید و بدون فعال‌سازی Production این Gateها را پاس کند:

- Migration forward شمارهٔ 45، Definition/Template قطعی، digest و permission برابر
  `project-state.read`؛
- strict parser که فقط `{}` را می‌پذیرد و property اضافه، array/null، Snapshot ID و selector را رد
  می‌کند؛
- pin شدن Project profile نسخه‌دار در پذیرش Run و validate دوباره Tenant/Project/cutoff/accepted-at؛
- allowlist سه‌خانواده‌ای با Source permission مستقل؛ F01/F02 برابر `field.daily-reports.read` و F03
  برابر `project-state.read`؛
- فیلتر per-definition در Catalog و List Runs و re-evaluation در Get/Retry/Cancel/Download/Verify؛
- همگرایی `IReportingReadService` برای Catalog/Run/Output metadata با همان policy و منع نشت میان F01/F02/F03؛
- جلوگیری از bypass مجوز در idempotent replay Retry؛
- Worker dispatch فقط از `IProjectStateReportingSource`، Snapshot builder و Registry اختصاصی F03؛
- re-evaluation `reporting.run.create + project-state.read` پیش از Snapshot و پیش از Storage؛
- parse و integrity check دوباره semantic Snapshot پیش از render؛
- TestHarness با Finance Manager دارای `project-state.read` و فاقد Daily source permission؛ Catalog
  فقط F03، strict rejection، Observer deny، create/replay/conflict و Run filtering؛
- Run کامل `Queued → BuildingSnapshot → SnapshotReady → Rendering → Complete` با `NoData` صریح؛
- دو Output PDF/XLSX، filename امن، SHA-256/ETag/content type، verification و object ownership؛
- عدم تغییر `Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` و PDF license `Unconfigured`؛
- عدم Regression Goldenهای F01/F02/F03، security/recovery/capacity/observability و UI/E2E.

Source `40afeb37d7bf90e97a988cae141901e28d336516` با tree
`ae06285bf1a68fe2592dacc76c7d31cb291ab924` و PR validation merge
`4944731391c2b649cd401fc9095619cb19d41f72` دارای همان tree، در Run 156 (`35515989200`) هر هشت Job
را پاس کرد: `387/387` تست C#، `66/66` تست قراردادی Node، `139/139` تست Web، پنج browser scenario،
validator روی `367` فایل ماژولی، system audit ثابت `274/204/5`، هارنس متصل F03 برابر `14/14`،
Restore Drill کامل `45` Migration و Qualification برابر `7/7` Suite و `12/12` Command با صفر failure.

Qualification artifact `10606892723` با digest
`sha256:6d0eb7ee8a5bb9254946f8e04f1577920cdba9d51ca74f8dd8885aa8e632df6b`، Integration artifact
`10606788092` با digest `sha256:faf08b54b3dba1697c99cbc48a4246cb0a73b5922086a451b15c31fe184fc188`
و UI-E2E artifact `10606882688` با digest
`sha256:ecbfd966d7fa016b572bed3a329a4f266c3fb0d54a5ac2fbea212cc85864ed0f` ثبت شدند.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS09-C1` اتصال End-to-End F03 را می‌بندد. F04 تا F10، UI و
Production enablement بازند و تمام Suiteها و Goldenهای V1/F01/F02/F03 بدون Regression باقی مانده‌اند.

## ۳۲. قرارداد Qualification خانواده F04 — Slice 07 Micro-Step 10

قرارداد `PMCS-RPT1-F04-SEMANTIC-001 v1.0.0` پیش از هر Runtime این Gateها را قطعی می‌کند:

- پارامتر Client دقیقاً `{}` و منع `baselineId`، تاریخ/interval Curve، Planning Mode، forecast flag
  یا Source دلخواه؛
- انتخاب Planning configuration و Baseline رسمی با lifecycle مستقل
  `approvedAt <= cutoff < supersededAt` و منع tie-break روی Baseline هم‌پوشان؛
- Source فقط از Application Contract باریک Planning؛ بدون DbContext/SQL، endpoint زنده
  `/planning/progress` یا `IProgressFactSource` بدون cutoff؛
- انتخاب correction-safe نسخه رسمی Daily Report و Milestone تا cutoff و حذف Draft/Submitted/
  Returned/Provisional؛
- target/unit snapshot در Approval Baseline و منع استفاده از target/profile/status جاری برای تاریخچه؛
- Actual وزن‌دار با cap صددرصد فقط در aggregate، Planned خطی/Milestone، و Variance دقیقاً
  `Actual - Planned`؛
- `MeasurementWeights` با Actual معتبر ولی Schedule/Curve برابر `NotConfigured`؛
- S-Curve با horizon قطعی، grid روزانه یا یکنواخت و حداکثر ۳۶۶ نقطه؛ Actual آینده null و بدون
  Forecast/EVM؛
- جدایی `NotConfigured/NoData/InsufficientData/Available`، reason allowlist و failureهای lineage؛
- هر سه Permission `planning.progress.read`، `planning.baselines.read` و
  `planning.milestones.read`، propagation Classification و منع redaction خاموش؛
- ordering/hash canonical، entry budget و عدم truncate؛
- Golden matrix بیست‌ودوسناریویی `F04-C01..CL01` برای cutoff/correction، lifecycle، status،
  calculation، curve، security، classification و determinism.

Contract test وجود سند، هر ۲۲ fixture، شکاف صریح Runtime جاری، منع Runtime و هم‌راستایی معماری،
API، Security، Roadmap و Canonical Reference را کنترل می‌کند. Candidate
`f8829027c2ce073c207cd0e04a49c306b546c6a1` با tree
`2b784f135894092ef55bf7c7df201b1f03e0c77f` در Run 158 (`35522512734`) هر هشت Job را پاس کرد:
`387/387` تست C#، `68/68` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، validator
`367` فایل و audit ثابت `274/204/5`. Restore Drill همان ۴۵ Migration و Qualification report هر
`7/7` Suite و `12/12` Command را با صفر failure حفظ کرد.

این Micro-Step فقط contract/readiness را qualify می‌کند. هیچ C# Runtime، Migration، endpoint،
Catalog/Template seed، Renderer، TestHarness یا UI اضافه نشده و F04
`Contract Ready / Runtime Not Implemented` است.

## ۳۳. Runtime Core محدود خانواده F04 — Slice 07 Micro-Step 11 Safe Checkpoint

Candidate باید بدون Migration، API، Renderer یا Worker wiring موارد زیر را با Unit/contract test
اثبات کند:

- Runtime identity و parameter/snapshot/profile schema نسخه‌دار؛ پارامتر معنایی همچنان `{}`؛
- FieldOperations فقط lineage رسمی WorkProgress را با cutoff مستقل و بدون Narrative/Comment برگرداند؛
- Planning فقط projection نسخه‌دار configuration/Baseline/target/Milestone/evidence را مصرف کند و
  هر legacy history غیرقابل‌اثبات را fail-closed نگه دارد؛
- انتخاب lifecycle با `approvedAt <= cutoff < supersededAt`، رد overlap و عدم splice Baseline؛
- tie-break Milestone بر پایه `statusDate → approvedAt → updateId` و correction-safe بودن Daily Report؛
- چهار data status، سه status مستقل Actual/Schedule/Curve و reason allowlist مرتب؛
- Actual quantity/manual، missing-null، overrun cap فقط در aggregate، Planned working/calendar days و
  Variance دقیقاً `Actual - Planned`؛
- S-Curve روزانه یا grid یکنواخت، cutoff اجباری، حداکثر ۳۶۶ نقطه و future Actual/Variance برابر null؛
- شمار Factهای Approved خارج Baseline بدون تخصیص، unit mismatch بدون conversion و target pin اجباری؛
- Classification propagation و fail-closed برای Tenant/Project/version/classification/hash نامعتبر؛
- hash یکسان twinها مستقل از query order، Run ID و build time؛
- عدم وجود Forecast/EVM/Composite Health، endpoint، Migration، Renderer و Production enablement.

Source `deb1571ec66d820868e8f4b77b631471e3c8207c` با tree
`9e41495a357480af03f1555ef640962ab863d332` و PR validation merge
`f0d3a5550d9bd1c10d8ddd5a3c0ada24eb0fead5` دارای همان tree، در Run 163 (`35527577826`) هر هشت
Job را پاس کرد: `412/412` تست C# شامل `25/25` case متمرکز F04، `69/69` تست قراردادی Node،
`139/139` تست Web، پنج browser scenario، validator روی `378` فایل ماژولی، system audit ثابت
`274/204/5`، Restore Drill کامل `45` Migration و Qualification برابر `7/7` Suite و `12/12`
Command با صفر failure.

Qualification artifact `10610087527` با digest
`sha256:c5aedf39da9ff26acf0229022be0fc2c07ac4276feea2ea963bfbcafef085aef`، Integration artifact
`10609649369` با digest `sha256:3bd0caa9ce75948cee75835844e5313e4d3aed971d1d843c6b7fb27d45c44fab`
و UI-E2E artifact `10609914179` با digest
`sha256:4ff0672c2ff02f142a4dfc901e563bcaed6249c14ca3d4b4bdbd8c0aac5726f2` ثبت شدند.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS11-C1` فقط Runtime Core را می‌بندد. Renderer/Golden،
Catalog/API/Worker wiring، UI و Production enablement بازند و همه Suiteهای V1/F01/F02/F03 بدون
Regression سبز مانده‌اند.

## ۳۴. Renderer/Golden خانواده F04 — Slice 07 Micro-Step 12

Candidate باید بدون Migration، Catalog/API/Worker wiring یا Production enablement موارد زیر را اثبات
کند:

- Template `1.0.0`، content digest و Renderer/Layout identity ثابت و fail-closed؛
- parser/request/model با تطبیق Definition، schema، cutoff، profile، manifest/semantic hash و filename؛
- نگهداری مستقل data/metric statusها، reasonها و null بدون صفر یا وضعیت سبز ساختگی؛
- PDF فارسی/RTL و A4 افقی با summary، configuration، Baseline، Entry، Milestone، S-Curve و lineage؛
- XLSX با هشت Sheet ثابت `Metadata/Summary/Configuration/Baseline/Entries/Milestones/S-Curve/Lineage`،
  RTL، frozen header، سلول عددی واقعی، ZIP deterministic و صفر Formula؛
- neutralization متن فرمول‌مانند، NoData header-only و نبود Forecast/EVM/SPI/CPI/Composite Health؛
- fail-closed برای budget، Actual آینده، format نادرست، version/hash ناسازگار و بدون truncate؛
- byte equality رندر تکراری، Goldenهای PDF/XLSX، visual digest دو صفحه و performance cold/warm؛
- Registry اختصاصی F04 موجود اما بدون registration در DI، Worker، endpoint یا Migration.

Goldenهای قطعی XLSX/PDF به‌ترتیب
`8a1866b7bdb3b9cb96d83a1897d80db4584c1590b3727e9ebb6a17856d672fb7` و
`bdc9c3a99c1dc5a0da57f9431d7bc7f04830fbbfbeb578b24c7234df708785ef` هستند. visual digestهای
صفحهٔ اول و دوم به‌ترتیب
`556d3b6a56d59e0c4ac8e8fcd526fca405fe9ba066ae5823b2c9c7482ea4b69b` و
`8fbb9b69d9322522e8a55f041e16c8785716dc7554660bf2eacdf9030fe5e850` پین شده‌اند.

Source `6a717f10e4bff167ad7e2643313008f5afcc8264` با tree
`995c7fae108bbb5265faa036f951036d36e7061e` و PR validation merge
`782f42ff73425cf5cad69b0635bacf05790d2ff1` دارای همان tree، در Run 167 (`35532522587`) هر هشت
Job را پاس کرد: `418/418` تست C# شامل شش case Renderer/Golden تازه و `31/31` case متمرکز F04،
`70/70` تست قراردادی Node، `139/139` تست Web، پنج browser scenario، validator روی `381` فایل
ماژولی، system audit ثابت `274/204/5`، Restore Drill کامل `45` Migration و Qualification برابر
`7/7` Suite و `12/12` Command با صفر failure.

Qualification artifact `10611741833` با digest
`sha256:d0f0c1e6b7e580823a9f07d8adcaf4bf6ada44e0712d7f843ea53ce662e9cc00`، Integration artifact
`10611777630` با digest `sha256:71a090f4c49a39a24803948a248aa0cec45c1a16316b1730ae62c9a6423b03f6`
و UI-E2E artifact `10611587190` با digest
`sha256:720ca87b8e30b3904cd327e345f086262587f7824e0c16e550eedc99f837e423` ثبت شدند.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS12-C1` فقط Renderer/Golden را می‌بندد. Catalog/API/Worker
wiring، UI و Production enablement بازند و همه Suiteهای V1/F01/F02/F03 بدون Regression سبز مانده‌اند.

## ۳۵. اتصال Catalog/API/Worker خانواده F04 — Slice 07 Micro-Step 13

Candidate باید بدون UI یا Production enablement موارد زیر را End-to-End اثبات کند:

- Migration forward شمارهٔ 46 و Definition/Template immutable با identity، digest، Landscape و هر
  سه Permission خواندنی Planning؛
- Catalog visibility فقط با تمام Source permissionها و عدم نشت F04 به Actor دارای permission ناقص؛
- parser دقیق `{}` و رد `baselineId` یا هر property اضافه؛
- Project profile pin سروری و تطبیق Tenant/Project/Time Zone/revision/configuration/cutoff؛
- re-evaluation مجوز در request، processing و download/verify؛ idempotent replay بدون bypass؛
- dispatch Worker فقط به `IProjectProgressReportingSource`، Snapshot builder و Registry نسخه‌دار F04؛
- `NotConfigured` صریح برای fixture دارای `PlanningMode=None`، بدون درصد یا Curve صفرساخته؛
- PDF/XLSX immutable با metadata، SHA-256، ETag، security header و verification معتبر؛
- Audit/Outbox/Idempotency یکتا، دو Output governed و absence واژه‌های Forecast/EVM/Composite/
  Finance/Commercial در Snapshot؛
- Database verification و Restore Drill کامل ۴۶ Migration؛
- حفظ تمام Goldenها و regressionهای F01/F02/F03/F04، defaults خاموش و audit معماری ثابت.

Source `4c48c03aad126a594e5328fc7995a72728ba2274` با tree
`49f957729fdccb0397dd153b93135ce2eaddd68a` و PR validation merge
`05ca8ac7e3fa643e111b9c8511e3e08d62be60a5` دارای همان tree، در Run 169 (`35535904655`) هر هشت
Job را پاس کرد: `419/419` تست C#، `71/71` تست قراردادی Node، `139/139` تست Web، پنج browser
scenario، هارنس متصل F04 برابر `15/15`، validator روی `382` فایل ماژولی، system audit ثابت
`274/204/5`، Restore Drill کامل `46` Migration و Qualification برابر `7/7` Suite و `12/12`
Command با صفر failure.

Qualification artifact `10613446342` با digest
`sha256:b254f06e6663f243ed2a69042d625e4467f03d0a1570bddddbee53f806eef55b`، Integration artifact
`10613605950` با digest `sha256:908054eb99778c981befe78e2c960bae7ef46ec2f1df291ca39a8a3e59d4f9d5`
و UI-E2E artifact `10613036659` با digest
`sha256:a72471664fb355edfd74a66a2afd17e17a2168826f7c515654385be3df8fbce9` ثبت شدند.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS13-C1` اتصال End-to-End F04 را می‌بندد. F05 تا F10، UI/UX2
و Production enablement بازند و همه Suiteها و Goldenهای V1/F01/F02/F03/F04 بدون Regression سبز
مانده‌اند.

## ۳۶. قرارداد معنایی وضعیت مالی خانواده F05 — Slice 07 Micro-Step 14

این Micro-Step بدون افزودن Runtime باید موارد زیر را اثبات کند:

- پارامتر Client دقیقاً `{}` و منع انتخاب Budget، ارز، Aging bucket، cutoff محلی، filter یا Source؛
- انتخاب فقط Financial Recordهای `Posted` با `postedAt <= cutoff` و transaction date محلی معتبر؛
- formulaهای قطعی Receipt/Payment/PettyCash و عدم دوباره‌شماری Funding/Expense؛
- انتخاب Obligationهای Approved و settlementهای immutable تا cutoff با ماندهٔ fail-closed؛
- تفکیک Payable/Receivable و Aging ثابت `NotDue/1–30/31–60/61+`؛
- Budget اختیاری با lifecycle مستقل و comparison nullable، بدون zero fabrication؛
- منع FX، Forecast، EVM، Management Fee و join پنهان Commercial/F06؛
- چهار Permission Finance/Budget و Classification حداقل `Confidential`؛
- آشکارسازی شکاف تاریخی سرویس‌های current-state Finance و منع DbContext/HTTP fallback؛
- Golden matrix دقیقاً بیست‌وپنج‌سناریویی و نبود هرگونه Runtime/API/Migration/Renderer F05.

Source `72fa88349d01edd4c6455eb0af1aebfdeced8c35` با tree
`d2722dd8fab797650ed0c9befb80df93fc0be135` و PR validation merge
`6f1918ed1323fa3f6f14eeeaedcad8b2cf241ff7` دارای همان tree، در Run 171 (`35538654765`) هر هشت
Job را پاس کرد: `419/419` تست C#، `73/73` تست قراردادی Node، `139/139` تست Web، پنج browser
scenario، validator روی `382` فایل ماژولی، system audit ثابت `274/204/5`، Restore Drill کامل `46`
Migration و Qualification برابر `7/7` Suite و `12/12` Command با صفر failure.

Qualification artifact `10612744490` با digest
`sha256:88a62a1ccfd2a15b7d80d1f62fac888616762acde61c90d985da2bc5843e6c3c`، Integration artifact
`10613816900` با digest `sha256:fca6c121bebb10518db49907dae3af854f1ed33be7d0becc93695d0e2d88d417`
و UI-E2E artifact `10613811669` با digest
`sha256:4adc0ac90be4059e78b3d2e2ac1860cf01979eddccd62c80d40125bff7c7f6e9` ثبت شدند.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS14-C1` فقط DoR/semantic contract F05 را می‌بندد. F05 هنوز
Runtime یا API قابل اجرا ندارد؛ F06 تا F10، UI/UX2 و Production enablement بازند و همه Suiteها و
Goldenهای V1/F01/F02/F03/F04 بدون Regression سبز مانده‌اند.

## ۳۷. Runtime Core محدود خانواده F05 — Slice 07 Micro-Step 15 Safe Checkpoint

Runtime Core باید بدون API، Migration، Renderer یا Worker wiring موارد زیر را با Unit/contract test
اثبات کند:

- identityهای Definition و parameter/snapshot/profile schema و Contract/manifest/policy Finance
  همگی صریح و نسخه‌دار باشند؛
- selector فقط Posted/Approved/settled/budget evidence واجد cutoff را انتخاب و lifecycle، tenant،
  project، currency، overlap و over-allocation نامعتبر را fail-closed رد کند؛
- calculator تمام formulaهای Cash و Budget و Aging جداگانهٔ Payable/Receivable را با rounding قطعی،
  بدون netting یا zero fabrication اجرا کند؛
- statusهای `NotConfigured/NoData/InsufficientData/Available` و reasonهای پایدار بخش‌بندی Cash،
  obligation و Budget را حفظ کنند؛
- semantic Snapshot builder فقط Source نسخه‌دار Finance و Project profile پین‌شده را مصرف کند و
  DbContext، SQL، endpoint زنده یا read service جاری را دور نزند؛
- classification حداقل `Confidential`، completeness، canonical manifest/hash، capacity budget و
  twin-run determinism fail-closed باشند؛
- compatibility source تغییر configuration پس از cutoff و Budget supersession history غیرقابل‌اثبات
  را رد کند و fallback به current state نداشته باشد؛
- Reporting endpoint، Worker، Catalog migration و Renderer هیچ reference یا dispatch جدید F05
  نداشته باشند و defaultها خاموش بمانند.

`ProjectFinancialPositionReportingTests` دقیقاً `31/31` case متمرکز را روی ۲۵ سناریوی Golden معنایی
و boundaryهای تکمیلی Runtime اجرا می‌کند. Source `77ad46cbac12b899116516b0a58665ae888b3bf2`
با tree `5c67523b0fbed8d521627fe406f74271a1bbdcfe` و PR validation merge
`5f9ad7bd2bf0cb48c5a47dbfbe29ab09afc3f92c` دارای همان tree، در Run 175 (`35541740268`) هر هشت
Job را پاس کرد: `450/450` تست C#، `75/75` تست قراردادی Node، `139/139` تست Web، پنج browser
scenario، validator روی `390` فایل ماژولی، system audit ثابت `274/204/5`، Restore Drill کامل `46`
Migration و Qualification برابر `7/7` Suite و `12/12` Command با صفر failure.

Qualification artifact `10615056923` با digest
`sha256:b0aab26340bb30c39005b128f643335b1a5ae6eb379972410cdc54b9119bc401`، Integration artifact
`10614658300` با digest `sha256:7326b21eb0d9858b8532ee687c69cce313f2656a153ff6f0c796d69dfdb4d5c8`
و UI-E2E artifact `10615141576` با digest
`sha256:842a550a2146542ce5b7924e0c09f0fd9893cccc085f96fa7b8ec004874f3bc5` ثبت شدند.

Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS15-C1` فقط Runtime Core F05 را می‌بندد. Template/Renderer و
Golden PDF/XLSX، Catalog/API/Worker wiring، F06 تا F10، UI/UX2 و Production enablement بازند و همه
Suiteها و Goldenهای V1/F01/F02/F03/F04 بدون Regression سبز مانده‌اند.
