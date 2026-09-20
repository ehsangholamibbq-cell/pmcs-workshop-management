# PMCS V1.1 — قرارداد معنایی گزارش وضعیت مالی، Cash Position و Aging

- شناسه: `PMCS-RPT1-F05-SEMANTIC-001`
- نسخه: `1.1.1`
- خانواده: `RPT1-F05`
- وضعیت: `Runtime Core Safe Checkpoint | Renderer/Wiring Not Implemented`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS14-C1`
- Runtime change: Bounded identity/source/selector/calculator/Snapshot builder
- Migration / API / Renderer / Template change: None

## ۱. هدف و مرز خانواده

F05 یک Snapshot قابل ممیزی از وضعیت مالی رسمی همان پروژه در یک cutoff مشخص است. این خانواده گردش
نقدی قطعی ثبت‌شده، تعهدات پرداختنی و دریافتنی باز، Aging آن‌ها و مقایسه با Budget Baseline رسمی را
در صورت پیکربندی گزارش می‌کند. وضعیت مالی از Operational Status، Project Health و وضعیت تجاری مستقل
است و هیچ رنگ یا امتیاز مرکبی تولید نمی‌کند.

`externalNetCash` در این قرارداد فقط خالص گردش نقد بیرونی ثبت‌شده است؛ مانده حساب بانکی، موجودی
صندوق، سرمایه در گردش، سود و زیان، صورت جریان وجوه نقد حسابداری یا Cash Forecast نیست. همچنین
`recognizedSpend` هزینهٔ شناسایی‌شده در Finance Lite است، نه Cost-to-Complete، Earned Value یا بهای
تمام‌شدهٔ حسابداری.

F05 هیچ General Ledger، invoice/tax، FX یا تسعیر، accrual، forecast، EVM، CPI/SPI، AI summary یا
تفسیر علت تولید نمی‌کند. Contract/Amendment/Purchase/Supply متعلق به F06 است و حتی اگر شناسهٔ آن‌ها
روی رکورد Finance وجود داشته باشد join نمی‌شود. `ManagementFeePolicy`، کنترل workflow درخواست تنخواه
و reconciliation آن، جزئیات اسناد مالی، فایل‌ها، Audit comment، Project State و خانواده‌های F04 و
F06 تا F10 نیز خارج از Snapshot هستند. تنها رکوردهای `PettyCashFunding` و `PettyCashExpense` رسمی در
محاسبات گردش نقدی F05 حضور دارند.

نبود Budget یک حالت معتبر است و Cash/Aging را متوقف نمی‌کند. نبود دادهٔ رسمی هرگز به صفر، وضعیت سالم
یا مصرف بودجهٔ صفر تبدیل نمی‌شود. ارزهای متفاوت نیز جمع یا پنهانی تبدیل نمی‌شوند.

## ۲. پارامترهای canonical و evidence سروری

پارامتر معنایی Client دقیقاً یک object خالی `{}` است. `projectId` از route و scope معتبر و
`sourceCutoffUtc` از `asOfUtc` پین‌شدهٔ Run می‌آیند. هر property، از جمله `currencyCode`،
`budgetBaselineId`، `obligationType`، `agingBuckets`، تاریخ محلی، include/detail flag، Contract/Party/
Cost Center/WBS filter، forecast flag، Query، SQL یا Source selector باید به‌صورت strict رد شود.
Client نمی‌تواند Budget مطلوب، ارز، bucket یا cutoff محلی را انتخاب کند.

Server هنگام پذیرش Run این evidence را pin می‌کند و Client حق ارسال آن را ندارد:

- Tenant/Project و Project code/name/revision جاری برای هویت خروجی؛
- Time Zone و Base Currency معتبر پروژه، زمان پذیرش Run و `sourceCutoffUtc` نرمال‌شده به UTC؛
- `cutoffLocalDate` حاصل از Time Zone پروژه؛
- نسخهٔ policy انتخاب configuration/record/obligation/settlement/budget و Aging؛
- permissionهای Source و Classification موردنیاز Definition؛
- Project profile capture time برای تشخیص تغییر هویت، Time Zone یا Base Currency پس از پذیرش.

`asOfUtc` آینده نامعتبر است. Time Zone یا Base Currency ناشناخته، conversion نامعتبر یا profile
ناسازگار fail-closed است و fallback حدسی به UTC/IRR مجاز نیست. هویت جاری پروژه برای نمایش pin
می‌شود، اما Finance/Budget state و Base Currency مؤثر در cutoff باید از projection تاریخی نسخه‌دار
بیاید؛ profile جاری جای تاریخچه را نمی‌گیرد.

Runtime Core این identityهای قطعی و نسخه‌دار را همراه کد و تست واقعی تثبیت کرده است:

- Definition: `project-financial-position-certified/1.0.0`؛
- parameter schema: `pmcs.reporting.project-financial-position.parameters/v1`؛
- semantic Snapshot schema: `pmcs.reporting.project-financial-position.snapshot/v1`؛
- Project profile schema: `pmcs.reporting.project-financial-position.project-profile/v1`؛
- Finance source contract: `pmcs.finance.project-financial-position-reporting/v1`؛
- Finance source manifest: `pmcs.finance.project-financial-position-manifest/v1`؛
- selection policy: `pmcs.finance.project-financial-position-policy/v1`.

Template/Renderer/Layout identity عمداً تا Slice مستقل Renderer تخصیص داده نمی‌شود.

## ۳. Source lineage و مرز ماژولی

Runtime Core F05 فقط دو Application Contract خواندنی را مصرف می‌کند:

1. Projects برای هویت جاری، lifecycle، revision، Time Zone و Base Currency پین‌شده؛
2. Finance برای projection نسخه‌دار، cutoff-aware و classification-aware وضعیت مالی رسمی.

Finance مالک انتخاب رکوردها، تعهدات، settlementها و Budget مؤثر و محاسبهٔ financial projection
می‌ماند. Reporting حق import از `FinanceDbContext` یا `ProjectsDbContext`، اجرای SQL روی schemaهای
آن‌ها، فراخوانی HTTP به `GET /finance/state` یا `GET /finance/control`، یا مصرف مستقیم
`IFinancialStateSource` و `IFinanceControlReadService` جاری را ندارد. این سرویس‌ها read model زنده و
current-state هستند و Source گزارش Certified تاریخی محسوب نمی‌شوند.

Application Contract نسخه‌دار Finance این evidence را برمی‌گرداند:

- contract version، Tenant/Project، cutoff UTC/local و Classification صریح؛
- Finance/Budget feature state، Base Currency و configuration revision مؤثر در cutoff با بازهٔ اثر؛
- تمام Financial Recordهای رسمی واجد شرایط با identity/revision، type، transaction date، amount،
  currency و `postedAt` مستقل؛
- تمام Financial Obligationهای رسمی با identity/revision، type، number snapshot، counterparty
  snapshot، issue/due date، amount، currency و `approvedAt` مستقل؛
- settlementهای immutable هر تعهد با identity، amount، `settledAt` و lineage به Financial Record
  رسمی؛
- Budget Baselineهای رسمی با amount/currency و `approvedAt/supersededAt` مستقل؛
- completeness marker مستقل برای ledger مالی، تعهدات و settlement lineage؛
- `sourceMaxChangedAt`، source manifest مرتب‌شده و SHA-256 آن.

Draft، Submitted و Returned وارد Snapshot نمی‌شوند. Review Comment، Notes، Description رکورد مالی،
Document Number، creator/reviewer، Party/Contract/Commitment/Location/CostCenter/WBS ID، فایل و Audit
payload نیز وارد خروجی نمی‌شوند. شماره و counterparty snapshot تعهد فقط برای جدول Aging قابل نمایش
است؛ Reporting برای تازه‌سازی آن‌ها به Commercial یا Projects join نمی‌زند. Source IDها فقط
به‌اندازهٔ lineage ممیزی در semantic manifest نگه داشته می‌شوند و نباید در filename یا diagnostic
نشت کنند.

## ۴. انتخاب رکورد مالی رسمی در cutoff

یک Financial Record فقط وقتی در F05 رسمی است که هم‌زمان این شروط را داشته باشد:

1. Tenant/Project آن دقیقاً با Run منطبق باشد؛
2. lifecycle آن `Posted` باشد و `postedAt <= sourceCutoffUtc`؛
3. `transactionDate <= cutoffLocalDate`؛
4. type آن یکی از `Receipt`، `Payment`، `PettyCashFunding` یا `PettyCashExpense` باشد؛
5. currency آن دقیقاً Base Currency مؤثر پروژه در cutoff باشد؛
6. identity، revision، amount دو رقم اعشار و lineage آن کامل و یکتا باشد.

رکوردی که پیش از cutoff ایجاد ولی پس از آن Posted شده یا transaction date آینده دارد حذف می‌شود.
Draft/Submitted/Returned حتی اگر تاریخ معامله قدیمی داشته باشند رسمی نیستند. `postedAt` گمشده، type
ناشناخته، ID تکراری یا currency ناسازگار corruption است؛ Reporting آن را به `NoData` یا صفر تبدیل
نمی‌کند.

چهار نوع رکورد جداگانه جمع می‌شوند. Receipt با Receivable Obligation و Payment با Payable Obligation
یکی نیست؛ Financial Record جریان قطعی است و Obligation مانده تعهد. پیوند settlement فقط lineage
تسویه را اثبات می‌کند و باعث حذف یا دوباره‌شماری رکورد Cash نمی‌شود.

## ۵. قرارداد Cash Position و هزینه

برای مجموعهٔ رسمی بخش قبل، محاسبات دقیق نسخهٔ اول چنین‌اند:

- `totalReceipts = Σ Receipt`؛
- `directPayments = Σ Payment`؛
- `pettyCashFunding = Σ PettyCashFunding`؛
- `pettyCashExpenses = Σ PettyCashExpense`؛
- `externalNetCash = totalReceipts - directPayments - pettyCashFunding`؛
- `recognizedSpend = directPayments + pettyCashExpenses`؛
- `pettyCashBalance = pettyCashFunding - pettyCashExpenses`.

Funding تنخواه هزینه نیست و در `recognizedSpend` دوباره شمرده نمی‌شود. Expense تنخواه نیز بار دوم از
`externalNetCash` کسر نمی‌شود، چون خروج وجه بیرونی هنگام Funding ثبت شده است. مقدار منفی
`pettyCashBalance` با reason اطلاعاتی `NegativePettyCashBalance` حفظ می‌شود و Source یا Reporting
اجازه ساخت Funding مصنوعی ندارد.

تمام moneyها decimal Base Currency با scale دو هستند و جمع بدون تبدیل واحد/ارز انجام می‌شود. اگر
هیچ رکورد رسمی واجد شرایط وجود نداشته باشد `cashStatus=NoData` و همهٔ metricهای Cash برابر `null`
هستند؛ صفر فقط داخل یک Snapshot `Available` و به‌عنوان نتیجهٔ واقعی مجموعهٔ رسمی مجاز است.

## ۶. تعهدات، settlement و Aging

Obligation فقط وقتی رسمی است که `approvedAt <= sourceCutoffUtc`، Tenant/Project و currency آن منطبق،
و lifecycle Approval آن مستقل و قابل اثبات باشد. `issueDate > cutoffLocalDate` تعهد را از Snapshot
همان cutoff حذف می‌کند. Draft/Submitted/Returned وارد نمی‌شوند. وضعیت جاری
`Approved/PartiallySettled/Settled` به‌تنهایی جای `approvedAt` و settlement history را نمی‌گیرد.

مانده هر تعهد در cutoff برابر است با:

`outstandingAmount = obligationAmount - Σ eligibleSettlementAmount`

Settlement واجد شرایط باید `settledAt <= sourceCutoffUtc` داشته باشد، به همان Tenant/Project/
Obligation متصل باشد و Financial Record رسمی متناظر را با type درست (`Payment` برای Payable و
`Receipt` برای Receivable)، currency یکسان و `postedAt <= cutoff` ارجاع دهد. settlement پس از cutoff
روی Run تاریخی اثر ندارد. aggregate فعلی `SettledAmount` مرجع تاریخی نیست؛ source باید ردیف‌های
immutable settlement را جمع کند. مجموع منفی، بیش از amount، link گمشده/تکراری یا timestamp ناسازگار
failure امن است.

فقط تعهد با `outstandingAmount > 0` در جدول Open/Aging می‌آید. Payable و Receivable همیشه جدا
می‌مانند و net نمی‌شوند. برای هر type، count و amount در چهار bucket زیر ساخته می‌شود:

| Bucket | قاعده |
| --- | --- |
| `NotDue` | `dueDate >= cutoffLocalDate` |
| `Overdue1To30` | `1 <= cutoffLocalDate - dueDate <= 30` روز |
| `Overdue31To60` | `31 <= cutoffLocalDate - dueDate <= 60` روز |
| `Overdue61Plus` | اختلاف حداقل ۶۱ روز |

سررسید دقیقاً در روز cutoff دیرکرد نیست. تعداد روز بر مبنای `DateOnly.DayNumber` و بدون ساعت، holiday
یا calendar adjustment محاسبه می‌شود. ردیف‌ها در هر type با ترتیب
`dueDate → number ordinal → obligationId` صعودی‌اند. اگر source صریحاً settlement lineage ناقص را
اعلام کند، summary و ردیف‌های تعهد `null/empty`، `obligationStatus=InsufficientData` و کل Snapshot
`InsufficientData` می‌شود؛ Cash سالم حفظ می‌شود، اما ماندهٔ حدسی یا redaction خاموش مجاز نیست.

## ۷. Budget Baseline و مقایسه

Budget اختیاری و مستقل است. state مؤثر `NotConfigured/NotEnabled` به `budgetStatus=NotConfigured`،
`SetupRequired` به `SetupRequired`، `Suspended` به `Suspended` و state فعال بدون Baseline رسمی به
`NoData` نگاشت می‌شود.

Baseline رسمی باید هم‌زمان این شروط را داشته باشد:

1. Tenant/Project و Base Currency آن دقیقاً منطبق باشد؛
2. `approvedAt <= sourceCutoffUtc`؛
3. `supersededAt` نداشته باشد یا `sourceCutoffUtc < supersededAt` باشد؛
4. amount مثبت دو رقم اعشار و lifecycle lineage کامل داشته باشد.

برای هر cutoff حداکثر یک Baseline مؤثر مجاز است. overlap، approval/supersession گمشده یا tie-break
روی `ReviewedAt` corruption و processing failure است. Baseline تصویب‌شده پس از cutoff حذف و
Baselineای که بعد از cutoff Supersede شده در Run تاریخی قبلی حفظ می‌شود.

با Baseline معتبر، `approvedBudgetAmount` نمایش داده می‌شود. فقط وقتی `cashStatus=Available` باشد:

- `budgetRemainingAmount = approvedBudgetAmount - recognizedSpend`؛
- `budgetConsumedPercent = recognizedSpend × 100 / approvedBudgetAmount` با یک رقم اعشار و
  `MidpointRounding.AwayFromZero`.

درصد بالاتر از ۱۰۰ و مانده منفی cap یا سبز نمی‌شوند. اگر Cash برابر `NoData` یا
`InsufficientData` باشد Budget identity/amount می‌تواند باقی بماند، اما دو metric مقایسه `null` و
`budgetComparisonStatus` به‌ترتیب `NoData` یا `InsufficientData` است؛ نبود Fact رسمی مصرف صفر فرض
نمی‌شود.

## ۸. مدل معنایی خروجی

Snapshot مستقل از PDF/XLSX حداقل این بخش‌ها را دارد:

- هویت پروژه، Time Zone، Base Currency، revision/profile capture و cutoff UTC/local؛
- Finance/Budget configuration مؤثر و نسخه‌دار؛
- `dataStatus` و statusهای مستقل Cash، Obligation، Budget و Budget Comparison؛
- Cash summary با هفت metric بخش ۵ و data-quality reasonها؛
- Budget identity/lifecycle/amount و comparison metricهای nullable؛
- summary جداگانه Payable/Receivable شامل open/overdue count/amount؛
- Aging matrix چهار-bucketی مستقل برای هر type؛
- ردیف‌های Open Obligation با number/counterparty snapshot، issue/due date، amount، settled-at-cutoff،
  outstanding و bucket؛
- countهای source/excluded/incomplete، reason codeهای canonical و policy version؛
- source manifest hash و semantic hash.

Snapshot transaction-level ledger، متن شرح، reviewer/creator، bank/account، attachment، Contract/
Commitment/Party ID، management fee، petty-cash request status، forecast یا KPI خانواده دیگر ندارد.
نبود مقدار با null و status/reason صریح نمایش داده می‌شود؛ field ظاهراً صفر یا برچسب «سالم» ساخته
نمی‌شود.

## ۹. وضعیت داده و reason codeها

`dataStatus` مالی با اولویت زیر تعیین می‌شود:

| اولویت | وضعیت | قرارداد دقیق |
| --- | --- | --- |
| ۱ | `NotConfigured` | Finance state مؤثر `NotConfigured/NotEnabled/SetupRequired/Suspended` است |
| ۲ | `NoData` | Finance فعال است ولی Cash و Obligation هر دو هیچ دادهٔ رسمی واجد شرایط ندارند |
| ۳ | `InsufficientData` | حداقل یک collection رسمی با completeness صریح ناقص است و metric امن قابل محاسبه نیست |
| ۴ | `Available` | حداقل Cash یا Obligation دادهٔ رسمی و lineage کامل دارد |

Budget status مستقل است و نبود/تعلیق آن Cash/Aging را downgrade نمی‌کند. در Snapshot Available،
بخش دیگر می‌تواند `NoData` باشد. `InsufficientData` فقط برای نقص صریح و bounded source است؛
corruption، contract mismatch یا ambiguity به artifact جزئی تبدیل نمی‌شود و processing را fail می‌کند.

reason codeهای allowlist عبارت‌اند از `FinanceReportingNotConfigured`، `FinanceSetupRequired`،
`FinanceSuspended`، `OfficialFinancialRecordsMissing`، `OfficialObligationsMissing`،
`FinancialSourceIncomplete`، `ObligationSettlementLineageIncomplete`، `BudgetNotConfigured`،
`BudgetSetupRequired`، `BudgetSuspended`، `OfficialBudgetBaselineMissing`،
`OfficialCashDataMissingForBudgetComparison` و `NegativePettyCashBalance`. reasonها بدون تکرار و
ordinal مرتب می‌شوند؛ reason منفی تنخواه در وضعیت Available هشدار است، نه failure یا auto-correction.

Tenant/Project mismatch، configuration history نامعلوم، lifecycle timestamp گمشده/هم‌پوشان، currency
ناهمسان، type/status ناشناخته، settlement over-allocation یا link نامعتبر، ID/revision تکراری، Source
contract/classification mismatch و hash mismatch باید Run را fail-closed کنند و Artifact ظاهراً سالم
نسازند.

## ۱۰. Permission، Classification و minimization

- Catalog/View به `reporting.catalog.read` و هر چهار Permission منبع `financial-state.read`،
  `finance.records.read`، `finance.obligations.read` و `budget.baselines.read` در همان Project نیاز دارد؛
- Create/Retry علاوه بر همهٔ Permissionهای Source به `reporting.run.create` نیاز دارد؛
- Download/Verify علاوه بر Source permissionها به `reporting.output.download` و clearance طبقه‌بندی
  جاری نیاز دارد؛
- request، processing و download هر سه Membership، Project lifecycle و تمام Permissionها را دوباره
  ارزیابی می‌کنند؛
- نبود هر Permission کل Definition/Run/Output را deny می‌کند؛ حذف خاموش Budget، یک type تعهد یا ردیف
  حساس مجاز نیست؛
- Permissionهای capture/submit/review/settle و `finance.control.read` برای اجرای read-only F05 لازم
  نیستند و Reporting هیچ Commandی فراخوانی نمی‌کند.

Classification Definition و خروجی F05 حداقل `Confidential` است. Source باید Classification را صریح
برگرداند و خروجی بیشترین مقدار میان Definition، configuration و evidenceهای واردشده را می‌گیرد؛
unknown یا downgrade failure امن است. چون مدل Finance جاری per-record classification ندارد، producer
آینده حق برگرداندن `Internal` برای دادهٔ مالی را ندارد. filename/log/event/diagnostic نباید مبلغ، شماره
تعهد، counterparty یا Source ID را نشت دهد.

## ۱۱. Determinism، hash و budget

semantic JSON و source manifest با property order ثابت، enum case-sensitive، UTC canonical، DateOnly
به ISO، decimal invariant و collectionهای مرتب‌شده تولید می‌شوند. دو Run با Project profile pin،
cutoff و Source یکسان باید semantic/source-manifest hash یکسان داشته باشند؛ query order، DB plan،
Run ID، attempt، build time و render time نباید hash را تغییر دهند. Worker آینده پس از ساخت Snapshot
آن را برای Retry/Download از Source بازسازی نمی‌کند.

نسخه اول حداکثر ۱۰۰٬۰۰۰ Financial Record، ۲۰٬۰۰۰ Obligation و ۱۰۰٬۰۰۰ Settlement را می‌پذیرد.
عبور از budget، متن بیش‌ازحد یا manifest/semantic hash ناسازگار باید non-transient و fail-closed باشد،
نه truncate، sample، net یا group پنهان. PDF/XLSX، page/sheet/row budget، visual digest و performance
فقط در Renderer Slice مستقل قطعی می‌شوند.

## ۱۲. Runtime Core پیاده‌شده و شکاف Compatibility

Runtime Core مستقل Certified F05 اکنون پیاده و checkpoint شده است:

- `IProjectFinancialPositionReportingSource` مرز خواندنی، versioned، cutoff-aware و
  classification-aware Finance را تعریف می‌کند؛
- `ProjectFinancialPositionReportingSelector` lifecycle رکورد، تعهد، settlement و Budget را انتخاب،
  overlap/currency/over-allocation را رد و manifest canonical را تولید می‌کند؛
- `ProjectFinancialPositionReportingCalculator` Cash، recognized spend، Petty Cash، Budget comparison
  و Aging جداگانهٔ Payable/Receivable را با rounding قطعی می‌سازد؛
- `ProjectFinancialPositionReportSnapshotBuilder` فقط Finance source نسخه‌دار و Project profile
  پین‌شده را validate و به Snapshot معنایی allowlisted تبدیل می‌کند؛
- classification پایین‌تر از `Confidential`، schema/version ناشناخته، Tenant/Project mismatch،
  completeness ناقص یا source hash ناسازگار fail-closed است؛
- ۳۱ Unit/contract case سناریوهای Golden معنایی و boundaryهای Runtime را پوشش می‌دهند.

سرویس‌های legacy/current-state زیر همچنان به‌تنهایی Source معتبر Certified نیستند:

- `IFinancialStateSource.GetCurrentAsync` فقط آخرین Snapshot را می‌دهد و cutoff، source manifest،
  lifecycle یا completeness تاریخی ندارد؛
- `FinancialStateSnapshot` رخدادمحور و latest-state است، برای هر cutoff ساخته نشده و در حالت NoData
  عددهای صفر جاری را نگه می‌دارد؛ این Snapshot جای projection Certified با null صریح نیست؛
- `IFinanceControlReadService.GetCurrentAsync` از ساعت جاری، status جاری و `FinanceDbContext` مستقیم
  استفاده می‌کند و historical as-of ندارد؛
- `ProjectControlProfile` فقط Base Currency و feature state جاری را می‌دهد و تاریخچهٔ مؤثر در cutoff
  را ارائه نمی‌کند؛
- `BudgetBaseline.ReviewedAt` هنگام Supersede بازنویسی می‌شود و `approvedAt` مستقل باقی نمی‌ماند؛
- `FinancialObligation.SettledAmount/SettledAt` aggregate جاری است؛ هرچند ردیف Settlement وجود دارد،
  Source نسخه‌دار و cutoff-aware برای بازسازی آن هنوز ارائه نشده است؛
- `FinanceControlCalculator` Aging bucketها را میان Payable/Receivable جمع می‌کند و current-state
  collection می‌گیرد؛ قرارداد F05 دو جهت را جدا نگه می‌دارد؛
- Sourceهای legacy جاری version/classification/completeness و manifest hash موردنیاز Reporting را
  ندارند.

`ProjectFinancialPositionReportingSource` به‌عنوان compatibility producer فقط history قابل‌اثبات
از Persistence فعلی را به Contract نسخه‌دار تبدیل می‌کند. تغییر configuration پس از cutoff، رکورد یا
تعهد غیرقابل‌بازسازی، Budget legacy با supersession از دست‌رفته و هر lineage مبهم با reason پایدار
fail-closed می‌شود. حدس `approvedAt` از Audit، استفاده از status/profile جاری به‌عنوان history، انتخاب
latest Snapshot یا fallback به endpoint زنده ممنوع است. تکمیل producer تاریخی غنی‌تر، در صورت نیاز،
یک Slice دامنه‌ای مستقل است و شرط Renderer Slice بعدی نیست.

## ۱۳. Golden matrix الزامی برای Sliceهای بعدی

| ID | Fixture | انتظار قطعی |
| --- | --- | --- |
| `F05-C01` | Posted قبل/بعد cutoff و transaction date آینده | فقط رکورد با هر دو زمان واجد شرط وارد Cash می‌شود |
| `F05-C02` | Draft/Submitted/Returned با تاریخ قدیمی | همه حذف؛ هیچ‌کدام Cash رسمی نمی‌سازند |
| `F05-C03` | settlement قبل و بعد cutoff | فقط settlement رسمی تا cutoff از outstanding کم می‌شود |
| `F05-C04` | Budget قبل/بعد cutoff و Supersede آینده | دقیقاً Baseline با `approvedAt <= cutoff < supersededAt` مؤثر است |
| `F05-N01` | Finance state غیرفعال یا Suspended | `NotConfigured` با reason صریح و همه metricها null |
| `F05-N02` | Finance فعال بدون رکورد و تعهد رسمی | `NoData`، نه Cash/Aging صفر |
| `F05-I01` | ledger رسمی با completeness ناقص | Cash null و `InsufficientData`؛ بدون aggregate جزئی |
| `F05-I02` | settlement lineage صریحاً ناقص | Obligation null/empty و `InsufficientData`؛ Cash سالم حفظ می‌شود |
| `F05-M01` | هر چهار نوع رکورد مالی | هفت formula Cash دقیقاً مطابق بخش ۵ است |
| `F05-M02` | Funding و Expense تنخواه | Funding در spend و Expense در external cash دوباره شمرده نمی‌شود |
| `F05-B01` | Budget پیکربندی‌نشده | Cash/Aging مستقل؛ Budget metricها null |
| `F05-B02` | Baseline رسمی و Cash Available | remaining و consumed با rounding قطعی محاسبه می‌شوند |
| `F05-B03` | spend بیش از Budget | consumed بالای ۱۰۰ و remaining منفی بدون cap حفظ می‌شود |
| `F05-O01` | Payable و Receivable هم‌زمان | totals و Aging جدا؛ هیچ netting انجام نمی‌شود |
| `F05-O02` | partial/full settlementهای چندگانه | outstanding از ردیف‌های واجد cutoff و به‌ترتیب مستقل محاسبه می‌شود |
| `F05-A01` | due date دقیقاً cutoff | bucket برابر `NotDue` و overdue برابر false است |
| `F05-A02` | مرزهای ۱/۳۰/۳۱/۶۰/۶۱ روز | هر ردیف دقیقاً در یک bucket ثابت قرار می‌گیرد |
| `F05-X01` | currency خارج از Base Currency | processing failure؛ بدون FX یا حذف خاموش |
| `F05-X02` | دو Budget Baseline هم‌پوشان | processing failure؛ بدون latest/tie-break حدسی |
| `F05-D01` | Source یکسان با query order متفاوت و twin Run | ordering و semantic/manifest hash یکسان است |
| `F05-PM01` | revoke یکی از چهار Permission یا Cross-Tenant ID | deny/fail-closed؛ صفر Source/Output leakage |
| `F05-CL01` | Definition/Source با Classification پایین‌تر، بالاتر یا unknown | حداقل Confidential، propagation بالاتر یا deny؛ هرگز downgrade |
| `F05-MN01` | source دارای description/comment/link و شناسه‌های تجاری | فقط allowlist معنایی وارد Snapshot؛ metadata حساس نشت نمی‌کند |
| `F05-SP01` | پارامتر خالی در برابر currency/bucket/baseline/filter اضافه | `{}` پذیرفته و هر property اضافه strict رد می‌شود |
| `F05-SC01` | تلاش برای current service/DbContext/HTTP fallback | contract test رد می‌کند؛ فقط Application Contract نسخه‌دار مجاز است |

Qualification آینده باید انتخاب cutoff را با query مستقل کنترل، semantic Snapshot را parse و absence
FX/forecast/health/management-fee/F06 join را اثبات کند. Golden PDF/XLSX جای Golden معنایی را
نمی‌گیرد.

## ۱۴. Definition of Ready و Slice مجاز بعدی

| Gate | وضعیت |
| --- | --- |
| Scope/Non-Scope و معنای Cash Position | بسته |
| پارامتر خالی، cutoff و evidence سروری | بسته |
| Source lineage و مرز Projects/Finance | بسته |
| انتخاب Financial Record رسمی و formulaهای Cash | بسته |
| Obligation/Settlement و Aging چهار-bucketی دوطرفه | بسته |
| Budget lifecycle و comparison nullable | بسته |
| Data status، reasonها و failure boundary | بسته |
| چهار Permission و Classification حداقل Confidential | بسته |
| Golden matrix بیست‌وپنج‌سناریویی | بسته |
| Runtime Definition و parameter/snapshot/profile/source IDs | بسته؛ `v1`/`1.0.0` نسخه‌دار |
| historical projection و Application Contract cutoff-aware | بسته؛ compatibility مبهم fail-closed |
| selector/calculator/semantic Snapshot builder | بسته؛ ۳۱ case متمرکز |
| Template/Renderer و Golden binary | Not Implemented؛ Slice مستقل بعدی |
| Catalog/API/Worker wiring | Not Implemented؛ Slice متصل بعدی |

Micro-Step بعدی فقط می‌تواند Template/Renderer/Layout identity، render model canonical، PDF/XLSX
قطعی و Goldenهای binary/visual/performance خانواده F05 را روی Snapshot نسخه‌دار موجود اضافه کند.
Migration Catalog، endpoint/dispatch، Worker wiring، UI و Production enablement در آن Slice مجاز
نیستند.

## ۱۵. Gate statement

DoR، semantic contract و Runtime Core خانواده F05 بسته‌اند. هیچ API، Migration، Catalog seed،
Template/Renderer، Worker dispatch، feature flag، UI یا Production setting اضافه یا فعال نشده است.
F05 اکنون `Runtime Core Safe Checkpoint / Renderer/Wiring Not Implemented` و F06 تا F10 همچنان
`Required / Not Implemented` هستند؛ RPT1 و PMCS V1.1 بسته، Qualified، Final یا Locked نیستند.
