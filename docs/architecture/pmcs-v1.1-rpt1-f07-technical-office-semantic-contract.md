# PMCS V1.1 — قرارداد معنایی گزارش دفتر فنی

- شناسه: `PMCS-RPT1-F07-SEMANTIC-001`
- نسخه: `1.0.0`
- خانواده: `RPT1-F07`
- وضعیت: `Semantic Contract Candidate | Runtime Not Implemented | F08-F10/UI/Production open`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS21-C1`
- مرز: `Document / RFI / Submittal / Transmittal`
- Runtime / Renderer / Migration / Catalog / API / Worker change: None

## ۱. هدف و مرز مالکیت

F07 وضعیت رسمی، قابل ردیابی و مقید به cutoff دفتر فنی همان Tenant/Project را در چهار دفتر مستقل
Document/Revision، Transmittal، RFI و Submittal گزارش می‌کند. منشأ حقیقت فقط
`Pmcs.Modules.TechnicalOffice` از طریق Application Contract خواندنی و نسخه‌دار آن است؛ Reporting
حق خواندن `TechnicalOfficeDbContext`، جدول‌های دفتر فنی، HTTP `GET /state` یا JSON پاسخ Endpoint
را ندارد. `GET /state` امروز به‌ترتیب ۵۰۰ Document، ۱۰۰۰ Revision و ۵۰۰ ردیف برای هر دفتر دیگر
محدود است و وضعیت جاری را نشان می‌دهد؛ منبع گزارش Certified یا Snapshot تاریخی نیست.

F07 به‌صورت صریح از F01 تا F06 مستقل است: هیچ گردش مالی/تعهد F05، قرارداد/خرید/تأمین F06،
Progress/WBS اجباری، Agent diagnosis، درصد تکمیل یا امتیاز کیفی از رکورد فنی استنتاج نمی‌شود.
`ContractId`، `CommitmentId`، `SourceIssueId`، `WbsReference`، `WorkItemReference` و
`LocationReference` فقط پیوند/metadata اختیاری همان منبع‌اند و مجوز join به Domain دیگر نمی‌دهند.
نبود Contract، WBS یا Budget مانع Source معتبر F07 نیست. محتوای باینری فایل و متن خام Evidence یا
Correspondence در Snapshot/Renderer قرار نمی‌گیرد؛ کنترل اصالت فایل برای مسیر رسمی Evidence/Documents
جدا باقی می‌ماند. گزارش Snapshot است و هیچ approval، issue، acknowledge یا close تازه انجام نمی‌دهد.

## ۲. پارامتر، cutoff و Project evidence

ورودی معنایی Client دقیقاً JSON object خالی `{}` است. حتی یک کلید مانند `documentId`،
`revisionId`، `rfiId`، `submittalId`، `transmittalId`، `status`، `discipline`، تاریخ محلی،
`includeConfidential`، `fileReference`، `contractId`، `wbsReference`، `page`، SQL، Query یا
Source selector رد می‌شود. `projectId` فقط از route معتبر و `sourceCutoffUtc` فقط از `asOfUtc`
پین‌شدهٔ Run می‌آیند؛ زمان آینده یا نامعتبر رد می‌شود.

Server هنگام پذیرش Run، Tenant/Project، کد/نام/Revision پروژه، Time Zone معتبر، زمان پذیرش،
`sourceCutoffUtc` و `cutoffLocalDate` را pin می‌کند. سیاست source-selection، versionهای projection،
permissions و classification باید در manifest ثبت شوند. تغییر Project identity/Time Zone یا تغییر
نسخه/Scope Source پس از پذیرش نباید Snapshot بازپخش‌شده را خاموش تغییر دهد؛ mismatch به failure
قطعی منجر می‌شود. fallback به UTC یا تاریخ سیستم در Time Zone ناشناخته مجاز نیست. تاریخ‌های
`DateOnly` دفتر فنی برحسب تقویم محلی پروژه در همان cutoff تفسیر می‌شوند؛ تبدیل به Instant بدون
Time Zone/قاعده نسخه‌دار ممنوع است. همهٔ Instantهای transition به UTC نرمال می‌شوند و مرز
`eventAtUtc <= sourceCutoffUtc` برای هر event اعمال می‌شود.

## ۳. Source lineage و شکاف تاریخی فعلی

مالک TechnicalOffice در Micro-Step Runtime باید یک
`IProjectTechnicalOfficeReportingSource` خواندنی و cutoff-aware عرضه کند. شکل دقیق type/schema
و version در همان Micro-Step نهایی می‌شود. Source باید فقط یک Tenant/Project را با bound و
continuation/manifest کامل بخواند و Document، Revision، Transmittal، RFI/response و
Submittal/review را همراه با شناسه، ترتیب event و proof of completeness برگرداند. مالک TechnicalOffice
وظیفهٔ انتخاب وضعیت رسمی در cutoff، اعتبار Tenant/Project، linkها، منحصربه‌فرد بودن نسخه و
historical classification را دارد؛ Reporting فقط projection معتبر را validate و Snapshot می‌سازد.

مدل جاری `TechnicalDocumentRevision`، زمان Submit/Review/Issue/Supersede را نگه می‌دارد، اما
مدل‌های RFI/Submittal و Transmittal تمام زمان‌های intermediate transition و
configuration/classification تاریخی را برای بازسازی هر cutoff ندارند. بنابراین استفاده از
`Status` کنونی برای cutoff گذشته یا حدس‌زدن زمان `InternalReview`، `ResponseAccepted`،
`ClarificationRequired`، `UnderReview` و `ApprovedAsNoted` ممنوع است. Source نسخه‌دار بعدی باید
event lineage موردنیاز و coverage تا cutoff را با contract ثابت فراهم کند. تا آن زمان بخش
وابسته `InsufficientData` با reason صریح می‌شود، یا اگر هویت/مرز امنیتی/تاریخی مبهم باشد کل
پردازش fail-closed می‌شود؛ هیچ `latest-row`، audit text یا Endpoint snapshot جای تاریخچه را
نمی‌گیرد. گزارش current-only حتی با cutoff برابر اکنون بدون completeness proof Certified نیست.

Manifest شامل source schema/policy version، watermark، تعداد/رنج/هش رکوردها و eventها،
completeness per section، Tenant/Project، cutoff، classification و source semantic hash است.
Page limit به معنی کامل‌بودن نیست. duplicate event، same-ID با payload متناقض، دو
`CurrentOfficialRevisionId` ناسازگار، حلقهٔ supersession، link cross-project، gap حل‌نشده،
cutoff/manifest ناسازگار یا هش نامعتبر processing failure هستند و نباید silently skip شوند.

## ۴. Document و Revision رسمی

`TechnicalDocument.Number` را سرور با پیشوند `DOC` تولید می‌کند؛ Client number ایجاد نمی‌کند.
`Type`، `Discipline`، `Originator` و پیوندهای اختیاری فقط طبق نسخهٔ واجد cutoff وارد خلاصه می‌شوند.
`RevisionCode` در هر Document یکتا است؛ `RevisionDate`، `Purpose`، نام فایل، SHA-256 و
`SupersedesRevisionId` به lineage تعلق دارند. `Purpose=Approved` یا `ForConstruction`،
`Status=Approved`، وجود hash یا بارگذاری فایل، صدور رسمی را ثابت نمی‌کند.

فقط Revision `Approved` که از طریق Transmittal متعلق به همان Tenant/Project با event `Issued`
تا cutoff ابلاغ شده و `IssuedThroughTransmittalId` معتبر دارد، «official issued» است.
`CurrentOfficialRevisionId` در cutoff از زنجیرهٔ issue/supersede آن زمان بازسازی می‌شود؛
فیلد جاری Document برای cutoff تاریخی truth نیست. در یک Transmittal صادرشده، Revisionهای مختلف
همان Document یا revision بدون Approval معتبر failure هستند. نسخهٔ قدیمی فقط از زمان صدور
نسخهٔ جانشین `Superseded` می‌شود و در تاریخ قبل از آن همچنان نسخهٔ رسمی است.
`Returned` و Draft/Submitted/Approved بدون Issue جدا در pending/review نمایش داده می‌شوند،
هرگز «ابلاغ‌شده» محسوب نمی‌شوند. نبود هیچ نسخهٔ صادرشده همراه completeness proof برابر `NoData`
بخش رسمی است، نه صفر ساختگی.

## ۵. Transmittal و Acknowledgment

`TechnicalTransmittal.Number` با پیشوند `TRN` سروری است. `Draft` تا زمان `Issue` اثر ابلاغ
ندارد. Issue معتبر فقط با revisionهای Approved، متعلق به همان Project، از Documentهای متمایز
و با `baseRevision` صحیح رخ می‌دهد. زمان `IssuedAt`، Actor صادرکننده، recipientهای نسخه‌دار،
purpose، channel و RevisionIds قابل ردیابی هستند. `Acknowledged` تنها پس از `Issued` و با
`AcknowledgedAt` و `AcknowledgmentReference` معتبر است؛ دریافت/تأیید دریافت با تأیید محتوای
Document یا صدور دوباره برابر نیست. Ack پس از cutoff در Snapshot تاریخی هنوز `Issued` و
`Unacknowledged` است. `DueResponseDate` فقط در Project local date و برای Transmittal صادرشدهٔ
باز و پاسخ‌داده‌نشده ارزیابی می‌شود: `DueResponseDate < cutoffLocalDate` overdue است؛ تساوی
overdue نیست. نبود due date به `NotAssessable` می‌رسد، نه `OnTime`. هیچ receipt یا delivery
خارجی از روی Ack ساختگی ایجاد نمی‌شود.

## ۶. RFI، پاسخ و اثر احتمالی

`TechnicalRfi.Number` با پیشوند `RFI` سروری است. Draft فقط پس از evidence reference معتبر
و `InternalReview` می‌تواند صادر شود. `Submitted` در Domain فعلی یعنی RFI صادرشده؛
`Answered` یعنی پاسخ خارجی دریافت شده، نه پذیرفته یا بسته‌شده؛ `ResponseAccepted` هم هنوز
`Closed` نیست. `ClarificationRequired` باز می‌ماند و پاسخ بعدی sequence تازه است. هر پاسخ
با `respondingParty` خارجی، `receivedBy` داخلی، `responseAt`، classification پاسخ،
`ReferencedRevisionIds` و نتیجه review/acceptance مستقل ثبت می‌شود؛ پاسخ‌دهنده با دریافت‌کننده
یکی فرض نمی‌شود. رکورد ReturnToDraft از جنس پاسخ خارجی نیست.

برای هر cutoff، `RaisedDate`، `RequiredByDate` و `IsBlocking` تنها روی RFIهای رسمیِ واجد
event issue اعمال می‌شوند؛ Draft/Review در شمارش «RFI باز» قرار نمی‌گیرند.
`RequiredByDate < cutoffLocalDate` فقط برای RFI صادرشده و هنوز `Closed` overdue است.
`PotentialImpact` و `changePotential` پرچم بررسی‌اند: زمان تمدید، مبلغ اصلاحیه، تغییر طراحی،
دستور کار یا Approval قرارداد تولید نمی‌کنند. متون آزاد `Question`، `ProposedSolution` و
`Response.Text` و evidence URI از Snapshot مدیریتی حذف می‌شوند؛ فقط شناسه‌های allowlisted،
status، classification پاسخ و شمارنده‌های قابل اثبات وارد می‌شوند.

## ۷. Submittal، review و ارسال مجدد

`TechnicalSubmittal.Number` با پیشوند `SUB` سروری است. بسته حداقل یک DocumentRevision
متعلق به همان Tenant/Project دارد. `Draft → Submitted → UnderReview → outcome` به زمان
event معتبر مقید است. `Approved`، `ApprovedAsNoted`، `ReviseAndResubmit`، `Rejected`،
`ForInformation` و `Closed` در گزارش تفکیک می‌شوند. در Domain کنونی `ForInformation`
می‌تواند `Status=ApprovedAsNoted` داشته باشد؛ `ReviewOutcome` باید مستقل حفظ شود تا اشتباه
با Approval محتوا رخ ندهد. Rejected/ReviseAndResubmit دلیل معتبر می‌خواهند؛
Resubmission یک aggregate جدید با `SupersedesSubmittalId` و شماره چرخهٔ بعدی است، نه mutation
بستهٔ قبلی. Review یا Close به معنی Issue رسمی Document، تحویل مصالح یا Site Acceptance نیست.

`ReviewDueDate` قبل از `cutoffLocalDate` فقط برای بسته‌های `Submitted` یا `UnderReview` هنوز
بدون outcome، «review overdue» است. `RequiredByDate` و `PlannedSubmissionDate` جدا از
`ReviewDueDate` حفظ می‌شوند؛ عدم وجود هر کدام به zero delay یا `OnTime` تبدیل نمی‌شود.
پیوند `CommitmentId` فقط reference فنی است و F06 supply/acceptance را نمی‌سازد.

## ۸. Snapshot معنایی، وضعیت و reasonها

Projection گزارش چهار بخش مستقل و مرتب دارد:

| بخش | فیلدهای مجاز |
| --- | --- |
| Document/Revision | DocumentId/Number/Type/Discipline، current official revision id/code/date/issuedAt و شمارش pending/returned/superseded واجد cutoff |
| Transmittal | TransmittalId/Number/issuedAt، تعداد Revision، acknowledgment/due state و شمارش issued/acknowledged/unacknowledged |
| RFI | RfiId/Number/issue/response/accept/close status و تاریخ‌های اثبات‌شده، blocking/overdue و response classification/count |
| Submittal | SubmittalId/Number/Type/discipline/status/review outcome، چرخه ارسال و شمارش در انتظار/دیرکرد review |

همهٔ شناسه‌ها/ردیف‌ها با ترتیب ثابت `Number` سپس `Id`، eventها با `eventAtUtc` سپس
`sequence` و `Id` مرتب می‌شوند. Source/semantic hash از canonical payload، policy/schema
version و source manifest مشتق است و query order، RunId، زمان render یا pagination آن را تغییر
نمی‌دهد. Summary شمارش فقط برای مجموعهٔ با coverage کامل و scope معلوم قابل محاسبه است؛
unknown count برابر null می‌ماند و صفر جای آن نمی‌نشیند.

وضعیت هر بخش یکی از `NotConfigured`، `NoData`، `InsufficientData` یا `Available` است.
`NotConfigured` فقط با evidence نسخه‌دارِ غیرفعال بودن Source در cutoff صادر می‌شود؛
از missing config یا Error حدس زده نمی‌شود. `NoData` به proof of complete source و صفر fact رسمی
واجد cutoff نیاز دارد. `InsufficientData` یعنی Source مجاز ولی history/coverage fact لازم
ناقص است؛ بخش‌های مستقل سالم حفظ می‌شوند و amount/rate/overdue ناقص null هستند.
`Available` نیاز به حداقل یک fact رسمی و completeness همان بخش دارد. وضعیت کلی اگر تمام
بخش‌ها `NotConfigured` باشند همان است؛ اگر بخشی ناقص و هیچ failure امنیتی وجود نداشته باشد
`InsufficientData` است؛ اگر هیچ fact رسمی نباشد `NoData` وگرنه `Available` است.
Reasonهای نسخه‌دار: `TechnicalSourceNotConfigured`، `NoOfficialDocumentRevision`،
`NoIssuedTransmittal`، `NoIssuedRfi`، `NoSubmittedSubmittal`،
`HistoricalTransitionUnavailable`، `SourceCoverageIncomplete`،
`OfficialRevisionLineageInvalid`، `CrossProjectReference`، `ClassificationUnknown`،
`DueDateUnavailable` و `ReviewOutcomeUnavailable`. Reasonهای امنیتی/identity و lineage
متناقض به processing failure می‌رسند و نباید با `NoData` پوشانده شوند.

## ۹. Permission، Classification و حداقل‌سازی

Definition مستقل F07 علاوه بر gateهای Reporting، هر دو Source permission
`technical.read` و `technical.confidential.read` را برای کل Definition require می‌کند.
این انتخاب عمداً whole-definition است: وجود/عدم‌وجود Document محرمانه از حذف خاموش ردیف
یا aggregate لو نمی‌رود. Role، Permission mutation یا دسترسی F01–F06 به‌تنهایی F07 را باز
نمی‌کند. Catalog/List/Get/Create/Retry/Cancel/Download/Verify، read service و Worker در
مرحله اتصال باید همهٔ permissionها را در scope Tenant/Project ارزیابی و پیش از render/publish
دوباره بررسی کنند؛ idempotent replay هم gate را دور نمی‌زند.
`technical.documents.create`، `technical.rfis.respond`، `technical.submittals.review` یا
`technical.transmittals.issue` Source read substitute نیستند.

Classification حداقل `Confidential` است و بیشترین سطح معتبر میان Definition، Project،
Document/Revision و referenced recordهای همان Source به Output منتقل می‌شود.
`Restricted` فقط با Permission و policy مربوطه قابل انتشار است؛ مقدار ناشناخته/آزاد
`TechnicalDocument.Confidentiality` بدون mapping نسخه‌دار و approval معتبر failure است،
نه پیش‌فرض `Internal`. Metadata کم‌ضرر را نمی‌توان از پیوند به Document محرمانه جدا کرد و با
classification کمتر منتشر نمود. fileReference، SHA فایل، recipientهای شخصی،
`RespondingParty`، free text، review comments، evidence URI و محتوای سند در Snapshot/PDF/XLSX
حذف می‌شوند؛ IDهای lineage و hash فقط داخل manifest محافظت‌شده باقی می‌مانند. این حذف
همراه permission whole-definition انجام می‌شود و به معنی فیلترکردن خاموش داده برای Actor نیست.
هیچ پیوندی از Tenant/Project دیگر و هیچ URL دانلود عمومی مجاز نیست.

## ۱۰. Determinism، budget و failure boundary

Contract آینده باید سقف تعداد Document/Revision/RFI/Response/Submittal/Transmittal/event،
عمق lineage، bytes و زمان را نسخه‌دار کند و قبل از render enforcement داشته باشد.
Page/row limit باید به completeness proof همراه continuation ختم شود؛ overflow، cycle،
double issue ناسازگار یا طبقه‌بندی مبهم transient نیست و با status `NoData` یا خروجی ناقص
جایگزین نمی‌شود. تغییر permission پیش از انتشار یا دریافت فایل fail-closed است.
تفاوت Snapshot برای دو Run با Tenant/Project، cutoff، source version و semantic facts یکسان
ممنوع است. Semantic Golden جدا از PDF/XLSX Golden است.

## ۱۱. Golden matrix و acceptance سناریوها

| ID | Fixture | انتظار قطعی |
| --- | --- | --- |
| `F07-D01` | Draft/Approved Revision بدون Transmittal | official ندارد؛ approved با issued یکی نیست |
| `F07-D02` | Issue پیش/پس از cutoff | فقط Issue واجد cutoff رسمی است |
| `F07-D03` | Supersede در دو سوی cutoff | نسخه قدیم تا صدور جانشین رسمی می‌ماند |
| `F07-D04` | دو Revision از یک Document در یک Issue | failure؛ انتخاب latest ممنوع |
| `F07-D05` | Purpose برابر ForConstruction بدون Issue | purpose منشأ ابلاغ نیست |
| `F07-T01` | Issue و Acknowledge در دو سوی cutoff | Ack آینده در گذشته بازسازی نمی‌شود |
| `F07-T02` | موعد response برابر/قبل cutoff local date | فقط قبل از cutoff و بدون Ack overdue است |
| `F07-T03` | Transmittal بدون موعد | `NotAssessable`، نه `OnTime` |
| `F07-R01` | Draft/InternalReview/Submitted | فقط issued در شمارش RFI باز |
| `F07-R02` | Answered/ResponseAccepted/Closed | سه وضعیت جدا؛ پاسخ Accepted خودکار Closed نیست |
| `F07-R03` | Clarification و response دوم | sequence و review response مستقل |
| `F07-R04` | تغییر احتمالی Cost/Time | flag فقط بررسی؛ هیچ F05/F06 mutation |
| `F07-R05` | پاسخ خارجی و دریافت داخلی | respondingParty و receivedBy یکی فرض نمی‌شوند |
| `F07-S01` | Submitted/UnderReview و outcome | تأخیر review فقط در وضعیت بازِ واجد cutoff |
| `F07-S02` | ForInformation با Status=ApprovedAsNoted | ReviewOutcome جدا حفظ و Approval تلقی نمی‌شود |
| `F07-S03` | Rejected/ReviseAndResubmit | دلیل و resubmission aggregate تازه لازم است |
| `F07-S04` | Approved package بدون Document Issue | ابلاغ رسمی یا Site Acceptance نمی‌سازد |
| `F07-C01` | `GET /state` با >۵۰۰ رکورد | page cap proof of completeness نیست |
| `F07-C02` | history transition ناقص | `InsufficientData` یا failure؛ current status fallback ممنوع |
| `F07-C03` | no fact در Source کامل | `NoData` با count صفر قابل اثبات؛ draft نباید رسمی شود |
| `F07-C04` | unknown config/zone/source policy | failure؛ نه UTC یا Internal fallback |
| `F07-P01` | revoke هر کدام از دو read Permission | کل Definition/Run/Output deny |
| `F07-P02` | confidential/Restricted و unknown classification | propagation یا deny؛ no downgrade |
| `F07-P03` | ID متعلق به Tenant/Project دیگر | failure؛ صفر نشت metadata |
| `F07-H01` | دو Run با facts برابر و ترتیب query متفاوت | semantic/manifest hash برابر |
| `F07-B01` | overflow در event/page/lineage budget | failure بدون truncate/output ناقص |
| `F07-X01` | extra property در JSON `{}` | strict reject؛ Client scope/cutoff نمی‌سازد |

## ۱۲. Definition of Ready و Micro-Step بعدی

| Gate | تصمیم F07 |
| --- | --- |
| Scope و ownership چهار دفتر | بسته؛ فقط TechnicalOffice Application Contract |
| Strict parameters و Server-pinned project/cutoff | بسته؛ `{}` و UTC/project local date |
| Status رسمی Document/Transmittal/RFI/Submittal | بسته؛ Issue/Review/Ack جدا |
| Missing historical lineage و completeness proof | قاعده بسته؛ implementation در Runtime Core باز |
| Optional WBS/Contract/Budget و cross-domain boundary | بسته؛ هیچ join پنهان |
| Tenant/Project identity، version، hash و budget | قاعده بسته؛ runtime implementation باز |
| Source permissions و classification | بسته؛ هر دو `technical.read` و `technical.confidential.read`، حداقل Confidential |
| NoData/InsufficientData/NotConfigured و reasonها | بسته |
| Golden/negative matrix | بسته؛ ۲۷ سناریو |
| Runtime/Core source projection/selector/snapshot | باز |
| Renderer/Golden binary و Catalog/API/Worker wiring | باز |

گام بعدی `S07-MS23` فقط Runtime Core محدود F07 است: identity و schema نسخه‌دار،
Application Contract خواندنی و historical cutoff-aware در TechnicalOffice، completeness manifest،
selector/calculator/Snapshot builder و Unit/contract test. Runtime باید شکاف event history
مشخص‌شده در بخش ۳ را به‌صورت fail-closed حل کند. Renderer، Migration/Catalog/API/Worker wiring،
UI/UX2، Production enablement، Report Designer و F08 در MS23 مجاز نیستند.

## ۱۳. Gate statement

این متن فقط DoR و semantic contract مستقل F07 را candidate می‌کند. F01 تا F06
`Connected Safe Checkpoint` دارند؛ F07 هنوز Runtime، Renderer و مسیر اجرای Catalog/API/Worker
ندارد. RPT1 و PMCS V1.1 همچنان فعال و خارج از Qualified/Final/Locked هستند. Feature flagها،
Worker و Production defaults خاموش، `PdfLicense=Unconfigured` و
`OrphanRemediationMode=Disabled` باقی می‌مانند.
