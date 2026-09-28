# Roadmap حاکم تکامل محصول PMCS پس از V1

- شناسه سند: `PMCS-RM-POST-V1-001`
- نسخه سند: `1.97.0`
- وضعیت: `V1.1 Development`؛ F01–F10 و COL1 متصل، UX2 فعال، INT1/QA1 باز
- تاریخ ثبت: ۱۴۰۵/۰۷/۰۶ (۲۰۲۶-۰۹-۲۸)
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
