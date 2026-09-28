# PMCS V1.1 — VX-G1 Active UI Audit Review

- شناسه: `PMCS-UX-VX-G1-REVIEW-001`
- مرحله: `UX2-MS39`؛ `VX-G1 Audit Complete` پس از Run 401 با هشت Job سبز.
- مرجع اجرا: `src/web/e2e/visual-baseline.json` و `docs/ux/pmcs-v1.1-route-state-baseline.md`
- محدوده: Web فعال V1.1 در Fixture مجزای QA، Chromium، فارسی/RTL و زمان تهران.

## پوشش قابل بازتولید

تمام ۱۱ Route فعال `app/**/page.tsx` در manifest به Capture وصل‌اند. مجموعهٔ
۴۳قابی Run 400 با ۳۰ نام State، Default هر Route، حالت‌های ضروری خطا، منع دسترسی، Flag خاموش،
مجاز محدود، آفلاین، تعارض و Preview، سه اندازهٔ Desktop/Tablet/Mobile و
ممیزی چاپ مرورگر را پوشش می‌دهد. 404 یک State افزوده است. ابزار
`visual-baseline.mjs check-source` کشف واقعی Route و Source هر Capture را
کنترل می‌کند؛ `verify` فایل‌های اضافی/گمشده، ساختار و ابعاد PNG، PDF همراه،
SHA-256، Source Commit و Run را در index ثبت می‌کند. عرض ۳۲۰ پیکسل جداگانه
برای overflow کل سند و دسترسی صفحه‌کلید بررسی می‌شود.

| سطح | Captureهای نماینده | وضعیت ممیزی |
| --- | --- | --- |
| Identity و Admin | 01، 16–18، 42 | ورود و callback خطا، پروفایل، کاربران و ظاهر ورود ثبت‌اند |
| Portfolio و Shell | 02–03، 20، 25–27، 39 | عادی/خالی/بارگذاری/خرابی و انتهای Navigation ثبت‌اند |
| Project و Offline | 04–15، 21، 28–31، 43 | بخش‌های عملیاتی، Responsive، تقویم، آفلاین و چاپ مرورگر ثبت‌اند |
| Collaboration و Reporting | 22–24، 33–38، 40 | Flag خاموش، منع دسترسی، مجاز محدود و تعارض ویرایش پیام ثبت‌اند |
| Bootstrap و 404 | 19، 32، 41 | Wizard، Preview مسدود و مسیر ناموجود ثبت‌اند |

هر ۴۳ PNG و PDF Run 400، Artifact `10992023150` با digest
`sha256:bd9c9d13854908142c0bc78e645f6ee122058d9c332f8d6f38534632e8485591`
با index از نظر Hash، ابعاد و Source/Run تطبیق شدند. بازبینی
نمایندهٔ قاب‌ها شامل فرم‌های Admin، Mobile، Loading، Failure، Offline،
Permission، Reporting و Print بود؛ این کار Visual Diff خودکار یا تأیید همهٔ
صفحه‌ها برای انتشار نیست. Run 400 اصلاح کنتراست کارت حساب Wizard را روی همین
manifest کنترل کرد؛ قاب 19 متن خوانا دارد و قاب 41 مانع اجرای Preview را
همچنان نشان می‌دهد.

## Gapهای ثبت‌شده و مقصد اصلاح

| اولویت | Evidence | مقصد |
| --- | --- | --- |
| P1 بخشی اصلاح‌شده | PDF A4 چاپ مرورگر در ۲۲ صفحه، Navigation بریده و فرم تعاملی داشت | MS43 correction Run 409 برگهٔ محدود یک‌صفحه‌ای A4 بدون فرم/Navigation و متن NoSnapshot صادق را نشان داد؛ Golden و همهٔ خروجی‌ها در `VX-G3/G5` بازند |
| P1 بسته‌شده | متن کارت حساب Wizard در قاب 19 روی زمینهٔ روشن سفید بود | MS39 رنگ متن را اصلاح و E2E نسبت حداقل ۴٫۵ به ۱ را کنترل کرد؛ قاب 19 Run 400 بازبینی شد |
| P2 بخشی اصلاح‌شده | متن Navigation افقی موبایل در قاب‌های 20/23 از لبه بریده دیده می‌شود | MS41 راهنمای پیمایش/کلید تب را در شش Shell و قاب‌های 20–23 Run 404 افزود؛ جایگزین Responsive در `VX-G3/G4` باز است |
| P2 بخشی اصلاح‌شده | قاب 26 بارگذاری Portfolio فضای عمدتاً خالی داشت | MS42 Skeleton خنثی بدون Fact و حذف آن در خطا را به قاب 26/27 می‌افزاید؛ قرارداد مشترک سایر Loadingها در `VX-G3/G4` باز است |
| P2 | کنترل بومی انتخاب فایل قاب 18 عبارت انگلیسی `Choose File` دارد | FileInput فارسی و دسترس‌پذیر در Micro-Step بعدی |
| Gate | فونت فارسی تازه انتخاب/Qualified نشده؛ Componentها و Print هنوز قرارداد کامل ندارند | تأیید بصری مالک در `VX-G3`، مهاجرت `VX-G4` و Qualification `VX-G5` |

`VX-G1` فقط ممیزی و ثبت شواهد و Gapها را می‌بندد؛ وجود Gap باعث نمی‌شود
رابط یا چاپ Qualified اعلام شود. `VX-G2` برای جهت «مدیریت ممتاز» مصوب است.
Source Run 400 و تصاویر اصلاحی بررسی شدند؛ CI مستندات Run 401 نیز هر هشت
Job را پاس کرد و `VX-G1` پذیرفته شد. Capture 44 در MS40 به baseline افزوده
شد. MS40 در Run 403 Safe شد و MS41 Source Run 404 هشت Job سبز و ۴۴ قاب
معتبر دارد؛ راهنمای Navigation موبایل افزوده شده اما جایگزین Responsive هنوز
باز است. `VX-G3/G4/G5` و INT1/QA1 باز می‌مانند.
MS41 documentation Run 405 نیز هشت Job سبز شد و Safe است. MS42 Source Run
406 هشت Job سبز و Artifact `10995736502` با ۴۴ تصویر/PDF و index معتبر
دارد؛ قاب‌های 26/27 بازبینی شدند. CI مستندات شرط پذیرش MS42 است.
MS42 documentation Run 407 سبز و Safe شد. MS43 correction Run 409 با
PDF A4 یک‌صفحه‌ای و Artifact ۴۴تایی معتبر، Gap چاپ مرورگر را در نمونهٔ
مرکز فرمان کاهش داد؛ CI مستندات شرط پذیرش MS43 و Print Golden کلی باز است.
