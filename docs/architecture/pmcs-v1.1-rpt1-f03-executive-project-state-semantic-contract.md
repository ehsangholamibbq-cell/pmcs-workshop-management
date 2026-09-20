# PMCS V1.1 — قرارداد معنایی گزارش مدیریتی / Executive Project State

- شناسه: `PMCS-RPT1-F03-SEMANTIC-001`
- نسخه: `1.3.1`
- خانواده: `RPT1-F03`
- وضعیت: `Connected Candidate | Safe Resume S07-MS08 | Full CI pending`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS08-C1`
- Runtime change: `Catalog/API/Worker wiring implemented on checkpointed Runtime + Renderer`
- Migration / API / Catalog / Worker change: `Candidate; Production defaults unchanged`

## ۱. هدف و مرز خانواده

F03 یک نسخهٔ قابل ممیزی، ثابت و مدیریتی از Project State رسمی همان پروژه در یک cutoff مشخص است.
این گزارش Project State را محاسبه، اصلاح یا تفسیر آزاد نمی‌کند؛ فقط Snapshot immutable تولیدشده توسط
bounded context `ProjectIntelligence` را همراه کیفیت داده، scope محدود، lineage و روند رسمی نمایش
می‌دهد.

این خانواده Dashboard زنده، تحلیل AI، Executive Intelligence Center، گزارش Portfolio یا Composite
Health نیست. F03 دادهٔ مالی، Planned/Actual/Variance، S-Curve، قرارداد/خرید، دفتر فنی، Quality/HSE،
Risk/Decision/Action یا وضعیت جاری disposition را از F04 تا F10 join نمی‌کند. هیچ نتیجه، پیش‌بینی،
علت‌یابی، امتیاز مدیریتی یا خلاصهٔ LLM داخل Snapshot ساخته نمی‌شود.

`Stable` فقط وضعیت عملیاتی در scope رسمی `ApprovedDailyOperations` است. این واژه هرگز به معنی سلامت
مالی، برنامه‌ای، قراردادی، تأمین، Quality، HSE یا کل پروژه نیست و گزارش باید این محدودیت را کنار
وضعیت نمایش دهد.

## ۲. پارامترهای canonical و ورودی‌های سروری

پارامتر معنایی Client دقیقاً یک object خالی `{}` است. `projectId` از route و scope معتبر و
`sourceCutoffUtc` از `asOfUtc` پین‌شدهٔ Run می‌آیند. هر property، از جمله `snapshotId`، تاریخ محلی،
تعداد نقاط روند، status، include flag، Query، SQL یا انتخاب Source باید به‌صورت strict رد شود.

Server هنگام پذیرش Run این evidence را پین می‌کند و Client حق ارسال یا override آن را ندارد:

- Tenant/Project و Project revision جاری؛
- Project code/name و Time Zone معتبر؛
- زمان پذیرش Run و `sourceCutoffUtc` نرمال‌شده به UTC؛
- تاریخ محلی cutoff در Time Zone پروژه؛
- نسخهٔ policy انتخاب Snapshot و ترتیب canonical؛
- permission/classification موردنیاز Definition.

`asOfUtc` آینده نامعتبر است. Time Zone ناشناخته، ناموجود یا دارای conversion نامعتبر fail-closed
است و Server حق fallback حدسی به UTC ندارد. این قرارداد ادعای بازسازی تاریخی Project profile را
نمی‌کند؛ profile جاری هنگام پذیرش جداگانه pin می‌شود و revision آن برای تشخیص outdated بودن Snapshot
استفاده می‌شود.

Runtime identity داخلی `executive-project-state-certified/1.0.0`، parameter schema
`pmcs.reporting.executive-project-state.parameters/v1`، Snapshot schema
`pmcs.reporting.executive-project-state.snapshot/v1` و Project profile pin
`pmcs.reporting.executive-project-state.project-profile/v1` در Safe Checkpoint `S07-MS07`
تخصیص یافته‌اند. Checkpoint `S07-MS08` نیز Template `1.0.0` با content digest
`4bf4f1f5de92eda854ab16702fc87aaebae951eea17ef023569cc338a5ce7d7a`، قرارداد Renderer
`pmcs.reporting.executive-project-state.renderer/v1` و Layout
`pmcs.reporting.executive-project-state.layout/v1` را pin کرده است. Candidate متصل جاری همان identityها
را با Catalog/Template seed و pipeline مشترک منتشر می‌کند و هیچ contract یا digest قبلی را بازنویسی
نمی‌کند.

## ۳. Source lineage و مرز ماژولی

F03 فقط دو Application Contract خواندنی و نسخه‌دار را مصرف می‌کند:

1. Projects، هویت، lifecycle، Time Zone و revision پین‌شدهٔ پروژه را می‌دهد؛
2. ProjectIntelligence، Snapshotهای رسمی Project State و metadata لازم برای currency/classification
   را تا cutoff برمی‌گرداند.

Runtime Core یک Contract باریک reporting-read با نسخه
`pmcs.project-intelligence.project-state-reporting/v1` در `ProjectIntelligence` اضافه می‌کند. import از
`ProjectIntelligenceDbContext`، SQL روی schema ماژول دیگر، فراخوانی HTTP به Command Center، یا
Recalculate کردن Project State از داخل Reporting ممنوع است. Source contract حداقل باید این lineage
را برگرداند:

- `snapshotId`، Tenant/Project، project code/name و classification؛
- `calculationVersion`، `projectConfigurationRevision`، `asOfDate`، `calculatedAt`،
  `windowStart/windowEnd` و `sourceMaxChangedAt`؛
- `assessmentScope` و `isPartial`؛
- Operational/Coverage/Freshness/Confidence و Coverage basis/countها؛
- شمار Factهای رسمی و Attention summary؛
- feature stateهای pin‌شدهٔ Contract/Planning/Budget/Quality/HSE؛
- Attention Itemهای immutable با `sourceReportId/sourceFactId` و Location lineage؛
- آخرین Project revision و آخرین تغییر Source رسمی مؤثر در cutoff برای تشخیص currency؛
- حداکثر ۱۴ نقطهٔ trend رسمی با Snapshot identity؛
- source manifest مرتب‌شده و SHA-256 آن.

خروجی `GET /command-center` منبع مستقیم F03 نیست، زیرا Finance/Commercial و disposition جاری را
براساس Permission و زمان درخواست join می‌کند. F03 فقط Projection رسمی و immutable Project State را
مصرف می‌کند. `project-state-v1` و `project-state-v2` برای خواندن تاریخچه allowlisted هستند؛ نسخهٔ
ناشناخته، Snapshot mutable یا lineage ناقص failure پردازشی امن است، نه وضعیت داده.

## ۴. قاعده cutoff و انتخاب Snapshot رسمی

Snapshot واجد شرایط باید هم‌زمان این شرایط را داشته باشد:

1. Tenant و Project آن دقیقاً با Run منطبق باشد؛
2. `calculatedAt <= sourceCutoffUtc`؛
3. `asOfDate <= cutoffLocalDate`؛
4. calculation version آن در allowlist این قرارداد باشد.

از میان Snapshotهای واجد شرایط، ابتدا بزرگ‌ترین `asOfDate`، سپس جدیدترین `calculatedAt` و در تساوی
بزرگ‌ترین `snapshotId` با مقایسهٔ ordinal انتخاب می‌شود. Client نمی‌تواند Snapshot مطلوب خود را
انتخاب کند.
Snapshotی که بعد از cutoff محاسبه شده، حتی اگر `asOfDate` قدیمی‌تری داشته باشد، وارد خروجی تاریخی
نمی‌شود.

Trend حداکثر ۱۴ تاریخ متمایز تا Snapshot منتخب دارد. برای هر `asOfDate` فقط آخرین Snapshot واجد
شرایط با همان tie-break انتخاب و نقاط در خروجی از قدیمی به جدید مرتب می‌شوند. Recalculateهای تکراری
یک تاریخ، trend را با چند نقطهٔ تکراری منحرف نمی‌کنند.

Correction یا Approval بعد از cutoff ممکن است Snapshot رسمی تازه‌ای بسازد، اما خروجی Run قبلی را
تغییر نمی‌دهد. Worker آینده پس از ساخت semantic Snapshot هرگز Source را برای Retry/Download دوباره
انتخاب نمی‌کند. زمان Worker، Run ID، attempt، زمان render و identity فایل بخشی از hash معنایی نیستند.

## ۵. مدل معنایی خروجی

Snapshot F03 مستقل از PDF/XLSX است و حداقل این بخش‌ها را دارد:

- هویت پروژه، Time Zone، revision پین‌شده و cutoff؛
- identity و lineage کامل Snapshot رسمی منتخب؛
- `dataStatus` و reason codeهای مرتب‌شده؛
- `assessmentScope` و هشدار صریح `isPartial`؛
- Operational Status بدون تغییر معنا؛
- Coverage، Freshness و Confidence مستقل همراه basis، درصد و expected/approved day count؛
- آخرین تاریخ گزارش رسمی و شمار Factهای Approved بر اساس Kind؛
- شمار Issue/Stoppage، High/Critical و قدیمی‌ترین Attention age؛
- feature stateهای Contract/Planning/Budget/Quality/HSE فقط به‌عنوان وضعیت پیکربندی، نه performance؛
- Attention Itemهای immutable با impact/priority/age/status و lineage منبع؛
- حداکثر ۱۴ trend point رسمی و canonical؛
- source manifest hash و semantic hash.

ترتیب Attention Itemها `Critical → High → Medium → Low → Unassessed`، سپس `ageDays` نزولی،
`reportDate` صعودی و در پایان `sourceReportId → sourceFactId` به‌صورت ordinal است. ترتیب trend بر
اساس `asOfDate → calculatedAt → snapshotId` است. null هرگز صفر، `Low`، `Stable` یا متن خالی ساختگی
نمی‌شود.

Disposition جاری، Action ID، Assignee و تصمیم بعد از Snapshot داخل F03 نیستند؛ این داده mutable و
متعلق به F09 است. feature state نیز فقط پیکربندی را نشان می‌دهد و نبود metric در یک حوزه به معنای
سلامت یا عملکرد مطلوب آن حوزه نیست.

## ۶. وضعیت داده، currency و reasonها

`dataStatus` با اولویت زیر تعیین می‌شود و از `operationalStatus` مستقل است:

| اولویت | وضعیت | قرارداد دقیق |
| --- | --- | --- |
| ۱ | `NotConfigured` | policy/source رسمی Project State برای Reporting پیکربندی نشده است |
| ۲ | `NoData` | Snapshot واجد شرایط وجود ندارد، یا Snapshot منتخب Operational/Coverage/Confidence برابر NoData دارد |
| ۳ | `InsufficientData` | Snapshot رسمی وجود دارد اما Operational آن InsufficientData، Coverage ناکافی، Freshness کهنه، Confidence پایین یا Snapshot در cutoff outdated است |
| ۴ | `Available` | Snapshot رسمی current با lineage کامل وجود دارد؛ Operational می‌تواند Stable، Watch، AtRisk یا Critical باشد |

Snapshot در cutoff outdated است اگر حداقل یکی از این دو شرط برقرار باشد:

- `snapshot.projectConfigurationRevision != pinnedProjectRevision`؛
- آخرین تغییر Approved Source مؤثر تا cutoff از `sourceMaxChangedAt` Snapshot جدیدتر باشد.

Reason codeهای allowlist عبارت‌اند از `ProjectStateReportingNotConfigured`،
`OfficialSnapshotMissing`، `OfficialSnapshotNoData`، `OfficialSnapshotInsufficient`،
`CoverageInsufficient`، `FreshnessStale`، `ConfidenceLow`،
`ProjectConfigurationRevisionOutdated` و `ApprovedSourceChangedAfterSnapshot`.
reasonها بدون تکرار و ordinal مرتب می‌شوند.

`isPartial=true` برای calculation versionهای موجود یک محدودیت صریح scope است و به‌تنهایی وضعیت را
`InsufficientData` نمی‌کند؛ حتی در `Available` باید نمایش داده شود. `Freshness=Aging` نیز در
`project-state-v2` می‌تواند Operational را `Watch` کند و به‌تنهایی dataStatus را ناکافی نمی‌کند.

چهار مقدار جدول وضعیت داده‌اند، نه failure پردازشی. Tenant/Project mismatch، Permission denial،
Time Zone نامعتبر، calculation version ناشناخته، classification نامعلوم، Snapshot تکراری با
tie-break غیرقطعی، hash mismatch یا corruption باید Run را fail-closed کنند و Artifact ظاهراً سالم
نسازند.

## ۷. منع Composite Health و inference

- `OperationalStatus`، `CoverageStatus`، `FreshnessStatus` و `ConfidenceStatus` دقیقاً از Snapshot
  رسمی حمل می‌شوند و Renderer اجازه بازنویسی یا ترکیب آن‌ها را ندارد؛
- هیچ رنگ/امتیاز کل از feature stateها ساخته نمی‌شود؛
- Fact count به درصد پیشرفت، بهره‌وری، forecast یا earned value تبدیل نمی‌شود؛
- Attention count به Risk score یا Action status تبدیل نمی‌شود؛
- نبود WBS، Budget، HSE یا Contract می‌تواند انتخاب معتبر پیکربندی باشد و نقص یا سلامت فرض نمی‌شود؛
- Finance، Commercial، Planning، Quality/HSE و Governance فقط در خانواده‌های مالک خود گزارش می‌شوند؛
- متن Attention بدون خلاصه‌سازی، ترجمهٔ معنایی، sentiment یا اولویت‌سازی AI حمل می‌شود.

## ۸. Permission و Classification

- Catalog/View در HTTP و سرویس read-only ابزارها به `reporting.catalog.read` و
  `project-state.read` در همان Project نیاز دارد؛
- Create/Retry علاوه بر Source permission به `reporting.run.create` نیاز دارد؛
- Download/Verify علاوه بر Source permission به `reporting.output.download` و Classification جاری
  نیاز دارد؛
- request، processing و download هر سه Membership، Project lifecycle و Permission را دوباره ارزیابی
  می‌کنند؛
- `project-state.recalculate` برای F03 لازم نیست و Reporting هرگز آن را فراخوانی نمی‌کند؛
- نبود `financial-state.read`، `commercial-state.read` یا Permissionهای F04 تا F10 موجب حذف بخشی از
  F03 نمی‌شود، چون آن داده‌ها اصولاً Source این خانواده نیستند؛
- نبود `project-state.read` کل گزارش را deny می‌کند؛ حذف خاموش Attention Item یا trend point مجاز نیست.

Catalog، فهرست/جزئیات Run و metadata خروجی در `IReportingReadService` نیز باید همان policy
definition-aware را اعمال کنند؛ gate ثابت Daily برای F03 و نمایش metadata میان خانواده‌ها ممنوع است.

Classification خروجی بیشترین مقدار میان Definition، Project/source configuration و Snapshotهای
واردشده است و حداقل `Internal` باقی می‌ماند. Source contract باید Classification را صریح برگرداند؛
نبود آن failure امن است و Caller/Template/Renderer اجازه downgrade ندارد. filename، event، log و
diagnostic نباید شرح Attention، نام Location یا lineage حساس را نشت دهند.

## ۹. Determinism، hash و budget

semantic JSON و source manifest با property order ثابت، enumهای case-sensitive، UTC canonical، تاریخ
ISO، decimal invariant و collectionهای مرتب‌شده تولید می‌شوند. دو Run با profile، cutoff و Source
یکسان باید semantic/source-manifest hash یکسان داشته باشند؛ query order، DB plan، Run ID و زمان
پردازش نباید hash را تغییر دهند.

Renderer حق truncate خاموش Attention یا trend را ندارد. trend طبق قرارداد حداکثر ۱۴ نقطه است؛
عبور Attention/row/page از budget نسخه‌دار Renderer، متن بیش‌ازحد و hash/identity ناسازگار
non-transient و fail-closed هستند، نه حذف داده. PDF دوصفحه‌ای A4/RTL و XLSX هشت‌Sheet قطعی از همان
render model canonical ساخته می‌شوند. SHA-256 قطعی XLSX برابر
`e19809b6c3ffa5ff3443babe683c9f286c3b928986d176f1d515166f336cf5a3` و PDF برابر
`d765dfc98873fbc07e28b7320524fd156cfa5acc80c6f4da2b2d42941c4e09d1` است. visual digestهای دو
صفحه در ۹۶ DPI به‌ترتیب
`6d18d03ff3e9100ffe0e12c5da05a6f1976c3d7c1d2ba7f27b36656dcc5ff0ba` و
`252a6dd6c8562242a37e4466dfb4d0a0155831abb39c30e2751309eb3acfa205` هستند. cold/warm render،
اندازهٔ PDF و row budget همان قرارداد گواهی‌شدهٔ مشترک را پاس می‌کنند.

## ۱۰. Golden matrix الزامی برای Sliceهای بعدی

| ID | Fixture | انتظار قطعی |
| --- | --- | --- |
| `F03-C01` | Snapshot پیش و پس از cutoff | فقط Snapshot با `calculatedAt <= cutoff` انتخاب می‌شود |
| `F03-C02` | چند Snapshot برای یک `asOfDate` | جدیدترین calculated-at و tie-break ordinal انتخاب می‌شود |
| `F03-C03` | Approval/Correction و Snapshot تازه بعد از cutoff | Run تاریخی و hash آن بدون تغییر می‌ماند |
| `F03-N01` | source/policy گزارش Project State پیکربندی نشده | `NotConfigured` با reason صریح |
| `F03-N02` | هیچ Snapshot واجد شرایط وجود ندارد | `NoData`؛ بدون وضعیت Stable یا عدد ساختگی |
| `F03-N03` | Snapshot رسمی با Operational/Coverage NoData | `NoData` همراه identity و scope همان Snapshot |
| `F03-I01` | Coverage ناکافی یا Operational InsufficientData | `InsufficientData` و reasonهای دقیق |
| `F03-I02` | Freshness Stale یا Confidence Low | `InsufficientData` بدون تبدیل به AtRisk ساختگی |
| `F03-I03` | Project revision جدیدتر از Snapshot | `ProjectConfigurationRevisionOutdated` |
| `F03-I04` | Approved Source در cutoff جدیدتر از source max Snapshot | `ApprovedSourceChangedAfterSnapshot` |
| `F03-O01` | Snapshot Stable و current با `isPartial=true` | `Available` همراه هشدار scope؛ بدون ادعای سلامت کل |
| `F03-O02` | Watch، AtRisk و Critical با داده کافی | status عیناً حفظ می‌شود و dataStatus همچنان `Available` است |
| `F03-T01` | بیش از ۱۴ Snapshot و Recalculate تکراری یک تاریخ | ۱۴ تاریخ متمایز، canonical و قدیم‌به‌جدید |
| `F03-A01` | Attentionهای چند priority/age و impact null | ordering ثابت؛ `Unassessed` مستقل و lineage کامل |
| `F03-S01` | revoke permission و Cross-Tenant/Project IDs | fail-closed؛ صفر Source/Output leakage |
| `F03-S02` | Source با Classification بالاتر یا نامعلوم | propagation بالاتر یا deny؛ هرگز downgrade |
| `F03-D01` | Source یکسان با query order متفاوت و دو Run twin | semantic/manifest hash یکسان و canonical ordering |

Qualification متصل آینده باید selection و currency را با query مستقل کنترل، semantic Snapshot را
parse و عدم join مالی/تجاری/Action را اثبات کند. Golden PDF/XLSX checkpointed جای Golden معنایی را
نمی‌گیرد و هنوز از API/Worker قابل اجرا نیست.

## ۱۱. Definition of Ready و Slice مجاز بعدی

| Gate | وضعیت |
| --- | --- |
| Scope/Non-Scope خانواده | بسته |
| پارامتر خالی و cutoff | بسته |
| Source lineage و Snapshot selection | بسته |
| Data status، currency و partial-state semantics | بسته |
| منع Composite Health و inference | بسته |
| Permission/classification | بسته |
| Golden matrix هفده‌سناریویی | بسته |
| Runtime Definition و parameter/snapshot/profile schema IDs | Checkpointed in Run 148 |
| Template/Renderer/Layout identity | Checkpointed in Run 154 |
| Source contract/selector/Snapshot builder | Checkpointed in Run 148 |
| PDF/XLSX/visual/performance | Checkpointed in Run 154 |
| Catalog/API/Worker wiring | Connected Candidate؛ Full CI pending |

Checkpoint `S07-MS08` روی Runtime Core نسخه‌دار، parser/request/model fail-closed، PDF فارسی A4 با
وضعیت‌های مستقل و هشدار scope، و XLSX هشت‌Sheet با RTL/freeze، text escaping و صفر Formula را اضافه
کرده است. Source
`d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f` با tree
`58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434` در Run 154 (`35512969648`) هر هشت Job، `383/383`
تست C#، `65/65` تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration را پاس
کرد.

Candidate جاری Migration forward شمارهٔ 45 را برای Definition/Template قطعی F03 اضافه می‌کند،
strict empty-object parser را روی همان endpoint مشترک اعمال می‌کند و gateهای Catalog/Run/Retry/Cancel/
Download/Verify و Worker را definition-aware می‌سازد. Worker فقط از `IProjectStateReportingSource`
برای selection cutoff-aware استفاده می‌کند، Project profile سروری را هنگام پذیرش pin می‌کند و
Renderer Registry اختصاصی F03 را dispatch می‌کند. هارنس متصل با Actor دارای `project-state.read` ولی
فاقد `field.daily-reports.read`، isolation کاتالوگ، ایجاد/Replay/Conflict، Run visibility و هر دو خروجی
PDF/XLSX را کنترل می‌کند. تا سبزشدن Full CI، Safe Resume همان `S07-MS08` است؛ UI و Production
enablement همچنان جدا و خاموش‌اند.

## ۱۲. Gate statement

این نسخه Candidate اتصال End-to-End خانواده F03 است، نه Safe Checkpoint نهایی آن. Migration،
Catalog/Template seed، strict API، Project profile pin، permissionهای definition-aware، Worker dispatch،
DI registration و qualification متصل اضافه شده‌اند؛ feature flag، license، UI و Production setting
تغییر نکرده‌اند. تنها پس از Full CI سبز، `S07-MS09` می‌تواند Safe Checkpoint شود؛ F04 تا F10 و RPT1
همچنان باز هستند.
