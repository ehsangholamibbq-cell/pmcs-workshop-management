# Production authentication boundary

## تصمیم V1

چون سازمان SSO موجود ندارد، Keycloak 26.7.3 Identity Provider مستقل V1 است. PMCS صادرکننده Token نیست؛ Keycloak Access Token امضاشده و کوتاه‌عمر صادر می‌کند و API فقط امضا، issuer، audience و expiry را اعتبارسنجی می‌کند. Claim mapping داخلی ASP.NET غیرفعال است.

Token معتبر باید حداقل این دو Claim دامنه‌ای را داشته باشد:

| Claim | قرارداد |
| --- | --- |
| `sub` | شناسه UUID حساب PMCS؛ باید با User فعال Tenant منطبق باشد |
| `tenant_id` | شناسه UUID Tenant؛ مرز قطعی جداسازی داده |
| `device_id` | اختیاری؛ شناسه دستگاه Field برای Sync و Audit |

وجود Role در Token مجوز پروژه ایجاد نمی‌کند. تمام مجوزهای Tenant/Project/Module/Operation پس از احراز هویت و از داده داخلی IdentityAccess محاسبه می‌شوند؛ بنابراین تغییر عضویت پروژه به صدور مجدد Token وابسته نیست.

## Configuration اجباری

در هر محیط غیر Development:

```text
Authentication__Authority=https://identity.example.com/
Authentication__Audience=pmcs-api
Authentication__MetadataAddress=https://identity-internal.example.com/realms/pmcs/.well-known/openid-configuration
PMCS_WEB_ORIGINS=https://pmcs.example.com
AllowedHosts=api.pmcs.example.com
PMCS_DEV_IDENTITY_ENABLED=false
PMCS_SEED_ENABLED=false
ObjectStorage__CreateBucketIfMissing=false
PMCS_WEB_URL=https://pmcs.example.com
PMCS_INTERNAL_API_URL=https://api.internal.example.com
PMCS_OIDC_PUBLIC_ISSUER=https://identity.example.com/realms/pmcs
PMCS_OIDC_INTERNAL_ISSUER=https://identity-internal.example.com/realms/pmcs
PMCS_WEB_AUTH_DATABASE_URL=postgresql://user:password@database:5432/pmcs_web_auth?sslmode=verify-full
PMCS_AUTH_ALLOW_INSECURE_HTTP=false
IdentityProvisioning__Enabled=true
IdentityProvisioning__BaseUrl=https://identity-admin.example.com
IdentityProvisioning__Realm=pmcs
IdentityProvisioning__ClientId=pmcs-identity-admin
IdentityProvisioning__ClientSecret=<secret-store-reference>
IdentityProvisioning__WebClientId=pmcs-web
IdentityProvisioning__WebReturnUrl=https://pmcs.example.com
```

Connection string و credentialهای Object Storage باید از Secret Store تزریق شوند. وجود credential توسعه‌ای، Authority غیر HTTPS، Origin غیر HTTPS، `AllowedHosts=*` یا ساخت خودکار Bucket باعث توقف Startup می‌شود.

چند Origin با ویرگول یا `;` جدا می‌شود. Wildcard CORS پشتیبانی نمی‌شود.

## Browser/PWA session boundary

Next.js نقش BFF را دارد. Login با Authorization Code + PKCE S256 و OIDC nonce انجام می‌شود. Client Secret و Access/Refresh/ID Token فقط در Server هستند؛ Tokenها در PostgreSQL اختصاصی BFF با AES-256-GCM رمز می‌شوند. Browser فقط Cookie نشست `HttpOnly/Secure/SameSite=Lax` دارد و JavaScript به Token دسترسی ندارد.

BFF فقط مسیر `/api/v1` را عبور می‌دهد، Headerهای هویتی و Proxy ورودی را حذف و `Authorization` معتبر Server را جایگزین می‌کند. API پاسخ را `no-store` دریافت می‌کند و `Set-Cookie` داخلی به Browser عبور نمی‌کند.

`PMCS_AUTH_SECRET` و Client Secret حداقل‌های طول را رعایت می‌کنند و مقدارهای نمونه در حالت عملیاتی رد می‌شوند. HTTP فقط با پرچم صریح و تنها برای localhost/شبکه تک‌بخشی Compose پذیرفته می‌شود.

## Provisioning

ساخت User در Keycloak دسترسی PMCS ایجاد نمی‌کند. `sub` باید UUID همان User فعال در IdentityAccess باشد و attribute اجباری `tenant_id` باید UUID Tenant فعال را حمل کند. تغییر Role یا Membership فقط در PMCS انجام می‌شود و نیاز به صدور دوباره Token ندارد.

پنل `/admin/users` تنها برای مدیر فعال Tenant است. دعوت‌ها در صف پایدار ثبت می‌شوند و Service Account محرمانه `pmcs-identity-admin` فقط نقش‌های `manage-users` و `view-users` دارد. حساب بیرونی با `pmcs_invitation_id` به دعوت دقیق PMCS متصل می‌شود؛ تشابه ایمیل برای تصاحب یک حساب موجود کافی نیست. شناسه حساب بیرونی پیش از تحویل ایمیل پایدار می‌شود تا لغو، انقضا یا شکست نهایی بتواند حذف Idempotent آن را Retry کند. تعلیق ابتدا مجوز داخلی و همه توکن‌های صادرشده پیشین را می‌بندد و سپس Disable/Logout Provider را Retry می‌کند.

هر درخواست احراز‌شده باید Claim استاندارد `iat` داشته باشد. API وضعیت فعال Tenant/User را از مرجع داخلی کنترل می‌کند و فقط توکنی را می‌پذیرد که پس از آخرین تغییر وضعیت حساب صادر شده باشد. Adapter توسعه از این Claim معاف است، اما فقط در Development و با سوییچ صریح فعال می‌شود.

Realm موجود در Repository مخصوص Development است و فقط از طریق mount خواندنی Compose محلی وارد می‌شود؛ Image بهینه Keycloak آن را درون خود ندارد. Production باید Realm را از مسیر Provisioning جدا و بدون Demo user بسازد، ثبت‌نام عمومی را بسته نگه دارد و SMTP، OTP، brute-force protection، TLS، Backup و hostname مدیریتی جدا داشته باشد.

## Acceptance checks پیش از Pilot

- Token با issuer، audience، signature یا expiry نادرست پاسخ `401` بگیرد.
- Token معتبر بدون `tenant_id` یا `sub` UUID پاسخ `401` بگیرد.
- User غیرفعال یا فاقد عضویت، حتی با Token معتبر، مجوز پروژه نگیرد.
- آخرین مدیر فعال سازمان و حساب جاری مدیر از Self-lockout محافظت شوند.
- دعوت موجود با ایمیل مشابه اما بدون marker دعوت PMCS به UserAccount داخلی متصل نشود.
- تعلیق داخلی پیش از پاسخ Provider مؤثر و Retryهای قدیمی به وضعیت نهایی حساب همگرا شوند.
- Headerهای `X-Tenant-Id/X-User-Id` در Production بی‌اثر باشند.
- logout و refresh و revoke طبق رفتار Provider و BFF آزموده شوند.
- Token یا Claimهای حساس در Log، URL، IndexedDB و Local Storage ثبت نشوند.
- تغییر حساب روی یک Device، IndexedDB و Cache کاربر قبلی را نمایش ندهد.
