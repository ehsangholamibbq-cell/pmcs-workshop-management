# PMCS V1.1 — بستهٔ بازبینی Prototype و قلم، UX2-MS44

- شناسه: `PMCS-UX-PROTOTYPE-REVIEW-001`
- نسخهٔ Candidate: `0.5.0`
- وضعیت: نمونه‌های مستقل برای بازبینی؛ وزیرمتن MS48 پذیرفته، `VX-G3/G4/G5` باز
- ورودی: مسیر مصوب «مدیریت ممتاز»، نشان شفاف رسمی، Design System `1.0.0-rc.10`، قرارداد فونت `PMCS-UX-TYPOGRAPHY-001 v1.5.0`

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
| اندازه | Desktop ۱۴۴۰، Tablet ۷۶۸، Mobile ۳۹۰ و بررسی نبود overflow سند در ۳۲۰ |
| چاپ | A4 از نمونهٔ مرکز فرمان برای هر دو قلم Candidate؛ UI کنترل‌ها در چاپ حذف می‌شود |

Prototype رفتار اجرایی، Permission و خروجی Reporting را شبیه‌سازی نمی‌کند.
دکمه‌های داخل قاب فقط نمایش ظاهری‌اند. حالت‌های فاقد داده واقعیت ساختگی
نمی‌سازند؛ Chat فقط گروه پروژه است، و در نمونهٔ Reporting دسترسی خروجی خاموش
و در Intelligence پیشنهاد از Fact جدا است.

## منشأ و مجوز قلم‌ها

در MS44، دو فایل WOFF2 بدون تغییر از Repository رسمی و Commit مشخص دریافت شدند.
نسخهٔ کامل OFL 1.1 هر قلم در همان پوشه کنار آن است. SHA-256 در آزمون E2E
کنترل می‌شود. آن هنگام این فایل‌ها فقط متعلق به Prototype بودند؛ manifest
تولیدی `assets/typography/pmcs-fonts.json`، PDF، XLSX و cache آفلاین هنوز
تغییر نکرده بودند. MS48 بعداً همان قلم pinned را متصل کرد.

| قلم | Commit upstream | WOFF2 SHA-256 | مجوز |
| --- | --- | --- | --- |
| [Vazirmatn](https://github.com/rastikerdar/vazirmatn/tree/6e553e33489a8f9dfaccc76860a2e3f3c1e66de7) | `6e553e33489a8f9dfaccc76860a2e3f3c1e66de7` | `4e3fa217d38fdafc1fea4414ceb58ca5e662cf0ab5fa735a8c8c20e8b42cad92` | `fonts/Vazirmatn-OFL.txt` |
| [Estedad](https://github.com/aminabedi68/Estedad/tree/0dbe689787b8c2ea302373cb601d0f352f9f98e5) | `0dbe689787b8c2ea302373cb601d0f352f9f98e5` | `18a2278ad5c9c2f60e034270ea2d5856d04c4e984e47c65483aa63b41e3c5a1e` | `fonts/Estedad-OFL.txt` |

قلم مبنا روی ماشین Chromium Linux ممکن است به fallback برسد؛ آن قاب بیانگر
Baseline مرورگر QA است، نه تضمین نصب Tahoma روی دستگاه مقصد. مقایسهٔ PDF
در MS44 فقط Print مرورگر با WOFF2 بود؛ Embedding PDF رسمی QuestPDF و
نام فونت XLSX در MS48 با Golden واقعی کنترل شدند.

## شواهد و معیار بازبینی

`src/web/e2e/ms44-prototype.spec.ts` تمام ۱۰ سناریو و ۷ حالت را می‌گردد،
نشان را بارگذاری می‌کند، Hash و load واقعی دو قلم را کنترل می‌کند، در ۳۲۰
پیکسل overflow کل سند را رد می‌کند و PNGهای Desktop/Tablet/Mobile و PDFهای A4 را
به Artifact `pmcs-ms44-prototype-review` همراه `index.json` دارای Source SHA
و Hash هر فایل می‌فرستد. این شواهد باید پس از CI باز و از نظر clipping،
فاصله، تراکم جدول، ارقام فارسی، تیترها، RTL، حالت‌های خطا/نبود داده و چاپ
بازبینی چشمی شوند. آزمون و تصاویر به‌تنهایی تأیید مالک محصول نیستند.
نشان کامل روی سطح روشن در Sidebar خوانا می‌ماند و در Mobile، برش رسمی نماد
روی همان سطح به‌کار می‌رود؛ هر دو فایل تأییدشده و بدون دستکاری هستند.

Correction Run 412 (`36485197274`) هشت Job سبز و Artifact
`pmcs-ms44-prototype-review` با ID `10998892196` و digest
`sha256:bd9b537cd43437384f0320ba9cad84b23217b37ec51c5bc779f4031673d52203`
دارد. ۱۱ فایل با Source/Hash/حجم index تطبیق شدند؛ قاب‌های سه اندازه و
حالت خطا بازبینی شدند. هر دو چاپ A4 یک صفحه با متن فارسی قابل استخراج و
بدون کنترل تعاملی‌اند. در شواهد، تفاوت وزن و تراکم دو قلم دیده می‌شود؛
این مشاهده در MS48 با انتخاب وزیرمتن و Golden واقعی PDF/XLSX/Offline
پیگیری شد؛ Accessibility و Performance جامع در `VX-G5` می‌مانند.

برای بستن `VX-G3` هنوز contract همهٔ Component stateها، نمونهٔ دقیق‌تر
Login/Shell/Chart و Mobile fallback و بازبینی بصری مالک روی بستهٔ ملموس باقی است.
برای `VX-G4` مهاجرت همهٔ Routeهای فعال و برای `VX-G5` Visual Diff،
Accessibility، Responsive، PDF/XLSX/Print Golden و Performance لازم‌اند.

MS45 تمرین TTF/PDF/XLSX را در `docs/ux/prototypes/ms45/README.md` جداگانه
ثبت کرد؛ بستهٔ مرور MS44 مبنای انتخاب مستند و قابل بازگشت قلم در UX2 است.

MS47 Prototype اجزای مشترک را در `docs/ux/prototypes/ms47/README.md`
و نمونهٔ تعاملی همان پوشه ثبت کرد. شواهد MS44/45، وزیرمتن را برای
MS48 انتخاب کردند؛ Source Run 425 و مستندات Run 426 هر هشت Job سبز
و Goldenهای رسمی Web/Offline/PDF/XLSX/Print دارند.

MS49 در `docs/ux/prototypes/ms49/` ناوبری جایگزین موبایل، پیام‌های
NoData/Loading/Error/Permission/Offline/Conflict/Success و گفت‌وگوی
تأیید نسخه را با دادهٔ فرضی برای مرور اضافه می‌کند. Index ۱۱ عکس
Desktop/Tablet/Mobile و آزمون Focus/Escape/قفل اقدام در Source Run 429
با هشت Job سبز و Artifact `11004643711` معتبر/بازبینی‌شده‌اند؛
نمونه به‌تنهایی مجوز مهاجرت Routeهای فعال
یا بستن `VX-G3` نیست.

MS50 بستهٔ منبع‌دار `docs/ux/review/ms50/index.html` را برای مرور
ده تصویر دست‌نخوردهٔ UI فعال و Prototypeهای MS44/47/49 افزود.
`images.json` Commit، Artifact ID/digest و Hash/حجم/ابعاد را به هر قاب
وصل می‌کند. سه نمونهٔ تعاملی از همان صفحه باز می‌شوند. نظر مالک روی
این بسته جداگانه ثبت می‌شود؛ حضور تصاویر جای تکمیل Component Contract
و تأیید `VX-G3` نیست.

MS51 در `docs/ux/prototypes/ms51/` Popover/Drawer/Toast، هشت وضعیت
داده و fallback جدول به Cardهای Label/Value موبایل را با نشان/وزیرمتن
نسخهٔ `2.0.0` نمونه می‌کند. Browser E2E قفل Action، نبود Fact ساختگی
در Loading/Permission، Focus/Escape/بازگشت Focus و عرض ۳۲۰px را
می‌سنجد. Run 433 هشت Job سبز و Artifact `11005944021` با ۱۲ PNG،
Source/Hash/ابعاد معتبر دارد؛ Default/Loading/Error/Popover/Drawer
در موبایل و Desktop بازبینی شدند. Drawer در Screenshot Full-page
از viewport بلندتر است و Backdrop فقط viewport فعلی را می‌پوشاند؛
نمای واقعی ۳۹۰×۸۴۴ خواناست. نمونه، مهاجرت مصرف‌کنندهٔ فعال نیست.

MS52 در `docs/ux/prototypes/ms52/` ترکیب Login، Shell و Chart را با
نشان رسمی، وزیرمتن `2.0.0` و دادهٔ صریحاً فرضی افزود. Chart دارای
عنوان/توصیف SVG و جدول همان شش مقدار است؛ در Loading/NoData/Error/
Permission/Offline هر دو با هم پنهان می‌شوند. E2E Navigation موبایل،
Focus/Escape، عرض ۳۲۰px، فونت/نشان و نبود درخواست بیرونی را می‌سنجد.
Run 435 هشت Job سبز و Artifact `11007206344` با ۹ PNG و PDF A4
تک‌صفحه‌ای/متن فارسی قابل استخراج، Source/Hash/حجم معتبر دارد.
قاب‌های Desktop/Mobile، Loading/Permission، Navigation و چاپ مرور
شدند؛ Print صرفاً نمونه است، Golden خروجی رسمی نیست.
