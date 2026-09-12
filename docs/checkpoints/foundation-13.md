# Foundation checkpoint 13 — Planning Baseline V1

- Date: 2026-09-11
- Status: Implemented; live PostgreSQL migration and browser UAT remain environment gates
- Baseline: Checkpoint 12 (`d5c7305`)

## Scope delivered

- تغییر نسخه‌دار Planning Mode از `None` تا Simple/Milestone/WBS/External با مالکیت Project؛
- Aggregate نسخه‌دار Planning Baseline با Draft/Submit/Approve/Return/Supersede؛
- مبنای وزن‌دهی ساده، برنامه نقاط عطف، خط مبنای ساختار شکست کار (WBS) و برنامه بیرونی مرجع‌دار؛
- اعتبارسنجی جمع دقیق ۱۰۰٪، سلسله‌مراتب بدون Cycle، Parent خلاصه و Mapping یکتای Measurement Item؛
- کنترل آمادگی قلم‌های Quantity-based هنگام Approval: تعلق Tenant/Project، Active بودن و Target معلوم؛
- Milestone Progress Update همراه Evidence، Revision، بازبینی انسانی و جلوگیری از عقب‌گرد تاریخ وضعیت؛
- محاسبه قطعی Official/Planned/Variance با تفکیک داده ناقص، Mode mismatch و برنامه‌ریزی پیکربندی‌نشده؛
- استفاده از تقویم کاری اختیاری پروژه در Planned Activity و تاریخ محلی پروژه برای As-of؛
- رابط فارسی ساخت/اصلاح/گردش مبنا، ثبت/بررسی نقاط عطف و نمایش موعدها؛
- Permissionهای مستقل Baseline و Milestone برای نقش‌های Project Manager، Technical Office، Site Supervisor، Observer و Portfolio Viewer؛
- Migration افزایشی Planning به شماره `20260911-002` بدون بازنویسی Migration قبلی.

## Semantic safeguards

- WBS، Baseline و برنامه بیرونی هیچ‌کدام پیش‌نیاز ثبت Fact یا Actual نیستند.
- تغییر Mode، داده و نسخه تاریخی را حذف یا خودکار Mapping نمی‌کند.
- فقط نسخه مصوب و سازگار با Mode فعلی وارد محاسبه می‌شود.
- نبود Actual رسمی در هر ردیف وزن‌دار به صفر تبدیل نمی‌شود و درصد کل را ناشناخته نگه می‌دارد.
- Submitted/Provisional به Official وارد نمی‌شود.
- درصد دستی فقط برای Milestone و فقط بعد از Evidence + Approval انسانی رسمی است.
- `forecastCompletionDate` تا وجود مبنای روند معتبر همچنان `null` است.
- AI هیچ Fact، Baseline، Approval یا Permission رسمی را تغییر نمی‌دهد.

## Verification

- Build هدفمند Planning و Build کامل Release C# با Warning-as-error موفق؛
- ۱۱۴ تست C# شامل وزن‌دهی، داده ناقص، Mode mismatch، تقویم کاری، WBS/External validation و Milestone review موفق؛
- ۶۰ تست Frontend، ESLint، ممیزی فارسی و Build تولیدی Next.js موفق؛
- Architecture Guard و `git diff --check` موفق؛
- Reference موقت Compile برای JWT فقط خارج Repository استفاده و PackageReference رسمی پیش از ثبت نسخه بازگردانده شد.

اجرای `dotnet format --verify-no-changes` به‌علت ممنوعیت Named Pipe در محیط اجرا آغاز نشد؛ Build کامل با Warning-as-error و کنترل Diff مستقل جایگزین Gate قالب‌بندی خودکار شدند.

## Remaining environment gates

- اجرای Migration `planning:20260911-002` روی PostgreSQL واقعی؛
- UAT مرورگر برای Mode → Baseline → Submit → Approve → Progress و Milestone → Evidence → Review؛
- آزمون هم‌زمانی تصویب دو Baseline و دو Milestone Update روی PostgreSQL؛
- Import واقعی فایل برنامه بیرونی و نگاشت تعاملی Activityها؛
- موتور Forecast مبتنی بر روند رسمی و کیفیت داده؛
- Gateهای قبلی Keycloak، SMTP، S3 و UAT نیز تا محیط متصل باز می‌مانند.
