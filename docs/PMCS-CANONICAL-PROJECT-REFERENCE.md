# PMCS — Canonical Project Reference

- شناسه: `PMCS-CANONICAL-REF-001`
- نسخه: `1.4.0`
- آخرین کنترل: ۱۴۰۵/۰۶/۲۸ (۲۰۲۶-۰۹-۱۹)
- وضعیت: `Authoritative working reference | V1 locked | V1.1 Development / RPT1 Active`
- هدف: مرجع واحد Resume و کنترل انطباق؛ این سند جای Roadmap/ADR/Checkpoint را نمی‌گیرد، بلکه آخرین
  وضعیت معتبر آن‌ها را یکجا مشخص می‌کند.

## قاعده اعتبار و Supersession

ترتیب مرجع در صورت اختلاف چنین است: وضعیت واقعی Git tree و Runtime ← Evidence سبز CI ← آخرین سند
نسخه‌دار Governance/Roadmap/Checkpoint ← ادعاهای Conversation. هر ادعای قدیمی که با Evidence یا
تصمیم نسخه‌دار جدیدتر ناسازگار باشد `Superseded` است. «Done» فقط با کد/سند قابل ردیابی و Evidence
معتبر پذیرفته می‌شود.

## Current Baseline

| مورد | مرجع معتبر |
| --- | --- |
| Baseline قفل‌شده محصول | `PMCS V1 — Qualified | Final | Baseline Locked` |
| V1 source baseline | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` |
| خط فعال | `PMCS V1.1 — Development` روی `v1.1-development` |
| V1.1 repository start | `0389b52cbd3385bdcc9f0e2a94411800389ae2fc` |
| Stage فعال | `V1.1-RPT1 — Reporting Center Phase 1` |
| آخرین Source Candidate واجد Evidence | `d81ecc00762145210e1c688f8f5843f46d62fc04`؛ tree `5f40383ad506d94520c741eb69fcd00086283734` |
| Remote source head پیش از انتشار Checkpoint | `d81ecc00762145210e1c688f8f5843f46d62fc04`؛ parent مستقیم Checkpoint MS06 |
| Source lineage | S07-MS01 Candidate فرزند مستقیم `640fc7e4e8c75907f24883168be7317fae1f5548` است؛ هیچ rebase یا baseline reset انجام نشد |
| Migration count | `43`؛ Restore Drill متصل پاس شده است |

PMCS V1.1 هنوز `Feature Complete`، `Release Candidate`، `Qualified`، `Final` یا `Baseline Locked`
نیست. Baseline قفل‌شده V1 نیز باز نشده است.

## Effective Roadmap

| وضعیت | سند مؤثر |
| --- | --- |
| Active | `docs/roadmaps/pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.24.0` |
| Active program | `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.0.0` |
| Active program | `docs/roadmaps/pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.2.0` |
| Historical/Complete | `docs/roadmaps/pmcs-v1-development-and-qualification.md` |

ترتیب مؤثر V1.1: `G0 → UX1 → EXT1 → DOC1 → IAM1/PRJ1/RPT1/COL1 → UX2 → INT1 → QA1 → V1.1 Locked`.
وضعیت فعلی: G0، UX1، EXT1، DOC1، IAM1 و PRJ1 بسته؛ RPT1 فعال؛ COL1، UX2، INT1 و QA1 باز.

## آخرین تصمیم‌های مؤثر و Supersededها

| موضوع | وضعیت قبلی | مرجع مؤثر فعلی |
| --- | --- | --- |
| وضعیت V1 | `Feature Complete` یا Qualification در جریان | Superseded؛ V1 با Run 69 `Qualified | Final | Baseline Locked` است |
| Roadmap Post-V1 | نسخه‌های تا `v1.23.0` | Superseded؛ `v1.24.0` مرجع است |
| انتهای Development 05 | توقف در RPT1/MS05 | Superseded؛ GitHub/CI پیشرفت معتبر تا `S07-MS01` را اثبات می‌کند |
| Agent مدیریتی | عنوان کلی یا پنج فاز | Superseded؛ دقیقاً هفت Stage مستقل با Gateهای مستقل |
| Reporting | Report Designer آزاد در V1.1 | Superseded/خارج از Scope؛ V1.1 فقط گزارش‌های استاندارد و تأییدشده، Designer در V1.2 |
| UI | بسته‌شدن UX1 یعنی پایان بازطراحی | Superseded؛ UX1 فقط جهت بصری «مدیریت ممتاز» را بست؛ مهاجرت کامل در UX2 است |
| PDF license | تصمیم ثبت‌نشده و `Unconfigured` | ADR 0030؛ `QuestPDF Community` برای Qualification، با default همچنان `Unconfigured` و بازاعتبارسنجی eligibility پیش از Production |
| کاتالوگ RPT1 | ابهام میان تکمیل ۹ خانواده یا کاهش Scope | ADR 0031؛ Scope هر ۱۰ خانواده حفظ شد، F02 تا F10 با Micro-Slice مستقل الزامی‌اند و RPT1 پیش از تکمیل آن‌ها بسته نمی‌شود |

تصمیم‌های پابرجا: Modular Monolith؛ داده رسمی فقط از state و Fact تأییدشده؛ Audit/Outbox/Idempotency؛
Permission و Tenant boundary؛ Offline و conflict semantics؛ Jalali/RTL در مرز UI؛ وضعیت‌های Operational،
Financial و Commercial مستقل؛ نبود داده هرگز صفر/سبز تلقی نشود؛ Agent فقط از Tool Registry مجاز و
Application Service استفاده کند و SQL/DB مستقیم نداشته باشد؛ Chat منبع حقیقت رسمی نیست؛ Login/Profile
و Project Bootstrap نسخه‌دار و محدود؛ Reporting یک Bounded Context مستقل؛ تمام flagهای RPT1 پیش‌فرض
خاموش و دسترسی به خروجی fail-closed باقی بماند.

## Completed & Verified Work

- V1: تمام Sliceهای توسعه و QA، Full Regression Run 69 و قفل Baseline تکمیل شده‌اند.
- V1.1: G0، UX1، EXT1، DOC1 (Run 83)، IAM1 (Run 88) و PRJ1 (Run 92) بسته‌اند.
- RPT1 Core/Generated Documents و PostgreSQL/MinIO: Run 99.
- Cancel/Security، دو Worker/Crash Recovery و Worker Revocation/Object Integrity: Runهای 102، 104 و 108.
- Capacity و connected load/poison/fairness: MS01/MS02، Runهای 110 و 113.
- Metrics/queue-age و OTLP/scrape/alert delivery: MS03-C1/C2، Runهای 117 و 120.
- Inventory/dry-run/remediation امن orphan با retention/legal hold/audit/idempotency: MS04، Run 123.
- Semantic cutoff و XLSX Golden قطعی برای `daily-report-certified/1.0.0`: MS05، Run 130.
- تصمیم Community، pin image/font و PDF Golden/visual/performance: MS06، Run 133.
- تصمیم صریح حفظ Scope ده‌گانه و الزام Micro-Slice مستقل F02 تا F10: S07-MS01، ADR 0031، Run 135.
- Run 130: هر ۸ Job سبز، `329/329` تست C#، `51/51` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario، `13/13` Golden assertion و Restore کامل ۴۳ Migration.
- Evidence checkpoint معتبر: `docs/checkpoints/v1.1-rpt1-slice-07-ms01-candidate.md`.
- Anchor انتقال Run 132 (`35459192122`) روی commit `345d9d6fc2e661144a74e3001150c28d73a212c7`
  هر هشت Job را سبز کرد.

## Current In-Progress Work

`V1.1-RPT1` فعال است و تا سبزشدن Candidate فعلی، Safe Resume Point قطعی آن
`PMCS-V1.1-RPT1-S07-MS01-C1` است. تصمیم
`QuestPDF Community` در ADR 0030 ثبت و package/image/font digestها، PDF Golden متصل QA-only، visual
digest و performance budget در Run 133 qualify شده‌اند. `PdfLicense=Unconfigured` و
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` در defaults و `OrphanRemediationMode=Disabled`
حفظ شده‌اند. ADR 0031 انتخاب صریح مالک محصول برای حفظ Scope ده‌گانه را ثبت کرده است: فقط
`RPT1-F01` qualify شده و F02 تا F10 Required/Not Implemented هستند. Run 135 هر هشت Job را روی
Decision Candidate سبز کرد؛ هیچ Runtime، Migration یا feature flag تغییر نکرد. قرارداد
`PMCS-RPT1-F02-SEMANTIC-001 v1.0.0` اکنون DoR، period/cutoff، source lineage، coverage/status،
permission/classification و Golden matrix F02 را به‌صورت Candidate تثبیت می‌کند؛ Runtime و Renderer
هنوز پیاده نشده‌اند و Checkpoint این Micro-Step به CI سبز وابسته است.

## Remaining Work

1. Qualification و Checkpoint قرارداد F02؛ سپس Runtime identity، period source contract/resolver و
   semantic Snapshot builder در Micro-Step محدود مستقل. F02 تا Renderer/Golden نهایی Done نیست.
2. تکمیل `RPT1-F03` تا `RPT1-F10` با Micro-Slice و Qualification مستقل؛ Scope ده‌گانه طبق ADR 0031
   حفظ شده و فقط `daily-report-certified/1.0.0` در F01 فعلاً qualify است.
3. UI اختصاصی Reporting و visual regression در UX2؛ سپس تکمیل COL1/UX2/INT1/QA1 طبق ترتیب مصوب.
4. Pilot و gateهای وابسته به محیط واقعی فقط در زمان مقرر؛ Evidence فعلی مجوز Production rollout نیست.

## Known Gaps / Issues

| شدت | مورد | اثر/اقدام لازم |
| --- | --- | --- |
| Implementation | کاتالوگ ده‌گانه طبق ADR 0031 حفظ شده، اما فقط F01 موجود است | F02 تا F10 باید جداگانه پیاده و qualify شوند؛ هیچ‌کدام Done محسوب نمی‌شوند |
| Documentation | متن PR #2 هنوز Roadmap `v1.14.0`، head قدیمی و gateهای MS03–MS05 را باز نشان می‌دهد | PR body با این مرجع و Roadmap فعال همگام شود؛ کد/CI متأثر نیست |
| Traceability | دو ADR با شماره `0027` وجود دارد | بدون renumber شتاب‌زده، یک تصمیم نسخه‌دار برای شناسه یکتا ثبت شود |
| Ownership | اسناد، UI Reporting را هم «gate باز RPT1» و هم کار UX2 می‌خوانند | مالک gate بسته‌شدن RPT1/UX2 باید در Roadmap صریح شود |
| Workspace metadata | branch محلی upstream ندارد و `pmcs.upstreamcommit` روی SHA قدیمی است | مبنای sync باید Remote head/tree بالا باشد، نه config محلی قدیمی |
| Legal operations | Community انتخاب شده، اما eligibility دائمی از code استنباط نمی‌شود | پیش از Production و حداقل سالانه توسط مالک تجاری/حقوقی بازاعتبارسنجی شود |
| Intentional gate | PDF license در defaults پیکربندی نشده و feature flagها خاموش‌اند | نقص Runtime نیست؛ ADR 0030 فقط QA qualification را مجاز کرده است |

## Current GitHub / CI State

- Repository: `ehsangholamibbq-cell/pmcs-workshop-management`؛ PR #2 از `v1.1-development` به `main`.
- PR: `open`، `draft` و ادغام‌نشده است.
- Checkpoint head پیش از Decision Candidate: `640fc7e4e8c75907f24883168be7317fae1f5548`؛ Run 134
  (`35464073235`) هر هشت Job را سبز کرد.
- Catalog Decision Candidate: `d81ecc00762145210e1c688f8f5843f46d62fc04`؛ tree
  `5f40383ad506d94520c741eb69fcd00086283734`؛ Run 135 (`35466775368`) هر هشت Job موفق،
  `330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web و پنج browser scenario.
- F02 semantic contract Candidate: قرارداد `PMCS-RPT1-F02-SEMANTIC-001 v1.0.0`؛ commit/tree و CI
  پس از انتشار همین Micro-Step ثبت می‌شوند؛ هیچ Runtime/Migration/Renderer/default تغییر نکرده است.
- Source Candidate MS06: `b8f21492a4f44c7c412e5b7eda0b164e7f256758`؛ tree
  `e94b6ba3753e67b42ea0ec99e998761fdad0bcc3`.
- آخرین Source CI بررسی‌شده: Run 133 (`35463350892`) — هر ۸ Job
  `architecture/backend/integration/pilot-contract/web/ui-e2e/identity-container/qualification-report` موفق.
- Run 133: build بدون warning، `330/330` تست C#، `52/52` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario و PDF Golden متصل `8/8` پاس شدند؛ PDF برابر `42489` byte و
  SHA-256 `cc188c842ddcced9a24acd5e18f92c4a6511252c40104055c8c38627cdfac863` بود.
- visual digest در ۹۶ DPI برابر
  `95d6e71de15d9d130041d5c295c94239b9fe9095e6572e581aa9a655ee85c9b2` و بازبینی مستقل Poppler
  بدون defect بود. Qualification artifact `10590681541` با digest
  `sha256:00f630ce572f49041af75d5687f0e79dafccb3a8359bc2ddb741c9e7b3c8d9e7` ثبت شد.

## Exact Next Micro-Step

**ابتدا همین Candidate قرارداد F02 را با Full CI qualify و Checkpoint کن.** پس از آن، Micro-Step
بعدی فقط Runtime identity نسخه‌دار، period-read contract در FieldOperations، resolver مرز
هفتگی/ماهانه و semantic Snapshot builder با Unit/contract test است. Renderer، Catalog Production،
UI و Golden binary به Sliceهای بعد موکول شوند؛ F03 تا F10 Done اعلام نشوند و هیچ feature flag
Production روشن نشود.

## Resume Rule

در ادامه‌های بعدی ابتدا همین سند، GitHub branch head/tree و آخرین Checkpoint خوانده شوند. فقط اگر
یکی از آن‌ها تغییر کرده بود، اختلاف هدفمند بررسی شود؛ مرور دوباره همه Conversationها لازم نیست.
