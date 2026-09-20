# PMCS — Canonical Project Reference

- شناسه: `PMCS-CANONICAL-REF-001`
- نسخه: `1.11.0`
- آخرین کنترل: ۱۴۰۵/۰۶/۲۹ (۲۰۲۶-۰۹-۲۰)
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
| آخرین Source Candidate واجد Evidence | `7fc55c167ad2159a31c895b32a52d78f47574df9`؛ tree `d665fe4cdf29369f96ec0875bc6f1535db349d55` |
| Current evidence-bearing source checkpoint | `7fc55c167ad2159a31c895b32a52d78f47574df9`؛ Run 144 سبز |
| Source lineage | Source S07-MS05 زنجیرهٔ مستقیم دو commit از Checkpoint `1f1fc2b31840de64c70236b97d44f27fdad247d2` است؛ هیچ rebase یا baseline reset انجام نشد |
| Migration count | `44`؛ Restore Drill متصل پاس شده است |

PMCS V1.1 هنوز `Feature Complete`، `Release Candidate`، `Qualified`، `Final` یا `Baseline Locked`
نیست. Baseline قفل‌شده V1 نیز باز نشده است.

## Effective Roadmap

| وضعیت | سند مؤثر |
| --- | --- |
| Active | `docs/roadmaps/pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.31.0` |
| Active program | `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.0.0` |
| Active program | `docs/roadmaps/pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.2.0` |
| Historical/Complete | `docs/roadmaps/pmcs-v1-development-and-qualification.md` |

ترتیب مؤثر V1.1: `G0 → UX1 → EXT1 → DOC1 → IAM1/PRJ1/RPT1/COL1 → UX2 → INT1 → QA1 → V1.1 Locked`.
وضعیت فعلی: G0، UX1، EXT1، DOC1، IAM1 و PRJ1 بسته؛ RPT1 فعال؛ COL1، UX2، INT1 و QA1 باز.

## آخرین تصمیم‌های مؤثر و Supersededها

| موضوع | وضعیت قبلی | مرجع مؤثر فعلی |
| --- | --- | --- |
| وضعیت V1 | `Feature Complete` یا Qualification در جریان | Superseded؛ V1 با Run 69 `Qualified | Final | Baseline Locked` است |
| Roadmap Post-V1 | نسخه‌های تا `v1.30.0` | Superseded؛ `v1.31.0` مرجع جاری است |
| انتهای Development 05 | توقف در RPT1/MS05 | Superseded؛ GitHub/CI پیشرفت معتبر تا `S07-MS05` را اثبات می‌کند |
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
- DoR و قرارداد معنایی گزارش هفتگی/ماهانه F02: S07-MS02، Run 137؛ Runtime/Renderer هنوز باز است.
- Checkpoint اسنادی S07-MS02: commit `42e607ee98e2bbdedafaec892c8af47b9f947aa1`، Run 138 هر هشت
  Job را سبز کرد.
- Runtime Core محدود F02: source `6fc28cf54a6df820c49a2365eab76e3550ae421a`، tree
  `5188dac79fe5187b319e6aa727da89163fa37c1b` و Run 139 با هر هشت Job سبز.
- Checkpoint Runtime Core F02: commit `d8fd4398309b08d1cdc90e26140c4581dc476636`، tree
  `5345fdfc0cf1b0663b9cb1fa3bb97a4b6abf7c9b` و Run 140 با هر هشت Job سبز.
- Renderer/Golden محدود F02: source `4f68f57de2c2a79b654a19128894d9c89878ab65`، tree
  `f4b592c72ea65974c00b936ca59c0428eb47f981` و Run 141 با هر هشت Job سبز؛ PDF/XLSX deterministic،
  Golden هفتگی، Monthly boundary و حالت‌های NoData/NotConfigured پاس شدند.
- Catalog/API/Worker متصل F02: source `7fc55c167ad2159a31c895b32a52d78f47574df9`، tree
  `d665fe4cdf29369f96ec0875bc6f1535db349d55` و Run 144 با هر هشت Job سبز؛ هارنس متصل `13/13`،
  Restore کامل ۴۴ Migration و Qualification برابر `7/7` پاس شدند.
- Run 130: هر ۸ Job سبز، `329/329` تست C#، `51/51` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario، `13/13` Golden assertion و Restore کامل ۴۳ Migration.
- Evidence checkpoint معتبر: `docs/checkpoints/v1.1-rpt1-slice-07-ms05-candidate.md`.
- Anchor انتقال Run 132 (`35459192122`) روی commit `345d9d6fc2e661144a74e3001150c28d73a212c7`
  هر هشت Job را سبز کرد.

## Current In-Progress Work

`V1.1-RPT1` فعال است و Safe Resume Point قطعی فعلی آن `PMCS-V1.1-RPT1-S07-MS05-C1` است. روی این
Checkpoint، Catalog/API/Worker و qualification متصل F02 بسته شده‌اند. تصمیم
`QuestPDF Community` در ADR 0030 ثبت و package/image/font digestها، PDF Golden متصل QA-only، visual
digest و performance budget در Run 133 qualify شده‌اند. `PdfLicense=Unconfigured` و
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` در defaults و `OrphanRemediationMode=Disabled`
حفظ شده‌اند. ADR 0031 انتخاب صریح مالک محصول برای حفظ Scope ده‌گانه را ثبت کرده است:
`RPT1-F01` و `RPT1-F02` checkpoint متصل دارند و F03 تا F10 `Required / Not Implemented` هستند. قرارداد
`PMCS-RPT1-F02-SEMANTIC-001 v1.3.1` Runtime Core، Renderer/Golden و wiring checkpointed دارد:
identity/schema نسخه‌دار، Project configuration pin، period source/resolver، semantic Snapshot،
render request/model fail-closed و PDF/XLSX قطعی. Migration 44، Definition/Template seed، strict
API، Project profile pin و Worker/renderer dispatch با QA متصل در Run 144 تأیید شده‌اند. UI و feature
flag تازه‌ای وجود ندارد و production defaults خاموش‌اند.

## Remaining Work

1. DoR و قرارداد معنایی مستقل `RPT1-F03 — Executive Project State`؛ هیچ Runtime پیش از checkpoint
   قرارداد شروع نشود.
2. تکمیل `RPT1-F03` تا `RPT1-F10` با Micro-Slice و Qualification مستقل؛ Scope ده‌گانه طبق ADR 0031
   حفظ شده و فقط `daily-report-certified/1.0.0` در F01 فعلاً qualify است.
3. UI اختصاصی Reporting و visual regression در UX2؛ سپس تکمیل COL1/UX2/INT1/QA1 طبق ترتیب مصوب.
4. Pilot و gateهای وابسته به محیط واقعی فقط در زمان مقرر؛ Evidence فعلی مجوز Production rollout نیست.

## Known Gaps / Issues

| شدت | مورد | اثر/اقدام لازم |
| --- | --- | --- |
| Implementation | F01 و F02 واجد Safe Checkpoint متصل‌اند؛ F03 تا F10 هنوز پیاده نشده‌اند | هر خانواده با DoR، Micro-Slice و Qualification مستقل تکمیل شود |
| Documentation | متن PR #2 هنوز Roadmap `v1.14.0`، head قدیمی و gateهای MS03–MS05 را باز نشان می‌دهد | PR body با این مرجع و Roadmap فعال همگام شود؛ کد/CI متأثر نیست |
| Traceability | دو ADR با شماره `0027` وجود دارد | بدون renumber شتاب‌زده، یک تصمیم نسخه‌دار برای شناسه یکتا ثبت شود |
| Ownership | اسناد، UI Reporting را هم «gate باز RPT1» و هم کار UX2 می‌خوانند | مالک gate بسته‌شدن RPT1/UX2 باید در Roadmap صریح شود |
| Workspace metadata | branch محلی upstream ندارد و `pmcs.upstreamcommit` روی SHA قدیمی است | مبنای sync باید Remote head/tree بالا باشد، نه config محلی قدیمی |
| Legal operations | Community انتخاب شده، اما eligibility دائمی از code استنباط نمی‌شود | پیش از Production و حداقل سالانه توسط مالک تجاری/حقوقی بازاعتبارسنجی شود |
| Intentional gate | PDF license در defaults پیکربندی نشده و feature flagها خاموش‌اند | نقص Runtime نیست؛ ADR 0030 فقط QA qualification را مجاز کرده است |

## Current GitHub / CI State

- Repository: `ehsangholamibbq-cell/pmcs-workshop-management`؛ PR #2 از `v1.1-development` به `main`.
- PR: `open`، `draft` و ادغام‌نشده است.
- Checkpoint head پیش از F02 Candidate: `a5d80de29f945c504ffaa9562b4e0993a2a44aea`؛ Run 136
  (`35467329335`) هر هشت Job را سبز کرد.
- F02 semantic contract Candidate: `b4a59fa966320a1da4b53759814224e21893c01e`؛ tree
  `6b5b486dace3c07b0b4e0385413bf1add5aee7a3`؛ Run 137 (`35474388839`) هر هشت Job موفق،
  `330/330` تست C#، `56/56` تست قراردادی Node، `139/139` تست Web، پنج browser scenario و Restore
  کامل ۴۳ Migration.
- F02 semantic Checkpoint: `42e607ee98e2bbdedafaec892c8af47b9f947aa1`؛ tree
  `aa2aa2af814f64b0806a15e59c4def0a151eaf5e`؛ Run 138 (`35474992254`) هر هشت Job موفق.
- F02 Runtime Core Source: `6fc28cf54a6df820c49a2365eab76e3550ae421a`؛ tree
  `5188dac79fe5187b319e6aa727da89163fa37c1b`؛ Run 139 (`35477179493`) هر هشت Job موفق،
  `346/346` تست C#، `58/58` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۳
  Migration.
- F02 Runtime Core Checkpoint: `d8fd4398309b08d1cdc90e26140c4581dc476636`؛ tree
  `5345fdfc0cf1b0663b9cb1fa3bb97a4b6abf7c9b`؛ Run 140 (`35477677819`) هر هشت Job موفق.
- F02 Renderer/Golden Source: `4f68f57de2c2a79b654a19128894d9c89878ab65`؛ tree
  `f4b592c72ea65974c00b936ca59c0428eb47f981`؛ Run 141 (`35495791821`) هر هشت Job موفق،
  `353/353` تست C#، `60/60` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۳
  Migration.
- F02 Catalog/API/Worker Source: `7fc55c167ad2159a31c895b32a52d78f47574df9`؛ tree
  `d665fe4cdf29369f96ec0875bc6f1535db349d55`؛ PR validation merge
  `2a91fc442a3a84a2ee3d6c59fe5186c8f0ed3efb` با همان tree؛ Run 144 (`35498990050`) هر هشت Job
  موفق، `355/355` تست C#، `61/61` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F02
  برابر `13/13` و Restore ۴۴ Migration.
- Catalog Decision Candidate: `d81ecc00762145210e1c688f8f5843f46d62fc04`؛ tree
  `5f40383ad506d94520c741eb69fcd00086283734`؛ Run 135 (`35466775368`) هر هشت Job موفق،
  `330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web و پنج browser scenario.
- Source Candidate MS06: `b8f21492a4f44c7c412e5b7eda0b164e7f256758`؛ tree
  `e94b6ba3753e67b42ea0ec99e998761fdad0bcc3`.
- آخرین CI بررسی‌شده: Run 144 (`35498990050`) — هر ۸ Job
  `architecture/backend/integration/pilot-contract/web/ui-e2e/identity-container/qualification-report` موفق.
- Qualification artifact Run 144 برابر `10601457941` با digest
  `sha256:3b577627970885596d83e992d04bce9093ad69e9b594e58f2919294a676206e8` است؛ Integration artifact
  `10601880116` با digest `sha256:3b582c64f4301cf1215157263d66270bf300d1009b763679e7e86e5fe5623fd7`
  و UI-E2E artifact `10601678972` با digest
  `sha256:5d35548a4675c0515d54597702eb911cb9413c66eb5ba09989603874c4803ea7` ثبت شدند.
- Run 133: build بدون warning، `330/330` تست C#، `52/52` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario و PDF Golden متصل `8/8` پاس شدند؛ PDF برابر `42489` byte و
  SHA-256 `cc188c842ddcced9a24acd5e18f92c4a6511252c40104055c8c38627cdfac863` بود.
- visual digest در ۹۶ DPI برابر
  `95d6e71de15d9d130041d5c295c94239b9fe9095e6572e581aa9a655ee85c9b2` و بازبینی مستقل Poppler
  بدون defect بود. Qualification artifact `10590681541` با digest
  `sha256:00f630ce572f49041af75d5687f0e79dafccb3a8359bc2ddb741c9e7b3c8d9e7` ثبت شد.

## Exact Next Micro-Step

**گام بعدی فقط DoR و قرارداد معنایی `RPT1-F03 — Executive Project State` در Micro-Slice مستقل است.**
Runtime پیش از checkpoint قرارداد شروع نشود؛ UI و Production enablement همچنان جدا و خاموش بمانند.

## Resume Rule

در ادامه‌های بعدی ابتدا همین سند، GitHub branch head/tree و آخرین Checkpoint خوانده شوند. فقط اگر
یکی از آن‌ها تغییر کرده بود، اختلاف هدفمند بررسی شود؛ مرور دوباره همه Conversationها لازم نیست.
