# PMCS V1.1 — قرارداد معنایی گزارش هفتگی و ماهانه پروژه

- شناسه: `PMCS-RPT1-F02-SEMANTIC-001`
- نسخه: `1.0.0`
- خانواده: `RPT1-F02`
- وضعیت: `Contract Ready | Runtime Not Implemented`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS01-C1`
- Runtime change: None
- Migration / API / Renderer / Template change: None

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

شناسه Runtime Definition، Template Version، parameter schema identifier و snapshot schema
identifier در این Micro-Step تخصیص داده نمی‌شوند؛ آن‌ها باید همراه کد و migration/seed واقعی در
Slice محدود بعدی قطعی شوند.

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

چهار مقدار جدول `dataStatus` هستند، نه failure پردازشی. Runtime/Renderer آینده برای
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

Qualification آینده باید boundaryها را مستقل از implementation محاسبه، Snapshot را parse و
Source manifest را با query مستقل بررسی کند. Golden متن/PDF/XLSX جای Golden معنایی را نمی‌گیرد.

## ۹. Definition of Ready و Slice مجاز بعدی

| Gate | وضعیت |
| --- | --- |
| Scope/Non-Scope خانواده | بسته |
| پارامتر و period boundary | بسته |
| Source lineage و cutoff | بسته |
| Data status و coverage | بسته |
| Permission/classification | بسته |
| Golden matrix | بسته |
| Runtime Definition/Template/schema IDs | عمداً باز برای Slice پیاده‌سازی |
| Source contract/Builder/Catalog seed | Not Implemented |
| PDF/XLSX/visual/performance | Not Implemented |

Micro-Step بعدی فقط می‌تواند Runtime identity نسخه‌دار، Contract خواندنی period، resolver مرز
هفتگی/ماهانه، semantic Snapshot builder و Unit/contract tests را پیاده کند. Renderer، Catalog
فعال Production، UI و Golden binary باید در Sliceهای بعدی و پس از Snapshot قطعی وارد شوند.

## ۱۰. Gate statement

این قرارداد F02 را `Ready for bounded Runtime implementation` می‌کند، نه `Implemented` یا
`Qualified`. هیچ API، Migration، Catalog seed، Domain runtime، Renderer، feature flag یا Production
setting در این Micro-Step تغییر نکرده است. F02 تا پایان Runtime، Golden، Renderer و CI متصل
`Required / Not Implemented` باقی می‌ماند؛ F03 تا F10 و RPT1 نیز باز هستند.
