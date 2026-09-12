# ADR 0005 — Deterministic, coverage-aware Project State

- Status: Accepted
- Date: 2026-09-09

## Context

PMCS باید داده عملیاتی را به وضعیت مدیریتی تبدیل کند، اما نبود WBS، Budget Baseline، HSE یا حتی گزارش تأییدشده نباید به عدد صفر یا وضعیت سبز تبدیل شود. پیش‌نویس، داده آفلاین پذیرفته‌نشده و گزارش Submitted نیز هنوز حقیقت رسمی سیستم نیستند.

## Decision

- فقط Daily Report با وضعیت `Approved` ورودی Project State V1 است.
- Calculator خالص و بدون I/O، زمان سیستم، randomness یا AI است.
- هر اجرای رسمی یک Snapshot immutable با `calculationVersion = project-state-v1` می‌سازد.
- Scope این نسخه `ApprovedDailyOperations` و همیشه `isPartial = true` است.
- Coverage، Freshness و Confidence مستقل از Operational Status ذخیره می‌شوند.
- Coverage V1 بر پایه هفت روز تقویمی است و حد کافی ۶۰٪ است؛ این مبنا صریحاً با `SevenCalendarDays` ذخیره می‌شود.
- Freshness: حداکثر ۱ روز `Current`، ۲ تا ۳ روز `Aging` و بیش از ۳ روز `Stale` است.
- نبود داده `NoData` و Coverage/Freshness ناکافی `InsufficientData` می‌سازد؛ این دو هیچ رنگ سلامت مثبت نمی‌گیرند.
- Issue و Stoppage تأییدشده در پنجره ۳۰روزه به `NeedsTriage` تبدیل می‌شوند. نبود Impact برابر `Unassessed` است، نه `Low`.
- قابلیت‌های Contract، Planning/WBS، Budget، Quality و HSE مستقل‌اند و فقط در صورت داشتن Metric رسمی وارد ارزیابی مربوط به خود می‌شوند.
- Recalculation مجوز و Idempotency مستقل دارد و Snapshot، Audit، Outbox و Idempotency در یک transaction ذخیره می‌شوند.

## Consequences

- مدیر می‌تواند منشأ، نسخه قاعده، تاریخ محاسبه و کیفیت داده هر وضعیت را ببیند.
- یک Project State «کامل سازمانی» هنوز ادعا نمی‌شود؛ خروجی فعلی فقط ارزیابی عملیاتی Partial است.
- با اضافه‌شدن Project Calendar، مبنای Coverage باید با calculation version جدید تغییر کند و Snapshotهای قبلی بازنویسی نشوند.
- چرخه Assignment/Resolution برای Attention Item و اجرای خودکار Projection از Outbox در Vertical Slice بعدی تکمیل می‌شوند.
