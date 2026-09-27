# PMCS V1.1 — قرارداد معنایی گزارش Quality/HSE

- شناسه: `PMCS-RPT1-F08-SEMANTIC-001`
- نسخه: `1.0.0`
- خانواده: `RPT1-F08`
- وضعیت: `DoR / Semantic Contract Locked | Runtime/Renderer/Catalog/API/Worker open`
- Parent checkpoint: `PMCS-V1.1-RPT1-S07-MS26-C1`
- مرز مالکیت: `Pmcs.Modules.QualitySafety`، دو بخش مستقل Quality و HSE
- MS27 contract-only Runtime / Renderer / Migration / Catalog / API / Worker change: None

## ۱. دامنه و منبع حقیقت

F08 وضعیت رسمی کنترل کیفیت و ایمنی/بهداشت پروژه را در cutoff واحد گزارش می‌کند؛ Quality و HSE
دو بخش مستقل با وضعیت، شمارش، دلایل و Classification جدا هستند. مالک داده فقط
`Pmcs.Modules.QualitySafety` است و Reporting صرفاً از Application Contract خواندنی، نسخه‌دار
و مقید به Tenant/Project/cutoff آن استفاده می‌کند. اتصال به `QualitySafetyDbContext`، SQL
ماژول مالک، `GET /quality-safety/state` یا DTOهای Endpoint ممنوع است. Endpoint فعلی `Take(100)`
برای اغلب دفترها، `Take(150)` برای Action و `Take(120)` برای Exposure دارد و current-state است؛
این خروجی Source Certified یا proof of completeness نیست.

Quality: Intake رسمی پس از triage، Inspection/Result، NCR/Disposition، Defect/Verification،
Quality Test و Action مرتبط با Quality. HSE: Intake رسمی، Incident و شدت اولیه/نهایی جدا،
Permit/Activation، Toolbox Talk، Competency/Exposure فقط به‌صورت خلاصهٔ غیرشخصی و Action
مرتبط با HSE. Matrix، ITP و Checklist نسخه‌دار و Project configuration مبنای اعتبارند،
نه fact عملکردی. `SourceRecordId`/`InspectionId`/`GoodsReceiptId` فقط ارجاع اختیاری برای
اعتبارسنجی درون Scope هستند؛ F08 به Supply، Governance، Finance یا Project State join ندارد.
F09 مالک Issue/Risk/Decision/Escalation است؛ F08 از شمارش Action خود، سلامت کل پروژه یا
امتیاز ایمنی/کیفیت استنتاج نمی‌کند. فایل، عکس، جزئیات پزشکی، Facts آزاد، نام اشخاص و
متن تحقیق/علت ریشه‌ای در Snapshot و Renderer قرار نمی‌گیرند.

## ۲. ورودی، Project و زمان

Client فقط JSON object دقیقاً خالی `{}` می‌فرستد. `area`، `status`، `severity`،
`includeRestricted`، `includeMedical`، شناسهٔ منبع، تاریخ دلخواه، صفحه، SQL و هر کلید دیگر
رد می‌شوند. Project route و `asOfUtc` پین‌شدهٔ Run تنها scope/cutoff هستند. Server هویت
Tenant/Project، کد/نام/Revision، ConfigurationVersion/ChangedAt، Time Zone و زمان پذیرش را
pin می‌کند؛ unknown zone یا نسخهٔ ناسازگار fail-closed است. `cutoffLocalDate` فقط از Time Zone
پروژه و UTC instant cutoff مشتق می‌شود؛ DateOnly موعد یا دوره بدون آن تفسیر نمی‌شود.
فقط رخداد `atUtc <= sourceCutoffUtc` واجد cutoff است؛ CreatedAt به‌جای زمان transition
فرض نمی‌شود. Cutoff پیش از تغییر پیکربندی بدون history معتبر، policy آن زمان را ثابت نمی‌کند.

## ۳. Configuration، Source و شکاف تاریخچه

Project feature stateهای Quality و HSE، modeهای `Disabled/DefectPunchOnly/InspectionAndNcrLite/FullV1`
و `Disabled/ObservationIntakeOnly/HseLite/FullV1`، readiness، matrix version و effective time
باید برای هر بخش در cutoff اثبات و در manifest pin شوند. `Disabled`/`NotEnabled` با proof
و بدون fact رسمی `NotConfigured` است؛ `Suspended` به معنی صفر یا سالم نیست. تغییر configuration
بعد از cutoff بدون تاریخچهٔ قابل اعتماد نباید با مقدار کنونی به گذشته اعمال شود.

مالک یک Source repeatable-read و bounded با شمارش کامل، continuation و manifest با
source/policy schema version، watermark، تعداد و digest هر دفتر، coverage و completeness
بخش‌ها فراهم می‌کند. هیچ `Take(100)` یا sample به معنی کامل بودن نیست. Intake زمان Capture
و Triage دارد؛ Quality Test و Toolbox Talk رخداد ثبت مستقل دارند؛ Exposure Hours زمان Approval
و دوره دارد. اما NCR، Defect، Incident، Permit، Inspection readiness و Action همهٔ زمان‌های
intermediate transition و classification/configuration تاریخی را در مدل جاری نگه نمی‌دارند.
برای رکورد/بازهٔ فاقد chronology معتبر، owner باید همان بخش را `InsufficientData` با شمارش
نامعلوم (`null`) و reason نسخه‌دار برگرداند؛ استفاده از Status کنونی برای cutoff تاریخی،
Audit text، `latest-row` یا حدس‌زدن زمان transition ممنوع است. اگر ابهام Tenant/Project،
Classification، linkage یا تمامیت manifest باشد کل Run fail-closed می‌شود.

## ۴. معنای رسمی دو بخش

Intake با `Captured` صرفاً مشاهده است؛ تا triage/convert رسمی، NCR/Incident/Defect یا Action
محسوب نمی‌شود. تبدیل با نوع و target هم‌Scope اثبات شود؛ `Dismissed` یا
`RetainedAsGeneralIssue` با formal record یکسان نیست. Inspection `Requested`،
`ReadinessRecorded` و `ResultRecorded` مستقل‌اند؛ `Ready` به معنی `Pass` نیست و
`PassWithObservation`، `Fail`، `HoldOrDeferred` و `NotReadyOrNotInspected` تفکیک می‌شوند.
Quality Test دارای نتیجهٔ Pass/Fail/Inconclusive و Sample/Criteria همان نسخه است، ولی
به‌تنهایی Closure NCR یا تأیید کل پروژه نیست. NCR از Issued تا Containment/Investigation/
Disposition/Verification/Closed/Reopened ترتیب دارد؛ Concession نیازمند approver مستقل و
closure نیازمند Evidence یا waiver ممیزی‌شده است. Defect Rectified، Accepted و Closed
سه مرحله‌اند؛ Assigned یا DueDate معیار تأخیر بدون status تاریخی نیست. Action Completed،
Verified و Closed جدا هستند و extension موعد فقط با approval معتبر اعمال می‌شود.

HSE Incident زمان وقوع و ثبت جدا دارد؛ `PreliminarySeverity` هرگز جای
`FinalSeverity` یا FinalReview را نمی‌گیرد. NearMiss یک intake است مگر تبدیل معتبر به Incident.
Permit Draft/Submitted/Approved/Active/Suspended/Closed جداست؛ ApprovedAt خودکار Active
نیست و validity window مجوز اشتغال واقعی را ثابت نمی‌کند. Toolbox Talk حضور/مدرک رسمی
ثبت‌شده دارد؛ حضور نامی/Competency و جزئیات پزشکی در گزارش چاپ نمی‌شود. Exposure Hours فقط
در صورت Approval، Evidence و پوشش دورهٔ معلوم وارد مخرج احتمالی می‌شود. نرخ حادثه
`incidents × 200000 / exposureHours` فقط با numerator رسمی هم‌بازه، denominator مثبت و
کامل، دورهٔ غیرهمپوشان، eligibility و دسترسی مجاز قابل انتشار است؛ در غیر این صورت مقدار
`null` با reason `ExposureBasisIncomplete` است، هرگز صفر یا شاخص سبز ساختگی نیست.

## ۵. Permission و Classification

تعریف واحد F08 به‌صورت whole-definition علاوه بر gateهای Reporting به هر سه
`quality.read`، `hse.read` و `hse.confidential.read` نیاز دارد. نبود هر کدام Catalog،
Create، List/Get، Retry/Cancel، Download/Verify و Worker recheck را deny می‌کند؛ حذف خاموش
HSE از گزارش، وجود Incident محرمانه را لو می‌دهد. `ProjectManager` با policy موجود هر سه
را دارد؛ `QualityController` فقط Quality و `HseOfficer` فقط HSE را دارد. IAM، grant یا role
در F08 تغییر نمی‌کند.

Definition حداقل `Confidential` است. `RestrictedQuality` به خروجی `Restricted` ارتقا می‌یابد؛
`ConfidentialHse` حداقل Confidential است. `PersonalMedical` و `LegalInvestigation` با policy
فعلی برای خروجی Certified مجوز/طرح redaction مستقل ندارند: کل Run fail-closed می‌شود،
حتی اگر رکورد مربوطه در صفحهٔ انتخابی نمایش داده نشود. مقدار ناشناخته نیز fail است.
Renderer فقط شناسه/شمارهٔ رسمی، تاریخ، وضعیت، severity یا نتیجهٔ غیرشخصی و aggregate
مجاز را می‌بیند؛ Evidence bytes/reference، افراد، Facts و waiver text حذف می‌شوند.
Classification خروجی بیشینهٔ مجاز Definition و Source است و در Snapshot، Document و
download ثابت می‌ماند؛ downgrade ممنوع است.

## ۶. وضعیت داده و integrity

هر بخش `NotConfigured`، `NoData`، `InsufficientData` یا `Available` با
`OfficialCount: int?` و reason دارد. `NoData` فقط با configuration/source coverage کامل و
صفر fact رسمی قابل اثبات است؛ `InsufficientData` count=`null` و هیچ مجموع ناقص را صفر
نشان نمی‌دهد. Aggregate کلی اگر هر بخش ناقص باشد InsufficientData است؛ اگر هر دو
NotConfigured باشند NotConfigured؛ اگر بخش کامل دارای رکورد باشد Available؛ و فقط با
دو بخش کامل و صفر رسمی NoData. Configuration مختلط به‌صورت دو section جدا نمایش داده می‌شود.
Reasonهای حداقلی: `QualityNotConfigured`، `HseNotConfigured`، `NoOfficialQualityFact`،
`NoOfficialHseFact`، `HistoricalTransitionUnavailable`، `SourceCoverageIncomplete`،
`ConfigurationHistoryUnavailable`، `ExposureBasisIncomplete` و
`RestrictedPublicationUnavailable`. Hash canonical semantic و manifest، duplicate ID،
cross-project reference، coverage، cutoff/watermark و bound باید قبل از Snapshot validate
شوند؛ tamper/overflow/unknown classification processing failure است.

## ۷. Golden و Gate پذیرش

| شناسه | سناریو | انتظار |
| --- | --- | --- |
| `F08-Q01` | Intake captured/triaged/converted | مشاهده با formal record یکی نشود |
| `F08-Q02` | Inspection ready/result across cutoff | Ready به Pass تبدیل نشود |
| `F08-Q03` | PassWithObservation/Fail/Hold | نتیجه‌ها تفکیک شوند |
| `F08-Q04` | NCR concession/closure/reopen | approval/evidence/reopen مستقل بماند |
| `F08-Q05` | Defect rectified/accepted/closed | تأیید با closure یکی نشود |
| `F08-Q06` | Action completed/verified/extended | deadline و verify تاریخچه لازم دارند |
| `F08-Q07` | Test Pass/Fail/Inconclusive | یک تست سلامت پروژه نشود |
| `F08-H01` | NearMiss intake و Incident | تبدیل/شماره رسمی مستقل |
| `F08-H02` | Preliminary vs Final severity | شدت اولیه جای نهایی ننشیند |
| `F08-H03` | Permit approved/active/suspended | Approval اشتغال واقعی نیست |
| `F08-H04` | Toolbox/Competency | شخص/پزشکی در renderer نباشد |
| `F08-H05` | Exposure interval gap/overlap | نرخ حادثه `null` بماند |
| `F08-C01` | Owner `/state` capped | completeness از page cap حدس نشود |
| `F08-C02` | configuration changed after cutoff | current mode تاریخچه نشود |
| `F08-C03` | disabled، no data، partial | سه وضعیت جدا؛ count نامعلوم صفر نشود |
| `F08-P01` | revoke هر Source permission | whole-definition fail-closed |
| `F08-P02` | PersonalMedical/LegalInvestigation | کل Run deny؛ metadata نشت نکند |
| `F08-P03` | cross-project/unknown class | failure و صفر خروجی |
| `F08-S01` | تکرار با ترتیب query متفاوت | semantic و manifest hash برابر |
| `F08-B01` | overflow/duplicate/event gap | failure بدون truncate |
| `F08-X01` | property اضافی در `{}` | strict reject |
| `F08-R01` | PDF/XLSX NoData و partial | sectionهای مستقل، بدون صفر ساختگی |

DoR: مالک Source و permission/classification مشخص؛ policy cutoff و legacy صریح؛ parameter
خالی و Time Zone pin؛ renderer/Golden مستقل؛ Migration/Definition فقط پس از Runtime و
Renderer معتبر؛ API/Worker و CI End-to-End پس از آن. Micro-Step بعد `S07-MS28` فقط
Runtime Core محدود و Source مالک است. UI/UX2، Production enablement، Report Designer و F09
خارج از F08 هستند؛ defaultهای RPT1 خاموش می‌مانند.
