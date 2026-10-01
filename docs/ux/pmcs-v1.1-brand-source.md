# UX2 — منبع نشان رسمی بتن بسپار قزوین

- منبع مالک محصول: `assets/brand/official-mark.pdf`، SHA-256: `28556628d0828bc6051323a2ecbfc81cd6c17ad2915fa81eef811bbf84f48a26`.
- PDF یک تصویر RGB با اندازهٔ `764×606` و Soft Mask شفاف دارد؛ فایل Vector قابل ویرایش نیست. دو خروجی PNG مستقیماً از همان تصویر و ماسک استخراج شده‌اند، بدون بازترسیم نشان یا تغییر رنگ.
- `src/web/public/brand/bbq-official-lockup.png`: تمام نشان، `764×606`، RGBA، SHA-256: `af5025c9fd1c0de90316a13634fefbe6d751c70b746c7429dd8ae0ca9ca97a6a`.
- `src/web/public/brand/bbq-official-symbol.png`: برش محدودهٔ نماد از مختصات `(0,0)` تا `(764,393)` در همان خروجی RGBA، SHA-256: `530d46f4d8d2c66261014d00fb65fa77c261637eca4326091980447f979e0658`.

در `UX2-MS01` نماد رسمی جای Monogram موقت در Shell و Fallback ورود را می‌گیرد. لوگوی نسخه‌دار منتشرشده در `LoginExperienceDescriptor` همچنان در اولویت است؛ اگر لوگوی تنظیم‌شده وجود نداشته باشد، Fallback رسمی استفاده می‌شود. انتشار، پیش‌نمایش، بازگشت به نسخهٔ قبل و احراز هویت تغییر نمی‌کنند. این برش فقط هویت برند را متصل می‌کند؛ Gateهای Design System، مهاجرت کامل و Visual Qualification همچنان بازند.
