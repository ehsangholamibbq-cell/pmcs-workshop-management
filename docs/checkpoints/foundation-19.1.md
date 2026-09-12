# Foundation checkpoint 19.1 — Persian Calendar Enforcement

- Date: 2026-09-12
- Status: Repository implementation complete; cross-device visual UAT remains a Pilot gate
- Baseline: Checkpoint 19 (`ddab6a1`)

## Scope delivered

- هسته مرکزی و بدون وابستگی بیرونی برای تبدیل دقیق Gregorian/ISO و هجری شمسی؛
- Date Picker شمسی فارسی، راست‌به‌چپ، شنبه‌اول، دارای انتخاب امروز و پشتیبانی از ورود دستی؛
- نمایش ارقام فارسی و پذیرش ارقام فارسی، عربی و لاتین در ورود دستی؛
- اعتبارسنجی روز ماه و اسفند کبیسه بدون پذیرش تاریخ نامعتبر؛
- حفظ `DateOnly`، ISO 8601 و UTC در API/Backend/Database؛
- استفاده صریح از تقویم Persian و منطقه زمانی `Asia/Tehran` در تمام خروجی‌های تاریخ/زمان؛
- جایگزینی ۹ ورودی تاریخ بومی مرورگر در برنامه‌ریزی، مالی، دفتر فنی، کیفیت/HSE و اقدامات مدیریتی؛
- تمرکز همه خروجی‌های تاریخ در یک Formatter مشترک؛
- تبدیل بخش تاریخ شماره‌های رسمی جدید در فنی، تأمین، کیفیت/HSE و کنترل مدیریتی از میلادی به شمسی تهران، بدون بازشماری سوابق قبلی؛
- تبدیل تاریخ ISO موجود در ارجاعات قابل مشاهده تحلیل مشورتی به برچسب شمسی؛
- حذف سه پیاده‌سازی تکراری تاریخ امروز تهران و انتقال آن به هسته مشترک؛
- Gate جدید `audit:calendar` برای رد ورودی بومی میلادی و Formatter پراکنده در UI؛
- ADR 0023 و افزوده‌شدن کنترل تقویم به Pilot/Production checklist.

## Verification completed in this workspace

- ۹۷ تست Web/API contract موفق، شامل ۷ تست اختصاصی تقویم؛
- تطبیق رفت‌وبرگشت ۱۴٬۹۷۶ روز متوالی از ۲۰۰۰ تا ۲۰۴۰ با Intl Persian Calendar؛
- ESLint، TypeScript، ممیزی رابط فارسی و ممیزی تقویم شمسی موفق؛
- Next.js production build موفق؛
- Repository architecture validation موفق.

## Pilot evidence still required

- کنترل بصری Date Picker روی مرورگرها و دستگاه‌های هدف؛
- کنترل صفحه‌کلید، لمس، Zoom و چیدمان responsive؛
- کنترل تاریخ در خروجی‌های PDF/Excel زمانی که این خروجی‌ها به Scope اجرایی افزوده شوند؛
- Build/Test Backend و Integrationهای محیط واقعی طبق Gateهای Checkpoint 19.
