# UX2-MS45 — تمرین ایزولهٔ فونت در PDF و XLSX

این پوشه فقط دارایی QA برای مقایسهٔ وزیرمتن و استعداد است. چهار فایل TTF
Regular/Bold از همان Commitهای رسمی MS44 دریافت و بدون تغییر ذخیره شده‌اند؛
مجوزهای OFL 1.1 در `../ms44/fonts/*-OFL.txt` کنار WOFF2های نمونه‌اند.

| قلم | Commit upstream | Regular SHA-256 | Bold SHA-256 |
| --- | --- | --- | --- |
| [Vazirmatn](https://github.com/rastikerdar/vazirmatn/tree/6e553e33489a8f9dfaccc76860a2e3f3c1e66de7) | `6e553e33489a8f9dfaccc76860a2e3f3c1e66de7` | `b69fd4c680b8f3f225feabcc655a2c585d97627b8f5f5c0f9985e894069f3a56` | `f635fdbea28f265de395ba83b4b1570dcf2f58d13c65469e61903b1c2d2ae723` |
| [Estedad](https://github.com/aminabedi68/Estedad/tree/0dbe689787b8c2ea302373cb601d0f352f9f98e5) | `0dbe689787b8c2ea302373cb601d0f352f9f98e5` | `812996efdeff8fd68bb854fbb3db218886661fbfd50ed38a06d50bd14c1f3c7f` | `7fa317abae24c82aef5a5816bba4be8b86b344bd740c600276bcb738e416dd76` |

از ریشهٔ Repository، `dotnet run --project tools/qa/FontReview/FontReview.csproj --configuration Release -- --output artifacts/qa/ms45-font-review`
یک صفحهٔ A4 PDF و PNG همان صفحه و یک نمونهٔ XLSX راست‌به‌چپ برای هر قلم
می‌سازد. PDF با QuestPDF و قلم‌های Register شدهٔ همان Candidate ساخته می‌شود؛
XLSX در `xl/styles.xml` نام خانواده را ثبت می‌کند. برنامه SHA چهار فایل
را کنترل می‌کند، PDF/PNG یک‌صفحه‌ای و Style XLSX را Assert می‌کند و
`index.json` با Source Commit، SHA و حجم هر خروجی می‌سازد. CI backend این
تمرین را پس از Full Backend Regression اجرا و Artifact مستقل بارگذاری می‌کند.

این نمونه از مدل یا Renderer رسمی گزارش استفاده نمی‌کند، سند رسمی نیست و
هیچ مجوز یا Feature Flag تولیدی را تغییر نمی‌دهد. `PdfLicense` در defaults
همچنان `Unconfigured` است؛ Community فقط داخل فرآیند QA جداگانه تنظیم
می‌شود. نام فونت XLSX در نرم‌افزار گیرنده به نصب محلی قلم وابسته است. پس
از انتخاب بصری مالک، مهاجرت واقعی manifest مرکزی، Goldenهای PDF/XLSX،
Offline، Print و Qualification `VX-G5` جداگانه لازم‌اند.
