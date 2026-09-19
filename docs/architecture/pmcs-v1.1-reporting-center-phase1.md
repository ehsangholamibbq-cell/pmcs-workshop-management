# PMCS V1.1 — معماری Reporting Center Phase 1

- شناسه: `PMCS-ARCH-RPT1-001`
- نسخه: `1.11.0`
- وضعیت: `S07-MS03 Runtime Core Candidate | F02 API/Renderer not implemented | F03-F10/UI open`
- Checkpoint: `V1.1-RPT1`
- Parent commit: `720de8869e251f5a4c39a6940a76e9929232706b`
- آخرین Qualification Candidate: `b4a59fa966320a1da4b53759814224e21893c01e`
- Source tree: `6b5b486dace3c07b0b4e0385413bf1add5aee7a3`
- Connected evidence: Run 137 (`35474388839`) — `success`
- مرجع تصمیم: ADR 0029، ADR 0030 و ADR 0031

## ۱. Scope

RPT1 یک Bounded Context مستقل برای گزارش‌های استاندارد و Certified می‌سازد:

- Catalog و Template Versioning؛
- اجرای غیرهم‌زمان و قابل Retry؛
- Semantic Snapshot permission-aware با `asOf`؛
- PDF و XLSX؛ CSV فقط برای Dataset جدولی مجاز؛
- Output immutable با SHA-256، Retention، Archive و Audit؛
- RTL، تاریخ شمسی، ارقام فارسی و Time Zone پروژه؛
- Header/Footer، Revision، شماره صفحه، Watermark و Verification code؛
- `NoData/NotConfigured/InsufficientData` بدون صفر ساختگی؛
- نخستین Vertical Slice: گزارش روزانه رسمی و زنجیره اصلاحات.

ADR 0031 تصریح می‌کند که «نخستین Vertical Slice» به‌معنی محدودشدن Scope نیست. هر ده خانوادهٔ
کاتالوگ اولیه داخل RPT1 باقی می‌مانند؛ گزارش روزانه `RPT1-F01` است و F02 تا F10 باید با قرارداد
معنایی، Renderer/Golden و Checkpoint مستقل تکمیل شوند. Foundation مشترک یا Catalog placeholder
جایگزین Qualification خانواده‌ای نیست.

قرارداد `PMCS-RPT1-F02-SEMANTIC-001 v1.1.0` در
`pmcs-v1.1-rpt1-f02-weekly-monthly-semantic-contract.md` مرز گزارش هفتگی/ماهانه را به roll-up
نسخه‌های رسمی Daily Report محدود می‌کند و period/cutoff، source lineage، status،
permission/classification و Golden matrix آن را تثبیت می‌کند. Runtime Core محدود آن اکنون identity
و schemaهای نسخه‌دار، period-read contract، resolver و semantic Snapshot builder را بدون API،
Catalog seed، Worker dispatch یا Renderer پیاده می‌کند.

## ۲. Non-Scope

- Drag-and-drop Report Designer؛
- Query یا SQL دلخواه کاربر؛
- Scheduled delivery و Subscription؛
- Word output؛
- Dashboard builder؛
- تولید KPI/عدد توسط LLM؛
- لینک عمومی ناشناس؛
- Offline generation؛ Client فقط می‌تواند Draft پارامتر را محلی نگه دارد.

## ۳. مرز ماژول

`Pmcs.Modules.Reporting` دارای Descriptor غیر Legacy با شناسه `reporting.center` است.

Dependencyهای مجاز:

- `Pmcs.BuildingBlocks` برای Clock، Actor، Audit/Outbox/Idempotency و Manifest؛
- `Pmcs.Modules.Projects` فقط از `IProjectDirectory`؛
- `Pmcs.Modules.FieldOperations` فقط از `IDailyReportReportingSource`؛
- `Pmcs.Modules.Documents` فقط از Contract انتشار/خواندن Generated Document؛
- `Pmcs.Modules.IdentityAccess` به‌صورت مستقیم لازم نیست؛ Permission از BuildingBlocks contract تزریق می‌شود.

هیچ Import از namespace `Persistence` ماژول دیگر و هیچ SQL روی Schema دیگر مجاز نیست.

## ۴. Permission و Classification

| عملیات | Permission | Scope | Risk |
| --- | --- | --- | --- |
| Catalog/List/Get | `reporting.catalog.read` | Project/Tenant | Low |
| Create Run/Retry | `reporting.run.create` | Project/Tenant | Medium |
| Run/Output metadata | `reporting.catalog.read` | Project/Tenant | Low |
| Download | `reporting.output.download` + Source permissions | Project/Tenant | Medium |
| Publish Template | `reporting.template.publish` | Tenant | High |

گزارش روزانه علاوه بر Permissionهای Reporting به `field.daily-reports.read` نیاز دارد. Template
permission requirement را نسخه‌دار اعلام می‌کند. Output بیشترین Classification میان Definition،
پارامتر و Source را می‌گیرد؛ downgrade خودکار ممنوع است.

Permission snapshot شامل Policy version، actor، project، required operations، result، source و
evaluation time است. Snapshot مجوز دائمی ایجاد نمی‌کند و در processing/download دوباره ارزیابی می‌شود.

## ۵. Aggregateها و Persistence

### `ReportDefinition`

- `id`, `code`, `title`, `description`؛
- `scope`, `classification`, `supportedFormats`؛
- `requiredPermissions`؛
- `parameterSchemaVersion`؛
- `status = Active|Retired`؛
- `currentTemplateVersionId`.

RPT1 Definitionها allowlist و migration-seeded هستند؛ endpoint ساخت Definition عمومی ندارد.

### `ReportTemplateVersion`

- `id`, `definitionId`, semantic `version`؛
- `rendererContractVersion`, `layoutContractVersion`؛
- `contentDigest`, `publishedAt`, `retiredAt`؛
- `pageSize`, `orientation`, `locale`, `calendar`؛
- immutable پس از Publish.

### `ReportRun`

- Tenant/Project/Definition/Template identity؛
- canonical parameters و `parametersHash`؛
- `asOfUtc`, project time zone؛
- requester، Correlation ID و Idempotency identity؛
- request/processing permission snapshots؛
- status، attempts، claimed/started/completed timestamps؛
- safe diagnostic code/detail، next retry؛
- snapshot id و output count؛
- optimistic revision.

### `ReportSnapshot`

- immutable canonical semantic JSON؛
- `schemaVersion`, `dataStatus`, `sourceManifest`؛
- SHA-256 lowercase؛
- builtAt و source cut-off؛
- هیچ Secret، Token یا object key ندارد.

### `ReportOutput`

- run/snapshot/template identity؛
- format، media type، file name؛
- Generated Document ID؛
- size و SHA-256؛
- verification code و manifest hash؛
- classification، retention و archive state؛
- immutable پس از success.

## ۶. جدول‌ها و Migration

Schema مالک: `reporting`.

| جدول | هدف |
| --- | --- |
| `report_definitions` | Catalog allowlist |
| `report_template_versions` | Template immutable |
| `report_runs` | lifecycle و diagnostics |
| `report_snapshots` | semantic snapshot canonical |
| `report_outputs` | output manifest و Document reference |

Migration اولیه با order بعد از ۱۱۰۰ Documents و شمارهٔ ۴۲ ثبت شده است. Migration forward شمارهٔ
۴۳ قید unique قدیمی `verification_code` را به index غیر unique تبدیل می‌کند، زیرا دو Run با
Manifest معنایی یکسان می‌توانند کد راستی‌آزمایی یکسان ولی Output identity مستقل داشته باشند.
هر دو Migration فقط Expand/forward-compatible هستند و هیچ جدول V1/V1.1 موجود را تغییر معنا
نمی‌دهند. Restore drill بعدی باید inventory کامل ۴۳ Migration را نگه دارد. QA reset inventory
Schema جدید را دقیقاً شامل می‌شود.

## ۷. Application Contract منبع گزارش روزانه

`IDailyReportReportingSource.LoadChainAsync(tenantId, projectId, reportId, asOfUtc)` فقط DTOهای
read-only زیر را برمی‌گرداند:

- Root report و نسخه‌های رسمی `Approved/Superseded` که تا `asOf` رسمی شده‌اند؛
- report date، narrative، location و actor/time metadata؛
- `rootReportId/versionNumber/supersedes/supersededBy/supersededAt`؛
- aggregate revision و Factهای نسخه؛
- Fact lineage شامل `copiedFromFactId`، Location ID/name، quantity/unit و impact؛
- current official version در `asOf`.

قواعد as-of:

1. نسخه‌ای که بعد از cutoff تأیید شده، دیده نمی‌شود؛
2. نسخه‌ای در cutoff رسمی است که `approvedAt <= asOf` و
   `supersededAt is null || supersededAt > asOf`؛
3. Draft/Submitted/Returned هرگز وارد Snapshot رسمی نمی‌شوند؛
4. نبود نسخهٔ رسمی `NoData` است، نه گزارش خالی یا صفر؛
5. Reporting هیچ وضعیت Source را تغییر نمی‌دهد.

## ۸. Pipeline اجرا

1. API Actor/Tenant/Project و Definition/format/parameter را validate می‌کند؛
2. Permissionهای Reporting و Source بررسی و Snapshot درخواست ثبت می‌شوند؛
3. Idempotent Run با status `Queued` و Audit/Outbox در transaction ایجاد می‌شود؛
4. Worker Run را با `FOR UPDATE SKIP LOCKED` claim می‌کند؛
5. Permission و Project status دوباره بررسی می‌شوند؛
6. Source contract Snapshot معنایی را می‌سازد؛
7. canonical serializer hash را تولید می‌کند؛
8. تمام Rendererهای درخواست‌شده ابتدا bytes را می‌سازند و اعتبارسنجی می‌کنند تا خطای قطعی یک
   format پیش از انتشار format دیگر رخ دهد؛
9. هر output با stable identity از Generated Document contract عبور می‌کند؛ انتشار Document،
   Audit و Outbox مربوط به Documents در transaction مالک Documents ثبت می‌شود؛
10. Outputها، Run success، Audit و `reporting.report.completed.v1` در transaction مالک Reporting
    ثبت می‌شوند؛ Snapshot پیش‌تر با Audit مستقل و immutable ثبت شده است؛
11. دانلود فقط با current Permission و hash verification انجام می‌شود.

Retry پس از publish ناقص از stable output identity استفاده می‌کند و duplicate Document یا Output
نمی‌سازد. Partial failure تا کامل‌شدن همه formatهای درخواست‌شده `Succeeded` اعلام نمی‌شود.

## ۹. خطا و State

| Code | معنا | Retry |
| --- | --- | --- |
| `NoData` / `InsufficientData` | Data status صریح؛ Output بدون عدد ساختگی تولید می‌شود | خطا نیست |
| `reporting.permission.revoked` | Permission پس از Queue لغو شده | خیر |
| `reporting.template.retired` | Template pin‌شده دیگر قابل اجرا نیست | خیر |
| `reporting.renderer.transient` | خطای موقت Renderer/Storage | محدود |
| `reporting.renderer.license_unconfigured` | license پیش‌فرض عمداً فعال نشده است | فقط Retry صریح پس از پیکربندی مصوب |
| `reporting.renderer.license_unapproved` | tier با ADR 0030 و تصمیم `Community` منطبق نیست | فقط پس از تصمیم/پیکربندی مصوب |
| `reporting.renderer.font_missing` | فونت فارسی Certified در runtime موجود نیست | فقط Retry صریح پس از اصلاح image |
| `reporting.renderer.font_integrity_failed` | SHA-256 فونت با قرارداد Certified اختلاف دارد | فقط پس از اصلاح image/font |
| `reporting.renderer.configuration_unpinned` | digest image یا فونت با قرارداد Certified اختلاف دارد | فقط پس از اصلاح configuration |
| `reporting.output.integrity_failed` | hash/size/media mismatch | Fail-closed |
| `reporting.run.timeout` | پردازش از budget عبور کرده | محدود |

Diagnostics فقط code، attempt، duration، component و Correlation ID دارد. Source payload، Narrative،
نام فایل حساس، Token یا stack trace خام در log/API ذخیره نمی‌شود.

## ۱۰. Output contract

### PDF

- PDF واقعی، نه screenshot؛
- فونت فارسی embed و RTL/Bidi صحیح؛
- Template نخست A4/Portrait است؛ A3/Landscape فقط با Template version مستقل و Golden جدید؛
- header جدول تکرارشونده، page break کنترل‌شده و شماره صفحه؛
- Header/Footer، لوگو، عنوان، پروژه، تاریخ شمسی، Revision و cutoff؛
- Watermark برای Draft/Archive فقط طبق state؛
- Verification code/QR بدون public bypass؛
- metadata ثابت و deterministic؛
- Adapter فعلی `QuestPDF 2026.8.0` است و ADR 0030، tier برابر `Community` را برای Qualification
  تصویب کرده است. `PdfLicense` در تنظیمات پیش‌فرض همچنان `Unconfigured` می‌ماند و PDF در این
  وضعیت با code امن fail می‌شود؛ فقط QA ایزوله آن را صریحاً `Community` می‌کند.
- build/runtime image و دو فایل DejaVu Sans با digest دقیق pin شده‌اند؛ Renderer پیش از ثبت فونت
  hash را کنترل می‌کند. هر تغییر package/image/font/layout نیازمند Golden جدید است.
- Golden بصری در ۹۶ DPI، دو رندر byte-identical، کنترل متن/ساختار و budgetهای cold/warm به‌ترتیب
  ۵۰۰۰/۲۵۰۰ ms جزو Gate این Adapter هستند.

### XLSX

- workbook استاندارد و بدون Macro؛
- sheet metadata + sheet داده؛
- Freeze pane، filter و column type ثابت؛
- تاریخ قابل مشاهده شمسی به‌صورت متن صریح و UTC/ISO در sheet metadata؛
- عدد canonical و unit/currency در ستون مستقل؛
- formula از Source یا کاربر پذیرفته نمی‌شود؛ مقدارها server-calculated هستند؛
- cellهایی که با `=`, `+`, `-`, `@` شروع می‌شوند به‌عنوان text امن نوشته می‌شوند.

## ۱۱. Offline و Concurrency

- generation/download online-only است؛
- Draft پارامتر در Client Fact رسمی نیست؛
- `Idempotency-Key` برای Create/Retry اجباری است؛
- Template و parameter version در Run pin می‌شوند؛
- دو Run همسان می‌توانند برای Audit جدا باشند، اما retry همان Run duplicate نمی‌سازد؛
- Worker claim، attempt و finalization optimistic/transactional است؛
- cancellation فقط پیش از immutable output مجاز است.

## ۱۲. Retention و Archive

- Retention پیش‌فرض گزارش رسمی `LongTerm` است؛
- Template/Definition می‌تواند Policy طولانی‌تر تعیین کند، کوتاه‌ترشدن نیازمند Permission و Audit است؛
- Legal Hold از Shared Documents پیروی می‌کند؛
- حذف فیزیکی در RPT1 API وجود ندارد؛
- Retire/Archive metadata خروجی و Snapshot را بازنویسی نمی‌کند؛
- Source حذف یا اصلاح نمی‌تواند Output تاریخی را بی‌صدا تغییر دهد.

### ۱۲.۱ Generated Document orphan remediation

- remediation فقط برای `ReportOutput` آزادشده و قدیمی‌تر از grace صریح انجام می‌شود؛
- نبود owner در `reporting.report_outputs`، lineage یکتای release Audit، Run نهایی
  `Failed/Failed` و `output_count=0` پیش‌شرط‌اند؛ حالت مبهم یا recoverable حذف نمی‌شود؛
- retry و remediation روی همان Run از PostgreSQL transaction advisory lock مشترک استفاده می‌کنند؛
- `InventoryOnly` هیچ metadata یا object را تغییر نمی‌دهد و `ApplyEligible` فقط با retention
  شناخته‌شده و منقضی و بدون legal hold مجاز است؛ مقدار پیش‌فرض `Disabled` است؛
- Documents زیر `FOR UPDATE` هویت، revision، state، retention و legal hold را دوباره می‌سنجد،
  سپس domain deletion، حذف object و حذف metadata را انجام می‌دهد؛
- حذف metadata و Audit `GeneratedReportOrphanRemediated` در transaction مالک Documents اتمیک‌اند؛
  Audit شامل owner/run/retention/revision است و object key را ثبت نمی‌کند؛
- pagination، batch و سقف هر sweep bounded هستند و اجرای دوباره Audit یا حذف تکراری نمی‌سازد؛
- این worker endpoint حذف عمومی، bypass retention یا مجوز پاک‌سازی orphanهای مبهم ایجاد نمی‌کند.

### ۱۲.۲ Semantic cutoff و XLSX Golden

- projection هر نسخه با cutoff محاسبه می‌شود؛ supersession آینده نباید state، link، timestamp،
  correction reason، revision یا `LastModifiedAt` تاریخی را تغییر دهد؛
- Golden سه نسخه دارد: v1 و v2 رسمی و v3 Draft؛ فقط نسخه‌های رسمی مؤثر در cutoff وارد Snapshot و
  workbook می‌شوند؛
- twinهای یک cutoff باید Snapshot و Source Manifest hash یکسان داشته باشند و correction رسمی باید
  هر دو hash را تغییر دهد؛
- replay همان Output باید bytes/SHA-256 یکسان برگرداند؛ تفاوت identity خروجی twinها فقط در metadata
  خروجی‌ویژه مجاز است و semantic digest را تغییر نمی‌دهد؛
- parser مستقل باید ZIP/OpenXML allowlist، ترتیب sheet، RTL، freeze pane، filter، metadata و تمام
  ۱۸ ستون را بررسی کند و formula، macro و external link را رد کند؛
- SQL مستقل chain، revisionهای تاریخی، lineage Fact، چهار Run/Output و حذف Draft را تأیید می‌کند.

## ۱۳. Observability و SLO اولیه

- queue depth، oldest queued age، processing duration و success/failure/retry count؛
- render duration/size به تفکیک Definition/format بدون label حساس؛
- download integrity failure و permission denial؛
- health state برای Worker و storage adapter؛
- Correlation از API تا Run، Snapshot، Document، Audit و Outbox؛
- exporter OTLP فقط در composition و فقط با endpoint صریح؛ پیش‌فرض خاموش؛
- Collector/Prometheus/Alertmanager مسئول محیط استقرار، با ruleهای queue age، heartbeat و
  failure/retry و بدون identityهای Tenant/Project/User/Run؛
- budget اولیه: P95 ساخت گزارش روزانه دوفرمتی زیر ۳۰ ثانیه در dataset مرجع؛
- maximum صفحات/ردیف/حجم با fail-closed policy و تست load تعیین می‌شود.

## ۱۴. Rollout و Rollback

1. Migration Expand و Catalog seed؛
2. Feature flagهای `ReportingCenter:Phase1Enabled`، `OutputAccessEnabled` و Worker پیش‌فرض خاموش؛
3. Smoke روی dataset ایزوله و Golden files؛
4. فعال‌سازی محدود برای Admin/Project Manager؛
5. اندازه‌گیری queue/render/storage؛
6. فعال‌سازی role mapping مصوب؛
7. rollback با خاموش‌کردن Phase 1 و Worker انجام می‌شود؛ در صورت نبود Incident امنیتی، فقط
   `OutputAccessEnabled=true` برای Download/Verify خروجی‌های موجود روشن می‌ماند؛ داده حذف نمی‌شود؛
8. Contract/column فقط پس از compatibility window در نسخه آینده قابل جمع‌کردن است.

## ۱۵. Acceptance قابل‌اندازه‌گیری

- هیچ Cross-module Persistence import یا SQL وجود ندارد؛
- تمام reads با current Tenant/Project/Permission محدود می‌شوند؛
- Run create/retry idempotent و audited است؛
- Permission revocation میان Queue و processing و پیش از download اثبات می‌شود؛
- Daily Report as-of و chain reconstruction با Golden dataset deterministic است؛
- یک Template/version/parameter/source، Snapshot hash یکسان می‌دهد؛
- PDF/XLSX hash و bytes در replay یکسان‌اند یا metadata nondeterministic پیش از hash حذف می‌شود؛
- Jalali/RTL/Persian digits و Time Zone در Golden/visual tests پاس می‌شوند؛
- NoData/InsufficientData بدون عدد ساختگی است؛
- download عمومی Documents برای `ReportOutput` fail-closed است؛
- Migration/restore، worker crash/retry و object storage partial failure تست می‌شوند؛
- Full Regression V1 و Checkpointهای بسته‌شده V1.1 پاس می‌ماند.

## ۱۶. وضعیت پیاده‌سازی Source Candidate

در commit `ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173` موارد زیر در Source وجود دارند:

- Rendererهای PDF/XLSX، فرمت شمسی/RTL، stable identity و manifest/verification؛
- انتشار Generated Document با read-after-write، signature/size/SHA check و Retention `LongTerm`؛
- Worker مسیر `SnapshotReady → Rendering → Succeeded`، retry محدود و recovery با stable identity؛
- Retry/Cancel، Download/Verify، integrity audit و output-access rollback switch؛
- Migration 43، TestHarness متصل و assertionهای PostgreSQL/Documents/Audit/Outbox/Idempotency.

Run 99 همان tree را روی merge commit موقت
`ebbe1090f42cf6bd58928d8844a4b0f86e6abcf6` آزمود: Build و `295/295` تست C#، هارنس
PostgreSQL/Object Storage با `13/13` assertion گزارش، Restore Drill کامل ۴۳ Migration، Regression
هفت Suite و Qualification report همگی پاس شدند. این Evidence، PDF deterministic/golden با license
واقعی، crash/concurrency/load/soak، مجموعهٔ کامل Permission revocation/tamper، metrics/heartbeat و
UI تولیدی Reporting را پوشش نمی‌دهد؛ این Gateها برای بستن RPT1 باز هستند.

Qualification Slice 03 در Run 102 بدون تغییر معماری Runtime، Cancel با Worker خاموش، replay و
final-state guards، tenant/auth isolation، generic Documents isolation، revocation پس از success و
metadata tamper fail-closed/restore را متصل اثبات کرد. این Evidence با worker-time revocation، دو
Worker/stale lease/crash window، object-byte tamper و observability/Golden/UI برابر نیست.

Qualification Slice 04 در Run 104 نیز بدون تغییر API/Domain/Migration، دو Process واقعی Worker،
`FOR UPDATE SKIP LOCKED`، rollback claim پس از `SIGKILL`، lease کهنه و crash windowهای پیش و پس از
Storage را متصل اثبات کرد. crash-after-storage همان Generated Document پایدار را reuse کرد و برای
هر Run فقط یک Output/Document باقی ماند. pauseهای deterministic فقط پشت QA Gateway ایزوله فعال‌اند.
worker-time revocation، object-byte/missing-object tamper، orphan inventory، load/observability،
Golden و UI هنوز Gate باز RPT1 هستند.

Qualification Slice 05 در Run 108 پنجرهٔ worker-time revocation را با recheck مجوزهای Reporting و
Source بلافاصله پیش از انتشار Storage بست و snapshot ردشده را روی Run ثبت کرد. در محیط متصل، تعلیق
Membership میان Rendering و publish با `reporting.permission.revoked` و بدون Document/Output شکست
خورد. byte-tamper، missing و malformed object نیز برای Verify و Download fail-closed و سپس با
بازگردانی byteهای اصلی recover شدند. inventory یک orphan موقت crash-after-storage و صفرشدن آن پس
از reuse سند پایدار را اثبات کرد؛ sweeper/remediation تولیدی، load/observability، Golden/PDF و UI
هنوز Gate باز RPT1 هستند.

Slice 06 Micro-Step 01 در Run 110 Core بودجه‌های attempt/retry/row/page/byte/time، timeout،
terminalization وضعیت exhausted، fairness پروژه‌محور، meterهای کم‌کاردینالیتی و health مربوط به
heartbeat/queue age را در Full CI تأیید کرد. failure injection فقط پشت QA Gateway قرار دارد. این
Evidence جای orchestration اختصاصی load/poison/fairness در `S06-MS02` را نمی‌گیرد.

Slice 06 Micro-Step 02 در Run 113 orchestration متصل ۲۰ Run سالم + یک poison را با P95 برابر
`5.529s`، retry محدود سه-attemptی و ownership یکتای Output/Document/Audit/Outbox پاس کرد. سناریوی
fairness نیز با دو پروژه، دو Worker، دو row lock مستقل و rollback پس از `SIGKILL` هر `9/9`
assertion را پاس کرد. این Evidence load/poison/fairness را می‌بندد، اما exporter/scrape و alert
تولیدی، remediation امن orphan، Golden، PDF قانونی و UI اختصاصی Reporting همچنان Gate باز هستند.

Slice 06 Micro-Step 03 Checkpoint C1 در Run 117 قرارداد نه instrument پایدار Meter و چهار tag
کم‌کاردینالیتی را Unit-qualify کرد. readiness فقط برای check دقیق `reporting-worker` چهار مقدار
عددی allowlist‌شده منتشر می‌کند. سناریوی fairness متصل، صف aged را زیر دو row lock نگه داشت و
`Degraded` ناشی از queue age، payload محدود و عدم نشت Tenant/Project/User/Run ID را در assertion
دهم اثبات کرد. این C1 خود MS03 را نمی‌بندد؛ exporter، scrape و alert rule/delivery در C2 بازند.

Slice 06 Micro-Step 03 Checkpoint C2 در Run 120 exporter اختیاری OTLP را فقط در composition API
ثبت کرد و ماژول Reporting را از SDK مستقل نگه داشت. Collector نسخه‌پین‌شده metricها را با translation
صریح برای Prometheus منتشر کرد؛ سه rule queue-age/heartbeat/failure-retry load شدند و alert واقعی
queue-age از Prometheus به Alertmanager و webhook ایزوله تحویل شد. qualification هر `5/5` assertion
target/metric/privacy/firing/delivery را پاس کرد و MS03 بسته شد. remediation امن orphan، Golden،
PDF قانونی و UI اختصاصی Reporting همچنان Gate باز RPT1 هستند.

Slice 06 Micro-Step 04 در Run 123، worker داخلی orphan remediation را با سه mode
`Disabled|InventoryOnly|ApplyEligible` و مرز باریک Documents متصل qualify کرد. dry-run چهار Candidate
را بدون تغییر inventory کرد؛ apply فقط orphan منقضی و بدون hold را حذف کرد و Candidateهای دارای
retention، legal hold یا owner را حفظ کرد. حذف object واقعی MinIO، Audit یکتا و عاری از object key،
و idempotency sweep دوم هر `7/7` assertion را پاس کردند. هیچ API تجاری یا Migration اضافه نشد و
Restore Drill همان ۴۳ Migration را نگه داشت. MS04 بسته است؛ Golden معنایی/XLSX، PDF قانونی و UI
اختصاصی Reporting همچنان Gate باز RPT1 هستند.

Slice 06 Micro-Step 05 در Run 130، زنجیرهٔ سه‌نسخه‌ای v1/v2/v3 را با چهار Run twin پیش/پس از
correction qualify کرد. Golden متصل `13/13` assertion را برای hashهای cutoff، replay byte-identical،
OpenXML امن و deterministic، ۸/۱۶ ردیف semantic، هر هشت نوع Fact، measurement و correction lineage
پاس کرد. SQL مستقل revisionهای `11/12/5` و حذف v3 Draft را تأیید کرد و Unit test نشت metadata
supersession آینده را بست. هیچ API تجاری یا Migration اضافه نشد و Restore Drill همان ۴۳ Migration
را نگه داشت. MS05 بسته است؛ تصمیم قانونی و Golden/Performance PDF و UI اختصاصی Reporting هنوز
Gate باز RPT1 هستند.

Slice 06 Micro-Step 06 در Run 133 تصمیم `QuestPDF Community` را با ADR 0030 ثبت و Adapter را روی
QuestPDF `2026.8.0`، imageهای build/runtime با digest کامل و دو فونت vendored DejaVu Sans pin کرد.
دو رندر مستقل byte-identical، visual digest ثابت در ۹۶ DPI، budgetهای cold/warm و PDF Golden متصل
روی fixture MS05 هر `8/8` assertion را پاس کردند. PDF متصل یک صفحه و `42489` byte بود؛ parse مستقل
متن/ساختار، replay/download integrity و حذف Draft پاس شدند. defaultهای Production خاموش و license
پیش‌فرض `Unconfigured` ماندند. MS06 بسته است؛ اختلاف کاتالوگ ده‌گانه و UI اختصاصی UX2 Gateهای باز
باقی‌مانده‌اند و RPT1 Active است.

ADR 0031 اختلاف کاتالوگ را از نظر تصمیم Scope بست: هر ده خانواده حفظ شدند و کاهش یا انتقال ضمنی
نه خانواده رد شد. این تصمیم به‌معنی تکمیل Runtime نیست؛ F02 تا F10 همچنان Required/Not Implemented
هستند، RPT1 Active می‌ماند و Micro-Slice بعدی DoR/semantic contract گزارش هفتگی و ماهانه F02 است.
Candidate تصمیم در commit `d81ecc00762145210e1c688f8f5843f46d62fc04` و tree
`5f40383ad506d94520c741eb69fcd00086283734` با هر هشت Job سبز Run 135 (`35466775368`) qualify شد؛
هیچ API، Migration یا Runtime contract تغییر نکرد.

Slice 07 Micro-Step 02 قرارداد معنایی F02 را مستند و در Run 137 qualify کرده است. Weekly از شنبه تا
شنبهٔ بعد و Monthly از روز اول تا روز اول ماه شمسی بعد در Time Zone pin‌شده پروژه تعریف می‌شود؛
فقط current official Daily Report هر تاریخ در `asOfUtc` وارد می‌شود و
`NotConfigured/NoData/InsufficientData/Available` precedence نسخه‌دار دارد. Candidate
`b4a59fa966320a1da4b53759814224e21893c01e` با tree
`6b5b486dace3c07b0b4e0385413bf1add5aee7a3` هر هشت Job Run 137 (`35474388839`) را پاس کرد. F02
در این Checkpoint `Contract Ready / Runtime Not Implemented` بود.

Slice 07 Micro-Step 03 Candidate، Definition identity داخلی `project-periodic-certified/1.0.0`،
parameter/snapshot schema، Project configuration pin، Contract خواندنی
`pmcs.field-operations.daily-report-period/v1`، resolver شنبه/ماه شمسی و Snapshot builder قطعی را
اضافه می‌کند. Builder فقط از Application Contract استفاده می‌کند، Draft و metadata correction آینده
را وارد نمی‌کند، coverage سه cadence و چهار data status را می‌سازد، unitها را ordinal و جدا نگه
می‌دارد و Classification بالاتر Source را propagate می‌کند. `346/346` تست C# محلی و contract test
محدود سبز است؛ CI Candidate هنوز Gate انتشار این Micro-Step است. هیچ API، Migration، Catalog/
Template seed، Worker dispatch، PDF/XLSX، UI یا Production flag تغییر نکرده و F02 هنوز End-to-End
قابل اجرا/دانلود نیست.
