# PMCS Visual Excellence Program

- شناسه سند: `PMCS-RM-VISUAL-001`
- نسخه سند: `1.42.0`
- وضعیت: مسیر بصری «مدیریت ممتاز» مصوب؛ `VX-G3` در Contract/Prototype پذیرفته؛ مهاجرت مرحله‌ای مصرف‌کنندگان فعال تا MS79 در `VX-G4`، مهاجرت کامل/Qualification باز
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
| `VX-G1 Audit Complete` | inventory و screenshot baseline کامل | پذیرفته‌شده با Run 401: ۱۱ Route، ۳۰ State، ۴۳ تصویر و PDF با Gap ledger در `docs/ux/pmcs-v1.1-vx-g1-audit-review.md`؛ MS40 سپس Capture 44 را افزود |
| `VX-G2 Direction Approved` | یک Art Direction روی سناریوهای نماینده تصویب شده | **مصوب: مدیریت ممتاز** |
| `VX-G3 System Ready` | Token/component contract و prototype تمام stateهای قراردادی کامل | پذیرفته در محدودهٔ Contract/Prototype با MS63 Run 469؛ UI فعال در G4/G5 مستقل است |
| `VX-G4 Migration Complete` | تمام صفحات فعال به سیستم جدید منتقل شده‌اند | در جریان؛ Navigation شش Shell در MS64–66، Feedback/Form Loading پروفایل/هویت در MS67–68، پیش‌نمایش مجوز MS69، ظاهر ورود MS70، حقیقت داده/فرمان Wizard در MS71–72، بازخورد گزارش در MS73، حقیقت کارتابل/اعلان‌ها در MS74، خواندن Chat گروه پروژه در MS75 و تجمیع سبد در MS76 و حقیقت دسترسی مرکز فرمان پروژه در MS77 و فهرست پروژه در MS78؛ سایر مصرف‌کنندگان باز |
| `VX-G5 Visual Qualified` | visual/accessibility/responsive/print/performance suites پاس شده‌اند | باز |

هیچ UI تولیدی جدید پیش از `VX-G2` آغاز نمی‌شود و V1.1 پیش از `VX-G5` Qualified اعلام نمی‌شود.

## ۸. تاریخچه نسخه سند

| نسخه | تغییر |
| --- | --- |
| `1.0.0` | ایجاد Visual Excellence Program سراسری |
| `1.1.0` | تصویب مسیر «مدیریت ممتاز»، قرارداد برند و Motion، Login قابل پیکربندی، پروفایل شخصی و Prototype تکثیر پروژه |
| `1.2.0` | ثبت تأیید مالک محصول برای ترکیب Login، گرمی محیط و Motion؛ تثبیت الزام Preview/Publish/Rollback/Fallback برای Login و باز نگه‌داشتن VX-G3 |
| `1.3.0` | ممیزی قابل بازتولید ۱۱ Route و ۴۳ تصویر/یک PDF، اصلاح کنتراست Wizard و ثبت Gapها برای VX-G3/G4/G5؛ اعتبار VX-G1 به CI مستندات MS39 وابسته است |
| `1.4.0` | Run 401 Gate ممیزی VX-G1 را با هشت Job سبز پذیرفت؛ MS40 FileInput فارسی و Capture 44 را افزود، بدون بستن VX-G3/G4/G5 |
| `1.5.0` | Run 403 مستندات MS40 را با هشت Job سبز پذیرفت؛ MS41 راهنمای Navigation موبایل را در شش Shell فعال و آزمون keyboard/عرض ۳۲۰ ثبت کرد؛ Source Run 404 هشت Job سبز و Artifact بازبینی‌شده، CI مستندات شرط پذیرش MS41، Gateهای G3/G4/G5 باز |
| `1.6.0` | Run 405 مستندات MS41 را با هشت Job سبز پذیرفت؛ MS42 Loading سبد را با Skeleton خنثی و بدون Fact ساختگی، E2E خطا و قاب 26 اصلاح کرد؛ Source Run 406 هشت Job سبز و Artifact بازبینی‌شده، CI مستندات شرط اعتبار، G3/G4/G5 باز |
| `1.7.0` | Run 407 مستندات MS42 را با هشت Job سبز پذیرفت؛ MS43 قرارداد Print System و برگهٔ چاپ محدود مرکز فرمان را افزود؛ Run 408 متن بدون Snapshot را آشکار و correction Run 409 هشت Job سبز/PDF یک‌صفحه‌ای بازبینی‌شده، CI مستندات شرط اعتبار، G3/G4/G5 باز |
| `1.8.0` | Run 410 مستندات MS43 را با هشت Job سبز پذیرفت؛ MS44 نمونهٔ مستقل ۱۰ سناریو × ۷ حالت و مقایسهٔ دو قلم Web/Print را افزود. Run 411 نشان کم‌خوانا را آشکار کرد؛ correction Run 412 هشت Job سبز و Artifact ۱۱فایلی/PDF A4 بازبینی‌شده، CI مستندات شرط پذیرش، G3/G4/G5 باز |
| `1.9.0` | Run 413 مستندات MS44 را با هشت Job سبز پذیرفت؛ MS45 چهار TTF دارای Hash/OFL را در تمرین ایزولهٔ QuestPDF/XLSX ثبت کرد. Run 414 هشت Job سبز و PDF/PNG/XLSX مقایسه‌ای بازبینی‌شده؛ CI مستندات شرط اعتبار، Component states و G3/G4/G5 باز |
| `1.10.0` | Run 415 مستندات MS45 را با هشت Job سبز پذیرفت؛ MS46 ماتریس Stateهای مشترک و اصلاح محدود Hover دکمهٔ Disabled را همراه قاب 45 ثبت کرد. Source Run 416 هشت Job سبز و Artifact ۴۵قابی بازبینی شد؛ CI مستندات شرط اعتبار، Prototype Stateهای بعدی و G3/G4/G5 باز |
| `1.11.0` | Run 417 مستندات MS46 را با هشت Job سبز پذیرفت؛ MS47 نمونهٔ تعاملی ۹ State بنیادین و شواهد سه اندازه/۳۲۰px را افزود. Source Run 419 هشت Job سبز/Artifact ۱۱قابی معتبر؛ CI مستندات شرط اعتبار، MS48 font swap مستند، G3/G4/G5 باز |
| `1.12.0` | Run 420 مستندات MS47 را با هشت Job سبز پذیرفت؛ MS48 وزیرمتن را از manifest نسخهٔ 2.0.0 در Web/Offline/PDF/XLSX/Print با Golden رسمی و حفظ F05 دوصفحه‌ای متصل کرد. Source Run 425 هشت Job سبز، CI مستندات شرط پذیرش؛ State Contract، مهاجرت کامل و G3/G4/G5 باز |
| `1.13.0` | Run 426 مستندات MS48 را با هشت Job سبز پذیرفت؛ MS49 Prototype Navigation موبایل، پیام‌های هفت وضعیت و Dialog نسخه را با Browser E2E و ۱۱ قاب سه اندازه Candidate کرد. Source Run 429 هشت Job سبز، CI مستندات شرط پذیرش؛ مهاجرت مصرف‌کنندگان فعال و G3/G4/G5 باز |
| `1.14.0` | Run 430 مستندات MS49 را با هشت Job سبز پذیرفت؛ MS50 ده تصویر دست‌نخورده از UI فعال و Prototypeها را با شناسهٔ Artifact/Commit/Hash در بستهٔ مرور VX-G3 کنار هم قرار داد؛ Source Run 431 هشت Job سبز و Artifact `11005871839` با سه قاب معتبر/بازبینی‌شده، CI مستندات شرط Checkpoint، نظر مالک و شکاف‌های Component همچنان شرط G3، مهاجرت و G5 جدا |
| `1.15.0` | Run 432 مستندات MS50 را با هشت Job سبز پذیرفت؛ MS51 Popover/Drawer/Toast و ردیف موبایل را در هشت وضعیت با Focus/Keyboard و Artifact ۱۲قابی نمونه کرد؛ Source Run 433 هشت Job سبز/Artifact `11005944021` معتبر، CI مستندات شرط Checkpoint، Login/Shell/Chart و نظر مالک برای G3، مهاجرت و G5 جدا |
| `1.16.0` | Run 434 مستندات MS51 را با هشت Job سبز پذیرفت؛ MS52 Login/Shell/Chart را با جدول جایگزین، حالت‌های بدون داده، Navigation موبایل و Print A4 نمونه کرد؛ Source Run 435 هشت Job سبز/Artifact `11007206344` با ۹ PNG/PDF یک‌صفحه‌ای معتبر، CI مستندات شرط Checkpoint، بستهٔ مرور مالک G3 و G4/G5 باز |
| `1.17.0` | Run 436 مستندات MS52 را با هشت Job سبز پذیرفت؛ MS53 شواهد فعال و Prototype را در بستهٔ مرور مالک با Manifest منشأ و چهار معیار تصمیم گرد آورد؛ Source Run 437 هشت Job سبز و Artifact `11017486934` با سه قاب معتبر/بازبینی‌شده، CI مستندات شرط Checkpoint؛ تصمیم مالک و شکاف‌های Component شرط بستن G3، مهاجرت G4 و Qualification G5 جدا |
| `1.18.0` | Run 438 مستندات MS53 را با هشت Job سبز پذیرفت؛ مالک جهت بصری را با دو پیگیری مشخص قبول کرد، نه پذیرش Gate G3؛ MS54 نمونهٔ موبایل/متن فارسی را مستقل اصلاح کرد، Source Run 439 هشت Job سبز، Artifact `11018211937` با دو قاب معتبر؛ CI مستندات شرط Checkpoint، Contract/State G3 و G4/G5 باز |
| `1.19.0` | Run 440 مستندات MS54 را با هشت Job سبز پذیرفت؛ MS55 نمونهٔ تعاملی تصویر/برش/حریم خصوصی و فهرست صریح شکاف‌های G3 را Candidate کرد؛ Run 443 هشت Job سبز/Artifact `11019328744` با ۱۳ قاب معتبر، CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.20.0` | Run 444 مستندات MS55 را با هشت Job سبز پذیرفت؛ MS56 نمونهٔ انتخاب/تعارض/Preview/تأیید Wizard را مستقل آماده کرد؛ Run 445 هشت Job سبز/Artifact `11020397707` با ۱۴ قاب معتبر، CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.21.0` | Run 446 مستندات MS56 را با هشت Job سبز پذیرفت؛ MS57 نمونهٔ پیوست/مدرک را با وضعیت‌های فایل و مرور منشأ آماده کرد؛ Run 447 هشت Job سبز ولی index ابعاد تصویر نادرست داشت؛ correction Run 448 هشت Job سبز/Artifact `11022376179` با ۱۵ قاب معتبر؛ CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.22.0` | Run 449 مستندات MS57 را با هشت Job سبز پذیرفت؛ MS58 نمونهٔ وضعیت‌های خروجی و چاپ مرورگر A4/A3 عمودی/افقی را از خروجی رسمی جدا کرد؛ Run 450 هشت Job سبز اما متن فنی انگلیسی داشت؛ correction Run 451 هشت Job سبز/Artifact `11023680355` با ۱۳ PNG/۵ PDF معتبر؛ CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.23.0` | Run 452 مستندات MS58 را با هشت Job سبز پذیرفت؛ MS59 نمونهٔ مفهومی هوشمندی با ۹ وضعیت و مرز فرضیه/واقعیت/اقدام انسانی را Candidate کرد؛ Run 453/Artifact و CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.24.0` | Run 454 مستندات MS59 را با هشت Job سبز پذیرفت؛ MS60 فرم شمسی/فیلتر را در ده وضعیت با پیام مرتبط و نتیجهٔ صادق Candidate کرد؛ Run 455 هشت Job سبز ولی قاب ۳۲۰px متن فنی انگلیسی داشت؛ correction Run 456 هشت Job سبز/Artifact `11025098337` با ۱۶ قاب معتبر و بازبینی‌شده و CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.25.0` | Run 457 مستندات MS60 را با هشت Job سبز پذیرفت؛ MS61 کنتراست Token، Focus و Reduced Motion را سنجید و بستهٔ چهار معیار مالک را به‌روز کرد؛ Runهای 458/459 در آزمون نمایش معادل سفید شکست خوردند؛ اصلاح متن فارسی و آزمون در Run 460 هشت Job سبز ولی پیام بالای بسته مخفف انگلیسی داشت؛ اصلاح Run 461 هشت Job سبز/Artifact `11027117283` با سه قاب معتبر و بازبینی‌شده؛ CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.26.0` | UX2-MS61 documentation Run 462 هشت Job سبز و Safe؛ MS62 شاهد ۴۸ردیفی چاپ/تراکم را با صفحه‌بندی نمایش و چهار چاپ چندصفحه‌ای آماده کرد؛ Run 463 مشکل تاریخ ۷۶۸px را یافت، Runهای 464/465 سبز ولی مرور بصری اصلاح عنوان ستون و caption موبایل را لازم کرد؛ Source نهایی Run 466 هشت Job سبز/Artifact `11029742768` با هشت PNG/چهار PDF و ۲۴ صفحه معتبر/بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint؛ سه معیار نخست در سطح طراحی پذیرفته، معیار چهارم تا نمایش و پاسخ مالک باز؛ G3/G4/G5 باز |
| `1.27.0` | UX2-MS62 بعد از Source Run 466/Artifact ۸ PNG و ۴ PDF، در Run 467 آزمون Focus Login نشست واردشده را به ارث برد و شکست خورد؛ اصلاح محدود Run 468 هشت Job سبز، MS62 Safe. مالک پس از ارائهٔ بستهٔ چاپ پُرداده و موبایل معیار چهارم را نیز در سطح نمونه پذیرفت. MS63 فهرست خانواده‌های قرارداد و نمونه را برای VX-G3 ممیزی می‌کند؛ CI مستقل این تصمیم شرط پذیرش است، G4/G5 باز |
| `1.28.0` | MS63 Run 469 هشت Job سبز و G3 در محدودهٔ Contract/Prototype پذیرفته؛ MS64 نخستین Migration فعال G4 در Portfolio با Navigation بازشوندهٔ موبایل، Run 470 هشت Job سبز/Artifact `11032562526` شش قاب/Index معتبر و مرورشده. CI مستقل مستندات شرط Checkpoint؛ سایر Shellها، G4 کامل و G5 باز |
| `1.29.0` | MS64 docs correction Run 472 هشت Job سبز و Safe؛ MS65 Navigation مرکز فرمان پروژه را در Run 473 متصل و پس از مرور، راهنمای پیمایش فهرست بلند را در Run 474 با هشت Job سبز افزود؛ Artifact `11035335891` شش قاب/Index معتبر و مرورشده، CI مستندات شرط Checkpoint. چهار Shell دیگر، G4 کامل و G5 باز |
| `1.30.0` | MS65 docs Run 475 هشت Job سبز و Safe؛ MS66 چهار Shell دیگر را به Navigation موبایل مشترک منتقل کرد؛ Source Run 476 هشت Job سبز و Artifact `11036398624` با ۱۶ قاب/Index معتبر و مرورشده. CI مستقل مستندات شرط Checkpoint؛ زیرکار Navigation شش Shell تکمیل، G4 کامل و G5 باز |
| `1.31.0` | MS66 docs Run 477 هشت Job سبز و Safe؛ MS67 Feedback پروفایل را با Status/Error/Success و Form Loading فعال متصل کرد؛ Runs 478–480 هر هشت Job سبز، Artifact نهایی `11038772992` پنج قاب/Index معتبر و مرورشده؛ CI مستقل مستندات شرط Checkpoint، سایر مصرف‌کنندگان G4 و G5 باز |
| `1.32.0` | MS67 docs Run 481 پس از timeout بیرونی در بازاجرا هشت Job سبز و Safe؛ MS68 Feedback/Loading مدیریت هویت و حفظ فرم/هویت Retry دعوت را متصل کرد؛ Source Run 484 هشت Job سبز و Artifact `11040584680` با شش قاب/Index معتبر و مرورشده. CI مستقل مستندات شرط Checkpoint؛ پیش‌نمایش مجوز و سایر مصرف‌کنندگان G4 و Qualification G5 باز |
| `1.33.0` | MS68 docs Run 485 هشت Job سبز و Safe؛ MS69 پیش‌نمایش مجوز را هنگام تغییر نقش/پروژه از نتیجهٔ کهنه پاک و Selectهای آن را هنگام محاسبه قفل کرد؛ Source Run 489 هشت Job سبز/Artifact `11044303294` چهار قاب معتبر و بازبینی‌شده، CI مستقل مستندات شرط Checkpoint؛ سایر مصرف‌کنندگان G4 و G5 باز |
| `1.34.0` | MS69 docs Run 490 هشت Job سبز و Safe؛ MS70 تاریخچه/فرم مدیریت ظاهر ورود را با بارگذاری، خطا، Retry و قفل ذخیرهٔ قابل تشخیص متصل کرد؛ Source Run 491 هشت Job سبز/Artifact `11046207203` چهار قاب معتبر و بازبینی‌شده، CI مستقل مستندات شرط Checkpoint؛ سایر مصرف‌کنندگان G4 و G5 باز |
| `1.35.0` | MS70 docs Run 492 هشت Job سبز و Safe؛ MS71 دریافت پروژه/اعضای Wizard را از حالت خالی واقعی جدا و Preview را تا دریافت دادهٔ انتخاب‌شده قفل کرد؛ Run 493 متن Loading نادرست پس از خطا را یافت، correction Run 494 هشت Job سبز/Artifact `11049002783` چهار قاب معتبر و بازبینی‌شده؛ فارسی‌سازی کد نقش/وضعیت عضو و CI مستقل مستندات شرط Checkpoint، سایر مصرف‌کنندگان G4 و G5 باز |
| `1.36.0` | MS71 docs Run 495 هشت Job سبز و Safe؛ MS72 قفل فرم/مراحل Wizard هنگام Preview و برچسب فارسی مشترک نقش/وضعیت عضو را متصل کرد؛ Run 496 Assertion نادرست Fieldset را یافت، correction Run 497 هشت Job سبز/Artifact `11050303281` با سه قاب و Index معتبر و بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint؛ سایر مصرف‌کنندگان G4 و G5 باز |
| `1.37.0` | MS72 docs Run 498 هشت Job سبز و Safe؛ MS73 بازخورد درخواست/دانلود و حقیقت Refresh مرکز گزارش پروژه/سبد را متصل کرد؛ Source Run 499 هشت Job سبز، Artifact `11052607582` با چهار PNG/Index معتبر و بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint، ممیزی سایر مصرف‌کنندگان G4 و سپس G5 باز |
| `1.38.0` | MS73 docs Run 500 هشت Job سبز و Safe؛ MS74 حقیقت دریافت/فرمان کارتابل و اعلان‌ها را با تفکیک Loading/Current/Cached/Unavailable/Forbidden متصل کرد؛ Run 501 سبز ولی تصویر موبایل هم‌پوشانی داشت، اصلاح نهایی Run 502 هشت Job سبز/Artifact `11054696874` با سه PNG/Index معتبر و بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint، ممیزی سایر مصرف‌کنندگان G4 و سپس G5 باز |
| `1.39.0` | MS74 docs Run 503 و MS75 docs Run 507 هرکدام هشت Job سبز و Safe؛ خواندن و جست‌وجوی Chat گروه پروژه در MS75 متصل شد. MS76 تجمیع قدیمی سبد را در Refresh/Error پنهان کرد؛ Run 509 و ثبت اولیهٔ Run 510 سبز بودند، اما بازبینی رنگ خطا اصلاح Source را لازم کرد. Source نهایی Run 511 هشت Job سبز/Artifact `11058654451` چهار PNG/Index معتبر و بازبینی‌شده دارد؛ CI مستقل این اصلاح مستندات شرط C2 و سپس سایر مصرف‌کنندگان G4 و G5 بازند |
| `1.40.0` | MS76 C3 docs Run 513 هشت Job سبز و Safe؛ MS77 مرکز فرمان پروژه را در Refresh و قطع/بازگشت دسترسی از داده و فرمان کهنه پاک کرد. Run 516 هشت Job سبز/Artifact `11062655536` سه PNG/Index معتبر و بازبینی‌شده دارد؛ CI مستقل مستندات شرط Checkpoint، ممیزی سایر مصرف‌کنندگان G4 و سپس G5 بازند |
| `1.41.0` | MS77 docs Run 517 هشت Job سبز و Safe؛ MS78 فهرست پروژه را در Refresh/Error از کارت و فرمان کهنه پاک کرد. Run 518 سبز ولی بازبینی تصویر رنگ خنثای خطا را یافت؛ Source نهایی Run 519 هشت Job سبز/Artifact `11062936761` چهار PNG/Index معتبر و مرورشده دارد. دفتر ممیزی مصرف‌کنندگان فعال افزوده شد؛ CI مستقل مستندات شرط Checkpoint، باقی G4 و سپس G5 بازند |
| `1.42.0` | MS78 docs Run 520 هشت Job سبز و Safe؛ MS79 دفتر فنی را در دریافت تازه، نسخه ذخیره‌شده، قطع و بازگشت دسترسی از داده و فرمان کهنه پاک و پیام هشدار/خطر را با Token معنایی متمایز کرد. Source نهایی Run 527 هشت Job سبز/Artifact `11065892550` با چهار قاب و Index معتبر و بازبینی‌شده دارد؛ CI مستقل مستندات شرط Checkpoint، باقی G4 و سپس G5 بازند |
