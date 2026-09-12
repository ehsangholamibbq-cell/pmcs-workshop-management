# ADR 0007 — Optional Project Calendar and Independent Financial State

- Status: Accepted
- Date: 2026-09-09

## Context

پروژه عمرانی ممکن است WBS، تقویم اختصاصی یا Budget Baseline نداشته باشد، ولی ثبت واقعیت مالی و عملیاتی نباید متوقف شود. همچنین ترکیب زودهنگام شاخص‌های مالی و عملیات در یک رنگ واحد، نبود داده را به سلامت مصنوعی تبدیل می‌کند.

## Decision

- Project Calendar یک پیکربندی اختیاری و نسخه‌دار در ماژول Projects است.
- `project-state-v2` در نبود تقویم از `FallbackSevenCalendarDays` و در وجود هفته کاری از `ConfiguredWorkingDays` استفاده می‌کند.
- Snapshotهای `project-state-v1` بازنویسی نمی‌شوند و enum تاریخی `SevenCalendarDays` قابل خواندن می‌ماند.
- Finance Lite ماژول مستقل با schema مالک خود است.
- Receipt، Payment، PettyCashFunding و PettyCashExpense Factهای مالی مستقل‌اند.
- `ExternalNetCash = Receipt - Payment - PettyCashFunding` و `RecognizedSpend = Payment + PettyCashExpense` است؛ تأمین و مصرف تنخواه دوبار هزینه شمرده نمی‌شوند.
- فقط رکورد `Posted` وارد Financial State می‌شود.
- Budget Baseline اختیاری است. نبود آن مقدارهای Budget variance/consumption را `null` نگه می‌دارد.
- Financial State وضعیت مستقل `NotConfigured/NoData/Available` دارد و در Operational Health ادغام نمی‌شود.
- Contract Reference و Cost Center در Finance Lite اختیاری‌اند و WBS هیچ وابستگی اجباری ایجاد نمی‌کند.

## Consequences

- مدیر می‌تواند وضعیت نقد و هزینه قطعی را بدون بودجه اولیه ببیند.
- کیفیت داده تنخواه با مانده منفی به `NeedsAttention` می‌رود، نه اینکه سیستم خودکار Fact بسازد.
- مقایسه بودجه فقط پس از Approved Baseline فعال می‌شود.
- V1 فقط ارز پایه پروژه را تجمیع می‌کند؛ FX و تسعیر به Slice جدا نیاز دارد.
