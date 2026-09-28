# PMCS V1.1 — Design System Contract

- شناسه: `PMCS-DS-001`
- نسخه Candidate: `1.0.0-rc.8`
- مسیر بصری: `مدیریت ممتاز`
- وضعیت: `Awaiting Owner Visual Review`
- Runtime change: ندارد
- تأیید مالک محصول در ۲۰۲۶-۰۹-۲۸: نشان کامل و برش شفاف نماد از
  `assets/brand/official-mark.pdf` برای مبنای فعلی PMCS تأیید شدند؛ منشأ و Hash در
  `docs/ux/pmcs-v1.1-brand-source.md` ثبت است. این تأیید، Gate مهاجرت و Visual QA را نمی‌بندد.

## ۱. اصول غیرقابل مذاکره

- رابط فارسی و RTL اصیل است؛
- تمام تاریخ‌های ورودی، خروجی، گزارش و Print شمسی باقی می‌مانند؛
- UI حق تغییر Fact، Permission، Workflow یا State Truth را ندارد؛
- نشان PMCS همان نشان رسمی بتن بسپار قزوین است و بازطراحی مستقل آن ممنوع است؛
- سرمه‌ای پایه، سبز و نقره‌ای Accent محدود و Surfaceها گرم و کم‌خستگی هستند؛
- رنگ‌های وضعیت از Branding مستقل‌اند؛
- جلوه‌های متحرک کوتاه، هدفمند، غیرتکراری و قابل حذف با Reduced Motion هستند؛
- Dark Theme در V1.1 Scope خودکار نیست.

## ۲. Brand و Color Tokens

### Brand

| Token | Candidate | کاربرد |
| --- | --- | --- |
| `brand.navy.950` | `#071525` | Shell و Login architecture |
| `brand.navy.900` | `#0C2036` | Navigation و Heading |
| `brand.navy.800` | `#15334D` | Selected/secondary structure |
| `brand.green.700` | `#11643B` | Primary action محدود |
| `brand.green.600` | `#187A49` | Focus/active accent |
| `brand.silver.400` | `#9BA4A8` | Architectural accent |

### Warm Surface

| Token | Candidate | کاربرد |
| --- | --- | --- |
| `surface.canvas` | `#FBF8F1` | Canvas اصلی |
| `surface.subtle` | `#F5EFE4` | Secondary surface |
| `surface.card` | `#FFFFFF` | Card و Form |
| `border.default` | `#E9DFCF` | Border گرم |
| `text.primary` | `#17242E` | متن اصلی |
| `text.secondary` | `#68747C` | متن توضیحی |

### Semantic

Semantic tokens باید مستقل از Brand تعریف شوند:

- `status.info` و `status.info.surface`؛
- `status.success` و `status.success.surface`؛
- `status.warning` و `status.warning.surface`؛
- `status.danger` و `status.danger.surface`؛
- `state.draft`، `state.offline`، `state.stale`، `state.noData` و `state.noPermission`؛
- `insight.ai` با Label صریح و غیرقابل اشتباه با Fact.

هیچ معنا فقط با رنگ منتقل نمی‌شود؛ Label، Icon یا Shape نیز الزامی است.

## ۳. Typography و اعداد

- فونت فارسی Candidate: `Vazirmatn Variable` به‌صورت Self-hosted؛ انتخاب فونت نهایی هنوز باز است؛
- Fallback: `Tahoma, Segoe UI, sans-serif`؛
- وزن‌های Production: 400، 500، 600 و 700؛
- الزام مالک محصول در ۲۰۲۶-۰۹-۲۸: تغییر آیندهٔ فونت فارسی باید در تمام بخش‌های فعال
  از یک قرارداد مرکزی، نسخه‌دار و قابل بازگشت ممکن باشد؛ Login، Shell، Portfolio،
  Project، فرم و جدول، Chat، Reporting، نمایش موبایل و صفحهٔ Offline نباید
  font-family مستقل و پراکنده داشته باشند؛
- فونت گزارش PDF و Print از قرارداد نسخه‌دار خروجی استفاده می‌کند و با تغییر فونت
  باید Embedding، شکل‌گیری متن فارسی/اعداد، صفحه‌بندی و Goldenهای چاپ دوباره
  تأیید شوند. XLSX نام فونت را در Style ثبت می‌کند؛ نمایش همان قلم در نرم‌افزار
  گیرنده وابسته به نصب فونت روی دستگاه اوست؛
- تعویض فونت، متن ذخیره‌شده، دادهٔ Domain، تاریخ و مقدار Canonical را تغییر نمی‌دهد؛
- Headingها فشرده اما نه تزئینی؛ Body برای استفاده طولانی با Line-height باز؛
- عدد، درصد، ارز، واحد و تاریخ از Formatter مرکزی استفاده می‌کنند؛
- نمایش فارسی اعداد در UI و خروجی؛ مقدار Canonical در Domain تغییر نمی‌کند؛
- ستون‌های عددی و KPIها از Tabular figures استفاده می‌کنند.

## ۴. Geometry

- Grid پایه: ۴px؛
- Spacing رسمی: 4، 8، 12، 16، 20، 24، 32 و 40؛
- Radius رسمی: 8، 12، 16 و 20؛
- Border پیش‌فرض 1px و کم‌کنتراست؛
- Shadow فقط برای Layer، Popover و Surface مهم؛
- Glass/blur تزئینی و Shadow سنگین در Moduleهای پرتکرار ممنوع است.

## ۵. Motion Contract

| نوع | مدت Candidate | سیاست |
| --- | --- | --- |
| Hover/press | 120–160ms | فقط بازخورد مستقیم |
| Panel/state | 180–240ms | بدون Bounce |
| Login wireframe | حداکثر 2200ms | یک‌بار هنگام ورود؛ Non-blocking |
| Login glow | حداکثر یک Pass | Loop دائمی ممنوع |
| Skeleton | حداقل و آرام | بدون Flash |

در `prefers-reduced-motion: reduce` تمام Motionهای غیرضروری خاموش می‌شوند.

## ۶. Component Contract

### Foundation

- AppShell، Sidebar، Topbar و ProjectContext؛
- Button، IconButton، Link و Focus Ring؛
- TextField، Select، Textarea، PersianDateInput و FileInput؛
- Card، Section، Divider، Badge و StatusLabel؛
- Table، FilterBar، Pagination و Mobile row fallback؛
- Modal، Drawer، Popover، Toast و ConfirmDialog؛
- Empty، Loading، Skeleton، Error، Offline، Stale و NoPermission.

### V1.1

- BrandLockup و LoginComposition؛
- Avatar، AvatarFallback، ProfilePhotoCrop و PrivacyLabel؛
- ProjectDuplicationStepper، TransferSelection، ConflictResolution و PreviewSummary؛
- Attachment، Evidence و Collaboration primitives؛
- Report layout و Print primitives؛
- Executive Intelligence workspace؛ Agent هرگز به Chat box ساده تقلیل داده نمی‌شود.

هر Component باید Default، Hover، Focus-visible، Pressed، Disabled، Loading، Error و Offline state مرتبط خود را تعریف کند.

قرارداد `PMCS-UX-PRINT-001` چاپ مرورگر محدود MS43 را از خروجی رسمی
Reporting جدا می‌کند. برگهٔ نمونه فقط Snapshot مجاز یا نبود آن، منبع و
هشدار تازگی را نشان می‌دهد؛ Navigation/فرم چاپ نمی‌شوند. A4/Golden و فونت
تازه هنوز در `VX-G5` Qualified نشده‌اند.

در UX2-MS42، Portfolio هنگام دریافت اولیهٔ داده، Skeleton خنثای Card/Panel
با `aria-hidden` نشان می‌دهد و پیام زندهٔ Loading را حفظ می‌کند. Placeholder
عدد یا وضعیت ساختگی ندارد؛ در خطا حذف می‌شود تا پیام واقعی و دکمهٔ تلاش
دوباره دیده شوند. این نمونه، قرارداد Loading سایر Componentها را Qualified
نمی‌کند.

در UX2-MS40، `PmcsFileInput` برای مدیریت ظاهر Login به‌عنوان نخستین مصرف
مشترک FileInput افزوده شد: Input بومی همچنان فایل و محدودیت `accept` را
نگه می‌دارد، در حالی که انتخاب/نام فایل/حذف انتخاب و Focus در UI فارسی
نمایش داده می‌شود. Captureهای 18/44 و Run 402 شواهد همین سطح‌اند؛ مهاجرت
سایر ورودی‌های فایل و Qualification سراسری Component هنوز باز است.

در UX2-MS44، `PMCS-UX-PROTOTYPE-REVIEW-001` یک نمونهٔ مستقل برای بازبینی
۱۰ سناریو و ۷ State مرتبط، Desktop/Tablet/Mobile، و قلم Web/Print می‌دهد.
دو قلم خودمیزبان فقط در Prototype قرار دارند؛ این مشاهده انتخاب نهایی،
مهاجرت Routeهای فعال یا تأیید بصری مالک نیست. شرط `VX-G3` تکمیل Contract
همهٔ Stateهای لازم، نقد Prototype و تصمیم قلم است؛ `VX-G4/G5` جدا می‌مانند.

در UX2-MS46، `PMCS-UX-COMPONENT-STATES-001` ماتریس پوشش واقعی و شکاف
Componentهای مشترک را از روی Source و Captureهای فعال ثبت می‌کند. Hover
دکمهٔ غیرفعال در Preview مسدود دیگر نباید سیگنال رنگ Action فعال بدهد؛
قاب 45 و آزمون Browser شرط Evidence این اصلاح محدودند. این ماتریس
همهٔ Stateها را Qualified یا `VX-G3` را بسته اعلام نمی‌کند.

## ۷. Login Experience Contract

ظاهر Login از Authentication جدا می‌ماند. Descriptor فقط Schema-validated است و اجازه HTML/CSS/JavaScript دلخواه ندارد:

```json
{
  "schemaVersion": "1.0",
  "experienceVersion": "management-excellence-1",
  "composition": "architectural-split",
  "brandAssetId": "official-bbq-mark",
  "backgroundAssetId": null,
  "motionPolicy": "wireframe-once",
  "surfaceTone": "warm-ivory",
  "active": true
}
```

الزامات:

- Preview قبل از Publish؛
- Publish اتمیک؛
- Rollback به نسخه قبلی؛
- Fallback داخلی در خرابی Asset؛
- محدودیت حجم/نوع فایل و Malware scan؛
- Authentication، Redirect و BFF تحت تأثیر Descriptor قرار نمی‌گیرند.

## ۸. Responsive Contract

- Desktop: Sidebar کامل و Data-dense؛
- Tablet: Sidebar فشرده و دو ستون کنترل‌شده؛
- Mobile: Navigation جایگزین، یک ستون و جدول با fallback صریح؛
- در MS41، شش Shell فعال زیر Navigation افقی موبایل راهنمای فارسی پیمایش
  لمسی و حرکت با کلید تب دارند. این affordance، مهاجرت کامل Navigation
  جایگزین و آزمون‌های جامع Responsive را نمی‌بندد؛
- هیچ Action اصلی فقط با Hover قابل دسترسی نیست؛
- Targetهای لمسی حداقل 44px؛
- Zoom متن و عرض 320px نباید باعث از دست رفتن Action شود.

## ۹. Accessibility و Qualification

- WCAG AA برای متن و کنترل‌های اصلی؛
- Focus-visible سراسری و قابل مشاهده؛
- ترتیب Tab طبیعی و بدون Trap؛
- Label، Description و Error رابطه معنایی دارند؛
- Status، Chart و Icon فقط به رنگ متکی نیستند؛
- Screen reader announcement برای Sync، Error و نتیجه Action؛
- Visual regression روی Desktop/Tablet/Mobile؛
- Print golden test و Performance budget؛
- Gate فونت: جایگزینی آزمایشی یک خانوادهٔ فارسی از تنظیم مرکزی باید در تمام مسیرهای
  فعال، Offline و PDF/Print با Regression دسکتاپ/تبلت/موبایل، RTL، اعداد و
  نبود clipping پاس شود؛ هر استثنای فنی مستند و پیش از `VX-G5` بسته شود؛
- UX-G3 فقط بعد از تأیید بصری مالک محصول بسته می‌شود.
