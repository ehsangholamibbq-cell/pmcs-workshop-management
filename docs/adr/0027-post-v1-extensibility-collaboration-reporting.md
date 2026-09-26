# ADR 0027 — مرز تکامل Post-V1: Extensibility، Collaboration و Reporting

- وضعیت: Accepted
- تاریخ: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Parent product baseline: `PMCS V1 / 26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- Roadmap: `docs/roadmaps/pmcs-post-v1-product-evolution.md`

## Context

PMCS V1 با معماری Modular Monolith، Permission مرکزی، Audit/Outbox/Idempotency، Offline/Sync، Project State قطعی و Qualification کامل قفل شده است. سه نیاز جدید برای رشد محصول تأیید شده‌اند:

1. افزودن دامنه‌های آینده مانند PMO، PMBOK، توجیه اقتصادی، متره/برآورد و Scheduling/MSP بدون بازنویسی هسته؛
2. گفت‌وگوی گروهی اعضای هر پروژه همراه فایل و اتصال کنترل‌شده به رکوردهای رسمی؛
3. مرکز گزارش‌سازی و چاپ حرفه‌ای برای تمام دامنه‌های مجاز محصول.

رابط فعلی نیز از نظر ظرافت و انسجام بصری برای مرحلهٔ بعد کافی نیست و مالک محصول در حال حاضر زیرساخت اجرای مستقیم ندارد؛ بنابراین تأیید تجربهٔ بصری باید با Prototype و evidence تصویری پیش از پیاده‌سازی انجام شود.

## Decision

### ۱. V1 تغییر داده نمی‌شود

قابلیت‌های جدید در خط V1.1 ساخته می‌شوند. V1 فقط از طریق Patch versioned برای Defect/Security قابل تغییر است.

### ۲. Extensibility Foundation پیش‌نیاز ماژول‌های بزرگ است

Module، Permission، Navigation، Event و Agent Tool باید Manifest و Contract نسخه‌دار داشته باشند. ارتباط بین ماژول‌ها از Application Contract/Event انجام می‌شود و Persistence reference مستقیم ممنوع می‌ماند.

Runtime plugin loader برای Binary شخص ثالث در V1.1 ساخته نمی‌شود؛ این تصمیم سطح حمله و پیچیدگی عملیاتی غیرضروری ایجاد می‌کند.

### ۳. Collaboration یک Bounded Context مستقل است

هر پروژه یک Room گروهی پیش‌فرض دارد و Membership آن فقط از عضویت فعال پروژه مشتق می‌شود. Reply، Mention، Reaction، Pin، Search، unread، فایل، Offline/Retry، Moderation، Retention و Audit در Scope هستند.

موارد زیر خارج از Scope هستند:

- Direct/Private Message؛
- تماس صوتی یا تصویری؛
- Voice Room، Story، Status و Feed عمومی؛
- Room عمومی خارج از Project Membership؛
- Bot خودمختار با حق تغییر رکورد رسمی.

Chat منبع حقیقت رسمی نیست. تبدیل پیام یا فایل به Action، Issue، RFI، Daily Fact، Evidence یا Technical Document فقط با Command صریح، Permission، Validation، Human Confirmation و lineage انجام می‌شود.

### ۴. Shared Document Foundation از Evidence جدا است

Evidence V1 معنای تخصصی خود را حفظ می‌کند. فایل Chat، خروجی Report و اسناد عمومی روی Document/Attachment contract مشترک قرار می‌گیرند و دارای Object Storage خصوصی، Hash، Content validation، quarantine adapter، Permission، Retention و Audit هستند.

### ۵. Reporting یک Bounded Context مستقل است

Reporting از Semantic Read Modelهای permission-scoped و دادهٔ رسمی استفاده می‌کند. هر خروجی دارای Template Version، parameter/as-of، Snapshot، Hash، Audit و archive است.

V1.1 ابتدا گزارش‌های استاندارد و Certified را در PDF/Excel ارائه می‌کند. Report Designer آزاد، Scheduled delivery و customization پیشرفته پس از اثبات هسته به V1.2 منتقل می‌شوند.

LLM مجاز به محاسبه یا اختراع عدد گزارش نیست؛ فقط می‌تواند روایت یا خلاصهٔ مشورتی با Citation تولید کند.

### ۶. UX prototype قبل از Production UI الزامی است

Design System، Shell و Prototypeهای High-Fidelity برای Dashboard، Collaboration و Reporting پیش از پیاده‌سازی تصویب می‌شوند. بازطراحی UI نباید Business Rule یا State Truth را تغییر دهد.

حرفه‌ای‌سازی ظاهر یک Visual Excellence Program سراسری است و باید تمام صفحات V1، حالت‌های عملیاتی، چاپ، Collaboration و Executive Intelligence Center را مهاجرت و با visual/accessibility/responsive regression احراز کند. تغییر Theme یا چند component به‌تنهایی این تصمیم را برآورده نمی‌کند.

### ۷. Agent integration از Tool Registry عبور می‌کند

معماری پابرجا است:

`Agent → Permission-aware Tool → PMCS Application Service → Business Rules → Database`

هر Tool دارای Permission، Risk Class، schema، audit و citation policy است. فعال‌سازی عمومی `@PMCS` در Chat به V1.2 و پس از اثبات read/draft boundary موکول می‌شود.

Agent مدیریتی دقیقاً هفت Stage مستقل دارد: Intelligence Foundation، Read-only Intelligence، Knowledge/RAG/Evidence/Citations، Executive Intelligence UI، Draft Actions، Controlled Actions + Human Approval و Evaluation/QA/Security/Hardening. این Stageها نباید در یک عنوان کلی فشرده یا بدون Checkpoint/Evidence بسته شوند.

## Consequences

### مثبت

- V1 قابل بازگشت و قابل استناد باقی می‌ماند؛
- توسعه‌های آینده Contract-driven می‌شوند؛
- Chat به عملیات رسمی متصل است ولی حقیقت رسمی را آلوده نمی‌کند؛
- گزارش‌ها زیبا و در عین حال reproducible و ممیزی‌پذیر هستند؛
- UI پیش از هزینهٔ پیاده‌سازی قابل ارزیابی است؛
- Agent نمی‌تواند Permission یا Business Rule را دور بزند.

### هزینه و پیچیدگی پذیرفته‌شده

- V1.1 پیش از Featureهای ظاهری نیازمند Foundation و Contract work است؛
- Reporting و Collaboration تست‌های Security، concurrency، offline و retention جدید می‌خواهند؛
- خروجی حرفه‌ای چاپ به visual regression و template governance نیاز دارد؛
- Migration UI باید موجی باشد تا Regression کنترل شود.

## Alternatives Rejected

| گزینه ردشده | دلیل |
| --- | --- |
| افزودن قابلیت‌ها مستقیماً به V1 Locked | از بین‌رفتن مرجع Qualification و rollback |
| ساخت کپی کامل Telegram | Scope نامرتبط، هزینه زیاد و انحراف از PMCS |
| پیام خصوصی/Voice/Video | فاقد ارزش کافی برای هدف سامانه و افزایش ریسک/هزینه |
| استفاده از Chat به‌عنوان رکورد رسمی | نبود validation، authority و workflow قابل استناد |
| Report Builder کاملاً آزاد در اولین Slice | ریسک Scope، خطای عددی و پیچیدگی چاپ قبل از اثبات هسته |
| Query مستقیم Reporting به جدول همه ماژول‌ها | coupling، دورزدن مالکیت داده و Permission |
| محاسبهٔ KPI/Finance/Schedule توسط LLM | غیرقطعی و غیرقابل ممیزی |
| Agent با SQL یا DB access | نقض Permission و Business Rule |
| Dynamic third-party plugin loader در V1.1 | سطح حمله و پیچیدگی عملیاتی بدون نیاز اثبات‌شده |
| فشرده‌سازی هفت Stage Agent به یک Integration task | حذف Gate، Evaluation و مرزهای Permission/Human Approval |
| تقلیل Visual Excellence به Theme/CSS | باقی‌ماندن ناهماهنگی و نبود معیار حرفه‌ای قابل سنجش |
