# PMCS — Canonical Project Reference

- شناسه: `PMCS-CANONICAL-REF-001`
- نسخه: `1.0.0`
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
| Remote branch head در Snapshot کنترل | `dabc9025bd78806b68c4d0e63c70f5fde0c8b89f`؛ parent مستقیم commit انتشار همین مرجع |
| Local head پیش از ایجاد این سند | `e5e727ccc055fc74f62a540153334ffffea168eb` |
| Source equivalence | Local و Remote هر دو tree `164b2e1ed8749a91d7d68026b05dc71383a8b64f`؛ تفاوت SHA فقط ناشی از commit identity است |
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

## Current In-Progress Work

`V1.1-RPT1` فعال است و Safe Resume Point دقیق آن `PMCS-V1.1-RPT1-S06-MS05-C1` است. هیچ Runtime
work نیمه‌کاره یا migration نیمه‌اعمال‌شده در Worktree پیدا نشد. مسیر PDF عمداً به‌علت تصمیم حقوقی
باز است؛ `PdfLicense=Unconfigured` و `Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` هستند و
`OrphanRemediationMode=Disabled` است.

## Remaining Work

1. ثبت تصمیم حقوقی صریح درباره PDF renderer/license؛ تصمیم نباید از کد یا Conversation استنباط شود.
2. پس از تصمیم: pin کردن نسخه/digest renderer image و فونت، سپس PDF Golden، visual/pixel و performance budget.
3. پیش از اعلام بسته‌شدن RPT1، تعیین تکلیف رسمی اختلاف کاتالوگ: Roadmap ده خانواده گزارش استاندارد
   نام می‌برد، اما Runtime/Golden فعلی فقط `daily-report-certified/1.0.0` را پیاده و qualify کرده است.
4. UI اختصاصی Reporting و visual regression در UX2؛ سپس تکمیل COL1/UX2/INT1/QA1 طبق ترتیب مصوب.
5. Pilot و gateهای وابسته به محیط واقعی فقط در زمان مقرر؛ Evidence فعلی مجوز Production rollout نیست.

## Known Gaps / Issues

| شدت | مورد | اثر/اقدام لازم |
| --- | --- | --- |
| Scope | کاتالوگ RPT1 در Roadmap شامل ۱۰ خانواده است؛ فقط گزارش روزانه رسمی موجود است | ۹ خانواده دیگر Done نیستند؛ یا باید پیاده شوند یا Scope با Change Record نسخه‌دار اصلاح شود |
| Documentation | متن PR #2 هنوز Roadmap `v1.14.0`، head قدیمی و gateهای MS03–MS05 را باز نشان می‌دهد | PR body با این مرجع و `v1.20.0` همگام شود؛ کد/CI متأثر نیست |
| Traceability | دو ADR با شماره `0027` وجود دارد | بدون renumber شتاب‌زده، یک تصمیم نسخه‌دار برای شناسه یکتا ثبت شود |
| Ownership | اسناد، UI Reporting را هم «gate باز RPT1» و هم کار UX2 می‌خوانند | مالک gate بسته‌شدن RPT1/UX2 باید در Roadmap صریح شود |
| Workspace metadata | branch محلی upstream ندارد و `pmcs.upstreamcommit` روی SHA قدیمی است | مبنای sync باید Remote head/tree بالا باشد، نه config محلی قدیمی |
| Intentional gate | PDF license پیکربندی نشده و feature flagها خاموش‌اند | نقص Runtime نیست؛ شرط ایمنی تا تصمیم/Qualification است |

## Current GitHub / CI State

- Repository: `ehsangholamibbq-cell/pmcs-workshop-management`؛ PR #2 از `v1.1-development` به `main`.
- PR: `open`، `draft`، `mergeable`، ادغام‌نشده؛ ۶۶ commit و ۲۲۸ فایل تغییرکرده.
- Remote head در لحظه کنترل و parent انتشار این مرجع: `dabc9025bd78806b68c4d0e63c70f5fde0c8b89f`؛ merge-test commit:
  `1385aa00fd621035266c209f89d2b1b7b9494aa0`.
- آخرین CI بررسی‌شده: Run 131 (`35450076853`) — هر ۸ Job
  `architecture/backend/integration/pilot-contract/web/ui-e2e/identity-container/qualification-report` موفق.
- Run 130 (`35449387794`) Evidence اصلی MS05 روی Source Candidate است؛ Run 131 checkpoint/docs head را
  با همان مجموعه هشت‌گانه سبز کرده است.
- کنترل هدفمند محلی در ۲۰۲۶-۰۹-۱۹: repository validator روی ۳۴۳ فایل، system-contract audit روی
  ۲۷۴ endpoint/۲۰۴ mutation/۵ استثنای مستند، و `21/21` تست contract RPT1 پاس شد. Full suite دوباره
  اجرا نشد، چون CI سبز موجود معتبر بود و هدف این کنترل rerun گسترده نبود.

## Exact Next Micro-Step

**گام بعدی اکنون یک Decision checkpoint است، نه تغییر Runtime:** تصمیم حقوقی renderer/license PDF را
به‌صورت ADR/Update صریح تصویب و ثبت کن. پس از آن، فقط یک Micro-Step محدود از `S06-MS05` بساز:
pin نسخه و digest renderer image و فونت، فعال‌سازی صرفاً در QA، تولید PDF قطعی برای همان fixture Golden،
و سنجش integrity/text/visual/performance. سپس با Evidence مستقل checkpoint کن؛ هیچ feature flag
Production روشن نشود. قبل از بستن RPT1 نیز اختلاف کاتالوگ ۱۰گانه حتماً با Change Record حل شود.

## Resume Rule

در ادامه‌های بعدی ابتدا همین سند، GitHub branch head/tree و آخرین Checkpoint خوانده شوند. فقط اگر
یکی از آن‌ها تغییر کرده بود، اختلاف هدفمند بررسی شود؛ مرور دوباره همه Conversationها لازم نیست.
