# PMCS V1.1 — ماتریس Stateهای Component مشترک

- شناسه: `PMCS-UX-COMPONENT-STATES-001`
- نسخهٔ Candidate: `0.26.0` در `UX2-MS72`
- مرجع: `PMCS-DS-001`، `PMCS-RM-VISUAL-001` و inventory فعال `VX-G1`
- وضعیت: Evidence قرارداد/نمونه برای `VX-G3`؛ خانه‌های فعال `S/G` در G4/G5 بازند.

این ماتریس، رفتار تعاملی Component را از وضعیت داده و Permission جدا می‌کند.
`D` یعنی یک نمونه در UI فعال و آزمون/تصویر مشخص دارد؛ `S` یعنی Source
فعال دارد ولی همهٔ Stateها در Browser/تصویر سنجیده نشده‌اند؛ `P` یعنی
فقط Prototype مستقل MS44/47/49 است؛ `G` شکاف است. هیچ `D` به معنای
Qualification تمام مصرف‌کنندگان یا همهٔ viewportها نیست.

| خانواده | Default | Hover/Focus/Pressed | Disabled/Loading | Error/Success | Offline/Permission | Evidence فعلی و شکاف بعدی |
| --- | --- | --- | --- | --- | --- | --- |
| Action Button، Secondary و Link | D | Focus سراسری S؛ Hover محدود D؛ Pressed G | Disabled D؛ Loading متن/قفل S | نتیجه در سطح Panel S | توقف عملیات آفلاین/بدون مجوز S | MS46 قاب 45 و E2E ثبات Disabled در Hover؛ Pressed، Touch Target و مصرف‌کنندگان اختصاصی باز |
| TextField، Select و Textarea | D | Focus S؛ Hover/Pressed نامربوط | Disabled S؛ Loading در سطح Form؛ Invite Form Busy/Disabled، Preview Select، ظاهر ورود و Wizard قفل‌شده D | Error/Description پراکنده S؛ Success در دعوت D | Form آفلاین قاب 30؛ Permission وابسته به Route | MS68 دعوت، MS69 پیش‌نمایش مجوز، MS70 ظاهر ورود و MS72 Wizard؛ ارتباط Label/Error/Description و کنتراست Focus در همهٔ فرم‌ها باز |
| PersianDateInput و FileInput | D | Focus سراسری و FileInput اختصاصی S | Disabled S؛ Loading FileInput S | Date `aria-invalid` S؛ انتخاب/حذف FileInput D | تابع Form/Permission والد | قاب‌های 18/44، 29/30؛ صفحه‌کلید، بازه/تقویم و مصرف‌های دیگر نیاز به ماتریس مستقل دارند |
| StatusLabel، Badge و Fact/Draft | S | تعامل نامربوط | Loading نباید Fact بسازد D | Label همراه رنگ S | Offline/Stale/NoPermission Label S | semantic tokens در `globals.css`؛ تمایز Fact/Draft/AI و کنتراست هر مصرف باز |
| Empty، Loading/Skeleton و Error | D | Retry Focus S | Skeleton پنهان از AT در Portfolio D؛ Profile، Identity و Login Admin Form Busy/Disabled D؛ Bootstrap Source/Member Loading D | Error و Retry D؛ Profile، Identity، Login Admin و Bootstrap Feedback Error D | Offline/NoPermission در Routeهای منتخب D | MS67 Profile، MS68 Identity، MS70 Login Admin و MS71 Bootstrap با قاب/E2E؛ Stateهای مشترک سایر ماژول‌ها باز |
| Table، Filter و Mobile fallback | D | Row/Action Focus S | فیلتر خالی D؛ Loading وابسته به Route | Validation پراکنده S | دادهٔ ممنوع باید پنهان بماند D | قاب‌های 03/20/25 و 36/38؛ تراکم، overflow و جدول موبایل در G4/G5 باز |
| Modal، Dialog، Popover و Confirm | S | Focus/Keyboard نمونهٔ تقویم S | Blocking Preview D | Conflict/Blocked D | اجرای فاقد مجوز ممنوع S | قاب‌های 29/41 و E2E عدم Execute؛ MS49 Dialog بومی با Escape/return فقط P؛ مصرف‌کنندگان فعال و Popoverها باز |
| Shell/Navigation و Print | D | Keyboard Sidebar و شش Shell موبایل Escape/Focus D | Navigation موبایل شش Shell D؛ Routeهای بدون Sidebar مستقل | پیام وضعیت مستقل S | Print بدون Action/Navigation D | MS64–66 شش Shell، ۲۸ قاب مجزا/Index و E2E؛ سایر Componentها، Routeها و Qualification باز |
| Chat، Reporting، Agent | D/S | Action Focus S | Flags خاموش D | تعارض Chat D؛ Preview گزارش محدود S | Permission/Offline نمونه‌های D/S | قاب‌های 22–24، 33–40 و Prototype MS44؛ مهاجرت و Visual Qualification باز |

## قرارداد حداقلی هر State

1. Action فعال با `button` واقعی، Label روشن و Focus-visible قابل دیدن
   ارائه می‌شود. Hover یا Pressed تنها نشانهٔ امکان Action نیست. Disabled
   با `disabled` بومی و دلیل متنی کنار آن همراه می‌شود؛ Hover رنگ آن را
   تغییر نمی‌دهد. Loading، متن و Busy state واقعی را نشان می‌دهد و
   درخواست تکراری را محدود می‌کند.
2. Field با Label، Help و Error مرتبط است؛ Error با متن و `aria-invalid`
   مشخص می‌شود، نه فقط رنگ. مقدار canonical و تقویم شمسی با تغییر ظاهر
   عوض نمی‌شوند. Success پس از پاسخ معتبر اعلام می‌شود.
3. Feedback از Fact، Snapshot رسمی، Draft، NoData، Stale، Offline،
   Permission و AI Insight تمایز متنی دارد. Skeleton هیچ عدد یا وضعیت
   ساختگی نمی‌سازد و برای Assistive Technology محتوای تکراری ندارد.
4. Preview و Conflict، اقدام پرخطر را تا بررسی نسخه، مانع و تأیید صریح
   غیرفعال می‌کنند. Tooltip/Hover جایگزین متن مانع نیست.
5. در ۳۲۰px و Zoom، Action اصلی و Focus از دسترس خارج نمی‌شوند؛ در Print
   کنترل‌های عملیاتی حذف می‌شوند. این‌ها شرط آزمون هر Component هستند و
   صرف حضور در این سند معادل پاس‌شدن Gate نیست.

## شکاف محدود MS46

در CSS فعال، `button:hover` و `.secondary-button:hover` حتی روی
دکمهٔ `disabled` اعمال می‌شدند و می‌توانستند با تغییر رنگ، سیگنال اشتباه
تعامل بدهند. MS46 این دو Selector را به Action فعال محدود می‌کند. E2E
روی دکمهٔ اجرای Preview مسدود، Hover واقعی، ثابت‌ماندن background، حفظ
`disabled`، نبود درخواست Execute و قاب 45 را ثبت می‌کند. این اصلاح
سراسری CSS به معنای تکمیل State Contract همهٔ دکمه‌ها نیست.

## نمونهٔ Foundation MS47

`docs/ux/prototypes/ms47/index.html` نمونهٔ ایزولهٔ Action، Field،
Status، Feedback و Table با ۹ State است. E2E رفتار واقعی Hover/
Focus/Pressed، Label و ARIA، قفل Disabled/Loading/Offline و
Responsive ۳۲۰px را کنترل و تصویرهای سه اندازه را ثبت می‌کند.
این شواهد، خانه‌های `G` مربوط به مصرف‌کنندگان فعال را خودکار
به `D` تبدیل نمی‌کنند؛ پس از بازبینی Artifact و CI مستندات
فقط Prototype Foundation معتبر می‌شود.

## نمونهٔ ناوبری و تأیید MS49

`docs/ux/prototypes/ms49/index.html` با وزیرمتن نسخهٔ `2.0.0`،
Navigation قابل باز و بسته شدن در موبایل، پیام‌های جدا برای NoData،
Loading، Error، NoPermission، Offline، Conflict و Success و Dialog
بومی `showModal` را نشان می‌دهد. Dialog نسخهٔ فعلی و پیش‌نویس محلی را
روشن می‌کند، تا مرور صریح نسخه تأیید را قفل می‌کند، Escape و بازگشت
تمرکز را نگه می‌دارد و هیچ درخواست عملیاتی نمی‌فرستد. E2E Run 429
semantics/Keyboard، ۱۱ قاب Desktop/Tablet/Mobile و عرض ۳۲۰px را
سنجید؛ Artifact `11004643711` با Index/Hash/ابعاد تطبیق و قاب‌های
نماینده بازبینی شد. این `P` است؛ جایگزین Navigation موبایل و Dialogهای همهٔ
مصرف‌کنندگان فعال در `VX-G4` هنوز انجام نشده است.

## خروجی بعدی برای `VX-G3`

MS50 مقایسهٔ منبع‌دار `docs/ux/review/ms50/` را برای مرور مشترک
UI فعال و Prototypeها آماده می‌کند. ده تصویر از Artifactهای معتبر
CI بدون تغییر همراه SHA/ابعاد نگه داشته شده‌اند. این مقایسه هنوز
خانه‌های `G` یا `S` را به `D` تبدیل نمی‌کند؛ برای تکمیل قرارداد باید
رفتارهای باقیمانده به‌صورت محدود پیاده‌سازی و در Browser سنجیده شوند.

MS51 در `docs/ux/prototypes/ms51/` نمونهٔ مستقل Popover، Drawer،
Toast و fallback ردیف موبایل را در هشت وضعیت داده می‌افزاید. ردیف‌ها
در Loading/NoData/Error/NoPermission/Offline/Conflict پنهان‌اند؛ Skeleton
خنثی `aria-hidden` است و Action در وضعیت‌های فاقد پاسخ/مجوز/اتصال یا
دارای تعارض قفل می‌شود. Popover و Drawer Focus و Escape/بازگشت Focus
دارند؛ Toast متن زنده و دکمهٔ بستن دارد. E2E Run 433 و ۱۲ قاب با
Source/Hash/ابعاد معتبر و بازبینی‌شده‌اند. این نمونه فقط `P` است؛
خانه‌های UI فعال و Qualification مسیرها در `VX-G4/G5` باقی می‌مانند.

MS52 در `docs/ux/prototypes/ms52/` نمونهٔ Login، Shell و Chart را
با جدول جایگزین همان شش مقدار فرضی می‌افزاید. در Loading/NoData/
Error/NoPermission/Offline، نمودار و جدول هر دو پنهان‌اند؛ Skeleton
خنثی و پیام متنی به‌جای نتیجهٔ نامعتبر می‌آیند. Navigation موبایل
Disclosure با Escape/بازگشت Focus دارد؛ Run 435 و ۹ PNG/PDF A4
تک‌صفحه‌ای با Source/Hash/ابعاد معتبر/بازبینی‌شده‌اند. این هم فقط
`P` است، نه تبدیل خودکار خانه‌های مصرف‌کنندگان فعال به `D`.

MS53 در `docs/ux/review/ms53/` شواهد فعال/Prototype را با Manifest
منشأ و معیار تصمیم مالک کنار هم می‌گذارد. هر پاسخ به چهار معیار باید
به اصلاح یا شاهد مشخص برای خانه‌های `S/G` وصل شود؛ هیچ خانه‌ای با
وجود بستهٔ مرور به‌تنهایی به `D` ارتقا نمی‌یابد. تکمیل State contract
و تأیید صریح مالک پیش‌شرط `VX-G3` باقی می‌مانند.

مالک جهت بصری را با پیگیری تراکم موبایل و متن فارسی پذیرفت. MS54
نسخهٔ مستقل MS52 را در دو عرض ۳۹۰/۳۲۰px به‌ترتیب ۱۱۲/۱۱۱px
کوتاه‌تر و متن قابل‌نمایش را فارسی کرد؛ Run 439 و Artifact
`11018211937` دو تصویر با Source/Hash/ابعاد معتبر دارند. State truth،
Navigation/Focus و جدول جایگزین در E2E حفظ شدند. این اصلاح، وضعیت
خانه‌های فعال `S/G` جدول بالا را تغییر نمی‌دهد؛ قبل از پذیرش `VX-G3`
باید قرارداد و Prototypeهای لازم با شواهد صریح بررسی شوند.

- نمونهٔ تعاملی Componentهای مشترک با Default/Hover/Focus/Pressed/
  Disabled/Loading/Error/Success/Offline و مقایسهٔ Desktop/Tablet/Mobile؛
- کنترل معنایی و تصویری فرم، جدول، Status، Dialog و Navigation در
  مصرف‌کنندگان فعال، سپس رفع شکاف‌های محدود در Candidateهای مستقل؛
- وزیرمتن نسخهٔ `2.0.0` با Golden رسمی و Runهای 425/426 پذیرفته شده؛
  آزمون جامع بصری همهٔ مسیرهای مهاجرت‌یافته در `VX-G5` باقی است؛
- بازبینی مالک روی بستهٔ ملموس Prototype و Stateها پیش از اعلام
  `VX-G3`، سپس مهاجرت `VX-G4` و Qualification `VX-G5`.

MS55 در `docs/ux/prototypes/ms55/` نمونهٔ AvatarFallback،
ProfilePhotoCrop و PrivacyLabel را در fallback، تصویر نمونه، Loading،
Error، Offline، NoPermission، Conflict و Success محلی نشان می‌دهد.
Dialog بومی برش، Escape/بازگشت Focus، قفل Action در حالت‌های غیرمجاز،
پنهان‌بودن تصویر نامعتبر یا غیرمجاز و عرض‌های ۳۲۰/۳۹۰/۱۲۸۰ در Browser
کنترل می‌شوند. این فقط `P` برای خانوادهٔ پروفایل است؛ Asset واقعی،
کنترل امنیتی/Revision، مصرف‌کنندهٔ فعال و Qualification در G4/G5 بازند.
فهرست `docs/ux/pmcs-v1.1-vx-g3-closure-ledger.md` سایر خانواده‌های
باز و شواهد لازم را مشخص می‌کند؛ هیچ خانهٔ `S/G` فعال خودکار `D` نمی‌شود.

MS56 در `docs/ux/prototypes/ms56/` خانوادهٔ Wizard تکثیر را با انتخاب،
سیاست تعارض، Preview و مرور محلی نمونه می‌کند. Loading/Empty/Error/
Offline/NoPermission/Expired ردیف‌ها را پنهان و تأیید را قفل می‌کنند؛
تعارض با سیاست توقف، یا هر قلم مسدود حتی با سیاست ردکردن تعارض، مانع است.
تغییر انتخاب/سیاست، پیش‌نمایش قبلی را باطل می‌کند. این فقط `P` است؛
ساخت مقصد، اجرای انتقال، Revision/Permission واقعی و وضعیت خانه‌های فعال
در G4/G5 جدا می‌مانند.

MS57 در `docs/ux/prototypes/ms57/` نمونهٔ چرخهٔ پیوست پیام گروه پروژه
و منشأ مدرک را با صف محلی، ارسال، قرنطینه، آزادشده، رد، آفلاین، منع
مجوز، خطا، تعارض نسخه و نبود فایل نشان می‌دهد. نام/فراداده در منع مجوز
پنهان و همهٔ Actionهای عملیاتی قفل‌اند؛ فقط در وضعیت آزادشده، Dialog
مرور محلی با Escape/بازگشت Focus باز می‌شود. این فقط `P` است؛ اتصال
واقعی پیام، دانلود با تطبیق بایت/هش و تبدیل Evidence در G4/G5 سنجیده
می‌شوند و هیچ خانهٔ فعال خودکار `D` نمی‌شود.

MS58 در `docs/ux/prototypes/ms58/` وضعیت‌های بدون Snapshot، درخواست،
پردازش، آمادگی نمایشی، رد، انقضا، نبود مجوز، آفلاین و خطا را در کنار
برگهٔ چاپ مرورگر نمونه می‌کند. درخواست و دانلود واقعی همیشه قفل‌اند؛
برگه سنجهٔ ساختگی نمی‌سازد و منبع/تازگی/غیررسمی‌بودن را نگه می‌دارد.
PDFهای A4/A3 عمودی/افقی فقط شواهد چاپ مرورگرند. این هم `P` است؛
OutputAccess فعال، Golden رسمی و تمام مصرف‌کنندگان چاپ در G4/G5 بازند.

MS59 در `docs/ux/prototypes/ms59/` میز مفهومی هوشمندی مدیریتی را با
بدون تحلیل، Loading، پیشنهاد آزمایشی، عدم قطعیت بالا، منبع کهنه، منع
مجوز، آفلاین، خطا و تعارض نسخه نمونه می‌کند. فرضیه فقط در وضعیت‌های
مجازِ نمایشی دیده می‌شود؛ منشأ، تازگی، عدم قطعیت و گام انسانی جدا هستند.
مرور منشأ صرفاً محلی است و ساخت پیش‌نویس یا اقدام رسمی همیشه قفل‌اند.
این `P` برای Gate بصری است؛ UI اجرایی Agent و Stageهای ۲ تا ۷ در V1.2
و صلاحیت همهٔ مصرف‌کنندگان در Gateهای بعدی جدا می‌مانند.

MS60 در `docs/ux/prototypes/ms60/` تاریخ شمسی و فیلتر را در ده وضعیت
آماده، تاریخ نامعتبر، بازهٔ ناسازگار، Loading، موفق، بدون نتیجه، خطا،
آفلاین، منع مجوز و نتیجهٔ کهنه نمونه می‌کند. Label/Help/Error مرتبط،
Focus ورودی خطادار، قفل اقدام، پنهان‌سازی نتیجهٔ نامعتبر و جای‌نگهدار
خنثی با Browser E2E و تصویر سه عرض سنجیده می‌شوند. این فقط `P` برای
Field/Filter/Table است؛ parser، تقویم، API و همهٔ مصرف‌کنندگان فعال
در `VX-G4/G5` جداگانه آزموده می‌شوند.

MS61 آزمون Token متن/Status و Focus/Reduced Motion را به قرارداد
Foundation افزود و بستهٔ `docs/ux/review/ms61/` را با چهار معیار
مالک به‌روز کرد. خانه‌های نمونهٔ مستقل `P` می‌مانند؛ این شواهد
خانه‌های `S/G` مصرف‌کنندهٔ فعال را پیش از مهاجرت `VX-G4` به `D`
تبدیل نمی‌کنند. آزمون تمام مسیرها و مرورگرها در `VX-G5` باز است.

MS62 برای شکاف چاپ/تراکم، یک منبع ساختگی با ۴۸ ردیف و شرح بلند،
شش وضعیت، مسئول، تاریخ و مبلغ صحیح دارد. همان ردیف‌ها در موبایل
برچسب‌دار می‌شوند؛ صفحه‌بندی نمایش داده را حذف نمی‌کند و چاپ از صفحهٔ
دوم نیز تمام ردیف‌ها و جمع یکتا را می‌آورد. A4/A3 عمودی/افقی، تکرار
سرستون و زمینهٔ غیررسمی و شکست صفحه در Run 466 و Artifact
`11029742768` سنجیده و هر ۲۴ صفحهٔ چاپ بازبینی شدند؛ CI مستقل مستندات
شرط Checkpoint است.
این شاهد `P` است؛ معیار چهارم و G3 تا بازبینی و پاسخ مالک بازند.

## مرزبندی پذیرش VX-G3 در MS63

برای Action/Field/Status/Feedback/Table، نمونهٔ MS47 حالت‌های تعاملی،
Pressed، Disabled/Loading، Error/Success، Offline/Permission و ردیف
موبایل را پوشش می‌دهد؛ MS51 Overlay/Drawer/Toast و جدول موبایل را در
هشت وضعیت تکمیل می‌کند. MS49 Navigation، پیام و Dialog؛ MS52 ترکیب
Login/Shell/Chart؛ MS55 تصویر/برش/حریم خصوصی؛ MS56 Wizard انتخاب و
Conflict؛ MS57 پیوست/مدرک و منشأ؛ MS58 خروجی/چاپ؛ MS59 هوشمندی مفهومی؛
MS60 تاریخ/فیلتر و MS62 جدول پُرداده/چاپ چندصفحه‌ای را نمونه کرده‌اند.
MS48/61 قلم و Token، Focus و Reduced Motion را با شواهد مرورگر پوشش
می‌دهند. تأیید مالک برای چهار معیار در برگهٔ MS62 ثبت است.

حروف `S/G` در سطرهای بالا شکاف **مصرف‌کنندهٔ فعال** هستند، نه ادعای
نبود Prototype برای Contract. `VX-G3` فقط قرارداد و نمونه‌های شواهددار
را پس از CI مستقل MS63 می‌پذیرد؛ هیچ خانهٔ `S/G` با این تصمیم به `D`
تبدیل نمی‌شود. اتصال کامل همهٔ مسیرهای فعال به Componentهای مشترک در
`VX-G4`، سپس آزمون رفتاری/بصری/چاپ/دسترس‌پذیری در `VX-G5` انجام می‌شود.

## پیوست UX2-MS64 — نخستین مصرف‌کنندهٔ فعال

فهرست موبایل `/portfolio` با مجموعهٔ پیوندهای مشترک، حالت باز/بسته،
`aria-current`، Enter/Tab/Escape و بازگشت Focus در Run 470 و شش قاب
سه عرض سنجیده شد. این `D` فقط برای Navigation همان Route است؛ پنج Shell
دیگر در این مرحله تغییر نکرده‌اند و ستون‌های دیگر جدول با شاهد MS64
به‌طور خودکار تکمیل نمی‌شوند. Artifact `11032562526` و Checkpoint
`PMCS-V1.1-UX2-MS64-C1` منشأ این تغییر هستند.

## پیوست UX2-MS65 — مرکز فرمان پروژه

در Runهای 473/474 ناوبری مرکز فرمان پروژه به Disclosure مشترک منتقل شد.
Artifact نهایی `11035335891` شش قاب سه عرض، راهنمای پیمایش فهرست بلند،
Source/Hash/ابعاد معتبر و مرور بصری دارد. `D` فقط به دو Navigation فعال
Portfolio و Project Command Center اشاره می‌کند؛ چهار Shell باقی‌مانده،
Stateهای دیگر و Qualification در G4/G5 بازند.

## پیوست UX2-MS66 — Navigation شش Shell

چهار Shell مدیریت هویت، گزارش سبد، گفت‌وگوی پروژه و گزارش پروژه در
Run 476 به Disclosure مشترک منتقل شدند. Artifact `11036398624` شانزده
قاب ۳۹۰/۳۲۰ باز/بسته، Source/Hash/ابعاد معتبر و مرور بصری دارد.
Navigation شش Shell در محدودهٔ این زیرکار `D` است؛ Routeهای بدون
Sidebar، پیام/فرم/جدول/دیالوگ و Qualification مستقل G4/G5 بازند.

## پیوست UX2-MS67 — Feedback فعال پروفایل

Run 480 پیام‌های دریافت/ذخیره، خطا و موفقیت `/profile` را با Role/Tone
مجزا، Form Busy و غیرفعال‌بودن کنترل‌ها هنگام بارگذاری/ذخیره سنجید.
Artifact `11038772992` پنج قاب Source/Hash/ابعاد معتبر و مرورشده دارد.
این `D` فقط به مصرف‌کنندهٔ Profile اشاره می‌کند؛ سایر Form/Feedbackها،
Routeها و Qualification مستقل در G4/G5 بازند.

## پیوست UX2-MS68 — Feedback و Retry دعوت هویت

Run 484 دریافت/ثبت در `/admin/users` را با `status`/`alert`، پنهان‌کردن
فهرست تا دریافت معتبر، Busy/Disabled فرم، نگه‌داشتن ورودی پس از خطا و
Idempotency Key ثابت برای Retry همان payload سنجید. Artifact
`11040584680` شش قاب ۳۹۰/۳۲۰ با Source/Hash/ابعاد معتبر و مرورشده
دارد. این `D` فقط به مصرف‌کنندهٔ دعوت/فهرست هویت اشاره می‌کند؛
پیش‌نمایش مجوز و سایر مصرف‌کنندگان G4/G5 بازند.

## پیوست UX2-MS69 — حقیقت پیش‌نمایش مجوز

در `/admin/users`، تغییر پروژه یا نقش نتیجهٔ شبیه‌سازی قبلی را پاک
می‌کند. درخواست جدید تا پاسخ، Selectها و نتیجهٔ قبلی را قفل/پنهان می‌کند؛
Loading/Success با `status` و Error با `alert` از هم جدا هستند. Run 489
هشت Job سبز و Artifact `11044303294` چهار قاب با Source/Hash/ابعاد
معتبر و بازبینی بصری دارد. این `D` فقط به مصرف‌کنندهٔ پیش‌نمایش مجوز
اشاره می‌کند؛
سایر Form/Feedbackها و Qualification در G4/G5 بازند.

## پیوست UX2-MS70 — حقیقت تاریخچه و فرم ظاهر ورود

در `/admin/login-experience` دریافت تاریخچه با `aria-busy` و پیام
`status` از فهرست خالی معتبر جداست. خطای دریافت `alert` و Retry دارد
و رکوردی به دروغ «خالی» معرفی نمی‌شود. فرمان ساخت/انتشار، فرم را قفل
می‌کند؛ خطای ساخت دادهٔ ورودی را نگه می‌دارد و خطای Refresh پس از فرمان
به‌جای ادعای موفقیت فهرست نشان داده می‌شود. Run 491 هشت Job سبز و
Artifact `11046207203` چهار قاب با Source/Hash/ابعاد معتبر و بازبینی‌شده
دارد. این `D` محدود به مصرف‌کنندهٔ فعال ظاهر ورود است؛ G4/G5 سراسری
بازند.

## پیوست UX2-MS71 — حقیقت دادهٔ مبدأ و اعضا در Wizard

در `/project-bootstraps` فهرست پروژه و اعضا تا دریافت پاسخ معتبر
ناشناخته می‌مانند. خطای مبدأ، Select را با پیام خطا قفل می‌کند؛ خطای
اعضا به‌عنوان «بدون عضو» نمایش داده نمی‌شود. Retry هر دو فهرست را
تازه می‌خواند و Preview تا وجود مبدأ معتبر و در دسترس بودن دادهٔ
اعضای انتخاب‌شده مسدود است؛ حذف آگاهانهٔ دستهٔ اعضا امکان ادامهٔ
بدون آن را حفظ می‌کند. Run 493 متن Loading باقی‌مانده پس از خطا را
یافت؛ correction Run 494 هشت Job سبز و Artifact `11049002783` چهار قاب
معتبر و بازبینی‌شده دارد. کد نقش/وضعیت عضو هنوز در نمایش انگلیسی است
و در G4 بعدی فارسی می‌شود. این `D` فقط به دریافت دادهٔ Wizard مربوط
است؛ سایر G4/G5 بازند.

## پیوست UX2-MS72 — قفل فرمان Wizard و برچسب اعضا

در `/project-bootstraps` هنگام ساخت Preview تمام کنترل‌های Form با
Fieldset بومی و `aria-busy`، و جابه‌جایی مراحل با Disabled قفل‌اند.
تأیید Preview حین Refresh غیرفعال است و Handlerهای Preview/Execute/
Activate فرمان هم‌زمان را رد می‌کنند. خطای ساخت ورودی را نگه می‌دارد.
نقش و وضعیت اعضا با برچسب فارسی مشترک با مدیریت هویت نشان داده می‌شوند؛
کد نقش همچنان مقدار واقعی Option و Payload است. Run 497 هشت Job سبز و
Artifact `11050303281` با سه قاب ۳۹۰/۳۲۰ پس از تطبیق Source/Hash/بایت/ابعاد
و بازبینی بصری ثبت شدند.
این `D` فقط به مصرف‌کنندهٔ Wizard اشاره می‌کند؛ سایر G4/G5 بازند.
