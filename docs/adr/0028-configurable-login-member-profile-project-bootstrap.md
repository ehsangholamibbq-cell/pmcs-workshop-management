# ADR 0028 — Login قابل پیکربندی، پروفایل عضو و Project Bootstrap کنترل‌شده

- وضعیت: Accepted
- تاریخ: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Parent product baseline: `PMCS V1 / 26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- Product line: `PMCS V1.1`
- Roadmap: `docs/roadmaps/pmcs-post-v1-product-evolution.md` نسخه `1.2.0`

## Context

پس از تصویب مسیر بصری «مدیریت ممتاز»، سه نیاز محصولی ثبت شد:

1. طرح گرافیکی صفحهٔ ورود باید بدون دست‌کاری منطق Login و Authentication به‌راحتی قابل به‌روزرسانی باشد؛
2. هر عضو سامانه باید یک پروفایل شخصی حداقلی داشته باشد و بتواند تصویر خود را مدیریت کند؛
3. مشخصات Setup، افراد و تنظیمات انتخابی یک پروژه باید در صورت نیاز برای ایجاد پروژهٔ دیگر قابل استفاده باشند.

این نیازها اگر با Theme hard-code، کپی حساب کاربری یا Clone مستقیم Database پیاده شوند، ریسک امنیت، نشت داده، Permission drift و Coupling ایجاد می‌کنند. بنابراین قرارداد معماری آن‌ها قبل از کدنویسی تثبیت می‌شود.

## Decision

### ۱. Login presentation از Authentication جدا است

OIDC، Keycloak، BFF، Session، Redirect و Security headerهای V1 منبع حقیقت ورود باقی می‌مانند. تغییر طرح Login اجازهٔ تغییر این جریان را ندارد.

Presentation از یک `LoginExperienceDescriptor` نسخه‌دار خوانده می‌شود که فقط موارد زیر را کنترل می‌کند:

- Brand assetهای تأییدشده؛
- Design tokenهای allowlisted؛
- Composition variantهای از قبل پیاده‌سازی‌شده؛
- متن‌های مجاز و Localized؛
- Motion policy و reduced-motion behavior؛
- Active version، Preview، Publish و Rollback.

پنل مدیریت اجازهٔ بارگذاری HTML، CSS، JavaScript، iframe یا URL اجرایی دلخواه را نمی‌دهد. Assetها از مسیر امن Object Storage، Content validation و Quarantine عبور می‌کنند. در خرابی Descriptor یا Asset، Login باید با Fallback داخلی و بدون اختلال امنیتی قابل استفاده بماند.

### ۲. پروفایل شخصی با حساب هویتی یکی نیست

`MemberProfile` متعلق به Identity Access/Application Directory است و برای هر حساب Tenant دقیقاً یک Profile فعال دارد. Profile حداقلی شامل این داده‌ها است:

- نام نمایشی؛
- تصویر پروفایل؛
- عنوان شغلی؛
- واحد سازمانی؛
- اطلاعات تماس سازمانی مجاز.

Credential، Password، MFA و Session جزو Profile نیستند. Email/Username هویتی بر اساس Policy می‌توانند Read-only باشند.

کاربر فیلدهای Self-service مجاز و تصویر خود را تغییر می‌دهد. مدیر فقط با Permission مستقل می‌تواند Directory fields را اصلاح کند. تصویر خصوصی است، Thumbnail نسخه‌دار دارد و با حذف/جایگزینی، Cache آن کنترل می‌شود.

Profile در پروژه‌های مختلف Reference می‌شود؛ اما `ProjectMembership`، Role، Access Scope، Status و تاریخ عضویت همچنان برای هر پروژه جدا هستند. تغییر Role پروژه نباید Profile شخصی را تغییر دهد و برعکس.

### ۳. Duplicate پروژه به‌صورت Bootstrap Plan اجرا می‌شود

قابلیت محصول با عنوان «ساخت از روی پروژهٔ موجود» ارائه می‌شود و Clone مستقیم Database نیست.

فرآیند دارای این مراحل است:

1. انتخاب پروژهٔ مبدأ و ایجاد مقصد Draft؛
2. انتخاب دسته‌های مجاز؛
3. Dry-run و Preview شامل `Add / Skip / Conflict / Block`؛
4. تأیید انسانی؛
5. اجرای Idempotent و ثبت Audit؛
6. Validation نتیجه؛
7. Activation مستقل پروژهٔ مقصد.

هر ماژول فقط از طریق `ProjectBootstrapContributor` نسخه‌دار و allowlisted می‌تواند دادهٔ Setup خود را ارائه کند. Contributor باید Cloneability، Permission، dependency، compatibility و conflict policy را اعلام کند.

### ۴. مرز انتقال اعضا

انتقال افراد، حساب جدید یا کپی User ایجاد نمی‌کند. برای حساب موجود، Membership جدید با Role و Scope انتخاب‌شده ساخته می‌شود.

- عضو غیرفعال، تعلیق‌شده یا خارج از Tenant منتقل نمی‌شود؛
- دسترسی مقصد از Permission Engine دوباره ارزیابی می‌شود؛
- دسترسی مبدأ هیچ‌گاه به‌عنوان مجوز ضمنی مقصد پذیرفته نمی‌شود؛
- اعلان عضویت فقط پس از Confirmation نهایی ارسال می‌شود؛
- Profile photo و مشخصات شخص دوباره کپی نمی‌شوند و از Profile واحد خوانده می‌شوند.

### ۵. Allowlist و Denylist داده

قابل انتقال با انتخاب صریح:

- Module enablement و تنظیمات سازگار؛
- Calendar و Location structure؛
- Role، Workflow، Form و Report template؛
- Lookupهایی که مالک ماژول آن‌ها را Cloneable اعلام کرده است؛
- Membership فعال با Role/Scope انتخاب‌شده.

غیرقابل انتقال ضمنی:

- شناسه‌ها و مقادیر یکتا؛
- تراکنش‌های مالی، پرداخت، سند و بودجهٔ واقعی؛
- گزارش روزانه، پیشرفت واقعی، Project State و Snapshot؛
- قرارداد/خرید/RFI/Approval/Issue/Action اجراشده؛
- پیام، فایل، Attachment، Notification و Agent conversation؛
- Audit، Outbox، Idempotency، Sync و Offline state.

### ۶. Permission، Audit و اجرا

- `login-experience.manage` برای Draft/Preview/Publish/Rollback طرح Login؛
- `member-profile.read-self` و `member-profile.update-self` برای Self-service؛
- `member-profile.avatar.publish-self` فقط برای Release تصویر پاک و متعلق به همان کاربر؛
- `member-profile.read-directory` برای مشاهده در محدوده سازمانی مجاز یا پروژه مشترک؛
- `member-profile.manage-directory` برای مدیریت سازمانی؛
- `projects.bootstrap.create` برای ساخت پروژه از مبدأ؛
- `projects.bootstrap.members_copy` برای انتقال عضویت‌ها.

Bootstrap فقط داخل یک Tenant مجاز است. هر Run دارای Correlation ID، Idempotency key، Actor، Source/Target، Descriptor version، Preview digest، نتیجه و Audit کامل است. اجرای ناقص نباید پروژهٔ مقصد را Active کند.

## Consequences

### مثبت

- تغییر ظاهر Login بدون بازنویسی یا تضعیف Authentication ممکن می‌شود؛
- هر شخص یک هویت نمایشی یکپارچه دارد و در چند پروژه Duplicate نمی‌شود؛
- پروژه‌های مشابه سریع‌تر راه‌اندازی می‌شوند؛
- مرز دادهٔ Setup و دادهٔ عملیاتی قابل تست و ممیزی باقی می‌ماند؛
- Permission drift و کپی ناخواستهٔ اطلاعات حساس کنترل می‌شود.

### هزینه و پیچیدگی پذیرفته‌شده

- مدیریت Version/Preview/Rollback برای Login asset لازم است؛
- پردازش امن تصویر و Thumbnail به تست و Storage policy نیاز دارد؛
- هر ماژول برای Bootstrap باید Contributor و Compatibility contract مستقل داشته باشد؛
- Preview/Execute parity و recovery از Partial failure باید به‌طور واقعی تست شود.

## Alternatives Rejected

| گزینه ردشده | دلیل |
| --- | --- |
| Hard-code کردن طرح Login در یک Component | تغییر بعدی به Deployment و دست‌کاری کد وابسته می‌شود |
| بارگذاری HTML/CSS/JS آزاد برای Login | XSS، CSP bypass و تهدید مستقیم صفحهٔ احراز هویت |
| نگهداری تصویر کاربر در Keycloak به‌عنوان Blob | Coupling هویت و رسانه، ضعف نسخه/پردازش/Storage lifecycle |
| ساخت Profile جدا برای هر پروژه | چندگانگی هویت شخص و ناسازگاری اطلاعات |
| Clone مستقیم Database پروژه | کپی Audit/transaction/identifier و نشت داده |
| کپی User account همراه پروژه | تضاد Credential/Identity و ایجاد حساب‌های تکراری |
| انتقال خودکار تمام Roleها بدون Preview | privilege propagation و دسترسی ناخواسته |
