# PMCS V1.1 — Design System Contract

- شناسه: `PMCS-DS-001`
- نسخه Candidate: `1.0.0-rc.1`
- مسیر بصری: `مدیریت ممتاز`
- وضعیت: `Awaiting Owner Visual Review`
- Runtime change: ندارد

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

- فونت فارسی Candidate: `Vazirmatn Variable` به‌صورت Self-hosted؛
- Fallback: `Tahoma, Segoe UI, sans-serif`؛
- وزن‌های Production: 400، 500، 600 و 700؛
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
- UX-G3 فقط بعد از تأیید بصری مالک محصول بسته می‌شود.

