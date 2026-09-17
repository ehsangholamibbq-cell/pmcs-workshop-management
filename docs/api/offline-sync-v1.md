# قرارداد آفلاین و همگام‌سازی — نسخه ۳

این قرارداد برای ثبت کنترل‌شده واقعیت‌های کارگاه است. داده محلی تا پذیرش سرور حقیقت رسمی نیست. سرور در هر همگام‌سازی هویت، دستگاه، مجوز جاری، Lease، نسخه قرارداد، Workflow و قواعد دامنه را دوباره بررسی می‌کند.

## مرز امنیتی

- شناسه دستگاه جایگزین هویت کاربر نیست؛ همه Endpointها به Access Token معتبر نیاز دارند.
- نشست Sync فقط ۱۵ دقیقه، برای یک Tenant/User/Device/Project و زیر یک Lease مشخص معتبر است.
- Lease آفلاین حداکثر هفت روز و وابسته به Device و Authorization Version است.
- ساعت دستگاه فقط زمان ادعاشده ثبت را نشان می‌دهد؛ زمان سرور مرجع پذیرش، Audit و SLA است.
- عملیات بیرون بازه Lease، قدیمی‌تر از هفت روز، متعلق به Scope دیگر یا ساخته‌شده پس از Supersede شدن Lease خودکار رسمی نمی‌شود.
- Permission در زمان Push دوباره بررسی می‌شود؛ داشتن Lease مجوز فعلی را جایگزین نمی‌کند.
- Approval، Decision، Closure حساس، Access Management، Payment و Financial/Inventory Post آفلاین نیستند.

## ترتیب پروتکل

1. `POST /api/v1/sync/handshake`
2. Pull مسدودکننده‌های امنیتی در پاسخ Handshake
3. `POST /api/v1/sync/operations`
4. انتقال مستقل Attachmentها
5. `GET /api/v1/sync/pull`
6. Apply idempotent در IndexedDB
7. `POST /api/v1/sync/checkpoints`

Header نشست در مرحله‌های ۳ تا ۷:

```http
X-Pmcs-Sync-Session: <opaque-session-id>
```

BFF فقط همین Header اختصاصی Sync را همراه Access Token سروری عبور می‌دهد و Headerهای هویت ارسالی مرورگر را همچنان حذف می‌کند.

## Handshake

```http
POST /api/v1/sync/handshake
Content-Type: application/json
```

فیلدهای اصلی درخواست:

| فیلد | قاعده |
| --- | --- |
| `deviceId` | شناسه پایدار مرورگر؛ حداکثر ۱۲۰ کاراکتر امن |
| `projectId` | دقیقاً یک پروژه برای نشست |
| `appVersion` | حداقل `0.1.0` |
| `protocolVersion` | دقیقاً `3` |
| `localSchemaVersion` | دقیقاً `6` |
| `lastCheckpoint` | Token مبهم آخرین Apply موفق یا `null` |
| `deviceTime` | برای تشخیص Clock Skew، نه تصمیم رسمی |
| `queue` | تعداد و سن قدیمی‌ترین عملیات و حجم Attachment بدون Payload حساس |

پاسخ موفق شامل `sessionId`، زمان انقضا، Lease ID، Authorization Version، Policy Version، Watermark، Checkpoint فعلی و `currentCheckpointSequence`، حدود Batch/File، هشدار اختلاف ساعت و Dataset Manifest حداقلی است. Policy جاری `sync-policy-v3` است. اگر Checkpoint کلاینت با سرور یکسان نباشد، `bootstrapRequired=true` و Pull کنترل‌شده از Sequence صفر انجام می‌شود؛ Full database دانلود نمی‌شود.

خطاهای Compatibility با HTTP 426 و یکی از Codeهای زیر پاسخ داده می‌شوند:

- `sync.client.update_required`
- `sync.protocol.unsupported`
- `sync.schema.update_required`

دستگاه لغوشده پاسخ `sync.device.revoked` و `purgeRequired=true` می‌گیرد. Pending Data بی‌صدا حذف نمی‌شود و ابتدا باید Secure Recovery آن بررسی شود.

## Push عملیات

```http
POST /api/v1/sync/operations
X-Pmcs-Sync-Session: <session-id>
Content-Type: application/json
```

هر Batch حداکثر ۱۰۰ Operation دارد و Transaction واحد نیست؛ شکست یک Operation مستقل، موارد سالم بعدی را Rollback نمی‌کند. Gateway فقط Envelope/Session/Lease/Dependency را کنترل می‌کند و Command را به Handler ماژول مالک می‌سپارد؛ مستقیم در جدول کسب‌وکار نمی‌نویسد.

فیلدهای Operation:

| فیلد | کاربرد |
| --- | --- |
| `operationId` | ULID ثابت برای Retry و Inbox |
| `entityType/entityId/commandType` | مقصد صریح Command |
| `baseRevision` | نسخه‌ای که قصد محلی روی آن ساخته شد؛ برای Append مستقل می‌تواند `null` باشد |
| `payloadSchemaVersion` | نسخه Payload دامنه |
| `payload` | حداقل داده Command؛ نه Snapshot کامل موجودیت |
| `offlineLeaseId/authorizationVersion` | مجوز معتبر زمان Capture |
| `localSequence` | ترتیب داخل همان User/Device |
| `dependencies` | Operationهای مقدم در همان Batch، حداکثر ۵۰ مورد |
| `createdAtDevice/deviceTimezoneOffsetMinutes` | زمان ادعاشده و Offset دستگاه |
| `correlationId` | شناسه امن ردیابی بدون Payload |

Command رسمی‌شده در Gateway نسخه ۲:

- `DailyReport / CaptureDailyReportFact`

این Command یک Fact مستقل را Append می‌کند. نبود WBS، Budget یا HSE مانع آن نیست. `measurementItemId` اختیاری است و فقط برای Work Progress معتبر است. اگر ارسال شود، تعلق به همان Tenant/Project، فعال‌بودن و تطابق واحد در سرور بررسی می‌شود.

`locationId` برای هر Fact جدید الزامی است و باید به Location فعال همان Tenant/Project اشاره کند. نام Location در Payload مرجع رسمی نیست؛ سرور نام جاری رجیستری را همراه شناسه پایدار ذخیره می‌کند. پروژه نیز هنگام Handshake و هنگام اعمال Operation باید `Active` باشد.

نتیجه هر Operation یکی از موارد زیر است:

- `Applied`: پذیرفته و دارای Revision/Projection سرور؛
- `Conflict`: بدون Overwrite و همراه `conflictId`؛
- `Rejected`: Permission/Lease/Validation/Workflow نپذیرفته است؛
- `Unsupported`: Command یا Payload Version شناخته‌شده نیست.

Replay همان Operation/Payload نتیجه ذخیره‌شده را با `wasReplay=true` برمی‌گرداند. استفاده دوباره Operation ID با Payload متفاوت `sync.operation.reused` است.

هر تلاش در یک Receipt عملیاتی payload-free با کلید Tenant/User/Device/Operation ثبت می‌شود. کلید Idempotency داخلی نیز User و هش Device را در Scope خود دارد تا Operation ID یکسان میان دو کاربر هرگز باعث Replay یا Rejection متقاطع نشود. Receipt فقط Status، Revision/Conflict reference، تعداد تلاش/Replay، Correlation ID و زمان‌ها را نگه می‌دارد. Retry هم‌زمان یا پس از Crash نمی‌تواند Fact، Audit یا Change Feed دوم بسازد. استفادهٔ مجدد Operation ID با Payload متفاوت Reject می‌شود، اما Status/Revision پذیرفته‌شدهٔ Receipt قبلی را تنزل نمی‌دهد. Dependency یک Operation می‌تواند در Batch جاری یا Receipt پذیرفته‌شدهٔ Batch قبلی اثبات شود.

`correlationId` معتبر Operation بدون جایگزینی با Trace اتفاقی HTTP در Audit، Outbox، Change Feed و آخرین تلاش Receipt نگه داشته می‌شود. Envelope نامعتبر فقط در Scope پروژهٔ Session ثبت می‌شود و متن‌های تشخیصی آن پیش از Persistence محدود می‌شوند؛ بنابراین Project ID یا فیلد بلند ارسالی Client نمی‌تواند دادهٔ تشخیصی پروژهٔ دیگری را آلوده کند یا Batch را با خطای Server متوقف سازد.

ثبت اولیه Quality/HSE یک Draft provisional مستقل است: کلاینت فقط زیر Lease معتبر آن را نگه می‌دارد و پس از Handshake به Endpoint مالک QualitySafety می‌فرستد. آن Endpoint Permission، فعال‌بودن ماژول و Domain Rule جاری را دوباره کنترل می‌کند. Draft محلی Incident رسمی، شماره پرونده یا اعلان بحرانی نیست.

## Pull و Checkpoint

```http
GET /api/v1/sync/pull?projectId=<id>&checkpoint=<opaque-token>&limit=100
X-Pmcs-Sync-Session: <session-id>
```

Change Feed فقط Projection مجاز همان پروژه را برمی‌گرداند. در این Slice، Projectionهای پذیرفته‌شده Daily Report/Fact از همه Deviceهای مجاز پروژه در Feed قرار می‌گیرند؛ Raw database log یا Payload حساس منتشر نمی‌شود.

هر Page یک `checkpointOffer` کوتاه‌عمر دارد. Client ابتدا Changeها را با کلید `changeId` در Store جداگانه idempotent اعمال می‌کند و فقط بعد از موفقیت Apply آن Offer را تأیید می‌کند:

```http
POST /api/v1/sync/checkpoints
X-Pmcs-Sync-Session: <session-id>
Content-Type: application/json

{
  "projectId": "...",
  "checkpointOffer": "..."
}
```

Checkpoint یکنواخت است؛ Regression و Token متعلق به Device/Session دیگر رد می‌شود. Acknowledge تکراری همان Offer نتیجه قبلی را برمی‌گرداند. قطع برنامه پس از Apply محلی و پیش از Acknowledge فقط Pull تکراری می‌سازد و به‌دلیل `changeId` رکورد تکراری ایجاد نمی‌کند.

## مرکز تعارض

```http
GET  /api/v1/sync/conflicts?projectId=<id>
POST /api/v1/sync/conflicts/{conflictId}/resolve
```

Conflict Case هر دو سمت را نگه می‌دارد: قصد/Envelope محلی، Projection و Revision فعلی سرور، Actor، Device، زمان، Reason Code و Revision خود Conflict. کاربر فقط Conflict خود را می‌بیند؛ Project Manager می‌تواند Conflictهای همان پروژه را ببیند.

گزینه‌های Resolution این Slice:

- `KeepServer`: نسخه رسمی سرور حفظ و قصد محلی بایگانی می‌شود.
- `Reapply`: Original بازنویسی نمی‌شود؛ Client یک ULID جدید می‌سازد و همان قصد را با Base Revision فعلی دوباره در صف می‌گذارد.

Resolution به Permission جاری و Base Revision خود Conflict نیاز دارد، Audit جدا می‌سازد و در Retry همان تصمیم idempotent است. General last-write-wins، Merge خودکار Owner/Due/State/Approval و تغییر مخفی Payload وجود ندارد.

## مدیریت دستگاه و Diagnostics

```http
GET  /api/v1/sync/devices
POST /api/v1/sync/devices/{registrationId}/revoke
GET  /api/v1/sync/diagnostics?projectId=<id>&deviceId=<id>
```

لغو دستگاه همه Sessionها و Leaseهای همان User/Device را می‌بندد. Remote wipe روی دستگاه کاملاً آفلاین تضمین‌پذیر نیست؛ کنترل واقعی، انقضای Lease، توقف Session، Scope حداقلی و Purge در اتصال بعدی است.

Diagnostics فقط Metadata عملیاتی بدون Payload، Token، عکس یا متن حساس برمی‌گرداند: زمان سرور، آخرین Handshake، انقضای Lease، Sequence نقطه کنترل، Watermark، فاصله Checkpoint، تعداد Conflict باز، تعداد Rejection/Replay اخیر و زمان آخرین Operation.

`recoveryState` در سرور یکی از وضعیت‌های `NeverSynchronized`، `Healthy`، `PendingPull`، `AttentionRequired`، `LeaseExpired` یا `DeviceRevoked` است و از Device/Lease/Checkpoint/Conflict/Rejected Receipt به‌صورت قطعی محاسبه می‌شود.

## Attachment

Attachment Queue مستقل از Operation Queue است. والد ابتدا Sync می‌شود، سپس Upload Session مجوزدار ساخته می‌شود؛ SHA-256، MIME و اندازه کنترل و Blob پس از تأیید Object Storage از دستگاه خالی می‌شود. Idempotency ساخت Upload Session به شناسه ثابت Attachment متصل است و با شماره تلاش تغییر نمی‌کند. در پیاده‌سازی فعلی Retry در مرز کل فایل ۲۵ MiB انجام می‌شود؛ Multipart resume، Malware Scanner و Quarantine بیرونی در Pilot Release Gate آزمون/تکمیل می‌شوند و این سند آن‌ها را انجام‌شده اعلام نمی‌کند.

## وضعیت‌های محلی

رابط `Queued`، `Syncing`، `Synced`، `Conflict`، `Rejected` و `Resolved` را جدا نگه می‌دارد. Pending Operation، Conflict حل‌نشده و Evidence ارسال‌نشده با Cache eviction پاک نمی‌شوند. Logout امن فقط با Purge پایگاه داده Scope همان Tenant/User انجام می‌شود و Device ID فیزیکی برای مدیریت دستگاه باقی می‌ماند.

## چرخه Recovery و تطبیق نهایی

Coordinator واحد برای هر Tenant/User/Project روی Startup، Reconnect، Manual Sync یا Retry اجرا می‌شود. چرخه ابتدا Operation و Attachment نیمه‌تمام را بازیابی می‌کند، سپس عملیات، Draft اولیه Quality/HSE و فایل‌ها را در Batchهای محدود ارسال می‌کند و در پایان وارد Verification می‌شود.

وضعیت پایدار چرخه یکی از `offline`، `recovering`، `pushing`، `uploading`، `verifying`، `succeeded`، `attention`، `retry-scheduled` یا `blocked` است. نتیجه موفق فقط وقتی ثبت می‌شود که صف محلی خالی، Conflict/Rejected تعیین‌تکلیف‌شده، Device و Lease معتبر و Checkpoint محلی با Checkpoint و Watermark سرور سازگار باشد.

Retry خودکار فقط برای خطای شبکه، Timeout، Rate Limit، خطای موقت سرور و Session/Checkpoint قابل بازیابی انجام می‌شود. تأخیرها به‌ترتیب ۵، ۱۵، ۴۵، ۱۲۰ و ۳۰۰ ثانیه و پس از آن حداکثر ۳۰۰ ثانیه هستند؛ `Retry-After` معتبر بین یک ثانیه و پانزده دقیقه اولویت دارد. Validation، Permission، Lease و Conflict به `attention` یا `blocked` می‌روند و خودکار پنهان نمی‌شوند.

زمان‌ها در IndexedDB و API به ISO/UTC می‌مانند. هر زمان Recovery/Retry که به کاربر نمایش داده می‌شود با تقویم هجری شمسی مشترک و منطقه زمانی تهران قالب‌بندی می‌شود.
