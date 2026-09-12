# Foundation checkpoint 17 — Risk, Decision, SLA & Alerts V1

- Date: 2026-09-12
- Status: Implemented; PostgreSQL migration/concurrency and browser UAT remain environment gates
- Baseline: Checkpoint 16 (`f58f76c`)

## Scope delivered

- توسعه bounded context موجود `ActionControl` به دفتر مستقل Issue/Risk/Decision/SLA/Escalation؛
- Risk پیشنهادی، ارزیابی نسخه‌دار، فعال‌سازی، بازبینی residual، بستن، بازگشایی و materialization؛
- ساخت اتمیک Issue از Risk محقق‌شده بدون بازنویسی تاریخچه Risk؛
- Issue با Fact، Severity، Urgency، Owner، Target، Evidence و تفکیک Resolved/Closed با راستی‌آزمایی توسط کاربر مستقل؛
- Risk matrix پنج‌درپنج immutable با version و formula pinned؛
- Decision Request با Facts، Assumptions، Predictions، Options، Authority و required-by جدا؛
- Decision immutable با selected option، rationale، channel، decided-at و supersession chain؛
- اجرای تصمیم و effect review به‌صورت transitionهای صریح؛
- SLA rule نسخه‌دار به تفکیک entity/severity، elapsed hour یا project working day؛
- نبود Project Calendar در قواعد روز کاری به‌صورت uncomputable/setup-required، بدون fallback تقویمی؛
- Deadline outlook قطعی با تفکیک SLA due، target resolution، risk review و decision required-by؛
- Escalation thread deduplicated با reminder/escalation separation و Acknowledge غیرحل‌کننده؛
- Worker پس‌زمینه پنج‌دقیقه‌ای با Audit/Outbox اتمیک و ارزیابی دستی فوری؛
- مسیریابی fail-safe اعلان به گیرنده قاعده، مدیر پروژه یا مالک/مرجع همان منبع؛
- رکوردهای General، Restricted Management، Confidential HSE و Commercial Sensitive با fail-closed read/write؛
- نقش `ProjectController` و Permissionهای مستقل create/assess/review/decide/verify/escalate؛
- انتخاب مالک و مرجع تصمیم از افراد فعال همان پروژه، با حذف گزینه‌های فاقد اختیار تصمیم؛
- UI فارسی responsive با Setup هدایت‌شده، دفترهای Risk/Issue/Decision و Inbox مهلت/هشدار؛
- Audit، Outbox، Idempotency و optimistic Revision برای همه Commandهای رسمی؛
- Migration `action-control:20260911-002` و قرارداد API مستقل.

## Semantic safeguards

- Risk آینده است و Issue رخ داده؛ materialization فقط لینک می‌سازد.
- Risk پیشنهادی تا assessment امتیاز ندارد؛ ماتریس جدید تاریخچه را re-score نمی‌کند.
- Resolved به معنی Closed نیست؛ Closure Evidence مستقل است و حل‌کننده نمی‌تواند همان Issue را ببندد.
- Decision Request، Recommendation و AI Advisory تصمیم رسمی نیستند.
- Decision قبلی ویرایش نمی‌شود؛ replacement به آن supersede link می‌دهد.
- Reminder به معنی Escalation نیست؛ Acknowledge به معنی Resolution نیست.
- Escalation هیچ Owner، Severity، State یا Approval را خودکار تغییر نمی‌دهد.
- Outlook هفت‌روزه forecast احتمالاتی نیست و نبود مبنا `InsufficientData` است.
- WBS، Budget و HSE همچنان اختیاری‌اند و هیچ Composite Health ساخته نمی‌شود.
- همه عملیات رسمی این Slice Online-only هستند و داده حساس در Browser cache پایدار نمی‌شود.

## Verification

- Build هدفمند ActionControl: بدون Warning؛
- ۱۷۲ تست C# شامل state machine، verifier مستقل، evidence closure، risk matrix pinning، SLA scope، materialization، decision option/supersession، calendar SLA و escalation acknowledgement موفق؛
- ۸۳ تست Frontend/API contract، ESLint، TypeScript، ممیزی فارسی و Next.js production build موفق؛
- Architecture Guard روی ۳۰۰ فایل C# و `git diff --check` موفق؛
- Smoke بسته standalone: مسیر محافظت‌شده ۳۰۷، صفحه ورود ۲۰۰، Manifest و Service Worker برابر ۲۰۰ و مسیر نامعتبر ۴۰۴.

Reference آفلاین JWT فقط برای Compile/Test محلی استفاده و پیش از ثبت نسخه حذف شد؛ Repository همچنان فقط به Package رسمی Microsoft متکی است.

## Remaining environment and product gates

- اجرای Migration و unique/concurrency behavior روی PostgreSQL 17 واقعی؛
- تست transaction اتمیک Risk materialization + Issue + Audit/Outbox/Idempotency؛
- اتصال Notification delivery واقعی به Outbox هشدارها؛
- UAT مرورگر برای نقش‌های ProjectManager، ProjectController، authority و restricted viewer؛
- توسعه تقویم تعطیلات استثنایی در صورت نیاز سازمان؛
- اتصال summary مستقل Governance به Portfolio بدون Composite Health؛
- Forecast آماری فقط پس از تعریف data sufficiency و validation؛ تا آن زمان غیرفعال می‌ماند.
