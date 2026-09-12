# Development identity contract

این قرارداد فقط در `Development` و زمانی که `PMCS_DEV_IDENTITY_ENABLED=true` باشد فعال است.

```http
X-Tenant-Id: 11111111-1111-1111-1111-111111111111
X-User-Id: 22222222-2222-2222-2222-222222222222
```

این Headerها توسط Authentication Scheme مستقل `PmcsDevelopment` به Claimهای `tenant_id`، `sub` و در صورت وجود `device_id` تبدیل می‌شوند. اگر Header نامعتبر یا خالی باشد، API درخواست را با `401` رد می‌کند.

در هر محیط غیر Development این Scheme همواره `NoResult` می‌دهد. علاوه بر آن Startup در صورت فعال‌بودن `PMCS_DEV_IDENTITY_ENABLED` یا `PMCS_SEED_ENABLED` متوقف می‌شود. ارسال همین Headerها در Production هویت ایجاد نمی‌کند.

درخواست دارای `Authorization: Bearer ...` حتی در Development به JWT Bearer Scheme هدایت می‌شود و Header توسعه‌ای نمی‌تواند Bearer نامعتبر را دور بزند.
