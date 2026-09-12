# Evidence and Action Control API — V1 Lite

## Evidence

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/evidence` | `evidence.read` | فهرست metadata با filter اختیاری report/fact |
| `GET` | `/api/v1/projects/{projectId}/evidence/{evidenceId}` | `evidence.read` | metadata یک مدرک |
| `POST` | `/api/v1/projects/{projectId}/evidence/upload-sessions` | `evidence.upload` | ساخت یا تمدید upload session |
| `PUT` | `/api/v1/projects/{projectId}/evidence/{evidenceId}/content` | `evidence.upload` | اعتبارسنجی و انتقال باینری به S3-compatible storage |
| `GET` | `/api/v1/projects/{projectId}/evidence/{evidenceId}/content` | `evidence.read` | دریافت private object از مسیر مجوزدار API |

Create Session و Upload Content هر دو `Idempotency-Key` می‌خواهند. شناسه client-generated و SHA-256 سبب می‌شوند Retry همان فایل، رکورد جدید نسازد. فرمت‌های مجاز JPEG، PNG، WebP، HEIC/HEIF و PDF و سقف اندازه ۲۵ MiB است.

ترتیب PWA:

1. ذخیره Blob و metadata در attachment queue مستقل؛
2. Sync شدن Daily Report/Fact والد؛
3. دریافت upload session؛
4. PUT باینری با همان Evidence ID؛
5. کنترل hash و اندازه در API؛
6. PUT و HEAD در Object Storage؛
7. تغییر وضعیت metadata از `PendingUpload` به `Uploaded`؛
8. حذف Blob حجیم از دستگاه و نگهداری نتیجه Sync.

## Action Control

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/actions` | `actions.read` | Actionهای پروژه با filter اختیاری Status/AssignedToMe |
| `GET` | `/api/v1/projects/{projectId}/actions/{actionId}` | `actions.read` | جزئیات Action |
| `POST` | `/api/v1/projects/{projectId}/attention/{sourceFactId}/actions` | `attention.triage` + `actions.create` | تبدیل Issue/Stoppage تأییدشده به Action |
| `POST` | `/api/v1/projects/{projectId}/attention/{sourceFactId}/dismiss` | `attention.triage` | بستن مورد با دلیل |
| `POST` | `/api/v1/projects/{projectId}/actions/{actionId}/transition` | `actions.update` | Transition با Base Revision |

Action دارای Assignee، Due Date، Priority، Status و Revision است. فقط Assignee یا دارنده `actions.manage` می‌تواند Action را Transition دهد. وضعیت‌های `Done` و `Cancelled` terminal هستند.

Command Center برای هر Attention Item علاوه بر status محاسباتی، `disposition` مستقل برمی‌گرداند:

- `NeedsTriage`
- `ConvertedToAction` همراه `actionId`
- `Dismissed` همراه دلیل و زمان تصمیم

## Automatic Project State refresh

Hosted worker پیام `FieldOperations.DailyReportApproved` را از Outbox با `FOR UPDATE SKIP LOCKED` دریافت می‌کند، از Factهای Approved Snapshot جدید می‌سازد و Snapshot + Audit + پایان پردازش پیام را در یک transaction ثبت می‌کند. پس از ۱۰ خطا پیام برای بررسی عملیاتی باقی می‌ماند.
