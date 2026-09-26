# PMCS V1.1 — Visual and UX Audit

- شناسه: `PMCS-UX-AUDIT-001`
- وضعیت: `Evidence In Progress`
- خط محصول: `PMCS V1.1`
- Baseline بررسی: `4e401ab9e2bfab5bd197e9789d7a87e91e8a5784`
- تاریخ: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Runtime change: ندارد

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

