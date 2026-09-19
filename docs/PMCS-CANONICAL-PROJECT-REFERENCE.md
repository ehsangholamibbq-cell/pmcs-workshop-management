# PMCS — Canonical Project Reference

- شناسه: `PMCS-CANONICAL-REF-001`
- نسخه: `1.1.0-candidate`
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
| آخرین Source Candidate واجد Evidence | `38a03f33f4747d0b6a76696705877633acd17678`؛ tree `eb9369c9e32eb3f523c4faa22487d6428c2d7e34` |
| Remote branch head پیش از MS06 Candidate | `345d9d6fc2e661144a74e3001150c28d73a212c7`؛ tree `8902fde167c0ff75545d0dd0360cd25d59a7b4e5` |
| Source equivalence | بازسازی محلی پیش از تغییرات دقیقاً tree `8902fde167c0ff75545d0dd0360cd25d59a7b4e5` را تولید کرد |
| Migration count | `43`؛ Restore Drill متصل پاس شده است |

PMCS V1.1 هنوز `Feature Complete`، `Release Candidate`، `Qualified`، `Final` یا `Baseline Locked`
نیست. Baseline قفل‌شده V1 نیز باز نشده است.

## Effective Roadmap

| وضعیت | سند مؤثر |
| --- | --- |
| Active | `docs/roadmaps/pmcs-post-v1-product-evolution.md` — `PMCS-RM-POST-V1-001 v1.20.0` |
| Active program | `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md` — `PMCS-RM-AGENT-001 v1.0.0` |
| Active program | `docs/roadmaps/pmcs-visual-excellence-program.md` — `PMCS-RM-VISUAL-001 v1.2.0` |
| Historical/Complete | `docs/roadmaps/pmcs-v1-development-and-qualification.md` |

ترتیب مؤثر V1.1: `G0 → UX1 → EXT1 → DOC1 → IAM1/PRJ1/RPT1/COL1 → UX2 → INT1 → QA1 → V1.1 Locked`.
وضعیت فعلی: G0، UX1، EXT1، DOC1، IAM1 و PRJ1 بسته؛ RPT1 فعال؛ COL1، UX2، INT1 و QA1 باز.

## آخرین تصمیم‌های مؤثر و Supersededها

| موضوع | وضعیت قبلی | مرجع مؤثر فعلی |
| --- | --- | --- |
| وضعیت V1 | `Feature Complete` یا Qualification در جریان | Superseded؛ V1 با Run 69 `Qualified | Final | Baseline Locked` است |
| Roadmap Post-V1 | نسخه‌های تا `v1.14.0` | Superseded؛ `v1.20.0` مرجع است |
| انتهای Development 04 | توقف در RPT1/MS02 | Superseded؛ GitHub/CI پیشرفت معتبر تا `S06-MS05` را اثبات می‌کند |
| Agent مدیریتی | عنوان کلی یا پنج فاز | Superseded؛ دقیقاً هفت Stage مستقل با Gateهای مستقل |
| Reporting | Report Designer آزاد در V1.1 | Superseded/خارج از Scope؛ V1.1 فقط گزارش‌های استاندارد و تأییدشده، Designer در V1.2 |
| UI | بسته‌شدن UX1 یعنی پایان بازطراحی | Superseded؛ UX1 فقط جهت بصری «مدیریت ممتاز» را بست؛ مهاجرت کامل در UX2 است |
| PDF license | تصمیم ثبت‌نشده و `Unconfigured` | ADR 0030؛ `QuestPDF Community` برای Qualification، با default همچنان `Unconfigured` و بازاعتبارسنجی eligibility پیش از Production |

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
- Run 130: هر ۸ Job سبز، `329/329` تست C#، `51/51` تست قراردادی Node، `139/139` تست Web، پنج
  browser scenario، `13/13` Golden assertion و Restore کامل ۴۳ Migration.
- Evidence checkpoint معتبر: `docs/checkpoints/v1.1-rpt1-slice-06-ms05-candidate.md`.
- Anchor انتقال Run 132 (`35459192122`) روی commit `345d9d6fc2e661144a74e3001150c28d73a212c7`
  هر هشت Job را سبز کرد.

## Current In-Progress Work

`V1.1-RPT1` فعال است و آخرین Safe Resume Point واجد CI همچنان
`PMCS-V1.1-RPT1-S06-MS05-C1` است. تصمیم `QuestPDF Community` در ADR 0030 ثبت و MS06 به‌صورت Candidate
محلی پیاده شده است: package/image/font digestها pin، PDF Golden متصل QA-only، visual digest و
performance budget اضافه شده‌اند. تا سبزشدن CI و ثبت checkpoint مستقل، این Candidate Safe Point
جدید محسوب نمی‌شود. `PdfLicense=Unconfigured` و
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` در defaults و `OrphanRemediationMode=Disabled`
حفظ شده‌اند.

## Remaining Work

1. اجرای Full CI روی MS06 Candidate و ثبت Evidence checkpoint برای PDF Golden/visual/performance.
2. پیش از اعلام بسته‌شدن RPT1، تعیین تکلیف رسمی اختلاف کاتالوگ: Roadmap ده خانواده گزارش استاندارد
   نام می‌برد، اما Runtime/Golden فعلی فقط `daily-report-certified/1.0.0` را پیاده و qualify کرده است.
3. UI اختصاصی Reporting و visual regression در UX2؛ سپس تکمیل COL1/UX2/INT1/QA1 طبق ترتیب مصوب.
4. Pilot و gateهای وابسته به محیط واقعی فقط در زمان مقرر؛ Evidence فعلی مجوز Production rollout نیست.

## Known Gaps / Issues

| شدت | مورد | اثر/اقدام لازم |
| --- | --- | --- |
| Scope | کاتالوگ RPT1 در Roadmap شامل ۱۰ خانواده است؛ فقط گزارش روزانه رسمی موجود است | ۹ خانواده دیگر Done نیستند؛ یا باید پیاده شوند یا Scope با Change Record نسخه‌دار اصلاح شود |
| Documentation | متن PR #2 هنوز Roadmap `v1.14.0`، head قدیمی و gateهای MS03–MS05 را باز نشان می‌دهد | PR body با این مرجع و Roadmap فعال همگام شود؛ کد/CI متأثر نیست |
| Traceability | دو ADR با شماره `0027` وجود دارد | بدون renumber شتاب‌زده، یک تصمیم نسخه‌دار برای شناسه یکتا ثبت شود |
| Ownership | اسناد، UI Reporting را هم «gate باز RPT1» و هم کار UX2 می‌خوانند | مالک gate بسته‌شدن RPT1/UX2 باید در Roadmap صریح شود |
| Workspace metadata | branch محلی upstream ندارد و `pmcs.upstreamcommit` روی SHA قدیمی است | مبنای sync باید Remote head/tree بالا باشد، نه config محلی قدیمی |
| Legal operations | Community انتخاب شده، اما eligibility دائمی از code استنباط نمی‌شود | پیش از Production و حداقل سالانه توسط مالک تجاری/حقوقی بازاعتبارسنجی شود |
| Intentional gate | PDF license در defaults پیکربندی نشده و feature flagها خاموش‌اند | نقص Runtime نیست؛ ADR 0030 فقط QA qualification را مجاز کرده است |

## Current GitHub / CI State

- Repository: `ehsangholamibbq-cell/pmcs-workshop-management`؛ PR #2 از `v1.1-development` به `main`.
- PR: `open`، `draft` و ادغام‌نشده است.
- Remote head پیش از MS06 Candidate: `345d9d6fc2e661144a74e3001150c28d73a212c7`؛ tree
  `8902fde167c0ff75545d0dd0360cd25d59a7b4e5`.
- آخرین CI بررسی‌شده: Run 132 (`35459192122`) — هر ۸ Job
  `architecture/backend/integration/pilot-contract/web/ui-e2e/identity-container/qualification-report` موفق.
- Run 130 (`35449387794`) Evidence اصلی MS05 روی Source Candidate است؛ Run 132 Canonical anchor را
  با همان مجموعه هشت‌گانه سبز کرده است.
- کنترل محلی MS06 در ۲۰۲۶-۰۹-۱۹: build بدون warning، `330/330` تست C#، `21/21` تست contract RPT1،
  دو رندر PDF byte-identical، visual digest ثابت در ۹۶ DPI و بازبینی مستقل Poppler بدون defect پاس
  شدند. Connected CI هنوز باید این Candidate را qualify کند.

## Exact Next Micro-Step

**گام بعدی فقط تکمیل همان MS06 Candidate است:** Full CI هشت‌Job را اجرا، PDF Golden متصل را بررسی و
در صورت سبزشدن `PMCS-V1.1-RPT1-S06-MS06-C1` را ثبت کن؛ هیچ feature flag Production روشن نشود.
پس از این checkpoint و پیش از بستن RPT1، اختلاف کاتالوگ ۱۰گانه حتماً با Change Record رسمی حل شود.

## Resume Rule

در ادامه‌های بعدی ابتدا همین سند، GitHub branch head/tree و آخرین Checkpoint خوانده شوند. فقط اگر
یکی از آن‌ها تغییر کرده بود، اختلاف هدفمند بررسی شود؛ مرور دوباره همه Conversationها لازم نیست.
