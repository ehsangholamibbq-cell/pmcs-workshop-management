# Quality & HSE API V1

Base path:

```text
/api/v1/projects/{projectId}/quality-safety
```

همه Commandها به Actor معتبر، Permission همان عملیات و `Idempotency-Key` نیاز دارند. شناسه‌های مرجع دوباره با Tenant، Project و حوزه کنترل اعتبارسنجی می‌شوند. همه به‌روزرسانی‌ها `baseRevision` می‌خواهند و در تعارض، بدون بازنویسی پاسخ 409 می‌دهند.

## State and setup

| Method | Path | Permission | قاعده |
| --- | --- | --- | --- |
| GET | `/state` | `quality.read` یا `hse.read` | فقط حوزه‌ها و رکوردهای مجاز Actor؛ Incident محرمانه حذف می‌شود |
| POST | `/matrices` | `quality_safety.configure` | ماتریس نسخه‌دار برای Quality یا HSE |
| PUT | `/configuration` | `quality_safety.configure` | حالت‌ها، مسئول‌ها، ماتریس‌ها و قواعد آمادگی با Revision |

حالت هر حوزه یکی از `Hidden`، `NotEnabled`، `Suspended`، `SetupRequired`، `NoData` یا `Available` است. شمارنده Incident برای کاربر بدون `hse.confidential.read` برابر `null` است. نرخ رخداد در هر ۲۰۰ هزار نفر-ساعت فقط با مجوز محرمانه و مجموع کل ساعات مواجهه تأییدشده محاسبه می‌شود؛ فهرست نمایشی صفحه‌بندی‌شده مبنای محاسبه نرخ نیست.

## Intake and triage

| Method | Path | Permission | اثر رسمی |
| --- | --- | --- | --- |
| POST | `/intakes` | `quality.intake.capture` یا `hse.intake.capture` | ثبت Fact اولیه؛ پرونده نهایی نیست |
| POST | `/intakes/{id}/triage` | `quality.intake.triage` یا `hse.intake.triage` | آغاز بررسی انسانی |
| POST | `/intakes/{id}/resolve` | همان Triage | نگهداری به‌عنوان موضوع عمومی یا رد مستدل |
| POST | `/intakes/{id}/convert` | همان Triage | تبدیل حوزه‌سازگار و حفظ lineage به پرونده رسمی |

ثبت محلی فقط برای Intake است. شناسه Draft و Idempotency Key تا پذیرش سرور ثابت می‌مانند. Draft محلی Incident، اعلان بحرانی، شماره رسمی یا پرونده حادثه نیست و در خطای Sync حذف نمی‌شود.

## Quality workflows

| Method | Path | Permission | قاعده |
| --- | --- | --- | --- |
| POST | `/inspection-test-plans` | `quality.standards.manage` | برنامه بازرسی و آزمون immutable و نسخه‌دار |
| POST | `/checklist-templates` | `quality.standards.manage` | الگوی چک‌لیست immutable و نسخه‌دار |
| POST | `/inspections` | `quality.inspections.capture` | درخواست؛ آمادگی و نتیجه را تعیین نمی‌کند |
| POST | `/inspections/{id}/readiness` | `quality.inspections.result` | ثبت جداگانه Ready یا NotReady |
| POST | `/inspections/{id}/result` | `quality.inspections.result` | نتیجه صریح با Evidence لازم |
| POST | `/test-records` | `quality.tests.capture` | نتیجه آزمون مستقل با معیار و مدرک |
| POST | `/ncrs` | `quality.ncr.create` | پرونده عدم انطباق با منابع اختیاری خرید/دریافت |
| POST | `/ncrs/{id}/transition` | `quality.ncr.manage` یا `quality.ncr.verify` | Disposition، Root Cause، Concession و Closure کنترل‌شده |
| POST | `/defects` | `quality.defects.capture` | نقص اجرایی مستقل |
| POST | `/defects/{id}/assign` | `quality.defects.manage` | مسئول و سررسید |
| POST | `/defects/{id}/transition` | Manage یا Verify | اصلاح با بستن و راستی‌آزمایی یکی نیست |

## HSE workflows

| Method | Path | Permission | قاعده |
| --- | --- | --- | --- |
| POST | `/incidents` | `hse.incidents.report` | Incident رسمی با ماتریس نسخه‌دار و طبقه‌بندی محرمانه |
| POST | `/incidents/{id}/transition` | Investigate یا Verify + confidential read | شدت اولیه، شدت نهایی و Root Cause جدا باقی می‌مانند |
| POST | `/permits` | `hse.permits.create` | فقط Draft مجوز کار |
| POST | `/permits/{id}/transition` | Manage یا Issue | Submit، Approve، Activate، Suspend و Close جدا هستند |
| POST | `/toolbox-talks` | `hse.toolbox.capture` | جلسه با حاضران و Evidence |
| POST | `/competencies` | `hse.competencies.manage` | سابقه محرمانه آموزش/شایستگی |
| POST | `/exposure-hours` | `hse.exposure.approve` | مبنای نفر-ساعت بدون هم‌پوشانی دوره و با Evidence |

## Corrective actions

| Method | Path | Permission | قاعده |
| --- | --- | --- | --- |
| POST | `/corrective-actions` | `quality.actions.create` یا `hse.actions.create` | فقط برای منبع رسمی همان حوزه |
| POST | `/corrective-actions/{id}/transition` | area-specific Update یا Verify | Completion و Verification Evidence جدا دارند |
| POST | `/corrective-actions/{id}/extend` | area-specific Extend | فقط تاریخ دیرتر با دلیل و تأییدکننده |

اقدام منتسب به Incident برای کاربر فاقد `hse.confidential.read` نه در State دیده می‌شود و نه قابل تغییر است. Permissionهای Quality و HSE قابل جایگزینی با یکدیگر نیستند.

## Current boundaries

- WBS، Budget، Contract و Project Calendar برای این ماژول پیش‌شرط نیستند.
- Intake آفلاین فقط provisional است؛ سایر Commandها رسمی و Online-only هستند.
- Attachment binary همچنان در Evidence module نگهداری می‌شود و این API فقط reference را ثبت می‌کند.
- هیچ وضعیت Composite Health یا «صفر حادثه» از نبود داده ساخته نمی‌شود.
- AI مجاز به اجرای هیچ‌یک از Commandهای این API نیست.
