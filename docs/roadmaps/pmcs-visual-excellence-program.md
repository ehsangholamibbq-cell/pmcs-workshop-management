# PMCS Visual Excellence Program

- شناسه سند: `PMCS-RM-VISUAL-001`
- نسخه سند: `1.1.0`
- وضعیت: مسیر بصری مصوب؛ Design System و Qualification در حال برنامه‌ریزی
- تاریخ ثبت: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Parent product baseline: `PMCS V1 / 26bf222d44634562ca7f3fc0931f3f8b79ca04a1`

## ۱. ارزیابی صریح وضعیت فعلی

رابط V1 از نظر عملکرد، RTL، شمسی و مسیرهای اصلی قابل استفاده است، اما از نظر ظرافت، هویت بصری، سلسله‌مراتب، تراکم کنترل‌شده، کیفیت Data Visualization و حس یک محصول سازمانی ممتاز، Baseline نهایی طراحی محسوب نمی‌شود.

Visual Excellence در PMCS یک تغییر رنگ، Theme یا CSS نهایی نیست؛ یک Program سراسری محصول است که تمام صفحات، Stateها، چاپ‌ها و رابط Intelligence را پوشش می‌دهد.

## ۱.۱. مسیر بصری مصوب

مالک محصول مسیر «مدیریت ممتاز» را برای PMCS تصویب کرد. این تصمیم شامل سه معیار صریح است:

1. ترکیب صفحهٔ ورود معماری و حرفه‌ای باشد؛
2. محیط گرم، آرام، انگیزه‌بخش و مناسب استفادهٔ طولانی باشد؛
3. جلوه‌های متحرک کنترل‌شده، هدفمند و غیرمزاحم باشند.

قرارداد بصری لازم‌الاجرا:

- نشان رسمی شرکت بتن بسپار قزوین، نشان محصول PMCS نیز هست؛ نسخهٔ رابط کاربری باید از فایل رسمی Vector/Transparent استخراج شود و بازطراحی مستقل لوگو مجاز نیست؛
- سرمه‌ای برند رنگ پایه و سبز و نقره‌ای فقط Accentهای مهم هستند؛ استفادهٔ فراگیر از رنگ‌های لوگو یا اشباع زیاد ممنوع است؛
- Surfaceهای اصلی از طیف گرم Ivory/Sand/Off-white استفاده می‌کنند و رنگ‌های وضعیت دارای Semantic مستقل از Branding هستند؛
- صفحهٔ ورود از زبان معماری، سازه، بتن و Wireframe الهام می‌گیرد، اما کپی مستقیم تصویر مرجع نیست؛
- ترسیم Wireframe و درخشش کوتاه مجاز است، ولی نباید ورود را Block کند، دائماً تکرار شود یا در حالت `prefers-reduced-motion` اجرا شود؛
- همین زبان باید در Dashboard، Project Collaboration، Reporting و Executive Intelligence پیوسته بماند.

طرح گرافیکی Login نباید در Authentication flow، BFF یا صفحهٔ React به‌صورت Hard-code قفل شود. پیاده‌سازی باید یک `LoginExperienceDescriptor` نسخه‌دار با Token، Asset slot، Composition variant، Motion policy، Active version و Rollback داشته باشد. بارگذاری HTML/CSS/JavaScript دلخواه از پنل مدیریت ممنوع است؛ فقط Asset و گزینه‌های Schema-validated و امن قابل تغییر هستند.

## ۲. اهداف طراحی

- حرفه‌ای، آرام، دقیق و مدیریتی؛
- متناسب با پروژه‌های عمرانی و تصمیم‌گیری سازمانی، بدون ظاهر کلیشه‌ای کارگاهی؛
- Data-dense ولی خوانا؛
- سلسله‌مراتب روشن میان Fact، Status، Risk، Alert و Action؛
- فارسی و RTL اصیل، نه رابط LTR آینه‌شده؛
- Responsive واقعی در Desktop، Tablet و Mobile؛
- سازگار با کار طولانی و کاهش خستگی بصری؛
- اعتمادساز برای اعداد، اسناد، Workflow و AI؛
- قابل چاپ و قابل ارائه به مدیر، کارفرما و مشاور.

## ۳. پوشش اجباری

Program باید همهٔ این سطوح را پوشش دهد:

- Login و Identity؛
- پروفایل شخصی حداقلی عضو، Avatar و حالت بدون تصویر؛
- Portfolio و Project Registry؛
- Application Shell، Navigation و Project Context؛
- Command Center و Dashboardها؛
- My Work، Notification و Approval؛
- تمام فرم‌ها، جدول‌ها، فیلترها و Modalها؛
- Offline/Sync/Conflict/Error experiences؛
- Finance، Commercial، Planning، Technical Office، Quality/HSE و Governance؛
- Project Collaboration؛
- Wizard ساخت پروژه از روی پروژهٔ موجود و پیش‌نمایش اقلام قابل انتقال؛
- Reporting Center و Print output؛
- Executive Intelligence Center و Agent states؛
- Empty، Loading، Skeleton، No Data، Not Configured، No Permission، Stale و Failure.

بازطراحی فقط صفحات جدید یا Dashboard پذیرفته نیست؛ باقی‌ماندن صفحات قدیمی با زبان بصری متفاوت، Gate را رد می‌کند.

## ۴. Workstreamها

### `VX-01` — Visual and UX Audit

- inventory تمام صفحه‌ها و componentها؛
- ثبت inconsistency، hierarchy problem، density، overflow و responsive issue؛
- تحلیل مسیرهای پرتکرار هر Role؛
- screenshot baseline از تمام stateهای بحرانی؛
- اولویت‌بندی بر اساس impact، frequency و risk.

### `VX-02` — Art Direction

- تعریف ۲ تا ۳ مسیر بصری حرفه‌ای؛
- moodboard، رنگ، typography، iconography، chart style و surface language؛
- نمایش همان سناریو در Dashboard، Form، Table، Chat، Report و Agent؛
- انتخاب یک Direction رسمی پیش از پیاده‌سازی.

**نتیجه:** مسیر «مدیریت ممتاز» با قرارداد Branding، Warm Surface و Motion بالا تصویب شد. این تصویب به‌معنی تکمیل Design System یا مهاجرت UI نیست.

### `VX-03` — Design System Foundation

- semantic color tokens و status palette؛
- spacing، grid، radius، elevation و border؛
- typography فارسی و number treatment؛
- icon system؛
- motion، reduced-motion و feedback؛
- قرارداد نسخه‌دار `LoginExperienceDescriptor` و Brand asset؛
- Avatar، profile-photo crop/fallback و privacy states؛
- component state contract شامل hover/focus/pressed/disabled/error/success/offline؛
- light/dark decision فقط پس از Use Case؛ Theme دوم خودکار Scope نیست.

### `VX-04` — High-Fidelity Prototype and Review Pack

- Login و Shell؛
- پروفایل شخصی و ویرایش تصویر؛
- Portfolio و Project Command Center؛
- Project Duplication Wizard شامل Preview، Selection، Conflict و Confirmation؛
- Data table و complex form؛
- My Work/Notification؛
- Project Chat؛
- Reporting Center و Print Preview؛
- Executive Intelligence Center؛
- Desktop/Tablet/Mobile؛
- Empty/Loading/Error/Permission/Offline states؛
- Prototype قابل کلیک و تصویرهای مقایسه‌ای برای تأیید بدون نیاز به زیرساخت اجرایی.

### `VX-05` — Shared UI Implementation

- token package و component library؛
- Login composition registry و asset slots بدون کد دلخواه؛
- Shell/navigation/context؛
- form/table/filter/modal/workflow primitives؛
- chart/data visualization primitives؛
- attachment/timeline/comment/evidence primitives؛
- print primitives؛
- documentation و usage contract.

### `VX-06` — Full Product Migration

مهاجرت موجی و کامل:

1. Identity، Shell، Portfolio و Project Command Center؛
2. My Work، Notification و shared workflows؛
3. تمام ماژول‌های V1؛
4. Collaboration و Reporting؛
5. Executive Intelligence Center.

تغییر ظاهر حق تغییر Business Rule، Permission یا State Truth را ندارد.

### `VX-07` — Visual, Accessibility and Performance Qualification

- screenshot/visual regression؛
- responsive matrix؛
- RTL و Persian typography audit؛
- keyboard، focus order و screen-reader semantics در مسیرهای اصلی؛
- contrast حداقل WCAG AA برای متن و کنترل‌ها؛
- reduced-motion؛
- chart legend/color accessibility؛
- overflow و density stress tests؛
- Print/PDF visual golden tests؛
- performance budget و جلوگیری از animation/asset سنگین؛
- آزمون Rollback نسخهٔ طراحی Login و خرابی/نبود Asset؛
- cross-browser E2E.

## ۵. Quality Bar قابل پذیرش

- تمام صفحه‌های فعال از token و component رسمی استفاده کنند؛
- inline/ad-hoc style جدید بدون Design System decision ممنوع باشد؛
- تمام componentهای تعاملی state کامل و focus واضح داشته باشند؛
- Fact، AI Insight، Warning، Error و Draft از نظر بصری اشتباه‌پذیر نباشند؛
- اعداد، واحد، ارز، تاریخ شمسی و status در جدول/کارت/چاپ سازگار باشند؛
- جدول پرتراکم در Desktop و Tablet قابل استفاده و در Mobile دارای fallback روشن باشد؛
- هیچ صفحه‌ای فقط در Happy Path زیبا نباشد؛ stateهای خالی، خطا، عدم دسترسی و آفلاین نیز طراحی شوند؛
- خروجی چاپی ادامهٔ Design System باشد، نه screenshot صفحه؛
- هر تغییر مهم UI visual diff بازبینی‌شده داشته باشد؛
- تفاوت طراحی میان ماژول قدیمی و جدید در پایان V1.1 باقی نماند.

## ۶. مواردی که عمداً رد می‌شوند

- تزئین زیاد، gradient/glass/effect بدون کارکرد؛
- Animation نمایشی که سرعت کار را کم کند؛
- Dashboard پر از KPI بی‌معنا؛
- رنگ‌بندی وضعیت فقط بر اساس سلیقه و بدون semantics؛
- طراحی جداگانه و ناسازگار برای هر ماژول؛
- کپی ظاهری Telegram برای Collaboration؛
- Chat ساده به‌عنوان رابط اصلی Agent؛
- استفاده از PDF چاپ مرورگر به‌جای Print System حرفه‌ای؛
- تأیید UI فقط بر اساس یک تصویر Happy Path.

## ۷. Gateها

| Gate | شرط | وضعیت |
| --- | --- | --- |
| `VX-G1 Audit Complete` | inventory و screenshot baseline کامل | باز تا ثبت Evidence کامل |
| `VX-G2 Direction Approved` | یک Art Direction روی سناریوهای نماینده تصویب شده | **مصوب: مدیریت ممتاز** |
| `VX-G3 System Ready` | Token/component contract و prototype کامل | باز |
| `VX-G4 Migration Complete` | تمام صفحات فعال به سیستم جدید منتقل شده‌اند | باز |
| `VX-G5 Visual Qualified` | visual/accessibility/responsive/print/performance suites پاس شده‌اند | باز |

هیچ UI تولیدی جدید پیش از `VX-G2` آغاز نمی‌شود و V1.1 پیش از `VX-G5` Qualified اعلام نمی‌شود.

## ۸. تاریخچه نسخه سند

| نسخه | تغییر |
| --- | --- |
| `1.0.0` | ایجاد Visual Excellence Program سراسری |
| `1.1.0` | تصویب مسیر «مدیریت ممتاز»، قرارداد برند و Motion، Login قابل پیکربندی، پروفایل شخصی و Prototype تکثیر پروژه |
