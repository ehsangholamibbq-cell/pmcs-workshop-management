# ممیزی تداوم PMCS از Blueprint تا RPT1

- شناسه: `PMCS-AUDIT-CONTINUITY-2026-09-18`
- تاریخ: ۱۴۰۵/۰۶/۲۷ (۲۰۲۶-۰۹-۱۸)
- نوع: ممیزی فقط‌خواندنی تصمیم، Baseline و Traceability
- خط فعال: `PMCS V1.1`
- وضعیت رسمی خط فعال: `Development | UX1/EXT1/DOC1/IAM1/PRJ1 Closed | RPT1 Active`
- Parent product baseline: `PMCS V1 / 26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- Worktree source commit: `720de8869e251f5a4c39a6940a76e9929232706b`
- Source tree: `bae26b6e8295ef6ffa6f5c671d26d5fc39b34d1b`
- آخرین Qualification Candidate: `167133fc1985c5b57c3dac90535f7a962dfd03b7`
- آخرین Connected evidence: Run 108 (`35393509764`) — `success`

## ۱. هدف و روش

این ممیزی برای جلوگیری از گم‌شدن تصمیم‌ها میان گفت‌وگوهای «طراحی سامانه جامع کارگاهی ۱»،
«۲» و «۳» انجام شد. مبنا فقط حافظهٔ مکالمه نبود. سه لایهٔ مستقل با هم تطبیق داده شدند:

1. `PMCS_Blueprint_V1_21_FA.md` با ۱۰٬۸۶۰ خط و تمام بخش‌های تصمیم تأییدشده؛
2. Roadmap تکمیلی پس از Blueprint، شامل Checkpointهای ۲۲ تا ۲۴، QA Foundation و Agent-ready/Test-ready؛
3. Repository، ADRها، Checkpointها، Manifestهای Release، کد، Test contract و Evidence متصل CI.

دو نسخهٔ بازیابی‌شدهٔ Roadmap تکمیلی (`new roadmap.txt` و `new roadmap(1).txt`) از نظر متن
کاملاً یکسان بودند. بنابراین هیچ تعارض محتوایی میان نسخه‌های بازیابی‌شده وجود ندارد.

## ۲. زنجیرهٔ مرجع حاکم

ترتیب تقدم تصمیم‌ها به این صورت است:

1. Blueprint V1.21 برای معماری و Scope اصلی V1؛
2. Roadmap تکمیلی برای Checkpointهای ۲۲ تا ۲۴ و Qualification؛
3. Baseline قفل‌شدهٔ V1 و Evidence Run 69؛
4. Roadmap حاکم Post-V1 برای V1.1، V1.2 و V2.x؛
5. Roadmap مستقل هفت‌مرحله‌ای Agent و Visual Excellence Program؛
6. ADR و Contract هر Checkpoint برای جزئیات اجرایی همان Checkpoint.

سند جدیدتر فقط در محدودهٔ Change Control مصوب، سند قدیمی‌تر را تکمیل می‌کند. هیچ سند جدیدی
اجازهٔ بازنویسی تاریخچه، شکستن V1 Locked یا حذف قابلیت قبلی را ندارد.

## ۳. نتیجهٔ ممیزی تصمیم‌های Blueprint

| حوزه | تصمیم‌های حاکم | وضعیت Traceability |
| --- | --- | --- |
| معماری | Modular Monolith، Monorepo، مالکیت Schema، Application Contract، منع Cross-schema | ثبت در ADR و Guard معماری؛ برقرار |
| هویت و دسترسی | حساب مستقل، Tenant/Project boundary، RBAC+Scope+Guard، Default Deny، Permission عملیاتی | IdentityAccess، Preview و تست منفی؛ برقرار |
| واقعیت کارگاه | Daily Report نسخه‌دار، Fact ساختاریافته، Evidence، Workflow رسمی | V1 Qualified؛ برقرار |
| تاریخ و Locale | UTC/ISO در Backend، شمسی و RTL در تمام مسیر کاربر | ADR 0023 و audit fail-closed؛ برقرار |
| مالی | IRR canonical، تومان پیش‌فرض UI، هزینه/پرداخت/تعهد مستقل، Budget اختیاری | Finance Lite و Checkpoint 24؛ برقرار |
| فنی/قرارداد | Revision immutable، Transmittal رسمی، RFI Answered/Closed مستقل | TechnicalOffice؛ برقرار |
| خرید و انبار | WBS/Budget اختیاری، Receipt/Acceptance/Available مستقل، Ledger افزایشی | Commercial/Supply؛ برقرار |
| Quality/HSE | دو قابلیت مستقل و اختیاری، Corrective Action و محرمانگی HSE | QualitySafety؛ برقرار |
| Issue/Risk/Decision | موجودیت‌های مستقل، Complete/Verified/Closed مستقل، SLA/Escalation | ActionControl؛ برقرار |
| Project State | Read Model قطعی، Coverage/Freshness، نبود داده بدون صفر/سبز ساختگی | ProjectIntelligence؛ برقرار |
| Offline | Operation-based، Idempotency، Revision، Conflict و Server authority | Sync + Checkpoint 23 + QA Slice 4/5؛ برقرار |
| AI در V1 | Advisory-only، cited، permission-aware، بدون تغییر Fact یا محاسبه قطعی | V1 Intelligence محدود؛ برقرار و با Agent آینده یکی تلقی نشده است |

نتیجه: تصمیم تأییدشده‌ای از Blueprint که باید در Baseline محصول V1 حضور می‌داشت، بدون مالک یا
بدون Traceability رها نشده است. محدودیت‌های محیط Pilot در بخش ۷ این سند جدا شده‌اند.

## ۴. تکامل پس از انتهای Blueprint

Blueprint V1.21 تاریخچهٔ پیاده‌سازی را تا Checkpoint 19.1 ثبت می‌کند. ادامهٔ رسمی در Repository
و Roadmap تکمیلی ثبت شده و حذف نشده است:

| مرحله | خروجی | وضعیت |
| --- | --- | --- |
| Checkpoint 20 | System integrity و اصلاح عیوب پراثر | بسته |
| Checkpoint 21 | Project readiness و Effective Permission Preview | بسته |
| Checkpoint 22 | My Work، Notifications و Daily Report Revision | بسته |
| Checkpoint 23 | Offline/Sync/Recovery/Conflict | بسته |
| Checkpoint 24 | Finance Lite integration و Location | بسته؛ V1 Feature Complete |
| QA Slice 1 | Seed/Reset، Test Auth، Diagnostics و QA Gateway | بسته |
| QA Slice 2 | Permission/Workflow/Database/Audit verification | بسته |
| QA Slice 3 | File/Attachment verification | بسته |
| QA Slice 4 | Offline/Sync verification | بسته |
| QA Slice 5 | Browser UI/E2E | بسته |
| QA Slice 6 | Agent/Exploratory verification | بسته |
| QA Slice 7 | Regression Runner، Report Generator و Full Regression | بسته |
| PMCS V1 | Qualified → Final → Baseline Locked | بسته در Run 69 |

Run 69 هر هفت Suite و ۱۲ فرمان را روی یک SHA ثابت پاس کرد: ۲۵۳ تست Backend، ۱۲۴ تست Web،
Integration واقعی PostgreSQL/MinIO، چهار سناریوی مرورگر، Identity container و Restore drill.

## ۵. تکامل Roadmap پس از قفل V1

| Checkpoint | Evidence قطعی | وضعیت واقعی |
| --- | --- | --- |
| `V1.1-G0` | Governance، Change Control، Test/Compatibility contracts | بسته |
| `V1.1-UX1` | فقط `Visual Direction Approved` برای «مدیریت ممتاز» | Gate جهت بصری بسته؛ Program کامل نشده است |
| `V1.1-EXT1` | Module/Permission/Navigation/Event/Tool manifests | بسته؛ Run 77 |
| `V1.1-DOC1` | Shared Documents، Scanner/Quarantine/Retention | بسته؛ Run 83 |
| `V1.1-IAM1` | Login Descriptor و Member Profile | بسته؛ Run 88 |
| `V1.1-PRJ1` | Controlled Project Bootstrap/Duplication | بسته؛ Run 92 |
| `V1.1-RPT1` | Reporting Center Phase 1؛ Slice 01–05 | **فعال؛ core/cancel/security/recovery/revocation/object integrity متصل پاس شده و Gate خروج باز است** |
| `V1.1-COL1` | Project Collaboration | برنامه‌ریزی‌شده |
| `V1.1-UX2` | Full Product UI migration | برنامه‌ریزی‌شده |
| `V1.1-INT1` | Agent Stage 1 | برنامه‌ریزی‌شده |
| `V1.1-QA1` | V1.1 Qualification/Lock | برنامه‌ریزی‌شده |

آخرین Candidate واقعی PRJ1 روی source commit
`e1b5bf6af813af7324065edc1c91eecf2391eccd` و tree
`0539d3c7b337a1785ffd2334c7c1457f92d629c2` در Run 92 با هر هشت Job سبز بسته شد.
Evidence commit بعدی فقط وضعیت PRJ1/RPT1 را ثبت کرد. Tree جاری
`bae26b6e8295ef6ffa6f5c671d26d5fc39b34d1b` همان محتوای Evidence متصل ثبت‌شده است.

از زمان Snapshot اولیهٔ این ممیزی، RPT1 تا Slice 05 ادامه یافته است. آخرین Candidate روی source
commit `167133fc1985c5b57c3dac90535f7a962dfd03b7` و tree
`34fb70aee9a62a434a8446444d7c6d5c6c9819bd` در Run 108 هر هشت Job را پاس کرده است. Addendumهای
۱۱ تا ۱۶ زنجیرهٔ کامل این ادامه را بدون بازنویسی Evidence تاریخی ثبت می‌کنند.

## ۶. Agent مدیریتی؛ تطبیق کامل هفت Stage

عبارت پنج‌موردی Roadmap اولیه خلاصهٔ قابلیت‌ها بود و مرجع نهایی Stageبندی نیست. تصمیم تکامل‌یافته
و لازم‌الاجرا دقیقاً هفت Stage مستقل است:

1. `AGENT-S1` — PMCS Intelligence Foundation؛
2. `AGENT-S2` — Read-Only Project Intelligence Agent؛
3. `AGENT-S3` — Knowledge / RAG / Evidence / Citations؛
4. `AGENT-S4` — Executive Intelligence UI؛
5. `AGENT-S5` — Draft Actions؛
6. `AGENT-S6` — Controlled Actions + Permission + Human Approval؛
7. `AGENT-S7` — Evaluation / QA / Security / Hardening.

معماری ثابت و بدون استثنا:

`Agent → Permission-aware Tool → Application Service → Business Rules → Database`

Stage 1 در V1.1 و Stageهای 2 تا 7 در V1.2 نگاشت شده‌اند. پایهٔ Advisory موجود V1 هیچ Stage
جدیدی را خودکار کامل نمی‌کند. شروع Stage 1 نیازمند Gap Analysis و Gate مستقل است. بنابراین Agent
فراموش نشده؛ اجرای آن طبق Dependency Map پس از RPT1، COL1 و UX2 قرار دارد.

## ۷. موارد باز که «جاافتادگی» نیستند

موارد زیر صریحاً باز یا Deferred هستند و نباید به‌عنوان تکمیل‌شده گزارش شوند:

- `VX-G1` تکمیل Screenshot evidence، `VX-G3` تأیید کامل Design System، `VX-G4` مهاجرت و
  `VX-G5` Visual Qualification؛
- RPT1، COL1، UX2، INT1 و QA1 در V1.1؛
- Agent Stageهای 2 تا 7، Reporting/Collaboration Phase 2 در V1.2؛
- PMO، PMBOK، Feasibility، Quantify/BOQ، Scheduling/MSP و Specialist Agents در V2.x؛
- دامنه و Hosting واقعی، TLS/WAF، SMTP/OTP، Secretهای Production، UAT کاربران/دستگاه،
  Endurance هفت‌روزه و Load/Soak محیط Pilot؛
- Multipart بزرگ، Hardening چرخه Object Storage و شواهد عملیاتی Provider واقعی، هرجا Runbook
  Production هنوز آن را باز نگه داشته است.

V1 از نظر Baseline کد Qualified و Locked است؛ این گزاره معادل «محیط Production/Pilot واقعی
مستقر و Sign-off شده» نیست.

## ۸. موارد ردشده که همچنان رد هستند

- Microservices در V1؛
- SQL/Database مستقیم برای Agent یا Reporting؛
- محاسبه Finance/Progress/Schedule/Permission توسط LLM؛
- Chat به‌عنوان منبع حقیقت رسمی؛
- DM، Voice/Video، Story، Status و Feed عمومی؛
- Report Designer آزاد در Phase 1؛
- Dynamic binary/plugin loader شخص ثالث در V1.1؛
- HTML/CSS/JavaScript دلخواه برای Login؛
- کپی دادهٔ عملیاتی، فایل، پیام، Audit یا Sync هنگام ساخت پروژه از روی پروژه دیگر؛
- WBS، Budget، Contract یا HSE اجباری برای Core.

هیچ‌یک از این موارد در کد یا Roadmap فعال بازگردانده نشده‌اند.

## ۹. بازآزمایی Repository در این ممیزی

روی Tree جاری، Suite معماری محلی دوباره اجرا شد و نتیجه زیر را داد:

- Repository validation: پاس؛ ۳۰۵ فایل C# ماژولی؛
- System contract audit: پاس؛ ۲۶۵ Endpoint، ۲۰۱ Mutation و ۵ Mutation پروتکل‌محور مستند؛
- Worktree پیش از ادامه پاک بود؛
- Runtime محلی `dotnet` در دسترس نبود؛ بنابراین این ممیزی ادعای Build جدید C# ندارد و به
  Evidence متصل Run 93/Tree یکسان اتکا می‌کند.

یک ناسازگاری مستندی نیز اصلاح شد: Roadmap Registry نسخهٔ Visual Excellence Program را هنوز
`1.1.0` نمایش می‌داد، در حالی که خود سند حاکم `1.2.0` بود. Registry به `1.2.0` و Roadmap
Post-V1 برای ثبت DoR مرحله RPT1 به `1.8.0` همگام شد. این اصلاح هیچ Runtime یا تصمیم معماری را
تغییر نمی‌دهد.

## ۱۰. جمع‌بندی و نقطه ادامه

هیچ تصمیم معماری یا قابلیت تأییدشدهٔ بازیابی‌شده از Conversations 1–3 بدون مرجع حاکم باقی
نمانده است. تنها نکته‌ای که ممکن بود اشتباه گزارش شود، معنای «UX1 Closed» بود: این عبارت فقط
Gate جهت بصری را می‌بندد، نه کل Visual Excellence Program.

آخرین اقدام تکمیل‌شده: Qualification متصل Slice 06 Micro-Step 02 برای ۲۰ Run سالم + poison، P95
و fairness دو پروژه/دو Worker در Run 113؛ RPT1 همچنان فعال است.

نقطهٔ دقیق ادامه: Operational Observability شامل heartbeat/queue-age/metrics/export/scrape/alert؛
سپس remediation امن orphan، Golden معنایی/XLSX، PDF پس از تصمیم حقوقی license و UI گزارش در UX2.

## ۱۱. Addendum ادامه پس از ممیزی

نسخهٔ اولیهٔ این ممیزی پیش از Runtime نوشته شد؛ جدول وضعیت و جمع‌بندی جاری تا آخرین Addendum
همگام شده‌اند و بندهای زیر زنجیرهٔ زمانی را حفظ می‌کنند. پس از بسته‌شدن DoR، اولین Vertical Slice در Source Candidate
`43cac1b83ac7764fe6005fee108029597091a238` پیاده شد و Evidence آن در
`docs/checkpoints/v1.1-rpt1-slice-01-candidate.md` ثبت شده است. این ادامه نتیجهٔ ممیزی را تغییر
نمی‌دهد: تصمیمی جا نیفتاده است، RPT1 هنوز بسته نشده، Feature flag پیش‌فرض خاموش است و مرحلهٔ بعد
Generated Document/PDF/XLSX و Qualification متصل است.

## ۱۲. Addendum — Source Candidate دوم RPT1

مرحلهٔ بعدی پیش‌بینی‌شده در Addendum قبل، در Source Candidate
`1cb7e2856e72f8cdf51a6b08e24cc0190f9313b5` با tree
`6c7f0714490066ac1259e05e9e6af6da1d3067af` ادامه یافت. Generated Document، PDF/XLSX adapter،
Worker finalization، Retry/Cancel، Download/Verify، Migration 43 و هارنس connected در Source
وجود دارند. رگرسیون Node/Web و کنترل‌های ساختاری محلی سبز است، اما C# و PostgreSQL/Object Storage
در این محیط اجرا نشده‌اند و PDF license/golden نیز باز است.

این ادامه هیچ تصمیم Blueprint/Roadmap یا ترتیب Agent را تغییر نمی‌دهد. RPT1 همچنان مرحلهٔ فعال است؛
COL1، UX2، INT1 و هفت Stage مستقل Agent در جای مصوب خود باقی مانده‌اند. نقطهٔ دقیق ادامه، اجرای
Candidate موجود در CI متصل، رفع هر خطای Build/Integration، سپس Golden/Recovery/Observability و
Gateهای باقیمانده RPT1 است؛ بازطراحی یا شروع دوبارهٔ Reporting مجاز نیست.

## ۱۳. Addendum — Evidence متصل Candidate دوم RPT1

Candidate پس از چرخهٔ diagnostic Runs 94 تا 98 به source commit
`ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173` و tree
`4deccade8899a2438485fb1304cd918115af2654` رسید. Run 99 (`35381177208`) همان tree را روی PR merge
commit موقت `ebbe1090f42cf6bd58928d8844a4b0f86e6abcf6` آزمود و هر هشت Job را سبز کرد. Build و
`295/295` تست C#، `13/13` assertion Reporting متصل، Restore Drill ۴۳ Migration، پنج browser
scenario موجود و Qualification artifact هفت-Suite‌ای با صفر failure پاس شدند.

این Addendum نتیجهٔ ممیزی تداوم را تقویت می‌کند و ترتیب Roadmap را تغییر نمی‌دهد. RPT1 هنوز برای
PDF license/golden، crash/concurrency/revocation/tamper/load، observability و UI اختصاصی Reporting
باز است؛ COL1، UX2، INT1 و هر هفت Stage Agent در ترتیب و مرز مصوب باقی مانده‌اند.

## ۱۴. Addendum — Recovery و Extended Security Evidence

Qualification Slice سوم با source commit `b4da1e951debf76e1ba3b398bde2ccf60fbde5de` و tree
`aa4063214ad1dea8fac19685a81818623296c24c` در Run 102 (`35383686315`) هر هشت Job را پاس کرد.
Cancel/replay با Worker خاموش، anonymous/cross-tenant isolation، منع generic Documents، revocation
پس از success و metadata tamper fail-closed/restore به Evidence متصل تبدیل شدند.

این ادامه نه Scope را کوچک کرده و نه ترتیب Agent را تغییر داده است. Gateهای worker-time revocation،
دو Worker/crash، object-byte tamper، load، observability، Golden PDF/XLSX و UI Reporting همچنان
بازند و نقطهٔ ادامه RPT1 هستند.

## ۱۵. Addendum — Two-worker و Crash Recovery Evidence

Qualification Slice چهارم با source commit `e1ac3263df53a245b1aefb338a015be4854d367b` و tree
`f58881f7e0a77bf89f65b872d4f988bd154a809f` در Run 104 (`35390054888`) هر هشت Job را پاس کرد.
دو Worker واقعی، `SKIP LOCKED`، rollback پس از توقف سخت، stale lease و crash-before/after-storage
با Document پایدار و بدون side effect تکراری به Evidence متصل تبدیل شدند.

این Addendum معماری یا ترتیب Roadmap را تغییر نمی‌دهد. RPT1 هنوز برای revocation حین Worker،
object-byte/missing/malformed tamper، orphan inventory، load/observability، Golden، PDF قانونی و UI
Reporting باز است. COL1، UX2، INT1 و هر هفت Stage Agent در ترتیب مصوب باقی مانده‌اند و مرز
`Agent → Permission-aware Tool → Application Service → Business Rules → Database` حفظ شده است.

## ۱۶. Addendum — Worker Revocation، Object Integrity و Orphan Inventory

Qualification Slice پنجم با source commit `167133fc1985c5b57c3dac90535f7a962dfd03b7` و tree
`34fb70aee9a62a434a8446444d7c6d5c6c9819bd` در Run 108 (`35393509764`) هر هشت Job را پاس کرد.
مجوزهای Reporting/Source بلافاصله پیش از Storage دوباره ارزیابی شدند و تعلیق Membership حین
Rendering بدون Document/Output fail-closed شد. byte-tamper، missing و malformed object روی MinIO
واقعی، restore قطعی و Auditهای integrity پاس شدند. inventory نیز یک orphan در crash window و صفر
orphan پس از recovery پایدار را اثبات کرد.

این Addendum معماری، Scope یا ترتیب Roadmap را تغییر نمی‌دهد و inventory را به‌اشتباه sweeper
Production اعلام نمی‌کند. RPT1 برای retry/load/budgets، observability، remediation orphan، Golden،
PDF قانونی و UI Reporting باز است. COL1، UX2، INT1 و هر هفت Stage Agent در ترتیب مصوب باقی
مانده‌اند و مرز
`Agent → Permission-aware Tool → Application Service → Business Rules → Database` حفظ شده است.

## ۱۷. Addendum — Worker Capacity Core Safe Checkpoint

Slice 06 Micro-Step 01 با source commit `d085c44f9ed8b3c085af62de6009fa1dafc9ed8e` و tree
`9f8afd35b54fe7eacd38f202128538cc571c651f` در Run 110 (`35436466233`) هر هشت Job را پاس کرد.
بودجه‌های اجرایی، timeout، terminalization attempt نهایی، fairness پروژه‌محور، telemetry و
heartbeat/queue health بدون تغییر API خارجی یا Migration وارد Core شدند.

این Addendum به‌صراحت Full CI Core را از Connected Capacity Qualification جدا می‌کند. سناریوی
۲۰ Run سالم + poison، P95 و fairness واقعی در `S06-MS02` باقی می‌مانند؛ بنابراین RPT1 هنوز فعال است
و Scope، ترتیب COL1/UX2/INT1، هفت Stage Agent و مرز مصوب Agent تغییر نکرده‌اند.

## ۱۸. Addendum — Connected Capacity، Poison Isolation و Fairness

Slice 06 Micro-Step 02 با source commit `346fbb778aa5c4475fd48df3241b700341e96d83` و tree
`98b25e2dcd109356bdea08de138995f271260cfc` در Run 113 (`35437832281`) هر هشت Job را پاس کرد.
۲۰ Run سالم یک‌بار کامل شدند، poison پس از دو requeue در attempt سوم بدون side effect شکست خورد،
P95 برابر `5.529s` بود و fairness دو پروژه/دو Worker با row lock واقعی و rollback پس از `SIGKILL`
هر `9/9` assertion را پاس کرد.

Run 112 نشان داد خود دو سناریوی جدید سبز بودند و شکست فقط از expectation نمایشی boolean
PostgreSQL (`t` در برابر `true`) بود؛ fix محدود در Candidate نهایی و Full CI بعدی دوباره همهٔ
Regressionها را پاس کرد. RPT1 هنوز برای Operational Observability، remediation orphan، Golden،
PDF قانونی و UI Reporting باز است و Scope، ترتیب COL1/UX2/INT1، هفت Stage Agent و مرز مصوب Agent
تغییر نکرده‌اند.
