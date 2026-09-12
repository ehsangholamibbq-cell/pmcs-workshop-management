# Foundation checkpoint 14 — Technical Office & Document Control V1

- Date: 2026-09-11
- Status: Implemented; PostgreSQL migration/concurrency, technical attachment pipeline and browser UAT remain environment gates
- Baseline: Checkpoint 13 (`4b2d13d`)

## Scope delivered

- bounded context مستقل `TechnicalOffice` با DbContext و Migration افزایشی؛
- Document Register با شماره رسمی server-issued و پیوندهای اختیاری Contract/Location/Work Item/WBS؛
- Document Revision مستقل با Code/Date/Purpose/FileReference/SHA-256 و `SupersedesRevisionId`؛
- گردش Draft/Submit/Approve/Return بدون بازنویسی محتوای Revision؛
- Transmittal مستقل با Sender/Recipients/Channel/Purpose/Acknowledgment؛
- Issue تراکنشی Transmittal، جاری‌کردن Revision مصوب و Supersede نسخه قبلی؛
- RFI با Internal Review، Formal Issue، Response history، Acceptance، Clarification و Close مستقل؛
- ثبت پاسخ بیرونی همراه RespondingParty و ReceivedBy داخلی؛
- Potential Impact چندبعدی و IsBlocking بدون اثبات خودکار تأخیر پایان پروژه؛
- Submittal و Resubmission مستقل با گردش Review و Reason اجباری برای Reject/Revise؛
- اعتبارسنجی اختیاری Contract/Commitment از قرارداد خواندن Commercial بدون دسترسی به Persistence آن؛
- Permissionهای جدا برای Document، Transmittal، RFI و Submittal؛
- فیلتر fail-closed مدارک محرمانه و تمام رکوردهای دارای ارجاع به Revision محرمانه؛
- رابط فارسی در مرکز فرمان، Cache scope‌شده و اقدامات رسمی Online-only؛
- Audit، Outbox، Idempotency و optimistic revision برای همه Commandها.

## Semantic safeguards

- Upload یا Approval به‌تنهایی ابلاغ رسمی نیست.
- فقط Transmittal صادرشده Current Official Revision را تغییر می‌دهد.
- Revision قبلی حذف نمی‌شود و زمان Issue با زمان Supersede بازنویسی نمی‌شود.
- Answered RFI بدون Acceptance انسانی بسته نمی‌شود.
- پاسخ خارجی به نام کاربر داخلی ثبت نمی‌شود و Source/ReceivedBy جداست.
- Approval یک Submittal تحویل مصالح، پذیرش کارگاه یا تغییر قرارداد نیست.
- RFI، Drawing Revision یا Submittal مبلغ، مدت، Scope، Budget یا Planning Baseline را خودکار تغییر نمی‌دهد.
- WBS، Budget، Contract، Project Calendar، HSE و Quality همچنان اختیاری و مستقل‌اند.
- AI هیچ سند، ابلاغ، پاسخ، Current Revision یا Approval رسمی را تغییر نمی‌دهد.

## Verification

- Build هدفمند ماژول TechnicalOffice: بدون Warning؛
- Build کامل C# با Reference آفلاین سازگار JWT: بدون Warning؛
- ۱۲۶ تست C# شامل immutable revision، issue gate، RFI acceptance/clarification و Submittal review موفق؛
- ۶۴ تست Frontend/API contract، ESLint، TypeScript و ممیزی فارسی موفق؛
- Architecture Guard روی ۲۴۳ فایل C# و `git diff --check` موفق.

Reference آفلاین JWT فقط برای Compile محلی استفاده و پیش از ثبت نسخه حذف شد؛ Repository همچنان فقط به Package رسمی Microsoft متکی است.

## Remaining environment gates

- اجرای Migration `technical-office:20260911-001` روی PostgreSQL واقعی؛
- تست هم‌زمانی صدور دو Transmittal برای Revisionهای یک Document؛
- UAT مرورگر برای Document → Revision → Approve → Transmittal → Current؛
- upload عمومی مدرک فنی با resumable transfer، malware scan و signed download؛
- Draft آفلاین RFI/Instruction و Conflict workflow ماژول دفتر فنی؛
- اتصال RFI Blocking و Submittal overdue به Project State/Portfolio؛
- Change Event و Impact Review مستقل برای تغییرات محتمل.
