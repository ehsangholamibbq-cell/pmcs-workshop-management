# ADR 0017 — کنترل نسخه و ابلاغ مستقل مدارک فنی

- Status: Accepted
- Date: 2026-09-11

## Context

آپلود فایل، تأیید فنی، ابلاغ رسمی و جاری‌شدن یک Revision چهار رویداد متفاوت‌اند. ادغام آن‌ها باعث می‌شود فایل تأییدنشده در کارگاه معتبر دیده شود، تاریخچه Revision بازنویسی شود یا پاسخ یک RFI بدون پذیرش انسانی به‌صورت بسته تلقی شود. نبود Contract، WBS یا Budget نیز نباید ثبت گردش فنی را متوقف کند.

## Decision

- `TechnicalDocument` هویت پایدار مدرک و اشاره‌گر یکتای `CurrentOfficialRevisionId` را مالک است.
- هر `TechnicalDocumentRevision` محتوای immutable شامل Code، Date، Purpose، FileReference و SHA-256 دارد؛ اصلاح، Revision تازه با `SupersedesRevisionId` می‌سازد.
- Approval فقط آمادگی برای ابلاغ است. Revision فقط هنگام Issue یک `TechnicalTransmittal` معتبر، `Issued` و Current می‌شود.
- صدور یک Transmittal همه Revisionهای داخل آن را در همان تراکنش کنترل و جاری می‌کند و Revision جاری قبلی را بدون حذف تاریخچه `Superseded` می‌سازد.
- شماره رسمی Document، Transmittal، RFI و Submittal فقط در سرور و از شناسه پایدار Command ساخته می‌شود.
- RFI وضعیت‌های Answered، ResponseAccepted، ClarificationRequired و Closed را جدا نگه می‌دارد. پاسخ بیرونی همراه Source، RespondingParty و ReceivedBy داخلی ثبت می‌شود.
- Submittal و Resubmission رکوردهای جدا هستند. نتیجه فنی آن Delivery، Site Acceptance، تغییر قرارداد یا تغییر بودجه تولید نمی‌کند.
- Contract/Commitment فقط از `ICommercialReferenceDirectory` اعتبارسنجی می‌شوند و وابستگی Persistence بین ماژول‌ها ایجاد نمی‌شود.
- WBS، Contract، Procurement، Location و Work Item پیوندهای اختیاری‌اند.
- مدرک دارای Confidentiality و تمام Revision/Transmittal/RFI/Submittal وابسته به آن در Query و Command به‌صورت fail-closed با Permission مستقل فیلتر می‌شوند.
- تمام Commandهای رسمی Revision، Permission، Idempotency، Audit و Outbox دارند و Online-only هستند.

## Consequences

- کاربر می‌تواند با Drill-down از RFI/Submittal/Transmittal به Revision دارای Hash برسد.
- Upload یا Approval منفرد هرگز Current Official Revision را تغییر نمی‌دهد.
- اعمال هم‌زمان Revision و Document هنگام ابلاغ به تراکنش PostgreSQL وابسته است و باید در Integration Test واقعی کنترل شود.
- General-purpose technical attachment upload، malware scanning و signed download در Slice بعدی Evidence integration تکمیل می‌شود؛ در این Slice FileReference و Hash معتبر، lineage را حفظ می‌کنند.
