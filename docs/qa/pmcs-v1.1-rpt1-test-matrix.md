# PMCS V1.1 — RPT1 Test Matrix و Qualification Contract

- شناسه: `PMCS-QA-RPT1-001`
- نسخه: `1.2.0`
- وضعیت: Connected core + security/recovery + capacity/poison/fairness passed؛ RPT1 extended qualification open
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
