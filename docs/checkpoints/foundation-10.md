# Foundation checkpoint 10 — standalone identity and BFF

- Date: 2026-09-10
- Status: Implemented; live container and browser UAT remain environment gates
- Product source: PMCS Blueprint V1.12

## Scope delivered

- Keycloak 26.7.3 به‌صورت image بهینه و pin‌شده همراه PostgreSQL مستقل؛ Image عملیاتی فاقد Realm/User نمونه است و Realm توسعه فقط با mount خواندنی Compose وارد می‌شود؛
- Realm Development قابل import با ثبت‌نام عمومی بسته، محدودسازی brute force، عمر پنج‌دقیقه‌ای Access Token و الزام تغییر رمز/OTP در ورود نخست؛
- Client محرمانه `pmcs-web` با Authorization Code، PKCE S256، redirect محدود و audience مستقل `pmcs-api`؛
- Claim اجباری `tenant_id` از attribute کاربر و `sub` پایدار برابر UUID حساب PMCS؛
- Better Auth 1.7.4 به‌عنوان BFF دیتابیس‌محور، Token encryption، state دیتابیس‌محور، rate limit و Cookie امن؛
- جداسازی Public issuer از Backchannel endpoint برای شبکه Container بدون تغییر issuer Token؛
- Proxy هم‌مبدأ محدود به `/api/v1`، حذف Headerهای قابل جعل و عدم عبور Cookie/Token Browser؛
- Session واقعی از API شامل حساب و Tenant فعال، نام، نقش و نوع احراز هویت؛
- مسدودشدن کامل مجوز پروژه برای User یا Tenant غیرفعال؛
- صفحه ورود فارسی، خروج هماهنگ Provider/BFF و محافظ مسیرهای پروژه و Portfolio؛
- تفکیک IndexedDB و local cache برای هر Tenant/User و حذف داده همان Scope هنگام خروج امن؛
- Service Worker بدون Cache صفحه احراز‌شده و fallback عمومی آفلاین؛
- SQL idempotent پایگاه BFF، Compose development و smoke واردشدن Realm در CI.

## Verification

- ۵۰ تست Frontend شامل مرز محیط، Path/Header/Response BFF، مقصد امن بازگشت، استقرار هویت، Headerهای امنیتی، تفکیک حافظه محلی و قابلیت‌های قبلی؛
- ESLint بدون warning؛
- ممیزی رابط فارسی موفق؛
- Next.js production build و TypeScript موفق؛
- Realm JSON، Compose YAML، shell syntax و `git diff --check` معتبر؛
- Build محلی .NET بدون Warning و هر ۷۵ تست Domain/Infrastructure موفق بود. به‌علت نبود بسته رسمی JWT در کش آفلاین، Compile محلی با Reference سازگار موقت انجام و سپس حذف شد؛ Handler رسمی JWT همچنان باید در CI متصل اجرا شود.
- Smoke خروجی Standalone: صفحه ورود و پیام خطای فارسی پاسخ ۲۰۰ همراه Headerهای امنیتی دادند؛ مسیر محافظت‌شده بدون پایگاه نشست با پاسخ ۵۰۰ بسته ماند و به حالت عمومی سقوط نکرد.

## Open environment gates

- Docker در نشست فعلی موجود نیست؛ import واقعی Realm، Schema validation پایگاه BFF و Authorization Code callback در job جدید CI و سپس محیط استقرار باید اجرا شوند.
- Production image هیچ Realm/User نمونه‌ای حمل نمی‌کند؛ Provisioning عملیاتی همچنان باید SMTP، TLS، hostname مدیریتی جدا، Secret Store، Backup و monitoring داشته باشد.
- Login/Refresh/Logout، revoke، expiry، حساب suspended، Tenant isolation دو سازمان و پاک‌سازی device data باید در UAT مرورگر واقعی آزموده شوند.
- ساخت و دعوت کاربران Production هنوز از فرآیند مدیریت هویت انجام می‌شود؛ پنل self-service مدیریت کاربران بخشی از Slice بعدی Identity Administration است.

## Invariants preserved

- WBS، بودجه اولیه، تقویم پروژه، HSE و Quality اختیاری و مستقل باقی مانده‌اند.
- AI هیچ Fact، State، مجوز یا اقدام رسمی را تغییر نمی‌دهد.
- داده آفلاین تا پذیرش Server حقیقت رسمی نیست.
