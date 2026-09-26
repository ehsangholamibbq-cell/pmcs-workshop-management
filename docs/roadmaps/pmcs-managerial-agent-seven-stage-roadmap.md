# Roadmap هفت‌مرحله‌ای Agent مدیریتی PMCS

- شناسه سند: `PMCS-RM-AGENT-001`
- نسخه سند: `1.0.0`
- وضعیت: مصوب و لازم‌الاجرا؛ پیاده‌سازی Stageهای جدید هنوز آغاز نشده است
- تاریخ بازیابی و ثبت: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Parent product baseline: `PMCS V1 / 26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- Parent roadmap: `pmcs-post-v1-product-evolution.md`

## ۱. قاعده حاکم

Agent مدیریتی یک قابلیت واحد و یک‌مرحله‌ای نیست. ساخت آن دقیقاً هفت Stage دارد و هیچ Stage نباید حذف، در عنوانی کلی فشرده یا بدون Gate بسته شود.

معماری ثابت:

`Agent → Permission-aware Tool → PMCS Application Service → Business Rules → Database`

مسیر `Agent → SQL/Database` ممنوع است. Agent دقیقاً تحت Permission کاربر جاری عمل می‌کند و حق privilege elevation ندارد. Finance، Progress، Schedule، Contract، Permission و سایر محاسبات قطعی توسط موتورهای PMCS انجام می‌شوند؛ مدل فقط تحلیل، توضیح، ارتباط، تشخیص، Insight و Recommendation ارائه می‌کند.

## ۲. وضعیت دارایی موجود

V1 دارای یک پایهٔ Advisory/Permission-aware محدود است، اما این دارایی به‌تنهایی هیچ‌یک از هفت Stage آینده را کامل اعلام نمی‌کند. در آغاز Stage 1 باید Gap Analysis رسمی انجام شود و اجزای قابل استفاده با Contract و Test جدید اثبات شوند.

## ۳. Stage 1 — PMCS Intelligence Foundation

**شناسه:** `AGENT-S1`

### هدف

ایجاد Foundation مستقل، Provider-independent، permission-aware و قابل ممیزی.

### Scope

- Bounded Context مستقل `PMCS.Intelligence`؛
- Model Gateway و Provider abstraction؛
- Model/Prompt/Policy registry و versioning؛
- Session، Conversation Context، Request و Run lifecycle؛
- Permission-aware Tool Registry؛
- Tool schema، Risk Class و read/draft/write classification؛
- Context budget، timeout، retry و cancellation؛
- Structured Output و schema validation؛
- Audit، Correlation، telemetry، usage/cost و safe logging؛
- Secret isolation و data minimization؛
- Provider failure/fallback policy؛
- منع DB/SQL مستقیم و cross-module Persistence access.

### Gate خروج

- تعویض Provider بدون تغییر Business Logic اثبات شود؛
- Tool بدون Permission اجرا نشود؛
- cross-tenant/project و privilege escalation تست منفی داشته باشند؛
- Run/Audit/Model/Policy lineage کامل باشد؛
- خطای Provider، timeout و malformed output fail-safe باشند.

## ۴. Stage 2 — Read-Only Project Intelligence Agent

**شناسه:** `AGENT-S2`

### هدف

ارائهٔ تحلیل مدیریتی فقط‌خواندنی از دادهٔ رسمی و مجاز پروژه، بدون ایجاد یا تغییر رکورد.

### Scope

- Permission-aware Context Assembler؛
- استفاده از Canonical Project State و Read Modelهای رسمی؛
- Daily/Morning Brief و وضعیت استثناها؛
- سؤال مدیریتی درباره Finance، Progress، Commercial، Action، Risk و Data Quality؛
- تفکیک Fact، Inference، Assumption و Data Gap؛
- Structured response، Confidence و source reference؛
- history و expiry/staleness؛
- Portfolio scope فقط در محدوده دسترسی و بدون تجمیع پنهان ارز یا داده.

### Gate خروج

- هیچ ابزار write در Registry این Stage فعال نباشد؛
- هر ادعای factual دارای Source باشد؛
- نبود داده با حدس یا صفر جایگزین نشود؛
- تغییر Permission بلافاصله روی Context و خروجی مؤثر باشد؛
- پاسخ stale یا unsupported صریحاً علامت‌گذاری شود.

## ۵. Stage 3 — Knowledge / RAG / Evidence / Citations

**شناسه:** `AGENT-S3`

### هدف

افزودن دانش مستندی قابل استناد، نسخه‌دار و Permission-aware به تحلیل ساختاریافته.

### Scope

- ingestion کنترل‌شده از Document/Attachmentهای مجاز؛
- parser/OCR/chunking و metadata نسخه‌دار؛
- index namespace بر اساس Tenant/Project/Classification؛
- hybrid retrieval از Structured Data، Document و Evidence؛
- citation manifest با Source ID، Revision، page/section و hash؛
- stale/delete/reindex semantics؛
- پاسخ در صورت citation failure به‌صورت Unsupported یا عدم انتشار؛
- prompt-injection isolation برای محتوای بازیابی‌شده؛
- retrieval diagnostics بدون افشای محتوای غیرمجاز.

### Gate خروج

- citation به Source ناشناخته رد شود؛
- سند حذف/بازنشسته یا Permission-revoked دیگر بازیابی نشود؛
- cross-project retrieval تست منفی داشته باشد؛
- پاسخ بدون grounding لازم منتشر نشود؛
- index rebuild و disaster recovery قابل تکرار باشد.

## ۶. Stage 4 — Executive Intelligence UI

**شناسه:** `AGENT-S4`

### هدف

ساخت یک Executive Intelligence Center حرفه‌ای؛ نه Chat ساده.

### Scope

- Intelligence Dashboard و Morning Brief؛
- Command Bar و پرسش Context-aware؛
- تفکیک بصری Fact، AI Insight، Assumption و Data Gap؛
- Evidence/Citation قابل کلیک و drill-down؛
- Why، Confidence، freshness و model/policy provenance؛
- compare/current snapshot و history؛
- Action Draft preview بدون ایجاد رسمی؛
- RTL، شمسی، Responsive و Accessibility؛
- حالت‌های Empty/Loading/Streaming/Error/Unsupported/Stale/No Permission؛
- Entry point کنترل‌شده از Dashboard، Report و Project Collaboration؛
- پیروی کامل از `pmcs-visual-excellence-program.md`.

### Gate خروج

- Visual Direction و Prototype پیش از Production UI تصویب شده باشد؛
- هیچ AI output با Fact رسمی یکسان نمایش داده نشود؛
- Evidence و علت قابل دسترسی باشند؛
- صفحه در Desktop/Tablet/Mobile و حالت‌های خطا قابل استفاده باشد؛
- Chat پروژه فقط Entry Point باشد و جای Intelligence Center را نگیرد.

## ۷. Stage 5 — Draft Actions

**شناسه:** `AGENT-S5`

### هدف

تبدیل Insight به Draft قابل بازبینی، بدون ایجاد اثر رسمی.

### Scope

- Draft Action، Issue، RFI، Decision Request یا Report Narrative در Toolهای مجاز؛
- owner، due date، priority، rationale و evidence پیشنهادی؛
- source lineage و citation؛
- preview/diff و edit انسانی؛
- expiry و invalidation هنگام تغییر Context؛
- Draft persistence با Permission و Audit؛
- ممنوعیت Auto-submit/Auto-approve/Auto-close.

### Gate خروج

- Draft هیچ Project State یا رکورد رسمی را تغییر ندهد؛
- Human reviewer بتواند تمام فیلدها و Sourceها را ببیند؛
- stale draft قبل از تبدیل revalidate شود؛
- رد یا حذف Draft روی Source رسمی اثر نگذارد.

## ۸. Stage 6 — Controlled Actions + Permission + Human Approval

**شناسه:** `AGENT-S6`

### هدف

اجرای محدود Commandهای رسمی فقط پس از تأیید آگاهانه انسان و عبور مجدد از قواعد PMCS.

### Scope

- Allowlist بسیار محدود از Controlled Toolها؛
- confirmation صریح و نمایش دقیق اثر عملیات؛
- permission re-evaluation در لحظه اجرا؛
- business validation در Application Service؛
- base revision، optimistic concurrency و idempotency؛
- Audit/Correlation/Outbox و actor attribution؛
- approval workflow و separation of duties؛
- safe retry و partial-failure handling؛
- kill switch و per-tool feature flag؛
- عدم اجرای خودمختار scheduled write.

### Gate خروج

- مدل نتواند approval خود را جعل کند؛
- عملیات حساس بدون Human Approval اجرا نشود؛
- تغییر Permission یا Revision اجرای stale را متوقف کند؛
- duplicate/retry اثر تکراری نسازد؛
- هر عملیات تا کاربر، Tool، Draft، Source و Model Run قابل ردیابی باشد.

## ۹. Stage 7 — Evaluation / QA / Security / Hardening

**شناسه:** `AGENT-S7`

### هدف

احراز صلاحیت واقعی Agent پیش از Final/Lock؛ این Stage یک تست نمایشی نیست.

### Scope

- Golden managerial scenario set؛
- factuality، citation precision/recall و unsupported-claim rate؛
- permission، tenant/project isolation و tool abuse؛
- prompt injection، malicious document و data exfiltration؛
- hallucination، stale context و conflicting sources؛
- model/provider fallback و structured-output failure؛
- latency، cost، rate limit و load/soak؛
- human approval bypass attempts؛
- audit completeness و forensic replay؛
- red-team و adversarial tests؛
- UX trust/comprehension evaluation؛
- Full Regression PMCS و Agent Evaluation Report؛
- operational runbook، kill switch، monitoring و incident response.

### Gate خروج

- معیارهای کیفیت و امنیت از پیش تعیین‌شده پاس شوند؛
- هیچ Critical/High unresolved باقی نماند؛
- false claim و citation failure policy fail-closed باشد؛
- Rollback/disable Provider/disable Tool تمرین شود؛
- Candidate ثابت، report ماشین‌خوان، artifact digest و approvals ثبت شوند؛
- فقط پس از این Gate Agent می‌تواند `Qualified` و سپس `Baseline Locked` اعلام شود.

## ۱۰. نگاشت Stageها به نسخه‌ها

| Stage | نسخه فعلی برنامه | نکته |
| --- | --- | --- |
| S1 | V1.1 | پس از Extensibility Foundation؛ از دارایی V1 فقط بعد از Gap/Test استفاده می‌کند |
| S2 | V1.2 | Read-only و بدون write tool |
| S3 | V1.2 | پس از Shared Documents و permissioned retrieval |
| S4 | V1.2 | زیر Visual Excellence Program |
| S5 | V1.2 | Draft-only |
| S6 | V1.2 | Controlled allowlist + Human Approval |
| S7 | V1.2 Qualification | Qualification و Baseline مستقل Agent |

این نگاشت با Change Control قابل جابه‌جایی است، ولی ترتیب Stageها، Scope و Gateهایشان قابل حذف یا ادغام نیست.

## ۱۱. Checkpoint و Baseline

هر Stage باید Checkpoint Snapshot جداگانه داشته باشد و حداقل شامل Parent commit، start/end commit، Tool/Prompt/Policy/Model versions، Permission matrix، evaluation dataset version، test report، known risks و artifact digest باشد.

Stageهای 1 تا 6 Feature/Capability Checkpoint هستند. فقط Stage 7 مجاز است Qualification نهایی Agent و Baseline Lock را صادر کند.

