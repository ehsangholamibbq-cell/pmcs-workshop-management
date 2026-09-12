# ADR 0013 — هویت مستقل و مرز واسط Backend برای Frontend

- Status: Accepted
- Date: 2026-09-10

## Context

سازمان مقصد سامانه ورود یکپارچه موجود ندارد. قرار دادن رمز یا Token در برنامه وب، استفاده مستقیم مرورگر از API و اتکا به Role داخل Token با مرزهای Tenant/Project و کار آفلاین PMCS سازگار نیست.

## Decision

Keycloak به‌عنوان Identity Provider مستقل و قابل‌استقرار انتخاب می‌شود. برنامه وب Next.js الگوی Backend for Frontend (BFF) را اجرا می‌کند:

- Authorization Code همراه PKCE و OIDC nonce؛
- Cookie نشست `HttpOnly/Secure/SameSite=Lax`؛
- نگهداری رمز‌شده Access/Refresh/ID Token در PostgreSQL اختصاصی BFF؛
- Refresh فقط در Server و عدم دسترسی JavaScript مرورگر به Token؛
- جایگزینی هر Header هویتی Browser با Access Token دریافت‌شده در BFF؛
- اعتبارسنجی issuer، audience، امضا و expiry در API؛
- محاسبه Permission فقط از IdentityAccess داخلی و رد حساب یا Tenant غیرفعال؛
- تفکیک IndexedDB و Cacheهای محلی به ازای Tenant و User و پاک‌سازی هنگام خروج صریح.

`sub` کاربر Keycloak برابر UUID حساب PMCS و `tenant_id` یک Claim اجباری است. نبود WBS، بودجه اولیه یا HSE هیچ اثری بر ورود یا مجوز پایه ندارد.

## Consequences

- Keycloak و پایگاه نشست دو وابستگی عملیاتی جدید هستند و باید Backup، Monitoring و Patch شوند.
- ساخت کاربر در Identity Provider به‌تنهایی دسترسی ایجاد نمی‌کند؛ حساب متناظر PMCS باید فعال باشد.
- Realm نمونه فقط برای Development است. Production باید Realm جدا، Secret واقعی، SMTP، TLS، نام میزبان مدیریتی جدا و فرآیند بازیابی داشته باشد.
- صفحه‌های احراز‌شده در Cache عمومی Service Worker ذخیره نمی‌شوند؛ بازکردن نشست جدید در حالت کاملاً آفلاین مجاز نیست.
