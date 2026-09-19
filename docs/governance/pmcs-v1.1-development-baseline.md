# PMCS V1.1 — Development Baseline

- شناسه: `PMCS-GOV-V1.1-BASELINE-001`
- وضعیت: `Architecture Approved`
- تاریخ ثبت: ۱۴۰۵/۰۶/۲۷ (۲۰۲۶-۰۹-۱۸)
- شاخه توسعه: `v1.1-development`

## ۱. مراجع قطعی

| مرجع | مقدار | معنی |
| --- | --- | --- |
| Parent Product Baseline | `PMCS V1` | محصول Qualified/Final/Locked |
| Parent Runtime Commit | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` | آخرین Commit دارای Runtime قفل‌شده |
| Repository Start Commit | `0389b52cbd3385bdcc9f0e2a94411800389ae2fc` | Commit قفل و Evidence که شاخه V1.1 دقیقاً از آن ساخته می‌شود |
| Repository Start Tree | `c0f3f7dfc0295dbd02a20192ad8b56217db579db` | Tree شروع شاخه |
| Qualification Evidence | Run 69 / `35255343431` | Qualification هفت Suite روی Runtime Commit |
| Lock Evidence | Run 70 / `35256341709` | بازآزمایی سبز Commit قفل Repository |

`Repository Start Commit` یک Commit جلوتر از Runtime Parent است، اما فقط Release evidence، مستندات و Repository validation را تغییر داده و Runtime محصول را تغییر نداده است. بنابراین:

- مبنای Regression و Migration داده: `26bf222d...`؛
- مبنای شاخه و تاریخچه Repository: `0389b52c...`؛
- هیچ‌کدام با عبارت مبهم «آخرین main» جایگزین نمی‌شوند.

## ۲. وضعیت خط توسعه

| مورد | وضعیت |
| --- | --- |
| Product line | `PMCS V1.1` |
| SemVer target | `1.1.0` |
| State | `Development | UX1/EXT1/DOC1/IAM1/PRJ1 Closed | RPT1 Active` |
| Product runtime implementation | RPT1 Slice 01–05 متصل پاس؛ Slice 06 MS03-C1 تا source commit `83f13cf` با signal/connected queue-age health پاس و exporter/scrape/alert delivery در MS03-C2 باز |
| Database migration | ۴۳ Migration؛ Restore Drill متصلِ ۴۳ Migration در Run 108 پاس شده است |
| V1 maintenance line | مستقل و بدون Feature جدید |
| Visual direction | `مدیریت ممتاز` — Approved |

Governance source commit `d4ac64ea818c7e48476b650b84dafe31bf1872a4` در Run 71 (`35275795712`) با هفت Suite و Qualification Report سبز تأیید شد. این Approval فقط G0 را می‌بندد و هیچ Runtime/Migration جدیدی را جزو Baseline قفل‌شده V1 نمی‌کند.

PRJ1 با Candidate source commit `e1b5bf6af813af7324065edc1c91eecf2391eccd` و Run 92 (`35355855215`) در هر هشت Job تأیید شد. ۲۸۴ تست C#، ۱۳۹ تست Web، پنج سناریوی مرورگر واقعی، connected bootstrap regression و Restore Drill دارای ۴۱ Migration پاس شدند. این Evidence فقط Checkpoint توسعه PRJ1 را می‌بندد؛ PMCS V1.1 هنوز Feature Complete، Qualified، Final یا Locked نیست و مرحله فعال `V1.1-RPT1` است.

Definition of Ready مرحله RPT1 با شناسه `PMCS-V1.1-RPT1-DOR1` روی source commit جاری
ثبت شد. این بسته ADR، معماری، API، Permission/Threat، Test Matrix و Runbook را قفل می‌کند، اما
هیچ تغییر Runtime یا Migration ایجاد نمی‌کند و Gate خروج RPT1 همچنان باز است.

اولین Slice پیاده‌سازی RPT1 با source commit
`43cac1b83ac7764fe6005fee108029597091a238` و tree
`257d8c80f45435e462563e38bb3c5fa12c77808b` ثبت شد. Migration 42، Reporting core، Source
contract، API اولیه، Snapshot worker و Agent read-only manifests/application service در Source
وجود دارند، اما چون Build/CI متصل، PostgreSQL/Restore و Rendererهای PDF/XLSX هنوز Evidence ندارند،
این Commit Baseline تأییدشده یا Checkpoint بسته محسوب نمی‌شود. Feature flag پیش‌فرض خاموش است.

Slice 02 با source commit `ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173` و tree
`4deccade8899a2438485fb1304cd918115af2654` مسیر Generated Document، Rendererهای PDF/XLSX،
`Rendering → Succeeded`، Retry/Cancel، Download/Verify، Migration 43 و هارنس connected QA را در
Source اضافه کرد. ابزارها `40/40`، Web `139/139`، Web check، Repository validator و system contract
audit محلی پاس شدند. Run 99 (`35381177208`) همان tree را با هر هشت Job سبز آزمود: `295/295` تست
C#، `13/13` assertion Reporting روی PostgreSQL/Object Storage، پنج browser scenario موجود و
Restore Drill ۴۳ Migration پاس شدند. این نتیجه PDF license/golden، crash/concurrency/load، coverage
کامل Permission revocation/tamper، observability و UI اختصاصی Reporting را نمی‌بندد. بنابراین RPT1
همچنان Active است و این source commit Baseline قفل‌شده یا Checkpoint بسته نیست. هر سه switch Phase
1، output access و Worker به‌طور پیش‌فرض خاموش‌اند.

Qualification Slice 03 با source commit `b4da1e951debf76e1ba3b398bde2ccf60fbde5de` و tree
`aa4063214ad1dea8fac19685a81818623296c24c` هیچ API/Domain/Migration جدیدی ایجاد نکرد؛ هارنس و
orchestration متصل را برای Cancel با Worker خاموش، replay/final-state، anonymous/cross-tenant،
generic Documents isolation، Membership suspension پس از success و metadata tamper fail-closed
گسترش داد. Run 102 (`35383686315`) هر هشت Job، ۱۲ assertion جدید، Restore ۴۳ Migration و
Qualification report را پاس کرد. Gateهای worker-time revocation، دو Worker/crash، object-byte
tamper، load، observability، Golden و UI همچنان بازند؛ پس RPT1 Active باقی می‌ماند.

Qualification Slice 04 با source commit `e1ac3263df53a245b1aefb338a015be4854d367b` و tree
`f58881f7e0a77bf89f65b872d4f988bd154a809f` نیز API/Domain/Migration را تغییر نداد؛ QA-only
pauseهای fail-closed، Worker identity و orchestration دو Process واقعی را برای `SKIP LOCKED`،
stale lease و crash-before/after-storage اضافه کرد. Run 104 (`35390054888`) هر هشت Job،
`298/298` تست C#، recovery preparation/final هر `5/5`، orchestration هر `15/15`، Restore ۴۳
Migration، پنج browser scenario و `139/139` تست Web را پاس کرد. revocation حین Worker، object
tamper/orphan inventory، load/observability، Golden/PDF و UI بازند؛ RPT1 Active می‌ماند.

Qualification Slice 05 با source commit `167133fc1985c5b57c3dac90535f7a962dfd03b7` و tree
`34fb70aee9a62a434a8446444d7c6d5c6c9819bd` recheck مجوز بلافاصله پیش از Storage، processing
permission snapshot ردشده، object-byte/missing/malformed integrity و inventory orphan را اضافه کرد؛
API خارجی و Migration تغییر نکرد. Run 108 (`35393509764`) هر هشت Job، `302/302` تست C#، object
security و worker revocation هر `8/8`، recovery با `17/17` assertion، Restore ۴۳ Migration، پنج
browser scenario و `139/139` تست Web را پاس کرد. sweeper تولیدی، retry/load/budgets، observability،
Golden/PDF و UI بازند؛ RPT1 Active می‌ماند.

Slice 06 Micro-Step 01 با source commit `d085c44f9ed8b3c085af62de6009fa1dafc9ed8e` و tree
`9f8afd35b54fe7eacd38f202128538cc571c651f` بودجه‌های bounded، timeout، terminalization صریح
attempt نهایی، scheduling پروژه‌محور، telemetry، heartbeat/queue health و failure injection محدود به
QA را اضافه کرد؛ API خارجی و Migration تغییر نکرد. Run 110 (`35436466233`) هر هشت Job،
`311/311` تست C#، Restore ۴۳ Migration، پنج browser scenario و `139/139` تست Web را پاس کرد.
این Evidence هنوز orchestration اختصاصی ۲۰ Run سالم + poison و fairness متصل را اجرا نکرده است؛
آن Gate دقیقاً در `PMCS-V1.1-RPT1-S06-MS02` باز می‌ماند و RPT1 Active است.

Slice 06 Micro-Step 02 با source commit `346fbb778aa5c4475fd48df3241b700341e96d83` و tree
`98b25e2dcd109356bdea08de138995f271260cfc` orchestration اختصاصی Capacity و Fairness را بدون
تغییر API خارجی یا Migration اضافه کرد. Run 113 (`35437832281`) هر هشت Job، `311/311` تست C#،
`45/45` تست قراردادی، `139/139` تست Web، پنج browser scenario و Restore ۴۳ Migration را پاس کرد.
۲۰ Run سالم یک‌بار کامل شدند، poison در attempt سوم بدون side effect نهایی شد، P95 برابر `5.529s`
بود و fairness دو پروژه/دو Worker هر `9/9` assertion را پاس کرد. RPT1 برای Observability عملیاتی،
remediation orphan، Golden، PDF قانونی و UI Reporting همچنان Active است.

Slice 06 Micro-Step 03 Checkpoint C1 با source commit
`83f13cf43679b23a6a169cc0912985b391b1c017` و tree
`1705d184bd494e80e50d8a85b723f0bc63e20abc` قرارداد کم‌کاردینالیتی نه Meter instrument و readiness
عددی محدود `reporting-worker` را بدون تغییر endpoint تجاری `/api/v1` یا Migration اضافه کرد. Run
117 (`35441980440`) هر هشت Job، `313/313` تست C#، `46/46` تست قراردادی، `139/139` تست Web، پنج
browser scenario و Restore ۴۳ Migration را پاس کرد. fairness اکنون `10/10` assertion دارد و
`Degraded` صف aged و عدم نشت Tenant/Project/User/Run ID را متصل اثبات می‌کند. exporter/scrape و
alert delivery هنوز در C2 همین MS03 بازند؛ RPT1 Active باقی می‌ماند.

## ۳. قرارداد شاخه و ادغام

- شاخه `v1.1-development` فقط از SHA دقیق بالا ایجاد می‌شود؛
- قابلیت‌ها در شاخه‌های کوتاه‌عمر از Integration Baseline همین خط ساخته می‌شوند؛
- هیچ Commit قابلیت V1.1 مستقیماً روی Locked Runtime یا Maintenance line ثبت نمی‌شود؛
- Integration فقط با PR، expected head SHA و CI یکسان انجام می‌شود؛
- Rebase/force-push روی Commitهای Evidence، Checkpoint یا Baseline ممنوع است؛
- Main تا زمان Qualification نسخه V1.1 مرجع V1 قفل‌شده باقی می‌ماند؛
- Merge نهایی V1.1 فقط بعد از `Qualified → Final → Baseline Locked` مجاز است.

## ۴. Gateهای قبل از Runtime

اولین تغییر Runtime فقط پس از تکمیل هم‌زمان موارد زیر مجاز است:

1. ثبت Scope و Non-Scope؛
2. پذیرش ADRهای 0027 و 0028؛
3. Permission Catalog اولیه؛
4. API/Event/Migration/Offline strategy؛
5. Risk Register و Rollback policy؛
6. Test/Qualification Contract؛
7. `VX-G2 Direction Approved`؛
8. Checkpoint `V1.1-G0 Governance Approved` با CI سبز.

Art Direction تصویب شده است، اما `VX-G1 Audit Complete` و `VX-G3 System Ready` هنوز برای مهاجرت UI باز هستند. بستن G0 اجازه آغاز Foundationهای غیرنمایشی را می‌دهد؛ مهاجرت رابط تولیدی بدون Gateهای Visual مربوط مجاز نیست.

## ۵. قاعده بازگشت

- Rollback خط توسعه با بازگشت Branch/Deployment به `0389b52c...` انجام می‌شود؛
- Rollback Runtime به Artifact متناظر `26bf222d...` متکی است؛
- Migration جدید باید forward-only و سازگار با rollback Runtime باشد؛
- هیچ rollback مجاز نیست دادهٔ جدید را حذف کند؛
- هر Candidate باید Upgrade از Locked Baseline و Restore Drill مستقل را اجرا کند.
