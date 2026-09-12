# Foundation checkpoint 06.1 — Persian UI and Localization Foundation

- Date: 2026-09-10
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.8

## Outcome

رابط قابل مشاهده V1 فارسی و RTL شد، بدون آنکه نام‌های فنی Domain، API، Database یا enumها ترجمه شوند. پیام‌های خام انگلیسی Backend دیگر مستقیماً به کاربر نمایش داده نمی‌شوند و یک مرز مرکزی Presentation آن‌ها را به پیام امن فارسی تبدیل می‌کند.

## Implemented

- فارسی‌سازی عنوان برنامه، Manifest، ناوبری، Command Center و تمام کنترل‌های عملیاتی موجود؛
- صفحه‌های فارسی برای بارگذاری، نشانی نامعتبر و خطاهای صفحه/پوسته؛
- ترجمه اصطلاحات وضعیت پروژه، گزارش روزانه، مالی، قرارداد، خرید، اقدام و Sync؛
- نمایش WBS، RFI و HSE فقط همراه معادل فارسی؛
- مترجم مرکزی خطای API براساس error code و HTTP status با جلوگیری از نشت detail انگلیسی؛
- تبدیل خطاهای شبکه، IndexedDB، Attachment و Service Worker به پیام فارسی؛
- نمایش مبلغ با رقم فارسی و نام فارسی ارز به‌جای currency code؛
- fallback امن فارسی برای capabilityهای ناشناخته؛
- گیت خودکار `audit:fa` برای جلوگیری از ورود دوباره متن لاتین به رابط؛
- تست واحد برای خطای انگلیسی، شرح فارسی، ممنوعیت دسترسی، خطای شبکه و نمایش مبلغ.

## Verification evidence

- Frontend unit/API contract tests: ۲۹/۲۹ passed؛
- Persian UI audit: passed؛
- ESLint و TypeScript: passed؛
- Next.js production build: passed؛
- Production output scan برای عبارت‌های انگلیسی شناخته‌شده: passed؛
- Next.js standalone smoke: صفحه اصلی، Manifest، Service Worker و صفحه فارسی نشانی نامعتبر passed؛
- `git diff --check`: passed.

## Boundaries

- داده واردشده توسط کاربر، نام پروژه، نام اشخاص، شماره قرارداد/سند و نام فایل ترجمه نمی‌شود.
- انتخاب زبان و رابط انگلیسی قابلیت آینده است؛ این Checkpoint فقط زیرساخت توسعه‌پذیر آن را آماده می‌کند.
- API error codeها برای Logging و پشتیبانی فنی حفظ می‌شوند، اما در صفحه به کاربر نمایش داده نمی‌شوند.

## Next checkpoint

Vertical Slice «Portfolio Command Center V1» مطابق Checkpoint 06، با حفظ رابط فارسی و ابعاد مستقل وضعیت عملیاتی، مالی و تجاری.
