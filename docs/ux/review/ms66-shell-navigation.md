# UX2-MS66 — چهار Shell باقی‌مانده در موج سوم VX-G4

ناوبری موبایل مدیریت هویت، گزارش سبد، گفت‌وگوی گروه پروژه و گزارش پروژه
از نوار افقی بریده به Disclosure مشترک منتقل می‌شود. پیوندهای مجاز هر
Route در دسکتاپ و موبایل یک منبع دارند؛ وضعیت صفحهٔ جاری همان متن/نشان
قبلی را حفظ می‌کند. در عرض‌های ۳۹۰ و ۳۲۰px، باز/بسته شدن با Enter و
Escape/بازگشت Focus، برابری پیوندها، نبود Overflow و شانزده قاب تصویری
با Source/Hash/ابعاد در Browser E2E سنجیده می‌شوند.

این تغییر فقط Navigation را لمس می‌کند. وضعیت Feature Flagهای
Collaboration و Reporting/OutputAccess/Worker همچنان در defaults خاموش
است، Chat فقط گروه پروژه و `PdfLicense=Unconfigured` می‌ماند. مسیرهای
مجوز، Business Rule، داده، و خروجی رسمی تغییر نمی‌کنند. پس از Full CI
باید Artifact دریافت و تصاویر بازبینی شوند؛ این پوشش به‌تنهایی Gate
کامل `VX-G4` یا Qualification `VX-G5` نیست.
