# Foundation checkpoint 04 — Evidence and Action Control

- Date: 2026-09-09
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.5

## Outcome

مسیر `Offline Photo/File → Verified S3 Object → Fact Lineage` و مسیر `Approved Attention → Action/Decision → Command Center` پیاده شد. کاربران کارگاه همچنان واقعیت و مدرک را ثبت می‌کنند؛ تعیین مسئول، مهلت و تصمیم در لایه مدیریتی انجام می‌شود. هیچ بخش این Slice به وجود WBS، Budget Baseline یا HSE وابسته نیست.

## Implemented

- ماژول `Evidence` با schema و migration مستقل؛
- metadata نسخه‌دار فایل، lineage تا Daily Report/Fact و Object Key امن Tenant/Project؛
- upload session قابل تمدید، client-generated ID و resource-level idempotency؛
- بررسی MIME، سقف ۲۵ MiB، SHA-256 واقعی و اندازه Object Storage؛
- private S3-compatible adapter با AWS SDK و MinIO development binding؛
- API مجوزدار list/metadata/upload/download؛
- IndexedDB schema v2 و attachment queue مستقل از operation queue؛
- recovery آپلود قطع‌شده، Retry مستقل، نمایش فایل ردشده و آزادکردن Blob بعد از موفقیت؛
- camera/file capture در PWA و اتصال مدرک به آخرین Fact یا گزارش روز؛
- ماژول `ActionControl` و Aggregate دارای Assignee، Due Date، Priority، Status و Revision؛
- تبدیل فقط Issue/Stoppageهای Approved به Action یا Dismissal دارای دلیل؛
- جلوگیری از triage دوباره و overlay تصمیم روی Attention Item بدون تغییر Snapshot؛
- Transitionهای مجاز Action، کنترل Revision و محدودیت Assignee/Manager؛
- Action Inbox و کنترل triage در Command Center؛
- refresh خودکار Project State از Outbox پس از `DailyReportApproved`؛
- transaction واحد برای Snapshot خودکار، Audit و پایان پردازش Outbox.

## Verification evidence

- .NET Release build: ۹ پروژه محصول + پروژه تست، ۰ warning و ۰ error؛
- Domain tests: ۲۷/۲۷ passed؛
- Frontend unit/API contract tests: ۱۶/۱۶ passed؛
- ESLint، TypeScript و Next.js production build: passed؛
- Repository architecture validation: ۹۴ فایل C# ماژول بررسی و passed؛
- `git diff --check`: passed.

## Constraints still open

- اجرای واقعی migration، S3 upload و transaction worker در PostgreSQL/MinIO این محیط ممکن نیست، چون Docker/PostgreSQL runtime نصب نیست.
- Upload فعلی برای فایل حداکثر ۲۵ MiB در حافظه API buffer می‌شود؛ multipart streaming در Hardening بعدی است.
- Antivirus scan، quarantine، thumbnail/EXIF policy و retention/Legal Hold هنوز پیاده نشده‌اند.
- Assignee picker در UI فعلاً کاربر جاری است؛ API از ابتدا Assignee پروژه را اعتبارسنجی می‌کند.
- Action هنوز Comment، Evidence اختصاصی، Verification و Close جداگانه ندارد.
- Project Calendar و Coverage basis روزهای کاری هنوز اضافه نشده است.

## Next checkpoint

Vertical Slice «Project Calendar & Finance Control Lite»:

1. Project Calendar اختیاری و `project-state-v2` بدون بازنویسی Snapshotهای v1؛
2. Finance Lite بدون الزام Budget Baseline؛
3. Payment/Receipt/Petty Cash facts و workflow پایه؛
4. Contract/Cost Center reference اختیاری؛
5. Financial State مستقل با `NotConfigured/NoData/Available`؛
6. ورود انتخابی Financial State به Command Center بدون Composite Health مصنوعی.
