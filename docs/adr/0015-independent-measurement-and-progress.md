# ADR-0015: Independent measurement and progress

- Status: Accepted
- Date: 2026-09-11

## Decision

Measurement Item موجودیتی مستقل از WBS است. Project با `PlanningMode.None` می‌تواند Actual Quantity و Fact پیشرفت معتبر داشته باشد. اتصال Fact به Measurement Item اختیاری است و Reference متنی WBS نیز مستقل و اختیاری باقی می‌ماند.

مقدارهای گزارش `Submitted` موقت و مقدارهای `Approved` رسمی‌اند. تجمیع این دو در یک عدد ممنوع است. درصد هر قلم فقط از هدف معلوم و مقدار تأییدشده محاسبه می‌شود. درصد کل فیزیکی به وزن‌دهی رسمی و انحراف/پیش‌بینی به خط مبنای رسمی نیاز دارد؛ در نبود این مبناها API مقدار `null` برمی‌گرداند، نه صفر.

## Boundary

- Planning فقط Contract عمومی FieldOperations را می‌خواند؛ دسترسی به DbContext آن ندارد.
- FieldOperations اعتبار قلم و واحد را از Contract عمومی می‌پرسد؛ به Persistence Planning وابسته نیست.
- تغییر یا غیرفعال‌سازی قلم تاریخچه Fact را بازنویسی نمی‌کند و واحد قلم پس از ایجاد immutable است.
- Commandهای قلم اندازه‌گیری Permission، Revision، Idempotency، Audit و Outbox دارند.

## Consequences

- پروژه بدون WBS ناقص یا بد تلقی نمی‌شود و پیشرفت واقعی خود را از دست نمی‌دهد.
- نبود Fact تأییدشده با پیشرفت صفر اشتباه نمی‌شود.
- Milestone، WBS Baseline، برنامه بیرونی و وزن‌دهی رسمی در ADR-0016 بدون تغییر معنای Actual افزوده شده‌اند.
