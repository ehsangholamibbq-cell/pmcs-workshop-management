# Foundation checkpoint 18 — Offline/Sync & Conflict Center Hardening V1

- Date: 2026-09-12
- Status: Implemented; PostgreSQL/object-storage integration and real-device endurance remain Pilot gates
- Baseline: Checkpoint 17 (`7fd9ee1`)

## Scope delivered

- bounded context مستقل `Sync` برای Device Registration، Offline Authorization Lease، Sync Session، Change Feed، Device Checkpoint و Conflict Case؛
- Handshake سازگار با Protocol نسخه ۲، Local Schema نسخه ۵، حداقل App Version و Dataset Manifest حداقلی؛
- Session مبهم پانزده‌دقیقه‌ای و Lease حداکثر هفت‌روزه، مقید به Tenant/User/Device/Project و Authorization Version؛
- supersede و revoke صریح Lease، کنترل زمان ثبت دستگاه، Clock Skew، سن عملیات، ترتیب محلی و Dependency؛
- Push عملیات Operation-based با Batch مستقل، Idempotency، Correlation و نتیجه‌های Applied/Conflict/Rejected/Unsupported؛
- انتقال ثبت واقعیت گزارش روزانه از Sync Gateway به Handler ماژول `FieldOperations` بدون دسترسی Gateway به Persistence آن ماژول؛
- بازاعتبارسنجی Permission، Project، Measurement Item و قواعد دامنه در Handler مالک هنگام پذیرش؛
- Pull فقط از Projection مجاز و General، بدون Full Database replication یا Payload حساس؛
- Apply-before-ack در IndexedDB، Store مجزای Changeهای اعمال‌شده و Checkpoint Offer کوتاه‌عمر و یکنواخت؛
- Conflict Case پایدار با قصد محلی، Projection و Revision سرور، Actor، Device، Reason و Audit؛
- Resolution فقط Keep Server یا Reapply با Operation ID جدید؛ Original هیچ‌گاه بازنویسی نمی‌شود؛
- مرکز فارسی همگام‌سازی و تعارض شامل وضعیت Queue، آخرین Push/Pull، Lease، Clock Skew، Bootstrap، تعارض‌ها و دستگاه‌ها؛
- لغو دستگاه همراه بستن همه Sessionها و Leaseهای همان User/Device و بیان صریح محدودیت Remote Wipe در PWA؛
- Diagnostics بدون Payload، Token، عکس یا متن حساس؛
- اتصال Queueهای Daily Report، Evidence و Intake موقت Quality/HSE به Lease/Handshake جدید؛
- Migration اولیه `sync-control:20260912-001`، ADR 0021، قرارداد API نسخه ۲ و Runbook بازیابی.

## Semantic and security safeguards

- داده محلی تا پذیرش سرور Fact رسمی نیست و Server مرجع Canonical باقی می‌ماند.
- Lease مجوز جاری را زنده نگه نمی‌دارد؛ Permission در هر Push دوباره بررسی می‌شود.
- General last-write-wins و Merge خودکار Owner، Due، State، Approval، مبلغ یا موجودی وجود ندارد.
- Approval، Decision، Closure حساس، Access Management، Payment و Financial/Inventory Posting آنلاین باقی می‌مانند.
- دستگاه یا Lease لغوشده هیچ Operation جدیدی را پوشش نمی‌دهد؛ Supersede فقط ثبت‌های واقعاً پیش از آن را قابل بررسی نگه می‌دارد.
- Full Database و Cache سبد پروژه روی دستگاه وجود ندارد؛ Change Feed فقط Projection صریح و مجاز است.
- WBS، Budget و HSE همچنان اختیاری‌اند؛ نبود آن‌ها خطا یا سلامت ساختگی نیست.
- AI Advisory هیچ Fact، Conflict Resolution، Permission، Checkpoint یا اقدام رسمی Sync را تغییر نمی‌دهد.

## Verification

- ۱۸۵ تست C# شامل سازگاری پروتکل، Lease binding/window، supersede/revoke، Session scope/expiry و Conflict resolution موفق؛
- ۸۷ تست Frontend/API contract شامل Handshake، immutable envelope، apply-before-ack، BFF header boundary و IndexedDB schema موفق؛
- Build Release Backend با Package رسمی JWT و بدون Warning/خطا موفق؛
- ESLint، TypeScript، ممیزی رابط فارسی و Next.js production build موفق؛
- Architecture Guard روی ۳۱۱ فایل C#، syntax اسکریپت‌های عملیاتی و `git diff --check` موفق؛
- Smoke بسته standalone: صفحه ورود، Manifest و Service Worker برابر ۲۰۰ و مسیر نامعتبر فارسی برابر ۴۰۴.

## Remaining Pilot gates

- اجرای Migration، Unique/Concurrency constraints و Restart recovery روی PostgreSQL 17 واقعی؛
- اجرای Integration Smoke کامل Handshake → Push → Pull → Checkpoint → Conflict → Resolve؛
- آزمون Crash-after-commit، Pull تکراری و Acknowledge تکراری با API و پایگاه واقعی؛
- آزمون دو User و دو Device، Permission revoke، Device revoke، Clock skew و Client upgrade روی دستگاه کارگاه؛
- آزمون endurance صف هفت‌روزه و صدها فایل روی دستگاه هدف؛
- تکمیل و آزمون Multipart Resume، Malware Scan/Quarantine و Object Storage واقعی؛
- UAT مرورگر برای Recovery، Reapply، Logout/Purge و تغییر حساب روی دستگاه مشترک.
