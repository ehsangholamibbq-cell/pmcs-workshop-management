# Foundation checkpoint 16 — Independent Quality & HSE V1

- Date: 2026-09-11
- Status: Implemented; PostgreSQL migration/concurrency and browser UAT remain environment gates
- Baseline: Checkpoint 15 (`d1ab53f`)

## Scope delivered

- bounded context مستقل `QualitySafety` با DbContext، Migration و API پروژه‌محور؛
- Setup مستقل Quality/HSE با Owner، operating mode، ماتریس ریسک نسخه‌دار، workflow/SLA، template و closure rule؛
- State صریح برای Hidden/NotEnabled/Suspended/SetupRequired/NoData/Available بدون سلامت ترکیبی؛
- Intake سریع کیفیت و HSE، Triage، Resolve و Conversion انسانی با lineage؛
- Inspection request، readiness و result مستقل؛ برنامه بازرسی و آزمون و چک‌لیست نسخه‌دار؛
- NCR کامل با Disposition، Root Cause، Concession و Closure Evidence؛
- Defect assignment و workflow راستی‌آزمایی‌شونده؛
- Incident محرمانه با preliminary/final severity مستقل، investigation و closure evidence؛
- Permit-to-work، Toolbox Talk، Competency و Exposure Hours؛
- Corrective/Preventive Action حوزه‌مند با تفکیک Completed/Verified/Closed و تمدید ممیزی‌شده؛
- نرخ رخداد فقط از کل Incidentهای رسمی و کل ساعات مواجهه تأییدشده؛
- نقش‌های `QualityController` و `HseOfficer` و Permissionهای area-specific؛
- حذف Incident، شایستگی و اقدام منتسب به Incident از پاسخ کاربر فاقد مجوز محرمانه؛
- رابط کاملاً فارسی در مرکز فرمان پروژه و Draft موقت آفلاین فقط برای Intake؛
- Audit، Outbox، Idempotency، optimistic revision و Migration `quality-safety:20260911-001`.

## Semantic safeguards

- Quality و HSE مستقل و اختیاری‌اند؛ غیرفعال‌بودن یکی هسته یا حوزه دیگر را مسدود نمی‌کند.
- Disabled و NoData سبز نیستند و نبود مجوز محرمانه به عدد صفر تبدیل نمی‌شود.
- Intake اولیه Incident رسمی یا اعلان بحرانی نیست.
- Inspection readiness نتیجه Inspection نیست.
- Rectified، Completed و ReadyForVerification به معنی Verified یا Closed نیستند.
- Permit Draft یا Approved تا Active نشده مجوز اجرا نیست.
- نرخ Incident بدون Exposure basis معتبر محاسبه نمی‌شود.
- WBS، Budget، Contract و Calendar همچنان اختیاری‌اند.
- AI حق تعیین Severity، Root Cause، Disposition، Result، Concession، Permit یا Closure را ندارد.

## Verification

- Build هدفمند ماژول QualitySafety: بدون Warning؛
- Build کامل C# با warnings-as-errors و Reference آفلاین سازگار JWT: بدون Warning؛
- ۱۶۰ تست C# شامل state separation، triage، workflowها، evidence، permission role catalog، standard versioning و rate basis موفق؛
- ۷۶ تست Frontend/API contract، ESLint، TypeScript، ممیزی فارسی و Next.js production build موفق؛
- Architecture Guard روی ۲۸۸ فایل C# و `git diff --check` موفق.

Reference آفلاین JWT فقط برای Compile محلی استفاده و پیش از ثبت نسخه حذف شد؛ Repository همچنان فقط به Package رسمی Microsoft متکی است.

## Remaining environment and product gates

- اجرای Migration و Queryهای محرمانگی روی PostgreSQL 17 واقعی؛
- تست هم‌زمانی Revision/Idempotency برای Intake conversion، workflow transition و Corrective Action؛
- UAT مرورگر برای Setup، Offline Intake Sync و workflowهای Quality/HSE؛
- تست Integration واقعی Evidence object storage برای اسناد Inspection، Incident و Closure؛
- Notification escalation بحرانی و SLA engine که در Checkpoint بعدی Risk/Decision/SLA پیاده می‌شود؛
- اتصال شمارنده‌های مستقل Quality/HSE به Project State و Portfolio بدون Composite Health.
