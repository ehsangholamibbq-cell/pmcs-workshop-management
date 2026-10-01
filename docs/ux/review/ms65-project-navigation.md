# UX2-MS65 — ناوبری مرکز فرمان پروژه در موج دوم VX-G4

فهرست بازشوندهٔ موبایل مرکز فرمان پروژه جای پیمایش افقی نوار همان Route
را می‌گیرد. پیوندهای مجازِ نقش کاربر در دسکتاپ و موبایل از یک منبع
رندر می‌شوند؛ Menu بلند در عرض ۳۲۰px داخل خود فهرست پیمایش می‌شود.
راهنمای کوتاه در منو وجود بخش‌های پایین‌تر را روشن می‌کند.
Enter/Tab، Escape و بازگشت Focus، انتخاب پیوند درون صفحه و بدون Overflow
بودن سند/Sidebar در سه عرض ۸۲۰/۳۹۰/۳۲۰ در Browser E2E سنجیده می‌شوند.

مرز این برش فقط `/projects/[projectId]` است. تغییر CSS مشترک Disclosure
رفتار Portfolio از MS64 را نگه می‌دارد. چهار Shell باقی‌مانده، Login،
Permission/Business Rule، داده، Chat و Reporting تغییر نمی‌کنند.
شش قاب باز/بسته با Index شامل Source/Hash/ابعاد پس از Full CI باید
دریافت و بصری بررسی شوند. این موج، `VX-G4 Migration Complete` یا
`VX-G5 Visual Qualified` نیست.

Source اولیه `4b224baddcd3a04136b1c2b5bbc39774c3d237fe` در Run 473
هشت Job سبز داشت؛ پس از بازبینی تصویر، اصلاح
`8c20ef881cc50c168d3adee1621a79966ceec7ae` در Run 474 نیز هشت
Job سبز شد. Artifact نهایی `11035335891` با ZIP SHA-256
`76977dc5302b343935eba0fa1f8e9835c4f2bdcbb0f185baa6c8b74527384521`
شش PNG/Index را با Source/Hash/ابعاد معتبر و مرور بصری ثبت کرد. CI
مستقل Checkpoint `PMCS-V1.1-UX2-MS65-C1` شرط Safe شدن است.
