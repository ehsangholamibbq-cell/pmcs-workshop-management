# UX2-MS47 — Foundation Component States

نمونهٔ مستقل `index.html` برای مرور Stateهای Action، Field، Status،
Feedback و جدول فارسی در مسیر «مدیریت ممتاز» است. داده‌ها فرضی‌اند،
عملکرد عملیاتی یا درخواست شبکه ندارد، و فونت تولیدی را تغییر نمی‌دهد.
نشان رسمی تأییدشده از Asset موجود بارگذاری می‌شود.

Stateهای Default، Hover، Focus، Pressed، Disabled، Loading، Error،
Success و Offline از Select قابل انتخاب‌اند. دکمهٔ واقعی، Toggle با
`aria-pressed`، ورودی با Label/`aria-describedby`/`aria-invalid`،
پیام با Status/Alert، دلیل متنی Disabled/Offline و جدول با ارقام فارسی
در یک صفحه دیده می‌شوند. Hover/Focus واقعی، قفل دکمه، نبود overflow
کل صفحه در ۳۲۰px و بارگذاری نشان در E2E کنترل می‌شوند.

`src/web/e2e/ms47-component-states.spec.ts` قاب‌های Desktop، Tablet
و Mobile را با `index.json` شامل Source، Hash فایل Prototype،
SHA-256/حجم هر PNG و ابعاد Viewport در Artifact
`pmcs-ms47-component-states` منتشر می‌کند. پس از CI، تصاویر باید
از نظر Focus، برش، تراکم، متن فارسی و تمایز حالت‌ها بازبینی شوند.
این نمونه Gate `VX-G3`، مهاجرت `VX-G4` یا Qualification `VX-G5`
را به‌تنهایی نمی‌بندد.
