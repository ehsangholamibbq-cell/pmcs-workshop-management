# Roadmap حاکم تکامل محصول PMCS پس از V1

- شناسه سند: `PMCS-RM-POST-V1-001`
- نسخه سند: `1.20.0`
- وضعیت: `V1.1 Development`؛ UX1، EXT1، DOC1، IAM1 و PRJ1 بسته شده‌اند و RPT1 فعال است
- تاریخ ثبت: ۱۴۰۵/۰۶/۲۸ (۲۰۲۶-۰۹-۱۹)
- مرجع پیشین: `docs/roadmaps/pmcs-v1-development-and-qualification.md`
- Baseline منبع V1: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- وضعیت V1: `Qualified | Final | Baseline Locked`
- خط توسعه فعال بعدی: `PMCS V1.1`
- شاخه توسعه: `v1.1-development`
- Repository Start Commit: `0389b52cbd3385bdcc9f0e2a94411800389ae2fc`
- مرحله فعال: `V1.1-RPT1 — Reporting Center Phase 1`

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
| `D-PV1-12` | مسیر بصری «مدیریت ممتاز» با لوگوی رسمی بتن بسپار قزوین، Surface گرم و Accent کنترل‌شدهٔ سرمه‌ای/سبز/نقره‌ای مبنای V1.1 است. | مصوب |
| `D-PV1-13` | طرح گرافیکی Login از جریان Authentication جدا و با Descriptor/Asset نسخه‌دار، امن و قابل Rollback تغییرپذیر است؛ HTML/CSS/JS دلخواه قابل بارگذاری نیست. | مصوب |
| `D-PV1-14` | هر عضو سامانه یک پروفایل شخصی حداقلی و یکپارچه دارد و می‌تواند تصویر خود را مدیریت کند؛ عضویت و نقش پروژه جدا از پروفایل شخصی باقی می‌ماند. | مصوب |
| `D-PV1-15` | ایجاد پروژه از روی پروژهٔ موجود با Preview و انتخاب اقلام Setup/Member مجاز است؛ دادهٔ عملیاتی، مالی، پیام، فایل، Audit و سابقه هرگز ضمنی کپی نمی‌شود. | مصوب |

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

### `V1.1-COL1` — Project Collaboration

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
