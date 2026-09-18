# PMCS V1.1 — RPT1 Test Matrix و Qualification Contract

- شناسه: `PMCS-QA-RPT1-001`
- نسخه: `1.0.0`
- وضعیت: Definition of Ready
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

- Migration 42 و Catalog seed؛
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
