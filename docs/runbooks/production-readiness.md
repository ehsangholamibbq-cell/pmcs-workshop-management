# Production readiness checklist

## Identity and network

- [ ] Keycloak Production بدون Realm/User نمونه، با نسخه pin‌شده و برنامه Patch تصویب شده است.
- [ ] issuer، audience، redirect URL، logout URL و hostname مدیریتی جدا تصویب شده‌اند.
- [x] PWA از Authorization Code + PKCE/BFF استفاده می‌کند و Token در Local Storage/IndexedDB نیست.
- [ ] OTP، SMTP، recovery code، brute-force protection و فرآیند help desk آزموده شده‌اند.
- [ ] Service Account مدیریت کاربران فقط `manage-users/view-users` دارد و Secret آن از Secret Store تزریق و دوره‌ای چرخانده می‌شود.
- [ ] دعوت، ارسال مجدد، انقضا، لغو، ایمیل ناموجود و قطع موقت Keycloak/SMTP در UAT آزموده شده‌اند.
- [ ] تعلیق حساب فوراً Permission داخلی را می‌بندد و Disable/Logout Provider با Retry کامل می‌شود.
- [ ] آخرین مدیر فعال، Self-lockout و اتصال‌ندادن ایمیل موجود بدون marker دعوت آزموده شده‌اند.
- [ ] پایگاه BFF دارای TLS، least privilege، Backup و monitoring است.
- [ ] TLS در edge فعال و `AllowedHosts` و `PMCS_WEB_ORIGINS` دقیق‌اند.
- [ ] WAF/DDoS protection و rate limit لبه با load test تنظیم شده‌اند.
- [ ] Headerهای Development و seed در Production غیرفعال‌اند.
- [ ] `PMCS_AUTH_ALLOW_INSECURE_HTTP=false` و Cookieها Secure/HttpOnly/SameSite هستند.

## Data and dependencies

- [ ] PostgreSQL Production دارای TLS، least privilege، PITR و monitoring است.
- [ ] Bucket private از قبل ساخته و Versioning/replication/retention فعال شده است.
- [ ] OpenAI key/model فقط در صورت فعال‌سازی AI از Secret Store تزریق شده‌اند.
- [ ] هیچ داده نمونه یا credential توسعه‌ای در محیط وجود ندارد.
- [ ] نبود WBS، Budget baseline یا HSE به‌عنوان وضعیت معتبر Optional آزموده شده است.

## Persian calendar boundary

- [x] تمام ورودی‌های تاریخ UI از Date Picker مشترک هجری شمسی استفاده می‌کنند.
- [x] تمام خروجی‌های تاریخ/زمان UI از Formatter مرکزی با ارقام فارسی و منطقه زمانی تهران استفاده می‌کنند.
- [x] API، Backend و PostgreSQL تاریخ را همچنان با ISO/UTC، `DateOnly` و `DateTimeOffset` نگه می‌دارند.
- [x] `audit:calendar` ورود بومی میلادی و Formatter پراکنده را در CI رد می‌کند.
- [ ] ورود دستی، لمس، صفحه‌کلید، نوروز و اسفند کبیسه روی دستگاه‌های واقعی Pilot آزموده شده‌اند.
- [ ] هر خروجی PDF/Excel افزوده‌شده به Scope، فقط تاریخ شمسی قابل مشاهده دارد.

## Operability

- [ ] `/health/live` فقط حیات process و `/health/ready` اتصال PostgreSQL را گزارش می‌کند.
- [ ] Logها با correlation id جمع‌آوری و body، query string، Token و payload AI حذف می‌شوند.
- [ ] Meter با نام `Pmcs.Api` و meterهای داخلی ASP.NET/Npgsql به Collector متصل‌اند.
- [ ] alertهای 5xx، latency، saturation، migration failure، queue backlog و storage failure تعریف شده‌اند.
- [ ] Backup checksumدار و Restore Drill هم‌نسخه PostgreSQL/Object Storage موفق است.

## Offline and sync

- [x] Protocol نسخه ۲، Local Schema نسخه ۵ و حداقل App Version در Handshake به‌صورت fail-closed کنترل می‌شوند.
- [x] Sync Session پانزده‌دقیقه‌ای و Offline Lease حداکثر هفت‌روزه به Tenant/User/Device/Project و Authorization Version مقیدند.
- [x] Permission و قواعد دامنه هنگام Push دوباره بررسی می‌شوند و Lease جایگزین مجوز جاری نیست.
- [x] Apply محلی Change Feed پیش از Acknowledge نقطه کنترل انجام می‌شود و `changeId` از تکرار رکورد جلوگیری می‌کند.
- [x] Conflict Case هر دو سمت را بدون بازنویسی نگه می‌دارد و فقط Keep Server یا Reapply با Operation ID جدید دارد.
- [x] لغو دستگاه Session و Lease را می‌بندد و محدودیت Remote Wipe در PWA صریح است.
- [x] Approval، Decision، Closure حساس، Access Management، Payment و Posting آفلاین نیستند.
- [ ] Migration، Unique Constraint، Crash-after-commit و Checkpoint retry روی PostgreSQL 17 واقعی آزموده شده‌اند.
- [ ] دو User و دو Device، قطع دسترسی، لغو دستگاه، Clock Skew و Client Update اجباری در UAT دستگاه واقعی پاس شده‌اند.
- [ ] صف هفت‌روزه و صدها Attachment روی دستگاه هدف از نظر زمان، فضا و Battery اندازه‌گیری شده‌اند.
- [ ] Multipart Resume، Malware Scan/Quarantine و Object Storage واقعی پاس شده‌اند.

## Release gates

- [x] Release Identity در Artifactهای Web/API تعبیه و در endpointهای مستقل قابل‌تطبیق است.
- [x] Candidate validator، Commit/Hash/expiry و مجموعه دقیق هشت Gate را fail-closed کنترل می‌کند.
- [x] تأیید Product/Security/Operations باید توسط سه هویت متفاوت و پس از Evidenceها انجام شود.
- [x] Smoke استقرار، Web و API را با Commit/نسخه/زمان Build همان Candidate تطبیق می‌دهد.
- [ ] build/test/audit فارسی و integration smoke در CI سبز است.
- [ ] migration روی clone از Production و rollback عملیاتی تمرین شده است.
- [ ] تست 401/403 و Tenant isolation برای دو Tenant واقعیِ آزمایشی موفق است.
- [ ] Offline capture، restart، retry، pull/checkpoint و conflict resolution روی دستگاه کارگاه آزموده شده است.
- [ ] تغییر حساب روی یک دستگاه، داده آفلاین و Cache کاربر قبلی را نمایش نمی‌دهد.
- [ ] UAT با داده‌ای که کاربران خودشان وارد کرده‌اند انجام و sign-off ثبت شده است.
