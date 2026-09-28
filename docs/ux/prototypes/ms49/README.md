# UX2-MS49 — Navigation, Feedback and Confirmation States

نمونهٔ مستقل `index.html` برای مرور بخشی از شکاف‌های `VX-G3` است:
ناوبری Responsive، NoData/Loading/Error/NoPermission/Offline/Conflict/
Success و گفت‌وگوی تأیید نسخه. داده و شمارهٔ نسخه فرضی است؛ هیچ درخواست
شبکه یا تغییر عملیاتی انجام نمی‌شود. نشان رسمی و وزیرمتن خودمیزبانِ
قرارداد `2.0.0` استفاده می‌شوند.

در عرض موبایل، فهرست با دکمهٔ واقعی و `aria-expanded` باز و بسته می‌شود؛
عرض ۳۲۰px نباید overflow سند داشته باشد. پیام خطا/تعارض متن و نقش Alert
دارد، Loading نتیجهٔ ساختگی نمی‌سازد، و اقدام در Offline/Permission/Error
قفل می‌شود. Dialog بومی `showModal`، عنوان/توضیح، تمرکز اولیه، چرخهٔ
Tab/Shift+Tab، Escape و بازگشت تمرکز به دکمهٔ آغازگر دارد؛ تا تیک مرور
نسخه، تأیید غیرفعال است.

`src/web/e2e/ms49-system-states.spec.ts` semantics، قفل اقدام، Dialog و
ناوبری keyboard/mobile را می‌سنجد و ۱۱ PNG در Desktop/Tablet/Mobile
با Index دارای Source، Hash Prototype، SHA/حجم و اندازه منتشر می‌کند.
پس از CI، قاب‌ها از نظر تراکم، فارسی، کنتراست و برش مرور می‌شوند.

این Prototype نه پوشش تمام مصرف‌کنندگان فعال است، نه تأیید مالک برای
`VX-G3`؛ مهاجرت Routeها در `VX-G4` و Qualification در `VX-G5` جداست.
