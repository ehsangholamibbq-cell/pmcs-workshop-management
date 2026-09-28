# PMCS V1.1 — بستهٔ بازبینی Prototype و قلم، UX2-MS44

- شناسه: `PMCS-UX-PROTOTYPE-REVIEW-001`
- نسخهٔ Candidate: `0.1.0`
- وضعیت: نمونهٔ مستقل برای بازبینی؛ `VX-G3/G4/G5` باز؛ انتخاب فونت نهایی باز
- ورودی: مسیر مصوب «مدیریت ممتاز»، نشان شفاف رسمی، Design System `1.0.0-rc.6`، قرارداد فونت `PMCS-UX-TYPOGRAPHY-001`

## باز کردن نمونه

فایل `docs/ux/prototypes/ms44/index.html` را در مرورگر Chromium باز کنید.
همهٔ دارایی‌های قلم محلی‌اند. سناریو، حالت و قلم از سه کنترل بالای صفحه
انتخاب می‌شوند. چاپ همان نمونه با دکمهٔ «چاپ نمونهٔ مقایسه‌ای» یا Print مرورگر
قابل بررسی است. این فایل به BFF، پایگاه داده، Session یا مسیرهای تولیدی متصل
نیست؛ متن و عددها نمونهٔ فرضی با برچسب آشکارند و خروجی آن گزارش رسمی نیست.

| گروه | پوشش انتخابگر |
| --- | --- |
| مسیرها | ورود، نمایه، Portfolio، مرکز فرمان، پیش‌نمایش تکثیر، کارهای من، جدول/فرم، Chat گروه پروژه، Reporting، Intelligence |
| حالت‌ها | عادی، خالی، بارگذاری، خطا، بدون دسترسی، آفلاین، تعارض |
| قلم | مبنای فعلی Tahoma/fallback، وزیرمتن، استعداد |
| اندازه | Desktop ۱۴۴۰، Mobile ۳۹۰ و بررسی نبود overflow سند در ۳۲۰؛ Tablet با تغییر اندازهٔ مرورگر قابل بازبینی است |
| چاپ | A4 از نمونهٔ مرکز فرمان برای هر دو قلم Candidate؛ UI کنترل‌ها در چاپ حذف می‌شود |

Prototype رفتار اجرایی، Permission و خروجی Reporting را شبیه‌سازی نمی‌کند.
دکمه‌های داخل قاب فقط نمایش ظاهری‌اند. حالت‌های فاقد داده واقعیت ساختگی
نمی‌سازند؛ Chat فقط گروه پروژه است، و در نمونهٔ Reporting دسترسی خروجی خاموش
و در Intelligence پیشنهاد از Fact جدا است.

## منشأ و مجوز قلم‌ها

دو فایل WOFF2 بدون تغییر از Repository رسمی و Commit مشخص دریافت شده‌اند.
نسخهٔ کامل OFL 1.1 هر قلم در همان پوشه کنار آن است. SHA-256 در آزمون E2E
کنترل می‌شود. این فایل‌ها فقط متعلق به Prototype هستند؛ manifest تولیدی
`assets/typography/pmcs-fonts.json`، PDF، XLSX و cache آفلاین تغییر نکرده‌اند.

| قلم | Commit upstream | WOFF2 SHA-256 | مجوز |
| --- | --- | --- | --- |
| [Vazirmatn](https://github.com/rastikerdar/vazirmatn/tree/6e553e33489a8f9dfaccc76860a2e3f3c1e66de7) | `6e553e33489a8f9dfaccc76860a2e3f3c1e66de7` | `4e3fa217d38fdafc1fea4414ceb58ca5e662cf0ab5fa735a8c8c20e8b42cad92` | `fonts/Vazirmatn-OFL.txt` |
| [Estedad](https://github.com/aminabedi68/Estedad/tree/0dbe689787b8c2ea302373cb601d0f352f9f98e5) | `0dbe689787b8c2ea302373cb601d0f352f9f98e5` | `18a2278ad5c9c2f60e034270ea2d5856d04c4e984e47c65483aa63b41e3c5a1e` | `fonts/Estedad-OFL.txt` |

قلم مبنا روی ماشین Chromium Linux ممکن است به fallback برسد؛ آن قاب بیانگر
Baseline مرورگر QA است، نه تضمین نصب Tahoma روی دستگاه مقصد. مقایسهٔ PDF
در این مرحله فقط Print مرورگر با WOFF2 است و Embedding/شکل‌گیری PDF رسمی
QuestPDF و نام فونت XLSX باید در Candidate بعدی کنترل شوند.

## شواهد و معیار بازبینی

`src/web/e2e/ms44-prototype.spec.ts` تمام ۱۰ سناریو و ۷ حالت را می‌گردد،
نشان را بارگذاری می‌کند، Hash و load واقعی دو قلم را کنترل می‌کند، در ۳۲۰
پیکسل overflow کل سند را رد می‌کند و PNGهای Desktop/Mobile و PDFهای A4 را
به Artifact `pmcs-ms44-prototype-review` همراه `index.json` دارای Source SHA
و Hash هر فایل می‌فرستد. این شواهد باید پس از CI باز و از نظر clipping،
فاصله، تراکم جدول، ارقام فارسی، تیترها، RTL، حالت‌های خطا/نبود داده و چاپ
بازبینی چشمی شوند. آزمون و تصاویر به‌تنهایی تأیید مالک محصول نیستند.

برای بستن `VX-G3` هنوز contract همهٔ Component stateها، نمونهٔ دقیق‌تر
Login/Shell/Chart و Mobile fallback، نقد بصری مالک و انتخاب قلم باقی است.
برای `VX-G4` مهاجرت همهٔ Routeهای فعال و برای `VX-G5` Visual Diff،
Accessibility، Responsive، PDF/XLSX/Print Golden و Performance لازم‌اند.
