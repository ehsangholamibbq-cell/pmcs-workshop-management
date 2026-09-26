# PMCS — Canonical Project Reference

- شناسه: `PMCS-CANONICAL-REF-001`
- نسخه: `1.32.0`
- آخرین کنترل: ۱۴۰۵/۰۷/۰۴ (۲۰۲۶-۰۹-۲۶)
- وضعیت: `Authoritative working reference | V1 locked | V1.1 RPT1 / F06 Connected Safe Checkpoint`
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
| آخرین Source Candidate واجد Evidence | `df3879dd8b17403787154a398cc114b27c7172bc`؛ tree `5483e684aaa220a32b3135ea0b2bb3b2136023be` |
| Current evidence-bearing source checkpoint | `df3879dd8b17403787154a398cc114b27c7172bc`؛ tree `5483e684aaa220a32b3135ea0b2bb3b2136023be`؛ Run 202 سبز |
| Source lineage | S07-MS21 فرزند Checkpoint `32772e1f19c9c9d8023947654a3402f53ee0f6b6` است؛ هیچ reset یا بازطراحی baseline انجام نشد |
| Current safe checkpoint | `PMCS-V1.1-RPT1-S07-MS21-C1`؛ F01 تا F06 متصل و F07 تا F10 بازند |
| Migration count | Safe Resume: `48` و Restore Drill سبز |

PMCS V1.1 هنوز `Feature Complete`، `Release Candidate`، `Qualified`، `Final` یا `Baseline Locked`
نیست. Baseline قفل‌شده V1 نیز باز نشده است.

## Effective Roadmap

| وضعیت | سند مؤثر |
| --- | --- |
| Active | `docs/roadmaps/pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.55.0` |
| Active program | `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.0.0` |
| Active program | `docs/roadmaps/pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.2.0` |
| Historical/Complete | `docs/roadmaps/pmcs-v1-development-and-qualification.md` |

ترتیب مؤثر V1.1: `G0 → UX1 → EXT1 → DOC1 → IAM1/PRJ1/RPT1/COL1 → UX2 → INT1 → QA1 → V1.1 Locked`.
وضعیت فعلی: G0، UX1، EXT1، DOC1، IAM1 و PRJ1 بسته؛ RPT1 فعال؛ COL1، UX2، INT1 و QA1 باز.

## آخرین تصمیم‌های مؤثر و Supersededها

| موضوع | وضعیت قبلی | مرجع مؤثر فعلی |
| --- | --- | --- |
| وضعیت V1 | `Feature Complete` یا Qualification در جریان | Superseded؛ V1 با Run 69 `Qualified | Final | Baseline Locked` است |
| Roadmap Post-V1 | نسخه‌های تا `v1.54.0` | Superseded؛ `v1.55.0` مرجع جاری است |
| انتهای Development 05 | توقف در RPT1/MS05 | Superseded؛ GitHub/CI پیشرفت معتبر تا `S07-MS21` را اثبات می‌کند |
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
- Runtime Core محدود وضعیت مالی F05: source
  `77ad46cbac12b899116516b0a58665ae888b3bf2`، tree
  `5c67523b0fbed8d521627fe406f74271a1bbdcfe` و Run 175 با هر هشت Job، `450/450` تست C# شامل
  `31/31` case متمرکز F05، `75/75` تست Node، `139/139` تست Web، پنج browser scenario، Restore ۴۶
  Migration و Qualification `7/7` سبز؛ Renderer/Golden و wiring بازند.
- Renderer/Golden قطعی وضعیت مالی F05: source
  `9ddf7f1d96324e7ffb22d2abec83071a6c087ec2`، tree
  `f873795dcb8893dc28f88d5e5fc8292c5201e1e4` و Run 178 با هر هشت Job، `456/456` تست C# شامل
  شش case Renderer/Golden تازه و `37/37` case متمرکز F05، `77/77` تست Node، `139/139` تست Web،
  پنج browser scenario، Restore ۴۶ Migration و Qualification `7/7` سبز؛ Catalog/API/Worker wiring
  باز است.
- Catalog/API/Worker متصل وضعیت مالی F05: source
  `6de1e9ac3b457426be5e50064d1767106cd50c39`، tree
  `a4a8e8e655c56d05da2be5d87e7b84a9bb9a7a1f` و Run 185 با هر هشت Job، `458/458` تست C# شامل
  `39/39` case متمرکز F05، `78/78` تست Node، `139/139` تست Web، پنج browser scenario، هارنس
  `15/15`، validator روی `394` فایل، Restore ۴۷ Migration و Qualification `7/7` سبز؛ UI/Production
  همچنان خاموش است.
- DoR و قرارداد معنایی قرارداد/خرید/تأمین F06: source
  `4c5d026466cb3f76f351221297540a0936337335`، tree
  `d9e8febc5f220f8d00eaff80926b00dec2ea0926` و Run 188 با هر هشت Job، `458/458` تست C#،
  `80/80` تست Node، `139/139` تست Web، پنج browser scenario، validator روی `394` فایل، Restore ۴۷
  Migration و Qualification `7/7` سبز؛ Runtime هنوز پیاده نشده است.
- Runtime Core محدود قرارداد/خرید/تأمین F06: source
  `177a1d89a07c23b2ae446218e98556cfbcf57a21`، tree
  `165cd1d451935f3cb94db7b9f5718678de00aca2` و Run 192 با هر هشت Job، `490/490` تست C# شامل
  `32/32` case متمرکز F06، `82/82` تست Node، `139/139` تست Web، پنج browser scenario، validator روی
  `402` فایل، Restore ۴۷ Migration و Qualification `7/7` سبز؛ Renderer/Golden و wiring بازند.
- Renderer/Golden قطعی قرارداد/خرید/تأمین F06: source
  `fb88b94d6949e7780f5f40aa567e2ea3f187a6e8`، tree
  `212c1193d249cf1120297920c59b4ea15cb80c07` و Run 196 با هر هشت Job، `496/496` تست C# شامل
  شش case Renderer/Golden تازه و `38/38` case متمرکز F06، `84/84` تست Node، `139/139` تست Web،
  پنج browser scenario، validator روی `405` فایل، Restore ۴۷ Migration و Qualification `7/7`
  سبز؛ Catalog/API/Worker wiring باز است.
- Catalog/API/Worker متصل قرارداد/خرید/تأمین F06: source
  `df3879dd8b17403787154a398cc114b27c7172bc`، tree
  `5483e684aaa220a32b3135ea0b2bb3b2136023be` و Run 202 با هر هشت Job، `497/497` تست C# شامل
  `39/39` case متمرکز F06، `85/85` تست Node، `139/139` تست Web، پنج browser scenario، هارنس
  `15/15`، validator روی `406` فایل، Restore ۴۸ Migration و Qualification `7/7` سبز؛ UI/Production
  همچنان خاموش است.
- Run 130: هر ۸ Job سبز، `329/329` تست C#، `51/51` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario، `13/13` Golden assertion و Restore کامل ۴۳ Migration.
- Evidence checkpoint معتبر: `docs/checkpoints/v1.1-rpt1-slice-07-ms21-candidate.md`.
- Anchor انتقال Run 132 (`35459192122`) روی commit `345d9d6fc2e661144a74e3001150c28d73a212c7`
  هر هشت Job را سبز کرد.

## Current In-Progress Work

`V1.1-RPT1` فعال است و Safe Resume Point قطعی فعلی آن `PMCS-V1.1-RPT1-S07-MS21-C1` است. روی این
Checkpoint، Catalog/API/Worker و qualification متصل F01 تا F06 بسته شده‌اند.
Migration 48،
Definition/Template seed، strict empty-object API، Project profile pin، مجوز منبع definition-aware و
Worker/Renderer dispatch برای F03 در Run 156 qualify شده‌اند. تصمیم
`QuestPDF Community` در ADR 0030 ثبت و package/image/font digestها، PDF Golden متصل QA-only، visual
digest و performance budget در Run 133 qualify شده‌اند. `PdfLicense=Unconfigured` و
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` در defaults و `OrphanRemediationMode=Disabled`
حفظ شده‌اند. ADR 0031 انتخاب صریح مالک محصول برای حفظ Scope ده‌گانه را ثبت کرده است:
`RPT1-F01` تا `RPT1-F06` checkpoint متصل دارند؛ F07 تا F10
`Required / Not Implemented` هستند. قرارداد
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
قرارداد checkpointed `PMCS-RPT1-F05-SEMANTIC-001 v1.3.1`، Cash Position را فقط از رکوردهای Posted
تا cutoff، تعهدات Approved و settlementهای immutable می‌سازد؛ Payable/Receivable و Aging چهار-bucketی
جدا هستند و Budget Baseline اختیاری lifecycle مستقل دارد. Client فقط `{}` می‌فرستد؛ چهار Permission
Finance/Budget و Classification حداقل `Confidential` قطعی‌اند. Runtime Core نسخه‌دار اکنون Contract
و compatibility source در Finance، selector lifecycle، calculator و semantic Snapshot builder را
دارد؛ history غیرقابل‌اثبات، currency/overlap/over-allocation و classification ناسازگار fail-closed
هستند. Run 175 Runtime Core را سبز کرد و Checkpoint `S07-MS16` نیز Template/Renderer/Layout
identity، render model canonical، PDF/XLSX deterministic و Goldenهای binary/visual/performance را
بست. Checkpoint `S07-MS17` با Migration 47، Catalog/Template، strict `{}`، Project profile pin، چهار
Permission منبع definition-aware و Worker/Renderer dispatch، این مسیر را End-to-End متصل کرد؛ Run
185 هر هشت Job و هارنس `15/15` را سبز کرد. defaultهای Production همچنان خاموش‌اند.

قرارداد checkpointed `PMCS-RPT1-F06-SEMANTIC-001 v1.3.1`، زنجیرهٔ Contract/Amendment/Request/
Order/Receipt/Service Acceptance را با lifecycle و cutoff مستقل تثبیت می‌کند. Client فقط `{}`
می‌فرستد؛ مبلغ/مدت مؤثر Contract، commitment سفارش، fulfillment، وضعیت تحویل و supplier count/rate
فقط از evidence رسمی، Party/Item snapshot و quantity basis نسخه‌دار ساخته می‌شوند. F06 هیچ عددی از
F05، Inventory/Stock، Invoice/Payment، RFQ/Tender، ranking یا AI تولید نمی‌کند. شش Permission
Commercial/Procurement/Supply و Classification حداقل `Confidential` قطعی‌اند. Runtime Core
نسخه‌دار اکنون Contract و compatibility source در Commercial، selector lifecycle، calculator و
semantic Snapshot builder را دارد؛ Item snapshot در زمان Issue پین و هر history غیرقابل‌اثبات
fail-closed می‌شود. Run 192 Runtime Core را سبز کرد. Checkpoint `S07-MS20` نیز
Template/Renderer/Layout identity، render model canonical، PDF فارسی/RTL سه‌صفحه‌ای، XLSX ده-Sheet
و Goldenهای binary/visual/performance را بست و Run 196 هر هشت Job را سبز کرد. Checkpoint
`S07-MS21` با Migration 48، Catalog/Template، strict `{}`، Project profile pin، شش Permission منبع
definition-aware و Worker/Renderer dispatch این مسیر را End-to-End متصل کرد؛ Run 202 هر هشت Job و
هارنس `15/15` را سبز کرد. defaultهای Production همچنان خاموش‌اند.

## Remaining Work

1. خانواده `RPT1-F07` با DoR/قرارداد معنایی مستقل، سپس Runtime و Renderer/wiring در Micro-Stepهای
   بعدی و Qualification مستقل.
2. سپس خانواده‌های `RPT1-F08` تا `RPT1-F10` با
   Micro-Slice و Qualification مستقل؛
   Scope ده‌گانه طبق ADR 0031 حفظ شده است.
3. UI اختصاصی Reporting و visual regression در UX2؛ سپس تکمیل COL1/UX2/INT1/QA1 طبق ترتیب مصوب.
4. Pilot و gateهای وابسته به محیط واقعی فقط در زمان مقرر؛ Evidence فعلی مجوز Production rollout نیست.

## Known Gaps / Issues

| شدت | مورد | اثر/اقدام لازم |
| --- | --- | --- |
| Implementation | F01 تا F06 واجد Safe Checkpoint متصل‌اند و F07 تا F10 پیاده‌نشده‌اند | DoR/قرارداد معنایی مستقل F07، سپس Micro-Sliceهای بعدی |
| Temporal source | endpoint جاری Planning زمان‌های Approval/Supersede، target و configuration تاریخی کافی ندارد | Contract/selector نسخه‌دار و compatibility producer متصل‌اند؛ history غیرقابل‌اثبات fail-closed است و توسعهٔ تاریخچهٔ کامل باید Slice دامنه‌ای مستقل باشد |
| Finance temporal source | read modelها و serviceهای legacy Finance cutoff تاریخی، postedAt/approvedAt مستقل، Budget supersession history و Aging دوطرفهٔ کامل ندارند | Contract/selector/compatibility source نسخه‌دار F05 متصل است و history غیرقابل‌اثبات را fail-closed رد می‌کند؛ producer تاریخی غنی‌تر در صورت نیاز Slice دامنه‌ای مستقل است |
| Commercial temporal source | source و endpointهای legacy current-state/truncated هستند؛ activation history، Party/Item snapshot، conversion version و completeness cutoff کامل ندارند | Contract/selector/compatibility source نسخه‌دار F06 متصل است و history غیرقابل‌اثبات را fail-closed رد می‌کند؛ producer تاریخی غنی‌تر در صورت نیاز Slice دامنه‌ای مستقل است |
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
- F05 Runtime Core Source: `77ad46cbac12b899116516b0a58665ae888b3bf2`؛ tree
  `5c67523b0fbed8d521627fe406f74271a1bbdcfe`؛ PR validation merge
  `5f9ad7bd2bf0cb48c5a47dbfbe29ab09afc3f92c` با همان tree؛ Run 175 (`35541740268`) هر هشت Job
  موفق، `450/450` تست C# شامل `31/31` case متمرکز F05، `75/75` تست Node، `139/139` تست Web، پنج
  browser scenario، validator روی `390` فایل، audit `274/204/5` و Restore ۴۶ Migration؛
  Renderer/Golden و wiring F05 باز است.
- F05 Renderer/Golden Source MS16: `9ddf7f1d96324e7ffb22d2abec83071a6c087ec2`؛ tree
  `f873795dcb8893dc28f88d5e5fc8292c5201e1e4`؛ PR validation merge
  `72ab7827731fa763828c049be953ee9ca8c128a4` با همان tree؛ Run 178 (`35558202348`) هر هشت Job
  موفق، `456/456` تست C# شامل شش case Renderer/Golden تازه و `37/37` case متمرکز F05، `77/77`
  تست Node، `139/139` تست Web، پنج browser scenario، validator روی `393` فایل، audit `274/204/5`
  و Restore ۴۶ Migration؛ Catalog/API/Worker wiring F05 باز است.
- F05 Connected Source MS17: `6de1e9ac3b457426be5e50064d1767106cd50c39`؛ tree
  `a4a8e8e655c56d05da2be5d87e7b84a9bb9a7a1f`؛ PR validation merge
  `cbd27a673b1887b2e245bef5340eb8199f480be4` با همان tree؛ Run 185 (`35563055242`) هر هشت Job
  موفق، `458/458` تست C# شامل `39/39` case متمرکز F05، `78/78` تست Node، `139/139` تست Web، پنج
  browser scenario، هارنس F05 برابر `15/15`، validator روی `394` فایل، audit `274/204/5` و Restore
  ۴۷ Migration؛ UI/Production خاموش است.
- F06 Semantic Contract Source MS18: `4c5d026466cb3f76f351221297540a0936337335`؛ tree
  `d9e8febc5f220f8d00eaff80926b00dec2ea0926`؛ PR validation merge
  `9bfb7badcde8a67d385567db997f265a67497f0f` با همان tree؛ Run 188 (`35567252567`) هر هشت Job
  موفق، `458/458` تست C#، `80/80` تست Node، `139/139` تست Web، پنج browser scenario، validator روی
  `394` فایل، audit `274/204/5` و Restore ۴۷ Migration؛ Runtime F06 پیاده نشده است.
- F06 Runtime Core Source MS19: `177a1d89a07c23b2ae446218e98556cfbcf57a21`؛ tree
  `165cd1d451935f3cb94db7b9f5718678de00aca2`؛ PR validation merge
  `2dbaf0ba14cf80ee863e07c2561ab7392037fd7c` با همان tree؛ Run 192 (`35573450703`) هر هشت Job
  موفق، `490/490` تست C# شامل `32/32` case متمرکز F06، `82/82` تست Node، `139/139` تست Web، پنج
  browser scenario، validator روی `402` فایل، audit `274/204/5` و Restore ۴۷ Migration؛
  Renderer/Golden و wiring F06 باز است.
- F06 Renderer/Golden Source MS20: `fb88b94d6949e7780f5f40aa567e2ea3f187a6e8`؛ tree
  `212c1193d249cf1120297920c59b4ea15cb80c07`؛ PR validation merge
  `9f8d6ce25b5ddbeec777d0e104024e9389b9a513` با همان tree؛ Run 196 (`35582136746`) هر هشت Job
  موفق، `496/496` تست C# شامل شش case Renderer/Golden تازه و `38/38` case متمرکز F06، `84/84`
  تست Node، `139/139` تست Web، پنج browser scenario، validator روی `405` فایل، audit `274/204/5`
  و Restore ۴۷ Migration؛ Catalog/API/Worker wiring F06 باز است.
- F06 Connected Source MS21: `df3879dd8b17403787154a398cc114b27c7172bc`؛ tree
  `5483e684aaa220a32b3135ea0b2bb3b2136023be`؛ PR validation merge
  `0900def8f237a8d282501a8ee4ae0be5676f2fda` با همان tree؛ Run 202 (`36235821024`) هر هشت Job
  موفق، `497/497` تست C# شامل `39/39` case متمرکز F06، `85/85` تست Node، `139/139` تست Web، پنج
  browser scenario، هارنس F06 برابر `15/15`، validator روی `406` فایل، audit `274/204/5` و Restore
  ۴۸ Migration؛ UI/Production خاموش است.
- Catalog Decision Candidate: `d81ecc00762145210e1c688f8f5843f46d62fc04`؛ tree
  `5f40383ad506d94520c741eb69fcd00086283734`؛ Run 135 (`35466775368`) هر هشت Job موفق،
  `330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web و پنج browser scenario.
- Source Candidate MS06: `b8f21492a4f44c7c412e5b7eda0b164e7f256758`؛ tree
  `e94b6ba3753e67b42ea0ec99e998761fdad0bcc3`.
- آخرین CI بررسی‌شده: Run 202 (`36235821024`) — هر ۸ Job
  `architecture/backend/integration/pilot-contract/web/ui-e2e/identity-container/qualification-report` موفق.
- Qualification artifact Run 202 برابر `10903829279` با digest
  `sha256:86b5a127f3c04d9732489668e97c58ac2c828d159e94e67bda06cdbf9426d12c` است؛ Integration artifact
  `10904376645` با digest `sha256:6a99e3a7ce2b4104085c71e29d9eadb43b6bd9898f821dcb5cb53a3871cd5ae9`
  و UI-E2E artifact `10904401482` با digest
  `sha256:e21ac77c6878a47ce455487c8ccf480cb5dfef099729b5cda85619f5feb1cd41` ثبت شدند.
- Run 133: build بدون warning، `330/330` تست C#، `52/52` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario و PDF Golden متصل `8/8` پاس شدند؛ PDF برابر `42489` byte و
  SHA-256 `cc188c842ddcced9a24acd5e18f92c4a6511252c40104055c8c38627cdfac863` بود.
- visual digest در ۹۶ DPI برابر
  `95d6e71de15d9d130041d5c295c94239b9fe9095e6572e581aa9a655ee85c9b2` و بازبینی مستقل Poppler
  بدون defect بود. Qualification artifact `10590681541` با digest
  `sha256:00f630ce572f49041af75d5687f0e79dafccb3a8359bc2ddb741c9e7b3c8d9e7` ثبت شد.

## Exact Next Micro-Step

**گام بعدی فقط DoR و قرارداد معنایی مستقل خانواده `RPT1-F07` است:** مرز دفتر فنی شامل
Document/RFI/Submittal/Transmittal، Source lineage، permission/classification، status/reason و Golden
matrix باید بدون Runtime تثبیت شوند. Runtime، Renderer، Migration، Catalog/API/Worker، UI/UX2،
Production enablement و Report Designer خارج از Scope و خاموش می‌مانند.

## Resume Rule

در ادامه‌های بعدی ابتدا همین سند، GitHub branch head/tree و آخرین Checkpoint خوانده شوند. فقط اگر
یکی از آن‌ها تغییر کرده بود، اختلاف هدفمند بررسی شود؛ مرور دوباره همه Conversationها لازم نیست.
