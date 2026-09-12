# قرارداد Session و BFF نسخه ۱

## مسیر Browser

مرورگر فقط مسیر هم‌مبدأ `/api/pmcs/api/v1/*` را فراخوانی می‌کند. BFF نشست Cookie را به‌صورت server-side بررسی، Token معتبر را Refresh و درخواست را به API داخلی ارسال می‌کند. `Authorization`، `Cookie`، `X-Tenant-Id`، `X-User-Id` و Headerهای Proxy ورودی هرگز به API منتقل نمی‌شوند.

مسیرهای خارج از `/api/v1` از این دروازه عبور نمی‌کنند. پاسخ‌های عبوری `no-store` هستند و `Set-Cookie` سرویس داخلی به Browser منتقل نمی‌شود.

## Session

`GET /api/pmcs/api/v1/session` پس از اعتبارسنجی OIDC و حساب داخلی فعال، این اطلاعات غیرحساس را برمی‌گرداند:

```json
{
  "userId": "uuid",
  "tenantId": "uuid",
  "deviceId": null,
  "displayName": "نام نمایشی",
  "email": "user@example.com",
  "tenantRole": "Member",
  "tenantName": "نام سازمان",
  "authentication": "oidc-access-token"
}
```

- نشست نامعتبر یا منقضی: `401`
- Token معتبر ولی حساب یا Tenant غیرفعال/ناموجود: `403`
- سرویس داخلی در دسترس نیست: `503`

Role داخل Token منبع مجوز نیست. نقش Tenant، عضویت پروژه، Scope ماژول و Operation از دیتابیس PMCS خوانده می‌شوند.
