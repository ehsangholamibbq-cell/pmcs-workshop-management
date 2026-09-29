# PMCS V1.1 — ماتریس Stateهای Component مشترک

- شناسه: `PMCS-UX-COMPONENT-STATES-001`
- نسخهٔ Candidate: `0.10.0` در `UX2-MS56`
- مرجع: `PMCS-DS-001`، `PMCS-RM-VISUAL-001` و inventory فعال `VX-G1`
- وضعیت: Evidence محدود؛ `VX-G3 System Ready` باز است.

این ماتریس، رفتار تعاملی Component را از وضعیت داده و Permission جدا می‌کند.
`D` یعنی یک نمونه در UI فعال و آزمون/تصویر مشخص دارد؛ `S` یعنی Source
فعال دارد ولی همهٔ Stateها در Browser/تصویر سنجیده نشده‌اند؛ `P` یعنی
فقط Prototype مستقل MS44/47/49 است؛ `G` شکاف است. هیچ `D` به معنای
Qualification تمام مصرف‌کنندگان یا همهٔ viewportها نیست.

| خانواده | Default | Hover/Focus/Pressed | Disabled/Loading | Error/Success | Offline/Permission | Evidence فعلی و شکاف بعدی |
| --- | --- | --- | --- | --- | --- | --- |
| Action Button، Secondary و Link | D | Focus سراسری S؛ Hover محدود D؛ Pressed G | Disabled D؛ Loading متن/قفل S | نتیجه در سطح Panel S | توقف عملیات آفلاین/بدون مجوز S | MS46 قاب 45 و E2E ثبات Disabled در Hover؛ Pressed، Touch Target و مصرف‌کنندگان اختصاصی باز |
| TextField، Select و Textarea | D | Focus S؛ Hover/Pressed نامربوط | Disabled S؛ Loading در سطح Form | Error/Description پراکنده S؛ Success G | Form آفلاین قاب 30؛ Permission وابسته به Route | ارتباط Label/Error/Description و کنتراست Focus در همهٔ فرم‌ها باز |
| PersianDateInput و FileInput | D | Focus سراسری و FileInput اختصاصی S | Disabled S؛ Loading FileInput S | Date `aria-invalid` S؛ انتخاب/حذف FileInput D | تابع Form/Permission والد | قاب‌های 18/44، 29/30؛ صفحه‌کلید، بازه/تقویم و مصرف‌های دیگر نیاز به ماتریس مستقل دارند |
| StatusLabel، Badge و Fact/Draft | S | تعامل نامربوط | Loading نباید Fact بسازد D | Label همراه رنگ S | Offline/Stale/NoPermission Label S | semantic tokens در `globals.css`؛ تمایز Fact/Draft/AI و کنتراست هر مصرف باز |
| Empty، Loading/Skeleton و Error | D | Retry Focus S | Skeleton پنهان از AT در Portfolio D | Error و Retry D | Offline/NoPermission در Routeهای منتخب D | قاب‌های 25–27، 30–31، 33/35؛ Stateهای مشترک سایر ماژول‌ها باز |
| Table، Filter و Mobile fallback | D | Row/Action Focus S | فیلتر خالی D؛ Loading وابسته به Route | Validation پراکنده S | دادهٔ ممنوع باید پنهان بماند D | قاب‌های 03/20/25 و 36/38؛ تراکم، overflow و جدول موبایل در G4/G5 باز |
| Modal، Dialog، Popover و Confirm | S | Focus/Keyboard نمونهٔ تقویم S | Blocking Preview D | Conflict/Blocked D | اجرای فاقد مجوز ممنوع S | قاب‌های 29/41 و E2E عدم Execute؛ MS49 Dialog بومی با Escape/return فقط P؛ مصرف‌کنندگان فعال و Popoverها باز |
| Shell/Navigation و Print | D | Keyboard انتهای Sidebar D | Navigation جایگزین موبایل G | پیام وضعیت مستقل S | Print بدون Action/Navigation D | قاب‌های 39، 20–23، 43؛ Prototype MS49 Disclosure موبایل دارد، اما UI فعال هنوز مهاجرت نکرده است |
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
