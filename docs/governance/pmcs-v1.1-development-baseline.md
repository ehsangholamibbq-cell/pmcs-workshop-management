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
| Product runtime implementation | F01، F02 و F03 متصل و واجد Safe Checkpoint؛ F04 Runtime Core checkpointed و Renderer/Wiring باز؛ F05–F10/UI باز |
| Database migration | ۴۵ Migration در Safe Resume و Restore Drill متصل سبز است |
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

Slice 06 Micro-Step 03 Checkpoint C2 با source commit
`9bb7ede9b89da2078e165cccb2927e0449116909` و tree
`a960cddb5264b3de8857812906b7595db0664ba5` exporter اختیاری OTLP را فقط در composition، Collector
و Prometheus/Alertmanager نسخه‌پین‌شده و سه rule عملیاتی را بدون تغییر API تجاری یا Migration اضافه
کرد. Run 120 (`35443563270`) هر هشت Job، `321/321` تست C#، `48/48` تست قراردادی، `139/139` تست
Web، پنج browser scenario و Restore ۴۳ Migration را پاس کرد. alert queue-age واقعاً firing و به
webhook ایزوله تحویل شد و payload metric/alert هیچ Tenant/Project/User/Run ID نداشت. MS03 بسته است؛
remediation orphan، Golden معنایی/XLSX، PDF قانونی و UI Reporting بازند و RPT1 Active می‌ماند.

Slice 06 Micro-Step 04 با source commit
`4ff44c96104ee1df87d267ca9a530d19b9248ba3` و tree
`d4c320e7917121f64a70dea1251169bef4b516ce` worker داخلی و default-off remediation را بدون endpoint
تجاری یا Migration جدید اضافه کرد. Run 123 (`35445497353`) هر هشت Job، `328/328` تست C#،
`50/50` تست قراردادی، `139/139` تست Web، پنج browser scenario و Restore ۴۳ Migration را پاس کرد.
dry-run هر چهار fixture را حفظ کرد و apply فقط orphan منقضی و بدون hold را حذف کرد؛ retention،
legal hold و owner موجود محفوظ ماندند، object واقعی MinIO بررسی شد، Audit یکتا و بدون object key بود
و sweep دوم idempotent ماند. MS04 بسته است؛ Golden معنایی/XLSX، PDF قانونی و UI Reporting بازند و
RPT1 Active می‌ماند.

Slice 06 Micro-Step 05 با source commit
`38a03f33f4747d0b6a76696705877633acd17678` و tree
`eb9369c9e32eb3f523c4faa22487d6428c2d7e34` semantic projection تاریخی و Golden مستقل XLSX را
بدون endpoint تجاری یا Migration جدید اضافه کرد. Run 130 (`35449387794`) هر هشت Job، `329/329`
تست C#، `51/51` تست قراردادی، `139/139` تست Web، پنج browser scenario و Restore ۴۳ Migration را
پاس کرد. چهار Run twin، cutoff پیش/پس از correction، Snapshot/manifest hash، replay byte-identical،
OpenXML امن، ۸/۱۶ ردیف semantic و حذف Draft هر `13/13` assertion را پاس کردند. MS05 بسته است؛
تصمیم قانونی و Golden/Performance PDF و UI Reporting بازند و RPT1 Active می‌ماند.

Slice 06 Micro-Step 06 با source commit
`b8f21492a4f44c7c412e5b7eda0b164e7f256758` و tree
`e94b6ba3753e67b42ea0ec99e998761fdad0bcc3` تصمیم Community و قرارداد pin‌شدهٔ PDF را بدون endpoint
تجاری یا Migration جدید اضافه کرد. Run 133 (`35463350892`) هر هشت Job، `330/330` تست C#،
`52/52` تست قراردادی، `139/139` تست Web، پنج browser scenario و Restore ۴۳ Migration را پاس کرد.
PDF Golden متصل `8/8` assertion، رندر byte-identical، visual digest و performance budget را پاس کرد.
MS06 بسته است؛ اختلاف کاتالوگ ده‌گانه و UI Reporting بازند و RPT1 Active می‌ماند. هیچ default
Production یا Baseline قفل‌شدهٔ V1 تغییر نکرد.

Slice 07 Micro-Step 01 با source commit
`d81ecc00762145210e1c688f8f5843f46d62fc04` و tree
`5f40383ad506d94520c741eb69fcd00086283734` تصمیم صریح مالک محصول را در ADR 0031 ثبت کرد: Scope
هر ده خانواده کاتالوگ RPT1 حفظ می‌شود و F02 تا F10 با Micro-Slice و Qualification مستقل الزامی‌اند.
Run 135 (`35466775368`) هر هشت Job، `330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web
و پنج browser scenario را پاس کرد. این Decision Record هیچ API، Migration، Runtime یا default
Production را تغییر نداد؛ F02 تا F10 همچنان Not Implemented و RPT1 Active هستند.

Slice 07 Micro-Step 02 با source commit
`b4a59fa966320a1da4b53759814224e21893c01e` و tree
`6b5b486dace3c07b0b4e0385413bf1add5aee7a3` قرارداد
`PMCS-RPT1-F02-SEMANTIC-001 v1.0.0` را برای period/cutoff، source lineage، status،
permission/classification و Golden matrix گزارش هفتگی/ماهانه ثبت کرد. Run 137 (`35474388839`) هر
هشت Job، `330/330` تست C#، `56/56` تست قراردادی Node، `139/139` تست Web، پنج browser scenario و
Restore ۴۳ Migration را پاس کرد. هیچ Runtime، API، Migration، Renderer یا default Production تغییر
نکرد؛ F02 اکنون Contract Ready ولی Runtime Not Implemented و RPT1 Active است.

Slice 07 Micro-Step 03، Runtime Core محدود F02 را روی همان Checkpoint بست:
Definition/schema identity نسخه‌دار، Project configuration pin، period-read Application Contract در
FieldOperations، resolver مرز هفتگی/ماه شمسی و semantic Snapshot builder. Source commit
`6fc28cf54a6df820c49a2365eab76e3550ae421a` با tree
`5188dac79fe5187b319e6aa727da89163fa37c1b` در Run 139 (`35477179493`) هر هشت Job، `346/346`
تست C#، `58/58` تست Node، `139/139` تست Web، پنج browser scenario و Restore کامل ۴۳ Migration را
پاس کرد. Safe Checkpoint آن `PMCS-V1.1-RPT1-S07-MS03-C1` است. این Slice هیچ API، Migration،
Catalog/Template seed، Worker dispatch، Renderer، UI یا default Production را تغییر نمی‌دهد.

Slice 07 Micro-Step 04 از Checkpoint
`d8fd4398309b08d1cdc90e26140c4581dc476636` و Run 140 شروع شد و فقط قرارداد Renderer مستقل،
render model canonical و PDF/XLSX deterministic خانواده F02 را اضافه کرد. Golden هفتگی، مرز
Monthly، `NoData/NotConfigured`، formula escaping، unit separation، hash/visual digest و budgetهای
bounded پوشش داده شده‌اند. Source commit `4f68f57de2c2a79b654a19128894d9c89878ab65` با tree
`f4b592c72ea65974c00b936ca59c0428eb47f981` در Run 141 (`35495791821`) هر هشت Job را پاس کرد.
Safe Checkpoint آن `PMCS-V1.1-RPT1-S07-MS04-C1` است. Rendererها در DI/Worker ثبت نشده‌اند و API،
Migration، Catalog seed، UI و default Production تغییر نکرده‌اند؛ F02/RPT1 Done محسوب نمی‌شوند.

Slice 07 Micro-Step 05 روی همین Checkpoint، Migration 44، Catalog/Template seed قطعی F02، strict
API، Project profile pin، Worker dispatch و QA متصل PDF/XLSX را اضافه کرد. Source
`7fc55c167ad2159a31c895b32a52d78f47574df9` با tree
`d665fe4cdf29369f96ec0875bc6f1535db349d55` در Run 144 (`35498990050`) هر هشت Job، `355/355`
تست C#، `61/61` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F02 برابر `13/13` و
Restore کامل ۴۴ Migration را پاس کرد. Safe Checkpoint آن `PMCS-V1.1-RPT1-S07-MS05-C1` است؛ UI و
defaultهای Production تغییر نکرده‌اند و F03 تا F10 همچنان باز هستند.

Slice 07 Micro-Step 06 قرارداد `PMCS-RPT1-F03-SEMANTIC-001 v1.0.0` را روی Checkpoint
`PMCS-V1.1-RPT1-S07-MS05-C1` تعریف کرد. F03 فقط Snapshot رسمی Project State را تا cutoff، با
partial scope، کیفیت داده، currency، lineage و trend canonical گزارش خواهد کرد؛ Recalculate،
Composite Health، AI summary و join پنهان F04 تا F10 ممنوع‌اند. Candidate
`e3218555a38f7ba460558e51b4db3f8bc17fcd9c` با tree
`1fe4cc804fdd078a71ff2633201c8c690447900e` در Run 146 (`35500809115`) هر هشت Job، `355/355`
تست C#، `63/63` تست قراردادی Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration
را پاس کرد. هیچ Runtime، Migration، API، Catalog seed، Renderer، UI یا default Production تغییر
نکرد؛ F03 اکنون `Contract Ready / Runtime Not Implemented` و RPT1 فعال است.

Slice 07 Micro-Step 07، قرارداد را به `PMCS-RPT1-F03-SEMANTIC-001 v1.1.1` ارتقا داد و فقط Runtime
Core داخلی شامل identity/schema نسخه‌دار، Project profile pin، Contract خواندنی
ProjectIntelligence، selector cutoff-aware و semantic Snapshot builder را اضافه کرد. Source
`22d5b0f91edf8d733192fae0ba946c8538c63bca` با tree
`eb5ea4253a7b80e8ab3320b9ccea747624f89790` در Run 148 (`35507127968`) هر هشت Job، `377/377`
تست C#، `64/64` تست قراردادی Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration
را پاس کرد. Safe Checkpoint آن `PMCS-V1.1-RPT1-S07-MS07-C1` است. هیچ endpoint، Migration،
Catalog/Template seed، Worker dispatch، Renderer، UI یا default Production تغییر نکرد؛ F03 هنوز
End-to-End Done نیست و RPT1 فعال است.

Slice 07 Micro-Step 08 فقط Renderer contract مستقل، render model canonical و PDF/XLSX قطعی F03 را
روی Runtime Core MS07 اضافه کرد. Source `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f` با tree
`58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434` در Run 154 (`35512969648`) هر هشت Job، `383/383`
تست C#، `65/65` تست قراردادی Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration
را پاس کرد. Safe Checkpoint آن `PMCS-V1.1-RPT1-S07-MS08-C1` است. هیچ endpoint، Migration،
Catalog/Template seed، Worker dispatch، DI registration، UI یا default Production تغییر نکرد؛ F03
هنوز End-to-End Done نیست و RPT1 فعال است.

Slice 07 Micro-Step 09 از Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS08-C1` شروع شده و فقط
Migration forward شمارهٔ 45، Definition/Template seed ثابت F03، پارامتر دقیقاً خالی `{}`، pin پروفایل
پروژه، مجوز منبع `project-state.read` به‌صورت definition-aware، Worker snapshot/renderer dispatch و
هارنس متصل PDF/XLSX را اضافه می‌کند. این تغییر هیچ UI یا Production enablement ندارد و defaultهای
`Phase1Enabled/OutputAccessEnabled/WorkerEnabled=false` و `PdfLicense=Unconfigured` را تغییر نمی‌دهد.
Source `40afeb37d7bf90e97a988cae141901e28d336516` با tree
`ae06285bf1a68fe2592dacc76c7d31cb291ab924` در Run 156 (`35515989200`) هر هشت Job، `387/387`
تست C#، `66/66` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F03 برابر `14/14`،
Restore ۴۵ Migration و Qualification `7/7` را پاس کرد. Safe Checkpoint آن
`PMCS-V1.1-RPT1-S07-MS09-C1` است؛ گام بعد فقط F04 و Safe Resume اکنون MS09 است.

Slice 07 Micro-Step 10 قرارداد `PMCS-RPT1-F04-SEMANTIC-001 v1.0.0` را روی Checkpoint
`PMCS-V1.1-RPT1-S07-MS09-C1` تعریف کرد. F04 فقط یک Baseline رسمی مؤثر، Actual تأییدشده، Planned
قطعی، Variance برابر `Actual - Planned` و S-Curve حداکثر ۳۶۶ نقطه را تا cutoff گزارش خواهد کرد؛
Forecast/EVM، Composite Health و join پنهان F05 تا F10 ممنوع‌اند. قرارداد شکاف lifecycle مستقل
Approval/Supersede، target snapshot و configuration تاریخی Runtime فعلی را نیز صریح ثبت می‌کند.
Candidate `f8829027c2ce073c207cd0e04a49c306b546c6a1` با tree
`2b784f135894092ef55bf7c7df201b1f03e0c77f` در Run 158 (`35522512734`) هر هشت Job، `387/387`
تست C#، `68/68` تست قراردادی Node، `139/139` تست Web، پنج browser scenario و Restore ۴۵ Migration
را پاس کرد. هیچ Runtime، Migration، API، Catalog seed، Renderer، UI یا default Production تغییر
نکرد؛ F04 اکنون `Contract Ready / Runtime Not Implemented` و Safe Resume برابر `S07-MS10` است.

Slice 07 Micro-Step 11 قرارداد را به `PMCS-RPT1-F04-SEMANTIC-001 v1.1.1` ارتقا داد و فقط Runtime
Core داخلی شامل identity/schema نسخه‌دار، Contractهای خواندنی و cutoff-aware در FieldOperations و
Planning، selector lifecycle، calculator و semantic Snapshot builder را اضافه کرد. Source
`deb1571ec66d820868e8f4b77b631471e3c8207c` با tree
`9e41495a357480af03f1555ef640962ab863d332` و PR validation merge
`f0d3a5550d9bd1c10d8ddd5a3c0ada24eb0fead5` دارای همان tree، در Run 163 (`35527577826`) هر هشت
Job، `412/412` تست C# شامل `25/25` case متمرکز F04، `69/69` تست Node، `139/139` تست Web، پنج
browser scenario، validator روی `378` فایل، audit `274/204/5` و Restore ۴۵ Migration را پاس کرد.
Safe Checkpoint آن `PMCS-V1.1-RPT1-S07-MS11-C1` است. هیچ Migration، API، Catalog/Template seed،
Worker dispatch، Renderer، UI یا default Production تغییر نکرد؛ گام بعد فقط Renderer/Golden F04 و
Safe Resume اکنون MS11 است.

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
