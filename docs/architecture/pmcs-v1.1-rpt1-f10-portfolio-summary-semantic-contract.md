# PMCS V1.1 — قرارداد معنایی Portfolio Summary رسمی

- شناسه: `PMCS-RPT1-F10-SEMANTIC-001`
- نسخه: `1.0.0`
- خانواده: `RPT1-F10`
- وضعیت: `DoR / Semantic Contract Safe Checkpoint MS37 | Runtime/Renderer/Catalog/API/Worker open`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS36-C1`
- مرجع: Roadmap `PMCS-RM-POST-V1-001 v1.71.0`، ADR 0010، ADR 0029 و ADR 0031
- MS37 contract-only Runtime / Renderer / Migration / Catalog / API / Worker change: None

## ۱. دامنه و هویت

F10 یک گزارش **Portfolio** با Tenant scope و cutoff مشترک است؛ یک Project Report با
چند ردیف پروژه یا خروجی `/portfolio/command-center` نیست. بدنه شامل فهرست پروژه‌های
مجاز، وضعیت عملیاتی رسمی هر پروژه، وضعیت دسترسی به ابعاد مالی/تجاری، exposureهای
هم‌ارز و کیفیت داده است. `OverallHealth`، امتیاز/رنگ ترکیبی، مقایسهٔ واحدهای ناهمگون،
پیش‌بینی و متن AI ممنوع است. وضعیت عملیاتی `Stable` سلامت مالی یا سلامت کل پروژه نیست.

Definition آینده `portfolio-summary-certified/1.0.0`، `scope=Portfolio` و فرمت‌های
`Pdf`/`Xlsx` دارد. یک Run دارای `TenantId`، `Scope=Portfolio`، cohort نسخه‌دار
پروژه‌ها، requester و cutoff UTC است؛ `ProjectId` آن **nullable** و برای این scope
تهی است. هیچ GUID ساختگی، «پروژهٔ میزبان»، ProjectId یکی از اعضا یا Project
Document برای نمای Tenant-wide استفاده نمی‌شود. مسیر آینده از نوع
`/api/v1/portfolio/reports` است و تا MS37 ایجاد نمی‌شود. ReportRun/Snapshot/
Output/Generated Document و قرارداد publisher باید پیش از wiring، tenant-scope
واقعی و permission همان scope را به‌صورت migration نسخه‌دار پشتیبانی کنند؛ تا
آن زمان F10 نباید در Catalog ظاهر شود. Project Reporting F01–F09 و دادهٔ موجود
با این توسعه باید بدون تغییر semantic و با migration سازگار باقی بمانند.

## ۲. ورودی، cutoff و cohort

پارامتر Client دقیقاً object خالی `{}` است. `projectIds`، currency، rate، dimension،
includeSensitive، status، sort، page، snapshotId، dateLocal یا Query/SQL از Client
پذیرفته نمی‌شوند. `asOfUtc` آینده رد می‌شود. Server در پذیرش Run Tenant، actor،
زمان پذیرش، cutoff UTC، policy/schema version و cohort مجاز را pin می‌کند.
`portfolio.read` در Tenant و `project-state.read` در Project، تقاطع ورود پروژه‌اند.
`ListProfilesAsync` فقط با scope مجاز فراخوانی می‌شود؛ تمام پروژه‌ها خوانده و سپس
با خروجی فیلتر نمی‌شوند. cohort به‌ترتیب ثابت `ProjectId` مرتب و با hash/تعداد
پین می‌شود؛ هیچ تعداد یا شناسه‌ای از پروژه‌های غیرمجاز در Header، reason، logs یا
manifest قابل دانلود وارد نمی‌شود. صفر پروژهٔ مجاز `NoData` برای cohort مجاز است،
نه شاهدی بر صفر پروژهٔ Tenant.

زمان‌بندی پروژه‌ها ممکن است Time Zone متفاوت داشته باشد. هر `cutoffLocalDate` از
**همان** cutoff UTC و Zone معتبر پروژه محاسبه و در ردیف خود پین می‌شود؛ یک تاریخ
شمسی مشترک به‌عنوان روز محلی همه پروژه‌ها تحمیل نمی‌شود. ConfigurationVersion،
Revision، status، base currency و زمان تغییر پروفایل هر پروژه همراه منبع pin
می‌شوند. تغییر پس از cutoff به گذشته تعمیم داده نمی‌شود. پروژه‌ای که configuration
تاریخی‌اش در cutoff قابل اثبات نیست `InsufficientData` مستقل است، نه اینکه از
cohort حذف یا با profile جاری بازسازی شود. پروژه Suspended/OnHold با وضعیت واقعی
نمایش داده می‌شود؛ permission و membership جاری از زمان پذیرش مستقل‌اند.

## ۳. مالک Source و پوشش تاریخی

Reporting فقط Application Contractهای read-only مالک را orchestration می‌کند:
`IProjectDirectory` از Projects؛ `IProjectStateReportingSource` از
ProjectIntelligence برای Snapshot رسمی؛ Sourceهای cutoff-aware F05 از Finance و
F06 از Commercial برای exposure مالی و تعهد/خرید. مصرف مستقیم
`ProjectIntelligenceDbContext`، SQL schema ماژول دیگر، DTO/HTTP
`GET /api/v1/portfolio/command-center`، `IFinancialStateSource.GetPortfolioAsync`
و `ICommercialStateSource.GetPortfolioAsync` ممنوع است؛ این منابع current/on-demand
و فاقد proof تاریخی Certified هستند. F10، Snapshotهای F03/F05/F06 صادرشده را
بدون تغییر مالکیت دوباره مصرف یا selector/محاسبهٔ حوزه‌ای آن‌ها را بازپیاده‌سازی
نمی‌کند. قرارداد باریک owner برای projection/selection واحد و completeness لازم
است. هیچ خواندن مخفی F01/F02/F04/F07/F08/F09، Daily Fact، HSE یا Action نیست.

Source manifest به ازای هر پروژه/بعد: source contract/policy version، Tenant/Project،
cutoff UTC و local، source watermark، configuration و source snapshot/record IDs،
complete coverage، classification، row count و digest canonical دارد. Cross-module
transaction سراسری ادعا نمی‌شود؛ انتخاب هر مالک در cutoff با proof پوشش و watermark
همان مالک ثبت می‌شود. Duplicate project/record، source خارج Tenant، unsorted/overflow،
missing watermark، نسخه ناشناخته، hash mismatch، currency code نامعتبر، timestamp
بعد cutoff، revision متناقض یا manifest دستکاری‌شده failure پردازشی است؛ truncate
یا NoData ساختگی مجاز نیست. سقف ثابت `MaximumProjects=200`، سقف projection هر مالک
و بودجهٔ byte/time پیش از publication اعمال می‌شوند. عبور سقف fail-closed است و
به `Take(200)` یا حذف بی‌صدای پروژه ختم نمی‌شود.

## ۴. Permission و classification

درخواست/Worker/Download/Verify برای actor درخواست‌کننده، `portfolio.read` در
Tenant و `project-state.read` برای **تمام** cohort پین‌شده را دوباره می‌سنجند.
تغییر membership یا سلب دسترسی به یکی از اعضا، کل خروجی immutable را تا زمانی
که مجوز دوباره برقرار شود deny می‌کند؛ با حذف یک ردیف و hash جدید همان Run
پاسخ داده نمی‌شود. cohort مجاز جاری نباید گسترش پنهان به Run قبلی بدهد؛ تغییر
cohort برای Run جدید ثبت می‌شود. خروجی F10 فقط برای requester پین‌شده و
کاربر TenantAdministrator با recheck کامل همان dimensionها قابل دسترسی است؛
مجوز `reporting.output.download` و tenant-scope لازم است.

بعد مالی فقط وقتی `financial-state.read` و permissionهای منبع F05 برای آن پروژه
درخواست/پردازش/دریافت برقرارند وارد گزارش می‌شود؛ بعد Commercial نیز فقط با
`commercial-state.read` و permissionهای منبع F06. در غیر این صورت وضعیت آن بعد
`NotAuthorized` است، بدون مبلغ، تعداد، source ID یا علت قابل inference. هیچ
«صفر مالی» برای فقدان مجوز نیست. فهرست پروژه‌ها با همین dimension mask و
permission evidence immutable pin می‌شود؛ revoke هر permission پین‌شده کل Run
را deny می‌کند، افزایش مجوزها به خروجی قدیمی داده اضافه نمی‌کند. Cache و
idempotency به Tenant/requester/cohort/mask/parameters/asOf متصل‌اند.

Definition حداقل `Confidential` و هر project/dimension ممکن است `Restricted`
باشد؛ classification خروجی بیشینهٔ تمام موارد **منتشرشده** و policy است.
Unknown/restricted-publication-unavailable failure است؛ data status پوشش امنیتی
نیست. نام افراد، Action title، selected option، Evidence URI، byte و record
حساس از F09 یا منابع دیگری در F10 نیست. Manifest محافظت‌شده و log امن، هویت
پروژه‌های فاقد دسترسی را آشکار نمی‌کنند.

## ۵. مدل معنایی و ارز

Header فقط `AuthorizedProjectCount`، شمار lifecycle و `NoData`/
`InsufficientData` همان cohort، تعداد project/dimension **مجاز و کامل** و
گروه‌های هم‌ارز را دارد. «کل پروژه‌های Tenant»، `OverallHealth`، denominator
پنهان و نمایشگاه رتبه‌بندی ممنوع‌اند. Project row شامل هویت و lifecycle، cutoff
محلی، Operational/Coverage/Freshness/Confidence رسمی و محدودیت `isPartial`،
status مستقل Financial/Commercial، classification، reason و lineage است.
`Stable/Watch/AtRisk/Critical` صرفاً operational و فقط وقتی Source رسمی معتبر
است؛ نبود Snapshot `NoData` است و به Stable تبدیل نمی‌شود. `NotConfigured`،
`NotEnabled`، `SetupRequired`، `NoData`، `Suspended` و `InsufficientData` هر
کدام صریح می‌مانند و در مخرج شاخص نامربوط قرار نمی‌گیرند.

Exposure فقط برای currencyCode استاندارد و برابر در همان بعد/واحد جمع می‌شود:
`RecognizedSpend` و `ExternalNetCash` از F05، `TotalCommittedAmount` و
`OpenCommitmentAmount` از F06 با source و status رسمی. Finance و Commercial در
یک currency group می‌توانند ستون‌های **مجزا** داشته باشند؛ نه اینکه همهٔ
مبالغ با هم جمع شوند. IRR و USD هرگز بدون FX policy نسخه‌دار تبدیل، جمع یا
به یک «total» واحد ارائه نمی‌شوند. اختلاف base currency پروژه و currency منبع،
missing code، precision/overflow یا مبلغ فاقد proof owner fail-closed است؛
`0` فقط وقتی ledger کامل و صفر رسمی است. Currency exposures بر اساس code
uppercase ordinal، ردیف پروژه بر `ProjectId` و reason/manifest بر شناسهٔ ثابت
مرتب می‌شوند. Round فقط مطابق واحد پول و قاعدهٔ نسخه‌دار owner است.

## ۶. وضعیت و determinism

`NotAuthorized` وضعیت **بعد** است و به data status کلی یا count تبدیل نمی‌شود.
`NotConfigured/NotEnabled/SetupRequired/Suspended/NoData/InsufficientData/
Available` برای dimension مجاز جداست. هر پروژه با source موجود و ناقص همان
`InsufficientData` را نشان می‌دهد؛ عدم دسترسی یک بعد، پروژه را از cohort اصلی
حذف نمی‌کند. `NoData` مجموعه فقط وقتی cohort مجاز صفر یا همهٔ بخش‌های مجاز با
proof کامل و بدون Snapshot/Fact رسمی‌اند. اگر بخشی ناقص باشد status کل
`InsufficientData`؛ اگر حداقل بخش رسمی و همهٔ بخش‌های واردشده کامل باشند
`Available`. در جمع Exposure، بخش ناقص subtotal مجهول (`null`) و دلیل دارد؛
aggregate «در دسترس» فقط پروژه‌های دارای پوشش کامل را با شمار مشارکت‌کنندگان
صریح نشان می‌دهد و هرگز «کل Portfolio» نامیده نمی‌شود.

Semantic hash از cohort/mask پین‌شده، cutoff، project/version/selected source،
status/count/reason/classification و exposureهای مرتب ساخته می‌شود؛ RunId،
WorkerAt، PDF metadata، page order و زمان Retry در آن نیست. PDF/XLSX از Snapshot
immutable با identity/Template digest/verification code ساخته می‌شوند و Download
bytes را دوباره از Source نمی‌خواند. Golden باید replay، reorder، FX isolation،
RTL/شمسی و data gap را پوشش دهد.

## ۷. پذیرش مستقل و ترتیب Micro-Step

| شناسه | سناریو | انتظار |
| --- | --- | --- |
| `F10-P01` | دو پروژه مجاز و یک پروژه غیرمجاز | cohort، count و نام فقط از دو مجاز |
| `F10-P02` | صفر پروژهٔ مجاز با Tenant دارای پروژه | NoData بدون افشای Tenant total |
| `F10-P03` | membership revoke بین Create و Worker | کل Run deny؛ Snapshot منتشر نشود |
| `F10-P04` | permission revoke پس از success | Download/Verify deny؛ byte ثابت بماند |
| `F10-P05` | Grant تازه بعد Run | پروژه/dimension جدید به Snapshot قبلی اضافه نشود |
| `F10-P06` | finance/commercial فقط برای بخشی از cohort | `NotAuthorized` بدون count/amount leaked |
| `F10-P07` | catalog tenant-scope | F10 فقط در Catalog Portfolio و مجاز؛ F01–F09 project-scope بمانند |
| `F10-P08` | requester دیگر با permission ناقص | IDOR deny؛ re-filter immutable ممنوع |
| `F10-C01` | IRR و USD | دو گروه مستقل و هیچ converted/global total |
| `F10-C02` | Finance/Commercial هم‌ارز | چهار ستون مستقل و شمار project contributor |
| `F10-C03` | code currency ناشناخته/ناسازگار | failure؛ FX حدسی یا discard ممنوع |
| `F10-C04` | ledger کامل صفر در برابر permission گمشده | صفر رسمی در برابر NotAuthorized |
| `F10-C05` | یک source ناقص در گروه currency | subtotal ناقص null و پوشش/دلیل صریح |
| `F10-T01` | cutoff UTC واحد و دو Time Zone | local date هر پروژه مستقل و قابل replay |
| `F10-T02` | snapshot محاسبه‌شده پس cutoff | از گزارش قدیمی حذف و جدید بعد cutoff قابل انتخاب |
| `F10-T03` | profile/version بعد cutoff | no current-state inference؛ InsufficientData |
| `F10-T04` | Correction بعد Run و retry | semantic hash و bytes از Snapshot قبلی ثابت |
| `F10-S01` | NoData/Stale/InsufficientData | operational flags و data status جدا از health |
| `F10-S02` | NotConfigured/NotEnabled/SetupRequired/Suspended | حالت‌ها صریح، denominator معتبر |
| `F10-S03` | mixed dimension authorization | operational موجود، Finance مخفی بدون صفر |
| `F10-S04` | restricted/classification unknown | max classification یا failure |
| `F10-B01` | ۲۰۰ پروژه و یک پروژه اضافه | ۲۰۰ مجاز؛ ۲۰۱ fail-closed، بدون truncation |
| `F10-B02` | duplicate ID/record/currency و cycle | failure قطعی؛ hash غیرقابل قبول |
| `F10-B03` | tenant/project mismatch، watermark/manifest tamper | failure و no output |
| `F10-O01` | tenant-scope Run/Snapshot/Output/Document | ProjectId تهی، lineage یکسان؛ Project route deny |
| `F10-O02` | strict `{}`/idempotency/retry | ورودی اضافی رد، replay یک Run، conflict امن |
| `F10-O03` | PDF/XLSX/Generated Document | bytes/hash/verify و retention/classification مستقل |
| `F10-O04` | migration و Restore | ۵۳ Migration فعلی سالم؛ افزوده فقط در Micro-Step مربوط |
| `F10-G01` | defaults و PR Draft | flagها off، PdfLicense Unconfigured، بدون Production enablement |
| `F10-G02` | Full Regression و QA | C#/Node/Web/browser/Restore و هشت GitHub Job، Checkpoint فقط پس از سبز |

MS37 فقط این DoR و Semantic Contract را ثبت می‌کند؛ Runtime، Renderer، Migration،
Definition/Template، Catalog/API/Worker، tenant-scoped storage، feature flag و
F10 output در این Micro-Step اضافه نمی‌شوند. Micro-Step بعد `S07-MS38` فقط
زیرساخت tenant-scope Run/Snapshot/Output و Documents owner contract را با migration
سازگار و تست fail-closed می‌بندد؛ سپس Source و Snapshot/selector F10، Renderer/
Golden، Catalog/API/Worker و Qualification متصل هرکدام Candidate و CI جدا دارند.
ADR 0010/0031 و ترتیب Roadmap حفظ می‌شود؛ RPT1 پس از F10 نیز بدون Gateهای
باقی‌مانده به Qualified یا Production ارتقا نمی‌یابد.
