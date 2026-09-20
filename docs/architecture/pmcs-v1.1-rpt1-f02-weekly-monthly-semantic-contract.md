# PMCS V1.1 — قرارداد معنایی گزارش هفتگی و ماهانه پروژه

- شناسه: `PMCS-RPT1-F02-SEMANTIC-001`
- نسخه: `1.2.1`
- خانواده: `RPT1-F02`
- وضعیت: `Renderer/Golden Safe Checkpoint | API/Worker/Catalog Not Implemented`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS03-C1`
- Runtime change: isolated renderer contract + deterministic PDF/XLSX only
- Migration / API / Catalog seed / Worker dispatch / UI change: None
- Parent evidence: checkpoint `d8fd4398309b08d1cdc90e26140c4581dc476636`؛ Run 140
- Renderer evidence: source `4f68f57de2c2a79b654a19128894d9c89878ab65`؛ Run 141

## ۱. هدف و مرز خانواده

F02 نمای دوره‌ای و قابل ممیزی گزارش‌های روزانه رسمی همان پروژه است. گزارش هفتگی و ماهانه فقط
نسخهٔ رسمی مؤثر هر تاریخ را در یک بازهٔ تقویمی ثابت جمع می‌کند و باید بتواند همان معنا را در یک
`asOfUtc` مشخص بازتولید کند.

این خانواده جایگزین گزارش مدیریتی، برنامه و S-Curve، مالی، قرارداد/خرید، دفتر فنی، Quality/HSE،
Issue/Risk یا Portfolio در F03 تا F10 نیست. دادهٔ آن ماژول‌ها، درصد پیشرفت، مبلغ، نرخ ارز، وضعیت
قرارداد، شاخص HSE یا KPI استنتاجی به F02 join نمی‌شود. Narrativeهای رسمی روزانه جداگانه و با منبع
نمایش داده می‌شوند؛ LLM آن‌ها را خلاصه، رتبه‌بندی یا به عدد تبدیل نمی‌کند.

## ۲. پارامترهای canonical

پارامترهای معنایی درخواست فقط این دو مقدار هستند؛ `projectId` از route/scope معتبر و `asOfUtc` از
قرارداد عمومی Run می‌آیند:

| فیلد | مقادیر و اعتبارسنجی |
| --- | --- |
| `periodKind` | دقیقاً `Weekly` یا `Monthly` |
| `periodStartLocalDate` | تاریخ مدنی ISO؛ برای Weekly باید شنبه و برای Monthly باید روز اول ماه شمسی در Time Zone پروژه باشد |

Server فیلدهای زیر را قطعی و در Snapshot ثبت می‌کند و Client حق ارسال یا override آن‌ها را ندارد:

- `periodEndLocalDateExclusive`: شنبهٔ بعد برای Weekly و روز اول ماه شمسی بعد برای Monthly؛
- `periodStartUtc` و `periodEndUtcExclusive`: تبدیل مرز محلی `00:00` با Time Zone pin‌شده پروژه؛
- `periodLabelFa`: فقط در Renderer از مرزهای canonical مشتق می‌شود و داخل Snapshot/hash ذخیره
  نمی‌شود؛
- `projectTimeZone`، Project revision و Configuration revision pin‌شده هنگام پذیرش Run؛
- `sourceCutoffUtc`: همان `asOfUtc` نرمال‌شده به UTC.

بازه نیمه‌باز `[periodStartLocalDate, periodEndLocalDateExclusive)` است. Weekly همیشه هفت روز مدنی
و Monthly دقیقاً یک ماه شمسی ۲۹، ۳۰ یا ۳۱ روزه است. `asOfUtc` آینده یا پیش از
`periodStartUtc` نامعتبر است. اجرای دوره‌ای که در cutoff هنوز پایان نیافته مجاز است، اما هرگز
`Available` اعلام نمی‌شود و reason صریح
`PeriodOpenAtCutoff` می‌گیرد. پارامتر end date، Time Zone، week number، Query، SQL، filter دلخواه
یا انتخاب دستی Source پذیرفته نمی‌شود.

تبدیل مرز محلی باید با Time Zone pin‌شده و بدون fallback به UTC انجام شود. Time Zone ناشناخته یا
مرز محلی ناموجود/مبهم باید fail-closed شود و Server حق انتخاب offset حدسی ندارد.

Runtime Core شناسه‌های `project-periodic-certified/1.0.0`،
`pmcs.reporting.project-periodic.parameters/v1` و
`pmcs.reporting.project-periodic.snapshot/v1` را pin می‌کند. Renderer checkpoint نیز Template
contract `1.0.0`، قرارداد `pmcs.reporting.project-periodic.renderer/v1` و layout
`pmcs.reporting.project-periodic.layout/v1` را pin می‌کند. این شناسه‌ها قرارداد کد هستند، نه رکورد
منتشرشدهٔ Catalog؛ `TemplateVersionId` واقعی تا Slice مستقل seed/wiring تخصیص داده نمی‌شود و F02
هنوز Definition قابل اجرا یا Output قابل دانلود منتشر نمی‌کند.

## ۳. Source lineage و مرز ماژولی

F02 فقط دو Application Contract خواندنی و نسخه‌دار را مصرف می‌کند:

1. Projects، هویت و پیکربندی pin‌شدهٔ پروژه شامل Time Zone، revision، تقویم کاری، cutoff روزانه،
   بسامد گزارش و workflow تأیید را می‌دهد؛
2. FieldOperations، ریشه‌های گزارش روزانه همان Tenant/Project و همان بازه را همراه نسخه‌های رسمی
   تا `asOfUtc` می‌دهد.

Runtime آینده باید یک Contract باریک period-read در FieldOperations اضافه کند؛ loop کردن endpoint
عمومی F01، import از `FieldOperationsDbContext` یا SQL روی schema ماژول دیگر ممنوع است. برای هر
تاریخ حداکثر یک root رسمی وجود دارد. Source response حداقل این lineage را نگه می‌دارد:

- `rootReportId`، `reportDate` و current official `reportId/versionNumber/revision` در cutoff؛
- `approvedAt`، `supersedesReportId`، `supersededByReportId` و `supersededAt` فقط در صورت مؤثربودن
  تا cutoff؛
- Factهای رسمی با `factId/copiedFromFactId/kind`، Location، quantity/unit، resourceCount، hours،
  impact، reference و measurement lineage؛
- Project/Configuration revision و زمان capture؛
- source manifest مرتب‌شده و SHA-256 آن.

Draft، Submitted، Returned، Rejected، نسخهٔ تأییدشده پس از cutoff و metadata یک correction آینده
وارد Snapshot نمی‌شوند. ترتیب canonical گزارش‌ها `reportDate → rootReportId` و ترتیب Factها
`kind → factId` است؛ ترتیب query/database نباید hash را تغییر دهد.

## ۴. قاعده cutoff و انتخاب نسخه رسمی

برای هر root داخل دوره، نسخه‌ای current official در cutoff است که:

1. `approvedAt <= asOfUtc`؛
2. `supersededAt is null || supersededAt > asOfUtc`؛
3. Tenant، Project و `reportDate` آن با Scope و بازه دقیقاً منطبق است؛
4. دقیقاً یک نسخه باید این شرط را پاس کند؛ صفر نسخه `OfficialVersionMissing` و بیش از یک نسخه
   invariant violation و failure امن است.

یک correction که بعد از cutoff تأیید می‌شود خروجی تاریخی را تغییر نمی‌دهد. همان پارامتر، Project
revision، Configuration revision، Template و Source cutoff باید semantic JSON، source manifest و
hash یکسان بسازند. cutoff پس از تأیید correction باید version/lineage و hash متفاوت بسازد. زمان
Worker، Run ID، retry/attempt، زمان render و identity فایل بخشی از hash معنایی نیستند.

`asOfUtc` cutoff دادهٔ رسمی است؛ پیکربندی پروژه با revision و زمان capture هنگام پذیرش Run pin
می‌شود. این دو زمان نباید با هم یکی فرض شوند و گزارش ادعای بازسازی پیکربندی تاریخی‌ای را که Source
نسخه‌دار ارائه نکرده است نمی‌کند.

## ۵. مدل معنایی خروجی

Snapshot F02 مستقل از PDF/XLSX است و حداقل این بخش‌ها را دارد:

- هویت پروژه، Time Zone و revisionهای pin‌شده؛
- نوع دوره، مرزهای محلی/UTC، cutoff و `periodClosedAtCutoff`؛
- `dataStatus`، reason codeهای مرتب‌شده و coverage واقعی؛
- تاریخ‌های مورد انتظار، تاریخ‌های دارای گزارش رسمی و تاریخ‌های فاقد گزارش؛
- نسخه رسمی منتخب هر روز و Factهای آن با lineage کامل؛
- شمار Factها بر اساس Kind؛
- جمع quantity فقط در bucketهای جداگانهٔ `(kind, sourceUnit)`؛ مقدار unit پس از validation Source
  به‌صورت ordinal استفاده می‌شود و alias، تغییر case یا conversion پنهان ندارد؛
- جمع `resourceCount` و `hours` فقط در Kind یکسان و با برچسب «جمع مشاهدات روزانه»، نه تعداد یکتای
  نفر/تجهیز؛
- Issue/Stoppageهای `High|Critical` فقط براساس impact صریح Source، بدون inference؛
- source manifest hash و semantic hash.

`WorkProgress` در F02 صرفاً Fact/quantity رسمی روزانه است. هیچ درصد کل، Planned/Actual/Variance،
earned value یا S-Curve از آن ساخته نمی‌شود. مقدار null به صفر تبدیل نمی‌شود و quantity بدون unit
در bucket جدا و صریح `UnitMissing` می‌ماند؛ چنین bucketی به جمع واحددار وارد نمی‌شود.

## ۶. Coverage و وضعیت‌های نبود داده

روز/slot مورد انتظار از پیکربندی pin‌شده پروژه به‌دست می‌آید:

- `Daily`: هر تاریخ مدنی که cutoff روزانهٔ تنظیم‌شدهٔ آن تا `asOfUtc` رسیده است؛
- `WorkingDays`: همین قاعده فقط برای روزهای فعال در `WorkingDaysMask`؛
- `Weekly`: برای هر intersection دوره با بازه شنبه تا جمعه، پس از cutoff آخرین روز آن intersection
  حداقل یک گزارش رسمی؛
- `NotConfigured`: قابل محاسبه نیست.

برای دوره باز، coverage فقط تا تاریخ محلی cutoff سنجیده می‌شود، اما خود دوره به‌دلیل
`PeriodOpenAtCutoff` همچنان `InsufficientData` است. وضعیت نهایی با اولویت زیر تعیین می‌شود:

| اولویت | وضعیت | قرارداد دقیق |
| --- | --- | --- |
| ۱ | `NotConfigured` | بسامد، workflow یا cutoff روزانه تنظیم نشده؛ یا حالت WorkingDays بدون تقویم/روز کاری معتبر است |
| ۲ | `NoData` | پیکربندی کافی است، اما هیچ current official report در بازه و cutoff وجود ندارد |
| ۳ | `InsufficientData` | حداقل یک گزارش رسمی وجود دارد، ولی دوره باز، slot مفقود، root بدون نسخه رسمی جاری یا گزارش رسمی بدون Fact است |
| ۴ | `Available` | دوره در cutoff بسته، تمام slotهای مورد انتظار پوشش‌دار، هر root دارای یک نسخه رسمی و هر گزارش دارای Fact است |

Reason codeهای allowlist عبارت‌اند از `ReportingCadenceMissing`، `DailyWorkflowMissing`،
`DailyCutoffMissing`، `WorkingCalendarMissing`، `PeriodOpenAtCutoff`، `ExpectedSlotMissing`،
`OfficialVersionMissing` و `OfficialReportEmpty`. Missing configuration یا
missing source عدد صفر، درصد صفر یا وضعیت سبز تولید نمی‌کند.

چهار مقدار جدول `dataStatus` هستند، نه failure پردازشی. Renderer فعلی برای
`NotConfigured`، `NoData` و `InsufficientData` نیز Artifact صریحِ بدون عدد ساختگی تولید می‌کند؛
فقط خطاهای پارامتر، امنیت، invariant یا زیرساخت Run را fail می‌کنند.

Permission denial، Tenant/Project mismatch، پارامتر نامعتبر، Time Zone نامعتبر و corruption
وضعیت داده نیستند؛ duplicate current official، duplicate root/date یا manifest conflict نیز باید
fail-closed و با diagnostic امن متوقف شوند، نه اینکه `InsufficientData` تولید کنند.

## ۷. Permission و Classification

- Catalog/View به `reporting.catalog.read` و `field.daily-reports.read` در همان Project نیاز دارد؛
- Create/Retry علاوه بر Source permission به `reporting.run.create` نیاز دارد؛
- Download/Verify علاوه بر Source permission به `reporting.output.download` و Classification جاری
  نیاز دارد؛
- هر سه نقطه request، processing و download Permission را دوباره ارزیابی می‌کنند؛
- نبود Source permission کل F02 را unavailable/denied می‌کند؛ حذف خاموش یک روز یا Fact و ساخت گزارش
  ناقص مجاز نیست.

Classification خروجی بیشترین مقدار میان Definition، Project/source configuration و تمام Sourceهای
واردشده است و Caller نمی‌تواند آن را پایین بیاورد. قرارداد فعلی Daily Report حداقل `Internal` است؛
اگر Source آینده Classification بالاتری صادر کند، Snapshot و تمام Outputها همان مقدار بالاتر را
می‌گیرند یا در نبود Permission fail-closed می‌شوند. filename، event و diagnostic نباید Narrative،
نام فرد یا محتوای High/Critical را نشت دهند.

## ۸. Golden matrix الزامی برای Sliceهای Runtime/Renderer

| ID | Fixture | انتظار قطعی |
| --- | --- | --- |
| `F02-W01` | هفته بسته شنبه تا جمعه، cadence روزانه و هفت گزارش رسمی | `Available`، هفت تاریخ و ordering/hash ثابت |
| `F02-W02` | هفته جاری با تمام روزهای سپری‌شده پوشش‌دار | `InsufficientData` + `PeriodOpenAtCutoff` |
| `F02-W03` | هفته بسته و صفر گزارش رسمی | `NoData`، بدون صفر ساختگی |
| `F02-W04` | cadence/workflow/cutoff یا تقویم لازم تنظیم نشده | `NotConfigured` با reason دقیق |
| `F02-W05` | یک تاریخ مفقود یا یک گزارش رسمی بدون Fact | `InsufficientData` با missing date/reason |
| `F02-W06` | twin پیش و پس از correction رسمی | hash برابر در هر cutoff و متفاوت میان cutoffها؛ Draft غایب |
| `F02-M01` | ماه شمسی ۳۱روزه | مرز دقیق ماه، ordering و coverage قطعی |
| `F02-M02` | اسفند leap و non-leap و گذار به فروردین | ۲۹/۳۰ روز صحیح و UTC boundary صحیح در Time Zone پروژه |
| `F02-A01` | quantityهای هم‌Kind با دو unit و یک unit null | bucketهای جدا؛ بدون conversion یا grand total |
| `F02-A02` | Labor/Equipment در چند روز | جمع با عنوان daily observations؛ بدون unique-count inference |
| `F02-S01` | permission revoke و Cross-Tenant/Project IDs | fail-closed؛ صفر Source/Output leakage |
| `F02-S02` | Source با Classification بالاتر | propagation بالاتر یا deny؛ هرگز downgrade/redaction خاموش |
| `F02-S03` | دو current official یا duplicate root/date | failure امن؛ بدون Snapshot/Output |
| `F02-D01` | Source یکسان با query order متفاوت و دو Run twin | semantic/manifest hash یکسان و canonical ordering |

Qualification متصل آینده باید boundaryها را مستقل از implementation محاسبه، Snapshot را parse و
Source manifest را با query مستقل بررسی کند. Golden محلی PDF/XLSX فعلی جای Golden معنایی یا
integration متصل را نمی‌گیرد.

## ۹. نگاشت Runtime Core و Renderer Checkpoint

Runtime Core این قرارداد را بدون بازکردن API یا Renderer به کد نگاشت می‌کند:

- `ProjectControlProfile` اکنون Project revision، Configuration version/change time، cadence،
  workflow، cutoff روزانه، Calendar و Time Zone pin‌شده را یکجا حمل می‌کند؛
- `IDailyReportPeriodReportingSource` با contract version
  `pmcs.field-operations.daily-report-period/v1` فقط rootها و نسخه‌های رسمی تا cutoff را در بازه
  نیمه‌باز می‌خواند؛ Draft/Submitted/Returned/Rejected و correction آینده وارد response نمی‌شوند؛
- Source روی duplicate root/date و بیش از یک current official fail-closed است و داخل Reporting هیچ
  `FieldOperationsDbContext` یا SQL بین‌ماژولی وجود ندارد؛
- `ProjectPeriodicReportPeriodResolver` مرز شنبه و ماه شمسی ۲۹/۳۰/۳۱روزه، UTC boundary، cutoff
  آینده/پیش از شروع و local time ناموجود/مبهم را قطعی می‌کند؛
- `ProjectPeriodicReportSnapshotBuilder` ordering، coverage سه cadence، چهار data status، reason
  allowlist، classification propagation، lineage manifest، semantic hash و aggregateهای بدون
  conversion/zero fabrication را می‌سازد؛
- ۱۶ case C# مرزهای هفتگی/ماهانه، leap Esfand، Daily/WorkingDays/Weekly coverage، وضعیت‌ها،
  unit ordinal، classification، correction cutoff، hash twin و duplicate invariant را پوشش می‌دهد.

Renderer checkpointed روی همان Snapshot یک parser و request fail-closed با تطبیق schema/definition،
semantic SHA-256، source-manifest SHA-256 و cutoff می‌سازد و سپس یک render model با ordering قطعی
به هر دو خروجی می‌دهد. PDF دوصفحه‌ای RTL، Jalali، reason/status، coverage، aggregateهای unit-safe،
High/Critical و lineage را با runtime/font pin‌شده تولید می‌کند. XLSX هشت Sheet
`Metadata/Coverage/Reports/Facts/Fact Counts/Quantities/Resources/High Impact` دارد، RTL/frozen،
formula-free و با ZIP timestamp/order ثابت است. XLSX hash برابر
`83fd80eedaa1024e84eb253bec76591379fe2f088be12c5b322573d63eb1909d` و PDF hash برابر
`52ec4e80c34e682f6994ef7a674b161b748a772e34b4e04ec12e27e94c98f989` است؛ visual digestهای
۹۶ DPI نیز `058a3da3045408a1d87dc9e5c942cd38ffdf7da1921b6594e6ee88a0aa22b396` و
`d61a1090d07d5f21a5d57c15b3abb197a341332b124ba3e8a98461996a42b770` هستند.

این Rendererها عمداً در DI/registry ثبت نشده‌اند و Worker/endpoint آن‌ها را فراخوانی نمی‌کند.
Definition/Template seed، Migration، API، UI و feature flag جدید وجود ندارد؛ Shared PDF runtime
فقط از F01 استخراج شده و Golden تصویری موجود F01 بدون تغییر پاس می‌شود.
Source commit `4f68f57de2c2a79b654a19128894d9c89878ab65` با tree
`f4b592c72ea65974c00b936ca59c0428eb47f981` در Run 141 (`35495791821`) هر هشت Job را پاس کرد و
Checkpoint `PMCS-V1.1-RPT1-S07-MS04-C1` مرز ادامه را ثبت می‌کند.

## ۱۰. Definition of Ready و وضعیت پیاده‌سازی

| Gate | وضعیت |
| --- | --- |
| Scope/Non-Scope خانواده | بسته |
| پارامتر و period boundary | بسته |
| Source lineage و cutoff | بسته |
| Data status و coverage | بسته |
| Permission/classification | بسته |
| Golden matrix | بسته |
| Runtime Definition/parameter/snapshot IDs | Checkpointed in S07-MS03 |
| Period source contract/resolver/Snapshot builder | Checkpointed in S07-MS03 |
| Unit/contract tests | `353/353` C# و `60/60` Node در Run 141 |
| Catalog/Template seed و Worker/API wiring | Not Implemented |
| PDF/XLSX/visual/performance | Checkpointed in S07-MS04؛ deterministic Golden پاس |

Micro-Step بعدی فقط Catalog/API/Worker wiring متصل F02 است.
UI و فعال‌سازی Production باید در Sliceهای مستقل بعدی باقی بمانند.

## ۱۱. Gate statement

این نسخه F02 را به `Renderer/Golden Safe Checkpoint` می‌رساند، نه `End-to-End Implemented` یا
Qualification کامل خانواده. هیچ API، Migration، Catalog/Template seed، Worker dispatch، DI
registration، UI، feature flag یا Production setting در این Micro-Step تغییر نکرده است. F02 تا
پایان wiring و integration متصل باز می‌ماند؛ F03 تا F10 و RPT1 نیز باز هستند.
