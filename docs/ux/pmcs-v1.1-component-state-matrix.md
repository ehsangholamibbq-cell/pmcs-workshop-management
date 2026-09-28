# PMCS V1.1 — ماتریس Stateهای Component مشترک

- شناسه: `PMCS-UX-COMPONENT-STATES-001`
- نسخهٔ Candidate: `0.1.1` در `UX2-MS46`
- مرجع: `PMCS-DS-001`، `PMCS-RM-VISUAL-001` و inventory فعال `VX-G1`
- وضعیت: Evidence محدود؛ `VX-G3 System Ready` باز است.

این ماتریس، رفتار تعاملی Component را از وضعیت داده و Permission جدا می‌کند.
`D` یعنی یک نمونه در UI فعال و آزمون/تصویر مشخص دارد؛ `S` یعنی Source
فعال دارد ولی همهٔ Stateها در Browser/تصویر سنجیده نشده‌اند؛ `P` یعنی
فقط Prototype مستقل MS44 است؛ `G` شکاف است. هیچ `D` به معنای
Qualification تمام مصرف‌کنندگان یا همهٔ viewportها نیست.

| خانواده | Default | Hover/Focus/Pressed | Disabled/Loading | Error/Success | Offline/Permission | Evidence فعلی و شکاف بعدی |
| --- | --- | --- | --- | --- | --- | --- |
| Action Button، Secondary و Link | D | Focus سراسری S؛ Hover محدود D؛ Pressed G | Disabled D؛ Loading متن/قفل S | نتیجه در سطح Panel S | توقف عملیات آفلاین/بدون مجوز S | MS46 قاب 45 و E2E ثبات Disabled در Hover؛ Pressed، Touch Target و مصرف‌کنندگان اختصاصی باز |
| TextField، Select و Textarea | D | Focus S؛ Hover/Pressed نامربوط | Disabled S؛ Loading در سطح Form | Error/Description پراکنده S؛ Success G | Form آفلاین قاب 30؛ Permission وابسته به Route | ارتباط Label/Error/Description و کنتراست Focus در همهٔ فرم‌ها باز |
| PersianDateInput و FileInput | D | Focus سراسری و FileInput اختصاصی S | Disabled S؛ Loading FileInput S | Date `aria-invalid` S؛ انتخاب/حذف FileInput D | تابع Form/Permission والد | قاب‌های 18/44، 29/30؛ صفحه‌کلید، بازه/تقویم و مصرف‌های دیگر نیاز به ماتریس مستقل دارند |
| StatusLabel، Badge و Fact/Draft | S | تعامل نامربوط | Loading نباید Fact بسازد D | Label همراه رنگ S | Offline/Stale/NoPermission Label S | semantic tokens در `globals.css`؛ تمایز Fact/Draft/AI و کنتراست هر مصرف باز |
| Empty، Loading/Skeleton و Error | D | Retry Focus S | Skeleton پنهان از AT در Portfolio D | Error و Retry D | Offline/NoPermission در Routeهای منتخب D | قاب‌های 25–27، 30–31، 33/35؛ Stateهای مشترک سایر ماژول‌ها باز |
| Table، Filter و Mobile fallback | D | Row/Action Focus S | فیلتر خالی D؛ Loading وابسته به Route | Validation پراکنده S | دادهٔ ممنوع باید پنهان بماند D | قاب‌های 03/20/25 و 36/38؛ تراکم، overflow و جدول موبایل در G4/G5 باز |
| Modal، Dialog، Popover و Confirm | S | Focus/Keyboard نمونهٔ تقویم S | Blocking Preview D | Conflict/Blocked D | اجرای فاقد مجوز ممنوع S | قاب‌های 29/41 و E2E عدم Execute؛ focus trap/return، Escape و Responsive در خانواده‌های دیگر باز |
| Shell/Navigation و Print | D | Keyboard انتهای Sidebar D | Navigation جایگزین موبایل G | پیام وضعیت مستقل S | Print بدون Action/Navigation D | قاب‌های 39، 20–23، 43؛ جایگزین موبایل، Golden و PDF رسمی باز |
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

## خروجی بعدی برای `VX-G3`

- نمونهٔ تعاملی Componentهای مشترک با Default/Hover/Focus/Pressed/
  Disabled/Loading/Error/Success/Offline و مقایسهٔ Desktop/Tablet/Mobile؛
- کنترل معنایی و تصویری فرم، جدول، Status، Dialog و Navigation در
  مصرف‌کنندگان فعال، سپس رفع شکاف‌های محدود در Candidateهای مستقل؛
- انتخاب مستند و قابل بازگشت فونت با اختیار واگذارشدهٔ مالک در
  ۲۰۲۶-۰۹-۲۹، سپس آزمون واقعی Web/Offline/PDF/XLSX/Print
  پیش از تعویض خانوادهٔ تولیدی؛
- بازبینی مالک روی بستهٔ ملموس Prototype و Stateها پیش از اعلام
  `VX-G3`، سپس مهاجرت `VX-G4` و Qualification `VX-G5`.
