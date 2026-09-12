# Foundation checkpoint 09 — V1 operational hardening

- Date: 2026-09-10
- Status: Implemented; environment integration gates remain open
- Product source: PMCS Product & System Blueprint V1.11

## Outcome

مرز API برای استقرار واقعی سخت‌سازی شد: هویت Production از Access Token استاندارد می‌آید، تنظیم ناامن fail-fast است، Health/Telemetry/Rate Limit تفکیک شده و Backup فقط همراه Restore Drill معتبر شناخته می‌شود. این Slice هیچ WBS، بودجه اولیه یا HSE ایجاد یا اجباری نکرده و هیچ داده عملیاتی نمونه‌ای به محصول اضافه نشده است.

## Implemented

- JWT Bearer trust boundary سازگار با OIDC/OAuth و validation امضا، issuer، audience و lifetime؛
- استخراج actor فقط از Claimهای معتبر `tenant_id/sub` و `device_id` اختیاری؛
- Development identity به‌عنوان Authentication Scheme جدا، بی‌اثر خارج Development و بدون امکان override کردن Bearer؛
- fallback authentication برای تمام endpointها به‌جز health، OpenAPI توسعه و foundation؛
- fail-fast در محیط غیر Development برای dev identity/seed، dev credential، Authority/Origin غیر HTTPS، wildcard host و auto-create bucket؛
- CORS چند Origin بدون wildcard و OpenAPI فقط در Development؛
- سقف body سی MiB در Kestrel و نگهداشت سقف Evidence روی ۲۵ MiB؛
- rate limit سراسری per actor/IP و policy محدودتر per actor برای Insight generation با پاسخ `429` ساختاریافته؛
- ترجمه فارسی `rate_limit.exceeded` بدون نمایش title انگلیسی Backend؛
- correlation id محدود و log-safe، structured request log بدون raw path/query/body و Meter پایدار `Pmcs.Api`؛
- `/health/live` مستقل از `/health/ready` و PostgreSQL readiness check؛
- Bucket Production ازپیش‌ساخته و private؛ auto-create فقط Development؛
- PostgreSQL custom-format backup با SHA-256 و permission محدود؛
- Restore Drill فقط روی دیتابیس جدید با نام کنترل‌شده و بدون drop/overwrite؛
- Runbook بازیابی هم‌نسخه PostgreSQL/Object Storage و Production readiness checklist؛
- CI integration job با PostgreSQL 17، اجرای migration/API، health، auth boundary، security header و هر دو rate limit؛
- ده تست زیرساخت برای claim boundary، configuration fail-fast و correlation id.

## Verification evidence

- Domain + infrastructure tests: ۷۴/۷۴ passed؛
- Frontend unit/API contract tests: ۳۷/۳۷ passed؛
- Persian UI audit و ESLint: passed؛
- Next.js production build: passed؛
- shell syntax، CI YAML parse، Repository architecture guard و `git diff --check`: passed؛
- تمام C# source با reference موقت compile-only هم‌سطح API عمومی JWT: ۰ warning و ۰ error.

## Boundaries and open gates

- محیط جاری اجازه دریافت بسته جدید از NuGet نداد. وابستگی نهایی Repository بسته رسمی `Microsoft.AspNetCore.Authentication.JwtBearer` نسخه 10.0.12 است؛ restore/build نهایی آن باید در CI متصل اجرا و سبز شود. reference موقت بخشی از Repository یا بسته تحویلی نیست.
- Docker/PostgreSQL/MinIO client در محیط حاضر موجود نیست؛ Integration job و smoke script پیاده شده‌اند ولی نتیجه runtime هنوز Gate باز است.
- Identity Provider، دامنه redirect و BFF/PKCE هنوز انتخاب نشده‌اند. Backend trust boundary آماده است، ولی Production login/logout/refresh تا این تصمیم آماده اعلام نمی‌شود.
- TLS termination، WAF/DDoS، Collector/Alert، PITR و Object Storage replication مسئولیت زیرساخت است و checklist آن‌ها باید قبل از Pilot امضا شود.
- OpenAI live test همچنان به secret و داده پایلوت تأییدشده نیاز دارد.
- هدف اولیه RPO/RTO در Runbook پیشنهادی است و باید توسط مالک محصول/عملیات تأیید شود.

## Next checkpoint

«Pilot release gate»: انتخاب Identity Provider و BFF، اجرای CI/Integration واقعی PostgreSQL و S3، اجرای Backup/Restore Drill، اتصال observability، smoke کنترل‌شده OpenAI و سپس UAT محدود با داده‌ای که کاربران خودشان از داخل نرم‌افزار وارد می‌کنند.
