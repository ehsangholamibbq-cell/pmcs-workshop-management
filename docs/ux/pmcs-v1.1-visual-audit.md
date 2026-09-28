# PMCS V1.1 — Visual and UX Audit

- شناسه: `PMCS-UX-AUDIT-001`
- وضعیت: `Evidence In Progress`
- خط محصول: `PMCS V1.1`
- Baseline بررسی: `4e401ab9e2bfab5bd197e9789d7a87e91e8a5784`
- تاریخ: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Runtime change: ندارد

> بخش‌های ۱ تا ۶ یافته‌های تاریخی baseline روز ۲۰۲۶-۰۹-۱۷ هستند. وضعیت زندهٔ
> مسیرها و stateها و Evidence جدید در پیوست UX2-MS33 پایین و سند
> `pmcs-v1.1-route-state-baseline.md` ثبت می‌شود؛ یافتهٔ تاریخی را وضعیت فعلی
> لوگو یا Design System تفسیر نکنید.

## ۱. دامنه ممیزی

ممیزی مستقیم روی Source فعال Web انجام شد و این محدوده را پوشش داد:

- ۵ Route اصلی App Router: ورود، Portfolio/Registry، پروژه، مدیریت کاربران و Landing؛
- ۲۹ Component فعال رابط؛
- ۴۰ فایل `TSX/CSS` و حدود ۵۰۰ هزار نویسه Source؛
- Stylesheet سراسری با ۶۵٬۱۰۸ نویسه؛
- Login، Shell، Dashboard، فرم‌ها، جدول‌ها، Offline/Sync، Error، Loading و Permission-aware states؛
- RTL، فارسی و تعامل با تقویم شمسی.

این ممیزی Business Rule، Permission Truth یا رفتار Backend را تغییر نمی‌دهد.

## ۲. نقاط سالم Baseline

- سند ریشه `lang=fa` و `dir=rtl` دارد؛
- محتوای عملیاتی، هشدار، نبود داده و وضعیت نامعلوم در بسیاری از مسیرها از هم جدا شده‌اند؛
- Date input شمسی و Auditهای فارسی/تقویم موجودند؛
- Responsive پایه در چند Breakpoint وجود دارد؛
- Offline، Sync، Conflict و Error state در Source دیده می‌شوند؛
- Style inline در Componentهای بررسی‌شده استفاده نشده است؛
- Fact، Status و AI Insight از نظر متن و منطق با هم مخلوط نشده‌اند.

## ۳. یافته‌های اصلی

### P1 — هویت بصری و سلسله‌مراتب

- نشان رسمی شرکت در Login و Shell استفاده نشده و Monogram موقت جای آن را گرفته است؛
- Typography فعلی بر Tahoma/Segoe UI تکیه دارد و Treatment حرفه‌ای متن فارسی و اعداد تعریف نشده است؛
- Login از نظر ترکیب و Motion با مسیر مصوب «مدیریت ممتاز» فاصله دارد؛
- Card، Form، Dashboard و Moduleها سلسله‌مراتب یکسان و قابل پیش‌بینی ندارند.

### P1 — Design Token و Semantic Color

- ۶۳ مقدار Hex مستقل در Stylesheet وجود دارد؛
- رنگ برند و رنگ وضعیت در چند نقطه به‌صورت Ad-hoc استفاده شده‌اند؛
- قرارداد مستقل برای Fact، Draft، Warning، Error، Offline، Stale و AI Insight کامل نیست؛
- Radius، Elevation، Spacing و Density هنوز Package رسمی و نسخه‌دار ندارند.

### P1 — Accessibility و Motion

- هیچ قرارداد `:focus-visible` سراسری ثبت نشده است؛
- `prefers-reduced-motion` در Stylesheet فعلی وجود ندارد؛
- Focus order، keyboard matrix و screen-reader state هنوز Evidence رسمی ندارند؛
- کنتراست و Target size باید در VX-G5 به‌صورت خودکار Qualification شوند.

### P2 — Responsive و Density

- فقط ۷ Media Query سراسری با Breakpointهای پراکنده وجود دارد؛
- جدول‌ها و فرم‌های پرتراکم Contract مشترک برای Desktop/Tablet/Mobile ندارند؛
- Shell جمع‌شونده، Navigation موبایل و Project Context استاندارد نشده‌اند؛
- Empty/Loading/Error از نظر منطق موجودند ولی زبان بصری یکپارچه ندارند.

### P2 — قابلیت‌های جدید V1.1

- پروفایل شخصی و Avatar هنوز Primitive رسمی ندارند؛
- Project Duplication Wizard، Preview و Conflict state هنوز UI رسمی ندارند؛
- LoginExperienceDescriptor هنوز Component/Token mapping بصری ندارد؛
- رابط Agent نباید به Chat box ساده تقلیل یابد و در UX بعدی به Workspace مدیریتی نیاز دارد.

## ۴. نتیجه حرفه‌ای

V1 از نظر Function و Operational State قابل اتکاست، اما ظاهر فعلی Baseline طراحی نهایی نیست. Patch محدود روی CSS این فاصله را حل نمی‌کند؛ مهاجرت باید پس از قفل‌شدن Tokenها و Component Contract به‌صورت موجی انجام شود.

## ۵. وضعیت Gateها

| Gate | وضعیت پس از این ممیزی | دلیل |
| --- | --- | --- |
| `VX-G1 Audit Complete` | Evidence In Progress | Source inventory کامل است؛ screenshot baseline تمام stateهای بحرانی هنوز باید در محیط Qualification تولید شود |
| `VX-G2 Direction Approved` | Approved | مسیر «مدیریت ممتاز» قبلاً توسط مالک محصول تصویب شده است |
| `VX-G3 System Ready` | Awaiting Owner Review | Token/Component Contract و Review Candidate آماده شده‌اند اما تأیید بصری مالک محصول لازم است |
| `VX-G4 Migration Complete` | Not Started | هیچ Runtime migration در این مرحله مجاز نیست |
| `VX-G5 Visual Qualified` | Not Started | پس از مهاجرت کامل اجرا می‌شود |

## ۶. اقدام بعدی مجاز

پس از تأیید Review Pack:

1. ثبت Design System Foundation به‌عنوان `VX-G3`؛
2. پیاده‌سازی Shared UI بدون تغییر Business Rule؛
3. مهاجرت Identity، Shell، Portfolio و Project Command Center؛
4. اجرای screenshot، accessibility، responsive و performance qualification.

## ۷. پیوست UX2-MS33 — Inventory زنده و Screenshot Candidate

Source فعال اکنون ۱۱ Route و ۳۸ Capture قراردادی از Identity، Shell، Portfolio،
Project، پروفایل، مدیریت، Collaboration و Reporting دارد. حالت‌های فیلتر خالی،
Loading، خطا، عدم دسترسی، مجازِ محدود به پروژه، Offline/Validation، 404،
Desktop/Tablet/Mobile و Featureهای پیش‌فرض خاموش در این مجموعه‌اند. فایل
`src/web/e2e/visual-baseline.json` و سند
`docs/ux/pmcs-v1.1-route-state-baseline.md` مالک فهرست فعلی‌اند. Source و
Artifact Run 380 با ۳۸ تصویر و index/SHA-256 تطبیق شدند. ۴۰ فایل Component TSX
موجود و CSS سراسری ۱۰۴٬۲۸۴ بایتی نسبت به inventory تاریخی ۲۹ Component و
۶۵٬۱۰۸ نویسه رشد کرده‌اند؛ قرارداد component state هنوز review کامل می‌خواهد.

### یافته‌های زنده و اولویت اقدام

| اولویت | Evidence واقعی | اقدام محدود بعدی |
| --- | --- | --- |
| P1 | تصویر 04 دسکتاپ: نشان Sidebar در ارتفاع ۹۰۰px به خط باریک فشرده شده و انتهای Navigation در viewport دیده نمی‌شود | MS34: جلوگیری از flex shrink، اسکرول مستقل و focus/keyboard قابل دسترس |
| P2 | تصاویر 20–23 موبایل: Navigation افقی بخشی از متن لبه را قطع می‌کند و affordance اسکرول روشن نیست | MS34: keyboard و نبود overflow کل سند در ۳۹۰/۳۲۰px؛ affordance بصری در Gate بعدی |
| P2 | تصویر 18 مدیریت ظاهر Login: کنترل‌های بومی انتخاب فایل متن انگلیسی دارند | مهاجرت shared FileInput و RTL/locale در Micro-Step بعدی |
| P2 | تصویر 26 Portfolio در Loading تقریباً خالی است | قرارداد Loading/Skeleton و کاهش جابه‌جایی محتوا |

تصاویر Run 379 نقطهٔ شروع Command Center موبایل را از میانهٔ کارت نشان دادند؛
Run 380 با scroll policy صریح، تصویرهای 04/21/28 را اصلاح کرد. Stateهای
Conflict، Preview تکثیر، خطای Login، Print/PDF و فونت جایگزین هنوز Evidence
بصری کافی ندارند؛ بنابراین `VX-G1` باز است و این Audit معادل Qualification نیست.

نشان شفاف رسمی فعلی تأیید شده است. مسیر نسخه‌دار تعویض فونت در MS32 ساخته شد؛
فونت فارسی تازه و Qualification چاپ/تصویر هنوز انجام نشده است. `VX-G2` مصوب
است؛ `VX-G3`، `VX-G4` و `VX-G5` بازند.

## ۸. پیوست UX2-MS34 — دسترسی به ناوبری بلند

Source correction Candidate `0dab8c390a01da1b03f6e79da368b086353d9e98` روی یافتهٔ P1
تصویر 04 متمرکز است: نشان Sidebar دیگر با Flex کوتاه نمی‌شود، لینک‌های پایین
در ظرف عمودی مستقل اسکرول می‌شوند و focus آخرین لینک در viewport و با outline
مشهود می‌ماند. تصویر 39 انتهای ناوبری را در `1440×900` ثبت می‌کند. سنجش
`320×720` علاوه بر `390×844` مانع overflow افقی کل صفحه است و رسیدن با صفحه‌کلید
به لینک پروفایل را کنترل می‌کند. Run 382 در assertion برنامه‌ای focus-visible
رد شد؛ Runهای 383/384 overflow عرض ۳۲۰ را آشکار کردند و Run 385 برچسب
`.section-note` را عامل ۲px باقی‌مانده شناسایی کرد. Run 386 هشت Job سبز شد و
Artifact ۳۹تایی با index/SHA و تصاویر 04/39/21 بازبینی شد. CI مستندات
شرط اعتبار Checkpoint است.

این اصلاح، affordance بصری کامل پیمایش موبایل یا Gapهای Conflict، Preview،
Login failure، Print و font جایگزین را Qualification نمی‌کند. `VX-G1` باز
می‌ماند و MS35 برای screenshot تعارض ویرایش پیام پروژه برنامه‌ریزی شده است.

## ۹. پیوست UX2-MS35 — State تعارض پیام پروژه

Source Candidate `31d6ea01515e49e8b9974f30768afcaeb4650a65` تصویر 40 را
در تست واقعی Chat گروه پروژه پس از پاسخ 409 اضافه می‌کند. نسخهٔ سرور و
پیش‌نویس نویسنده هم‌زمان دیده می‌شوند؛ پس از capture، مسیر rebase/تأیید
دوبارهٔ موجود ادامه می‌یابد. این Evidence تنها یک state محدود است؛ Feature
Flagها پیش‌فرض خاموش‌اند. Run 388 هر هشت Job سبز و Artifact چهل‌تصویری با
index/SHA-256 معتبر دارد؛ تصویر 40 بازبینی شد و متن هر دو نسخه در قاب دیده
می‌شود. CI مستندات Run 389 هر هشت Job سبز شد و MS35 Safe است.
Preview تکثیر، خطای Login، Print و انتخاب/Qualification فونت تازه بازند؛
`VX-G1/G3/G4/G5` بسته اعلام نمی‌شوند.

## ۱۰. پیوست UX2-MS36 — پیش‌نمایش مسدود تکثیر

Source Candidate `e99fc97f8d877a180e01f0711c1ceefd6953e078` قاب 41 را از
Wizard فعال می‌گیرد. Fixture محدود پاسخ نسخه‌دار ساخت برنامه را با ردیف‌های
افزودنی، متعارض و مسدود برمی‌گرداند؛ خود درخواست، هویت مبدأ/مقصد، سیاست
تعارض و کلید Idempotency کنترل می‌شوند. تأیید کاربر ثبت می‌شود اما دکمهٔ
اجرا در حضور مانع غیرفعال است و هیچ درخواست Execute صادر نمی‌شود. این
Evidence به‌معنای مجوز اجرای واقعی یا Qualification تمام Preview نیست. Run
390 به‌خاطر انتظار دکمه پس از گذار Wizard در UI-E2E شکست خورد؛ Run 391/392
نشان دادند Preview پیش از Submit صریح آزمون ساخته شده بود. Correction
`9202660180e48a0124130123ae43fc1d6821597c` از Navigation مستقل برای
مرحلهٔ اعضا استفاده و نبود درخواست ساخت پیش از Submit را کنترل می‌کند. Run
393 هر هشت Job سبز و Artifact `10989402355` با ۴۱ PNG، index/SHA و ابعاد
معتبر است؛ تصویر 41 بازبینی شد. CI مستندات Run 394 هشت Job سبز شد و MS36 Safe است. خطای Login، Print،
انتخاب و Qualification فونت و بازبینی نهایی `VX-G1/G3/G4/G5` باز می‌مانند.

## ۱۱. پیوست UX2-MS37 — خطای callback هویت

Source Candidate `32d6f6bda739f399d7df5689db920cabd9acb0b9` تصویر 42 را از
صفحهٔ Login پیش از نشست احرازشده می‌گیرد. پارامتر خطای callback، پیام فارسی
با `role=alert` و امکان تلاش دوباره را ظاهر می‌کند؛ پس از Capture، Setup
ورود واقعی OIDC را ادامه می‌دهد. Credential در صفحهٔ PMCS گرفته نمی‌شود.
Run 395 در Setup به‌دلیل Locator عمومی Alert با route announcer خالی Next.js
رد شد؛ correction `20e4a0e2ab64eb8893311cfc88daac6a60d07712` Alert خود کارت
Login را هدف می‌گیرد. Run 396 هر هشت Job سبز و Artifact `10990146859` با
۴۲ PNG، index/SHA و ابعاد معتبر است؛ تصویر 42 بازبینی شد. این Evidence فقط
state خطا و راه بازگشت را پوشش می‌دهد؛ CI مستندات شرط اعتبار Candidate است.
Print، فونت تازه و Qualification تمام
Stateها و Gateهای `VX-G1/G3/G4/G5` بازند.
