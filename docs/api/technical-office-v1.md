# Technical Office & Document Control API V1

Base route:

`/api/v1/projects/{projectId}/technical-office`

تمام مسیرها Tenant/Actor scope و Permission فعال پروژه را بررسی می‌کنند. همه Commandها به `Idempotency-Key` نیاز دارند و Transitionها `baseRevision` می‌گیرند.

## State

- `GET /state`

خروجی شامل Documents، Document Revisions، Transmittals، RFIs، Submittals و شمارنده‌های Blocking/Overdue/Superseded است. تاریخ overdue از تاریخ محلی Project محاسبه می‌شود. نبود Contract، WBS یا Budget مانع این Query نیست.

## Document register

- `POST /documents`
- `POST /documents/{documentId}/revisions`
- `POST /document-revisions/{revisionId}/submit`
- `POST /document-revisions/{revisionId}/approve`
- `POST /document-revisions/{revisionId}/return`

Document number در Request وجود ندارد و فقط سرور آن را تولید می‌کند. هر Revision یک `revisionCode` یکتا در همان Document، `fileReference` و SHA-256 دارد. Revision Returned یا قدیمی ویرایش نمی‌شود؛ نسخه تازه با `supersedesRevisionId` ساخته می‌شود.

## Transmittal

- `POST /transmittals`
- `POST /transmittals/{transmittalId}/issue`
- `POST /transmittals/{transmittalId}/acknowledge`

Issue فقط وقتی پذیرفته می‌شود که همه Revisionهای بسته Approved، متعلق به همان Project و از Documentهای متفاوت باشند. در یک تراکنش:

1. Transmittal صادر می‌شود؛
2. Revisionهای جدید Issued می‌شوند؛
3. Revision جاری قبلی Superseded می‌شود؛
4. `CurrentOfficialRevisionId` هر Document تغییر می‌کند؛
5. Audit، Outbox و Idempotency receipt ثبت می‌شوند.

آپلود یا Approval بدون Transmittal ابلاغ رسمی ایجاد نمی‌کند.

## RFI

- `POST /rfis`
- `POST /rfis/{rfiId}/internal-review`
- `POST /rfis/{rfiId}/return`
- `POST /rfis/{rfiId}/issue`
- `POST /rfis/{rfiId}/responses`
- `POST /rfis/{rfiId}/accept`
- `POST /rfis/{rfiId}/clarification`
- `POST /rfis/{rfiId}/close`

قبل از Internal Review حداقل یک Evidence Reference لازم است. Response خارجی به نام Actor داخلی جعل نمی‌شود؛ `respondingParty` و `receivedBy` جدا ثبت می‌شوند. `Answered` با `ResponseAccepted` و `Closed` یکسان نیست. `changePotential` فقط پرچم بررسی است و قرارداد/بودجه/برنامه را تغییر نمی‌دهد.

## Submittal

- `POST /submittals`
- `POST /submittals/{submittalId}/submit`
- `POST /submittals/{submittalId}/begin-review`
- `POST /submittals/{submittalId}/review`
- `POST /submittals/{submittalId}/close`

Outcomeهای داخلی: Approved، ApprovedAsNoted، ReviseAndResubmit، Rejected و ForInformation. Reject و ReviseAndResubmit به Reason نیاز دارند. Resubmission یک Aggregate تازه است و باید نسخه قبل را با شماره چرخه بعدی معرفی کند. Approval به‌تنهایی Material Delivery یا Site Acceptance نیست.

## Offline boundary

- آخرین State مجاز می‌تواند با Scope Tenant/User روی دستگاه Cache شود و با پیام stale نمایش داده شود.
- Formal Issue، Approval، Transmittal و Acknowledgment فقط آنلاین‌اند.
- Draft آفلاین عمومی، upload فنی resumable و scan فایل هنوز Gate Slice تکمیلی Evidence/Sync هستند و در این نسخه به‌صورت ساختگی اعلام نمی‌شوند.
