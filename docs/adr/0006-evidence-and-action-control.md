# ADR 0006 — Evidence lifecycle and attention-to-action control

- Status: Accepted
- Date: 2026-09-09

## Context

عکس و فایل باینری با Operationهای کوچک و ترتیبی Daily Report همگام نمی‌شود. همچنین Attention Item محاسباتی نباید برای ثبت تصمیم مدیر، داخل Snapshot تاریخی ویرایش شود. سیستم باید قطع اینترنت، Retry، تکرار درخواست و نبود WBS/Budget/HSE را بدون از دست‌دادن lineage تحمل کند.

## Decision

- ماژول `Evidence` مالک metadata و lifecycle فایل است؛ باینری در S3-compatible private object storage نگهداری می‌شود.
- Client برای هر فایل یک `attachmentId` ثابت، SHA-256، اندازه، MIME type و lineage به Daily Report/Fact می‌سازد.
- صف Attachment در IndexedDB از صف Operation مستقل است. ابتدا Operationها Sync می‌شوند و سپس Attachmentها upload session می‌گیرند.
- Upload Session ۲۴ ساعت اعتبار دارد و برای همان شناسه و metadata قابل تمدید است. استفاده دوباره از شناسه با metadata متفاوت Conflict است.
- API اندازه، نوع محتوا و SHA-256 واقعی باینری را پیش از ارسال به Object Storage بررسی می‌کند و پس از PUT اندازه Object را با HEAD کنترل می‌کند.
- Original object overwrite مفهومی ندارد؛ Object Key از Tenant/Project/Evidence ID ساخته می‌شود و نام فایل کاربر فقط metadata است.
- ماژول `ActionControl` مالک Action و Attention Disposition است. Snapshot و Attention Itemهای محاسباتی immutable می‌مانند.
- یک Attention Item فقط یک‌بار می‌تواند به Action تبدیل یا با دلیل Dismiss شود. این تصمیم به‌صورت overlay در Command Center نمایش داده می‌شود.
- Action بدون Assignee، Due Date و Priority معتبر ساخته نمی‌شود. Priority نامشخص از Observation هرگز خودکار به Low تبدیل نمی‌شود.
- Transitionها command-based، revision-aware و audit‌شونده‌اند؛ Action تکمیل یا لغوشده terminal است.
- Approval گزارش روزانه از Outbox باعث Recalculation خودکار Project State می‌شود. Snapshot و علامت‌گذاری پیام Outbox در یک transaction ثبت می‌شوند.

## Consequences

- از دست‌رفتن شبکه باعث duplicate فایل یا Action نمی‌شود و هر queue independently retry می‌شود.
- فایل Uploaded از Blob محلی پاک می‌شود، اما metadata و نتیجه Sync برای ردپا باقی می‌ماند.
- خواندن محتوای فایل از API مجوز پروژه را enforce می‌کند؛ Bucket عمومی نیست.
- در V1 فایل‌ها حداکثر ۲۵ MiB و از نوع تصویرهای پشتیبانی‌شده یا PDF هستند.
- Antivirus/malware scanning، thumbnail pipeline، multipart upload فایل بزرگ و retention policy در Hardening بعدی تکمیل می‌شوند.
