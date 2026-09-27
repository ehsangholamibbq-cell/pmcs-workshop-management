# PMCS V1.1 — قرارداد معنایی گزارش Governance و Action

- شناسه: `PMCS-RPT1-F09-SEMANTIC-001`
- نسخه: `1.0.0`
- خانواده: `RPT1-F09`
- وضعیت: `DoR / Semantic Contract Locked | Source/Runtime MS32 و Renderer/Golden MS33 checkpointed | producer/Catalog/API/Worker open`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS30-C1`
- مرز مالکیت: `Pmcs.Modules.ActionControl` برای Issue، Risk، Decision Request/Record، Escalation Thread و Management Action
- MS31 contract-only Runtime / Renderer / Migration / Catalog / API / Worker change: None

## ۱. هدف، مالکیت و استقلال

F09 پنج دفتر مستقل Issue، Risk، Decision، Escalation و Action را در یک Tenant/Project و
cutoff واحد گزارش می‌کند. Decision Request و Decision Record دو واقعیت مستقل در بخش Decision
هستند؛ Request ارسال‌شده به معنی Decision نیست. `ActionControl` مالک Source و قواعد دامنه است.
Reporting فقط Application Contract خواندنی، نسخه‌دار، bounded و cutoff-aware مالک را مصرف
می‌کند. دسترسی مستقیم به `ActionControlDbContext`، SQL آن، `GET /governance`،
`GET /actions`، DTOهای Endpoint، Projection زندهٔ Portfolio یا log متنی Audit ممنوع است.
Governance endpoint اکنون matrix/rule را `Take(100)`، دفترها را `Take(200)` و Action endpoint
را `Take(200)` می‌کند؛ فیلتر حساس و وضعیت جاری آن‌ها اثبات پوشش یا history نیست.

Issue واقعیت رخ‌داده است؛ Risk عدم قطعیت آینده، Action وظیفهٔ محول‌شده، Decision اختیار
انسانی ثبت‌شده و Escalation اعلام هشدار است. `Materialized` شدن Risk یک Issue پیوندخورده
می‌سازد، نه تبدیل خود Risk به Issue و نه دوباره‌شماری در همان دفتر. Escalation یا
Acknowledgement وضعیت/مالک/شدت منبع را تغییر نمی‌دهد. Action ناشی از Attention ثبت‌شده
خودبه‌خود Issue یا closure آن نیست. F09 به F01–F08، Daily Fact، HSE، Finance، WBS، Budget،
Project State یا Agent join ندارد و composite health، مبلغ، احتمال پیش‌بینی‌شده یا توصیهٔ AI
نمی‌سازد. Source link فقط با هویت و scope ثابت برای lineage محافظت‌شده بررسی می‌شود.

## ۲. پارامتر، هویت و cutoff

Client فقط JSON object دقیقاً خالی `{}` می‌فرستد. `status`، `type`، `owner`، `severity`،
`includeSensitive`، `actionId`، `sourceId`، `period`، تاریخ محلی، `page`، SQL و هر کلید
اضافی reject می‌شود. Tenant/Project از Actor و route مجاز، و `sourceCutoffUtc` فقط از
`asOfUtc` پین‌شدهٔ Run می‌آیند. Server هویت/کد/نام/Revision پروژه، Time Zone، زمان پذیرش،
`cutoffLocalDate` مشتق از cutoff و policy/schema version را pin می‌کند. زمان آینده، Time Zone
نامعتبر، تغییر نسخهٔ منبع یا drift هویت پروژه در replay به failure قطعی می‌رسد؛ UTC یا
ساعت سیستم جایگزین Time Zone ناشناخته نمی‌شود.

رویداد رسمی تنها وقتی در وضعیت cutoff اثر دارد که `eventAtUtc <= sourceCutoffUtc` و ترتیب
رویدادها قطعی باشد. `CreatedAt`، `LastChangedAt`، `LastRaisedAt` یا Status فعلی به‌جای
chronology میانی به گذشته تعمیم نمی‌یابد. موعدهای `DateOnly` با تقویم محلی همان پروژه
سنجیده می‌شوند؛ overdue روزانه تنها پس از عبور از روز موعد است. `SlaDueAt` یک UTC instant
جداست و قانون SLA نسخه‌دار، duration unit و Project working week/time zone لازم دارد؛
نبود تقویم برای `ProjectWorkingDays` برابر `SetupRequired` است، نه محاسبهٔ حدسی.
`DecidedAt` (زمان تصمیم واقعی) و `RecordedAt` (زمان ورود به سامانه) جدا می‌مانند:
تصمیم verbal دیرثبت‌شده پیش از `RecordedAt` قابل انتشار در Snapshot قبلی نیست.
`EffectiveDate` نیز با ثبت، تصویب یا supersession یکی فرض نمی‌شود.

## ۳. Source و اثبات پوشش

در MS32 مالک باید Source پنج‌دفترهٔ read-only با scope Tenant/Project، transaction
repeatable-read، continuation یا bound قطعی، count کامل، digest هر دفتر، watermark،
source/policy schema version، coverage interval، configuration/matrix/SLA version و
manifest قابل بازپخش عرضه کند. سقف row/event/lineage/bytes/time پیش از ساخت Snapshot
enforce می‌شود. ترتیب canonical هر دفتر با `Number`/`Id` (Action بدون شماره با `Id`)،
رویدادها با `eventAtUtc`/sequence/Id، و hash مستقل از page/query order/RunId است.
duplicate ID، cycle در supersession، thread key تکراری ناسازگار، cross-project link،
watermark نامعتبر، overflow و تغییر digest بدون event معتبر failure هستند؛ truncate یا
استفاده از `Take(200)` به‌عنوان proof ممنوع است. برای lineage cross-module هیچ متن یا
دادهٔ ماژول دیگر خوانده نمی‌شود.

مدل کنونی همهٔ transitionها را نگه نمی‌دارد: Issue فقط برخی زمان‌های Resolve/Close و
`LastChangedAt` دارد؛ Risk زمان آخرین Review/Close را نگه می‌دارد؛ DecisionRequest
chronology میانی ندارد؛ `DecisionRecord.Supersede` و `ReviewEffect` زمان مستقل ندارند؛
Escalation `Touch` و `CloseFromSource` مقدار `LastRaisedAt` را تغییر می‌دهند و log
تاریخی وضعیت ندارند؛ ManagementAction فقط آخرین تغییر و Completion را دارد.
پس current row، Audit prose یا max timestamp برای cutoff قدیمی قابل اتکا نیست.
Producer تازه باید transitionهای معتبر را در owner ثبت کند و legacy را بدون backfill
حدسی علامت بزند. Source باید نقص chronology را per-section با reason گزارش کند؛ هیچ
«latest-row» یا «تغییر پس از cutoff را نادیده بگیر» مجاز نیست.

## ۴. پنج بخش معنایی

| بخش | fact رسمی و وضعیت‌ها | مرز عدد/موعد |
| --- | --- | --- |
| Issue | Create رسمی؛ Open/Assessment/Response/PendingVerification/Resolved/Closed/Reopened/NotAnIssue/Void جدا | Resolved بدون verifier مستقل Closed نیست؛ closure evidence و دو actor جدا لازم‌اند. TargetResolutionDate و SLA دو deadline جدا هستند. |
| Risk | Proposed بدون امتیاز؛ Assessed، Active/Monitoring، Materialized، Expired/Closed/Reopened جدا | امتیاز inherent و residual با matrix/formula version زمان ارزیابی و انسان؛ `ResidualRating ?? InherentRating` فقط وقتی هر کدام معتبر است، نه حدس از Proposed. MaterializedIssueId lineage است. |
| Decision | Request Draft/ReadyForDecision/InDecision/MoreInformationRequired/Withdrawn/Decided/Implementing/EffectReviewed/Closed و Record/EffectReviewed/Superseded جدا | Draft رسمیِ ارسال‌شده نیست؛ دو گزینه و authority برای ارسال/تصمیم لازم‌اند. انتخاب گزینهٔ ثبت‌شده، زنجیرهٔ immutable supersession و اثرسنجی مستقل‌اند. شمارش Request و Record جدا می‌ماند. |
| Escalation | فقط `Raise` واقعی، با key منبع/دلیل/سطح و occurrence؛ Open/Acknowledged/ClosedBySourceResolution جدا | DueSoon reminder یا Overdue signal بدون Thread، Escalation نیست. Ack resolution نیست؛ closed فقط با تعیین‌تکلیف منبع، نه timestamp آخرین raise. |
| Action | Create رسمی از Attention approved، Open/InProgress/Blocked/Done/Cancelled | Done برابر verification یا closure Issue/Decision نیست؛ DueDate محلی و CompletedAt مستقل‌اند. `SourceFactId` مجوز خواندن Daily Fact یا classification قابل استنتاج نمی‌دهد. |

Matrix و SLA rule version configuration هستند، نه fact عملکردی. `SetupRequired` در نبود
matrix/rule لازم، status خود رکورد را پاک نمی‌کند؛ metric وابسته مثل risk score یا SLA
overdue با `null` و reason صریح می‌ماند. هیچ «critical count» از Proposed بدون assessment
یا outlook با denominator ناقص ساخته نمی‌شود. Reminder، overdue و escalation سه خروجی
معنایی مجزا دارند. Count هر دفتر فقط در صورت پوشش کامل همان دفتر منتشر می‌شود؛
summary پنج‌گانه هرگز از بخش‌های ناقص جمع زده نمی‌شود.

## ۵. Permission و Classification

Definition واحد F09 علاوه بر gateهای Reporting، هر سه Source permission
`governance.read`، `governance.sensitive.read` و `actions.read` را در scope یکسان
به‌صورت whole-definition نیاز دارد؛ هر کمبود در Catalog، Create، List/Get، Retry/Cancel،
Download/Verify و Worker recheck deny می‌شود. فیلترکردن محرمانه‌ها برای کاربر فاقد
`governance.sensitive.read`، حتی در count یا تشخیص NoData، ممنوع است. Role/Grant/IAM
در MS31 تغییر نمی‌کند. `PortfolioViewer` با governance.read و actions.read، بدون مجوز
sensitive، مجاز به F09 نیست. `ProjectController` سه permission را دارد؛ دسترسی نهایی
به scope فعال و Reporting gates وابسته می‌ماند.

Definition حداقل `Confidential` است. `GeneralProject` در این Definition به همین حداقل
می‌رسد؛ `RestrictedManagement`، `ConfidentialHse` و `CommercialSensitive` همگی
حداقل `Restricted` هستند و policy انتشار Restricted باید در Source اثبات شود.
Unknown confidentiality و نسخهٔ policy نامعتبر failure هستند. `ManagementAction`
فعلی classification صریح ندارد؛ `SourceFactId` برای تشخیص General/Sensitive کافی نیست.
مالک باید پیش از انتشار Certified برای هر Action classification/provenance معتبر و
نسخه‌دار عرضه کند یا کل Run را fail-closed کند. حذف خاموش Action یا downgrade به
General ممنوع است. Permission اضافهٔ HSE/Finance از پیوندها حدس زده نمی‌شود؛ هیچ
محتوای آن منابع به F09 وارد نمی‌شود.

در Runtime محدود MS32، policy مالک `all-management-actions-restricted/v1` به هر Action
به‌صورت یکنواخت `Restricted` می‌دهد و version آن در manifest ثبت می‌شود. این
classification محافظه‌کارانه به `SourceFactId` یا محتوای Daily Fact اتکا ندارد؛
متن Action، نام اشخاص و SourceFactId به renderer عبور نمی‌کنند. تغییر این policy
فقط با نسخه/Golden و gate امنیتی مستقل مجاز است.

Snapshot/Renderer فقط Number/Id رسمی، وضعیت، زمان/موعد، severity/rating نسخه‌دار و
aggregate مجاز را دریافت می‌کند. Facts/Assumptions/Predictions، narrative حساس،
Evidence URI/bytes، selected option/rationale، recipient/assignee/authority name،
source snapshot و acknowledgement note منتشر نمی‌شوند. IDهای رابطه و نسخه‌ها در
manifest محافظت‌شده باقی می‌مانند. Classification خروجی بیشینهٔ Definition/Source و
پنج بخش است و در Snapshot، Document و download ثابت می‌ماند؛ ناشناخته fail-closed.

## ۶. وضعیت داده و failure boundary

هر بخش `NotConfigured`، `NoData`، `InsufficientData` یا `Available` همراه
`OfficialCount: int?` و reason نسخه‌دار دارد. `NotConfigured` فقط با proof نسخه‌دار
Source-disabled در cutoff؛ نبود matrix/SLA یا خطای Source دلیل NotConfigured نیست.
`NoData` فقط برای Source فعال، پوشش کامل و صفر fact رسمی همان بخش است. `Available`
به حداقل یک fact رسمی و chronology/classification کامل همان بخش نیاز دارد.
`InsufficientData` با count=`null` برای Source فعال و history، configuration یا
coverage ناقص است؛ هیچ subtotal ناقص یا صفر ساختگی منتشر نمی‌شود. پنج بخش مستقل‌اند:
نقص chronology یک بخش، بخش کامل دیگر را به صفر تبدیل نمی‌کند. وضعیت کل با هر بخش
InsufficientData همان است؛ اگر همه NotConfigured باشند NotConfigured؛ اگر بخش کامل
fact رسمی داشته باشد Available؛ و با دست‌کم یک بخش فعالِ کامل و صفر fact رسمی در
همهٔ بخش‌های فعال، NoData است. بخش‌های NotConfigured در حالت مختلط جدا نمایش داده می‌شوند.

Reasonهای حداقلی: `GovernanceSourceNotConfigured`، `NoOfficialIssue`،
`NoAssessedRisk` (فقط metric امتیاز، نه حذف Risk پیشنهادی)، `NoSubmittedDecision`،
`NoRaisedEscalation`، `NoOfficialAction`، `HistoricalTransitionUnavailable`،
`SourceCoverageIncomplete`، `MatrixVersionUnavailable`، `SlaRuleUnavailable`،
`WorkingCalendarUnavailable`، `ActionClassificationUnavailable` و
`RestrictedPublicationUnavailable`. Ambiguous Tenant/Project، classification،
source-to-risk/issue/request/thread linkage، permission revocation، manifest tamper و
duplicate/cycle/overflow security or integrity failure هستند؛ به InsufficientData
یا NoData تنزل نمی‌یابند. Reasonِ `ActionClassificationUnavailable` تنها برای
تشخیص داخلی است؛ خروجی Certified با Action مبهم fail-closed می‌شود.

## ۷. DoR، ترتیب پیاده‌سازی و Gate

| شناسه | Fixture / پذیرش مستقل F09 | انتظار |
| --- | --- | --- |
| `F09-I01` | Issue Resolve و Close در دو سوی cutoff | Resolved با Closed یا دو actor یکی نشود |
| `F09-I02` | Reopen پس از Close | Closure قدیمی در cutoff پیشین حفظ شود |
| `F09-I03` | Risk materialize به Issue | دو دفتر و lineage بدون double counting |
| `F09-R01` | Proposed، Assessed، Active | Proposed امتیاز/critical ساختگی نگیرد |
| `F09-R02` | inherent، residual، matrix version | assessment قدیمی با matrix جدید بازمحاسبه نشود |
| `F09-R03` | Close/Expire/Reopen | وضعیت و review deadline در cutoff درست بماند |
| `F09-D01` | Draft، submit، begin | Draft Decision یا Request رسمی شمرده نشود |
| `F09-D02` | verbal DecidedAt پیش از RecordedAt | تصمیم پیش از ثبت در Snapshot منتشر نشود |
| `F09-D03` | Supersede و EffectReview | زنجیره و chronology مستقل؛ legacy ناقص اعلام شود |
| `F09-E01` | reminder، overdue، Raise | فقط Raise، Escalation Thread واقعی است |
| `F09-E02` | چند Raise با key واحد | dedup/occurrence بدون thread تکراری |
| `F09-E03` | Acknowledge و source resolution | Ack closure نیست؛ closed زمان واقعی لازم دارد |
| `F09-A01` | Action از approved Attention | Action Issue یا Daily Fact join نیست |
| `F09-A02` | Open/Blocked/Done/Cancelled | Done verification Issue نیست؛ موعد محلی جدا |
| `F09-A03` | Action بدون classification | کل Run fail-closed؛ عنوان/تعداد نشت نکند |
| `F09-C01` | `/governance` و `/actions` بیش از cap | sample/current response Source Certified نشود |
| `F09-C02` | transition میانی legacy پس از cutoff | `InsufficientData` با count=`null`؛ current status fallback نشود |
| `F09-C03` | Disabled اثبات‌شده، active empty، partial | NotConfigured/NoData/InsufficientData جدا |
| `F09-C04` | matrix/SLA/calendar ناقص | metric وابسته null؛ SetupRequired و موعد حدسی ممنوع |
| `F09-T01` | cutoff در UTC و مرز روز محلی | date-only و SLA instant قاطی نشوند |
| `F09-P01` | revoke هر یک از سه Source permission | whole-definition در همهٔ مسیرها deny شود |
| `F09-P02` | حساس Restricted و unknown classification | max classification یا failure، بدون فیلتر خاموش |
| `F09-P03` | cross-tenant/project link و thread | failure بدون metadata leakage |
| `F09-S01` | ترتیب query/page متفاوت | semantic/manifest hash برابر |
| `F09-B01` | bound، duplicate، cycle، watermark gap | failure بدون truncate یا عدد ناقص |
| `F09-X01` | هر property اضافه به `{}` | strict reject |
| `F09-G01` | PDF/XLSX پنج بخش + incomplete | status/count مستقل، `null` آشکار، بدون formula |

DoR: owner/source/permission/classification/cutoff و missing-history policy اینجا pin شدند؛
fixtureهای جدول مبنای Unit، integration و semantic/renderer Golden مستقل خواهند بود.
Micro-Step بعد `S07-MS32` فقط Source مالک و Runtime Core محدود است. سپس Renderer/Golden
مستقل، و بعد Migration/Definition، Catalog/API/Worker و qualification متصل هرکدام
Candidate/CI جدا دارند. UI/UX2، Production enablement، F10 و Report Designer خارج از
MS31 هستند. همهٔ defaultهای Reporting/OutputAccess/Worker خاموش و
`PdfLicense=Unconfigured` باقی می‌مانند.

## ۸. تحقق محدود Source/Runtime در MS32

`PMCS-V1.1-RPT1-S07-MS32-C1` با Source مالک هشت register و Snapshot builder
پنج‌بخشی در Run 242 (`36321908108`) هر هشت Job را سبز کرد. سقف خواندن
`20,000` در هر register، `60,000` semantic fact و `8 MiB` برای JSON Snapshot
است؛ identity/link/cycle، Project/cutoff، classification و digest کنترل می‌شوند.
Decision Request پس از اولین Submit و DecisionRecord پس از transition بدون ledger
تاریخی `InsufficientData` با count=`null` می‌شوند. هیچ تاریخ ساختگی برای legacy
یا join به Fact انجام نشده است. Renderer/Golden در MS33 و producer تاریخچه و
wiring در Micro-Stepهای مستقل بعدی باقی می‌مانند؛ Migration ۵۱ و defaults خاموش‌اند.

## ۹. تحقق Renderer/Golden در MS33

`PMCS-V1.1-RPT1-S07-MS33-C1` با PDF پنج صفحه و XLSX هفت Sheet، validation
Snapshot/Template/Render request، status/count مستقل، Action Restricted و Golden
byte/visual در Run 245 (`36324102914`) هر هشت Job را سبز کرد. Template digest
`7fcb7af589562b09850491fe88d7067b0e20297d8fdb03148b0408db97b52a01`
برای Layout نسخه‌دار F09 pin شد. Producer تاریخچه و wiring هنوز بازند؛ MS34 فقط
transitionهای تازهٔ owner را بدون backfill حدسی ثبت می‌کند.

## ۱۰. تحقق Producer در MS34

`PMCS-V1.1-RPT1-S07-MS34-C1` شش ledger nullable و زمان‌دار مالک را با
Migration ۵۲ در Run 248 (`36327282876`) و هشت Job سبز بست. Legacy null
به‌صورت حدسی پر نمی‌شود و transition جدید آن را کامل نمی‌کند. این تنها
تولید تاریخچه است؛ Source selector cutoff-aware در MS35 و wiring/Qualification
متصل در MS36 گام‌های جدا هستند.
