# Roadmap حاکم تکامل محصول PMCS پس از V1

- شناسه سند: `PMCS-RM-POST-V1-001`
- نسخه سند: `1.168.0`
- وضعیت: `V1.1 Development`؛ F01–F10 و COL1 متصل، UX2 فعال، INT1/QA1 باز
- تاریخ ثبت: ۱۴۰۵/۰۷/۰۷ (۲۰۲۶-۰۹-۲۹)
- مرجع پیشین: `docs/roadmaps/pmcs-v1-development-and-qualification.md`
- Baseline منبع V1: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- وضعیت V1: `Qualified | Final | Baseline Locked`
- خط توسعه فعال بعدی: `PMCS V1.1`
- شاخه توسعه: `v1.1-development`
- Repository Start Commit: `0389b52cbd3385bdcc9f0e2a94411800389ae2fc`
- مرحله فعال: `V1.1-UX2 — Product UI Implementation and Migration`

## ۱. هدف و قاعده حاکم

این سند ادامهٔ رسمی Roadmap قفل‌شدهٔ V1 است و سند تاریخی V1 را بازنویسی نمی‌کند. هدف آن تبدیل PMCS از یک V1 عملیاتی و Qualified به یک بستر توسعه‌پذیر، حرفه‌ای از نظر تجربهٔ کاربری، دارای همکاری پروژه‌ای و دارای مرکز گزارش‌سازی قابل استناد است؛ بدون شکستن مرزهای معماری، Permission، Audit، Offline و دادهٔ رسمی V1.

هیچ قابلیت جدیدی مجاز نیست مستقیماً به جدول‌های ماژول دیگر متصل شود، Permission کاربر را دور بزند، Chat را به منبع حقیقت رسمی تبدیل کند یا محاسبات قطعی را به LLM بسپارد.

قواعد Version و Baseline این Roadmap در `docs/governance/pmcs-version-and-baseline-policy.md` لازم‌الاجرا است.

## ۲. وضعیت مبنا و مرز عدم تغییر

| مورد | وضعیت قطعی |
| --- | --- |
| PMCS V1 | Qualified، Final و Locked |
| Baseline منبع V1 | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` |
| تغییر مستقیم V1 برای قابلیت جدید | ممنوع |
| تعمیر V1 | فقط Patch نسخه‌دار از Baseline قفل‌شده و با Full Regression |
| خط قابلیت جدید | V1.1 و نسخه‌های بعدی |
| معماری اصلی | Modular Monolith، Permission مرکزی، Audit/Outbox/Idempotency، Offline-first |
| مرز Agent | `Agent → Permission-aware Tool → Application Service → Business Rules → Database` |
| تاریخ کاربر | شمسی، ارقام فارسی و منطقه زمانی پروژه؛ داخل سیستم ISO/UTC |

Commit شروع Repository برای شاخهٔ V1.1 باید هنگام ایجاد شاخه به‌صورت جداگانه ثبت شود و نباید با عبارت‌هایی مانند «آخرین main» یا «نسخه فعلی» جایگزین شود. Runtime Parent آن همواره Baseline منبع V1 بالا است.

## ۳. تصمیم‌های قطعی Post-V1

| شناسه | تصمیم | وضعیت |
| --- | --- | --- |
| `D-PV1-01` | قبل از ماژول‌های بزرگ آینده، Extensibility Foundation ساخته می‌شود. | مصوب |
| `D-PV1-02` | Visual Excellence Program، Design System و نمونهٔ تصویری/کلیک‌پذیر قبل از پیاده‌سازی UI جدید تصویب می‌شوند. | مصوب |
| `D-PV1-03` | برای هر پروژه یک فضای گفت‌وگوی گروهی کنترل‌شده ساخته می‌شود. | مصوب |
| `D-PV1-04` | پیام خصوصی، تماس صوتی/تصویری، Story و شبکهٔ اجتماعی عمومی ساخته نمی‌شوند. | خارج از Scope دائمی فعلی |
| `D-PV1-05` | Chat منبع حقیقت رسمی نیست؛ تبدیل به رکورد رسمی فقط با Command صریح و Permission انجام می‌شود. | مصوب |
| `D-PV1-06` | مرکز گزارش‌سازی و چاپ حرفه‌ای یک Bounded Context مستقل خواهد بود. | مصوب |
| `D-PV1-07` | V1.1 با گزارش‌های استاندارد و تأییدشده شروع می‌شود؛ Report Designer آزاد به V1.2 موکول است. | مصوب |
| `D-PV1-08` | Agent فقط از Tool Registry مجوزدار استفاده می‌کند؛ SQL/DB مستقیم ممنوع می‌ماند. | مصوب |
| `D-PV1-09` | PMO، PMBOK، توجیه اقتصادی، متره/برآورد و Scheduling/MSP ماژول‌های مستقل V2.x هستند. | مصوب |
| `D-PV1-10` | نبود داده، Baseline، WBS، Budget یا ماژول اختیاری با صفر یا وضعیت سبز جایگزین نمی‌شود. | پابرجا |
| `D-PV1-11` | Agent مدیریتی دقیقاً هفت Stage مستقل و دارای Gate دارد و نباید به یک عنوان کلی یا پنج فاز فشرده شود. | مصوب و بازیابی‌شده |
| `D-PV1-12` | مسیر بصری «مدیریت ممتاز» با لوگوی رسمی بتن بسپار قزوین، Surface گرم و Accent کنترل‌شدهٔ سرمه‌ای/سبز/نقره‌ای مبنای V1.1 است؛ مالک محصول در ۲۰۲۶-۰۹-۲۸ فایل کامل و برش شفاف نماد موجود را برای مبنای فعلی تأیید کرد. | مصوب؛ Visual Qualification باز |
| `D-PV1-13` | طرح گرافیکی Login از جریان Authentication جدا و با Descriptor/Asset نسخه‌دار، امن و قابل Rollback تغییرپذیر است؛ HTML/CSS/JS دلخواه قابل بارگذاری نیست. | مصوب |
| `D-PV1-14` | هر عضو سامانه یک پروفایل شخصی حداقلی و یکپارچه دارد و می‌تواند تصویر خود را مدیریت کند؛ عضویت و نقش پروژه جدا از پروفایل شخصی باقی می‌ماند. | مصوب |
| `D-PV1-15` | ایجاد پروژه از روی پروژهٔ موجود با Preview و انتخاب اقلام Setup/Member مجاز است؛ دادهٔ عملیاتی، مالی، پیام، فایل، Audit و سابقه هرگز ضمنی کپی نمی‌شود. | مصوب |
| `D-PV1-16` | هر ده خانوادهٔ استاندارد کاتالوگ RPT1 در Scope باقی می‌مانند؛ خانواده‌های ۲ تا ۱۰ باید با Micro-Slice و Qualification مستقل تکمیل شوند و RPT1 پیش از آن بسته نمی‌شود. | مصوب؛ ADR 0031 |
| `D-PV1-17` | فونت فارسی باید در آینده برای تمام صفحات فعال، Login، Offline و PDF/Print از قرارداد مرکزی نسخه‌دار قابل تعویض باشد؛ انتخاب قلم نهایی باز است و Gate تعویض سراسری/Visual QA در UX2 الزامی است. | مصوب؛ `PMCS-DS-001` |

## ۴. نقشهٔ نسخه‌های محصول

| خط نسخه | هدف | قابلیت‌های اصلی | خروجی نهایی |
| --- | --- | --- | --- |
| `V1.0.x` | نگهداری Baseline V1 | فقط Security/Critical Defect؛ بدون قابلیت جدید | Patch Qualified مستقل |
| `V1.1` | بنیاد توسعه‌پذیری و تجربهٔ محصول | Extensibility، Visual Excellence، Login قابل پیکربندی، پروفایل شخصی، Project Bootstrap، Documents، Collaboration، Reporting Phase 1 و Agent Stage 1 | Baseline جدید Qualified |
| `V1.2` | بلوغ Intelligence، گزارش و همکاری | Agent Stages 2–7، Advanced Report Builder، Scheduled Reports و Topic Channels | Baseline جدید Qualified |
| `V2.x` | توسعهٔ دامنه‌های راهبردی | PMO، PMBOK، Feasibility، Quantify، Scheduling/MSP و Agentهای تخصصی | Baseline مستقل هر Increment |

نسخهٔ V2 یک بستهٔ یک‌مرحله‌ای عظیم نیست. هر قابلیت راهبردی باید Increment و Baseline مستقل خود را داشته باشد تا ریسک و Regression کنترل شود.

## ۵. Roadmap اجرایی PMCS V1.1

### `V1.1-G0` — Governance و Development Baseline

**هدف:** شروع نسخه از مرجع دقیق و جلوگیری از توسعهٔ مبهم.

خروجی‌های لازم:

- ثبت Parent Runtime Baseline و Repository Start Commit؛
- ثبت Scope، Non-Scope، Decision Register و Risk Register؛
- ADRهای Extensibility، Documents، Collaboration، Reporting و UX؛
- Permission Catalog اولیهٔ قابلیت‌های جدید؛
- API/Event/Migration strategy؛
- Test Strategy و Qualification Contract نسخهٔ V1.1؛
- ایجاد Checkpoint Manifest بدون ادعای Feature Complete یا Qualified.

**Gate خروج:** `Governance Approved`؛ بدون این Gate هیچ کد محصولی V1.1 آغاز نمی‌شود.

**Evidence:** Gate با Governance source commit `d4ac64ea818c7e48476b650b84dafe31bf1872a4` و Run 71 (`35275795712`) بسته شد. Main/Runtime V1 تغییر نکرده است.

### `V1.1-UX1` — Visual Excellence، Design System و High-Fidelity Product Prototype

**هدف:** بازطراحی بنیادین تجربهٔ بصری در سطح یک محصول سازمانی ممتاز، نه اجرای یک Skin یا Polish محدود.

مرجع لازم‌الاجرا: `docs/roadmaps/pmcs-visual-excellence-program.md`.

Scope:

- Design Tokenهای رنگ، فاصله، Radius، Elevation، Motion و State؛
- Typography فارسی، سلسله‌مراتب اطلاعات و قواعد RTL؛
- Shell، Navigation، Header، Project Context و Command Surfaces؛
- جدول، فرم، فیلتر، نمودار، Timeline، Attachment و Workflow components؛
- Empty، Loading، Error، Offline، No Permission و No Data؛
- Responsive برای Desktop، Tablet و Mobile؛
- Print Design برای A4/A3، عمودی و افقی؛
- نمونهٔ High-Fidelity داشبورد، Chat پروژه و Reporting Center؛
- نمونهٔ High-Fidelity برای Executive Intelligence Center در هماهنگی با Stage 4 Agent؛
- نمونهٔ High-Fidelity پروفایل شخصی، ویرایش Avatar و حالت بدون تصویر؛
- نمونهٔ High-Fidelity Wizard ساخت پروژه از روی پروژهٔ موجود شامل Preview، Conflict و Confirmation؛
- بستهٔ بازبینی شامل تصاویر مسیرهای اصلی و Prototype قابل کلیک تا مالک محصول بدون زیرساخت اجرایی بتواند تجربه را ارزیابی کند.

**موج مهاجرت UI:**

1. Login، Shell، Portfolio و Project Command Center؛
2. My Work، Notification، فرم‌ها، جدول‌ها و Workflowهای مشترک؛
3. صفحات ماژول‌های V1؛
4. Collaboration و Reporting جدید.

**Gate خروج:** `Visual Direction Approved`. مسیر «مدیریت ممتاز» از نظر ترکیب Login، گرمی محیط و شدت Motion توسط مالک محصول تصویب شده است. این تصمیم فقط Gate انتخاب Art Direction را می‌بندد؛ `VX-G3 System Ready`، Prototype تمام stateها و Design System هنوز باید پیش از مهاجرت تولیدی کامل شوند.

**Evidence:** مالک محصول Candidate `PMCS-V1.1-UX1-RC1` را در ۲۰۲۶-۰۹-۱۷ برای سه معیار قفل‌شده تأیید کرد. موشن، تصویر و تم Login باید در IAM1 از Authentication جدا، نسخه‌دار و دارای Preview/Publish/Rollback/Fallback باشند.

### `V1.1-EXT1` — Extensibility Foundation

**هدف:** اتصال قابلیت‌های آینده از طریق Contract، نه تغییرات موردی.

Scope:

- `ModuleDescriptor` شامل شناسه، نسخه، Capability و dependency مجاز؛
- `PermissionManifest` ماژول‌محور با تصمیم نهایی در Permission Engine مرکزی؛
- `NavigationManifest` و Feature Flag؛
- `ToolManifest` برای ابزارهای Agent با Input/Output schema، Permission و Risk Class؛
- Integration Eventهای نسخه‌دار روی Outbox؛
- Application Contractهای پایدار برای خواندن بین‌ماژولی؛
- Compatibility metadata و migration ownership؛
- Architecture Gate برای منع دسترسی مستقیم به Persistence ماژول دیگر؛
- Contract test برای Module، Permission، Event، Navigation و Agent Tool.

**Non-Scope:** بارگذاری Dynamic و ناامن Binary/Plugin شخص ثالث در Runtime. V1.1 یک Platform Contract می‌سازد، نه Marketplace افزونه.

**Gate خروج:** یک Reference Module باید از Manifest تا UI Navigation، Permission، Event و Tool Contract به‌طور کامل در تست اثبات شود.

**Evidence:** Reference Module `platform.foundation` با Manifest schema نسخه‌دار، Permission مرکزی، Navigation fail-closed، Event v1 و Tool read-only در Candidate source commit `b61a644de91ec3a0cf4a6288e54185e174794e1f` اثبات شد. هر هشت Job در Run 77 (`35283742706`) سبز شدند؛ ۲۶۴ تست C#، ۱۲۸ تست Web، ۴ سناریوی مرورگر واقعی و Restore Drill دارای ۳۸ Migration پاس شدند. EXT1 بسته است، اما V1.1 هنوز Feature Complete، Qualified یا Locked نیست.

### `V1.1-DOC1` — Shared Document and Attachment Foundation

**هدف:** جداسازی فایل عمومی محصول از Evidence تخصصی گزارش روزانه.

Scope:

- Document/Attachment عمومی با `ownerType/ownerId` کنترل‌شده؛
- Tenant/Project boundary، Classification و Permission مستقل؛
- Upload Session، Object Storage خصوصی، SHA-256 و Content Signature؛
- محدودیت نوع/حجم، Malware Scanner Adapter، Quarantine و Release State؛
- نسخه، Retention، Legal Hold، Audit و دانلود Permission-aware؛
- Idempotency، Retry و Offline upload queue؛
- قرارداد اتصال به Chat، Reporting، Technical Office و ماژول‌های آینده.

رکوردهای Evidence موجود بدون Migration پنهان و بدون تغییر معنا حفظ می‌شوند. تبدیل یا اشتراک فایل میان Contextها فقط با Command صریح انجام می‌شود.

**Gate خروج:** تست Upload/Download/Retry/Duplicate/Permission/Quarantine/Audit و جداسازی Tenant/Project.

**Evidence:** ماژول مستقل `documents.shared`، Migration شماره ۳۹، Object Storage خصوصی، Offline upload queue و مرزهای Permission/Quarantine در Candidate source commit `3fb9f3cb9d14cef5ecbc3de1a3f1f266e88e0e11` اثبات شد. هر هشت Job در Run 83 (`35338895848`) سبز شدند؛ ۲۷۴ تست C#، ۱۳۰ تست Web، ۴ سناریوی مرورگر واقعی و Restore Drill دارای ۳۹ Migration پاس شدند. DOC1 بسته است، اما V1.1 هنوز Feature Complete، Qualified یا Locked نیست.

### `V1.1-IAM1` — Configurable Login and Minimal Member Profile

**هدف:** جداکردن هویت بصری Login از جریان امنیتی و فراهم‌کردن پروفایل شخصی حداقلی برای تمام اعضای سامانه.

Scope:

- `LoginExperienceDescriptor` نسخه‌دار شامل Design token، Asset slot، Composition variant، Motion policy، Active version و Rollback؛
- مدیریت Asset فقط برای مدیر دارای Permission مشخص، با همان Upload/Validation/Quarantine pipeline بنیاد اسناد؛
- Fallback داخلی سالم در صورت حذف، خرابی یا ناسازگاری Asset؛
- منع کامل HTML/CSS/JavaScript دلخواه و منع تغییر OIDC/BFF/Authentication contract از مسیر تنظیمات گرافیکی؛
- یک `MemberProfile` به‌ازای هر حساب Tenant شامل نام نمایشی، تصویر، عنوان شغلی، واحد سازمانی و اطلاعات تماس سازمانی مجاز؛
- تصویر پیش‌فرض، Crop و Thumbnailهای کنترل‌شده، محدودیت نوع/حجم، حذف/جایگزینی، Cache invalidation و Audit؛
- Self-service برای ویرایش فیلدهای مجاز و Permission مستقل برای اصلاح Directory fields توسط مدیر؛
- نمایش یک Profile واحد در تمام پروژه‌ها، در حالی که Project Membership، Role و Access Scope همچنان پروژه‌محور و مستقل هستند؛
- Privacy contract برای اینکه هیچ دادهٔ شخصی خارج از Tenant/Project permission نمایش داده نشود.

**Non-Scope:** شبکهٔ اجتماعی شخصی، Follow، Status، پیام خصوصی، رزومهٔ گسترده، Gallery و پروفایل عمومی اینترنتی.

**Gate خروج:** تست Authentication isolation، Descriptor version/rollback، asset failure fallback، self/admin permission، image validation، privacy و نمایش سازگار Profile در چند پروژه.


**Evidence:** پروفایل یکپارچه عضو، Avatar خصوصی، Login Descriptor نسخه‌دار و مسیر بصری «مدیریت ممتاز» در Candidate source commit `86f9f4efd086e4e67823a930fcb3ef1b7249b71f` اثبات شد. هر هشت Job در Run 88 (`35345558791`) سبز شدند؛ ۲۷۹ تست C#، ۱۳۷ تست Web، ۵ سناریوی مرورگر واقعی و Restore Drill دارای ۴۰ Migration پاس شدند. IAM1 بسته است، اما V1.1 هنوز Feature Complete، Qualified یا Locked نیست.

### `V1.1-PRJ1` — Controlled Project Bootstrap and Duplication

**هدف:** ساخت سریع پروژهٔ جدید از روی Setup یک پروژهٔ موجود، بدون تکثیر دادهٔ عملیاتی یا ایجاد دسترسی پنهان.

قابلیت در UI با عنوان «ساخت از روی پروژهٔ موجود» ارائه می‌شود و دارای Wizard انتخابی است. کاربر باید پیش از اجرا Preview دقیق Added/Skipped/Conflict را ببیند.

اقلام قابل انتخاب برای انتقال:

- تنظیمات پایه و Module enablementهای سازگار؛
- Calendar، Location structure، Role template، Workflow/Form/Report template و Lookupهای صراحتاً Cloneable؛
- اعضای فعال پروژه به‌صورت Reference به حساب موجود، همراه Role و Access Scope انتخاب‌شده؛
- تنظیمات اعلان و گروه پروژه فقط به‌صورت Default جدید، نه کپی سابقه.

مواردی که هرگز به‌صورت ضمنی کپی نمی‌شوند:

- شناسه، کد، نام، تاریخ‌ها، قرارداد و مقادیر یکتای پروژهٔ مبدأ؛
- Daily Report، Progress actual، Finance، Payment، Voucher، Budget actual، Procurement transaction و Contract history؛
- پیام، فایل، Attachment، Technical document، RFI، Approval، Issue، Action، Notification و Agent conversation؛
- Audit، Outbox، Idempotency key، Offline queue، Sync state، Project State snapshot و هر سابقهٔ عملیاتی.

کنترل‌های الزامی:

- فقط داخل یک Tenant و با Permissionهای مستقل `projects.bootstrap.create` و `projects.bootstrap.members_copy`؛
- حساب کاربر Duplicate نمی‌شود؛ فقط Membership جدید به Identity موجود ساخته می‌شود؛
- عضو غیرفعال، تعلیق‌شده یا ناسازگار در Preview با دلیل `Skipped/Blocked` نمایش داده می‌شود؛
- اجرای Idempotent با Correlation ID، Audit، Dry-run، conflict policy و نتیجهٔ قابل دانلود؛
- پروژهٔ مقصد تا پایان Validation در وضعیت Draft باقی می‌ماند و Activation یک Command مستقل است؛
- ارسال اعلان عضویت فقط پس از Confirmation نهایی و مطابق Policy پروژهٔ مقصد؛
- هر Template/Module باید صریحاً Clone contract و Version compatibility خود را اعلام کند.

**Gate خروج:** تست Preview/execute parity، permission و cross-tenant denial، inactive-member handling، idempotency، partial failure recovery، audit، عدم کپی دادهٔ عملیاتی و Activation مستقل.

**Evidence:** Plan/Digest/Expiry، یازده Contributor نسخه‌دار، Membership reference-only، denylist دادهٔ عملیاتی، Wizard فارسی و Activation مستقل در Candidate source commit `e1b5bf6af813af7324065edc1c91eecf2391eccd` اثبات شد. هر هشت Job در Run 92 (`35355855215`) سبز شدند؛ ۲۸۴ تست C#، ۱۳۹ تست Web، پنج سناریوی مرورگر واقعی، connected bootstrap regression و Restore Drill دارای ۴۱ Migration پاس شدند. PRJ1 بسته است، اما V1.1 هنوز Feature Complete، Qualified یا Locked نیست.

### `V1.1-RPT1` — Reporting Center Phase 1

**هدف:** تولید گزارش‌های رسمی، نسخه‌دار، قابل چاپ و قابل ممیزی.

Scope معماری:

- Reporting bounded context مستقل؛
- Semantic Read Model فقط از دادهٔ مجاز و رسمی؛
- Report Catalog و Template Versioning؛
- Job غیرهم‌زمان با Status، Retry و Diagnostics؛
- Data Cut-off/As-of، پارامترها و Permission snapshot؛
- خروجی immutable با Hash، Audit، Retention و Archive؛
- PDF و Excel؛ CSV فقط برای دادهٔ جدولی مجاز؛
- RTL، تاریخ شمسی، ارقام فارسی و Time Zone پروژه؛
- Header/Footer، لوگو، شماره صفحه، Revision، Watermark، امضا و QR/Verification Code؛
- A4/A3، Portrait/Landscape، تکرار Header جدول و Page-break کنترل‌شده؛
- NoData/NotConfigured/InsufficientData صریح و بدون صفر ساختگی.

کاتالوگ استاندارد اولیه:

1. گزارش روزانه رسمی و زنجیرهٔ اصلاحات؛
2. گزارش هفتگی و ماهانهٔ پروژه؛
3. گزارش مدیریتی/Executive Project State؛
4. پیشرفت، Planned/Actual/Variance و S-Curve در صورت وجود Baseline معتبر؛
5. مالی، Cash Position، تعهدات، Aging و بودجه در صورت پیکربندی؛
6. قرارداد، اصلاحیه، خرید و تأمین؛
7. دفتر فنی شامل Document/RFI/Submittal/Transmittal؛
8. Quality و HSE با رعایت Classification؛
9. Issue، Risk، Decision، Escalation و Action؛
10. Portfolio Summary به تفکیک دسترسی و ارز، بدون تبدیل پنهان.

**Non-Scope V1.1:** Designer آزاد Drag-and-Drop، Query مستقیم کاربر به Database و تولید عدد توسط LLM.

**Gate خروج:** Golden-file و visual print tests، determinism، Permission، Snapshot/Audit، Jalali/RTL و بازتولید خروجی از Template Version یکسان.

**Definition of Ready:** بسته `PMCS-V1.1-RPT1-DOR1` روی Parent commit
`720de8869e251f5a4c39a6940a76e9929232706b` ثبت شد. ADR 0029، معماری Semantic Snapshot،
API، Permission/Threat contract، Test Matrix و Runbook آماده‌اند. این Evidence فقط آغاز
پیاده‌سازی RPT1 را مجاز می‌کند و هیچ Runtime/Migration یا Gate خروج RPT1 را کامل اعلام نمی‌کند.

**Implementation Slice 01:** Source Candidate با commit
`43cac1b83ac7764fe6005fee108029597091a238` و tree
`257d8c80f45435e462563e38bb3c5fa12c77808b` ثبت شد. Module/Descriptor، Migration 42،
Catalog/Create/Get/List، Source contract زنجیره گزارش روزانه، canonical Snapshot، Worker claim،
Permission re-evaluation، Role mapping، read-only Agent manifests/application service و جلوگیری از
generic Documents access برای `ReportOutput` پیاده شده‌اند. Feature flag پیش‌فرض خاموش است و این
Candidate هنوز Build/CI/connected integration، Rendererهای PDF/XLSX، Download/Verify، Golden،
Restore و Full Regression ندارد؛ در نتیجه RPT1 همچنان فعال و باز است.

**Implementation Slice 02:** Source Candidate با commit
`ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173` و tree
`4deccade8899a2438485fb1304cd918115af2654` ثبت شد. Generated Document publish/read با
read-after-write integrity، Rendererهای PDF/XLSX، RTL/Jalali/Persian formatting، stable
output/document identity، Worker rendering/finalization، Retry/Cancel، Download/Verify، integrity
Audit، rollback-aware output access، Migration 43 و هارنس PostgreSQL/Object Storage در Source
پیاده شده‌اند. رگرسیون محلی ابزارها `40/40` و Web `139/139` به‌همراه Web check، architecture
validator و system contract audit سبز است. Run 99 (`35381177208`) هر هشت Job، `295/295` تست C#،
هارنس Reporting با `13/13` assertion متصل و Restore Drill ۴۳ Migration را روی همان tree پاس کرد.
این Candidate هنوز PDF license/golden، crash/concurrency/load، revocation/tamper گسترده،
observability و UI اختصاصی Reporting را ندارد؛ Feature/Worker/OutputAccess پیش‌فرض خاموش‌اند و
RPT1 بسته نشده است. نقطهٔ ادامه تکمیل Gateهای Recovery/Security/Golden/Observability است، نه شروع
معماری جدید.

**Qualification Slice 03:** Candidate با commit
`b4da1e951debf76e1ba3b398bde2ccf60fbde5de` و tree
`aa4063214ad1dea8fac19685a81818623296c24c` در Run 102 (`35383686315`) هر هشت Job را پاس کرد.
Cancel با Worker خاموش، replay/final-state، denial ناشناس و cross-tenant، منع generic Documents،
revocation پس از success، metadata tamper fail-closed/restore و Audit/Outbox/Idempotency متصل اثبات
شدند. این Slice API/معماری Runtime را تغییر نداد و revocation حین Worker، دو Worker/crash window،
object-byte tamper، load، observability، Golden و UI Reporting را باز نگه می‌دارد.

**Qualification Slice 04:** Candidate با commit
`e1ac3263df53a245b1aefb338a015be4854d367b` و tree
`f58881f7e0a77bf89f65b872d4f988bd154a809f` در Run 104 (`35390054888`) هر هشت Job را پاس کرد.
دو Worker واقعی، `SKIP LOCKED`، rollback claim پس از `SIGKILL`، stale lease و
crash-before/after-storage با reuse سند پایدار و بدون side effect تکراری متصل اثبات شدند. pauseهای
Qualification فقط در QA Gateway ایزوله و default-off هستند. این Slice معماری/API/Migration را
تغییر نداد و revocation حین Worker، object tamper/orphan inventory، load/observability، Golden،
PDF قانونی و UI Reporting را باز نگه می‌دارد.

**Qualification Slice 05:** Candidate با commit
`167133fc1985c5b57c3dac90535f7a962dfd03b7` و tree
`34fb70aee9a62a434a8446444d7c6d5c6c9819bd` در Run 108 (`35393509764`) هر هشت Job را پاس کرد.
Permissionهای Reporting/Source بلافاصله پیش از Storage دوباره ارزیابی و revocation حین Rendering
بدون انتشار Document/Output fail-closed شد. byte-tamper، missing و malformed object روی MinIO واقعی
برای Verify/Download fail-closed و سپس restore شد؛ inventory نیز orphan پنجرهٔ crash-after-storage و
صفرشدن آن پس از recovery را اثبات کرد. این Slice API خارجی/Migration/معماری را تغییر نداد و sweeper
تولیدی، retry/load/budget، observability، Golden، PDF قانونی و UI Reporting را باز نگه می‌دارد.

**Slice 06 Micro-Step 03 — Safe Checkpoint C1:** Candidate با commit
`83f13cf43679b23a6a169cc0912985b391b1c017` و tree
`1705d184bd494e80e50d8a85b723f0bc63e20abc` در Run 117 (`35441980440`) هر هشت Job را پاس کرد.
نه Meter instrument و چهار tag کم‌کاردینالیتی به قرارداد تست‌شده تبدیل شدند؛ readiness فقط چهار
مقدار عددی allowlist‌شدهٔ `reporting-worker` را منتشر می‌کند. fairness متصل `10/10`، وضعیت
`Degraded` صف aged و عدم نشت Tenant/Project/User/Run ID را اثبات کرد. این Safe Checkpoint میانی
MS03 است: exporter/scrape/alert rule و delivery در `S06-MS03-C2` بازند و ترتیب RPT1، COL1، UX2،
INT1 یا هفت Stage Agent تغییر نکرده است.

**Slice 06 Micro-Step 03 — Safe Checkpoint C2:** Candidate با commit
`9bb7ede9b89da2078e165cccb2927e0449116909` و tree
`a960cddb5264b3de8857812906b7595db0664ba5` در Run 120 (`35443563270`) هر هشت Job را پاس کرد.
exporter OTLP فقط با endpoint صریح فعال می‌شود؛ Collector/Prometheus/Alertmanager نسخه‌پین‌شده سه
rule queue-age/heartbeat/failure-retry را load کردند و alert queue-age واقعاً firing و به webhook
ایزوله تحویل شد. پنج assertion observability و کنترل عدم نشت identity پاس شدند. MS03 بسته است، اما
RPT1 برای remediation امن orphan، Golden معنایی/XLSX، PDF قانونی و UI Reporting باز می‌ماند؛
نقطهٔ بعدی `S06-MS04` است و ترتیب RPT1، COL1، UX2، INT1 یا هفت Stage Agent تغییر نکرده است.

**Slice 06 Micro-Step 04 — Safe Checkpoint:** Candidate با commit
`4ff44c96104ee1df87d267ca9a530d19b9248ba3` و tree
`d4c320e7917121f64a70dea1251169bef4b516ce` در Run 123 (`35445497353`) هر هشت Job را پاس کرد.
worker داخلی و default-off در `InventoryOnly` چهار Candidate را بدون side effect inventory کرد و در
`ApplyEligible` فقط orphan منقضی، بدون legal hold و بدون owner را حذف کرد. retention و legal hold
زیر row lock دوباره سنجیده شدند؛ Retry/remediation advisory lock مشترک، Audit یکتا و بدون object key
و sweep دوم idempotent بودند. هیچ API تجاری یا Migration اضافه نشد. MS04 بسته است، اما RPT1 برای
Golden معنایی/XLSX، تصمیم قانونی و Golden/Performance PDF و UI Reporting باز می‌ماند؛ ترتیب RPT1،
COL1، UX2، INT1 یا هفت Stage Agent تغییر نکرده است.

**Slice 06 Micro-Step 05 — Safe Checkpoint:** Candidate با commit
`38a03f33f4747d0b6a76696705877633acd17678` و tree
`eb9369c9e32eb3f523c4faa22487d6428c2d7e34` در Run 130 (`35449387794`) هر هشت Job را پاس کرد.
چهار Run twin روی زنجیرهٔ سه‌نسخه‌ای، cutoff پیش/پس از correction، Snapshot/manifest hash، replay
byte-identical، parser مستقل OpenXML و SQL مستقل را هر `13/13` assertion پاس کردند. projection
تاریخی metadata supersession آینده را پنهان کرد و Draft از Snapshot/XLSX حذف ماند. هیچ API تجاری
یا Migration اضافه نشد. MS05 بسته است، اما RPT1 برای تصمیم قانونی و Golden/Performance PDF و UI
Reporting باز می‌ماند؛ ترتیب RPT1، COL1، UX2، INT1 یا هفت Stage Agent تغییر نکرده است.

**Slice 06 Micro-Step 06 — Safe Checkpoint:** Candidate با commit
`b8f21492a4f44c7c412e5b7eda0b164e7f256758` و tree
`e94b6ba3753e67b42ea0ec99e998761fdad0bcc3` در Run 133 (`35463350892`) هر هشت Job را پاس کرد.
ADR 0030 تصمیم `QuestPDF Community` را ثبت کرد؛ package، build/runtime image و دو فونت DejaVu Sans
با digest دقیق pin شدند. PDF Golden متصل `8/8` assertion، رندر byte-identical، visual digest ثابت،
parse متن/RTL و performance budget را پاس کرد. defaultهای Production خاموش و license پیش‌فرض
`Unconfigured` ماندند. MS06 بسته است، اما RPT1 تا تعیین تکلیف رسمی اختلاف کاتالوگ ده‌گانه با Runtime
تک‌گزارش فعال می‌ماند؛ UI اختصاصی Reporting طبق برنامه در UX2 است و ترتیب RPT1، COL1، UX2، INT1 یا
هفت Stage Agent تغییر نکرده است.

**Catalog Completion Decision — Slice 07 Micro-Step 01 Safe Checkpoint:** مالک محصول در ADR 0031
گزینهٔ «حفظ هر ۱۰ خانواده» را صریحاً انتخاب کرد. Candidate با commit
`d81ecc00762145210e1c688f8f5843f46d62fc04` و tree
`5f40383ad506d94520c741eb69fcd00086283734` در Run 135 (`35466775368`) هر هشت Job را پاس کرد؛
`330/330` تست C#، `54/54` تست قراردادی Node، `139/139` تست Web و پنج browser scenario سبز شدند.
کاتالوگ اولیه RPT1 کاهش نیافت؛ `RPT1-F01` همان گزارش روزانه qualifyشده است و `RPT1-F02` تا
`RPT1-F10` به‌عنوان Required/Not Implemented با Micro-Sliceهای مستقل باقی می‌مانند. زیرساخت مشترک
یا placeholder هیچ خانواده‌ای را Done نمی‌کند. هیچ API، Migration، Runtime، feature flag، Baseline
یا ترتیب کلان Roadmap تغییر نکرد. نخستین Slice اجرایی بعدی DoR و قرارداد معنایی گزارش
هفتگی/ماهانه `RPT1-F02` است و RPT1 فعال می‌ماند.

**F02 Weekly/Monthly Semantic Contract — Slice 07 Micro-Step 02 Safe Checkpoint:** قرارداد
`PMCS-RPT1-F02-SEMANTIC-001 v1.0.0`، F02 را به roll-up دوره‌ای گزارش‌های روزانه رسمی همان پروژه
محدود می‌کند. Weekly بازهٔ شنبه تا شنبه بعد و Monthly بازهٔ روز اول تا روز اول ماه شمسی بعد را در
Time Zone pin‌شده پروژه دارد. پارامتر بسته، source lineage، cutoff correction-safe، coverage،
precedence وضعیت‌های `NotConfigured/NoData/InsufficientData/Available`، permission/classification
fail-closed و Golden matrix چهارده‌سناریویی تثبیت شده‌اند. Candidate
`b4a59fa966320a1da4b53759814224e21893c01e` با tree
`6b5b486dace3c07b0b4e0385413bf1add5aee7a3` در Run 137 (`35474388839`) هر هشت Job،
`330/330` C#، `56/56` contract، `139/139` Web، پنج browser scenario و Restore ۴۳ Migration را پاس
کرد. هیچ Runtime Definition، Template/schema ID، API، Migration، Source implementation، Renderer
یا feature flag ایجاد نشد؛ F02 اکنون `Contract Ready / Runtime Not Implemented` است و RPT1 فعال
می‌ماند.

**F02 Runtime Core Safe Checkpoint — Slice 07 Micro-Step 03:** قرارداد معنایی
`PMCS-RPT1-F02-SEMANTIC-001 v1.1.1` به identity داخلی `project-periodic-certified/1.0.0`،
schemaهای parameter/snapshot، Project configuration pin، period-read contract نسخه‌دار در
FieldOperations، resolver شنبه/ماه شمسی و semantic Snapshot builder نگاشت شد. Core، سه cadence،
چهار data status، reason allowlist، lineage/hash قطعی، aggregation بدون conversion و classification
propagation را با ۱۶ case جدید پوشش می‌دهد. Source commit
`6fc28cf54a6df820c49a2365eab76e3550ae421a` و tree
`5188dac79fe5187b319e6aa727da89163fa37c1b` در Run 139 هر هشت Job، `346/346` تست C#، `58/58`
تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۳ Migration را پاس کردند. Safe
Checkpoint آن `PMCS-V1.1-RPT1-S07-MS03-C1` است. API، Migration، Catalog/Template seed، Worker
dispatch، Renderer، UI و Production defaults دست‌نخورده‌اند؛ بنابراین F02 هنوز End-to-End Done
نیست و RPT1 فعال می‌ماند.

**F02 Renderer/Golden Safe Checkpoint — Slice 07 Micro-Step 04:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS03-C1` قراردادهای Template `1.0.0`، Renderer
`pmcs.reporting.project-periodic.renderer/v1` و Layout
`pmcs.reporting.project-periodic.layout/v1` اضافه شدند. parser/request hash و cutoff را fail-closed
تطبیق می‌دهند و یک model canonical، PDF دوصفحه‌ای RTL/Jalali و XLSX هشت-Sheet deterministic را
می‌سازد. Golden قطعی PDF/XLSX و دو visual digest، Monthly boundary، `NoData`، `NotConfigured`،
formula escaping، unit separation، budget و regression بدون تغییر F01 را پوشش می‌دهند. این
Slice عمداً هیچ Catalog/Template seed، API، Worker dispatch، DI registration، Migration، UI،
feature flag یا Production enablement ندارد. Source commit
`4f68f57de2c2a79b654a19128894d9c89878ab65` و tree
`f4b592c72ea65974c00b936ca59c0428eb47f981` در Run 141 هر هشت Job، `353/353` تست C#، `60/60`
تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۳ Migration را پاس کردند. Safe
Checkpoint آن `PMCS-V1.1-RPT1-S07-MS04-C1` است؛ wiring متصل F02/RPT1 همچنان باز می‌ماند.

**F02 Catalog/API/Worker Safe Checkpoint — Slice 07 Micro-Step 05:** روی Checkpoint
`PMCS-V1.1-RPT1-S07-MS04-C1`، Migration 44 Definition/Template رسمی F02 و Project profile pin‌شده را
اضافه کرد؛ API و Worker روی contractهای موجود dispatch می‌کنند و QA متصل PDF/XLSX، مجوز،
idempotency و database evidence را می‌سنجد. Source
`7fc55c167ad2159a31c895b32a52d78f47574df9` با tree
`d665fe4cdf29369f96ec0875bc6f1535db349d55` در Run 144 هر هشت Job، `355/355` تست C#، `61/61`
تست Node، `139/139` تست Web، پنج browser scenario، هارنس متصل `13/13` و Restore ۴۴ Migration را
پاس کرد. Safe Checkpoint آن `PMCS-V1.1-RPT1-S07-MS05-C1` است. UI و Production defaults عمداً
تغییر نکرده‌اند؛ گام بعد فقط DoR/semantic contract مستقل F03 است و F03 تا F10 باز می‌مانند.

**F03 Executive Project State Semantic Contract — Slice 07 Micro-Step 06 Safe Checkpoint:** روی Safe
Checkpoint `PMCS-V1.1-RPT1-S07-MS05-C1`، قرارداد `PMCS-RPT1-F03-SEMANTIC-001 v1.0.0` گزارش
مدیریتی را به Snapshot رسمی و immutable Project State محدود می‌کند. پارامتر Client فقط `{}` است؛
انتخاب Source تا cutoff و server-owned، trend چهارده‌تاریخی canonical و currency براساس Project
revision و آخرین Approved Source است. Operational Status با Coverage/Freshness/Confidence و
`dataStatus` یکی نمی‌شود، `isPartial` صریح می‌ماند و `Stable` سلامت کل پروژه نیست. Recalculate،
Composite Health، AI summary و join Finance/Commercial/F04 تا F10 ممنوع‌اند. Candidate
`e3218555a38f7ba460558e51b4db3f8bc17fcd9c` با tree
`1fe4cc804fdd078a71ff2633201c8c690447900e` در Run 146 (`35500809115`) هر هشت Job، `355/355`
C#، `63/63` contract، `139/139` Web، پنج browser scenario و Restore ۴۴ Migration را پاس کرد. این
Checkpoint فقط DoR و Golden matrix هفده‌سناریویی را می‌بندد؛ Runtime identity/source/builder،
Renderer، Catalog/API/Worker، UI و Production enablement هنوز پیاده نشده‌اند.

**F03 Bounded Runtime Core — Slice 07 Micro-Step 07 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS06-C1` فقط identity/schema نسخه‌دار، Project profile pin، Application
Contract خواندنی ProjectIntelligence، selector cutoff-aware و semantic Snapshot builder اضافه شده
است. Unit/contract testها cutoff/tie-break، trend چهارده‌تاریخی، چهار data status، currency، partial
scope، Attention ordering، Classification و determinism را پوشش می‌دهند. Source
`22d5b0f91edf8d733192fae0ba946c8538c63bca` با tree
`eb5ea4253a7b80e8ab3320b9ccea747624f89790` در Run 148 هر هشت Job، `377/377` تست C#، `64/64`
تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration را پاس کرد. Safe
Checkpoint آن `PMCS-V1.1-RPT1-S07-MS07-C1` است و هیچ Renderer، Catalog/API/Worker wiring،
Migration، UI یا Production enablement وارد این Micro-Step نشده است.

**F03 Renderer/Golden — Slice 07 Micro-Step 08 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS07-C1` فقط Template/Renderer/Layout identity، parser/request/model
fail-closed، PDF دوصفحه‌ای A4 فارسی و XLSX هشت‌Sheet قطعی اضافه شده است. Operational،
Coverage/Freshness/Confidence، partial scope، Attention و trend بدون Composite Health یا truncate
حفظ می‌شوند. Source `d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f` با tree
`58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434` در Run 154 هر هشت Job، `383/383` تست C#، `65/65`
تست Node، `139/139` تست Web، پنج browser scenario و Restore ۴۴ Migration را پاس کرد. Safe
Checkpoint آن `PMCS-V1.1-RPT1-S07-MS08-C1` است و هیچ Catalog/API/Worker wiring، Migration، DI
registration، UI یا Production enablement وارد این Micro-Step نشده است.

**F03 Catalog/API/Worker — Slice 07 Micro-Step 09 Connected Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS08-C1`، Migration forward شمارهٔ 45، Definition/Template قطعی F03، parser
سخت‌گیرانهٔ `{}`، Project profile pin و permission policy مستقل هر Definition اضافه شده است. Worker
فقط از `IProjectStateReportingSource` و Runtime/Rendererهای checkpointed استفاده می‌کند و
permission را پیش از Snapshot و Storage دوباره می‌سنجد؛ HTTP و سرویس read-only ابزارها نیز metadata
را per-definition فیلتر می‌کنند. TestHarness با Finance Manager دارای
`project-state.read` و فاقد `field.daily-reports.read`، جداسازی Catalog/Run و تولید/verify هر دو
خروجی را کنترل می‌کند. Source `40afeb37d7bf90e97a988cae141901e28d336516` با tree
`ae06285bf1a68fe2592dacc76c7d31cb291ab924` در Run 156 هر هشت Job، `387/387` تست C#، `66/66`
تست Node، `139/139` تست Web، پنج browser scenario، هارنس `14/14`، Restore ۴۵ Migration و
Qualification `7/7` را پاس کرد. Safe Resume اکنون `S07-MS09` و UI/Production defaults خاموش‌اند؛
گام بعد فقط F04 است.

**F04 Progress / Planned-Actual-Variance / S-Curve Semantic Contract — Slice 07 Micro-Step 10
Safe Checkpoint:** روی Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS09-C1`، قرارداد
`PMCS-RPT1-F04-SEMANTIC-001 v1.0.0` پیشرفت فیزیکی Certified را به Planning configuration، یک
Baseline رسمیِ مؤثر و evidence تأییدشده تا cutoff محدود می‌کند. پارامتر Client فقط `{}` است؛ انتخاب
Baseline و grid Curve server-owned، Variance دقیقاً `Actual - Planned` و Curve حداکثر ۳۶۶ نقطه است.
MeasurementWeights بدون Schedule معتبر می‌ماند و Forecast/EVM/Composite Health یا join F05 تا F10
ممنوع است. سه Permission خواندنی Planning و Classification کامل لازم‌اند. قرارداد همچنین صریح می‌کند
که lifecycle/target/profile جاری برای بازسازی تاریخی کافی نیست و Runtime بعدی باید projection
cutoff-aware بسازد. Candidate `f8829027c2ce073c207cd0e04a49c306b546c6a1` با tree
`2b784f135894092ef55bf7c7df201b1f03e0c77f` در Run 158 (`35522512734`) هر هشت Job، `387/387`
C#، `68/68` contract، `139/139` Web، پنج browser scenario و Restore ۴۵ Migration را پاس کرد. این
Checkpoint فقط DoR و Golden matrix بیست‌ودوسناریویی را می‌بندد؛ Runtime، Migration، Renderer،
Catalog/API/Worker، UI و Production enablement هنوز پیاده نشده‌اند.

**F04 Bounded Runtime Core — Slice 07 Micro-Step 11 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS10-C1` فقط identity/schema نسخه‌دار، Application Contractهای باریک و
cutoff-aware در Planning و FieldOperations، lifecycle selector، calculator قطعی و semantic Snapshot
builder اضافه شده است. Unit/contract testها ماتریس ۲۲سناریویی، correction/rebaseline تاریخی،
Actual/Planned/Variance، sampling حداکثر ۳۶۶ نقطه، status/reason، Classification و determinism را
پوشش می‌دهند. compatibility projection هر legacy history غیرقابل‌اثبات را fail-closed می‌کند. Source
`deb1571ec66d820868e8f4b77b631471e3c8207c` با tree
`9e41495a357480af03f1555ef640962ab863d332` و PR validation merge
`f0d3a5550d9bd1c10d8ddd5a3c0ada24eb0fead5` دارای همان tree، در Run 163 (`35527577826`) هر هشت
Job، `412/412` تست C# شامل `25/25` case متمرکز F04، `69/69` تست Node، `139/139` تست Web، پنج
browser scenario، validator روی `378` فایل، audit `274/204/5`، Restore ۴۵ Migration و Qualification
`7/7` را پاس کرد. هیچ Migration، Renderer، Catalog/API/Worker wiring، UI یا Production enablement
وارد این Checkpoint نشده است؛ Safe Resume اکنون `S07-MS11` و گام بعد فقط Renderer/Golden F04 است.

**F04 Deterministic Renderer/Golden — Slice 07 Micro-Step 12 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS11-C1` فقط Template/Renderer/Layout identity، parser/request/model
fail-closed و PDF/XLSX قطعی اضافه شده است. PDF فارسی/RTL و A4 افقی، XLSX هشت-Sheet با ZIP قطعی،
RTL، frozen header، سلول عددی واقعی و صفر Formula، NoData بدون صفر ساختگی و budgetهای fail-closed
دارد. Goldenهای XLSX/PDF به‌ترتیب
`8a1866b7bdb3b9cb96d83a1897d80db4584c1590b3727e9ebb6a17856d672fb7` و
`bdc9c3a99c1dc5a0da57f9431d7bc7f04830fbbfbeb578b24c7234df708785ef` هستند. Source
`6a717f10e4bff167ad7e2643313008f5afcc8264` با tree
`995c7fae108bbb5265faa036f951036d36e7061e` و PR validation merge
`782f42ff73425cf5cad69b0635bacf05790d2ff1` دارای همان tree، در Run 167 (`35532522587`) هر هشت
Job، `418/418` تست C# شامل شش case Renderer/Golden تازه و `31/31` case متمرکز F04، `70/70` تست
Node، `139/139` تست Web، پنج browser scenario، validator روی `381` فایل، audit `274/204/5`،
Restore ۴۵ Migration و Qualification `7/7` را پاس کرد. هیچ Migration، Catalog/API/Worker wiring،
DI registration، UI یا Production enablement وارد این Checkpoint نشده است؛ Safe Resume اکنون
`S07-MS12` و گام بعد فقط wiring متصل F04 است.

**F04 Catalog/API/Worker — Slice 07 Micro-Step 13 Connected Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS12-C1`، Migration forward شمارهٔ 46، Definition/Template قطعی F04، parser
سخت‌گیرانهٔ `{}`، Project profile pin و policy چندPermissionی هر Definition اضافه شده است. Catalog،
Runها و Outputها فقط با هر سه Permission `planning.progress.read`، `planning.baselines.read` و
`planning.milestones.read` دیده و اجرا می‌شوند و سرویس read-only نیز همان policy را fail-closed
اعمال می‌کند. Worker permissionها را دوباره ارزیابی، از `IProjectProgressReportingSource` و Snapshot
builder checkpointed استفاده و PDF/XLSX را فقط از Registry نسخه‌دار F04 منتشر می‌کند. هارنس متصل
strict parameters، isolation، create/replay/conflict، `NotConfigured` صریح و integrity/verify هر دو
فرمت را کنترل می‌کند. Source `4c48c03aad126a594e5328fc7995a72728ba2274` با tree
`49f957729fdccb0397dd153b93135ce2eaddd68a` و PR validation merge
`05ca8ac7e3fa643e111b9c8511e3e08d62be60a5` دارای همان tree، در Run 169 (`35535904655`) هر هشت
Job، `419/419` تست C#، `71/71` تست Node، `139/139` تست Web، پنج browser scenario، هارنس F04
برابر `15/15`، validator روی `382` فایل، audit `274/204/5`، Restore ۴۶ Migration و Qualification
`7/7` را پاس کرد. Safe Resume اکنون `S07-MS13` است؛ UI/UX2 و Production defaults خاموش‌اند و گام
بعد فقط DoR/قرارداد معنایی مستقل F05 است.

**F05 Financial Position / Cash / Obligations / Aging Semantic Contract — Slice 07 Micro-Step 14
Safe Checkpoint:** روی Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS13-C1`، قرارداد
`PMCS-RPT1-F05-SEMANTIC-001 v1.0.0` Cash Position را فقط از Financial Recordهای Posted تا cutoff،
تعهدات Approved و settlementهای immutable و Budget Baseline اختیاری می‌سازد. Cash formulaها،
تفکیک Payable/Receivable، Aging چهار-bucketی، lifecycle مستقل Budget، status/reason و absence صفر
ساختگی قطعی‌اند. FX، Forecast، EVM، Management Fee و join پنهان F06 ممنوع است. Client فقط `{}`
می‌فرستد؛ چهار Permission Finance/Budget و Classification حداقل `Confidential` لازم‌اند.

Candidate `72fa88349d01edd4c6455eb0af1aebfdeced8c35` با tree
`d2722dd8fab797650ed0c9befb80df93fc0be135` و PR validation merge
`6f1918ed1323fa3f6f14eeeaedcad8b2cf241ff7` دارای همان tree، در Run 171 (`35538654765`) هر هشت
Job، `419/419` تست C#، `73/73` تست contract، `139/139` تست Web، پنج browser scenario، validator روی
`382` فایل، audit `274/204/5`، Restore ۴۶ Migration و Qualification `7/7` را پاس کرد. این Checkpoint
فقط DoR و Golden matrix بیست‌وپنج‌سناریویی را می‌بندد؛ Runtime، Migration، Renderer،
Catalog/API/Worker، UI و Production enablement پیاده نشده‌اند. Safe Resume اکنون `S07-MS14` و گام
بعد فقط Runtime Core محدود F05 است.

**F05 Bounded Runtime Core — Slice 07 Micro-Step 15 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS14-C1`، Definition `project-financial-position-certified/1.0.0`، schemaهای
parameter/snapshot/profile نسخه‌دار، Contract و manifest/policy نسخه‌دار Finance، selector lifecycle،
calculator و semantic Snapshot builder اضافه شدند. انتخاب Cash فقط Posted تا cutoff، تعهد و
settlement رسمی، Budget مؤثر، تفکیک Payable/Receivable، Aging چهار-bucketی، Classification حداقل
`Confidential` و absence صریح پیاده شده‌اند. currency mismatch، Budget overlap، settlement
over-allocation، completeness ناقص و history غیرقابل‌اثبات fail-closed هستند؛ هیچ current-state
service، DbContext یا endpoint زنده‌ای از Reporting دور زده نمی‌شود.

Candidate `77ad46cbac12b899116516b0a58665ae888b3bf2` با tree
`5c67523b0fbed8d521627fe406f74271a1bbdcfe` و PR validation merge
`5f9ad7bd2bf0cb48c5a47dbfbe29ab09afc3f92c` دارای همان tree، در Run 175 (`35541740268`) هر هشت
Job، `450/450` تست C# شامل `31/31` case متمرکز F05، `75/75` تست Node، `139/139` تست Web، پنج
browser scenario، validator روی `390` فایل، audit `274/204/5`، Restore ۴۶ Migration و Qualification
`7/7` را پاس کرد. هیچ Migration، Template/Renderer، Catalog/API/Worker، UI یا Production enablement
وارد این Checkpoint نشده است؛ Safe Resume اکنون `S07-MS15` و گام بعد فقط Renderer/Golden F05 است.

**F05 Deterministic Renderer/Golden — Slice 07 Micro-Step 16 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS15-C1`، Template `1.0.0`، content digest و Renderer/Layout identity نسخه‌دار،
render model canonical و PDF/XLSX قطعی برای Snapshot موجود F05 اضافه شدند. Renderer همهٔ statusها و
nullهای Cash/Budget/Obligation را حفظ می‌کند؛ Payable/Receivable و Aging جدا هستند، Budget negative
remaining و consumption بالای صددرصد cap نمی‌شود و هیچ FX، Forecast، EVM، Management Fee یا join
پنهان F06 تولید نمی‌شود. PDF فارسی/RTL دوصفحه‌ای و XLSX هشت-Sheet با ZIP deterministic، RTL، frozen
header، عدد واقعی و صفر Formula، Goldenهای binary/visual/performance پین‌شده دارند.

Candidate `9ddf7f1d96324e7ffb22d2abec83071a6c087ec2` با tree
`f873795dcb8893dc28f88d5e5fc8292c5201e1e4` و PR validation merge
`72ab7827731fa763828c049be953ee9ca8c128a4` دارای همان tree، در Run 178 (`35558202348`) هر هشت
Job، `456/456` تست C# شامل شش case Renderer/Golden تازه و `37/37` case متمرکز F05، `77/77` تست
Node، `139/139` تست Web، پنج browser scenario، validator روی `393` فایل، audit `274/204/5`،
Restore ۴۶ Migration و Qualification `7/7` را پاس کرد. هیچ Migration، Catalog/Template seed، API،
Worker/DI wiring، UI یا Production enablement وارد این Checkpoint نشده است؛ Safe Resume اکنون
`S07-MS16` و گام بعد فقط wiring متصل Catalog/API/Worker F05 است.

**F05 Catalog/API/Worker — Slice 07 Micro-Step 17 Connected Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS16-C1`، Migration forward شمارهٔ 47، Definition/Template ثابت
`project-financial-position-certified/1.0.0`، strict parser برای `{}`، Project profile pin و policy
چهار-Permissionی اضافه شدند. Catalog، Run/Retry/Cancel، Download/Verify و `IReportingReadService`
فقط با تمام Permissionهای `financial-state.read`، `finance.records.read`،
`finance.obligations.read` و `budget.baselines.read` Definition/Run/Output را ارائه می‌کنند. Worker
همین مجوزها را دوباره ارزیابی، `IProjectFinancialPositionReportingSource` را با cutoff پین‌شده مصرف،
Snapshot را با builder checkpointed تولید و فقط از Registry نسخه‌دار PDF/XLSX F05 رندر می‌کند.

کاندید اولیه `f8d9078b6fc29863d9d3222f3b69b505f5b2a4ff` یک اختلاف دقت زیر-microsecond میان timestamp پین‌شده
.NET و round-trip PostgreSQL را fail-closed آشکار کرد. اصلاح
`6de1e9ac3b457426be5e50064d1767106cd50c39` مقایسه را به دقت ذخیره‌سازی canonical کرد و regression
test همان boundary را pin نمود. این source با tree `a4a8e8e655c56d05da2be5d87e7b84a9bb9a7a1f` و PR
validation merge `cbd27a673b1887b2e245bef5340eb8199f480be4` دارای همان tree، در Run 185
(`35563055242`) هر هشت Job، `458/458` تست C# شامل `39/39` case متمرکز F05، `78/78` تست Node،
`139/139` تست Web، پنج browser scenario، هارنس F05 برابر `15/15`، validator روی `394` فایل، audit
`274/204/5`، Restore ۴۷ Migration و Qualification `7/7` را پاس کرد. UI، feature flagها، license و
Production defaults تغییر نکرده‌اند؛ Safe Resume اکنون `S07-MS17` و گام بعد فقط DoR/قرارداد معنایی
مستقل F06 برای قرارداد، اصلاحیه، خرید و تأمین است.

**F06 Contract / Amendment / Procurement / Supply Semantic Contract — Slice 07 Micro-Step 18 Safe
Checkpoint:** روی Safe Checkpoint `PMCS-V1.1-RPT1-S07-MS17-C1`، قرارداد
`PMCS-RPT1-F06-SEMANTIC-001 v1.0.0` زنجیرهٔ Contract/Amendment/Request/Order/Receipt/Service
Acceptance را با lifecycle و cutoff رسمی تثبیت می‌کند. مبلغ/مدت مؤثر Contract، known subtotal در
برابر total کامل، commitment تجاری Order، fulfillment مبتنی بر quantity basis پین‌شده، delivery
status و supplier count/rate قطعی‌اند. F05/Finance join، Inventory، Invoice Matching، FX،
RFQ/Tender، ranking و AI ممنوع است. Client فقط `{}` می‌فرستد؛ شش Permission
Commercial/Procurement/Supply و Classification حداقل `Confidential` لازم‌اند.

Candidate `4c5d026466cb3f76f351221297540a0936337335` با tree
`d9e8febc5f220f8d00eaff80926b00dec2ea0926` و PR validation merge
`9bfb7badcde8a67d385567db997f265a67497f0f` دارای همان tree، در Run 188 (`35567252567`) هر هشت
Job، `458/458` تست C#، `80/80` تست contract، `139/139` تست Web، پنج browser scenario، validator
روی `394` فایل، audit `274/204/5`، Restore ۴۷ Migration و Qualification `7/7` را پاس کرد. این
Checkpoint فقط DoR و Golden matrix سی‌ودوسناریویی را می‌بندد؛ Runtime، Migration، Renderer،
Catalog/API/Worker، UI و Production enablement پیاده نشده‌اند. Safe Resume اکنون `S07-MS18` و گام
بعد فقط Runtime Core محدود F06 است.

**F06 Bounded Runtime Core — Slice 07 Micro-Step 19 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS18-C1`، Definition
`project-commercial-procurement-supply-certified/1.0.0`، schemaهای parameter/snapshot/profile
نسخه‌دار، Contract و manifest/policy نسخه‌دار Commercial، selector lifecycle، calculator و semantic
Snapshot builder اضافه شدند. lifecycleهای Contract/Amendment/Request/Order، Party snapshot و
Item/quantity basis پین‌شده در زمان Issue، receipt/inspection/service acceptance و excess approval
با cutoff رسمی validate می‌شوند؛ history غیرقابل‌اثبات و Source current-state/truncated fail-closed
است.

Source نهایی `177a1d89a07c23b2ae446218e98556cfbcf57a21` با tree
`165cd1d451935f3cb94db7b9f5718678de00aca2` و PR validation merge
`2dbaf0ba14cf80ee863e07c2561ab7392037fd7c` دارای همان tree، در Run 192 (`35573450703`) هر هشت
Job، `490/490` تست C# شامل `32/32` case متمرکز F06، `82/82` تست Node، `139/139` تست Web، پنج
browser scenario، validator روی `402` فایل، audit `274/204/5`، Restore ۴۷ Migration و Qualification
`7/7` را پاس کرد. هیچ Migration، Template/Renderer، Catalog/API/Worker، UI یا Production enablement
وارد این Checkpoint نشده است؛ Safe Resume اکنون `S07-MS19` و گام بعد فقط Renderer/Golden F06 است.

**F06 Deterministic Renderer/Golden — Slice 07 Micro-Step 20 Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS19-C1`، Template `1.0.0` با content digest پین‌شده، قراردادهای نسخه‌دار
Renderer/Layout، render request/model fail-closed و PDF/XLSX قطعی برای Snapshot موجود F06 اضافه
شدند. PDF فارسی/RTL در سه صفحهٔ A4 افقی قرارداد/اصلاحیه، خرید/سفارش/تأمین و supplier/lineage را
نمایش می‌دهد. XLSX ده Sheet ثابت، ZIP deterministic، RTL، frozen header، عدد واقعی و صفر Formula
دارد؛ NoData header-only است و هیچ F05 join، Inventory، FX، ranking یا truncation تولید نمی‌شود.

کاندید اولیه `e8b86d8d65e2fed79acd5213586235a87ceb40fb` خطاهای compile gate را آشکار کرد؛ اصلاح
`72933e6850bab80eb348466b2f7db446766c4668` build را سبز کرد و Run 195 فقط دو placeholder Golden
را برای آشکارسازی digest قطعی fail کرد. Source نهایی `fb88b94d6949e7780f5f40aa567e2ea3f187a6e8`
با tree `212c1193d249cf1120297920c59b4ea15cb80c07` و PR validation merge
`9f8d6ce25b5ddbeec777d0e104024e9389b9a513` دارای همان tree، در Run 196 (`35582136746`) هر هشت
Job، `496/496` تست C# شامل شش case Renderer/Golden تازه و `38/38` case متمرکز F06، `84/84` تست
Node، `139/139` تست Web، پنج browser scenario، validator روی `405` فایل، audit `274/204/5`،
Restore ۴۷ Migration و Qualification `7/7` را پاس کرد. هیچ Migration، Catalog/Template seed، API،
Worker/DI wiring، UI یا Production enablement وارد این Checkpoint نشده است؛ Safe Resume اکنون
`S07-MS20` و گام بعد فقط wiring متصل Catalog/API/Worker F06 است.

**F06 Catalog/API/Worker — Slice 07 Micro-Step 21 Connected Safe Checkpoint:** روی Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS20-C1`، Migration forward شمارهٔ 48، Definition/Template ثابت، strict `{}`،
Project profile پین‌شده، هر شش Permission definition-aware و Worker/Renderer dispatch متصل F06
اضافه شدند. Catalog/Create/List/Get/Retry/Cancel/Download/Verify و read service fail-closed هستند؛
Worker در processing/rendering re-authorization می‌کند و فقط Source cutoff-aware و Registry نسخه‌دار
F06 را مصرف می‌کند. هارنس متصل `15/15` assertion را پاس کرد و Production defaults خاموش ماندند.

Source نهایی `df3879dd8b17403787154a398cc114b27c7172bc` با tree
`5483e684aaa220a32b3135ea0b2bb3b2136023be` و PR validation merge
`0900def8f237a8d282501a8ee4ae0be5676f2fda` دارای همان tree، در Run 202 (`36235821024`) هر هشت
Job، `497/497` تست C# شامل `39/39` case متمرکز F06، `85/85` تست Node، `139/139` تست Web، پنج
browser scenario، validator روی `406` فایل، audit `274/204/5`، Restore ۴۸ Migration و Qualification
`7/7` را پاس کرد. Safe Resume اکنون `S07-MS21` و گام بعد فقط DoR/قرارداد معنایی مستقل F07 برای
دفتر فنی Document/RFI/Submittal/Transmittal است؛ Runtime/Renderer/wiring F07 و UI/Production در آن
Micro-Step خارج از Scope می‌مانند.

**F07 Technical Office Semantic Contract — Slice 07 Micro-Step 22 Safe Checkpoint:** روی
`PMCS-V1.1-RPT1-S07-MS21-C1`، قرارداد مستقل
`PMCS-RPT1-F07-SEMANTIC-001 v1.0.0` برای Document/Revision، Transmittal،
RFI و Submittal بسته شد. Strict `{}`، cutoff UTC/local date و وضعیت‌های
`NotConfigured/NoData/InsufficientData/Available`، هر دو Permission
`technical.read` و `technical.confidential.read`، Classification حداقل
`Confidential` و Golden matrix ۲۷ سناریویی قطعی شدند. Approval Revision قبل از
Transmittal Issue ابلاغ رسمی نیست؛ RFI Answered/Accepted/Closed و
Submittal ForInformation/ApprovedAsNoted یکی نیستند.

Source جاری TechnicalOffice current و capped است و بخشی از event history برای cutoff
قدیمی ندارد؛ Runtime بعدی فقط با Application Contract تاریخی، completeness manifest و
failure امن برای تاریخچهٔ نامعلوم مجاز است. Candidate اولیه Run 204 به دلیل syntax تست
تازه رد و همان assertion اصلاح شد. Source نهایی
`11168534372487bff3cc798861ca036489b3dea0` با tree
`af7e9e48c706c23f50521dbf715e6027e1512194` و PR validation merge
`438ac2c4feeab006f850fe9be6c5641bbdcd5368` دارای همان tree در Run 205
(`36281376786`) تمام هشت Job، `497/497` C#، `89/89` Node، `139/139` Web،
پنج browser scenario، validator `406` فایل، audit `274/204/5`،
Restore ۴۸ Migration و Qualification `7/7` را پاس کرد. Safe Resume اکنون
`S07-MS22` و گام بعد فقط Runtime Core محدود F07 است. Renderer، Migration،
Catalog/API/Worker wiring، UI/Production و F08 وارد این Micro-Step نشده‌اند.

**F07 Bounded Runtime Core — Slice 07 Micro-Step 23 Safe Checkpoint:**
`IProjectTechnicalOfficeReportingSource`، selector/calculator و Snapshot Builder نسخه‌دار با
cutoff/project-local-date، bounded repeatable-read، completeness manifest و semantic hash اضافه
شدند. برای RFI/Submittal موجود که تاریخچهٔ میانی ندارند، فقط `InsufficientData` و count نامعلوم
گزارش می‌شود؛ Document/Transmittal با lineage اثبات‌شده مستقل می‌مانند. چهار Candidate/اصلاح
fast-forward ثبت شد؛ Runهای 208–210 compile/fixture را آشکار کردند و Source نهایی
`7f66113d3fe091829747d1f5059eb8f16c82cb88` با tree
`88ba4957b76c6803893afa04d618b22c47c919e9` و PR merge
`22ec3d66aeeb5f7e15070bfdaa3ba2f6b1c17cf2` در Run 211 (`36296626010`) هر هشت Job،
`512/512` C#، `90/90` Node، `139/139` Web، پنج browser scenario، validator `415` فایل،
audit `274/204/5`، Restore ۴۸ Migration و Qualification `7/7` را پاس کرد.
Safe Resume اکنون `S07-MS23` است؛ گام بعد فقط Renderer/Golden محدود F07 در `S07-MS24` است.
Historical producer کامل RFI/Submittal، Migration/Catalog/API/Worker، UI/Production و F08
در این Micro-Step وارد نشدند.

**F07 Bounded Renderer/Golden — Slice 07 Micro-Step 24 Safe Checkpoint:**
روی `PMCS-V1.1-RPT1-S07-MS23-C1` فقط Template/Renderer/Layout identity نسخه‌دار، parser و
render model fail-closed، PDF چهار بخش RTL فارسی، XLSX شش Sheet قطعی و Goldenهای
binary/visual/performance برای Snapshot محدود F07 اضافه شدند. Coverage Sheet و PDF،
Document/Transmittal رسمی را مستقل نمایش می‌دهند و RFI/Submittal با history legacy ناقص را
`InsufficientData` با count نامعلوم و علت صریح نگه می‌دارند؛ صفر فقط برای Source کامل و
`NoData` معتبر است. Source Run 215 `8a08d3a0876cb6307613cb3eb51d918ff0269564` با tree
`ffa44deb661c4055f06fd32064bdfa8f61de425f` و PR merge
`121960696bb6c3fd4a7c490371ee20367840cb0b` دارای همان tree همهٔ هشت Job،
`518/518` C#، `91/91` Node، `139/139` Web، پنج browser scenario، validator `419` فایل،
audit `274/204/5`، Restore ۴۸ Migration و Qualification `7/7` را پاس کرد. Safe Resume اکنون
`S07-MS24` است؛ گام بعد `S07-MS25` فقط historical transition producer RFI/Submittal در مالک
TechnicalOffice است. Catalog/API/Worker wiring، UI/UX2، Production enablement، Report Designer و
F08 بازند؛ defaults همچنان خاموش‌اند.

**F07 Historical Transition Producer — Slice 07 Micro-Step 25 Safe Checkpoint:**
مالک TechnicalOffice برای RFI و Submittal تازه، رخدادهای رسمی کمینه با sequence، timestamp UTC و
classification/outcome را اتمیک با همان aggregate ثبت می‌کند. Migration شمارهٔ ۴۹ فقط ستون
nullable ledger را forward-only می‌افزاید: هیچ رکورد قدیمی از Status/Response کنونی backfill یا
Certified نمی‌شود؛ بخش دارای history قدیمی/ناسازگار `InsufficientData` و count نامعلوم است.
Source برای ledger کامل جدید، revision و status/timestamp/response/outcome را با دقت ذخیره‌سازی
PostgreSQL تطبیق می‌دهد و cutoff را از eventها می‌سازد. Run 219 (`36304170407`) با Candidate
`b2cc811e9202b49dd643972bde547c105fd9dc02` و tree
`1673d1b48ca41fd425199da9235ec87c712d81b2` هر هشت Job، `522/522` C#، `92/92` Node،
`139/139` Web، پنج browser scenario، Restore ۴۹ Migration و Qualification `7/7` را سبز کرد.
Safe Resume `S07-MS25` و گام بعد `S07-MS26` فقط Catalog/API/Worker wiring و Qualification
End-to-End مستقل F07 است؛ F08–F10/UI/Production و تمام defaultهای خاموش دست‌نخورده‌اند.

**F07 Connected Catalog/API/Worker — Slice 07 Micro-Step 26 Safe Checkpoint:**
Definition و Template تغییرناپذیر F07 با Migration شمارهٔ ۵۰ منتشر و فقط با دو Source permission
`technical.read` و `technical.confidential.read` در Catalog/API نمایان شدند. Worker همان
Application Contract مالک TechnicalOffice و Snapshot نسخه‌دار را به Rendererهای PDF/XLSX پین‌شده
وصل می‌کند؛ پارامتر Client فقط `{}` است و legacy بدون ledger کامل همچنان `InsufficientData`
می‌ماند. نقش `TechnicalOffice` به‌تنهایی مجوز confidential ندارد و deny مستقل دارد؛
`ContractAdministrator` واجد هر دو مجوز مسیر موفق را qualify کرد. Candidate
`b7a44b35eb7f498bf4382990324e3253033c0284`، tree
`bb6ebad2d3435caf4e085a65e08a085a2271761f` و Run 222 (`36305583760`)
هر هشت Job، `523/523` C#، `94/94` Node، `139/139` Web، پنج مرورگر، F07 connected
`17/17`، Restore ۵۰ Migration و Qualification `7/7` را سبز کردند. Safe Resume
`S07-MS26` است؛ گام بعد فقط DoR و قرارداد معنایی مستقل F08 برای Quality/HSE در
`S07-MS27` است. F09/F10، UI/UX2، Production enablement و Report Designer بازند و defaults خاموش‌اند.

**F08 Quality/HSE Semantic Contract — Slice 07 Micro-Step 27 Safe Checkpoint:**
قرارداد `PMCS-RPT1-F08-SEMANTIC-001 v1.0.0` دو بخش مستقل Quality/HSE را با مالک
QualitySafety، زمان/پیکربندی پین‌شده، Source کامل، وضعیت‌های NoData/InsufficientData/
NotConfigured، سه permission خواندنی، سیاست fail-closed برای PersonalMedical/
LegalInvestigation و ماتریس ۲۲ سناریو بست. Run 224 (`36307100818`) روی source
`b72ab1c09376e0ec45b6b52a460660f4b807b15a` با tree
`14334896b2155f6ebefe612a29123bd73bce3760` همهٔ هشت Job و `98/98` Node را
سبز کرد. Safe Resume `S07-MS27`؛ گام بعد فقط Runtime Core محدود F08 در MS28 است.

**F08 Quality/HSE Runtime Core — Slice 07 Micro-Step 28 Safe Checkpoint:**
مالک `QualitySafety` قرارداد Source خواندنی نسخه‌دار را از ۱۴ دفتر bounded و
repeatable-read با manifest/hash، cutoff، configuration pin، linkage و classification
fail-closed ارائه می‌کند. Quality/HSE مستقل می‌مانند و legacy/SetupRequired/Suspended
با count نامعلوم `InsufficientData` هستند. Reporting Snapshot را فقط از Application
Contract مالک می‌سازد. Run 231 (`36308573306`) هر هشت Job را سبز کرد. Safe Resume
`S07-MS28`؛ گام بعد فقط Renderer/Golden قطعی F08 در MS29 است؛ wiring، F09/F10 و
UI/Production باز، defaults خاموش‌اند.

**F08 Quality/HSE Renderer/Golden — Slice 07 Micro-Step 29 Safe Checkpoint:**
PDF دو بخش و XLSX چهار Sheet قطعی با status/count/classification مستقل، متن امن و
عدم نمایش نرخ حادثهٔ بدون denominator در Template `1.0.0` پین شدند. Golden PDF/XLSX
و دو صفحهٔ visual در Checkpoint MS29 ثبت شدند. Source
`68a8c49311d05480824f3c7ec58933510877c8e6`، tree
`14fe30b65d983c553cf3675dc202b682d90f5c9d` و Run 235 (`36309751881`)
هر هشت Job را سبز کردند. Safe Resume `S07-MS29`؛ گام بعد فقط اتصال Catalog/API/Worker
و Qualification مستقل F08 در MS30 است. F09/F10، UI/Production باز و defaults خاموش‌اند.

**F08 Quality/HSE Connected — Slice 07 Micro-Step 30 Safe Checkpoint:**
Migration 51، Catalog/API strict `{}` و Worker با سه permission whole-definition
`quality.read`، `hse.read` و `hse.confidential.read` به Source مالک و Rendererهای
قطعی MS29 متصل شدند. Run 237 (`36310745011`) هر هشت Job، هارنس `20/20`، Restore Drill
۵۱ Migration، PDF/XLSX download/verify و Qualification `7/7` را سبز کرد. Source
`786f032e5ce91ceffa80599c4ce02ba23f30bb1d`، tree
`9adcf1558c1bcab8a40a242c49efdf4df816a2dc`. Safe Resume `S07-MS30`؛ F01
تا F08 End-to-End بسته‌اند. گام بعد فقط DoR/قرارداد معنایی F09 برای Issue/Risk/
Decision/Escalation/Action در MS31 است؛ F10، UI/Production باز و defaults خاموش‌اند.

در `S07-MS31`، DoR/قرارداد معنایی مستقل F09 با ۲۷ fixture برای پنج دفتر
Issue/Risk/Decision/Escalation/Action بسته شد. Candidate
`67b60c00779d51ea9479dee4d887c810d572d485` با tree
`400add67f58dc03b559c3f1420d186fb6cf5ec15` و Run 239 (`36318261047`)
هر هشت Job را سبز کرد. Runtime/Renderer/wiring F09 هنوز باز است؛ MS32 فقط
Source مالک ActionControl و Runtime Core محدود را آغاز می‌کند. F10، UI/Production
باز و همهٔ defaults خاموش‌اند.

در `S07-MS32`، Source مالک ActionControl هشت register را bounded و repeatable-read
می‌خواند و Snapshot Core محدود F09 پنج وضعیت مستقل، manifest/digest، classification
محافظه‌کارانهٔ Action و legacy `InsufficientData` با count نامعلوم را حفظ می‌کند.
Candidate `2000b965dc31e6a83f0b1366c9a203ae809cee03`، tree
`14e9b0c6bcc4c4a1b785e761c678e18f0d216a8c` و Run 242 (`36321908108`)
هر هشت Job را سبز کردند. Migration ۵۱ و defaultها ثابت‌اند؛ MS33 فقط
Renderer/Golden محدود F09 است و producer تاریخچه، wiring، F10/UI/Production بازند.

در `S07-MS33`، Renderer/Golden PDF پنج‌بخشی و XLSX هفت Sheet F09 با
Snapshot/Template pin، count نامعلوم آشکار و spreadsheet بدون formula در Run 245
(`36324102914`) هر هشت Job را سبز کرد. Candidate
`0701a44155d1955a1b8e27db01d561c370b6be6e`، tree
`db7347e04344394f219cd68257a23c27159fd200` و C# `538/538` ثبت شدند.
MS34 فقط producer تاریخچهٔ مالک بدون backfill حدسی است؛ wiring و F10 بازند.

در `S07-MS34`، شش Aggregate مالک transitionهای تازه را با ترتیب/زمان UTC و
payload کمینه در ledger nullable ثبت می‌کنند. Migration ۵۲ هیچ backfill حدسی
انجام نداد. Candidate `79bc3c62f17711eaa61281f90d995852f9128147`، tree
`e32fb658426a74fcfb0f61a463ab9748d1b8b686` در Run 248 (`36327282876`)
هشت Job سبز گرفت؛ C# `540/540`، Node `109/109` و Restore Drill ۵۲. MS35 فقط
selector cutoff-aware Source مالک است؛ سپس wiring/Qualification مستقل F09 در
MS36 می‌آید. F10، UI/Production و defaults همچنان باز/خاموش‌اند.

در `S07-MS35`، Source مالک رخدادهای F09 را با revision/sequence، cutoff و
تطبیق وضعیت جاری انتخاب کرد. Register digest از تصویر زمانی ساخته می‌شود؛
legacy، دادهٔ ناقص و classification محافظه‌کارانه جدا می‌مانند. Candidate
`44bd46689bcb795534bcf059f42591cecdaa35ba`، tree
`d849b915a0157dd10812acb8271c8b64dd108c29` در Run 250 (`36329655993`)
هشت Job سبز گرفت؛ C# `542/542` و Restore Drill ۵۲. MS36 فقط اتصال
Catalog/API/Worker و Qualification متصل F09 است؛ F10/UI/Production بازند.

در `S07-MS36`، Catalog/Template نسخه‌دار F09 با Migration ۵۳ و سه permission
`governance.read`، `governance.sensitive.read`، `actions.read` به API/Worker
متصل شد. PDF پنج صفحه و XLSX هفت Sheet در مسیر Generated Document از Snapshot
cutoff-aware صادر شدند. Candidate `2bb925b8cf8531442c5136e55811a9f8c31db655`، tree `3085dc51931b2b7965521b7b18fe0b9bfa55ec70`،
Run 252 (`36331528136`) هر هشت Job را سبز کرد؛ هارنس F09 `18/18`، C#
`542/542`، Node `110/110`، Web `139/139`، پنج مرورگر و Restore ۵۳ موفق‌اند.
F01 تا F09 End-to-End بسته‌اند. Exact Next فقط DoR/قرارداد معنایی مستقل F10 در
`S07-MS37` است؛ UI/Production و defaultها باز/خاموش‌اند.

در `S07-MS37`، DoR/قرارداد معنایی مستقل F10 برای Portfolio واقعی و نه Project
Report، permission cohort، cutoff هر پروژه، currencyهای مجزا و ۳۰ Gate پذیرش ثبت شد.
Candidate `bb17a37fa19b06d111443fad178c2589e376bb25`، tree
`82eba0ffebc1c631281a78727f4b2325a50f0ecb` در Run 254 (`36333343004`)
هر هشت Job را سبز کرد. F10 فقط Contract Ready است؛ Exact Next MS38 زیرساخت
Tenant-scope Report/Document با Migration سازگار، سپس Source/Runtime، Renderer و
wiring/Qualification متصل مستقل. RPT1، UI/Production و defaults باز/خاموش‌اند.

در `S07-MS38`، Migrationهای افزایشی Documents/Reporting scope واقعی Portfolio
با ProjectId nullable، owner سند Tenant و CHECKهای fail-closed را ثبت کردند؛ Worker
پروژه‌ای فقط Project را claim می‌کند. Candidate `ea5215e26ba382bdecd756adec1083dd3ef28d06`،
tree `63e5542679e9d0427c6ec96ad7fa3404810f340d` در Run 256
(`36335141253`) هشت Job، C# `544/544`، Node `117/117` و Restore ۵۵
Migration را سبز کرد. Exact Next MS39 فقط Source/Runtime Core F10؛ Renderer
و wiring/Qualification بعدی باز است.

در `S07-MS39`، Source فقط cohort مجاز و owner selectorهای F03/F05/F06 را
با permission mask مستقل می‌خواند؛ Runtime ۲۰۰ پروژه را fail-closed، ارزها را
جدا، profile تاریخی نامعلوم را `InsufficientData` و manifest نسخه‌دار را pin
می‌کند. Candidate `c45b72be7bc913a6dc65b656bea0188f7da0dc8c`، tree
`00ad037a2eec41c2d5dbacfe0c186e04c4584cce` در Run 261 (`36338179481`)
هشت Job، C# `549/549`، Node `120/120` و Restore ۵۵ را سبز کرد.
Exact Next MS40 PDF/XLSX Renderer/Golden؛ wiring/Qualification بعدی باز است.

در `S07-MS40`، Rendererهای PDF/XLSX F10 مدل Snapshot/Manifest immutable را
replay و identity/template/classification را fail-closed اعتبارسنجی می‌کنند.
Golden PDF سه صفحه و XLSX شش sheet با دو ارز مستقل و پوشش ناقص پین است.
Candidate `d57f73611e647d352ec5c59a19cd996e589d0fe2`، tree
`52daac29eff755e3a95c132d581fa7de37ef6387` در Run 264 (`36340600926`)
هشت Job، C# `552/552`، Node `123/123` و Restore ۵۵ را سبز کرد.
Exact Next MS41 Catalog/Template و Tenant API؛ Worker/OutputAccess بعدی باز است.

در `S07-MS41`، Migration `reporting/20260927-012` Catalog/Template واقعی F10
را با scope Portfolio منتشر کرد. Tenant API مستقل، `{}` سخت‌گیرانه، format
allowlist، cohort/mask و idempotency را pin و List/Get را با recheck کامل
محافظت می‌کند. Candidate `c22f08778a4c9b523d926f1aedc9f8d9142655d4`، tree `3e5c6f93fae75200f3abba18b292eac4a17d3069` در Run 266 (`36343299949`)
هشت Job، C# `553/553`، Node `126/126` و Restore ۵۶ را سبز کرد.
Exact Next MS42 Worker تولید، سپس MS43 OutputAccess/Qualification است.

در `S07-MS42`، Worker مستقل F10 فقط Runهای Portfolio را claim و با cohort/mask
پین‌شده، cutoff با دقت پایگاه‌داده و digest canonical JSONB دو خروجی PDF/XLSX
را در سند Tenant ثبت می‌کند. Candidate `7a8a560fec6da560aa6982f1b9f11fc253dafef4`، tree
`0683b1cbe844a321862e3e25fe2aa4e01e93174c` در Run 272 (`36349571188`)
هشت Job، C# `554/554`، Node `128/128`، QA متصل `4/4` و Restore ۵۶ را سبز کرد.
Exact Next MS43 OutputAccess، Retry/Cancel و Qualification End-to-End F10 است.

در `S07-MS43`، Download/Verify اختصاصی Tenant سند `TenantReportOutput` را
با cohort/mask permission recheck، مالکیت، digest Snapshot/manifest و byte/hash
کنترل می‌کند. Retry خطای مجاز Snapshot را حفظ می‌کند و Cancel پیش از Render
idempotent است. Candidate `84e4e76ca6173f834cec5ea481adc475e9dfd22f`، tree
`033b2e4cc3652735b60b7fa9b849707906f8acc0` در Run 275 (`36352517816`)
هر هشت Job، C# `554/554`، Node `130/130`، Web `139/139`، QA متصل F10
`12/12`، Retry `3/3`، Cancel `4/4`، شش کنترل امنیت، Restore ۵۶ و
Qualification `7/7` را سبز کرد. F01 تا F10 End-to-End متصل‌اند.
Exact Next طبق ترتیب Roadmap، `V1.1-COL1` با DoR مستقل است؛ UI اختصاصی
Reporting در UX2 و Production enablement gateهای جدا و همچنان بازند.

### `V1.1-COL1` — Project Collaboration

DoR مستقل `PMCS-V1.1-COL1-DOR1` روی Commit `56597133e6d85372b1d7c8e1b2940ade17bb97ef`
با Run 280 و هشت Job سبز ثبت شد. MS01 هستهٔ Room/Message با Migration 57،
عضویت فعال، Permission، ordering، duplicate prevention و Audit/Outbox متصل
روی source `a09f52506158eaa69c8aa6692cc057c9704900b0` / tree
`850ddf4e940e3da440b877115d4cb53e5ca14fcf` در Run 281
(`36359396205`) هشت Job را پاس کرد. Exact Next `COL1-MS02` است؛
MS03–MS06 و UX2/Production بازند. Defaults Collaboration خاموش‌اند.

MS02 با Reply/Mention، Reaction/Pin، Search، read cursor/unread و notification
متصل روی source `005e25dad7925ea4d7d3f28a0b3b0b94a7af506d` / tree
`14d4844e2bbf869d97aefaae152d4dd73e65596c` در Run 283
(`36360228559`) هر هشت Job را پاس کرد. Migration count اکنون ۵۸ است.
Exact Next `COL1-MS03`؛ MS04–MS06 و UX2/Production بازند.

MS03 روی source `c47c7e4202b9c7616602bedda587bc24b75246ed` / tree
`2ebc69d8b528274b2828ccdc8ef367e3fc8f0fe7` در Run 286
(`36361342403`) هشت Job را پاس کرد: WebSocket مجاز با replay از sequence،
fallback long-poll از BFF، recheck عضویت، صف IndexedDB هویت‌محور، آزمون متصل
دو کاربر و مرورگر واقعی offline/reload/retry. Run 285 کاندید اولیه در Build
TestHarness شکست خورد و با commit اصلاحی fast-forward شد؛ checkpoint فقط به
Run 286 متکی است. Exact Next `COL1-MS04`؛ UX2/Production بازند.

MS04 روی source `7f3a09cb5ca5e305b9d20fdd4cb7a10079803d5d` / tree
`f0f1e526043bda8f4689f64be9fb86a7db0b8ef9` در Run 294
(`36363690667`) هر هشت Job را پاس کرد. پیوست Chat با owner contract و
quarantine/release، دانلود خصوصی و hash/version، و تاریخچهٔ immutable،
تعدیل با دلیل، tombstone و Legal Hold در تست متصل اثبات شد؛ ۶۰ Migration
Restore شدند. پاک‌سازی فیزیکی تا سیاست retention تأییدشده خاموش است.
Exact Next `COL1-MS05` شش تبدیل رسمی؛ MS06 و UX2/Production بازند.

MS05 روی source `d9e327a3e04fb9d2ca22a403b6e84d612abfbaf3` / tree
`5ab89f36cc93f832bbc752f3ce0c8416e18b1689` در Run 301
(`36367098137`) هر هشت Job را پاس کرد. Action/Issue، RFI/Technical Document،
Daily Fact/Evidence از Command مالک مقصد و تراکنش واحد با lineage، Audit و
Idempotency ساخته شدند. MS05A در Run 297 و MS05B در Run 299 مستقل سبز شدند؛
۶۳ Migration و Restore Drill سبز است. Exact Next `COL1-MS06` Qualification
امنیت/رقابت و Checkpoint پایان ساخت؛ UX2/Production بازند.

MS06 روی source `856089b4070ef4c8720aa01a4289139a9a0e4adc` / tree
`f98685e0dc45acdff075420d9d0a92408e3a7593` در Run 304
(`36368449059`) هر هشت Job را پاس کرد. رقابت دو نقش برای یک RFI فقط یک
رکورد رسمی و یک Lineage/Audit ساخت؛ پس از تعلیق عضویت، Chat، تبدیل، Evidence
و سند فنی `403` شدند. Full Regression و Restore Drill ۶۳ Migration سبزند.
ساخت متصل COL1 بسته است؛ `Collaboration:Enabled=false` و Gateهای UX2، INT1،
QA1، Pilot و Production بازند. Exact Next `V1.1-UX2`.

UX2-MS01 روی source `16ade7062aef5090b1b6ebc2b9d9409d3db46e19` / tree
`df807f044995ef6957089b4e699028d44f4e58ba` در Run 306
(`36369509553`) هر هشت Job را پاس کرد. PDF رسمی نشان با ماسک شفاف،
خروجی‌های PNG و منشأ/Hash ثبت شد؛ Shell و Login Fallback بدون تغییر
Descriptor نسخه‌دار یا احراز هویت از نماد رسمی استفاده می‌کنند.
`VX-G3` و مهاجرت Chat/Reporting/ماژول‌ها و Visual Qualification بازند.
Exact Next `UX2-MS02` Token/Focus/Reduced Motion و ناوبری موبایل است.

UX2-MS02 روی source `62bf1ef6c5c5b23478a0580786a4875b990f4ef8` / tree
`f690245e91ac362fcc5abf290088718562e6955a` در Run 309
(`36370829403`) هر هشت Job را پاس کرد. Tokenهای `PMCS-DS-001`، Focus-visible،
Reduced Motion و ناوبری موبایل به UI وصل‌اند؛ Run 308 شکست آزمون Focus
برنامه‌ای را با آزمون Tab/Shift+Tab واقعی در Commit جلوبرنده رفع کرد.
Exact Next `UX2-MS03` رابط خواندنی Chat محدود به پروژه است؛ Gateهای
Design System، مهاجرت کامل، Reporting UI و Qualification همچنان بازند.

UX2-MS03 روی source `282fc1726b332e4d060d4aae0c028017c00345ab` / tree
`90351f1e7fb1691cb63366924ea648acfac4a2ec` در Run 313
(`36372486466`) هر هشت Job را پاس کرد. Route خواندنی Chat فقط در Context
پروژه، با Flag خاموش، مرز 403، tombstone و چیدمان فشردهٔ موبایل در مرورگر
واقعی تأیید شد. Run 311 شکست Audit عنوان لاتین را با اصلاح جلوبرنده در Run
312 رفع کرد؛ Run 313 اصلاح چیدمان موبایل را تثبیت کرد. Exact Next
`UX2-MS04` ارسال، دریافت زنده و بازیابی صف آفلاین در UI است؛ Reporting و
Gateهای مهاجرت و Qualification بازند.

UX2-MS04 روی source `c09fd37ecd674fe888ca52c6e369d504d19d4802` / tree
`b0fd1056967d91e052c16c6157e1d8aa0f34e6ed` در Run 315
(`36373439021`) هر هشت Job را پاس کرد. ارسال گروهی با Client ID و کلید
Idempotency پایدار، بازیابی صف IndexedDB پس از اتصال، رویداد زنده و حذف
پیام‌های قبلی پس از 403 در مرورگر واقعی تأیید شد. Exact Next `UX2-MS05`
تعامل‌های محدود به پروژه و سپس Reporting اختصاصی است؛ Gateهای UX2 بازند.

UX2-MS05 روی source `324e4f930e71fa072ded7b190eea935d468de74f` / tree
`04a22d38ee4fe2181e58def8a7e61e2677c9af43` در Run 317
(`36374318684`) هر هشت Job را پاس کرد. جست‌وجوی محدود به پیام‌های مجاز
همان پروژه، Reply با شناسهٔ پیام مرجع و read cursor با اقدام صریح کاربر در
مرورگر واقعی سبزند؛ 403 اطلاعات نمایشی/پیش‌نویس را پاک می‌کند. Exact Next
`UX2-MS06` رابط اختصاصی Reporting Center با Catalog/History خواندنی است؛
تعامل‌های باقی‌مانده Chat و Gateهای مهاجرت/Qualification بازند.

UX2-MS06 روی source `5914b2184a15cbec0cc8c7224e05fecf8098fb19` / tree
`094d693ddfdb01d8cc30d068cf5928d31ca667ef` در Run 319
(`36375230576`) هر هشت Job را پاس کرد. رابط اختصاصی Reporting Center،
Catalog و History مجاز همان پروژه را با مرز 404/403/Cross-project و حالت
پیش‌فرض خاموش نمایش می‌دهد؛ ناوبری و تصویر واقعی موبایل تأیید شدند.
Exact Next `UX2-MS07` درخواست استاندارد گزارش‌های بدون پارامتر با هویت
Idempotency پایدار است؛ F01/F02 ورودی روز/دوره، خروجی و Gateها بازند.

UX2-MS07 روی source `70c547c2993c521246ff61e688b9ba98e28f70eb` / tree
`ebf6ddd75ad5727f4924438861c70378318a2696` در Run 321
(`36376206850`) هر هشت Job را پاس کرد. هفت گزارش استاندارد بدون پارامتر
با قالب مجاز و Client ID پایدار درخواست می‌شوند؛ Retry پس از 503 و Reload
در مرورگر واقعی همان هویت را نگه می‌دارد. Exact Next `UX2-MS08` ورودی
گزارش روزانه/دوره‌ای F01/F02 است؛ OutputAccess و Gateهای UX2 بازند.

UX2-MS08 روی source `89b6e378f25d5e7c470896c6c35013ccc8bb2dec` / tree
`19841f9f92ac013bb8efb4199047473ec2d2a8a0` در Run 323
(`36377269239`) هر هشت Job را پاس کرد. F01 فقط گزارش‌های روزانهٔ تأییدشدهٔ
همان پروژه را انتخاب می‌کند و Revision را صریح می‌فرستد. F02 ورودی هفته
و ماه شمسی دارد؛ آغاز شنبه/اول ماه و دامنهٔ دوره در UI و Backend اعتبارسنجی
می‌شوند. Client ID پایدار و مرزهای مجوز/پروژه حفظ شده‌اند. Exact Next
`UX2-MS09` دسترسی امن و دانلود خروجی مجاز است؛ باقی Chat/UX2 و Gateها بازند.

UX2-MS09 روی source `73b170aa011dc61f78a4de6f45c7a8747eb14721` / tree
`140b9c99bb69b5c81a9dd7705f5364249f4588d9` در Run 326
(`36378694244`) هر هشت Job را پاس کرد. UI فقط برای خروجی ثبت‌شدهٔ Run موفق
به Endpoint همان پروژه می‌رود؛ MIME، اندازه و SHA-256 بایت‌ها با متادیتای
مجاز تطبیق می‌شود و 403 نمای مجاز را می‌بندد. Backend همچنان مرجع نهایی
Permission، Flag، Audit و یکپارچگی سند است؛ defaults خاموش‌اند.
Exact Next `UX2-MS10` رابط گزارش سبد پروژه‌ها با مجوز Tenant مستقل است؛
تعامل‌های Chat، مهاجرت سراسری و Gateهای UX2 بازند.

UX2-MS10 روی source `de22569ca12659fc96e3e3065d938a11dc5e3def` / tree
`e4add5830df07ddc749b35fdc5a0599ee46667a3` در Run 328
(`36379588242`) هر هشت Job را پاس کرد. مسیر مجزای سبد فقط Catalog/History
مجاز Tenant را می‌خواند؛ Flag خاموش، 403 پیش از خواندن سابقه و Run پروژه‌ای
نامعتبر را رد می‌کند. هیچ درخواست یا دانلود خروجی سبد در این گام فعال نشده
است. Exact Next `UX2-MS11` درخواست استاندارد با هویت پایدار است؛ دانلود
خروجی سبد، Chat و Gateهای UX2 بازند.

UX2-MS11 روی source `37cefe7bd76f2a1f4fa87fab03ecf07dfc05a69b` / tree
`2178071f82d7d9a8c7d356ff76ff7b8b74c7da25` در Run 331
(`36380518179`) هر هشت Job را پاس کرد. درخواست F10 فقط تعریف استاندارد
Portfolio با `parameters={}`، قالب مجاز و Client ID/Idempotency-Key یکسان
را می‌فرستد. Retry پس از خطا و Reload Payload/قالب XLSX را حفظ می‌کند؛ 403
دسترسی را می‌بندد. Exact Next `UX2-MS12` دانلود امن خروجی سبد است؛ Chat
و Gateهای UX2 بازند.

UX2-MS12 روی source `6a5b0649d1709eb3d483331ffcec0ee52f998920` / tree
`273511d67bc428d31e149e48e4630c45f465a3e3` در Run 334
(`36381545470`) هر هشت Job را پاس کرد. UI فقط خروجی Run موفق سبد را از
Endpoint Tenant دریافت می‌کند و MIME، اندازه و SHA-256 بایت‌ها را با متادیتای
مجاز تطبیق می‌دهد؛ 403 نما را می‌بندد. Backend همچنان مرجع نهایی مجوز
Tenant/cohort، Flag مستقل، Audit و یکپارچگی سند است. Exact Next
`UX2-MS13` واکنش‌های محدود Chat گروهی پروژه است؛ تعامل‌های دیگر و Gateهای
UX2 بازند.

UX2-MS13 روی source `8cc8c54ad471481c6231a083baa4966b8ab9e28c` / tree
`1e73b3aeb077c6336b3b42c942ff56188cd26b15` در Run 337
(`36390541888`) هر هشت Job را پاس کرد. UI چهار واکنش را با شمارش و وضعیت
کاربر پس از Reload از API خواندنی همان پیام/پروژه دریافت می‌کند؛ مجوز خواندن
و ارسال جدا هستند و 403 نما را می‌بندد. تأیید منبع لوگوی فعلی و قرارداد
تعویض سراسری فونت در Design System ثبت شد، بدون بستن Gate بصری. Exact next
`UX2-MS14` سنجاق/برداشتن سنجاق محدود به مجوز تعدیل است؛ پیوست، تبدیل/تعدیل،
مهاجرت کامل و Qualification بازند.

UX2-MS14 روی source `9185ab5bb65fb0af0c59fd1278aacd127c695486` / tree
`400ee81a5c5c7bcf4d9faabe299820f24496539f` در Run 339
(`36392574978`) هر هشت Job را پاس کرد. Room توانایی تعدیل را از مجوز مؤثر
و عضویت فعال می‌خواند؛ UI فقط برای تعدیل‌گر سنجاق/برداشتن سنجاق را نشان
می‌دهد، پاسخ را در محدودهٔ پیام/پروژه تطبیق می‌دهد و حالت خواندنی بعد از
Reload برای عضو عادی نیز باقی می‌ماند. 403 نمای قبلی را می‌بندد. Exact next
`UX2-MS15` نمایش/دانلود پیوست Released پیام همان پروژه است؛ ساخت/آپلود/اتصال
و سایر تعامل‌ها Micro-Step مستقل و Gateهای UX2 بازند.

UX2-MS15 روی source `a27f5b78a0bbc4cfc417e809f8d88330b224988e` / tree
`b272a94a06a9781cb7038603643cf98c57b116c9` در Run 341
(`36394249854`) هر هشت Job را پاس کرد. UI فهرست Released پیام زندهٔ همان
پروژه را با تطبیق شناسه/URL و دانلود با MIME، اندازه و SHA-256 نمایش می‌دهد؛
پیام حذف‌شده کنترل ندارد و 403 نما را می‌بندد. Exact next `UX2-MS16` آپلود
ProjectChat برای پیام زندهٔ نویسنده و وضعیت قرنطینه از مسیر محدود است؛
اتصال سند پس از Released، تبدیل/تعدیل و Gateهای بصری هنوز بازند.

UX2-MS16 روی source `56fdf8be44646901cc7d51b2f2c742e05fa4f9e7` / tree
`c97c8e224b246e7255d910218aa21481304ef004` در Run 343
(`36396255492`) هر هشت Job را پاس کرد. نویسندهٔ پیام زنده با دو Permission
مؤثر، فایل owner `ProjectChat` را در صف هویت‌محور همان پیام می‌گذارد؛ مسیر
محدود وضعیت قرنطینه/Released را پس از Reload می‌خواند و 403 نما را می‌بندد.
فایل قرنطینه‌شده هنوز پیوست پیام نیست. Exact next `UX2-MS17` اتصال صریح
سند Released و تأیید پاسخ سرور است؛ Gateهای UX2 بازند.

UX2-MS17 روی source `ff11d7848e0f0fbab32ddd017b1f5bae0730e009` / tree
`6c8c8cfe185248611b12ddc74cc572f6e89e787c` در Run 345
(`36397698204`) هر هشت Job را پاس کرد. تنها سند Released از مسیر نویسنده
با پاسخ محدوده/هش تأییدشده به پیام متصل می‌شود؛ فهرست بعد از اتصال و Reload
بازخوانی می‌شود و قرنطینه قابل اتصال نیست. Exact next `UX2-MS18` ویرایش
پیام خود با Revision و Conflict است؛ حذف/تعدیل، تبدیل و Gateهای UX2 بازند.

UX2-MS18 روی source `8698034157666b36fc147aba52529cdc19af3bf4` / tree
`9657f9d39ace8e76b1f8350757f6c92ef28be549` در Run 347 سبز شد؛
اصلاح تأیید envelope پیام روی `c740abebc5ba46f610a0365113cebfef7c90b216`
/ tree `c637ea3d5b2fead0c26c1519d9b0c3cd8976afe1` در Run 348
(`36399518663`) هر هشت Job را پاس کرد. ویرایش فقط با مجوز نویسنده، Revision
و Idempotency انجام می‌شود؛ Conflict پیش‌نویس را حفظ و بازخوانی و انتخاب
صریح را الزامی می‌کند. Exact next `UX2-MS19` حذف نمایشی پیام خود با Legal
Hold است؛ تعدیل، تبدیل و Gateهای UX2 بازند.

UX2-MS19 روی source `a746981e2f0e1bac58d29880b49f3970bf1fe51c` / tree
`aa6696686868c0c095eb5484911ad43b38b306fa` در Run 350
(`36400977033`) هر هشت Job را پاس کرد. نویسندهٔ پیام زنده با Revision و
Idempotency و تأیید صریح، حذف نمایشی انجام می‌دهد؛ Legal Hold کنترل را
می‌بندد و Conflict بازخوانی و تأیید دوباره می‌خواهد. متن و سابقهٔ سازمانی
حفظ می‌شود. Exact next `UX2-MS20` تعدیل پیام با دلیل، Redaction و Revision
است؛ تاریخچه، تبدیل و Gateهای UX2 بازند.

UX2-MS20 روی source `f9bb46d655d8385c6581ec02fcf842c3526e69af` / tree
`13859e54a56bc870e0e828b5f8ea173a7f4e39e8` در Run 352
(`36403055126`) هر هشت Job را پاس کرد. ناظر پروژه Redaction و تغییر
Legal Hold را تنها با دلیل، Revision، Idempotency و تأیید پاسخ محدود به همان
پیام انجام می‌دهد؛ Conflict بازخوانی و تأیید دوباره و 403 بستن نما را
الزامی می‌کند. جست‌وجوی مانده و پاسخ به پیام پنهان‌شده پاک می‌شوند. Exact
next `UX2-MS21` تاریخچهٔ محدود Revision/تعدیل است؛ تبدیل و Gateهای UX2 بازند.

UX2-MS21 روی source `04ecceea75bdf7964949b13b15279614bcf647d6` / tree
`bbad3b5bebf4e132a59f0afe3d9c93bde86eecdf` در Run 354
(`36404623940`) هر هشت Job را پاس کرد. تاریخچه از مسیر مستقل و محدود
برای نویسنده/ناظر خوانده می‌شود؛ عضو عادی کنترل ندارد، متن و دلیل در Timeline
عمومی نیستند و 403 دادهٔ خصوصی و نما را پاک می‌کند. Exact next `UX2-MS22`
اعلام مجوز تبدیل و lineage خواندنی است؛ فرم‌های شش مقصد و Gateهای UX2 بازند.

UX2-MS22 روی source `ac2fe6e03496cc3f2805f1b96b5cca558d438cf5` / tree
`b082f21a47a4a1f1bee5b2444892ac8e44e818d2` در Run 356
(`36406178932`) هر هشت Job را پاس کرد. Room مجوز مؤثر `canConvert`
را در مرز عضویت/Permission می‌دهد؛ فقط دارندهٔ مجوز، تبار خواندنی و
محدود شش نوع مقصد رسمی همان پیام را می‌بیند؛ 403 نما را می‌بندد. این مرحله
هیچ رکورد رسمی نمی‌سازد. Exact next `UX2-MS23` تبدیل تأییدشده به Action
با مسئول خود کاربر است؛ پنج مقصد دیگر/پیوست‌ها و Gateهای UX2 بازند.

UX2-MS23 روی source `3ff5a49d9478a21ec6d2d2481c7af5c5c3caf80b` / tree
`c8cbca067b87126ea100d085c52f05d23340fed6` در Run 359
(`36416141135`) هر هشت Job را پاس کرد. Room قابلیت Action را تنها با
مجوز تبدیل و ساخت Action می‌دهد؛ فرم تأییدشده با مسئول خود کاربر، مهلت
شمسی، Revision و هویت پایدار درخواست، تبار را پیش و پس از ساخت کنترل
می‌کند. Conflict تأیید دوباره، نتیجهٔ نامعلوم Retry با همان هویت و 403
بستن نما را الزام می‌کند. هیچ سندی خودکار منتقل نمی‌شود. Run 358 به دلیل
ابهام انتخاب‌گر تست مرورگر واجد Checkpoint نبود و در Run 359 اصلاح شد.
Exact next `UX2-MS24` تبدیل تأییدشدهٔ Issue است؛ چهار مقصد دیگر، پیوست‌ها
و Gateهای UX2 بازند.

UX2-MS24 روی source `677176ec198fe8aca275e84c4a276a132a9609a3` / tree
`f4b805cf9268256b5f722f7791db982f2d62c36a` در Run 361
(`36422098823`) هر هشت Job را پاس کرد. Room مجوز Issue را از ترکیب
`collaboration.convert` و `issues.create` می‌دهد؛ مسئلهٔ عمومی با مسئول
خود کاربر، طبقه‌بندی/مهلت شمسی، تأیید صریح و هویت پایدار ساخته می‌شود.
سرور تحت قفل پیام، Issue دوم همان پیام را رد می‌کند؛ Conflict، Reload و
403 در مرورگر واقعی تأیید شدند. Exact next `UX2-MS25` تبدیل RFI است؛
Daily Fact، Evidence، Technical Document، پیوست‌ها و Gateهای UX2 بازند.

UX2-MS25 روی source `13a192fe490702a3e3632d52637baa11493e922a` / tree
`02272e93bdc375790950db95e0ac60063c40896a` در Run 363
(`36424473183`) هر هشت Job را پاس کرد. Room مجوز RFI را از ترکیب
`collaboration.convert` و `technical.rfis.create` می‌دهد؛ فرم تأییدشده
پیش‌نویس رسمی را با پرسش فنی، مخاطب، رشته، اثر احتمالی، مانع بودن و موعد
اختیاری شمسی می‌سازد. یک RFI به‌ازای پیام تحت قفل سروری تضمین شده،
هویت Retry پایدار و 403 قطع دسترسی را می‌بندد. Exact next `UX2-MS26`
تبدیل Daily Fact است؛ Evidence، Technical Document، پیوست‌ها و Gateها بازند.

UX2-MS26 روی source `a37e61313f775da914909f2db96361b2a89c1a23` / tree
`2b182cd96807f68a478eea73f22b3f383c94d538` در Run 365
(`36426748297`) هر هشت Job را پاس کرد. Room مجوز Daily Fact را از ترکیب
`collaboration.convert` و `field.daily-reports.capture` می‌دهد؛ فرم تأییدشده
فقط به گزارش روزانهٔ Draft و Location فعال همان پروژه می‌افزاید و نوع و
فیلدهای ساختاری واقعیت را می‌گیرد. Revision گزارش و پیام مستقل کنترل می‌شوند،
تعارض بازخوانی و تأیید دوباره می‌خواهد، Retry هویت پایدار دارد و سرور زیر
قفل پیام واقعیت دوم همان پیام را رد می‌کند. مرورگر Reload و 403 را تأیید کرد.
Exact next `UX2-MS27` تبدیل فایل Released به Evidence است؛ Technical Document،
سایر پیوست‌ها و Gateهای UX2 بازند.

UX2-MS27 روی source `41d610a5ffea4995a91389407be36958a1e71c0a` / tree
`ee0e23ca1d308bfb2eaf7a7e948c9b18f253720c` در Run 367
(`36428852963`) هر هشت Job را پاس کرد. Room مجوز Evidence را از ترکیب
`collaboration.convert`، `evidence.upload` و `documents.read` می‌دهد.
یک فایل Released متصل به همان پیام، با نسخه و SHA-256، برای گزارش روزانه
و Fact اختیاری همان پروژه پس از تأیید صریح به Evidence رسمی تبدیل می‌شود.
سرور زیر قفل پیام تبدیل دوبارهٔ همان فایل به Evidence را رد می‌کند و پاسخ
Hash/نسخه را با منبع تطبیق می‌دهیم. مرورگر Conflict، Reload و 403 را تأیید کرد.
Exact next `UX2-MS28` تبدیل Technical Document است؛ سایر پیوست‌های تبدیل
و Gateهای UX2 بازند.

UX2-MS28 روی source `6e7de6864669c66c4ae9043897aa003ab57dd414` / tree
`47636c7b609d5fd1196af5d4258379e7bbaab850` در Run 369
(`36430874696`) هر هشت Job را پاس کرد. Room مجوز ساخت سند، ساخت Revision
و خواندن فایل را با مجوز تبدیل ترکیب می‌کند؛ یک فایل Released متصل به پیام
با عنوان، نوع، رشته و کد نسخهٔ تأییدشده به Technical Document رسمی با
Revision کاری Draft تبدیل می‌شود. SHA-256 و نسخهٔ فایل در lineage و پاسخ
کنترل می‌شوند؛ سرور زیر قفل پیام سند فنی دوم از همان فایل را رد می‌کند.
مرورگر Conflict، Reload و 403 را تأیید کرد. Exact next `UX2-MS29` انتخاب
صریح پیوست‌های Released برای تبدیل Action است؛ Issue/RFI، مهاجرت و Gateها بازند.

UX2-MS29 روی source `bd6c8ce271ae28c5a1ebd47c4f2f0bf859b460c4` / tree
`a1a3e0382d344b75adbd3d52f7256946cf8b46e9` در Run 371
(`36432927789`) هر هشت Job را پاس کرد. در فرم Action، فایل‌های Released
همان پیام تنها با انتخاب صریح کاربر و حداکثر ده شناسه به فرمان تبدیل می‌روند؛
بدون انتخاب، تبار فایل خالی می‌ماند. پاسخ از نظر شناسه، SHA-256، نسخه،
نام، MIME و اندازه با انتخاب تطبیق دارد. Conflict فایل‌ها و تبار را دوباره
می‌خواند و در صورت تغییر منبع تأیید تازه می‌خواهد؛ Retry نتیجهٔ نامعلوم
هویت قبلی را حفظ می‌کند. مرورگر انتخاب، Conflict، Reload و 403 را تأیید کرد.
Exact next `UX2-MS30` انتخاب اختیاری پیوست برای تبدیل Issue است؛ RFI،
مهاجرت و Gateهای بصری بازند.

UX2-MS30 روی source `02823eee601aba7a60b3772202e091f4f9688380` / tree
`fae7baa2e64a49601a822f6c637e581062754f9c` در Run 373
(`36435409906`) هر هشت Job را پاس کرد. فرم Issue انتخاب صریح حداکثر ده
فایل Released همان پیام را به فرمان رسمی می‌دهد؛ هیچ فایل پیش‌فرض نیست.
مالک ActionControl شواهد مسئله را به پیام و نسخه‌اش و شناسهٔ سند با SHA-256
پیوند می‌دهد و نسخه/Hash/نام/MIME/اندازهٔ پاسخ کنترل می‌شود. Conflict فایل‌ها
و تبار را تازه می‌خواند و تأیید را پاک می‌کند؛ مرورگر انتخاب یک فایل از دو،
تعارض، Reload و 403 را تأیید کرد. Exact next `UX2-MS31` پیوست RFI است؛
مهاجرت و Gateهای بصری بازند.

UX2-MS31 روی source `63855ec910909f8cf133f36313240c0ae25a2202` / tree
`39c67b515b0102124d6788301e32325f76871167` در Run 375
(`36437725041`) هر هشت Job را پاس کرد. فرم Draft RFI انتخاب اختیاری و
صریح حداکثر ده فایل Released همان پیام را به فرمان مالک Technical Office
می‌برد؛ شماره و صدور از Browser تعیین نمی‌شوند. شواهد RFI به پیام/Revision
و سند/Hash اشاره می‌کنند و پاسخ از نظر متادیتای کامل فایل تطبیق می‌شود.
Conflict فایل‌ها/تبار را تازه می‌کند و تأیید دوباره لازم است؛ مرورگر انتخاب،
Conflict، Reload و 403 را تأیید کرد. Exact next `UX2-MS32` قرارداد مرکزی
نسخه‌دار فونت برای UI/Offline/PDF/XLSX/Print است؛ مهاجرت و Gateهای بصری بازند.

UX2-MS32 روی source `07d7f2e2c4bf9eea8e47b636c5dacd6f6e9bb9ea` / tree
`92abf70a377c0fba939f6ffc145d861b09d9b4ff` در Run 377
(`36440859534`) هر هشت Job را پاس کرد. Manifest نسخه‌دار فونت، CSS رابط فعال
و Offline/Print، Fontهای PDF با Hash، Family تمام XLSXها، asset خودمیزبان
و cache نسخه‌دار را به یک مسیر تولید/بررسی متصل می‌کند. فونت موجود و Goldenهای
گزارش حفظ شده‌اند؛ انتخاب فونت فارسی آینده و آزمون بصری/چاپی آن باز است.
Exact next `UX2-MS33` inventory تازهٔ صفحات/stateها و screenshot baseline
قابل بازتولید برای `VX-G1` است؛ سایر Gateهای بصری بازند.

UX2-MS33 source نخست `9a775f19bc0c093e49aab2996e47f7fd7f68491e` / tree
`243c99b185c83e16b3c992ed880f37b541790bde` در Run 379
(`36449082359`) موفق بود. Audit فعال ۱۱ Route و ۳۸ تصویرِ Chromium از stateهای
عملیاتی، موبایل/تبلت، عدم دسترسی، Offline، Loading/Failure و Featureهای خاموش
را با SHA-256 و index در Artifact ثبت کرد. بازبینی تصویر، قاب‌بندی میانی
Command Center موبایل را نشان داد؛ correction source
`bbc55ab0b290f312b36ef364acd7e857e8c2d93f` / tree
`c58a0d3db600d03d2c853e50cb7e5acf5bd9c1bc` نقطهٔ اسکرول سه viewport
را pin کرد. Run 380 (`36450124654`) موفق و Artifact جدید ۳۸تایی بازبینی شد.
Qualification Report هفت suite را `passed` ثبت کرد؛ `VX-G1` و Gateهای
مهاجرت/Qualification بازند. Exact next `UX2-MS34` رفع محدود Sidebar دسکتاپ و
ناوبری responsive بر پایهٔ Evidence است.

UX2-MS34 source Candidate `5692956280215096f6d4cc7f148053ba050b73e5` / tree
`a86249db00b2b04f9c50ce5e6b1cc9cef929c5e4` در Run 382 به‌علت assertion
نادرست focus-visible رد شد. correction `ab753ad921cc110275babd705cd597fef2116f6e`
/ tree `d284a585fc1e38b6e68f5965b0d569bd955efb17` با Tab واقعی در Run 383
overflow عرض ۳۲۰ را آشکار کرد. correction `5000954c5e8c180f272c1f951f1c56d19cd8b8c3`
/ tree `501b9540ba66cfa3458c2897def1934d97d31bba` track Grid باریک را
محدود کرد اما Run 384 هنوز ۲px overflow داشت. correction
`b16ed8e8a44d53b7b3989c61fd7fbaa7d6ce9b79` / tree
`7ebd00cdf57719c3836f0380d94bec9d66825511` حداقل عرض Workspace را صفر و
تشخیص دقیق overflow را افزود؛ Run 385 برچسب `.section-note` با متن غیرقابل‌شکست
را عامل ۲px overflow یافت. correction `0dab8c390a01da1b03f6e79da368b086353d9e98`
/ tree `bcebb8704c3bbf60b6fa0ca869793dce5836737a` این برچسب را در عرض باریک
می‌شکند و Run 386 هر هشت Job را پاس کرد. Artifact ۳۹ تصویر با SHA-256 و
قاب‌های 04/39/21 بازبینی شد؛ CI مستندات شرط اعتبار Checkpoint است. در Sidebar
دسکتاپ، نشان ثابت و اسکرول مستقل جای فشردگی را می‌گیرد و آخرین لینک با focus
و outline در دسترس است؛ viewport ۳۲۰ پیکسل و تصویر ۳۹ انتهای ناوبری به
baseline افزوده شده‌اند. Gateهای `VX-G1/G3/G4/G5` بازند؛ بعد از Full CI و
بازبینی Artifact، MS35 به state تعارض ویرایش Chat گروه پروژه می‌پردازد؛ Preview
تکثیر و Login failure در Micro-Stepهای مستقل بعدی‌اند. INT1/QA1 طبق ترتیب بعد از UX2 هستند.

UX2-MS34 documentation `2c5154a1f743902effab1e964038bf1e8a53eced` در Run 387
هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS34-C1` Safe Checkpoint شد. UX2-MS35
source Candidate `31d6ea01515e49e8b9974f30768afcaeb4650a65` / tree
`6369a120cdcd3bf349015065cb9e82d2d3866911` تصویر 40 تعارض Revision
ویرایش پیام گروه پروژه را در E2E موجود ثبت می‌کند: نسخهٔ سرور و پیش‌نویس
نویسنده هم‌زمان دیده می‌شوند و مسیر تأیید دوباره پس از capture ادامه دارد.
Run 388 هر هشت Job را پاس کرد و Artifact چهل‌تصویری با index/SHA و تصویر 40
بازبینی شد؛ CI مستندات هنوز شرط اعتبار Checkpoint است. Exact next پس از Gateهای MS35،
`UX2-MS36` Preview تکثیر و مانع اجرایی در baseline است. `VX-G1/G3/G4/G5`
و INT1/QA1 همچنان بازند.

UX2-MS35 documentation `7270f8e3dd7c164788019c003f7a56f5a293ba85` در Run 389
هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS35-C1` Safe شد. UX2-MS36 Source
Candidate `e99fc97f8d877a180e01f0711c1ceefd6953e078` / tree
`9813c31337f6985f38da01c5d1c110de1b266933` یک Capture از Preview
تکثیر با Added/Conflict/Blocked، تأیید صریح و دکمهٔ اجرای غیرفعال دارد. Fixture
فقط پاسخ مرورگر را کنترل می‌کند و به API واقعی برای ساخت مقصد یا اجرا درخواست
نمی‌زند. Run 390 در UI-E2E روی انتظار دکمهٔ Wizard شکست خورد. Run 391
گذار مرحله‌ها را Assert کرد و Run 392 نشان داد Preview پیش از Submit صریح
آزمون ساخته می‌شد؛ قاب خطا خود state درست را آشکار کرد. Correction نهایی
`9202660180e48a0124130123ae43fc1d6821597c` / tree
`6f9242e1063d8448091f958e0e7eb5b36c0530a9` از Navigation مستقل
به مرحلهٔ اعضا می‌رود و صفر بودن درخواست ساخت را پیش از Submit کنترل می‌کند.
Run 393 هر هشت Job سبز شد و Artifact ۴۱تصویری با SHA/ابعاد معتبر و قاب 41
بازبینی شد؛ CI مستندات شرط اعتبار Checkpoint است. بعد از Gateهای MS36،
MS37 به خطای Login می‌پردازد. `VX-G1/G3/G4/G5` و INT1/QA1 باز می‌مانند.

UX2-MS36 documentation `6146ab37cca599f9e01e80fa633d94da32bce741` در Run 394
هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS36-C1` Safe شد. UX2-MS37 Source
Candidate `32d6f6bda739f399d7df5689db920cabd9acb0b9` / tree
`f147bd703c2ad9c8fc8399a1995240a093e642ee` قاب 42 را برای خطای
callback هویت در Login پیش از ورود واقعی OIDC افزوده است. پیام فارسی
`role=alert` و دکمهٔ ورود دوباره در همان setup کنترل می‌شوند؛ Feature Flag
یا Authentication flow تغییر نمی‌کند. Run 395 در Setup UI-E2E به‌دلیل
Locator عمومی `role=alert` با route announcer خالی Next.js شکست خورد؛ correction
`20e4a0e2ab64eb8893311cfc88daac6a60d07712` / tree
`3c29b376e3c743e19d92276c13f86e6994e3ee90` Assertion را به Alert
کارت Login محدود می‌کند. Run 396 هر هشت Job سبز شد و Artifact ۴۲تصویری با
SHA/ابعاد معتبر و قاب 42 بازبینی شد؛ CI مستندات شرط اعتبار Candidate است.
گام بعدی MS38، baseline چاپ و Print Preview است؛
`VX-G1/G3/G4/G5` و INT1/QA1 باز می‌مانند.

UX2-MS37 documentation `796ea6f6dd5c6b6807aa705bc38e62647a0e1a6e` در Run 397
هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS37-C1` Safe شد. UX2-MS38 Source
`8cdad07eb97a67f81e46f3e4bb0e34b0ace8a84b` / tree
`9835d0b8eff3d5f0f0ccf2a0726addb5cba738cc` baseline را به ۴۳ Capture
و یک PDF A4 چاپ مرورگر رساند. Run 398 هر هشت Job را پاس کرد؛ Artifact
`10990673834` با index و SHA/ابعاد تمام ۴۳ تصویر و PDF تطبیق شد. PDF ۲۲صفحه‌ای
در صفحهٔ نخست Navigation بریده و فرم‌های تعاملی دارد؛ این Evidence شکاف چاپ
است و Print System یا خروجی رسمی نیست. CI مستندات شرط پذیرش MS38 است. Exact
next پس از Gateهای MS38، `UX2-MS39` بازبینی یکپارچهٔ inventory و Evidence
`VX-G1` است؛ `VX-G1/G3/G4/G5` و INT1/QA1 باز می‌مانند.

UX2-MS38 documentation `5efed8b2260ac169109efcf982dc06fa5319aed2` در Run 399
هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS38-C1` Safe شد. UX2-MS39 Source
`57d33dc05900f1a0a7994817b7cb84dabe34377e` / tree
`ea1bc9ca4259ea461c4ac2853df1a1401ec71b9a` کنتراست کارت حساب Wizard
را با Tokenهای متن خوانا اصلاح کرد و E2E نسبت حداقل ۴٫۵ به ۱ را کنترل می‌کند.
Run 400 هشت Job سبز و Artifact `10992023150` با ۴۳ تصویر/PDF و index معتبر
است؛ قاب 19 خوانا و قاب 41 Preview مسدود همچنان حاضر است. ممیزی `VX-G1`
در `docs/ux/pmcs-v1.1-vx-g1-audit-review.md` پوشش ۱۱ Route، ۳۰ State و
Gapهای اولویت‌دار را ثبت کرد؛ CI مستندات شرط پذیرش Gate است. Exact next
`UX2-MS40` FileInput فارسی/دسترس‌پذیر در مدیریت ظاهر Login است؛ `VX-G3/G4/G5`
و INT1/QA1 باز می‌مانند.

UX2-MS39 documentation `f78a413038f650d215c131984823a03f90814ace` در Run 401
هر هشت Job را پاس کرد؛ `PMCS-V1.1-UX2-MS39-C1` Safe و `VX-G1 Audit Complete`
با inventory ۱۱ Route/۳۰ State و Gap ledger پذیرفته شد. UX2-MS40 Source
`9ea2a44f1574a6045ce47cecf51fbcf3d6c7e4e8` / tree
`87cf2e83b4eacb40cb0ce79d7844cf4dbb0d4700` FileInput فارسی را در
مدیریت ظاهر Login به UI متصل و حالت انتخاب فایل را با Capture 44 ثبت کرد.
Run 402 هر هشت Job سبز و Artifact `10993615536` با ۴۴ PNG/PDF و index معتبر
است؛ قاب‌های 18/44 بازبینی شدند. CI مستندات شرط پذیرش MS40 است. Exact next
`UX2-MS41` affordance پیمایش Navigation موبایل در Shell مشترک است؛
`VX-G3/G4/G5`، انتخاب فونت و Qualification چاپ بازند.

UX2-MS40 documentation `41077ad5ed9428d6a623f3d6859cd31035474345` در
Run 403 هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS40-C1` Safe شد. Source
UX2-MS41 `d76a8645d9e784b71c2a167aa04d315f871762d7` / tree
`d90dc6ec188cec7ac52e39ed768641fc60c50181` راهنمای فارسی برای پیمایش
افقی Navigation را در شش Shell فعال در اندازهٔ موبایل نشان می‌دهد. آزمون
مرورگر نمایش راهنما، رسیدن keyboard به پیوند دورتر و نبود overflow کل سند
در ۳۲۰ پیکسل را کنترل می‌کند. Run 404 هر هشت Job سبز و Artifact
`10993184623` با ۴۴ PNG/PDF و index معتبر دارد؛ قاب‌های 20/21/22/23
بازبینی شدند. CI مستندات شرط پذیرش MS41 است. Exact next `UX2-MS42` Skeleton صادق برای
Loading Portfolio است؛ `VX-G3/G4/G5`، فونت تازه و Qualification چاپ بازند.

UX2-MS41 documentation `f02abcd28debc68fb63826c2850926d4e98db59b` در
Run 405 هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS41-C1` Safe شد. Source
UX2-MS42 `41b5eeb06dedab3e9d7e4b2f7ee52ffeba8e44af` / tree
`010814c9f03923dad2189e238b549dcfedd84dfe` فضای Loading اولیهٔ
Portfolio را با Placeholderهای خنثای KPI و بخش محتوا پر می‌کند. آن‌ها
`aria-hidden` هستند و هیچ عدد، Fact یا وضعیت پروژه نمی‌سازند؛ متن زندهٔ
Loading دست‌نخورده است. E2E قاب 26 را می‌گیرد و در خرابی سرویس، حذف
Placeholder و پیام خطای واقعی را کنترل می‌کند. Run 406 هشت Job سبز و
Artifact `10995736502` با ۴۴ PNG/PDF و index معتبر دارد؛ قاب‌های 26/27
بازبینی شدند. CI مستندات شرط پذیرش MS42 است. Exact next `UX2-MS43` قرارداد Print System
و نمونهٔ چاپ محدود مرکز فرمان است؛ `VX-G3/G4/G5`، فونت و Qualification بازند.

UX2-MS42 documentation `401f1356f659c5c489c5fd3f626adc8865a3b723` در
Run 407 هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS42-C1` Safe شد. Source
UX2-MS43 `f9847db38e79bce7c7986f61fcdc5a628a407422` / tree
`a60ff1a03da6c78c69fa9ef3a0623244a24a83fa` برای چاپ مرورگر مرکز
فرمان یک برگهٔ محدود فقط با تصویر وضعیت مجاز، منبع و هشدار تازگی ایجاد
می‌کند. Navigation و فرم‌ها در media چاپ پنهان‌اند؛ نبود Snapshot به KPI
ساختگی تبدیل نمی‌شود. `PMCS-UX-PRINT-001` مرز این نمونه با PDF/XLSX رسمی
Reporting و معیارهای A4/Golden/فونت را نسخه‌دار می‌کند. Run 408 هر هشت
Job سبز و PDF یک‌صفحه‌ای A4 داشت؛ بازبینی محتوایی، پیام دریافت تصویر رسمی
را هم‌زمان با نبود Snapshot یافت. Correction
`ce179fd6ffadc0f90a203c522dbea1c106e67765` / tree
`ca08e1894453d922bf7885b4c52bc690a1385b75` پیام را اصلاح و E2E
حالت بدون Snapshot را Assert می‌کند. Run 409 هشت Job سبز و Artifact
`10996804117` با ۴۴ PNG/PDF و index معتبر دارد. PDF یک صفحهٔ A4 با متن
فارسی قابل استخراج، بدون ناوبری/فرم و پیام متناقض است؛ قاب 43/PDF بازبینی
شدند. CI مستندات شرط پذیرش MS43 است. Exact next `UX2-MS44` بستهٔ Prototype/Review
برای `VX-G3` و مقایسهٔ فونت Web/Print است؛ `VX-G3/G4/G5` بازند.

UX2-MS43 documentation `6e94fcd861a01e7ac2ab4633942f1d57d3c68d57`
در Run 410 هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS43-C1` Safe شد.
MS44 Prototype مستقل را با ۱۰ سناریو × ۷ حالت، سه قلم و خروجی مقایسه‌ای
Desktop/Tablet/Mobile/A4 به `docs/ux/prototypes/ms44/` افزود. فایل‌های
WOFF2 دو قلم با منشأ، مجوز OFL و SHA ثابت فقط در Review هستند؛ انتخاب قلم
و manifest تولیدی باز مانده‌اند. Source initial
`c58bf12830ab1bd36c5197cd5309860d8ac4c30d` / tree
`ab824ee527b3e156b3beea51f746a8b9da4b0fe1` در Run 411 هشت Job
سبز و Artifact معتبر داشت، ولی بازبینی تصویر لوگوی کم‌خوانا را یافت.
Correction `efba632036098ca3e5dabab81ba5770c3561929e` / tree
`6e6de8594fda086a8b8ce0a6c12bcccac02b38f0` سطح روشن نشان، نماد رسمی
Mobile و شواهد Tablet را افزود. Run 412 (`36485197274`) هشت Job سبز و
Artifact `10998892196` با ۱۱ فایل و PDFهای A4 یک‌صفحه‌ای معتبر و بازبینی‌شده
دارد؛ CI مستندات شرط پذیرش MS44 است. Exact next `UX2-MS45` تمرین PDF/XLSX دو قلم و تکمیل Stateهای
Component برای `VX-G3` است؛ `VX-G3/G4/G5` همچنان بازند.

UX2-MS44 documentation `f964695c288bf72d12ab5332f385f6643c4e4f4b`
در Run 413 هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS44-C1` Safe شد.
MS45 source `43d263d836f719278b49ef4e983e36feda83f1da` / tree
`2deffd12cff01d5c4f88ab6170b98d91477b4a16` چهار TTF Regular/Bold
دارای مجوز OFL و Hash ثابت را برای دو قلم فقط در Review افزود. فرآیند QA
مجزا یک نمونهٔ QuestPDF و XLSX راست‌به‌چپ برای هر قلم می‌سازد؛
`CertifiedPdfRuntime`، manifest مرکزی، Permission و defaults تغییر نمی‌کنند.
Run 414 هشت Job سبز و Artifact `10999743061` با شش خروجی معتبر دارد؛ دو
PDF یک‌صفحه‌ای A4/PNG بازبینی بصری، XLSX با Parser مستقل و Style/RTL
بررسی شد. CI مستندات شرط پذیرش MS45 است. Exact next `UX2-MS46` ماتریس
Stateهای Component مشترک و اصلاح محدود شکاف‌ها است؛ انتخاب قلم، مهاجرت
تولیدی، `VX-G3/G4/G5` و Goldenهای رسمی بازند.

UX2-MS45 documentation `92cee0a9273f025f9f7cdba2709b9f3ae951fc0c`
در Run 415 هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS45-C1` Safe شد.
MS46 source `4d320c13e3a6f66c6c7d7d836891fcbbe14311b3` / tree
`2984d1db45a9a466b7ea3cee31e4afea3c5fa326` ماتریس
`PMCS-UX-COMPONENT-STATES-001` و اصلاح محدود Hover دکمهٔ Disabled
را در UI فعال افزود. E2E Preview مسدود ثبات رنگ، قفل Action،
نبود Execute و قاب 45 را Assert کرد. Run 416 هشت Job سبز و Artifact
`11001141954` با ۴۵ PNG/PDF و Source/Hash/ابعاد معتبر است؛
قاب‌های 41/45 در Hover Disabled همسان و یک‌صفحه‌ای‌بودن PDF A4
بازبینی شد. CI مستندات شرط پذیرش MS46 است. Exact next `UX2-MS47` نمونهٔ
تعاملی Stateهای Foundation و آزمون Responsive است؛
`VX-G3/G4/G5`، تصمیم فونت و مهاجرت کامل بازند.

UX2-MS46 documentation `0f9f348486276dfe11294743651a7760be4aaac2`
در Run 417 هر هشت Job را پاس کرد و `PMCS-V1.1-UX2-MS46-C1` Safe شد.
MS47 source `20b52c1e8f685b4efa1155c652512f3e63100538` / tree
`b27cc8be9d669eeacac5119b574a3a964830f4e3` نمونهٔ تعاملی
Foundation Stateها را برای Action، Field، Status، Feedback و جدول
فارسی در ۹ حالت افزود. Desktop/Tablet/Mobile، Focus/Keyboard،
ARIA، ۳۲۰px و ۱۱ تصویر Hash-indexed در Run 419 با هشت Job سبز
و Artifact `11002615768` سنجیده و بازبینی شدند؛ CI مستندات Run 420
هشت Job سبز داشت و MS47 Safe شد. MS48 وزیرمتن را از manifest مرکزی
نسخهٔ `2.0.0` به Web/Offline/PDF/XLSX/Print منتقل و Goldenهای رسمی
را بازتولید کرد؛ F05 با Lineage در صفحهٔ اول، دو صفحهٔ A4 است. Source
Candidate `218dbfff78b94e8f3ce897321136544d5ef3f630` / tree
`e4fa7ec1e393966bd01a1e1dd693863e7eef29bd` در Run 425 هشت Job
سبز و Golden رسمی معتبر داشت؛ CI مستندات Run 426 نیز هشت Job سبز و
MS48 Safe شد. MS49 Source Candidate `7336fcae49505f0edfb78842316ba2bcf96b3094`
/ tree `c66bb5b5832ff596c8d19be569397dc412610df4` نمونهٔ مستقل
Navigation موبایل، Feedback داده/Permission/Offline و Dialog تأیید
نسخه را با E2E/قاب‌های سه اندازه می‌سازد؛ Run 429 هشت Job سبز و
Artifact ۱۱قابی بازبینی‌شده دارد؛ Run 430 مستندات هر هشت Job سبز و
MS49 Safe شد. MS50 Source `2c2d2848dc5a174982daff2331d3f4093aee8ab5`
بستهٔ مرور ده قاب دست‌نخورده از UI فعال و Prototypeهای MS44/47/49 را
با SHA/Artifact/Commit و لینک نمونه‌ها می‌سازد؛ Run 431 هشت Job سبز و
Artifact `11005871839` با سه قاب معتبر/بازبینی‌شده دارد، CI مستندات شرط
Checkpoint است. Exact next `UX2-MS51` تکمیل محدود شکاف‌های باقیماندهٔ
Component/State برای بازبینی مالک `VX-G3` است؛
`VX-G3/G4/G5` هنوز بازند.

MS50 documentation Run 432 هشت Job سبز و Safe شد. MS51 Source
`215853f91c2bb9f4b2462ac4facc54520e65a1be` Prototype مستقل
Popover/Drawer/Toast و ردیف‌های موبایل را در هشت وضعیت داده با
Keyboard/Focus و E2E سه اندازه افزود. Run 433 هشت Job سبز و Artifact
`11005944021` با ۱۲ قاب معتبر/بازبینی‌شده دارد؛ CI مستندات شرط
Checkpoint است. Exact next `UX2-MS52` تکمیل محدود نمونهٔ
Login/Shell/Chart و شواهد Responsive/Keyboard برای مرور مالک
`VX-G3` است؛ `VX-G4/G5` و INT1/QA1 بازند.

MS51 documentation Run 434 هشت Job سبز و Safe شد. MS52 Source
`07e1b5d8cc9eac6dc1da30df9ce0eddd8fd56338` نمونهٔ Login/Shell/
Chart با نمودار و جدول جایگزین، وضعیت‌های NoData/Loading/Error/
Permission/Offline و Navigation موبایل ساخت. Run 435 هشت Job سبز
و Artifact `11007206344` با ۹ PNG و PDF A4 تک‌صفحه‌ای معتبر/
بازبینی‌شده دارد؛ CI مستندات شرط Checkpoint است. Exact next
`UX2-MS53` تکمیل بستهٔ مرور منبع‌دار و معیار تصمیم مالک برای `VX-G3`
است؛ مهاجرت `VX-G4`، Qualification `VX-G5` و INT1/QA1 بازند.

MS52 documentation Run 436 هشت Job سبز و Safe شد. MS53 در
`docs/ux/review/ms53/` شواهد UI فعال MS50 و Prototypeهای MS49/51/52
را با Manifest منشأ، لینک تعاملی و چهار معیار تصمیم مالک کنار هم قرار
می‌دهد. Source Run 437 هشت Job سبز و Artifact `11017486934` با
سه قاب معتبر/بازبینی‌شده دارد. این بسته آمادهٔ تصمیم است؛ پذیرش `VX-G3` به نظر صریح مالک و
تکمیل شکاف‌های شرط Gate وابسته می‌ماند. پس از آن مهاجرت مرحله‌ای
`VX-G4`، Qualification `VX-G5` و سپس INT1/QA1 انجام می‌شوند.

MS53 documentation Run 438 هشت Job سبز و Safe شد. مالک جهت بصری را
با دو پیگیری مشخص پذیرفت، نه تمام چهار معیار یا Gate `VX-G3` را.
MS54 در Prototype مستقل، متن قابل‌نمایش را فارسی و ارتفاع موبایل را
در عرض ۳۹۰/۳۲۰px به‌ترتیب ۱۱۲/۱۱۱px کمتر کرد. Run 439 هشت Job سبز
و Artifact `11018211937` با دو قاب معتبر/بازبینی‌شده دارد. CI
مستندات Run 440 هشت Job سبز و Checkpoint MS54 Safe شد. MS55 فهرست
بسته‌شدن Contract/State برای `VX-G3` و نمونهٔ مستقل تصویر/برش محلی/
حریم خصوصی را می‌افزاید؛ Source Run 443 هشت Job سبز و Artifact
`11019328744` با ۱۳ قاب معتبر/بازبینی‌شده دارد؛ Run 444 مستندات
هشت Job سبز و Checkpoint MS55 Safe شد. MS56 نمونهٔ مستقل انتخاب،
سیاست تعارض، Preview و تأیید محلی Wizard را می‌افزاید؛ Source Run
445 هشت Job سبز و Artifact `11020397707` با ۱۴ قاب معتبر دارد؛
Run 446 مستندات هشت Job سبز و Checkpoint MS56 Safe شد. MS57 نمونهٔ
مستقل وضعیت پیوست/مدرک را با مرز صف/قرنطینه/انتشار/رد و مجوز آماده
کرد؛ Source Run 448 هشت Job سبز و Artifact `11022376179` با ۱۵ قاب معتبر/بازبینی‌شده دارد؛ Run 449 مستندات هشت Job سبز و Checkpoint MS57 Safe شد. MS58 نمونهٔ مستقل وضعیت خروجی رسمی/چاپ مرورگر و اندازه‌های A4/A3 را آماده می‌کند؛ Run 450 هشت Job سبز/Artifact معتبر اما متن فنی انگلیسی داشت؛ correction Run 451 هشت Job سبز/Artifact `11023680355` با ۱۳ PNG/۵ PDF معتبر/بازبینی‌شده دارد؛ Run 452 مستندات هشت Job سبز و Checkpoint MS58 Safe شد. MS59 نمونهٔ مفهومی هوشمندی مدیریتی را با فرضیهٔ برچسب‌دار، منشأ، تازگی، عدم قطعیت و قفل اقدام آماده کرد؛ Run 453 هشت Job سبز/Artifact `11023743292` با ۱۵ قاب معتبر دارد؛ Run 454 مستندات هشت Job سبز و Checkpoint MS59 Safe شد. MS60 نمونهٔ ده وضعیت فرم شمسی/فیلتر را با State truth آماده کرد؛ Run 455 هشت Job سبز ولی قاب ۳۲۰px متن فنی انگلیسی داشت؛ correction Run 456 هشت Job سبز/Artifact `11025098337` با ۱۶ قاب معتبر و بازبینی‌شده دارد؛ Run 457 مستندات هشت Job سبز و Checkpoint MS60 Safe شد. MS61 ممیزی کنتراست/حرکت و بستهٔ تصمیم چهار معیار را Candidate کرد؛ Runهای 458/459 در آزمون نمایش معادل سفید شکست خوردند؛ اصلاح متن فارسی و آزمون در Run 460 هشت Job سبز ولی پیام بالای بسته مخفف انگلیسی داشت؛ اصلاح Run 461 هشت Job سبز/Artifact `11027117283` با سه قاب معتبر و بازبینی‌شده؛ Run 462 مستندات هشت Job سبز و MS61 Safe شد. MS62 چاپ/تراکم را با ۴۸ ردیف ساختگی، صفحه‌بندی نمایش و چهار چاپ A4/A3 آماده کرد؛ Source Run 466 هشت Job سبز و Artifact `11029742768` با هشت PNG/چهار PDF در مجموع ۲۴ صفحه معتبر و بازبینی‌شده دارد؛ Run 467 مستندات در آزمون Focus نشست ارث‌رسیده شکست خورد؛ correction Run 468 هشت Job سبز و MS62 Safe شد. مالک پس از دریافت بستهٔ بازبینی معیار چهارم را در سطح نمونه پذیرفت. MS63 Contract/Prototype G3 را ممیزی و با CI مستقل Candidate می‌کند؛ مهاجرت
`VX-G4` پس از پذیرش Gate.

**هدف:** گفت‌وگوی گروهی عملیاتی در Context هر پروژه، بدون تبدیل PMCS به پیام‌رسان عمومی.

Scope:

- یک Room پیش‌فرض برای هر پروژه؛
- Membership مشتق‌شده از عضویت فعال پروژه و بدون Invite مستقل دورزننده؛
- Permissionهای Read، Send، Upload، Edit Own، Moderate و Convert؛
- پیام Real-time با SignalR/WebSocket و fallback/reconnect کنترل‌شده؛
- Reply، Mention، Reaction، Pin و Search؛
- Last-read cursor، unread count و اعلان Mention/Reply؛
- Attachment از Shared Document Foundation؛
- Offline queue، stable client message ID، idempotency و duplicate prevention؛
- Edit/Delete policy با history و Audit؛
- Retention، Legal Hold و Moderation؛
- Commandهای صریح تبدیل پیام/فایل به Action، Issue، RFI، Daily Fact، Evidence یا Technical Document؛
- حفظ lineage پیام، فایل، Hash، Actor و زمان در تبدیل رسمی.

مرز حقیقت:

- پیام Chat دادهٔ غیررسمی و زمینه‌ای است؛
- Chat مستقیماً Finance، Progress، Schedule، Approval یا Project State را تغییر نمی‌دهد؛
- تبدیل به رکورد رسمی Permission، Validation و Human Confirmation مستقل دارد؛
- حذف نمایشی پیام، Audit یا رکورد رسمی مشتق‌شده را حذف نمی‌کند.

خارج از Scope:

- پیام خصوصی و Direct Message؛
- تماس صوتی یا تصویری؛
- Voice Room، Story، Status و Feed عمومی؛
- Room عمومی خارج از Project Membership؛
- Bot خودمختار با حق اقدام رسمی؛
- رمزنگاری سرتاسری ناسازگار با Retention/Audit سازمانی.

**Gate خروج:** تست Real-time، reconnect، ordering، duplicate، دو کاربر هم‌زمان، عضویت/تعلیق، فایل، Offline، Search، Audit و تبدیل به رکورد رسمی.

### `V1.1-UX2` — Product UI Implementation and Migration

**هدف:** پیاده‌سازی Design System تصویب‌شده روی تمام مسیرهای فعال V1 و قابلیت‌های V1.1.

Scope:

- مهاجرت Shell و تمام shared components؛
- حذف ناهماهنگی بصری میان ماژول‌ها؛
- دسترس‌پذیری Keyboard/Focus/Contrast؛
- Responsive و density قابل کنترل برای داده‌های پرتراکم؛
- طراحی اختصاصی Chat و Reporting Center؛
- پیاده‌سازی Login نسخه‌پذیر، پروفایل شخصی و Project Duplication Wizard بر اساس Design System مصوب؛
- Visual regression در Desktop/Tablet/Mobile؛
- عدم تغییر Business Truth در جریان بازطراحی.

**Gate خروج:** Visual QA، Accessibility، RTL/Jalali، cross-browser و عدم Regression تمام Workflowهای V1.

### `V1.1-INT1` — Managerial Agent Stage 1: Intelligence Foundation

**هدف:** اجرای Stage 1 از برنامهٔ هفت‌مرحله‌ای Agent مدیریتی، بدون ادعای Read-only Agent کامل.

Scope:

- تثبیت Bounded Context مستقل `PMCS.Intelligence`؛
- Provider-independent Model Gateway و Provider abstraction؛
- Session/Request/Run lifecycle و Structured Output contract؛
- Permission-aware Tool Registry و Risk Classification؛
- Tool invocation pipeline با Permission evaluation در هر فراخوانی؛
- Audit، Correlation، Model/Prompt/Policy version و safe failure؛
- Cost/latency/usage telemetry بدون ثبت Secret یا Payload حساس؛
- ابزارهای Reference read-only برای Report Catalog، Report Status و Collaboration context مجاز؛
- negative-boundary tests برای SQL/DB، privilege escalation و cross-project access.

**Non-Scope V1.1:** Stage 2 Read-only Agent، RAG، Executive Intelligence UI تولیدی، Draft Action، Controlled Write و `@PMCS` عمومی در Chat. این قابلیت‌ها فقط با Gateهای Stageهای بعدی فعال می‌شوند.

**Gate خروج:** Provider swap contract، Tool/Permission isolation، Audit lineage، safe failure و منع DB/SQL مستقیم به‌طور مستقل اثبات شوند.

### `V1.1-QA1` — Qualification and Baseline Lock

**هدف:** بستن نسخه با Evidence، نه صرفاً سبزشدن Build.

Suiteهای الزامی افزوده بر قرارداد V1:

- Module/Manifest/Compatibility contract؛
- Permission matrix قابلیت‌های جدید؛
- Document security و malware/quarantine adapter contract؛
- Login descriptor/asset rollback، Member Profile privacy و image-security؛
- Project Bootstrap preview/execute، membership permission و operational-data non-copy contract؛
- Collaboration real-time/offline/concurrency؛
- Reporting determinism، print visual regression و export integrity؛
- UI visual/accessibility/responsive؛
- Agent Tool negative-boundary tests؛
- Migration از V1 Baseline و rollback/restore rehearsal؛
- Load/soak متناسب با Pilot و failure recovery؛
- Full Regression تمام قابلیت‌های V1.

ترتیب وضعیت:

`Planned → Architecture Approved → In Development → Feature Complete → Release Candidate → Qualified → Final → Baseline Locked`

هیچ وضعیت بعدی بدون Evidence وضعیت قبلی مجاز نیست.

## ۵.۱. برنامهٔ هفت‌مرحله‌ای Agent مدیریتی

برنامهٔ Agent یک Track مستقل و لازم‌الاجرا است و مرجع تفصیلی آن در `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md` قرار دارد.

| Stage | عنوان قطعی | Release mapping فعلی | وضعیت |
| --- | --- | --- | --- |
| 1 | Intelligence Foundation | `V1.1-INT1` | Planned |
| 2 | Read-Only Project Intelligence Agent | V1.2 Intelligence Track | Planned |
| 3 | Knowledge / RAG / Evidence / Citations | V1.2 Intelligence Track | Planned |
| 4 | Executive Intelligence UI | V1.2 Intelligence Track + Visual Excellence | Planned |
| 5 | Draft Actions | V1.2 Intelligence Track | Planned |
| 6 | Controlled Actions + Permission + Human Approval | V1.2 Intelligence Track | Planned |
| 7 | Evaluation / QA / Security / Hardening | V1.2 Qualification Track | Planned |

هیچ Stage با Stage بعدی ادغام یا با عنوان کلی «Agent integration» بسته نمی‌شود. هر Stage Definition of Ready، Gate خروج، Checkpoint Snapshot و Regression مستقل دارد.

## ۶. Roadmap PMCS V1.2

V1.2 فقط پس از قفل Baseline V1.1 آغاز می‌شود.

### Reporting Phase 2

- Report Builder کنترل‌شده برای انتخاب ستون، Filter، Group، Pivot و Chart؛
- Saved View و Template Workflow شامل Draft/Review/Publish/Retire؛
- Scheduled Report، Subscription و Delivery policy؛
- Executive Pack چندگزارشی و مقایسه دوره‌ای؛
- Word output فقط در صورت تعریف Use Case رسمی و تست Fidelity؛
- External analytics adapter فقط Read-only و Permission-scoped.

### Collaboration Phase 2

- Topic Channelهای پروژه با Policy؛
- Thread و Search پیشرفته؛
- Retention policy قابل تنظیم و Legal Hold UI؛
- Digest و summary؛
- بدون DM، Voice یا Video.

### Intelligence Integration Phase 2

V1.2 باید Stageهای 2 تا 7 برنامهٔ Agent مدیریتی را دقیقاً به‌ترتیب و با Gate مستقل اجرا کند:

1. Read-only Project Intelligence؛
2. Knowledge/RAG/Evidence/Citations؛
3. Executive Intelligence UI؛
4. Draft Actions؛
5. Controlled Actions + Human Approval؛
6. Evaluation/QA/Security/Hardening.

`@PMCS` فقط پس از Gateهای Stage 2، 3 و 4 به‌عنوان Interface کنترل‌شده در Room پروژه فعال می‌شود. Chat فقط Entry Point است؛ Executive Intelligence Center رابط اصلی باقی می‌ماند. ساخت Draft به Stage 5 و هر عملیات رسمی به Stage 6 محدود است. کل Agent فقط پس از Stage 7 Qualified محسوب می‌شود.

## ۷. Roadmap ماژول‌های PMCS V2.x

هر ردیف زیر یک Program Increment مستقل با ADR، Permission Catalog، Data Contract، Test Contract و Baseline جدا است.

| Increment | دامنه | قاعدهٔ کلیدی |
| --- | --- | --- |
| `V2.0` | PMO / Portfolio / Program Governance | تجمیع از Snapshot/Event ماژول‌ها؛ بدون Join مستقیم Persistence |
| `V2.1` | PMBOK / Process Governance | Template و Process Versioning؛ نه Hard-code کردن تمام سازمان |
| `V2.2` | Economic Feasibility | NPV/IRR/Cash Flow/Sensitivity قطعی؛ AI فقط توضیح و سناریو |
| `V2.3` | PMCS Quantify / BOQ / Estimate | Geometry/Quantity/Rate/Assembly قطعی، Evidence و Review مترور؛ بدون هزینهٔ لایسنس اجباری |
| `V2.4` | Scheduling / MSP Integration | CPM/Calendar/Logic قطعی، Import/Export نسخه‌دار و Validation |
| `V2.5` | Specialist Domain Agents | Agentهای تخصصی PMO/Feasibility/Quantify/Scheduling روی Orchestrator مدیریتی Qualified؛ نه جایگزین هفت Stage Agent اصلی |

ترتیب دقیق V2.x پس از دادهٔ Pilot و Discovery رسمی قابل بازاولویت‌بندی است؛ مرزهای معماری و Baseline آن قابل حذف نیست.

## ۸. Definition of Ready برای هر Checkpoint

قبل از کدنویسی هر Checkpoint باید موارد زیر ثبت شده باشند:

1. Scope و Non-Scope؛
2. Parent Baseline و Start Commit دقیق؛
3. ADR و مالک Bounded Context؛
4. Permission و Classification؛
5. API/Event/Data/Migration contract؛
6. Offline و conflict semantics؛
7. UX stateها و Prototype در قابلیت‌های UI؛
8. Threat/Privacy/Retention assessment؛
9. Test matrix و negative cases؛
10. Rollout، rollback و observability plan؛
11. Acceptance criteria قابل‌اندازه‌گیری؛
12. اثر بر قابلیت‌های V1 و Regression scope.

## ۹. Definition of Done برای هر Checkpoint

- Scope مصوب کامل و Non-Scope دست‌نخورده است؛
- Unit/Domain/Contract/Integration/UI تست‌های مرتبط سبز هستند؛
- Permission، Tenant و Project isolation تست منفی دارند؛
- Audit/Outbox/Idempotency/Correlation در عملیات لازم اثبات شده‌اند؛
- Migration از Baseline قبلی و Backup/Restore بررسی شده است؛
- RTL/Jalali/Responsive/Offline در صورت ارتباط کنترل شده‌اند؛
- Documentation، API contract و Runbook همگام‌اند؛
- Commit شروع و پایان، CI Run و Artifact digest ثبت شده‌اند؛
- Known limitation و Risk باقیمانده صریح است؛
- Checkpoint Snapshot ثبت می‌شود، اما تا Qualification کامل «Locked Product Baseline» نامیده نمی‌شود.

## ۱۰. ترتیب اجرایی و Dependency Map

```text
V1 Locked Baseline
  → G0 Governance
  → UX1 Visual Excellence Direction
  → EXT1 Extensibility
  → DOC1 Shared Documents
  → IAM1 Login/Profile ───────────────┐
  → PRJ1 Project Bootstrap ───────────┤
  → RPT1 Reporting Core ──────────────┼→ UX2 Product UI → INT1 Agent Stage 1 → QA1 Qualification → V1.1 Locked
  → COL1 Collaboration ───────────────┘
  → V1.2 Agent Stages 2–7 + Advanced Reporting/Collaboration
  → V2.x Domain Modules and Specialist Agents
```

IAM/Profile، Project Bootstrap، Reporting و Collaboration پس از EXT1 و DOC1 می‌توانند در شاخه‌های کاری مستقل توسعه یابند، اما ادغام آن‌ها فقط روی Integration Baseline مشترک و پس از Contract Test مجاز است.

## ۱۱. ریسک‌های اصلی و کنترل آن‌ها

| ریسک | کنترل الزامی |
| --- | --- |
| تبدیل Modular Monolith به وابستگی درهم | Manifest، contract، architecture guard و منع Persistence reference |
| تبدیل Chat به منبع تصمیم رسمی | Convert command، human confirmation، lineage و audit |
| نشت فایل یا پیام بین پروژه‌ها | Tenant/Project isolation و negative permission tests |
| گزارش زیبا ولی عدد نادرست | Semantic read model قطعی، as-of، snapshot، hash و golden test |
| بزرگ‌شدن بی‌مهار Report Builder | Phase 1 استاندارد؛ Designer پیشرفته فقط در V1.2 |
| بازطراحی ظاهری با Regression عملیاتی | migration موجی، E2E و visual regression |
| تقلیل «حرفه‌ای‌سازی ظاهر» به تغییر رنگ و CSS | Visual Excellence Program، prototype gate و مهاجرت تمام سطوح محصول |
| تزریق کد یا Asset ناسالم از تنظیمات Login | Schema بسته، Asset validation/quarantine، CSP، versioning و rollback |
| نشت اطلاعات پروفایل یا تصویر عضو | Tenant boundary، field-level permission، private object storage و privacy tests |
| انتقال اشتباه دسترسی یا دادهٔ محرمانه در Duplicate پروژه | Preview، allowlist صریح Clone contract، cross-tenant denial، Draft target و operational-data non-copy tests |
| فشرده‌شدن هفت Stage Agent و حذف Gateها | Roadmap مستقل A1–A7 و Checkpoint/Evidence جدا برای هر Stage |
| Agent دارای قدرت بیش از کاربر | Tool permission per call، risk class و human approval |
| توسعهٔ مبهم بدون نقطه بازگشت | Version/Baseline policy و checkpoint manifest |

## ۱۲. معیار تصمیم‌گیری حرفه‌ای

هر درخواست جدید قبل از ورود به Roadmap یکی از وضعیت‌های زیر را می‌گیرد:

- **تأیید:** ارزش محصول روشن، سازگار با معماری و دارای Scope قابل کنترل؛
- **تأیید مشروط:** مفید است اما نیازمند پیش‌نیاز، محدودسازی یا انتقال به نسخهٔ بعد؛
- **رد:** ناسازگار با هدف PMCS، پرریسک، تکراری یا دارای هزینهٔ بیشتر از ارزش واقعی.

موافقت مالک محصول به‌تنهایی جایگزین Architecture، Security و Qualification Gate نیست؛ همان‌طور که مخالفت فنی نیز باید با دلیل و شواهد ثبت شود.

## ۱۳. تاریخچه نسخه سند

| نسخه | تغییر |
| --- | --- |
| `1.0.0` | ایجاد Roadmap Post-V1، Collaboration، Reporting، Extensibility و Baseline governance |
| `1.1.0` | بازیابی و ثبت مستقل هفت Stage Agent مدیریتی و ایجاد Visual Excellence Program سراسری |
| `1.2.0` | تصویب مسیر «مدیریت ممتاز»، Login قابل پیکربندی، پروفایل شخصی عضو و Project Bootstrap/Duplication کنترل‌شده |
| `1.6.0` | ثبت Evidence قطعی IAM1 و فعال‌سازی PRJ1 پس از سبزشدن هشت Job CI |
| `1.7.0` | ثبت Evidence قطعی PRJ1 و فعال‌سازی RPT1 پس از سبزشدن هشت Job CI |
| `1.8.0` | ثبت ممیزی تداوم و Definition of Ready مرحله RPT1؛ بدون تغییر Runtime |
| `1.9.0` | ثبت Source Candidate اولین Slice هسته RPT1؛ بدون ادعای Qualification |
| `1.10.0` | ثبت Source Candidate دوم RPT1 برای Generated Document، PDF/XLSX، Download/Verify و هارنس متصل؛ Gate خروج همچنان باز |
| `1.11.0` | ثبت Evidence متصل Run 99 برای Build، PostgreSQL/Object Storage، Restore ۴۳ Migration و Full CI؛ Gateهای توسعه‌یافته RPT1 همچنان باز |
| `1.12.0` | ثبت Qualification Slice سوم RPT1 برای Cancel، revocation پس از success، tenant isolation و tamper fail-closed؛ Gate خروج همچنان باز |
| `1.13.0` | ثبت Qualification Slice چهارم RPT1 برای دو Worker، `SKIP LOCKED`، stale lease و crash-before/after-storage؛ Gate خروج همچنان باز |
| `1.14.0` | ثبت Qualification Slice پنجم RPT1 برای worker-time revocation، object-byte/missing/malformed integrity و orphan inventory؛ Gate خروج همچنان باز |
| `1.15.0` | ثبت Safe Checkpoint `S06-MS01` برای Core ظرفیت، timeout، retry exhaustion، fairness، telemetry و health؛ Qualification متصل load/poison/fairness در `MS02` باز است |
| `1.16.0` | ثبت Safe Checkpoint متصل `S06-MS02` برای ۲۰ Run سالم + poison، P95 و fairness دو پروژه/دو Worker؛ Operational Observability در `MS03` باز است |
| `1.17.0` | ثبت Safe Checkpoint میانی `S06-MS03-C1` برای قرارداد کم‌کاردینالیتی Meter و readiness متصل queue-age؛ exporter/scrape/alert delivery در `MS03-C2` باز است |
| `1.18.0` | ثبت Safe Checkpoint نهایی `S06-MS03-C2` برای OTLP، scrape، سه alert rule و delivery متصل queue-age؛ MS03 بسته و remediation orphan/Golden/PDF/UI باز است |
| `1.19.0` | ثبت Safe Checkpoint `S06-MS04` برای inventory/dry-run و remediation امن orphan با retention، legal hold، Audit و idempotency؛ MS04 بسته و Golden/PDF/UI باز است |
| `1.20.0` | ثبت Safe Checkpoint `S06-MS05` برای Golden معنایی cutoff و XLSX deterministic با replay، OpenXML و SQL مستقل؛ MS05 بسته و PDF/UI باز است |
| `1.21.0` | ثبت Safe Checkpoint `S06-MS06` برای تصمیم Community، pin image/font و PDF Golden/visual/performance؛ MS06 بسته و اختلاف کاتالوگ پیش از بستن RPT1 باز است |
| `1.22.0` | ثبت ADR 0031 و تصمیم صریح حفظ Scope ده‌گانه RPT1؛ F02 تا F10 با Micro-Slice مستقل الزامی‌اند و RPT1 فعال می‌ماند |
| `1.23.0` | ثبت Safe Checkpoint `S07-MS01` و Evidence سبز Run 135 برای تصمیم حفظ کاتالوگ ده‌گانه؛ F02 تا F10 همچنان بازند |
| `1.24.0` | ثبت Candidate قرارداد معنایی/DoR خانواده F02 برای گزارش هفتگی و ماهانه؛ Runtime/Renderer هنوز پیاده نشده‌اند |
| `1.25.0` | ثبت Safe Checkpoint `S07-MS02` و Evidence سبز Run 137 برای قرارداد معنایی F02؛ Runtime/Renderer باز است |
| `1.26.0` | ثبت Candidate محدود Runtime Core F02 برای identity/source/resolver/Snapshot و Unit/contract؛ API/Renderer و Qualification متصل باز است |
| `1.27.0` | ثبت Safe Checkpoint `S07-MS03` و Evidence سبز Run 139 برای Runtime Core F02؛ Renderer/Golden و wiring متصل باز است |
| `1.28.0` | ثبت Candidate محدود `S07-MS04` برای قرارداد Renderer و Golden قطعی PDF/XLSX خانواده F02؛ Catalog/API/Worker wiring و Full CI باز است |
| `1.29.0` | ثبت Safe Checkpoint `S07-MS04` و Evidence سبز Run 141 برای Renderer/Golden خانواده F02؛ Catalog/API/Worker wiring باز است |
| `1.30.0` | ثبت Source Candidate محدود `S07-MS05` برای Migration/Catalog/API/Worker و QA متصل F02؛ Full CI و Safe Checkpoint باز است |
| `1.31.0` | ثبت Safe Checkpoint `S07-MS05` و Evidence سبز Run 144 برای اتصال Catalog/API/Worker خانواده F02؛ F03 تا F10/UI/Production باز است |
| `1.32.0` | ثبت Candidate قرارداد معنایی/DoR خانواده F03 برای Executive Project State رسمی، cutoff-aware و بدون Composite Health؛ Runtime هنوز پیاده نشده است |
| `1.33.0` | ثبت Safe Checkpoint `S07-MS06` و Evidence سبز Run 146 برای قرارداد معنایی F03؛ Runtime/Renderer/wiring باز است |
| `1.34.0` | ثبت Candidate محدود `S07-MS07` برای Runtime Core F03؛ Full CI/Checkpoint و Renderer/wiring باز است |
| `1.35.0` | ثبت Safe Checkpoint `S07-MS07` و Evidence سبز Run 148 برای Runtime Core F03؛ Renderer/Golden و wiring متصل باز است |
| `1.36.0` | ثبت Candidate محدود `S07-MS08` برای Renderer contract و Golden قطعی PDF/XLSX خانواده F03؛ wiring و Full CI باز است |
| `1.37.0` | ثبت Safe Checkpoint `S07-MS08` و Evidence سبز Run 154 برای Renderer/Golden خانواده F03؛ Catalog/API/Worker wiring باز است |
| `1.38.0` | ثبت Connected Candidate `S07-MS09` برای Migration/Catalog، strict API، permissionهای definition-aware، Worker dispatch و QA متصل F03؛ Full CI و Safe Checkpoint باز است |
| `1.39.0` | ثبت Safe Checkpoint `S07-MS09` و Evidence سبز Run 156 برای اتصال End-to-End خانواده F03؛ F04 تا F10/UI/Production باز است |
| `1.40.0` | ثبت Candidate قرارداد معنایی/DoR خانواده F04 برای Baseline رسمی، Actual/Planned/Variance و S-Curve cutoff-aware؛ Runtime هنوز پیاده نشده است |
| `1.41.0` | ثبت Safe Checkpoint `S07-MS10` و Evidence سبز Run 158 برای قرارداد معنایی F04؛ Runtime/Renderer/wiring باز است |
| `1.42.0` | ثبت Candidate محدود `S07-MS11` برای Runtime Core F04؛ Full CI/Checkpoint و Renderer/wiring باز است |
| `1.43.0` | ثبت Safe Checkpoint `S07-MS11` و Evidence سبز Run 163 برای Runtime Core F04؛ Renderer/Golden و wiring باز است |
| `1.44.0` | ثبت Candidate محدود `S07-MS12` برای Renderer contract و Golden قطعی PDF/XLSX خانواده F04؛ wiring و Full CI باز است |
| `1.45.0` | ثبت Safe Checkpoint `S07-MS12` و Evidence سبز Run 167 برای Renderer/Golden خانواده F04؛ Catalog/API/Worker wiring باز است |
| `1.46.0` | ثبت Safe Checkpoint `S07-MS13` و Evidence سبز Run 169 برای اتصال End-to-End Catalog/API/Worker خانواده F04؛ F05–F10/UI/Production باز است |
| `1.47.0` | ثبت Safe Checkpoint `S07-MS14` و Evidence سبز Run 171 برای قرارداد معنایی F05؛ Runtime/Renderer/wiring و F06–F10/UI/Production باز است |
| `1.48.0` | ثبت Candidate محدود `S07-MS15` برای Runtime Core F05؛ Full CI/Checkpoint و Renderer/wiring باز است |
| `1.49.0` | ثبت Safe Checkpoint `S07-MS15` و Evidence سبز Run 175 برای Runtime Core F05؛ Renderer/Golden و wiring و F06–F10/UI/Production باز است |
| `1.50.0` | ثبت Safe Checkpoint `S07-MS16` و Evidence سبز Run 178 برای Renderer/Golden قطعی F05؛ Catalog/API/Worker wiring و F06–F10/UI/Production باز است |
| `1.51.0` | ثبت Connected Safe Checkpoint `S07-MS17` و Evidence سبز Run 185 برای اتصال End-to-End Catalog/API/Worker خانواده F05؛ F06–F10/UI/Production باز است |
| `1.52.0` | ثبت Safe Checkpoint `S07-MS18` و Evidence سبز Run 188 برای قرارداد معنایی F06؛ Runtime/Renderer/wiring و F07–F10/UI/Production باز است |
| `1.53.0` | ثبت Safe Checkpoint `S07-MS19` و Evidence سبز Run 192 برای Runtime Core F06؛ Renderer/Golden و wiring و F07–F10/UI/Production باز است |
| `1.54.0` | ثبت Safe Checkpoint `S07-MS20` و Evidence سبز Run 196 برای Renderer/Golden قطعی F06؛ Catalog/API/Worker wiring و F07–F10/UI/Production باز است |
| `1.55.0` | ثبت Connected Safe Checkpoint `S07-MS21` و Evidence سبز Run 202 برای اتصال End-to-End Catalog/API/Worker خانواده F06؛ F07–F10/UI/Production باز است |
| `1.56.0` | ثبت Safe Checkpoint `S07-MS22` و Evidence سبز Run 205 برای DoR/قرارداد معنایی مستقل F07؛ Runtime/Renderer/wiring و F08–F10/UI/Production باز است |
| `1.57.0` | ثبت Safe Checkpoint `S07-MS23` و Evidence سبز Run 211 برای Runtime Core محدود F07؛ Renderer/Golden، historical producer و wiring/F08–F10/UI/Production باز است |
| `1.58.0` | ثبت Safe Checkpoint `S07-MS24` و Evidence سبز Run 215 برای Renderer/Golden قطعی محدود F07؛ historical producer و wiring/F08–F10/UI/Production باز است |
| `1.59.0` | ثبت Safe Checkpoint `S07-MS25` و Evidence سبز Run 219 برای producer تاریخچهٔ RFI/Submittal بدون backfill؛ wiring F07 و F08–F10/UI/Production باز است |
| `1.60.0` | ثبت Connected Safe Checkpoint `S07-MS26` و Evidence سبز Run 222 برای Catalog/API/Worker و Qualification مستقل F07؛ F08–F10/UI/Production باز است |
| `1.61.0` | ثبت Safe Checkpoint `S07-MS27` و Evidence سبز Run 224 برای DoR/قرارداد معنایی مستقل F08؛ Runtime/Renderer/wiring و F09–F10/UI/Production باز است |
| `1.62.0` | ثبت Safe Checkpoint `S07-MS28` و Evidence سبز Run 231 برای Runtime Core و Source مالک F08؛ Renderer/Golden/wiring و F09–F10/UI/Production باز است |
| `1.63.0` | ثبت Safe Checkpoint `S07-MS29` و Evidence سبز Run 235 برای Renderer/Golden قطعی F08؛ Catalog/API/Worker و F09–F10/UI/Production باز است |
| `1.64.0` | ثبت Connected Safe Checkpoint `S07-MS30` و Evidence سبز Run 237 برای Catalog/API/Worker و Qualification مستقل F08؛ F09–F10/UI/Production باز است |
| `1.65.0` | ثبت Safe Checkpoint `S07-MS31` و Evidence سبز Run 239 برای DoR/قرارداد معنایی مستقل F09؛ Runtime/Renderer/wiring F09 و F10/UI/Production باز است |
| `1.66.0` | ثبت Safe Checkpoint `S07-MS32` و Evidence سبز Run 242 برای Source مالک و Runtime Core محدود F09؛ Renderer/producer/wiring F09 و F10/UI/Production باز است |
| `1.67.0` | ثبت Safe Checkpoint `S07-MS33` و Evidence سبز Run 245 برای PDF/XLSX Renderer/Golden پنج‌بخشی F09؛ producer/wiring F09 و F10/UI/Production باز است |
| `1.68.0` | ثبت Safe Checkpoint `S07-MS34` و Evidence سبز Run 248 برای owner transition producer F09 بدون backfill؛ selector/wiring F09 و F10/UI/Production باز است |
| `1.69.0` | ثبت Safe Checkpoint `S07-MS35` و Evidence سبز Run 250 برای Source selector cutoff-aware تاریخچهٔ F09؛ wiring F09 و F10/UI/Production باز است |
| `1.70.0` | ثبت Connected Safe Checkpoint `S07-MS36` و Evidence سبز Run 252 برای Catalog/API/Worker و Qualification مستقل F09؛ F10/UI/Production باز است |
| `1.71.0` | ثبت Safe Checkpoint `S07-MS37` و Evidence سبز Run 254 برای DoR/قرارداد معنایی مستقل F10؛ MS38 Tenant-scope و Runtime/Renderer/wiring بعدی باز است |
| `1.72.0` | ثبت Safe Checkpoint `S07-MS38` و Evidence سبز Run 256 برای زیرساخت واقعی Tenant-scope و Documents owner؛ MS39 Source/Runtime Core F10 باز است |
| `1.73.0` | ثبت Safe Checkpoint `S07-MS39` و Evidence سبز Run 261 برای Source مالک و Runtime Core محدود F10؛ MS40 Renderer/Golden باز است |
| `1.74.0` | ثبت Safe Checkpoint `S07-MS40` و Evidence سبز Run 264 برای PDF/XLSX Renderer/Golden قطعی F10؛ MS41 Catalog/Tenant API باز است |
| `1.75.0` | ثبت Safe Checkpoint `S07-MS41` و Evidence سبز Run 266 برای Catalog/Template و Tenant API F10؛ MS42 Worker و MS43 OutputAccess/Qualification باز است |
| `1.76.0` | ثبت Safe Checkpoint `S07-MS42` و Evidence سبز Run 272 برای Worker و دو سند Tenant F10؛ MS43 OutputAccess/Qualification باز است |
| `1.77.0` | ثبت Connected Safe Checkpoint `S07-MS43` و Evidence سبز Run 275 برای F10 End-to-End؛ گام بعد COL1 طبق ترتیب Roadmap، UX2/Production باز |
| `1.78.0` | DoR مستقل COL1 و MS01 هستهٔ Room/Message با Evidence سبز Runهای 280/281؛ MS02–MS06 و UX2/Production باز |
| `1.79.0` | MS02 تعامل و read state متصل با Evidence سبز Run 283؛ MS03–MS06 و UX2/Production باز |
| `1.80.0` | MS03 live/reconnect/fallback و صف آفلاین با Evidence سبز Run 286؛ MS04–MS06 و UX2/Production باز |
| `1.81.0` | MS04 سند Chat و حاکمیت پیام با Evidence سبز Run 294 و ۶۰ Migration؛ MS05–MS06 و UX2/Production باز |
| `1.82.0` | MS05 شش تبدیل مالک رسمی با Evidence سبز Runهای 297/299/301 و ۶۳ Migration؛ MS06 و UX2/Production باز |
| `1.83.0` | MS06 Qualification متصل و پایان ساخت COL1 با Run 304 و Restore ۶۳ Migration؛ UX2/INT1/QA1 و Production باز |
| `1.84.0` | UX2-MS01 نشان رسمی در Shell/Login با Run 306 و هشت Job سبز؛ MS02 و Gateهای Design System/مهاجرت/Visual Qualification باز |
| `1.85.0` | UX2-MS02 Token/Focus/Reduced Motion/Mobile با Run 309 و هشت Job سبز؛ MS03 و Gateهای UX2 باز |
| `1.86.0` | UX2-MS03 رابط خواندنی Chat گروه پروژه و موبایل با Run 313 و هشت Job سبز؛ MS04 ارسال/Live/Offline و Reporting/Gateهای UX2 باز |
| `1.87.0` | UX2-MS04 ارسال/Live/بازیابی صف آفلاین Chat با Run 315 و هشت Job سبز؛ MS05 تعامل‌ها و Reporting/Gateهای UX2 باز |
| `1.88.0` | UX2-MS05 جست‌وجو/Reply/read cursor گروه پروژه با Run 317 و هشت Job سبز؛ MS06 Reporting و باقی UX2 باز |
| `1.89.0` | UX2-MS06 مرکز گزارش‌های خواندنی پروژه با Run 319 و هشت Job سبز؛ MS07 درخواست و خروجی/Gateهای UX2 باز |
| `1.90.0` | UX2-MS07 درخواست استاندارد هفت خانواده با Retry پایدار و Run 321 هشت Job سبز؛ F01/F02 و OutputAccess/UX2 باز |
| `1.91.0` | UX2-MS08 انتخاب F01 و دورهٔ شمسی F02 با Run 323 هشت Job سبز؛ OutputAccess و باقی UX2 باز |
| `1.92.0` | UX2-MS09 دانلود خروجی پروژه با تطبیق SHA-256 و Run 326 هشت Job سبز؛ Portfolio/Chat و باقی UX2 باز |
| `1.93.0` | UX2-MS10 Catalog/History گزارش سبد با Run 328 هشت Job سبز؛ درخواست/خروجی سبد و Chat/UX2 باز |
| `1.94.0` | UX2-MS11 درخواست استاندارد F10 با Retry پایدار و Run 331 هشت Job سبز؛ خروجی سبد و Chat/UX2 باز |
| `1.95.0` | UX2-MS12 دانلود F10 با تطبیق SHA-256 و Run 334 هشت Job سبز؛ Chat و باقی UX2 باز |
| `1.96.0` | UX2-MS13 واکنش‌های محدود Chat با Run 337 هشت Job سبز؛ تأیید منبع لوگو و الزام تعویض سراسری فونت ثبت، MS14 و Gateهای UX2 باز |
| `1.97.0` | UX2-MS14 سنجاق با مجوز تعدیل و Run 339 هشت Job سبز؛ MS15 پیوست خواندنی و Gateهای UX2 باز |
| `1.98.0` | UX2-MS15 فهرست و دانلود پیوست Released با تطبیق SHA-256 و Run 341 هشت Job سبز؛ MS16 آپلود محدود و Gateهای UX2 باز |
| `1.99.0` | UX2-MS16 آپلود نویسنده و وضعیت قرنطینهٔ محدود با Run 343 هشت Job سبز؛ MS17 اتصال Released و Gateهای UX2 باز |
| `1.100.0` | UX2-MS17 اتصال صریح سند Released با Run 345 هشت Job سبز؛ MS18 ویرایش پیام خود و Gateهای UX2 باز |
| `1.101.0` | UX2-MS18 ویرایش پیام خود با Revision/Conflict و Run 348 هشت Job سبز؛ MS19 حذف نمایشی و Gateهای UX2 باز |
| `1.102.0` | UX2-MS19 حذف نمایشی پیام خود با Revision/Legal Hold و Run 350 هشت Job سبز؛ MS20 تعدیل و Gateهای UX2 باز |
| `1.103.0` | UX2-MS20 Redaction و Legal Hold با دلیل/Revision و Run 352 هشت Job سبز؛ MS21 تاریخچه و Gateهای UX2 باز |
| `1.104.0` | UX2-MS21 تاریخچهٔ خصوصی Revision/تعدیل با Run 354 هشت Job سبز؛ MS22 مجوز تبدیل و lineage خواندنی باز |
| `1.105.0` | UX2-MS22 مجوز مؤثر تبدیل و lineage خواندنی با Run 356 هشت Job سبز؛ MS23 Action و Gateهای UX2 باز |
| `1.106.0` | UX2-MS23 تبدیل تأییدشدهٔ Action با Revision/Idempotency و Run 359 هشت Job سبز؛ MS24 Issue و Gateهای UX2 باز |
| `1.107.0` | UX2-MS24 تبدیل تأییدشدهٔ Issue عمومی با جلوگیری از تکرار و Run 361 هشت Job سبز؛ MS25 RFI و Gateهای UX2 باز |
| `1.108.0` | UX2-MS25 تبدیل تأییدشدهٔ پیش‌نویس RFI با جلوگیری از تکرار و Run 363 هشت Job سبز؛ MS26 Daily Fact و Gateهای UX2 باز |
| `1.109.0` | UX2-MS26 تبدیل تأییدشدهٔ Daily Fact به گزارش Draft با تعارض Revision و Run 365 هشت Job سبز؛ MS27 Evidence و Gateهای UX2 باز |
| `1.110.0` | UX2-MS27 تبدیل فایل Released به Evidence رسمی با Hash و جلوگیری از تکرار و Run 367 هشت Job سبز؛ MS28 Technical Document و Gateهای UX2 باز |
| `1.111.0` | UX2-MS28 تبدیل فایل Released به Technical Document Draft/Revision با Run 369 هشت Job سبز؛ MS29 پیوست Action و Gateهای UX2 باز |
| `1.112.0` | UX2-MS29 انتخاب صریح پیوست Released برای تبار Action با Run 371 هشت Job سبز؛ MS30 پیوست Issue و Gateهای UX2 باز |
| `1.113.0` | UX2-MS30 انتخاب صریح پیوست Released برای شواهد Issue با Run 373 هشت Job سبز؛ MS31 پیوست RFI و Gateهای UX2 باز |
| `1.114.0` | UX2-MS31 انتخاب صریح پیوست Released برای شواهد Draft RFI با Run 375 هشت Job سبز؛ MS32 قرارداد فونت و Gateهای UX2 باز |
| `1.115.0` | UX2-MS32 قرارداد نسخه‌دار فونت UI/Offline/PDF/XLSX/Print با Run 377 هشت Job سبز؛ MS33 ممیزی بصری و Gateهای UX2 باز |
| `1.116.0` | UX2-MS33 inventory یازده Route و baseline سی‌وهشت screenshot با index/SHA-256 در Runهای 379/380؛ اصلاح قاب‌بندی Command Center، MS34 و Gateهای Visual باز |
| `1.117.0` | UX2-MS34 Candidate نشان ثابت، Sidebar مستقل و keyboard reachability در ۳۹ screenshot؛ Run 386 هشت Job سبز و Artifact بازبینی‌شده، CI مستندات شرط اعتبار؛ MS35 و Gateهای Visual باز |
| `1.118.0` | UX2-MS34 documentation Run 387 هشت Job سبز و Safe؛ UX2-MS35 Candidate تصویر تعارض Chat گروه پروژه در baseline چهل‌تایی، Run 388 هشت Job سبز و Artifact بازبینی‌شده، CI مستندات شرط اعتبار، MS36 و Gateهای Visual باز |
| `1.119.0` | UX2-MS35 documentation Run 389 هشت Job سبز و Safe؛ UX2-MS36 Preview مسدود تکثیر در baseline چهل‌ویک‌تایی، Runهای 390–392 علت گذار آزمون را آشکار و correction Run 393 هشت Job سبز/Artifact بازبینی‌شده، CI مستندات شرط اعتبار، MS37 و Gateهای Visual باز |
| `1.120.0` | UX2-MS36 documentation Run 394 هشت Job سبز و Safe؛ UX2-MS37 Candidate خطای callback هویت در baseline چهل‌ودوتایی، Run 395 Setup assertion شکست و correction Run 396 هشت Job سبز/Artifact بازبینی‌شده، CI مستندات شرط اعتبار، MS38 Print و Gateهای Visual باز |
| `1.121.0` | UX2-MS37 documentation Run 397 هشت Job سبز و Safe؛ UX2-MS38 Candidate چاپ مرورگر با ۴۳ PNG و PDF A4 بیست‌ودوصفحه‌ای، Run 398 هشت Job سبز/Artifact بازبینی‌شده، CI مستندات شرط اعتبار، MS39 و Gateهای Visual باز |
| `1.122.0` | UX2-MS38 documentation Run 399 هشت Job سبز و Safe؛ UX2-MS39 اصلاح کنتراست کارت حساب Wizard و جمع‌بندی ممیزی ۱۱ Route/۳۰ State/۴۳ تصویر، Run 400 هشت Job سبز/Artifact بازبینی‌شده، CI مستندات شرط اعتبار VX-G1، MS40 FileInput و Gateهای G3/G4/G5 باز |
| `1.123.0` | UX2-MS39 documentation Run 401 هشت Job سبز و VX-G1 ممیزی پذیرفته؛ UX2-MS40 FileInput فارسی با Capture 44، Run 402 هشت Job سبز/Artifact بازبینی‌شده، CI مستندات شرط اعتبار، MS41 Navigation موبایل و Gateهای G3/G4/G5 باز |
| `1.124.0` | UX2-MS40 documentation Run 403 هشت Job سبز و Safe؛ UX2-MS41 Navigation موبایل در شش Shell و آزمون keyboard/۳۲۰ پیکسل، Run 404 هشت Job سبز و Artifact ۴۴تایی بازبینی‌شده؛ CI مستندات شرط اعتبار، MS42 Loading صادق و Gateهای G3/G4/G5 باز |
| `1.125.0` | UX2-MS41 documentation Run 405 هشت Job سبز و Safe؛ UX2-MS42 Skeleton خنثای Loading سبد بدون Fact ساختگی، Source Run 406 هشت Job سبز و Artifact ۴۴تایی بازبینی‌شده؛ CI مستندات شرط اعتبار، MS43 Print System و Gateهای G3/G4/G5 باز |
| `1.126.0` | UX2-MS42 documentation Run 407 هشت Job سبز و Safe؛ UX2-MS43 برگهٔ چاپ محدود/قرارداد Print System، Run 408 متن NoSnapshot را آشکار و correction Run 409 هشت Job سبز/PDF یک‌صفحه‌ای بازبینی‌شده؛ CI مستندات شرط اعتبار، MS44 Prototype/Font review و Gateهای G3/G4/G5 باز |
| `1.127.0` | UX2-MS43 documentation Run 410 هشت Job سبز و Safe؛ UX2-MS44 Prototype مستقل با ۱۰ سناریو × ۷ حالت و مقایسهٔ دو قلم OFL، initial Run 411 و correction خوانایی نشان/Tablet در Run 412 هر دو هشت Job سبز؛ Artifact ۱۱فایلی/PDF A4 بازبینی‌شده، CI مستندات شرط اعتبار، G3/G4/G5 و انتخاب فونت باز |
| `1.128.0` | UX2-MS44 documentation Run 413 هشت Job سبز و Safe؛ UX2-MS45 تمرین ایزولهٔ دو قلم TTF در QuestPDF/XLSX، Source Run 414 هشت Job سبز و شش خروجی PDF/PNG/XLSX بازبینی‌شده؛ CI مستندات شرط اعتبار، MS46 Component states و G3/G4/G5 باز |
| `1.129.0` | UX2-MS45 documentation Run 415 هشت Job سبز و Safe؛ UX2-MS46 ماتریس Component states، اصلاح Hover دکمهٔ Disabled و قاب 45؛ Source Run 416 هشت Job سبز/Artifact ۴۵قابی معتبر، CI مستندات شرط اعتبار، MS47 و G3/G4/G5 باز |
| `1.130.0` | UX2-MS46 documentation Run 417 هشت Job سبز و Safe؛ UX2-MS47 نمونهٔ تعاملی ۹ State بنیادین با E2E/Desktop/Tablet/Mobile، Source Run 419 هشت Job سبز و Artifact ۱۱قابی بازبینی‌شده، CI مستندات شرط اعتبار؛ MS48 انتخاب مستند/نسخه‌دار وزیرمتن، G3/G4/G5 باز |
| `1.131.0` | UX2-MS47 documentation Run 420 هشت Job سبز و Safe؛ UX2-MS48 وزیرمتن نسخهٔ 2.0.0 را در Web/Offline/PDF/XLSX/Print با Golden رسمی، F05 دوصفحه‌ای، مرور قاب‌های UI/چاپ و Regression متصل کرد؛ Source Run 425 هشت Job سبز، CI مستندات شرط پذیرش، MS49 State Contract و G3/G4/G5 باز |
| `1.132.0` | UX2-MS48 documentation Run 426 هشت Job سبز و Safe؛ UX2-MS49 Prototype مستقل Navigation موبایل، Feedback هفت وضعیت و Dialog تأیید نسخه با E2E و Artifact ۱۱قابی سه اندازه را Candidate کرد؛ Source Run 429 هشت Job سبز، CI مستندات شرط اعتبار، MS50 شکاف‌های Component و G3/G4/G5 باز |
| `1.133.0` | UX2-MS49 documentation Run 430 هشت Job سبز و Safe؛ UX2-MS50 بستهٔ بازبینی بصری ده قاب از چهار Artifact معتبر را با منشأ/Hash و لینک Prototypeها Candidate کرد؛ Source Run 431 هشت Job سبز/Artifact `11005871839` معتبر و بازبینی‌شده، CI مستندات شرط اعتبار، MS51 Component gaps و G3/G4/G5 باز |
| `1.134.0` | UX2-MS50 documentation Run 432 هشت Job سبز و Safe؛ UX2-MS51 Prototype Popover/Drawer/Toast و ردیف موبایل در هشت وضعیت با Browser E2E/۱۲ قاب را Candidate کرد؛ Source Run 433 هشت Job سبز و Artifact `11005944021` معتبر/بازبینی‌شده، CI مستندات شرط اعتبار، MS52 Login/Shell/Chart و G3/G4/G5 باز |
| `1.135.0` | UX2-MS51 documentation Run 434 هشت Job سبز و Safe؛ UX2-MS52 نمونهٔ Login/Shell/Chart با جدول جایگزین، حالت‌های بدون داده و PDF A4 را Candidate کرد؛ Source Run 435 هشت Job سبز، Artifact `11007206344` با ۹ PNG/PDF تک‌صفحه‌ای معتبر/بازبینی‌شده، CI مستندات شرط اعتبار، MS53 بستهٔ مرور مالک و G3/G4/G5 باز |
| `1.136.0` | UX2-MS52 documentation Run 436 هشت Job سبز و Safe؛ UX2-MS53 بستهٔ مرور منبع‌دار VX-G3 را با شواهد MS50/51/52 و چهار معیار تصمیم Candidate کرد؛ Source Run 437 هشت Job سبز، Artifact `11017486934` با سه قاب معتبر/بازبینی‌شده، CI مستندات شرط Checkpoint؛ پذیرش مالک و شکاف‌های Component شرط G3، سپس G4/G5 و INT1/QA1 باز |
| `1.137.0` | UX2-MS53 documentation Run 438 هشت Job سبز و Safe؛ مالک جهت بصری را با پیگیری تراکم موبایل و فارسی‌سازی متن پذیرفت؛ UX2-MS54 Prototype مستقل را در ۳۹۰/۳۲۰px به‌ترتیب ۱۱۲/۱۱۱px کوتاه‌تر کرد، Source Run 439 هشت Job سبز و Artifact `11018211937` با دو قاب معتبر؛ CI مستندات شرط Checkpoint، G3 Contract/State و G4/G5 باز |
| `1.138.0` | UX2-MS54 documentation Run 440 هشت Job سبز و Safe؛ UX2-MS55 فهرست بسته‌شدن G3 و نمونهٔ مستقل هشت وضعیت Avatar/برش/حریم خصوصی را Candidate کرد؛ source correctionهای یکتایی نام قاب و متن فارسی، Run 443 هشت Job سبز/Artifact `11019328744` با ۱۳ قاب معتبر؛ CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.139.0` | UX2-MS55 documentation Run 444 هشت Job سبز و Safe؛ UX2-MS56 نمونهٔ مستقل Wizard تکثیر با انتخاب/سیاست تعارض/Preview/تأیید محلی و قفل اجرای عملیاتی را Candidate کرد؛ Source Run 445 هشت Job سبز/Artifact `11020397707` با ۱۴ قاب معتبر، CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.140.0` | UX2-MS56 documentation Run 446 هشت Job سبز و Safe؛ UX2-MS57 نمونهٔ مستقل چرخهٔ پیوست و منشأ مدرک را با قفل اتصال/دریافت و مرور محلی Candidate کرد؛ Run 447 هشت Job سبز ولی index ابعاد تصویر نادرست داشت؛ Source correction Run 448 هشت Job سبز/Artifact `11022376179` با ۱۵ قاب معتبر؛ CI مستندات شرط Checkpoint، G3/G4/G5 باز |
| `1.141.0` | UX2-MS57 documentation Run 449 هشت Job سبز و Safe؛ UX2-MS58 نمونهٔ مستقل وضعیت خروجی رسمی و برگهٔ غیررسمی چاپ مرورگر را با A4/A3 عمودی/افقی Candidate کرد؛ Run 450 هشت Job سبز اما متن فنی انگلیسی داشت؛ correction Run 451 هشت Job سبز/Artifact `11023680355` با ۱۳ PNG/۵ PDF معتبر؛ CI مستندات شرط Checkpoint، Intelligence/Foundation و G3/G4/G5 باز |
| `1.142.0` | UX2-MS58 documentation Run 452 هشت Job سبز و Safe؛ UX2-MS59 نمونهٔ مفهومی هوشمندی مدیریتی با ۹ وضعیت، فرضیه/منشأ/تازگی/عدم قطعیت و قفل اقدام را Candidate کرد؛ Source Run 453 هشت Job سبز/Artifact `11023743292` با ۱۵ قاب معتبر و بازبینی‌شده؛ CI مستندات شرط Checkpoint، Foundation و G3/G4/G5 باز؛ UI اجرایی Agent در V1.1 خارج از دامنه |
| `1.143.0` | UX2-MS59 documentation Run 454 هشت Job سبز و Safe؛ UX2-MS60 فرم شمسی و فیلتر را در ده وضعیت با پیام خطا/موفقیت، قفل و پنهان‌سازی داده Candidate کرد؛ Source Run 455 هشت Job سبز ولی قاب ۳۲۰px متن فنی انگلیسی داشت؛ correction Run 456 هشت Job سبز/Artifact `11025098337` با ۱۶ قاب معتبر و بازبینی‌شده و CI مستندات شرط Checkpoint، Token/Component و G3/G4/G5 باز |
| `1.144.0` | UX2-MS60 documentation Run 457 هشت Job سبز و Safe؛ UX2-MS61 Token متن ثانویه روی سطح کم‌رنگ را اصلاح، کنتراست/Reduced Motion/Focus را آزمود و بستهٔ چهار معیار مالک را Candidate کرد؛ Source Runهای 458/459 در آزمون نمایش معادل سفید شکست خوردند؛ اصلاح متن فارسی و آزمون در Run 460 هشت Job سبز ولی پیام بالای بسته مخفف انگلیسی داشت؛ اصلاح Run 461 هشت Job سبز/Artifact `11027117283` با سه قاب معتبر و بازبینی‌شده؛ CI مستندات شرط Checkpoint، تصمیم مالک و G3/G4/G5 باز |
| `1.145.0` | UX2-MS61 documentation Run 462 هشت Job سبز و Safe؛ MS62 شاهد ۴۸ردیفی چاپ/تراکم را با صفحه‌بندی نمایش و چهار چاپ چندصفحه‌ای آماده کرد؛ Run 463 مشکل تاریخ ۷۶۸px را یافت، Runهای 464/465 سبز ولی مرور بصری اصلاح عنوان ستون و caption موبایل را لازم کرد؛ Source نهایی Run 466 هشت Job سبز/Artifact `11029742768` با هشت PNG/چهار PDF و ۲۴ صفحه معتبر/بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint؛ سه معیار نخست در سطح طراحی پذیرفته، معیار چهارم تا نمایش و پاسخ مالک باز؛ G3/G4/G5 باز |
| `1.146.0` | UX2-MS62 بعد از Source Run 466/Artifact ۸ PNG و ۴ PDF، در Run 467 آزمون Focus Login نشست واردشده را به ارث برد و شکست خورد؛ اصلاح محدود Run 468 هشت Job سبز، MS62 Safe. مالک پس از ارائهٔ بستهٔ چاپ پُرداده و موبایل معیار چهارم را نیز در سطح نمونه پذیرفت. MS63 فهرست خانواده‌های قرارداد و نمونه را برای VX-G3 ممیزی می‌کند؛ CI مستقل این تصمیم شرط پذیرش است، G4/G5 باز |
| `1.147.0` | UX2-MS63 Run 469 هشت Job سبز و `VX-G3` فقط در مرز Contract/Prototype پذیرفته شد. MS64 موج نخست `VX-G4`، ناوبری موبایل Portfolio را با لینک‌های مجاز مشترک، Focus/Escape و سه عرض متصل کرد؛ Source Run 470 هشت Job سبز، شش PNG/Index Artifact `11032562526` معتبر و بازبینی‌شده. CI مستقل مستندات شرط Checkpoint؛ پنج Shell دیگر، G4 کامل، G5 و INT1/QA1 باز |
| `1.148.0` | UX2-MS64 docs Run 471 pin نسخهٔ قدیمی آزمون را آشکار کرد و correction Run 472 هشت Job سبز/MS64 Safe شد. MS65 مرکز فرمان پروژه را با Navigation مشترک موبایل و حفظ Permission/State truth مهاجرت داد؛ Run 473 هشت Job سبز اما مرور بصری راهنمای پیمایش منوی بلند را خواست؛ correction Run 474 هشت Job سبز/Artifact `11035335891` با شش قاب/Index معتبر و بازبینی‌شده. CI مستقل مستندات شرط Checkpoint؛ چهار Shell دیگر، G4/G5 و INT1/QA1 باز |
| `1.149.0` | UX2-MS65 docs Run 475 هشت Job سبز و Safe؛ MS66 چهار Shell مدیریت هویت، گزارش سبد، گفت‌وگوی پروژه و گزارش پروژه را به Navigation مشترک موبایل منتقل کرد. Source Run 476 هشت Job سبز/Artifact `11036398624` با ۱۶ قاب دو عرض و Index معتبر/بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint. زیرکار Navigation شش Shell تکمیل، اما مصرف‌کنندگان و Routeهای دیگر G4، سپس G5 و INT1/QA1 باز |
| `1.150.0` | UX2-MS66 docs Run 477 هشت Job سبز و Safe؛ MS67 Feedback/Form Loading فعال `/profile` را با Status/Error/Success و قفل واقعی کنترل‌ها متصل کرد. Run 478 هشت Job سبز، مرور تصویر عمل انتخابِ بی‌اثر در Loading را یافت؛ Run 479 اصلاح آن را سبز کرد و Run 480 قفل فیلدها/Busy در دریافت/ذخیره را با هشت Job سبز و Artifact نهایی `11038772992` پنج قاب/Index معتبر/بازبینی‌شده سنجید. CI مستقل مستندات شرط Checkpoint؛ سایر مصرف‌کنندگان G4، G5 و INT1/QA1 باز |
| `1.151.0` | UX2-MS67 docs Run 481 پس از timeout بیرونی Docker Hub در بازاجرا هشت Job سبز و Safe؛ MS68 بازخورد/بارگذاری فهرست و فرم دعوت `/admin/users` را با حفظ دادهٔ خطاخورده و Idempotency Key ثابت برای Retry همان payload متصل کرد. Run 482 با درخت کد یکسان هشت Job سبز؛ Run 483 با assertion نادرست ۵۰۳ شکست خورد و Run 484 هشت Job سبز/Artifact `11040584680` با شش قاب/Index معتبر و مرورشده دارد. CI مستقل مستندات شرط Checkpoint؛ تکمیل G4، Qualification G5 و INT1/QA1 بازند |
| `1.152.0` | UX2-MS68 docs Run 485 هشت Job سبز و Safe؛ MS69 پیش‌نمایش مجوز مؤثر `/admin/users` را با پاک‌کردن نتیجهٔ نقش/پروژهٔ قبلی، قفل Selectها هنگام محاسبه و خطای `alert` متصل کرد. Source Run 489 هشت Job سبز و Artifact `11044303294` با چهار قاب/Index معتبر و بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint، تکمیل G4، G5 و INT1/QA1 بازند |
| `1.153.0` | UX2-MS69 docs Run 490 هشت Job سبز و Safe؛ MS70 مدیریت ظاهر ورود `/admin/login-experience` را با تفکیک Loading/Error/Empty فهرست، Retry دریافت، حفظ فرم پس از خطای ذخیره و قفل کنترل‌ها هنگام فرمان متصل کرد. Source Run 491 هشت Job سبز/Artifact `11046207203` چهار قاب/Index معتبر و بازبینی‌شده، CI مستقل مستندات شرط Checkpoint؛ دیگر مصرف‌کنندگان G4، G5 و INT1/QA1 بازند |
| `1.154.0` | UX2-MS70 docs Run 492 هشت Job سبز و Safe؛ MS71 Wizard فعال `/project-bootstraps` را برای خطای دریافت مبدأ/اعضا، Retry و منع Preview با دادهٔ نامعتبر اصلاح کرد. Run 493 هفت Job سبز اما E2E متن Loading پس از خطای مبدأ را یافت؛ correction Run 494 هشت Job سبز/Artifact `11049002783` چهار قاب معتبر و بازبینی‌شده. متن کد نقش/وضعیت عضو در موج بعدی G4 فارسی می‌شود؛ CI مستقل مستندات شرط Checkpoint، باقی G4، G5 و INT1/QA1 بازند |
| `1.155.0` | UX2-MS71 docs Run 495 هشت Job سبز و Safe؛ MS72 Wizard را حین Preview با Fieldset و Navigation قفل کرد، فرمان‌های Preview/Execute/Activate را در Busy منع و نقش/وضعیت عضو را با برچسب فارسی مشترک نشان داد. Run 496 هفت Job سبز، Assertion نادرست Fieldset را یافت؛ correction Run 497 هشت Job سبز/Artifact `11050303281` با سه قاب و Index معتبر و بازبینی‌شده؛ CI مستقل مستندات شرط Checkpoint؛ باقی G4، G5 و INT1/QA1 بازند |
| `1.156.0` | UX2-MS72 docs Run 498 هشت Job سبز و Safe؛ MS73 دو مرکز گزارش را با خطای فرمان متمایز، Retry پایدار و Refresh بدون دادهٔ کهنه مهاجرت داد. Source Run 499 هشت Job سبز و Artifact `11052607582` چهار PNG/Index معتبر و مرورشده دارد؛ CI مستقل مستندات شرط Checkpoint، ممیزی مصرف‌کنندگان فعال G4، سپس G5 و INT1/QA1 بازند |
| `1.157.0` | UX2-MS73 docs Run 500 هشت Job سبز و Safe؛ MS74 کارتابل/اعلان‌ها را با حقیقت Loading/Current/Cached/Unavailable/Forbidden و منع فرمان روی دادهٔ قدیمی متصل کرد. Run 501 سبز بود ولی تصویر موبایل هم‌پوشانی داشت؛ Source نهایی Run 502 هشت Job سبز و Artifact `11054696874` سه PNG/Index معتبر و مرورشده دارد؛ CI مستقل مستندات شرط Checkpoint، ممیزی باقیماندهٔ G4، سپس G5 و INT1/QA1 بازند |
| `1.158.0` | UX2-MS74 docs Run 503 و MS75 docs Run 507 هرکدام هشت Job سبز و Safe؛ Chat گروه پروژه خواندن/جست‌وجوی صادق دارد. MS76 تجمیع سبد را در Refresh/Error پنهان و خطای دریافت را با Token خطر نشان می‌دهد. Run 508 در آزمون کارتابل قدیمی شکست خورد؛ Source Run 509 و docs اولیه Run 510 سبز شدند، اما مرور تصویر اصلاح رنگ را لازم کرد. Source نهایی Run 511 هشت Job سبز و Artifact `11058654451` چهار PNG/Index معتبر و مرورشده دارد؛ CI مستقل C2 شرط Checkpoint، ممیزی مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.159.0` | UX2-MS76 C3 docs Run 513 هشت Job سبز و Safe؛ MS77 مرکز فرمان پروژه را در قطع دسترسی از کش و فرمان کهنه پاک و بازگشت به فهرست را ممکن کرد. Source Run 516 هشت Job سبز/Artifact `11062655536` سه PNG/Index معتبر و مرورشده دارد؛ CI مستقل مستندات شرط Checkpoint، ممیزی فعال G4، سپس G5 و INT1/QA1 بازند |
| `1.160.0` | UX2-MS77 docs Run 517 هشت Job سبز و Safe؛ MS78 فهرست پروژه را در تازه‌سازی/خطا از داده و فرمان کهنه پاک و خطا را با Token خطر متمایز کرد. Run 518 سبز ولی بازبینی تصویر اصلاح رنگ خواست؛ Source نهایی Run 519 هشت Job سبز/Artifact `11062936761` چهار PNG/Index معتبر و مرورشده دارد. ممیزی فعال G4 ثبت شد؛ CI مستقل مستندات شرط Checkpoint، سپس G5 و INT1/QA1 بازند |
| `1.161.0` | UX2-MS78 docs Run 520 هشت Job سبز و Safe؛ MS79 دفتر فنی را در خطا، کش و قطع مجوز از جزئیات و فرمان کهنه جدا کرد. Source نهایی Run 527 هشت Job سبز/Artifact `11065892550` با چهار PNG/Index معتبر و مرورشده دارد؛ اصلاح قاب‌بندی و تثبیت State شاهد پس از بازبینی انجام شد. CI مستقل مستندات شرط Checkpoint، تاریخچه گزارش روزانه و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.162.0` | UX2-MS79 docs correction Run 529 هشت Job سبز و Safe؛ MS80 تاریخچه نسخه‌های گزارش روزانه را در Refresh، خطای دریافت، آفلاین و لغو دسترسی از فهرست/فرمان کهنه جدا کرد و پیام خطا را با Token خطر متمایز ساخت. Source نهایی Run 534 هشت Job سبز/Artifact `11068532055` با پنج قاب و Index معتبر و بازبینی‌شده دارد؛ CI مستقل مستندات شرط Checkpoint، برنامه‌ریزی و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.163.0` | UX2-MS80 docs correction Run 536 هشت Job سبز و Safe؛ MS81 دفتر پیشرفت را در Refresh/خطا/قطع مجوز از اقلام و فرمان کهنه جدا کرد و بازگشت پاسخ جاری را آزمود. Source Run 539 هشت Job سبز/Artifact `11069246238` با پنج قاب و Index معتبر/مرور شده؛ CI مستقل مستندات شرط Checkpoint، مبنای برنامه و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.164.0` | UX2-MS81 docs Run 540 هشت Job سبز و Safe؛ MS82 مبنای برنامه را در Refresh، خطا و قطع مجوز از نسخه/فرمان کهنه جدا و پیام موفقیت را به Refresh معتبر مشروط کرد. Source Run 541 هشت Job سبز/Artifact `11070325003` با پنج قاب و Index معتبر/مرور شده؛ CI مستقل مستندات شرط Checkpoint، مالی و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.165.0` | UX2-MS82 docs Run 542 هشت Job سبز و Safe؛ MS83 کنترل مالی پایه را در Refresh، خطا و لغو مجوز از سند/بودجه/فرمان کهنه جدا کرد. Source Run 544 هشت Job سبز/Artifact `11070393951` پنج قاب و Index معتبر/مرور شده؛ CI مستقل مستندات شرط Checkpoint، مالی تکمیلی و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.166.0` | UX2-MS83 docs Run 545 هشت Job سبز و Safe؛ MS84 کنترل مالی تکمیلی را در Refresh، خطا و لغو مجوز از تعهد/تنخواه/کارمزد و فرمان کهنه جدا کرد. Source Run 546 هشت Job سبز/Artifact `11070819091` پنج قاب و Index معتبر/مرور شده؛ CI مستقل مستندات شرط Checkpoint، قرارداد/تدارکات و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.167.0` | UX2-MS84 docs Run 547 هشت Job سبز و Safe؛ MS85 رجیستر قرارداد و خرید را در Refresh، خطا و لغو مجوز از فهرست/فرمان کهنه جدا کرد. Source Run 548 هشت Job سبز/Artifact `11072765941` پنج قاب و Index معتبر/مرور شده؛ CI مستقل مستندات شرط Checkpoint، واقعیت تدارکات و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
| `1.168.0` | UX2-MS85 docs Run 549 هشت Job سبز و Safe؛ MS86 واقعیت تدارکات را از نمای ذخیره‌شدهٔ آفلاین جدا و فرم/فرمان را به پاسخ جاری محدود کرد. Source Run 551 هشت Job سبز/Artifact `11072588519` شش قاب و Index معتبر/مرور شده؛ CI مستقل مستندات شرط Checkpoint، حاکمیت و سایر مصرف‌کنندگان G4، سپس G5 و INT1/QA1 بازند |
