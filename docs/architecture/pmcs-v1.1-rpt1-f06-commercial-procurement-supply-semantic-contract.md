# PMCS V1.1 — قرارداد معنایی گزارش قرارداد، اصلاحیه، خرید و تأمین

- شناسه: `PMCS-RPT1-F06-SEMANTIC-001`
- نسخه: `1.2.1`
- خانواده: `RPT1-F06`
- وضعیت: `Renderer/Golden Safe Checkpoint | Catalog/API/Worker Not Implemented`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS19-C1`
- Runtime change: Versioned Template/Renderer/Layout و deterministic PDF/XLSX روی Runtime موجود
- Migration / API / Catalog / Worker change: None

## ۱. هدف و مرز خانواده

F06 یک Snapshot قابل ممیزی از وضعیت رسمی قراردادها، اصلاحیه‌ها، درخواست‌های خرید، سفارش‌ها و شواهد
تحویل/پذیرش تأمین همان پروژه در یک cutoff مشخص است. این خانواده زنجیرهٔ
`Contract → Amendment → Purchase Request → Purchase Order → Receipt/Service Acceptance` را بدون
تغییر Source و بدون استنتاج مالی پنهان گزارش می‌کند.

مبلغ سفارش در F06 «تعهد تجاری صادرشده» است، نه پرداختنی حسابداری، Cash Outflow، هزینهٔ شناسایی‌شده،
Invoice، صورت‌وضعیت یا ماندهٔ قابل پرداخت. درخواست خرید Approved نیز به‌تنهایی تعهد مالی یا تجاری
صادرشده نیست. F06 هیچ عددی را از F05 نمی‌خواند و Contract/Purchase Order را با Financial Record،
Obligation، settlement یا Budget join نمی‌کند.

مرز «تأمین» در نسخهٔ اول از صدور سفارش تا دریافت فیزیکی، بازرسی و پذیرش خدمت/اجاره است. موجودی انبار،
انتقال، حواله و Custody، مصرف/پرت، Adjustment، Reservation، Return-to-vendor، Invoice Matching،
RFQ/Tender/Quote، Expediting، Vendor Rating سازمانی و traceability سریال/Batch خارج از Snapshot
هستند. ثبت Receipt به‌معنی پذیرش نیست، پذیرش به‌معنی ورود به Stock نیست و Closed شدن سفارش نیز
به‌تنهایی تحویل کامل را اثبات نمی‌کند.

F06 هیچ FX یا تسعیر، Forecast، Cost-to-Complete، EVM، Composite Health، امتیاز AI، تشخیص علت تأخیر
یا توصیهٔ خرید تولید نمی‌کند. عددها و statusها فقط از evidence رسمی نسخه‌دار ساخته می‌شوند. نبود
سقف قرارداد، مقدار سفارش، موعد تحویل یا شواهد تأمین هرگز به صفر، تکمیل صددرصد یا وضعیت سالم تبدیل
نمی‌شود.

## ۲. پارامترهای canonical و evidence سروری

پارامتر معنایی Client دقیقاً یک object خالی `{}` است. `projectId` از route و scope معتبر و
`sourceCutoffUtc` از `asOfUtc` پین‌شدهٔ Run می‌آیند. هر property، از جمله `contractId`، `partyId`،
`supplierId`، `purchaseRequestId`، `purchaseOrderId`، status/type filter، currency، تاریخ محلی،
include/detail flag، item/category/WBS/Budget filter، Query، SQL یا Source selector باید به‌صورت
strict رد شود. Client نمی‌تواند یک قرارداد، فروشنده، سفارش، واحد، currency یا cutoff محلی مطلوب را
انتخاب کند.

Server هنگام پذیرش Run این evidence را pin می‌کند و Client حق ارسال آن را ندارد:

- Tenant/Project و Project code/name/revision جاری برای هویت خروجی؛
- Time Zone و Base Currency معتبر پروژه، زمان پذیرش Run و `sourceCutoffUtc` نرمال‌شده به UTC؛
- `cutoffLocalDate` حاصل از Time Zone پروژه؛
- Contract Model و stateهای Contract/Procurement مؤثر با configuration revision و بازهٔ اثر؛
- نسخهٔ policy انتخاب lifecycle، محاسبهٔ مبلغ/مدت، fulfillment و supplier performance؛
- permissionهای Source و Classification موردنیاز Definition؛
- Project profile capture time برای تشخیص تغییر هویت، Time Zone، Base Currency یا configuration
  پس از پذیرش.

`asOfUtc` آینده نامعتبر است. Time Zone، Base Currency یا configuration ناشناخته و تبدیل زمان
نامعتبر fail-closed است و fallback حدسی به UTC/IRR مجاز نیست. هویت جاری پروژه برای نمایش pin
می‌شود، اما configuration و commercial/supply state مؤثر در cutoff باید از projection تاریخی
نسخه‌دار بیاید؛ profile جاری جای تاریخچه را نمی‌گیرد.

Runtime Core این identityهای قطعی و نسخه‌دار را همراه کد و تست واقعی تثبیت کرده است:

- Definition: `project-commercial-procurement-supply-certified/1.0.0`؛
- parameter schema: `pmcs.reporting.project-commercial-procurement-supply.parameters/v1`؛
- semantic Snapshot schema: `pmcs.reporting.project-commercial-procurement-supply.snapshot/v1`؛
- Project profile schema: `pmcs.reporting.project-commercial-procurement-supply.project-profile/v1`؛
- Commercial source contract: `pmcs.commercial.project-commercial-procurement-supply-reporting/v1`؛
- Commercial source manifest: `pmcs.commercial.project-commercial-procurement-supply-manifest/v1`؛
- selection policy: `pmcs.commercial.project-commercial-procurement-supply-policy/v1`.

هویت‌های Renderer این Slice نیز قطعی و نسخه‌دارند:

- Template version: `1.0.0`؛
- Template content digest: `e5966e5910ff9d875151b3e0c5891fb2c36027e8608771d34ee0a5d67120efb5`؛
- Renderer contract: `pmcs.reporting.project-commercial-procurement-supply.renderer/v1`؛
- Layout contract: `pmcs.reporting.project-commercial-procurement-supply.layout/v1`.

## ۳. Source lineage و مرز ماژولی

Runtime Core F06 فقط دو Application Contract خواندنی را مصرف می‌کند:

1. Projects برای هویت جاری، lifecycle، revision، Time Zone، Base Currency و configuration پین‌شده؛
2. Commercial برای projection نسخه‌دار، cutoff-aware و classification-aware قرارداد، خرید و تأمین.

Commercial مالک انتخاب versionهای مؤثر، بازسازی lifecycle، اعتبارسنجی linkها، تبدیل مقدار به واحد
پایه و محاسبهٔ commercial/supply projection می‌ماند. Reporting حق import از
`CommercialDbContext`/`CommercialSupplyDbContext` یا `ProjectsDbContext`، اجرای SQL روی schemaهای
آن‌ها، فراخوانی HTTP به `/commercial/state` یا `/commercial/supply/state`، یا مصرف مستقیم
`ICommercialStateSource` و DTOهای endpoint جاری را ندارد. این مسیرها current-state، bounded/truncated
یا aggregate هستند و Source گزارش Certified تاریخی محسوب نمی‌شوند.

Application Contract نسخه‌دار Commercial این evidence را برمی‌گرداند:

- contract version، Tenant/Project، cutoff UTC/local و Classification صریح؛
- Contract/Procurement feature state و configuration revision مؤثر در cutoff با بازهٔ اثر؛
- Party snapshotهای نسخه‌دار با code/name/type و بازهٔ اثر؛ بدون National ID، تلفن یا contact؛
- Contract version/lifecycle eventهای immutable شامل activation/suspension/close/termination time؛
- Amendmentهای نسخه‌دار با type، amount delta، extension days و `approvedAt` مستقل؛
- Purchase Request event history شامل submit/approve/order/cancel time و estimated amount nullable؛
- Purchase Order event history شامل issue/close/cancel time، amount، due date و linkهای Request/
  Contract/Party؛
- Supply Item و quantity basis پین‌شده در زمان Issue شامل kind، code/name، ordered quantity، unit،
  conversion version، base quantity و base unit؛
- Goods Receiptها با created/arrived/inspected time، quantity basis، result و excess approval lineage؛
- Service Acceptanceهای immutable با period، verified time و delivered/accepted/rejected quantity؛
- completeness marker مستقل برای contract lifecycle، amendment، procurement، party/item snapshot،
  receipt/inspection و service acceptance؛
- `sourceMaxChangedAt`، source manifest مرتب‌شده و SHA-256 آن.

Notes، review/closure/inspection comment، Description، National ID، تلفن، contact، delivery address
آزاد، Evidence reference، attachment، actor identity، Stock location، Batch/Lot، WBS/Budget link و
Audit payload وارد Snapshot نمی‌شوند. Number/title/code/name فقط به‌اندازهٔ register رسمی و با طول
bounded می‌آیند. Source IDها فقط برای lineage ممیزی در semantic manifest نگه داشته می‌شوند و نباید
در filename، event یا diagnostic نشت کنند.

## ۴. انتخاب lifecycle رسمی قرارداد و اصلاحیه در cutoff

یک Contract فقط وقتی در register رسمی F06 وارد می‌شود که Tenant/Project آن منطبق، هویت و revision
آن یکتا و `activatedAt <= sourceCutoffUtc` باشد. وضعیت آن در cutoff از eventهای immutable بازسازی
می‌شود؛ Close، Suspend یا Terminate بعد از cutoff وضعیت Run تاریخی قبلی را تغییر نمی‌دهد. Contract
با Activation پس از cutoff و lifecycleهای Draft/Submitted/Returned وارد register رسمی و جمع مبلغ
نمی‌شود؛ workflow pending می‌تواند فقط در بخش pending جداگانه نمایش داده شود.

هر Contract رسمی باید Party snapshot مؤثر، number/title/type، Base Currency، original amount nullable،
start/end date nullable و timestampهای lifecycle مستقل داشته باشد. Active contract با
`effectiveEndDate < cutoffLocalDate` منقضی است؛ تاریخ مساوی cutoff منقضی نیست. Closed یا Terminated
بودن به‌معنی انجام تعهد، تسویه یا تحویل کامل نیست.

یک Amendment فقط وقتی روی Contract اثر رسمی دارد که به Contract همان Tenant/Project متصل، revision
و identity یکتا، status آن Approved و `approvedAt <= sourceCutoffUtc` باشد. Approval پس از cutoff
و lifecycleهای Draft/Submitted/Returned وارد amount/time نمی‌شوند. shape مجاز هر type چنین است:

| Type | `amountDelta` | `extensionDays` | اثر رسمی |
| --- | --- | --- | --- |
| `ScopeChange` | `null` | `null` | count/register؛ بدون استنتاج عددی از title/notes |
| `ValueChange` | غیرصفر | `null` | تغییر مبلغ |
| `TimeExtension` | `null` | مثبت | تغییر مدت |
| `Mixed` | غیرصفر | مثبت | هر دو تغییر |

ترکیب دیگری، currency ناسازگار، Approval timestamp گمشده، link به Contract غیررسمی یا amendment
تکراری corruption است؛ Reporting مقدار اضافه را نادیده نمی‌گیرد و type را از فیلدها حدس نمی‌زند.

## ۵. محاسبهٔ مبلغ و مدت مؤثر قرارداد

برای هر Contract رسمی در cutoff:

- `approvedAmountDelta = Σ eligible ValueChange/Mixed amountDelta`؛
- اگر `originalApprovedAmount` معلوم باشد،
  `effectiveApprovedAmount = originalApprovedAmount + approvedAmountDelta`؛
- `approvedExtensionDays = Σ eligible TimeExtension/Mixed extensionDays`؛
- اگر `originalEndDate` معلوم باشد،
  `effectiveEndDate = originalEndDate + approvedExtensionDays`.

`effectiveApprovedAmount < 0`، overflow تاریخ/عدد یا scale بیش از دو رقم اعشار failure امن است.
Original amount نامعلوم به صفر تبدیل نمی‌شود؛ delta قابل نمایش می‌ماند اما effective amount آن
Contract برابر `null` و reason برابر `ContractCeilingUnavailable` است. Extension بدون end date نیز
حفظ می‌شود، ولی effective end date برابر `null` و reason برابر `ContractEndDateUnavailable` است.

Summary پروژه هم‌زمان دو مفهوم جدا دارد: `knownEffectiveContractCeilingSubtotal` جمع Contractهای
دارای amount معلوم است و `effectiveContractCeilingTotal` فقط وقتی مقدار دارد که amount تمام
Contractهای رسمی کامل باشد. subtotal هرگز با عنوان total نمایش داده نمی‌شود. مبلغ Contractها و
Amendmentها فقط در Base Currency مؤثر جمع می‌شوند؛ هیچ FX، netting با Purchase Order یا کسر Payment
وجود ندارد.

## ۶. درخواست خرید و سفارش/تعهد تجاری

وضعیت Purchase Request در cutoff از event history بازسازی می‌شود. Submitted در صف Approval، Approved
در انتظار سفارش و Ordered سه مفهوم مستقل‌اند. Approval به‌تنهایی commitment نیست و
`estimatedAmount` فقط estimate nullable درخواست است؛ با amount سفارش جمع یا جایگزین نمی‌شود.

Purchase Order فقط وقتی در register رسمی وارد می‌شود که `issuedAt <= sourceCutoffUtc`، Request آن
تا همان cutoff Approved، Party snapshot آن معتبر و Tenant/Project/Currency آن منطبق باشد. در V1 هر
Request حداکثر یک Order دارد؛ دو Order برای یک Request یا Order پیش از Approval failure امن است.
اگر `contractId` وجود دارد، Contract باید در همان cutoff رسمی و Party سفارش دقیقاً همان Party
Contract باشد؛ link نامعتبر حذف خاموش نمی‌شود.

وضعیت Order در cutoff از `issuedAt/closedAt/cancelledAt` مستقل ساخته می‌شود. Cancel بعد از cutoff روی
Run تاریخی قبل اثر ندارد. محاسبات مبلغی:

- `totalIssuedOrderAmount` جمع Orderهای Issued یا Closed در cutoff است؛
- `openOrderAmount` فقط Orderهای Issued در cutoff است؛
- Cancelled در register و count نگه داشته می‌شود، اما از دو مبلغ بالا حذف می‌شود؛
- Closed در total تاریخی می‌ماند، اما open نیست.

Closed شدن Order تحویل، پذیرش، Invoice یا Payment را اثبات نمی‌کند. `deliveryDueDate` دقیقاً برابر
`cutoffLocalDate` overdue نیست؛ فقط Order باز و تکمیل‌نشده با due date کوچک‌تر از cutoff،
`OverdueOpen` است. موعد گمشده به `DeliveryDueDateUnavailable`/`NotAssessable` می‌رود و «به‌موقع» فرض
نمی‌شود.

## ۷. شواهد تأمین، پذیرش و fulfillment

Goods Receipt فقط وقتی evidence شناخته‌شده در cutoff است که `createdAt <= sourceCutoffUtc` و
`arrivedAt <= sourceCutoffUtc` باشد. ثبت پس از cutoff با arrival backdated وارد Run تاریخی قبلی
نمی‌شود. نتیجهٔ Inspection فقط وقتی اثر دارد که `inspectedAt <= sourceCutoffUtc`؛ در غیر این صورت
Receipt در وضعیت pending باقی می‌ماند، حتی اگر current row بعداً Accepted شده باشد. Received،
Accepted، Rejected و Quarantined quantity جدا می‌مانند و received quantity هرگز خودکار accepted
نمی‌شود.

Service Acceptance فقط با `verifiedAt <= sourceCutoffUtc` و `periodEnd <= cutoffLocalDate` رسمی است.
Material Order فقط از Receipt/Inspection و Service/EquipmentRental Order فقط از Service Acceptance
تغذیه می‌شود؛ mix نوع evidence، Item/Party/Order mismatch یا unit basis ناسازگار failure امن است.

برای Order دارای quantity basis کامل:

- `deliveredBaseQuantity` برای Material از Receiptهای eligible و برای Service/Rental از
  Service Acceptanceهای eligible می‌آید؛
- `acceptedBaseQuantity` فقط از Inspection/Acceptance رسمی می‌آید؛
- `rejectedBaseQuantity` و `quarantinedBaseQuantity` جدا هستند؛
- `remainingOrderedQuantity = max(orderedBaseQuantity - acceptedBaseQuantity, 0)`؛
- `acceptedExcessQuantity = max(acceptedBaseQuantity - orderedBaseQuantity, 0)`؛
- `fulfillmentPercent = acceptedBaseQuantity × 100 / orderedBaseQuantity` با یک رقم اعشار و
  `MidpointRounding.AwayFromZero`.

درصد بالاتر از ۱۰۰ cap نمی‌شود و excess صریح باقی می‌ماند؛ اما فقط وقتی همهٔ evidenceهای اضافه،
approval lineage معتبر داشته باشند. over-delivery بدون Approval یا جمع‌های منفی/نامتوازن failure
امن است. اگر quantity/unit/conversion basis هنگام Issue پین نشده باشد، amount/order status حفظ
می‌شود ولی fulfillment metricها `null` و status برابر `NotAssessable` هستند؛ Source یا Reporting
حق استفاده از latest conversion را ندارد.

delivery status allowlist عبارت است از `NotAssessable`، `PendingDue`، `OnTimeFulfilled`،
`LateFulfilled`، `OverdueOpen`، `ClosedShort` و `Cancelled`. Completion date اولین تاریخ تحویل
canonical است که cumulative accepted quantity به ordered quantity می‌رسد؛ برای Material تاریخ محلی
`arrivedAt` و برای Service/Rental `periodEnd` استفاده می‌شود. Closed order با accepted quantity کمتر
از ordered، `ClosedShort` است و هرگز Fulfilled فرض نمی‌شود.

هیچ quantity میان item یا base unit متفاوت جمع نمی‌شود. Summary quantity فقط در گروه
`itemId + baseUnit` ساخته می‌شود و Summary سراسری فقط count/rate دارد.

## ۸. Supplier performance

Supplier performance فقط از Order و evidence رسمی همین Snapshot ساخته می‌شود و ranking، score یا
توصیه تولید نمی‌کند. برای هر Party snapshot حداقل این countها نگه داشته می‌شود:

- issued، open، closed و cancelled order؛
- assessable order، `OnTimeFulfilled`، `LateFulfilled`، `OverdueOpen` و `ClosedShort`؛
- Receipt count، pending inspection و Receipt دارای rejected/quarantined quantity؛
- Service Acceptance count و Service Acceptance دارای rejected quantity.

`onTimeFulfillmentRate = OnTimeFulfilled × 100 / assessableCompletedOrders` فقط وقتی denominator
مثبت است؛ در غیر این صورت `null` است، نه صفر یا صد. Order بدون due date/quantity basis از denominator
حذف و در `notAssessableCount` ثبت می‌شود. Party name/code باید snapshot مؤثر در cutoff باشد؛ join به
Party جاری یا نمایش National ID/contact ممنوع است.

## ۹. مدل معنایی خروجی

Snapshot مستقل از PDF/XLSX حداقل این بخش‌ها را دارد:

- هویت پروژه، Time Zone، Base Currency، revision/profile capture و cutoff UTC/local؛
- Contract Model و configuration مؤثر و نسخه‌دار Contract/Procurement؛
- `dataStatus` و statusهای مستقل Contract، Procurement و Supply؛
- Contract summary، completeness مبلغ، lifecycle countها و سقف known/total nullable؛
- Contract register با Party snapshot، type/state، start/original/effective end، original/effective
  amount nullable و amendment count/delta/extension؛
- Approved Amendment register و pending workflow summary جدا؛
- Procurement summary با Request state countها، Order state countها و amountهای issued/open؛
- Purchase Order register با Party/Contract/Request lineage، amount، due date، quantity basis،
  fulfillment، delivery status و quality countها؛
- Supply summary گروه‌بندی‌شده بر item/base unit و supplier performance count/rateهای nullable؛
- countهای source/excluded/incomplete، reason codeهای canonical و policy version؛
- source manifest hash و semantic hash.

Snapshot متن Notes/Comment/Description، contact/National ID، attachment/evidence link، Stock balance،
Material Issue، Inventory Adjustment، Payment/Invoice/Budget، actor identity، AI explanation یا KPI
خانوادهٔ دیگر ندارد. نبود مقدار با null و status/reason صریح نمایش داده می‌شود؛ field ظاهراً صفر،
fulfillment ساختگی یا برچسب «سالم» ساخته نمی‌شود.

## ۱۰. وضعیت داده و reason codeها

section statusها configuration-aware هستند. Contract یا Procurement با state
`NotConfigured/NotEnabled` به `NotConfigured`، `SetupRequired` به `SetupRequired` و `Suspended` به
`Suspended` نگاشت می‌شود. Procurement فعال، مالک status Supply نیز هست؛ Supply feature مستقل در
Project profile اختراع نمی‌شود.

`dataStatus` کل Snapshot با اولویت زیر تعیین می‌شود:

| اولویت | وضعیت | قرارداد دقیق |
| --- | --- | --- |
| ۱ | `NotConfigured` | هیچ‌یک از Contract/Procurement قابل ارزیابی و فعال نیستند |
| ۲ | `InsufficientData` | حداقل یک بخش فعال، completeness ناقص یا lineage لازمِ مبهم دارد |
| ۳ | `NoData` | بخش‌های فعال کامل‌اند ولی هیچ Contract/Request/Order/Supply evidence رسمی واجد cutoff ندارند |
| ۴ | `Available` | حداقل یک بخش دادهٔ رسمی دارد و همهٔ بخش‌های فعال completeness قابل‌اثبات دارند |

یک بخش NotConfigured یا NoData بخش سالم دیگر را downgrade نمی‌کند. نقص Contract فقط وقتی Procurement
را نیز ناکافی می‌کند که Order واقعاً Contract link داشته و link قابل اثبات نباشد. نقص Procurement
Supply را ناکافی می‌کند، چون denominator و lineage Order قابل اثبات نیست؛ نقص Supply، Contract یا
amountهای Procurement را به‌تنهایی از بین نمی‌برد.

reason codeهای allowlist عبارت‌اند از `CommercialReportingNotConfigured`، `ContractSetupRequired`،
`ContractSuspended`، `ProcurementSetupRequired`، `ProcurementSuspended`،
`OfficialContractsMissing`، `OfficialProcurementMissing`، `OfficialSupplyEvidenceMissing`،
`ContractLifecycleIncomplete`، `AmendmentLifecycleIncomplete`، `ProcurementLifecycleIncomplete`،
`SupplyLineageIncomplete`، `PartySnapshotUnavailable`، `UnitConversionHistoryUnavailable`،
`ContractCeilingUnavailable`، `ContractEndDateUnavailable`، `OrderQuantityBasisUnavailable`،
`DeliveryDueDateUnavailable`، `ClosedOrderSupplyGap`، `PendingInspection` و
`RejectedOrQuarantinedSupply`. reasonها بدون تکرار و ordinal مرتب می‌شوند.

Tenant/Project mismatch، event chronology نامعتبر، status/type ناشناخته، currency ناسازگار، amendment
shape نامعتبر، amount/quantity منفی یا overflow، duplicate identity/revision، Request/Order/Contract/
Party/Item link نامعتبر، Approval/Issue/Inspection/Acceptance time گمشده، unit conversion مبهم،
over-delivery بدون Approval، Source contract/classification mismatch و hash mismatch باید Run را
fail-closed کنند و Artifact جزئی یا ظاهراً سالم نسازند.

## ۱۱. Permission، Classification و minimization

- Catalog/View به `reporting.catalog.read` و هر شش Permission منبع `commercial-state.read`،
  `commercial.parties.read`، `contracts.read`، `procurement.requests.read`،
  `procurement.orders.read` و `supply.read` در همان Project نیاز دارد؛
- Create/Retry علاوه بر همهٔ Permissionهای Source به `reporting.run.create` نیاز دارد؛
- Download/Verify علاوه بر Source permissionها به `reporting.output.download` و clearance طبقه‌بندی
  جاری نیاز دارد؛
- request، processing و download هر سه Membership، Project lifecycle و تمام Permissionها را دوباره
  ارزیابی می‌کنند؛
- نبود هر Permission کل Definition/Run/Output را deny می‌کند؛ حذف خاموش amount، Party، Contract،
  Order یا Supply section مجاز نیست؛
- Permissionهای capture/submit/review/issue/inspect/inventory و Finance permissions برای اجرای
  read-only F06 لازم نیستند و Reporting هیچ Commandی فراخوانی نمی‌کند.

Classification Definition و خروجی F06 حداقل `Confidential` است. Source باید Classification را صریح
برگرداند و خروجی بیشترین مقدار میان Definition، configuration و evidenceهای واردشده را می‌گیرد؛
unknown یا downgrade failure امن است. filename/log/event/diagnostic نباید مبلغ، شماره Contract/Order،
Party/Item name یا Source ID را نشت دهد.

## ۱۲. Determinism، hash و budget

semantic JSON و source manifest با property order ثابت، enum case-sensitive، UTC canonical، DateOnly
به ISO، decimal invariant و collectionهای مرتب‌شده تولید می‌شوند. ترتیبهای canonical حداقل عبارت‌اند
از Contract با `number ordinal → contractId`، Amendment با `approvedAt → number → amendmentId`،
Request/Order با `number → id`، delivery evidence با `deliveryDate → officialNumber → id` و Supplier
با `partyCode → partyId`.

دو Run با Project profile pin، cutoff و Source یکسان باید semantic/source-manifest hash یکسان داشته
باشند؛ query order، DB plan، Run ID، attempt، build time و render time نباید hash را تغییر دهند.
Worker آینده پس از ساخت Snapshot آن را برای Retry/Download از Source بازسازی نمی‌کند.

نسخه اول حداکثر ۲۰٬۰۰۰ Contract، ۱۰۰٬۰۰۰ Amendment، ۱۰۰٬۰۰۰ Purchase Request، ۱۰۰٬۰۰۰ Purchase
Order، ۲۵۰٬۰۰۰ Receipt/Service Acceptance و ۵۰٬۰۰۰ Party/Item snapshot را می‌پذیرد. عبور از budget،
متن بیش‌ازحد یا manifest/semantic hash ناسازگار باید non-transient و fail-closed باشد، نه truncate،
sample، latest-only یا group پنهان. Renderer نیز row/page/text budget را پیش از انتشار fail-closed
کنترل می‌کند؛ PDF سه‌صفحه‌ای و XLSX ده-Sheet، visual digest و performance در همین Checkpoint قطعی
شده‌اند.

## ۱۳. Runtime Core پیاده‌شده و شکاف Compatibility

Runtime Core مستقل Certified F06 اکنون پیاده و checkpoint شده است:

- `IProjectCommercialProcurementSupplyReportingSource` مرز خواندنی، versioned، cutoff-aware و
  classification-aware Commercial را تعریف می‌کند؛
- `ProjectCommercialProcurementSupplyReportingSelector` lifecycle قرارداد، اصلاحیه، درخواست و
  سفارش را در cutoff بازسازی، link/unit/excess approval را validate و manifest canonical را تولید
  می‌کند؛
- `ProjectCommercialProcurementSupplyReportingCalculator` مبلغ/مدت مؤثر، commitment سفارش،
  fulfillment، delivery status و supplier count/rate را بدون FX، netting یا score محاسبه می‌کند؛
- Item snapshot و quantity/unit/conversion basis دقیقاً در زمان Issue سفارش پین می‌شوند و lifecycle
  eventها باید از sequence یک و بدون شکاف باشند؛
- `ProjectCommercialProcurementSupplyReportSnapshotBuilder` فقط Source نسخه‌دار Commercial و Project
  profile پین‌شده را validate و به Snapshot معنایی allowlisted تبدیل می‌کند؛
- classification پایین‌تر از `Confidential`، schema/version ناشناخته، Tenant/Project mismatch،
  completeness ناقص یا source hash ناسازگار fail-closed است؛
- ۳۲ Unit/contract case تمام سناریوهای Golden معنایی و boundaryهای Runtime را پوشش می‌دهند.

سرویس‌ها و endpointهای legacy/current-state زیر همچنان به‌تنهایی Source معتبر Certified نیستند:

- `ICommercialStateSource.GetCurrentAsync` فقط آخرین aggregate Snapshot را می‌دهد و cutoff، lifecycle
  event، source manifest، classification و completeness تاریخی ندارد؛
- `CommercialStateSource` آخرین `CalculatedAt` را انتخاب می‌کند و در نبود Snapshot با profile و زمان
  جاری state می‌سازد؛ این مسیر Source تاریخی Certified نیست؛
- `ProjectContract.Close` مقدار `ReviewedAt` را با زمان Close بازنویسی می‌کند و activation time مستقل
  در aggregate فعلی باقی نمی‌ماند؛
- Party فقط current row و `ChangedAt` دارد و snapshot نام/code مؤثر در cutoff را نگه نمی‌دارد؛
- Purchase Request status جاری event history کامل Submit/Approve/Order/Cancel را عرضه نمی‌کند؛
- Purchase Order مقدار/unit را نگه می‌دارد، اما ordered base quantity و conversion version پین‌شده در
  زمان Issue ندارد؛ fulfillment تاریخی با latest conversion قابل استنتاج نیست؛
- endpoint `/commercial/supply/state` collectionها را با `Take(500/1000/2000)` محدود، clock جاری و
  DbContext مستقیم می‌خواند؛ completeness و cutoff Certified ندارد؛
- Supplier performance جاری Receipt-level است، Inspection cutoff، Service Acceptance، quantity
  basis، ClosedShort و denominator قابل ممیزی Order-level را پوشش نمی‌دهد؛
- `CommercialStateSnapshot` count/amount aggregate دارد، اما register، source lineage، unit basis و
  status تاریخی هر Contract/Order/Supply fact را ندارد.

`ProjectCommercialProcurementSupplyReportingSource` به‌عنوان compatibility producer فقط history
قابل‌اثبات از Persistence فعلی را به Contract نسخه‌دار تبدیل می‌کند. configuration history، contract
lifecycle، Item snapshot در زمان Issue و excess approval غیرقابل‌بازسازی با reason پایدار fail-closed
می‌شوند؛ current state یا آخرین Item به‌عنوان history پذیرفته نمی‌شود. حدس activation از ReviewedAt،
استفاده از current status/profile، latest state، truncated endpoint یا Audit متن آزاد ممنوع است.
تکمیل producer تاریخی غنی‌تر، در صورت نیاز، یک Slice دامنه‌ای مستقل است و شرط wiring متصل بعدی
نیست.

### ۱۳.۱ Renderer/Golden پیاده‌شده

Renderer مستقل F06 روی Snapshot نسخه‌دار موجود بسته شده است:

- parser/request/model، Definition، schema، Template/Renderer/Layout، semantic/source hash، cutoff،
  Project profile و filename را fail-closed تطبیق می‌دهند؛
- render model بخش‌های Contract/Amendment، Procurement/Order، Supply و Supplier را canonical می‌کند
  و status/null/reason را بدون تبدیل به صفر یا وضعیت سالم نگه می‌دارد؛
- PDF فارسی/RTL و A4 افقی دقیقاً سه صفحه دارد: قراردادها/اصلاحیه‌ها، خرید/سفارش/تأمین و
  supplier/lineage؛
- XLSX دقیقاً ده Sheet ثابت `Metadata`، `Contract Summary`، `Contracts`، `Amendments`،
  `Procurement`، `Purchase Orders`، `Supply Summary`، `Suppliers`، `Source Counts` و `Lineage`
  دارد؛ ترتیب و timestamp ZIP ثابت، compression خاموش، RTL، frozen header، سلول عددی واقعی و صفر
  Formula است؛
- متن با prefix فرمول خنثی و NoData در Sheetهای تجاری header-only می‌شود؛ هیچ مقدار ساختگی، F05
  join، Inventory/Stock، FX، ranking، AI یا truncate تولید نمی‌شود؛
- عبور از budget، value/status یا identity/hash ناسازگار non-transient و fail-closed است؛
- Registry اختصاصی F06 عمداً خارج از composition root، endpoint و Worker باقی مانده است؛
- شش case Renderer/Golden و در مجموع `38/38` case متمرکز F06، binary/visual/performance را در Run
  196 قطعی کرده‌اند.

## ۱۴. Golden matrix الزامی برای Sliceهای بعدی

| ID | Fixture | انتظار قطعی |
| --- | --- | --- |
| `F06-C01` | Activate/Close/Suspend قبل و بعد cutoff | state قرارداد فقط از eventهای واجد cutoff بازسازی می‌شود |
| `F06-C02` | Amendment Approved قبل/بعد cutoff | فقط Approval تا cutoff روی مبلغ/مدت اثر دارد |
| `F06-C03` | Request/Order/Receipt/Inspection/Acceptance دو سوی cutoff | هر fact فقط با event رسمی واجد cutoff وارد می‌شود |
| `F06-N01` | Contract/Procurement غیرفعال، SetupRequired یا Suspended | status/reason صریح؛ هیچ count/amount ساختگی |
| `F06-N02` | feature فعال بدون fact رسمی | `NoData`، نه register یا total صفرنما |
| `F06-I01` | lifecycle Contract/Amendment ناقص | Contract section برابر `InsufficientData`؛ بدون latest-state fallback |
| `F06-I02` | Request/Order event یا link ناقص | Procurement و Supply ناکافی؛ Contract سالم حفظ می‌شود |
| `F06-I03` | Party/Item/unit history یا Supply completeness ناقص | metric وابسته null/ناکافی؛ بدون conversion یا نام جاری حدسی |
| `F06-CT01` | original amount و amendment مثبت/منفی | effective amount دقیق و منفی‌شدن سقف failure است |
| `F06-CT02` | Contract با original amount نامعلوم | known subtotal جدا، total null و `ContractCeilingUnavailable` |
| `F06-CT03` | TimeExtension/Mixed چندگانه | extension جمع و effective end date قطعی محاسبه می‌شود |
| `F06-CT04` | ScopeChange و type/field shape نامعتبر | ScopeChange اثر عددی ندارد؛ shape نامعتبر fail می‌شود |
| `F06-CT05` | Submitted Contract/Amendment | فقط pending summary؛ register و ceiling رسمی تغییر نمی‌کند |
| `F06-PR01` | Approved Request بدون Order | awaiting-order ثبت می‌شود و commitment صفر/ساختگی ایجاد نمی‌شود |
| `F06-PR02` | دو Order برای یک Request یا Order پیش از Approval | processing failure؛ بدون انتخاب latest |
| `F06-PO01` | Issued/Closed/Cancelled دو سوی cutoff | total/open/cancelled دقیقاً مطابق lifecycle محاسبه می‌شوند |
| `F06-PO02` | due date برابر cutoff و یک روز قبل | اولی overdue نیست؛ دومی فقط اگر باز/ناکامل باشد `OverdueOpen` است |
| `F06-PO03` | Order لینک‌شده به Contract با Party متفاوت | processing failure؛ link خاموش حذف نمی‌شود |
| `F06-S01` | Receipt pending/accepted/rejected/quarantined | received، accepted، rejected و quarantined جدا می‌مانند |
| `F06-S02` | Service/Rental acceptance | فقط acceptance verified و نوع درست وارد fulfillment می‌شود |
| `F06-S03` | چند تحویل جزئی تا تکمیل | cumulative accepted و completion date/order status قطعی است |
| `F06-S04` | excess مجاز و بدون Approval | مجاز بالای ۱۰۰ بدون cap؛ بدون Approval failure است |
| `F06-S05` | unitهای متفاوت و conversion version | فقط base basis پین‌شده؛ هیچ جمع cross-unit یا latest conversion |
| `F06-S06` | Order بدون quantity یا due date | amount حفظ و fulfillment/on-time برابر null/`NotAssessable` است |
| `F06-PF01` | Supplier دارای on-time/late/overdue/not-assessable | countها قطعی و rate فقط با denominator مثبت است |
| `F06-X01` | currency خارج از Base Currency | processing failure؛ بدون FX یا حذف خاموش |
| `F06-D01` | Source یکسان با query order متفاوت و twin Run | ordering و semantic/manifest hash یکسان است |
| `F06-PM01` | revoke یکی از شش Permission یا Cross-Tenant ID | deny/fail-closed؛ صفر Source/Output leakage |
| `F06-CL01` | Definition/Source با Classification پایین‌تر، بالاتر یا unknown | حداقل Confidential، propagation بالاتر یا deny؛ هرگز downgrade |
| `F06-MN01` | Source دارای contact/comment/evidence/stock/finance link | فقط allowlist معنایی وارد Snapshot؛ metadata حساس نشت نمی‌کند |
| `F06-SP01` | پارامتر خالی در برابر Contract/Party/status/date/filter اضافه | `{}` پذیرفته و هر property اضافه strict رد می‌شود |
| `F06-SC01` | تلاش برای current service/DbContext/HTTP/truncated fallback | contract test رد می‌کند؛ فقط Application Contract نسخه‌دار مجاز است |

Qualification آینده باید cutoff/lifecycle را با query مستقل کنترل، semantic Snapshot را parse و absence
FX، F05 join، inventory/custody، AI score و zero fabrication را اثبات کند. Golden PDF/XLSX جای
Golden معنایی را نمی‌گیرد.

## ۱۵. Definition of Ready و Slice مجاز بعدی

| Gate | وضعیت |
| --- | --- |
| Scope/Non-Scope و جدایی F05/Inventory | بسته |
| پارامتر خالی، cutoff و evidence سروری | بسته |
| Source lineage و مرز Projects/Commercial | بسته |
| lifecycle رسمی Contract/Amendment و pending workflow | بسته |
| مبلغ/مدت مؤثر و null/partial-total policy | بسته |
| Request/Order lifecycle و commitment semantics | بسته |
| Receipt/Inspection/Service Acceptance و fulfillment | بسته |
| Supplier performance بدون score/ranking | بسته |
| Data status، reasonها و failure boundary | بسته |
| شش Permission و Classification حداقل Confidential | بسته |
| Golden matrix سی‌ودوسناریویی | بسته |
| Runtime Definition و parameter/snapshot/profile/source IDs | بسته؛ `v1`/`1.0.0` نسخه‌دار |
| historical projection و Application Contract cutoff-aware | بسته؛ compatibility مبهم fail-closed |
| selector/calculator/semantic Snapshot builder | بسته؛ ۳۲ case متمرکز |
| Template/Renderer و Golden binary/visual/performance | بسته؛ PDF سه‌صفحه‌ای و XLSX ده-Sheet قطعی |
| Catalog/API/Worker wiring | Not Implemented؛ Slice متصل بعدی |

Micro-Step بعدی فقط می‌تواند Catalog/Template seed نسخه‌دار، strict `{}` API، Project profile
پین‌شده، شش Permission definition-aware و Worker/Renderer dispatch متصل F06 را روی همین Runtime و
Renderer اضافه و End-to-End qualify کند. UI/UX2، Production enablement، Report Designer و F07 در
آن Slice مجاز نیستند.

## ۱۶. Gate statement

DoR، semantic contract، Runtime Core و Renderer/Golden خانواده F06 بسته‌اند. هیچ API، Migration،
Catalog/Template seed، Worker dispatch/DI registration، feature flag، UI یا Production setting
اضافه یا فعال نشده است. F06 اکنون
`Renderer/Golden Safe Checkpoint / Catalog/API/Worker Not Implemented` و F07 تا F10 همچنان
`Required / Not Implemented` هستند؛ RPT1 و PMCS V1.1 بسته، Qualified، Final یا Locked نیستند.
