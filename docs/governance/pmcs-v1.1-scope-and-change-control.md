# PMCS V1.1 — Scope، Non-Scope و Change Control

- شناسه: `PMCS-GOV-V1.1-SCOPE-001`
- نسخه: `1.0.0`
- Parent baseline: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`

## ۱. هدف نسخه

V1.1 باید PMCS را بدون شکستن حقیقت‌ها و Workflowهای V1 به یک بستر توسعه‌پذیر، دارای تجربهٔ بصری ممتاز، پروفایل عضو، راه‌اندازی کنترل‌شده پروژه، اسناد مشترک، همکاری پروژه‌ای، گزارش‌سازی رسمی و Foundation مرحله اول Agent تبدیل کند.

## ۲. Scope قطعی

| Checkpoint | دامنه | خروجی لازم |
| --- | --- | --- |
| `V1.1-G0` | Governance | Baseline، ADR، Scope، Permission، Contract، Risk و Test strategy |
| `V1.1-UX1` | Visual Excellence Foundation | Audit، Token، Component contract و Prototype کامل |
| `V1.1-EXT1` | Extensibility | Module/Permission/Navigation/Event/Tool manifests و Reference Module |
| `V1.1-DOC1` | Shared Documents | Upload امن، نسخه، Classification، Quarantine، Retention و Audit |
| `V1.1-IAM1` | Login/Profile | Login descriptor نسخه‌دار و Member Profile حداقلی با تصویر |
| `V1.1-PRJ1` | Project Bootstrap | Preview و انتقال انتخابی Setup/Membership بدون داده عملیاتی |
| `V1.1-RPT1` | Reporting Phase 1 | گزارش استاندارد و Certified در PDF/Excel |
| `V1.1-COL1` | Project Collaboration | گروه رسمی هر پروژه با فایل، Offline، Search و Audit |
| `V1.1-UX2` | UI Migration | مهاجرت موجی تمام مسیرهای فعال به Design System |
| `V1.1-INT1` | Agent Stage 1 | Provider gateway، Tool registry، Permission/Audit و safe failure |
| `V1.1-QA1` | Qualification | Migration، Regression، Security، UI و Lock evidence |

## ۳. Non-Scope قطعی V1.1

- Stageهای 2 تا 7 Agent مدیریتی؛
- Agent دارای SQL/Database access یا Permission بالاتر از کاربر؛
- PMO، PMBOK، توجیه اقتصادی، متره/برآورد و Scheduling/MSP؛
- Report Designer آزاد، Scheduled Delivery و Topic Channel پیشرفته؛
- پیام خصوصی، تماس صوتی/تصویری، Story، Status و Feed عمومی؛
- Dynamic loader برای Binary/Plugin شخص ثالث؛
- HTML/CSS/JavaScript دلخواه در Login؛
- Duplicate کامل Database یا کپی دادهٔ عملیاتی پروژه؛
- Profile اجتماعی گسترده، Follow، Gallery یا رزومه عمومی؛
- جایگزینی محاسبات Finance/Progress/Schedule/Contract/Permission با LLM.

## ۴. قواعد Truth و Ownership

- هر Bounded Context مالک Persistence خود است؛
- Read بین‌ماژولی فقط از Application Contract یا Projection نسخه‌دار انجام می‌شود؛
- Integration Event از Outbox عبور می‌کند؛
- Chat دادهٔ زمینه‌ای است، نه منبع حقیقت رسمی؛
- Reporting فقط از Semantic Read Model مجاز و قطعی استفاده می‌کند؛
- Agent فقط از Permission-aware Tool استفاده می‌کند؛
- پروفایل شخصی Tenant-scoped است و Project Membership پروژه‌محور باقی می‌ماند؛
- Project Bootstrap فقط داده‌ای را منتقل می‌کند که مالک ماژول صریحاً Cloneable اعلام کرده باشد.

## ۵. Change Control

هر درخواست جدید پیش از کد یکی از وضعیت‌های زیر را می‌گیرد:

| نوع | مسیر |
| --- | --- |
| Clarification | مستندات و review |
| Defect نسبت به Contract مصوب | Checkpoint جاری + Regression |
| Additive Feature داخل Scope | Impact assessment و DoR همان Checkpoint |
| Additive Feature خارج Scope | Roadmap revision و Checkpoint مستقل |
| Breaking Change | ADR جایگزین، Migration strategy و تصمیم Major |
| Security Emergency | Patch ایزوله و retrospective اجباری |

پس از Feature Freeze، هر تغییر Runtime Candidate را باطل و Candidate جدید ایجاد می‌کند. Feature ناقص، endpoint پنهان، flag بدون مالک یا migration نیمه‌کاره در Baseline پذیرفته نمی‌شود.

## ۶. Acceptance سطح نسخه

- تمام Scopeهای بالا با Checkpoint مستقل و Evidence بسته شوند؛
- تمام Non-Scopeها در معماری، Route و UI غایب بمانند؛
- Migration از V1 قفل‌شده و Full Regression آن پاس شود؛
- Permission/Tenant/Project boundary منفی اثبات شود؛
- Design System در تمام صفحه‌های فعال یکپارچه باشد؛
- V1.1 فقط پس از گزارش ماشینی و انسانی `Qualified` قفل شود.

