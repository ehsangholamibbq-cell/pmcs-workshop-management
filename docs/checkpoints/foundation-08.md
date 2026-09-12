# Foundation checkpoint 08 — Project Intelligence Advisory V1

- Date: 2026-09-10
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.10

## Outcome

مدیر پروژه اکنون در مرکز فرمان فارسی می‌تواند از آخرین Project State رسمی و داده‌های مجاز خود یک تحلیل مدیریتی مشورتی، ساختاریافته و دارای ارجاع درخواست کند. خروجی هیچ حقیقت، محاسبه، سند یا اقدام رسمی را تغییر نمی‌دهد و پیش از پذیرش در وضعیت `NeedsReview` باقی می‌ماند. هیچ داده یا تحلیل نمونه‌ای ایجاد نشده است.

## Implemented

- bounded context مستقل `Intelligence` در Modular Monolith؛
- durable generation queue با status، lease، recovery، retry محدود و safe failure code؛
- Permissionهای `insights.view/generate/review` و recheck درست پیش از Model call؛
- Context assembler مبتنی بر latest Project State Snapshot و permission-scoped Financial/Commercial/Action data؛
- حذف نام فرد از Action context و جلوگیری از readback Insight برای کاربر فاقد permission ابعاد استفاده‌شده؛
- semantic صریح optional WBS/Planning، Budget baseline و HSE؛
- OpenAI Responses adapter با model/config خارجی، `store=false`، timeout و Structured Outputs strict؛
- JSON Schema و domain validation برای نوع، متن، facts، assumptions، data gaps، impact، actions و owner role؛
- citation manifest و رد خروجی دارای reference ناشناخته؛
- handling صریح refusal، incomplete، invalid response، timeout و provider unavailable؛
- persistence کامل Snapshot/Context hash/Provider/Model/response id/Prompt/Policy/expiry/data scope؛
- Human review با base revision، Idempotency، Audit و Outbox؛
- APIهای create/status/list/accept/dismiss؛
- کارت فارسی «تحلیل مشورتی مستند» با polling درخواست، شواهد، شکاف‌ها، اقدامات پیشنهادی و disclaimer؛
- حالت امن Provider not configured بدون پاسخ ساختگی و بدون اختلال در Project Command Center؛
- ADR، API contract، architecture guard و تست‌های Domain/Web.

## Verification evidence

- .NET solution build: ۰ warning و ۰ error؛
- Domain tests: ۶۴/۶۴ passed؛
- Frontend unit/API contract tests: ۳۶/۳۶ passed؛
- Persian UI audit: passed؛
- ESLint و TypeScript: passed؛
- Next.js production build: passed؛
- Repository architecture validation و `git diff --check`: passed.

## Boundaries and open gates

- API key در محیط حاضر وجود ندارد؛ بنابراین هیچ call واقعی و هیچ هزینه‌ای ایجاد نشد. Adapter و parser با تست ساختاری اعتبارسنجی شدند و live smoke test پس از تزریق secret در محیط Integration لازم است.
- PostgreSQL/Docker در این محیط نصب نیست؛ migration، `FOR UPDATE SKIP LOCKED`، transaction و multi-worker lease باید در Integration environment اجرا شوند.
- retrieval فعلی فقط از read modelهای ساختاریافته است؛ اسناد آزاد و Evidence content وارد Context نمی‌شوند.
- خروجی فقط project-level است؛ Portfolio intelligence و cross-project pattern در Slice بعدی قرار می‌گیرد.
- تبدیل پیشنهاد به Management Action عمداً پیاده نشده است؛ این کار به command و تأیید مستقل نیاز دارد.
- evaluation dataset واقعی موجود نیست. پس از ورود داده پایلوت باید golden cases برای unsupported claim، permission leakage، missing data semantics و Persian management usefulness ساخته شود.

## Next checkpoint

Vertical Slice «V1 operational hardening»: PostgreSQL/S3/OpenAI integration environment، Authentication production-grade، observability/backup، security/performance tests، evaluation با داده پایلوت و سپس User Acceptance Test محدود.
