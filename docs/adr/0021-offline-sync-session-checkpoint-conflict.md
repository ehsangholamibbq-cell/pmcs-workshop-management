# ADR 0021 — نشست Sync، Lease آفلاین، Checkpoint و Conflict Case

- Status: Accepted
- Date: 2026-09-12

## Context

Operation Queue پایه، ULID و idempotency از Foundation وجود داشت، اما Push بدون Handshake انجام می‌شد؛ Device/Lease ثبت پایدار نداشت؛ Pull و Checkpoint وجود نداشت؛ و Conflict فقط یک وضعیت محلی بدون رکورد قابل Audit و Resolution بود. این وضعیت برای Pilot چنددستگاهی، لغو دسترسی و بازیابی پس از Crash کافی نبود.

## Decision

- یک ماژول مستقل `Sync` مالک Device Registration، Offline Authorization Lease، Sync Session، Change Feed، Device Checkpoint و Conflict Case است.
- Gateway مستقیماً Persistence ماژول عملیات کارگاهی را نمی‌بیند. `IOfflineFieldOperationHandler` Command را در ماژول مالک اجرا می‌کند و همان Permission/Domain/Audit/Outbox/Idempotency قبلی را حفظ می‌کند.
- Handshake برای یک User/Device/Project، Session مبهم ۱۵دقیقه‌ای و Lease حداکثر هفت‌روزه صادر می‌کند. Protocol و Local Schema باید دقیقاً سازگار باشند.
- Lease قبلی هنگام Handshake جدید `Superseded` می‌شود. فقط Operation ثبت‌شده پیش از زمان Supersede و داخل بازه Lease قابل بررسی است؛ Revoked Lease هرگز پذیرفته نمی‌شود.
- Permission جاری در Handler دوباره بررسی می‌شود. Lease مجوز قدیمی را زنده نگه نمی‌دارد.
- Change Feed فقط Projection صریح و General فعلی را منتشر می‌کند. Full table replication و Cache سبد پروژه وجود ندارد.
- Pull یک Checkpoint Offer کوتاه‌عمر می‌دهد. Client ابتدا Apply می‌کند و سپس Offer را Acknowledge می‌کند؛ Token Checkpoint به User/Device/Project/Dataset مقید و Sequence یکنواخت است.
- Conflict Case قصد محلی و Projection سرور را نگه می‌دارد. Resolution فقط `KeepServer` یا `Reapply with new OperationId` است و History را بازنویسی نمی‌کند.
- عملیات حساس شامل Approval، Decision، Closure، Access Management، Payment و Posting آنلاین باقی می‌مانند.
- PWA محدودیت Remote Wipe و Background Sync را صریح نمایش می‌دهد؛ Session/Lease/Purge-on-reconnect کنترل واقعی هستند.

## Consequences

- Crash پس از Commit سرور با Replay Inbox و ثبت دیرهنگام Change Feed بازیابی می‌شود.
- Crash پس از Apply محلی و پیش از Acknowledge، Change را دوباره Pull می‌کند ولی Store محلی با `changeId` idempotent است.
- چند Device برای یک User، Queue و Checkpoint مستقل دارند؛ Sequence سرور مرجع Pull است.
- کاهش Permission باعث رد Operation در سرور می‌شود و داده محلی برای Secure Review باقی می‌ماند.
- Conflict به‌عنوان وضعیت کسب‌وکار Audit می‌شود و خطای شبکه تلقی نمی‌گردد.
- Change Feed عمومی همه Commandهای آنلاین، Multipart Evidence، Malware Scan و آزمون هفت‌روزه روی Device واقعی در Pilot Release Gate باقی می‌مانند.
