# Foundation checkpoint 11 — identity administration

- Date: 2026-09-11
- Status: Implemented; live Keycloak/SMTP and browser UAT remain environment gates
- Product source: PMCS Blueprint V1.13

## Scope delivered

- پنل فارسی `/admin/users` برای مدیر سازمان؛
- دعوت پایدار و Idempotent با نقش Tenant و عضویت اولیه اختیاری پروژه؛
- Worker Provisioning با Retry/backoff محدود و بازیابی کار نیمه‌تمام؛
- Keycloak Admin REST با Client Credentials و Service Account کم‌اختیار؛
- الزام تأیید ایمیل، تغییر رمز و پیکربندی رمز یک‌بارمصرف در دعوت؛
- markerهای `pmcs_invitation_id` و `tenant_id` برای جلوگیری از اتصال حساب موجود صرفاً با ایمیل؛
- ساخت UserAccount/ProjectMembership فقط پس از ارسال موفق دعوت؛
- تعلیق/غیرفعال‌سازی fail-closed همراه Disable و Logout نشست‌های Keycloak در صف پایدار؛
- همگرایی Retry به آخرین وضعیت داخلی حساب؛
- تغییر نقش سازمانی و عضویت پروژه با Tenant validation، Audit، Outbox و Idempotency؛
- حفاظت از آخرین مدیر فعال و جلوگیری از Self-lockout؛
- Rate Limit مستقل مسیر مدیریت هویت و Fail-fast تنظیمات HTTPS/Secret در Production؛
- Development realm با Service Account و تست Container برای مجوز Admin REST.

## Verification

- ۵۴ تست Frontend، ESLint، ممیزی رابط فارسی و Next.js production build موفق؛
- ۸۵ تست C# Domain/Infrastructure و Build کامل بدون Warning موفق؛
- تست HttpClient قرارداد Keycloak برای ساخت User، Required Actions و رد ایمیل تصاحب‌نشده موفق؛
- Architecture Guard با ۲۰۳ فایل ماژول، JSON validation و `git diff --check` موفق.

## Open environment gates

- Docker در نشست فعلی موجود نیست؛ Realm import و Service Account smoke در job CI تعریف شده ولی محلی اجرا نشده است.
- SMTP واقعی، رسیدن ایمیل فارسی، تکمیل Required Actions و ورود نخست باید در محیط آزمایشی سازمان اجرا شود.
- بسته رسمی JWT در شبکه این نشست قابل دریافت نبود؛ Build محلی با reference موقت خارج Repository انجام و سپس حذف شد. CI متصل باید Package رسمی را Restore و اجرا کند.
- تست یکپارچه واقعی PostgreSQL برای migration جدید و سناریوی صف/Restart در CI یا محیط دارای PostgreSQL باقی است.

## Invariants preserved

- حساب Keycloak بدون UserAccount فعال PMCS هیچ مجوزی ندارد.
- Tenant role و Project membership فقط از دیتابیس PMCS خوانده می‌شوند.
- WBS، بودجه اولیه، تقویم، HSE و Quality اختیاری باقی مانده‌اند.
- AI هیچ Fact، State، Permission یا اقدام رسمی را تغییر نمی‌دهد.
