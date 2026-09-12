# Runbook بازیابی آفلاین و همگام‌سازی

## بررسی اولیه کاربر

1. Banner اتصال باید «آنلاین» باشد؛ Online بودن Network به‌تنهایی سلامت API نیست.
2. Manual Sync را اجرا کنید؛ این مسیر حتی با Queue خالی Handshake/Pull را تازه می‌کند.
3. در «مرکز تعارض و همگام‌سازی» زمان آخرین ارسال، آخرین دریافت و انقضای Lease را بررسی کنید.
4. Conflict را فقط پس از مقایسه قصد محلی و نسخه رسمی با یکی از گزینه‌های صریح تعیین تکلیف کنید.
5. Rejected یا Evidence ارسال‌نشده را با پاک‌کردن Cache مرورگر حذف نکنید.

## معنی خطاهای اصلی

| Code | اقدام |
| --- | --- |
| `sync.session.invalid_or_expired` | Manual Sync برای Handshake جدید |
| `sync.device.revoked` | ارسال را متوقف و Pending Data را برای Secure Recovery بررسی کنید |
| `sync.lease.operation_not_covered` | Operation را خودکار Retry نکنید؛ زمان/Lease/Permission را بررسی کنید |
| `sync.checkpoint.mismatch` | Bootstrap کنترل‌شده از Feed مجاز؛ Full database دانلود نشود |
| `sync.operation.reused` | احتمال reuse شناسه با Payload متفاوت؛ Incident داده/امنیت ثبت شود |
| `sync.operation.dependency.blocked` | Parent یا Evidence وابسته را تعیین تکلیف کنید |
| `sync.protocol.unsupported` | Client را به نسخه سازگار ارتقا دهید |

## دستگاه گمشده یا مشکوک

1. از حساب روی یک دستگاه سالم وارد شوید.
2. در فهرست دستگاه‌ها، Registration هدف را با دلیل لغو کنید.
3. سامانه همه Sessionها و Leaseهای همان User/Device را می‌بندد.
4. انتظار Remote wipe قطعی از PWA کاملاً آفلاین نداشته باشید.
5. در اتصال بعدی، پاسخ `purgeRequired` باید دریافت و داده خارج Scope پاک شود.
6. اگر Pending Evidence یا Operation روی دستگاه وجود دارد، قبل از Purge مسیر Secure Recovery سازمان را اجرا کنید.

## Conflict

- `KeepServer`: قصد محلی کنار گذاشته می‌شود و Fact رسمی تغییر نمی‌کند.
- `Reapply`: Client یک Operation ID جدید و Base Revision فعلی می‌سازد؛ Original حل‌شده و قابل Audit می‌ماند.
- Owner، Due، Severity، State، Approval، مبلغ، موجودی و Closure هرگز با Merge عمومی حل نشوند.
- Conflict مالی/قراردادی/موجودی در این Gateway عمومی Resolve نمی‌شود و باید به Route تخصصی ماژول برود.

## Diagnostics امن برای پشتیبانی

مجاز برای ثبت در Ticket:

- Correlation ID؛
- Device ID و App/Schema/Policy Version؛
- تعداد Pending/Conflict و سن قدیمی‌ترین Operation؛
- زمان آخرین Handshake/Push/Pull؛
- Error Code؛
- Checkpoint Sequence و Watermark.

ممنوع:

- Access/Refresh Token یا Session Cookie؛
- Payload کامل گزارش، شرح حادثه یا داده پزشکی/حقوقی؛
- عکس و فایل اصلی؛
- رمز، Secret یا Header Authorization.

## Gate عملیاتی Pilot

- Migration و Unique/Concurrency constraintها روی PostgreSQL 17 واقعی اجرا شوند.
- سناریوی Crash-after-commit و Pull تکراری با API و Database واقعی پاس شود.
- دو User و دو Device روی یک Project Day آزموده شوند.
- Permission revoke، Device revoke، Clock skew و Client update اجباری آزموده شوند.
- Queue هفت روز عملیات متعارف و صدها فایل روی Device هدف اندازه‌گیری شود.
- Multipart resume، Malware Scan/Quarantine و Object Storage واقعی جداگانه پاس شوند.
