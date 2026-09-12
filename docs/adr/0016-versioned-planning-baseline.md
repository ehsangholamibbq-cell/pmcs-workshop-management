# ADR-0016: Versioned planning baseline and reviewed milestone progress

- Status: Accepted
- Date: 2026-09-11

## Decision

Planning Mode در Project مالک پیکربندی باقی می‌ماند و یکی از حالت‌های `None / SimpleWorkList / Milestones / WbsBaseline / ExternalSchedule` است. تغییر Mode با Revision، Audit، Outbox و Idempotency انجام می‌شود و هیچ Fact یا نسخه تاریخی را بازنویسی نمی‌کند.

Planning Baseline یک Aggregate نسخه‌دار با گردش `Draft → Submitted → Approved/Returned → Superseded` است. تنها نسخه `Approved` سازگار با Mode فعلی می‌تواند مبنای شاخص رسمی باشد. تصویب نسخه جدید، نسخه مصوب قبلی را بدون حذف داده به `Superseded` می‌برد.

هر ردیف پیشرفت غیرخلاصه وزن مصوب دارد و جمع وزن‌ها دقیقاً ۱۰۰٪ است. ردیف Quantity-based به Measurement Item مستقل و دارای Target متصل می‌شود. درصد دستی فقط برای Milestone مجاز است و در Aggregate جداگانه با Evidence، Submit و Approval انسانی نگهداری می‌شود.

## Deterministic calculation

- Actual فقط از مقدار `Approved` یا آخرین Milestone Update مصوب می‌آید.
- نبود Actual در یک ردیف وزن‌دار صفر نیست و کل رسمی را `null` نگه می‌دارد.
- پیشرفت کل رسمی مجموع وزن‌دار درصدهای موجود و کامل است؛ مقدار هر ردیف برای Aggregate در ۱۰۰٪ سقف می‌خورد ولی Overrun مقدار در دفتر قلم حفظ می‌شود.
- Planned برای Milestone بر اساس رسیدن موعد و برای Activity با توزیع خطی V1 محاسبه می‌شود؛ در صورت تنظیم Project Calendar فقط روزهای کاری مصوب در مخرج و صورت وارد می‌شوند.
- Variance فقط از `official - planned` روی Baseline مصوب ساخته می‌شود.
- Forecast تا وجود موتور روند و قواعد کیفیت داده معتبر تولید نمی‌شود.

## Boundaries

- WBS و Baseline همچنان اختیاری‌اند و Mode برابر `None` یک وضعیت معتبر است.
- Baseline فقط شناسه Measurement Item را نگه می‌دارد و مالک Fact یا قلم نیست.
- برنامه بیرونی در V1 یک Import/Reference نسخه‌دار است؛ همگام‌سازی پنهان یا overwrite برنامه داخلی وجود ندارد.
- AI هیچ مسیر ساخت، ارسال، تأیید یا تغییر Planning Baseline و Milestone Update ندارد.

## Consequences

- پروژه ساده می‌تواند بدون Schedule فقط وزن‌دهی رسمی داشته باشد.
- پروژه دارای برنامه می‌تواند Planned/Actual/Variance قابل ممیزی تولید کند.
- تغییر Mode، نبود Target یا کمبود Actual به عدد صفر یا وضعیت سالم تبدیل نمی‌شود.
- مدل برای افزودن Import فایل و Forecast در Sliceهای بعدی آماده است، بدون تغییر معنای Actualهای قبلی.
