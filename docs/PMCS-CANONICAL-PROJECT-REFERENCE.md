# PMCS — Canonical Project Reference

- شناسه: `PMCS-CANONICAL-REF-001`
- نسخه: `1.25.0`
- آخرین کنترل: ۱۴۰۵/۰۶/۲۹ (۲۰۲۶-۰۹-۲۰)
- وضعیت: `Authoritative working reference | V1 locked | V1.1 RPT1 / F05 Semantic Contract Safe Checkpoint`
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
| آخرین Source Candidate واجد Evidence | `72fa88349d01edd4c6455eb0af1aebfdeced8c35`؛ tree `d2722dd8fab797650ed0c9befb80df93fc0be135` |
| Current evidence-bearing source checkpoint | `72fa88349d01edd4c6455eb0af1aebfdeced8c35`؛ tree `d2722dd8fab797650ed0c9befb80df93fc0be135`؛ Run 171 سبز |
| Source lineage | S07-MS14 فرزند Checkpoint `4e50fcadeb11bf412eff9275b8fac8561fd7d097` است؛ هیچ reset یا بازطراحی baseline انجام نشد |
| Current safe checkpoint | `PMCS-V1.1-RPT1-S07-MS14-C1`؛ F01 تا F04 checkpoint متصل و F05 Contract Ready است؛ Runtime F05 و F06 تا F10 بازند |
| Migration count | Safe Resume: `46` و Restore Drill سبز |

PMCS V1.1 هنوز `Feature Complete`، `Release Candidate`، `Qualified`، `Final` یا `Baseline Locked`
نیست. Baseline قفل‌شده V1 نیز باز نشده است.

## Effective Roadmap

| وضعیت | سند مؤثر |
| --- | --- |
| Active | `docs/roadmaps/pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.47.0` |
| Active program | `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.0.0` |
| Active program | `docs/roadmaps/pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.2.0` |
| Historical/Complete | `docs/roadmaps/pmcs-v1-development-and-qualification.md` |

ترتیب مؤثر V1.1: `G0 → UX1 → EXT1 → DOC1 → IAM1/PRJ1/RPT1/COL1 → UX2 → INT1 → QA1 → V1.1 Locked`.
وضعیت فعلی: G0، UX1، EXT1، DOC1، IAM1 و PRJ1 بسته؛ RPT1 فعال؛ COL1، UX2، INT1 و QA1 باز.

## آخرین تصمیم‌های مؤثر و Supersededها

| موضوع | وضعیت قبلی | مرجع مؤثر فعلی |
| --- | --- | --- |
| وضعیت V1 | `Feature Complete` یا Qualification در جریان | Superseded؛ V1 با Run 69 `Qualified | Final | Baseline Locked` است |
| Roadmap Post-V1 | نسخه‌های تا `v1.46.0` | Superseded؛ `v1.47.0` مرجع جاری است |
| انتهای Development 05 | توقف در RPT1/MS05 | Superseded؛ GitHub/CI پیشرفت معتبر تا `S07-MS14` را اثبات می‌کند |
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
- Checkpoint اسنادی F02: commit `a7b7e885c1e59de894100ade96444184692ab5d3`، tree
  `5a476abb23ad650cb7334f537583ee4fa7b3c628` و Run 145 با هر هشت Job، `355/355` تست C#،
  `62/62` تست Node، `139/139` تست Web و Restore ۴۴ Migration سبز.
- DoR و قرارداد معنایی Executive Project State F03: source
  `e3218555a38f7ba460558e51b4db3f8bc17fcd9c`، tree
  `1fe4cc804fdd078a71ff2633201c8c690447900e` و Run 146 با هر هشت Job، `355/355` تست C#،
  `63/63` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration سبز؛ Runtime
  هنوز پیاده نشده است.
- Runtime Core محدود Executive Project State F03: source
  `22d5b0f91edf8d733192fae0ba946c8538c63bca`، tree
  `eb5ea4253a7b80e8ab3320b9ccea747624f89790` و Run 148 با هر هشت Job، `377/377` تست C#،
  `64/64` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration سبز؛ Renderer و
  wiring همچنان بازند.
- Renderer/Golden قطعی Executive Project State F03: source
  `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f`، tree
  `58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434` و Run 154 با هر هشت Job، `383/383` تست C#،
  `65/65` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration سبز؛
  Catalog/API/Worker wiring همچنان باز است.
- Catalog/API/Worker متصل Executive Project State F03: source
  `40afeb37d7bf90e97a988cae141901e28d336516`، tree
  `ae06285bf1a68fe2592dacc76c7d31cb291ab924` و Run 156 با هر هشت Job، `387/387` تست C#،
  `66/66` تست Node، `139/139` تست Web، پنج browser scenario، هارنس `14/14`، Restore ۴۵ Migration
  و Qualification `7/7` سبز؛ UI/Production همچنان خاموش است.
- Checkpoint اسنادی F03: commit `c59a2444f5d5dd70859441f27d05c23dea6c268e`، tree
  `8a7926ac11abb529a3887d1e8a2091f7aeedecbd` و Run 157 با هر هشت Job، `387/387` تست C#،
  `67/67` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۵ Migration و
  Qualification `7/7` سبز.
- DoR و قرارداد معنایی پیشرفت فیزیکی F04: source
  `f8829027c2ce073c207cd0e04a49c306b546c6a1`، tree
  `2b784f135894092ef55bf7c7df201b1f03e0c77f` و Run 158 با هر هشت Job، `387/387` تست C#،
  `68/68` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۵ Migration سبز؛ Runtime
  هنوز پیاده نشده است.
- Runtime Core محدود پیشرفت فیزیکی F04: source
  `deb1571ec66d820868e8f4b77b631471e3c8207c`، tree
  `9e41495a357480af03f1555ef640962ab863d332` و Run 163 با هر هشت Job، `412/412` تست C# شامل
  `25/25` case متمرکز F04، `69/69` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۵
  Migration و Qualification `7/7` سبز؛ Renderer/Golden و wiring بازند.
- Renderer/Golden قطعی پیشرفت فیزیکی F04: source
  `6a717f10e4bff167ad7e2643313008f5afcc8264`، tree
  `995c7fae108bbb5265faa036f951036d36e7061e` و Run 167 با هر هشت Job، `418/418` تست C# شامل
  `31/31` case متمرکز F04، `70/70` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۵
  Migration و Qualification `7/7` سبز؛ Catalog/API/Worker wiring باز است.
- Catalog/API/Worker متصل پیشرفت فیزیکی F04: source
  `4c48c03aad126a594e5328fc7995a72728ba2274`، tree
  `49f957729fdccb0397dd153b93135ce2eaddd68a` و Run 169 با هر هشت Job، `419/419` تست C#،
  `71/71` تست Node، `139/139` تست Web، پنج browser scenario، هارنس `15/15`، Restore ۴۶ Migration
  و Qualification `7/7` سبز؛ UI/Production همچنان خاموش است.
- DoR و قرارداد معنایی وضعیت مالی F05: source
  `72fa88349d01edd4c6455eb0af1aebfdeced8c35`، tree
  `d2722dd8fab797650ed0c9befb80df93fc0be135` و Run 171 با هر هشت Job، `419/419` تست C#،
  `73/73` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۶ Migration و Qualification
  `7/7` سبز؛ Runtime هنوز پیاده نشده است.
- Run 130: هر ۸ Job سبز، `329/329` تست C#، `51/51` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario، `13/13` Golden assertion و Restore کامل ۴۳ Migration.
- Evidence checkpoint معتبر: `docs/checkpoints/v1.1-rpt1-slice-07-ms14-candidate.md`.
- Anchor انتقال Run 132 (`35459192122`) روی commit `345d9d6fc2e661144a74e3001150c28d73a212c7`
  هر هشت Job را سبز کرد.

## Current In-Progress Work

`V1.1-RPT1` فعال است و Safe Resume Point قطعی فعلی آن `PMCS-V1.1-RPT1-S07-MS14-C1` است. روی این
Checkpoint، Catalog/API/Worker و qualification متصل F01 تا F04 و قرارداد معنایی F05 بسته شده‌اند.
Migration 46،
Definition/Template seed، strict empty-object API، Project profile pin، مجوز منبع definition-aware و
Worker/Renderer dispatch برای F03 در Run 156 qualify شده‌اند. تصمیم
`QuestPDF Community` در ADR 0030 ثبت و package/image/font digestها، PDF Golden متصل QA-only، visual
digest و performance budget در Run 133 qualify شده‌اند. `PdfLicense=Unconfigured` و
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` در defaults و `OrphanRemediationMode=Disabled`
حفظ شده‌اند. ADR 0031 انتخاب صریح مالک محصول برای حفظ Scope ده‌گانه را ثبت کرده است:
`RPT1-F01` تا `RPT1-F04` checkpoint متصل دارند؛ F05 `Contract Ready / Runtime Not Implemented` و
F06 تا F10 `Required / Not Implemented` هستند. قرارداد
`PMCS-RPT1-F02-SEMANTIC-001 v1.3.1` Runtime Core، Renderer/Golden و wiring checkpointed دارد:
identity/schema نسخه‌دار، Project configuration pin، period source/resolver، semantic Snapshot،
render request/model fail-closed و PDF/XLSX قطعی. Migration 44، Definition/Template seed، strict
API، Project profile pin و Worker/renderer dispatch با QA متصل در Run 144 تأیید شده‌اند. UI و feature
flag تازه‌ای وجود ندارد و production defaults خاموش‌اند. قرارداد
نسخه پایه `PMCS-RPT1-F03-SEMANTIC-001 v1.0.0` پارامتر Client خالی، انتخاب Snapshot رسمی تا cutoff،
currency و partial-state صریح، منع Composite Health و Golden matrix هفده‌سناریویی را تثبیت کرد و
Run 146 هر هشت Job را سبز کرد. نسخه `PMCS-RPT1-F03-SEMANTIC-001 v1.1.1` در Checkpoint `S07-MS07`
identity/schema نسخه‌دار، Project profile pin، Source contract و selector cutoff-aware در
ProjectIntelligence و semantic Snapshot builder را با Unit/contract test بست و Run 148 هر هشت Job را
سبز کرد. نسخه `PMCS-RPT1-F03-SEMANTIC-001 v1.2.1` در Checkpoint `S07-MS08`، Template/Renderer/Layout
identity، render model canonical و PDF/XLSX قطعی را بست و Run 154 هر هشت Job را سبز کرد. نسخهٔ
`PMCS-RPT1-F03-SEMANTIC-001 v1.3.1` این Runtime و Rendererها را از مسیر Catalog/API/Worker متصل و
در Run 156 checkpoint کرد؛ UI، feature flag تازه و Production enablement ندارد.
قرارداد پایه checkpointed `PMCS-RPT1-F04-SEMANTIC-001 v1.0.0`، Baseline رسمی،
Actual/Planned/Variance و S-Curve را تثبیت می‌کند. Client فقط `{}` می‌فرستد؛ lifecycle
Baseline/evidence تا cutoff، target snapshot، grid حداکثر ۳۶۶ نقطه‌ای، سه Permission خواندنی
Planning و Classification fail-closed قطعی‌اند و Run 158 هر هشت Job را سبز کرده است. endpoint زنده
Planning و lifecycle/target جاری برای تاریخچه کافی نیستند. نسخه checkpointed
`PMCS-RPT1-F04-SEMANTIC-001 v1.1.1` اکنون identity/schema، Contractهای نسخه‌دار FieldOperations و
Planning، selector lifecycle، calculator و semantic Snapshot builder را با Unit/contract test اضافه
کرده است. compatibility projection هر history غیرقابل‌اثبات را fail-closed می‌کند. Run 163 هر هشت
Job را سبز و Runtime Core را checkpoint کرده است؛ هیچ Migration، API، Catalog/Template seed، Worker
dispatch یا Renderer برای F04 در آن Checkpoint وجود نداشت. نسخه
`PMCS-RPT1-F04-SEMANTIC-001 v1.2.1` در Checkpoint `S07-MS12`، Template/Renderer/Layout identity،
render model canonical و PDF/XLSX قطعی را بست. نسخه `PMCS-RPT1-F04-SEMANTIC-001 v1.3.1` در
Checkpoint `S07-MS13`، Migration 46، Catalog/Template، strict API، Project profile pin، سه
Permission خواندنی Planning، Worker/Renderer dispatch و qualification متصل را روی همان قراردادها
بست و Run 169 هر هشت Job را سبز کرد. UI، feature flagها، license و Production enablement باز و
defaultها خاموش‌اند.
قرارداد checkpointed `PMCS-RPT1-F05-SEMANTIC-001 v1.0.0`، Cash Position را فقط از رکوردهای Posted
تا cutoff، تعهدات Approved و settlementهای immutable می‌سازد؛ Payable/Receivable و Aging چهار-bucketی
جدا هستند و Budget Baseline اختیاری lifecycle مستقل دارد. Client فقط `{}` می‌فرستد؛ چهار Permission
Finance/Budget و Classification حداقل `Confidential` قطعی‌اند و Run 171 هر هشت Job را سبز کرده است.
سرویس‌های current-state Finance، endpointهای زنده و DbContextها Source تاریخی Certified نیستند؛
Runtime بعدی باید Application Contract/Projection cutoff-aware و fail-closed بسازد. هیچ Runtime،
Migration، API، Renderer، Catalog/Template seed یا Worker dispatch برای F05 وجود ندارد.

## Remaining Work

1. Runtime Core محدود F05 شامل identity/schema نسخه‌دار، Application Contract/Projection خواندنی و
   cutoff-aware در Finance، selector، calculator و semantic Snapshot builder؛ Renderer و wiring وارد نشوند.
2. سپس Renderer/Golden و Catalog/API/Worker F05 و تکمیل `RPT1-F06` تا `RPT1-F10` با Micro-Slice و Qualification مستقل؛
   Scope ده‌گانه طبق ADR 0031 حفظ شده است.
3. UI اختصاصی Reporting و visual regression در UX2؛ سپس تکمیل COL1/UX2/INT1/QA1 طبق ترتیب مصوب.
4. Pilot و gateهای وابسته به محیط واقعی فقط در زمان مقرر؛ Evidence فعلی مجوز Production rollout نیست.

## Known Gaps / Issues

| شدت | مورد | اثر/اقدام لازم |
| --- | --- | --- |
| Implementation | F01 تا F04 واجد Safe Checkpoint متصل‌اند؛ F05 فقط Contract Ready و F06 تا F10 پیاده‌نشده‌اند | Runtime Core محدود F05، سپس Renderer/wiring مستقل همان خانواده |
| Temporal source | endpoint جاری Planning زمان‌های Approval/Supersede، target و configuration تاریخی کافی ندارد | Contract/selector نسخه‌دار و compatibility producer متصل‌اند؛ history غیرقابل‌اثبات fail-closed است و توسعهٔ تاریخچهٔ کامل باید Slice دامنه‌ای مستقل باشد |
| Finance temporal source | read modelها و serviceهای جاری Finance cutoff تاریخی، postedAt/approvedAt مستقل، Budget supersession history و Aging دوطرفهٔ قابل‌اثبات ندارند | Application Contract/Projection نسخه‌دار F05 باید در Runtime Slice بعدی این مرز را fail-closed ببندد؛ fallback به current state ممنوع است |
| Documentation | متن PR #2 هنوز Roadmap `v1.14.0`، head قدیمی و gateهای MS03–MS05 را باز نشان می‌دهد | PR body با این مرجع و Roadmap فعال همگام شود؛ کد/CI متأثر نیست |
| Traceability | دو ADR با شماره `0027` وجود دارد | بدون renumber شتاب‌زده، یک تصمیم نسخه‌دار برای شناسه یکتا ثبت شود |
| Ownership | اسناد، UI Reporting را هم «gate باز RPT1» و هم کار UX2 می‌خوانند | مالک gate بسته‌شدن RPT1/UX2 باید در Roadmap صریح شود |
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
- F02 Connected Checkpoint: `a7b7e885c1e59de894100ade96444184692ab5d3`؛ tree
  `5a476abb23ad650cb7334f537583ee4fa7b3c628`؛ Run 145 (`35499768898`) هر هشت Job موفق،
  `355/355` تست C#، `62/62` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 semantic contract Candidate: `e3218555a38f7ba460558e51b4db3f8bc17fcd9c`؛ tree
  `1fe4cc804fdd078a71ff2633201c8c690447900e`؛ PR validation merge
  `a6987cd47bb1be917d34a94fb064aa72ec6b89c2` با همان tree؛ Run 146 (`35500809115`) هر هشت Job
  موفق، `355/355` تست C#، `63/63` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 Runtime Core Source: `22d5b0f91edf8d733192fae0ba946c8538c63bca`؛ tree
  `eb5ea4253a7b80e8ab3320b9ccea747624f89790`؛ PR validation merge
  `b89ca9920fc216644203d9cd5b3868cabed453fe` با همان tree؛ Run 148 (`35507127968`) هر هشت Job
  موفق، `377/377` تست C#، `64/64` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 Renderer/Golden Source: `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f`؛ tree
  `58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434`؛ PR validation merge
  `235e0a0f12b560bba06792c3290724d739117b73` با همان tree؛ Run 154 (`35512969648`) هر هشت Job
  موفق، `383/383` تست C#، `65/65` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴
  Migration.
- F03 Connected Source MS09: `40afeb37d7bf90e97a988cae141901e28d336516`؛ tree
  `ae06285bf1a68fe2592dacc76c7d31cb291ab924`؛ PR validation merge
  `4944731391c2b649cd401fc9095619cb19d41f72` با همان tree؛ Run 156 (`35515989200`) هر هشت Job
  موفق، `387/387` تست C#، `66/66` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F03
  برابر `14/14` و Restore ۴۵ Migration.
- F03 Connected Checkpoint: `c59a2444f5d5dd70859441f27d05c23dea6c268e`؛ tree
  `8a7926ac11abb529a3887d1e8a2091f7aeedecbd`؛ PR validation merge
  `060f4807d640aff0d0014cddac27ea9aca7ad5ef` با همان tree؛ Run 157 (`35516916383`) هر هشت Job
  موفق، `387/387` تست C#، `67/67` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F03
  برابر `14/14`، validator روی `367` فایل و Restore ۴۵ Migration.
- F04 semantic contract Source: `f8829027c2ce073c207cd0e04a49c306b546c6a1`؛ tree
  `2b784f135894092ef55bf7c7df201b1f03e0c77f`؛ PR validation merge
  `80830a48ffd5b84ecdc97990b052f6d7037eda42` با همان tree؛ Run 158 (`35522512734`) هر هشت Job
  موفق، `387/387` تست C#، `68/68` تست Node، `139/139` تست Web، پنج browser scenario، validator
  روی `367` فایل و Restore ۴۵ Migration؛ Runtime F04 هنوز پیاده نشده است.
- F04 Runtime Core Source: `deb1571ec66d820868e8f4b77b631471e3c8207c`؛ tree
  `9e41495a357480af03f1555ef640962ab863d332`؛ PR validation merge
  `f0d3a5550d9bd1c10d8ddd5a3c0ada24eb0fead5` با همان tree؛ Run 163 (`35527577826`) هر هشت Job
  موفق، `412/412` تست C# شامل `25/25` case متمرکز F04، `69/69` تست Node، `139/139` تست Web، پنج
  browser scenario، validator روی `378` فایل، audit `274/204/5` و Restore ۴۵ Migration؛ Renderer و
  wiring F04 باز است.
- F04 Renderer/Golden Source: `6a717f10e4bff167ad7e2643313008f5afcc8264`؛ tree
  `995c7fae108bbb5265faa036f951036d36e7061e`؛ PR validation merge
  `782f42ff73425cf5cad69b0635bacf05790d2ff1` با همان tree؛ Run 167 (`35532522587`) هر هشت Job
  موفق، `418/418` تست C# شامل شش case Renderer/Golden تازه و `31/31` case متمرکز F04، `70/70`
  تست Node، `139/139` تست Web، پنج browser scenario، validator روی `381` فایل، audit `274/204/5`
  و Restore ۴۵ Migration؛ Catalog/API/Worker wiring F04 باز است.
- F04 Connected Source MS13: `4c48c03aad126a594e5328fc7995a72728ba2274`؛ tree
  `49f957729fdccb0397dd153b93135ce2eaddd68a`؛ PR validation merge
  `05ca8ac7e3fa643e111b9c8511e3e08d62be60a5` با همان tree؛ Run 169 (`35535904655`) هر هشت Job
  موفق، `419/419` تست C#، `71/71` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F04
  برابر `15/15`، validator روی `382` فایل، audit `274/204/5` و Restore ۴۶ Migration؛ UI/Production
  خاموش است.
- F05 semantic contract Source: `72fa88349d01edd4c6455eb0af1aebfdeced8c35`؛ tree
  `d2722dd8fab797650ed0c9befb80df93fc0be135`؛ PR validation merge
  `6f1918ed1323fa3f6f14eeeaedcad8b2cf241ff7` با همان tree؛ Run 171 (`35538654765`) هر هشت Job
  موفق، `419/419` تست C#، `73/73` تست Node، `139/139` تست Web، پنج browser scenario، validator
  روی `382` فایل، audit `274/204/5` و Restore ۴۶ Migration؛ Runtime F05 هنوز پیاده نشده است.
- Catalog Decision Candidate: `d81ecc00762145210e1c688f8f5843f46d62fc04`؛ tree
  `5f40383ad506d94520c741eb69fcd00086283734`؛ Run 135 (`35466775368`) هر هشت Job موفق،
  `330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web و پنج browser scenario.
- Source Candidate MS06: `b8f21492a4f44c7c412e5b7eda0b164e7f256758`؛ tree
  `e94b6ba3753e67b42ea0ec99e998761fdad0bcc3`.
- آخرین CI بررسی‌شده: Run 171 (`35538654765`) — هر ۸ Job
  `architecture/backend/integration/pilot-contract/web/ui-e2e/identity-container/qualification-report` موفق.
- Qualification artifact Run 171 برابر `10612744490` با digest
  `sha256:88a62a1ccfd2a15b7d80d1f62fac888616762acde61c90d985da2bc5843e6c3c` است؛ Integration artifact
  `10613816900` با digest `sha256:fca6c121bebb10518db49907dae3af854f1ed33be7d0becc93695d0e2d88d417`
  و UI-E2E artifact `10613811669` با digest
  `sha256:4adc0ac90be4059e78b3d2e2ac1860cf01979eddccd62c80d40125bff7c7f6e9` ثبت شدند.
- Run 133: build بدون warning، `330/330` تست C#، `52/52` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario و PDF Golden متصل `8/8` پاس شدند؛ PDF برابر `42489` byte و
  SHA-256 `cc188c842ddcced9a24acd5e18f92c4a6511252c40104055c8c38627cdfac863` بود.
- visual digest در ۹۶ DPI برابر
  `95d6e71de15d9d130041d5c295c94239b9fe9095e6572e581aa9a655ee85c9b2` و بازبینی مستقل Poppler
  بدون defect بود. Qualification artifact `10590681541` با digest
  `sha256:00f630ce572f49041af75d5687f0e79dafccb3a8359bc2ddb741c9e7b3c8d9e7` ثبت شد.

## Exact Next Micro-Step

**گام بعدی فقط Runtime Core محدود خانواده `RPT1-F05` است:** identity/schema نسخه‌دار، Application
Contract/Projection خواندنی و cutoff-aware Finance، compatibility projection fail-closed، selector،
calculator، semantic Snapshot builder و Unit/contract test. Migration/API/Catalog/Worker/Renderer،
UI/UX2 و Production enablement همچنان خارج از Scope و خاموش می‌مانند.

## Resume Rule

در ادامه‌های بعدی ابتدا همین سند، GitHub branch head/tree و آخرین Checkpoint خوانده شوند. فقط اگر
یکی از آن‌ها تغییر کرده بود، اختلاف هدفمند بررسی شود؛ مرور دوباره همه Conversationها لازم نیست.
