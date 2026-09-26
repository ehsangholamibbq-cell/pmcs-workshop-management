# PMCS V1.1 — قرارداد معنایی گزارش پیشرفت و S-Curve

- شناسه: `PMCS-RPT1-F04-SEMANTIC-001`
- نسخه: `1.3.1`
- خانواده: `RPT1-F04`
- وضعیت: `Connected Safe Checkpoint | UI/Production Disabled`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS12-C1`
- Runtime change: `Catalog/API/Worker pipeline checkpointed in Run 169`
- Migration / API / Catalog / Worker change: `Migration 46 and connected qualification`

## ۱. هدف و مرز خانواده

F04 یک Snapshot قابل ممیزی از پیشرفت فیزیکی رسمی همان پروژه در یک cutoff مشخص است. این خانواده،
Baseline مؤثر و سازگار را انتخاب می‌کند، Actual رسمی را فقط از شواهد تأییدشده می‌گیرد و در صورت وجود
Schedule معتبر، Planned، Variance و S-Curve قطعی می‌سازد. درصدهای این گزارش فقط درصد فیزیکی در scope
Baseline منتخب‌اند و به‌معنی سلامت کل پروژه، پیشرفت مالی، Earned Value یا تحقق قراردادی نیستند.

F04 برنامه را ویرایش، Rebaseline، Reschedule یا Forecast نمی‌کند. دادهٔ Finance/Commercial، Cash،
Budget، Procurement، Technical، Quality/HSE، Risk/Decision/Action، Project State مدیریتی و Portfolio
به این Snapshot join نمی‌شود. SPI/CPI، EV/PV/AC، Critical Path، Float، delay attribution، productivity،
تبدیل واحد/ارز، پیش‌بینی تاریخ پایان، خلاصهٔ AI و تفسیر علت انحراف نیز خارج از قرارداد هستند.

نبود WBS یا Schedule یک وضعیت معتبر است. Actualهای رسمی مستقل از WBS همچنان در منبع باقی می‌مانند،
اما درصد کل، Planned، Variance و Curve فقط با مبنای رسمی لازم تولید می‌شوند. null هرگز صفر، «طبق
برنامه» یا وضعیت سبز ساخته نمی‌شود.

## ۲. پارامترهای canonical و evidence سروری

پارامتر معنایی Client دقیقاً یک object خالی `{}` است. `projectId` از route و scope معتبر و
`sourceCutoffUtc` از `asOfUtc` پین‌شدهٔ Run می‌آیند. هر property، از جمله `baselineId`،
`measurementItemId`، تاریخ وضعیت، بازه Curve، interval، Planning Mode، include flag، forecast flag،
Query، SQL یا Source selector باید به‌صورت strict رد شود. Client نمی‌تواند Baseline مطلوب خود را
انتخاب کند یا cutoff محلی را override کند.

Server هنگام پذیرش Run این evidence را پین می‌کند و Client حق ارسال آن را ندارد:

- Tenant/Project و Project code/name/revision جاری برای هویت خروجی؛
- Time Zone معتبر پروژه، زمان پذیرش Run و `sourceCutoffUtc` نرمال‌شده به UTC؛
- `cutoffLocalDate` حاصل از Time Zone پروژه؛
- نسخهٔ policy انتخاب configuration/baseline/evidence و policy نمونه‌برداری Curve؛
- permissionها و Classification موردنیاز Definition؛
- Project profile capture time برای تشخیص تغییر هویت یا Time Zone پس از پذیرش.

`asOfUtc` آینده نامعتبر است. Time Zone ناشناخته یا conversion نامعتبر fail-closed است و fallback
حدسی به UTC مجاز نیست. هویت جاری پروژه برای نمایش pin می‌شود، اما محاسبهٔ رسمی باید Planning Mode،
Calendar و Baseline مؤثر در cutoff را از Source نسخه‌دار بگیرد؛ profile جاری جای تاریخچهٔ برنامه‌ریزی
را نمی‌گیرد.

Runtime identity داخلی `project-progress-certified/1.0.0`، parameter schema
`pmcs.reporting.project-progress.parameters/v1`، Snapshot schema
`pmcs.reporting.project-progress.snapshot/v1` و Project profile pin
`pmcs.reporting.project-progress.project-profile/v1` در Safe Checkpoint محدود `S07-MS11` تثبیت شده‌اند.
Source contractهای `pmcs.planning.project-progress-reporting/v1` و
`pmcs.field-operations.progress-evidence-reporting/v1` نیز نسخه‌دارند. Checkpoint `S07-MS12` Template
`1.0.0` با content digest
`3f19d880a7790854fcc0d79d4822c5653cb6bb888294eadf8eaeeee8b5857816`، Renderer
`pmcs.reporting.project-progress.renderer/v1` و Layout
`pmcs.reporting.project-progress.layout/v1` را تثبیت کرده است. Checkpoint `S07-MS13` نیز Migration
forward شمارهٔ 46، Catalog/Template seed، strict API، Project profile pin، policy سه-Permissionی و
Worker/Renderer dispatch را روی همین identityها متصل کرده است.

## ۳. Source lineage و مرز ماژولی

Runtime Core checkpointed F04 فقط دو Application Contract خواندنی را مصرف می‌کند:

1. Projects برای هویت جاری، lifecycle، revision و Time Zone پین‌شده؛
2. Planning برای read model نسخه‌دار، cutoff-aware و classification-aware پیشرفت رسمی.

Planning مالک انتخاب configuration/baseline و محاسبهٔ پیشرفت می‌ماند و در داخل مرز خود از Contract
نسخه‌دار FieldOperations برای زنجیرهٔ رسمی Daily Report استفاده می‌کند. Reporting حق import از
`PlanningDbContext` یا `FieldOperationsDbContext`، اجرای SQL روی schema آن‌ها، فراخوانی HTTP به
`GET /planning/progress`، یا مصرف مستقیم `IProgressFactSource` را ندارد. endpoint جاری
`GET /planning/progress` یک read model زنده است و Source گزارش Certified تاریخی محسوب نمی‌شود.

Application Contract نسخه‌دار Planning این evidence را در projection صریح برمی‌گرداند:

- contract version، Tenant/Project، cutoff و Classification صریح؛
- Planning Mode و Project configuration revision مؤثر در cutoff، همراه بازهٔ اثر آن؛
- Calendar state/mask و revision پین‌شدهٔ مبنای محاسبه؛
- Baseline identity/version/kind، `approvedAt` و `supersededAt` مستقل و تمام entryهای canonical؛
- snapshot غیرقابل‌تغییر code/title/unit/target قلم اندازه‌گیری در زمان تصویب Baseline؛
- زنجیرهٔ نسخه‌های رسمی Daily Report تا cutoff، فقط Factهای `WorkProgress` و lineage لازم؛
- زنجیرهٔ نسخه‌های Milestone Update با `approvedAt/supersededAt/statusDate` مستقل؛
- شمار شواهد Approved خارج از Baseline، بدون تخصیص اجباری؛
- `sourceMaxChangedAt`، source manifest مرتب‌شده و SHA-256 آن.

Draft، Submitted، Returned و مقدار Provisional وارد Actual رسمی نمی‌شوند. متن Narrative، Note،
Review Comment، Evidence Reference، نام reviewer و محتوای سایر Factها نیز وارد Snapshot F04
نمی‌شوند. Source IDها فقط به‌اندازهٔ lineage ممیزی در semantic manifest نگه داشته می‌شوند و نباید در
filename یا diagnostic نشت کنند.

## ۴. انتخاب configuration و Baseline مؤثر در cutoff

Planning configuration واجد شرایط باید نسخه‌ای باشد که در `sourceCutoffUtc` مؤثر بوده است. استفاده از
Planning Mode یا Calendar جاری برای بازسازی cutoff قدیمی ممنوع است. سپس Baseline رسمی باید هم‌زمان
این شروط را داشته باشد:

1. Tenant/Project آن دقیقاً با Run منطبق باشد؛
2. `approvedAt <= sourceCutoffUtc`؛
3. `supersededAt` نداشته باشد یا `sourceCutoffUtc < supersededAt` باشد؛
4. kind آن با Planning Mode مؤثر در cutoff مطابق جدول ADR 0016 باشد؛
5. تمام entryها، target snapshotها، وزن‌ها و lifecycle lineage آن کامل و معتبر باشند.

برای یک cutoff حداکثر یک Baseline می‌تواند مؤثر باشد. وجود دو Baseline هم‌پوشان، timestamp چرخهٔ عمر
گمشده، نسخهٔ تکراری یا supersession مبهم corruption است و Run را fail-closed می‌کند؛ tie-break
حدسی یا انتخاب جدیدترین `ReviewedAt` مجاز نیست. Baseline تصویب‌شده پس از cutoff حذف می‌شود و
Baselineای که پس از cutoff Supersede شده در همان cutoff قدیمی همچنان مؤثر است.

Curve هرگز دو Baseline را splice نمی‌کند. اگر Baseline جدید در cutoff مؤثر باشد، کل Snapshot و Curve
با همان یک نسخه محاسبه می‌شوند و نسخهٔ قبلی صرفاً در manifest lineage باقی می‌ماند.

## ۵. قرارداد محاسبه Actual، Planned و Variance

entryهای `Summary` وزن و درصد ندارند و وارد aggregate نمی‌شوند. وزن تمام entryهای غیرخلاصه باید
دقیقاً `100.0000` باشد. ترتیب ردیف‌ها `sortOrder → code → entryId` است.

### ۵.۱ Actual رسمی

- برای `QuantityBased`، فقط Factهای `WorkProgress` از نسخهٔ Daily Report رسمیِ مؤثر در
  `sourceCutoffUtc` و با `reportDate <= pointDate` جمع می‌شوند؛
- درصد ردیف برابر `approvedQuantity / pinnedTargetQuantity × 100` و با دو رقم اعشار و
  `MidpointRounding.AwayFromZero` است؛ raw quantity و درصد overrun حفظ می‌شوند؛
- برای aggregate، درصد هر ردیف حداکثر `100` لحاظ می‌شود و سپس مجموع وزن‌دار با دو رقم اعشار و همان
  rounding محاسبه می‌شود؛
- برای `ManualPercent` فقط آخرین Milestone Update رسمیِ مؤثر در cutoff با
  `statusDate <= pointDate` معتبر است؛ ترتیب انتخاب `statusDate → approvedAt → updateId` نزولی و
  ordinal است؛
- اگر حتی یک entry وزن‌دار Actual معتبر نداشته باشد، Actual کل و Variance همان نقطه `null` است؛
  missing به صفر تبدیل نمی‌شود؛
- Fact رسمی بدون Measurement Item یا خارج از Baseline شمارش و افشا می‌شود، اما به entry دیگری نسبت
  داده نمی‌شود و aggregate را پنهانی تغییر نمی‌دهد.

نسخهٔ رسمی Daily Report و Milestone Update یک بار در cutoff انتخاب می‌شود. Actual Curve یک
`status-date curve based on evidence official at source cutoff` است: correctionی که تا cutoff رسمی
شده می‌تواند مقدار تاریخ گزارش قدیمی را در Snapshot جدید اصلاح کند، اما approval بعد از cutoff وارد
نمی‌شود و Run تاریخی قبلی را تغییر نمی‌دهد.

### ۵.۲ Planned رسمی

- `MeasurementWeights` Schedule ندارد؛ Planned، Variance و Curve برای آن `NotConfigured` و `null`
  باقی می‌مانند، هرچند Actual کل می‌تواند `Available` باشد؛
- در `MilestonePlan`، `WbsBaseline` و `ExternalSchedule`، Milestone پیش از `plannedFinish` صفر و در
  روز موعد یا بعد از آن ۱۰۰ درصد برنامه‌ای است؛
- Activity در فاصلهٔ inclusive میان `plannedStart` و `plannedFinish` خطی است؛ درصد ردیف ابتدا با
  چهار رقم اعشار و aggregate با دو رقم اعشار و `AwayFromZero` محاسبه می‌شود؛
- اگر Calendar معتبر پیکربندی شده باشد فقط روزهای کاری mask پین‌شده شمرده می‌شوند؛ در غیر این صورت
  روزهای تقویمی استفاده و basis صریح `CalendarDays` ثبت می‌شود؛
- تعطیلات موردی، lag، constraint، resource leveling و curve توزیع غیریکنواخت چون در Source V1
  وجود ندارند استنتاج نمی‌شوند.

### ۵.۳ Variance

`variancePercent = actualPercent - plannedPercent` فقط وقتی هر دو مقدار موجود باشند. مقدار مثبت یعنی
جلوتر از برنامه و مقدار منفی یعنی عقب‌تر از برنامه در همان scope فیزیکی. Variance درصد-نقطه است، نه
درصد تغییر، تأخیر زمانی، SPI یا forecast. Renderer حق معکوس‌کردن علامت یا جایگزینی null با صفر ندارد.

## ۶. قرارداد S-Curve و نمونه‌برداری

برای Baseline زمان‌دار، horizon از کمینهٔ `earliestPlannedStart` و `cutoffLocalDate` تا بیشینهٔ
`latestPlannedFinish` و `cutoffLocalDate` است. policy نسخهٔ اول این نقطه‌ها را می‌سازد:

1. اگر horizon حداکثر ۳۶۶ روز تقویمی inclusive باشد، هر تاریخ یک نقطه است؛
2. در horizon طولانی‌تر، ۳۶۵ نقطهٔ یکنواخت شامل ابتدا و انتها با offset قطعی
   `floor(i × spanDays / 364)` برای `i=0..364` ساخته می‌شود؛
3. اگر `cutoffLocalDate` در grid نباشد، جداگانه افزوده می‌شود؛ بنابراین حداکثر ۳۶۶ نقطه وجود دارد؛
4. duplicate حذف و نقاط فقط بر اساس تاریخ صعودی مرتب می‌شوند.

Planned برای تمام horizon قابل محاسبه است، چون از Baseline رسمیِ معلوم در cutoff می‌آید. Actual فقط
برای نقطه‌های `pointDate <= cutoffLocalDate` محاسبه می‌شود؛ نقطه‌های آینده Actual و Variance برابر
`null` دارند و خط صاف یا Forecast ساختگی تولید نمی‌شود. هر نقطه `plannedPercent`، `actualPercent`،
`variancePercent` و شمار entryهای فاقد Actual را حمل می‌کند. policy sampling و این‌که Curve
نمونه‌برداری‌شده است باید کنار خروجی نمایش داده شود؛ حذف خاموش نقطه مجاز نیست.

برای `MeasurementWeights` نقطه‌ای ساخته نمی‌شود و `curveStatus=NotConfigured` است. array خالی در کنار
این status به معنی Curve صفر نیست.

## ۷. مدل معنایی خروجی

Snapshot مستقل از PDF/XLSX حداقل این بخش‌ها را دارد:

- هویت پروژه، Time Zone، revision/profile capture و cutoff UTC/local؛
- Planning configuration و Calendar basis مؤثر و نسخه‌دار؛
- Baseline identity/version/kind، lifecycle، source reference کنترل‌شده و hash تعریف؛
- `dataStatus`، statusهای مستقل Actual/Schedule/Curve و reason codeهای canonical؛
- summary در cutoff شامل Actual، Planned، Variance و missing-entry count؛
- ردیف‌های Baseline با weight، pinned target، raw approved quantity/percent و planned/variance؛
- Milestoneهای رسمی با planned date، آخرین status date و درصد تأییدشده، بدون note/evidence text؛
- S-Curve حداکثر ۳۶۶ نقطه‌ای و sampling policy؛
- شمار Factهای رسمی خارج از Baseline و هشدار صریح عدم تخصیص؛
- source manifest hash و semantic hash.

نبود مقدار با null و status/reason صریح نمایش داده می‌شود. Forecast completion field در F04 وجود
ندارد؛ افزودن field null با برچسب Forecast نیز مجاز نیست، چون می‌تواند قابلیت پیاده‌نشده را القا کند.

## ۸. وضعیت داده و reason codeها

`dataStatus` با اولویت زیر تعیین می‌شود:

| اولویت | وضعیت | قرارداد دقیق |
| --- | --- | --- |
| ۱ | `NotConfigured` | Planning reporting policy/feature فعال نیست یا Planning Mode مؤثر `None` است |
| ۲ | `NoData` | Planning پیکربندی است ولی هیچ Baseline رسمیِ واجد شرایط تا cutoff وجود ندارد |
| ۳ | `InsufficientData` | Baseline وجود دارد اما mode ناسازگار است یا Actual رسمی در cutoff غایب/ناقص است |
| ۴ | `Available` | Baseline و lineage معتبر و Actual همهٔ entryهای وزن‌دار در cutoff کامل است |

`MeasurementWeights` با Actual کامل می‌تواند `dataStatus=Available` داشته باشد، درحالی‌که
`scheduleStatus/curveStatus=NotConfigured` هستند. Planned معتبر همراه Actual ناقص نیز حفظ می‌شود، اما
dataStatus کلی `InsufficientData` و Actual/Variance کل null می‌ماند.

reason codeهای allowlist عبارت‌اند از `ProgressReportingNotConfigured`، `PlanningModeNone`،
`OfficialBaselineMissing`، `PlanningModeBaselineMismatch`، `OfficialActualMissing`،
`OfficialActualIncomplete`، `ScheduleNotConfiguredForMeasurementWeights`، `CalendarDaysFallback` و
`ApprovedProgressOutsideBaseline`. reasonها بدون تکرار و ordinal مرتب می‌شوند؛ دو مورد آخر می‌توانند
در وضعیت `Available` نیز هشدار اطلاعاتی باشند.

این چهار status failure پردازشی نیستند. Tenant/Project mismatch، lifecycle timestamp گمشده یا
هم‌پوشان، target snapshot گمشده، وزن نامعتبر، Calendar mask نامعتبر، version/classification ناشناخته،
Source contract mismatch، hash mismatch یا collection تکراری باید Run را fail-closed کنند و Artifact
ظاهراً سالم نسازند.

## ۹. Permission، Classification و minimization

- Catalog/View به `reporting.catalog.read` و هر سه Permission منبع `planning.progress.read`،
  `planning.baselines.read` و `planning.milestones.read` در همان Project نیاز دارد؛
- Create/Retry علاوه بر همهٔ Permissionهای Source به `reporting.run.create` نیاز دارد؛
- Download/Verify علاوه بر Source permissionها به `reporting.output.download` و clearance طبقه‌بندی
  جاری نیاز دارد؛
- request، processing و download هر سه Membership، Project lifecycle و تمام Permissionها را دوباره
  ارزیابی می‌کنند؛
- نبود هر Permission کل Definition/Run/Output را deny می‌کند؛ حذف خاموش entry، milestone یا curve
  point مجاز نیست؛
- Permissionهای capture/submit/review یا `projects.planning.configure` برای اجرای read-only F04 لازم
  نیستند و Reporting هیچ Commandی فراخوانی نمی‌کند.

Classification خروجی بیشترین مقدار میان Definition، Project/source configuration، Baseline و تمام
evidenceهای واردشده و حداقل `Internal` است. Source باید Classification را صریح برگرداند؛ unknown یا
downgrade failure امن است. گزارش محتوای Evidence، comment، نام actor و Daily Report narrative را حمل
نمی‌کند و filename/log/event/diagnostic نباید ID منبع یا عنوان حساس را نشت دهد.

## ۱۰. Determinism، hash و budget

semantic JSON و source manifest با property order ثابت، enum case-sensitive، UTC canonical، DateOnly
به ISO، decimal invariant و collectionهای مرتب‌شده تولید می‌شوند. دو Run با Project profile pin،
cutoff و Source یکسان باید semantic/source-manifest hash یکسان داشته باشند؛ query order، DB plan،
Run ID، attempt، build time و render time نباید hash را تغییر دهند. Worker متصل بعد از ساخت Snapshot
آن را برای Retry/Download از Source بازسازی نمی‌کند.

Curve حداکثر ۳۶۶ نقطه و Baseline حداکثر ۵۰۰۰ entry دارد. عبور از budget نسخه‌دار، متن بیش‌ازحد یا
manifest/semantic hash ناسازگار باید non-transient و fail-closed باشد، نه truncate یا تجمیع پنهان.
Renderer مستقل MS12، PDF فارسی/RTL و A4 افقی را در دو بخش قطعی و XLSX را با هشت Sheet ثابت، RTL،
frozen header، ZIP قطعی، سلول عددی واقعی و صفر Formula می‌سازد. row/page/fact budget، visual digest،
performance، formula escaping، Actual آینده، format و identity/hash ناسازگار fail-closed آزموده شده‌اند.
XLSX Golden برابر `8a1866b7bdb3b9cb96d83a1897d80db4584c1590b3727e9ebb6a17856d672fb7` و PDF Golden برابر
`bdc9c3a99c1dc5a0da57f9431d7bc7f04830fbbfbeb578b24c7234df708785ef` است.

## ۱۱. projection نسخه‌دار و مرز سازگاری Persistence موجود

Runtime Core، projection کامل و مستقل lifecycle را به‌صورت Contract/selector پیاده کرده است، اما
Persistence قدیمی فقط در حالت‌هایی به read-through source تبدیل می‌شود که بازسازی تاریخی قابل اثبات
باشد. این محدودیت‌ها همچنان وجود دارند:

- `IProgressFactSource.LoadAsync` cutoff ندارد، Submitted را نیز برمی‌گرداند و lifecycle نسخهٔ رسمی
  Daily Report را حفظ نمی‌کند؛
- `GET /planning/progress` از ساعت جاری، profile/target جاری و Baseline با status جاری استفاده می‌کند؛
- `PlanningBaseline.ReviewedAt` هنگام Supersede بازنویسی می‌شود و `approvedAt` مستقل باقی نمی‌ماند؛
- `MilestoneProgressUpdate.ReviewedAt` نیز زمان Approval و Supersede را جدا نگه نمی‌دارد؛
- Baseline فعلی target/unit/title قلم اندازه‌گیری را هنگام Approval snapshot نمی‌کند؛
- Project history فعلی Planning Mode/Calendar مؤثر در cutoff را از Contract گزارش‌دهی نسخه‌دار ارائه
  نمی‌کند.

Source سازگاری Planning فقط وقتی state جاری را projection می‌کند که آخرین configuration پیش از cutoff
باشد، Baseline یا Milestone superseded با lifecycle قدیمی وجود نداشته باشد و Measurement Item پس از
Approval تغییر نکرده باشد. در غیر این صورت با
`configuration_history.unavailable`، `baseline_history.unavailable`،
`milestone_history.unavailable` یا `target_history.unavailable` fail-closed می‌شود. selector خالص و
نسخه‌دار lifecycle کامل synthetic/آینده را بدون fallback می‌پذیرد و Catalog/API/Worker متصل همین
compatibility producer را بدون ادعای تاریخچهٔ ناموجود مصرف می‌کند. Migration تولیدکنندهٔ تاریخچهٔ
کامل، در صورت نیاز سناریوهای آینده، یک تغییر دامنه‌ای مستقل خواهد بود. استفاده از status یا target
جاری، حدس timestamp از Audit یا اعلام تاریخچهٔ قابل بازسازی بدون projection همچنان ممنوع است.

## ۱۲. Golden matrix الزامی برای Sliceهای بعدی

| ID | Fixture | انتظار قطعی |
| --- | --- | --- |
| `F04-C01` | Baseline قبل و بعد از cutoff | فقط نسخه با `approvedAt <= cutoff < supersededAt` مؤثر است |
| `F04-C02` | Supersede بعد از cutoff | Baseline قبلی در Run تاریخی باقی می‌ماند |
| `F04-C03` | دو Baseline مؤثر هم‌پوشان | processing failure؛ بدون tie-break حدسی |
| `F04-C04` | correction گزارش روزانه قبل/بعد cutoff | فقط نسخه رسمیِ مؤثر در cutoff وارد Actual می‌شود |
| `F04-C05` | Milestone updateهای چند status/approval | ترتیب `statusDate → approvedAt → updateId` قطعی است |
| `F04-N01` | Planning Mode برابر None | `NotConfigured` و بدون درصد یا Curve ساختگی |
| `F04-N02` | Mode پیکربندی و بدون Baseline رسمی | `NoData` با `OfficialBaselineMissing` |
| `F04-I01` | Baseline ناسازگار با Mode مؤثر | `InsufficientData` با `PlanningModeBaselineMismatch` |
| `F04-I02` | Baseline معتبر و بدون Actual رسمی | Planned حفظ؛ Actual/Variance null و `OfficialActualMissing` |
| `F04-I03` | یک entry وزن‌دار بدون Actual | Actual کل null و missing count دقیق، نه صفر |
| `F04-A01` | quantity overrun بالاتر از target | raw percent حفظ و contribution aggregate در ۱۰۰ cap می‌شود |
| `F04-A02` | MeasurementWeights با Actual کامل | `Available`؛ Schedule/Curve `NotConfigured` |
| `F04-P01` | Activity با Calendar کاری | planned خطی فقط روی روزهای mask پین‌شده |
| `F04-P02` | Calendar پیکربندی‌نشده | planned تقویمی با `CalendarDaysFallback` صریح |
| `F04-V01` | Actual بالاتر/پایین‌تر از Planned | Variance دقیقاً `Actual - Planned` با علامت درست |
| `F04-S01` | horizon کوتاه | همه تاریخ‌ها و حداکثر ۳۶۶ نقطه |
| `F04-S02` | horizon چندساله و cutoff خارج grid | grid قطعی ۳۶۵تایی + cutoff، بدون بیش از ۳۶۶ نقطه |
| `F04-S03` | نقاط آینده پس از cutoff | Planned موجود؛ Actual/Variance null و بدون Forecast |
| `F04-X01` | Fact Approved بدون mapping Baseline | شمارش هشدار؛ بدون تخصیص یا تغییر aggregate |
| `F04-D01` | Source یکسان با query order متفاوت و twin Run | semantic/manifest hash و ordering یکسان |
| `F04-PM01` | revoke یکی از سه Permission یا Cross-Tenant ID | deny/fail-closed؛ صفر Source/Output leakage |
| `F04-CL01` | Source با Classification بالاتر یا unknown | propagation بالاتر یا deny؛ هرگز downgrade |

Qualification آینده باید baseline/evidence selection را با query مستقل کنترل، semantic Snapshot را
parse و absence مالی/EVM/forecast/inference را اثبات کند. Golden PDF/XLSX جای Golden معنایی را
نمی‌گیرد.

## ۱۳. Definition of Ready و Slice مجاز بعدی

| Gate | وضعیت |
| --- | --- |
| Scope/Non-Scope و معنای درصد فیزیکی | بسته |
| پارامتر خالی، cutoff و evidence سروری | بسته |
| Source lineage و مرز Projects/Planning/FieldOperations | بسته |
| انتخاب configuration/Baseline correction-safe | بسته |
| Actual/Planned/Variance و sampling حداکثر ۳۶۶ نقطه | بسته |
| Data status، reasonها و failure boundary | بسته |
| Permission/classification/minimization | بسته |
| Golden matrix بیست‌ودوسناریویی | بسته |
| Runtime Definition و parameter/snapshot/profile/source IDs | Checkpointed in Run 163 |
| Template/Renderer identity | Checkpointed in Run 167 |
| Historical projection و Application Contract cutoff-aware | Checkpointed in Run 163؛ legacy gapها fail-closed |
| selector/calculator/semantic Snapshot builder | Checkpointed in Run 163 |
| PDF/XLSX/visual/performance | Checkpointed in Run 167 |
| Catalog/API/Worker wiring | Checkpointed in Run 169 |

Checkpoint `S07-MS11` فقط Runtime identity نسخه‌دار، projection/Contract خواندنی و باریک در Planning
و FieldOperations، selector cutoff-aware، calculator/Snapshot builder و Unit/contract tests را اضافه
کرده است. lifecycle Baseline/configuration/evidence، Actual/Planned/Variance، grid حداکثر ۳۶۶ نقطه،
status/reason، Classification و hash در Core مستقل از Renderer پیاده شده‌اند. Source
`deb1571ec66d820868e8f4b77b631471e3c8207c` با tree
`9e41495a357480af03f1555ef640962ab863d332` و PR validation merge
`f0d3a5550d9bd1c10d8ddd5a3c0ada24eb0fead5` دارای همان tree، در Run 163 (`35527577826`) هر هشت
Job، `412/412` تست C# شامل `25/25` case متمرکز F04، `69/69` تست Node، `139/139` تست Web، پنج
browser scenario، validator روی `378` فایل، audit ثابت `274/204/5`، Restore کامل ۴۵ Migration و
Qualification برابر `7/7` Suite و `12/12` Command را پاس کرد. Renderer، Golden binary،
Catalog/API/Worker wiring، UI و Production enablement در پایان MS11 باز بودند.

Checkpoint `S07-MS12` فقط parser/request/model canonical، Template/Renderer/Layout identity و
PDF/XLSX قطعی را روی Snapshot بالا اضافه کرده است. Source
`6a717f10e4bff167ad7e2643313008f5afcc8264` با tree
`995c7fae108bbb5265faa036f951036d36e7061e` و PR validation merge
`782f42ff73425cf5cad69b0635bacf05790d2ff1` دارای همان tree، در Run 167 (`35532522587`) هر هشت
Job، `418/418` تست C# شامل شش case Renderer/Golden تازه و `31/31` case متمرکز F04، `70/70` تست
Node، `139/139` تست Web، پنج browser scenario، validator روی `381` فایل، audit ثابت `274/204/5`،
Restore کامل ۴۵ Migration و Qualification `7/7` Suite و `12/12` Command را پاس کرد. در پایان MS12،
Registry اختصاصی F04 عمداً به DI/Worker متصل نبود و Catalog/API/Worker wiring، UI و Production
enablement باز بودند.

Checkpoint `S07-MS13` Migration forward شمارهٔ 46 و Definition/Template ثابت F04 را منتشر می‌کند،
پارامتر API را دقیقاً به `{}` محدود و Project profile/cutoff را server-owned pin می‌کند. Catalog،
Run و Output metadata و عملیات Create/Retry/Cancel/Download/Verify فقط با هر سه Permission منبع
قابل دسترسی‌اند؛ `IReportingReadService` و Worker نیز همان policy را fail-closed اعمال می‌کنند.
Worker فقط `IProjectProgressReportingSource`، Snapshot builder و Renderer Registry نسخه‌دار F04 را
dispatch می‌کند و payload را پیش از render دوباره validate می‌کند. Source
`4c48c03aad126a594e5328fc7995a72728ba2274` با tree
`49f957729fdccb0397dd153b93135ce2eaddd68a` و PR validation merge
`05ca8ac7e3fa643e111b9c8511e3e08d62be60a5` دارای همان tree، در Run 169 (`35535904655`) هر هشت
Job، `419/419` تست C#، `71/71` تست Node، `139/139` تست Web، پنج browser scenario، هارنس متصل
F04 برابر `15/15`، validator روی `382` فایل، audit ثابت `274/204/5`، Restore کامل ۴۶ Migration و
Qualification `7/7` Suite و `12/12` Command را پاس کرد. UI و Production enablement باز و defaultها
خاموش/Unconfigured باقی مانده‌اند.

## ۱۴. Gate statement

این نسخه Safe Checkpoint متصل F04 است. Catalog/API/Worker و qualification انتهابه‌انتهای این خانواده
بسته شده‌اند، اما UI/UX2، feature flagها، license و Production setting تغییر نکرده‌اند. Micro-Step
بعدی فقط DoR و قرارداد معنایی مستقل F05 است؛ F05 تا F10 و RPT1 همچنان باز هستند و Evidence فعلی
مجوز Production rollout نیست.
