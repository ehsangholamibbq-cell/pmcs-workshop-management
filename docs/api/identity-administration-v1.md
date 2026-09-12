# قرارداد مدیریت کاربران و دسترسی‌ها نسخه ۱

همه مسیرها زیر `/api/v1/identity` فقط برای UserAccount فعال با نقش `TenantAdministrator` در همان Tenant قابل استفاده‌اند. مسیرهای Mutation به `Idempotency-Key` نیاز دارند و در Rate Limit مستقل مدیریت هویت قرار می‌گیرند.

## فهرست مدیریتی

`GET /directory`

حساب‌های Tenant، وضعیت همگام‌سازی Provider، عضویت‌های پروژه، ۱۰۰ دعوت اخیر و کاتالوگ نقش‌های مجاز را برمی‌گرداند. داده Tenant دیگر هرگز وارد پاسخ نمی‌شود.

## دعوت

`POST /invitations`

```json
{
  "displayName": "نام کاربر",
  "email": "user@example.com",
  "tenantRole": "Member",
  "projects": [
    {
      "projectId": "uuid",
      "roleCode": "Observer"
    }
  ]
}
```

- پاسخ `202` فقط ثبت پایدار صف را تأیید می‌کند؛ به معنی ارسال قطعی ایمیل نیست.
- همه Project IDها باید واقعاً متعلق به Tenant باشند.
- حداکثر ۵۰ عضویت اولیه و فقط یک Role برای هر Project پذیرفته می‌شود.
- UserAccount داخلی فقط بعد از ساخت/بازیابی حساب مدیریت‌شده Keycloak و ارسال موفق ایمیل ساخته می‌شود.
- `POST /invitations/{invitationId}/resend` دعوت را با مهلت جدید در صف می‌گذارد.
- `POST /invitations/{invitationId}/revoke` دعوت ارسال‌نشده را لغو می‌کند.

وضعیت‌ها: `Queued`، `Processing`، `RetryScheduled`، `Sent`، `Failed`، `Revoked` و `Expired`.

## چرخه حساب

`PUT /users/{userId}/status`

بدنه یکی از `Active`، `Suspended` یا `Deactivated` است. تعلیق/غیرفعال‌سازی ابتدا مرجع مجوز داخلی را می‌بندد و سپس `DisableAndLogout` را برای Keycloak صف‌بندی می‌کند. `Deactivated` در V1 نهایی است و به Active برنمی‌گردد.

`PUT /users/{userId}/tenant-role`

Role سازمانی یکی از `Member`، `PortfolioViewer` یا `TenantAdministrator` است. آخرین مدیر فعال و Self-lockout محافظت می‌شوند.

## عضویت پروژه

- `PUT /users/{userId}/memberships/{projectId}` با بدنه `roleCode` عضویت را می‌سازد یا فعال و به‌روزرسانی می‌کند.
- `DELETE /users/{userId}/memberships/{projectId}` عضویت را با EndsAt و وضعیت `Revoked` خاتمه می‌دهد.

Roleهای پروژه محدود به کاتالوگ سرور هستند. نبود WBS، بودجه اولیه، HSE یا Quality مانع عضویت نیست.

## خطا و ممیزی

Provider response body، Token و Secret در API یا رابط منتشر نمی‌شوند. فقط کد خطای امن نگهداری می‌شود. هر Mutation شامل Actor، Tenant، قبل/بعد لازم، Resource، زمان، Correlation ID، Outbox و رسید Idempotency است.
