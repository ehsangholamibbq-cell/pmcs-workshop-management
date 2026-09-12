# Advisory Intelligence API — V1

## Endpoints

| Method | Path | Permission | کاربرد |
| --- | --- | --- | --- |
| `GET` | `/api/v1/projects/{projectId}/insights` | `insights.view` | فهرست خروجی‌های قابل مشاهده و درخواست‌های فعال |
| `POST` | `/api/v1/projects/{projectId}/insight-generation-requests` | `insights.generate` | ثبت idempotent درخواست در صف پایدار |
| `GET` | `/api/v1/projects/{projectId}/insight-generation-requests/{requestId}` | `insights.view` + مالک/بازبین | مشاهده وضعیت پردازش |
| `POST` | `/api/v1/projects/{projectId}/insights/{insightId}/accept` | `insights.review` | پذیرش انسانی متن مشورتی |
| `POST` | `/api/v1/projects/{projectId}/insights/{insightId}/dismiss` | `insights.review` | کنارگذاشتن انسانی متن مشورتی |

همه commandها به `Idempotency-Key` نیاز دارند. درخواست Generation در حالت `Pending` با HTTP 202 برمی‌گردد و Worker آن را به `Processing` و سپس `Succeeded/Failed` می‌برد. Retry خطاهای گذرا حداکثر سه تلاش با فاصله زمانی و lease بازیابی دارد.

## Preconditions

- OpenAI API key و نام مدل باید از Configuration/secret store تأمین شوند؛ هیچ کلید یا مدل hard-coded نیست.
- Project State Snapshot رسمی باید وجود داشته و revision پیکربندی آن با پروژه برابر باشد.
- Permission `insights.generate` هنگام ثبت و دوباره هنگام اجرای Worker بررسی می‌شود.
- AI آنلاین است و وارد Operation Queue آفلاین کارگاه نمی‌شود.

## Context boundary

همیشه فقط Project profile و Project State رسمی وارد Context می‌شوند. وضعیت مالی، تجاری و اقدامات باز تنها در صورت داشتن `financial-state.read`، `commercial-state.read` و `actions.read` اضافه می‌شوند. نام مسئول اقدام به مدل ارسال نمی‌شود. Insight پرچم ابعاد استفاده‌شده را نگه می‌دارد و Read API آن را از کاربر فاقد همان مجوز پنهان می‌کند.

WBS/Planning، Budget و HSE اختیاری‌اند. نبود یا فعال‌نبودن آن‌ها در `knownDataGaps` ثبت می‌شود و policy صریحاً استنتاج عملکرد منفی را ممنوع می‌کند.

## Output contract

Structured Output شامل فیلدهای زیر است:

- `insightType`, `statement`, `confidenceBand`, `potentialImpact`؛
- `evidenceReferences` محدود به citation manifest Context؛
- `factsUsed`, `assumptions`, `dataGaps`؛
- `suggestedActions` و `suggestedOwnerRole`.

علاوه بر Output، سامانه Snapshot ID، Context hash، Provider/Model، Provider response ID، Prompt/Policy version، دامنه داده، generated/expiry time و Review revision را ذخیره می‌کند.

## Review semantics

Insight با `NeedsReview` ایجاد می‌شود و فقط یک‌بار با base revision معتبر به `Accepted` یا `Dismissed` می‌رود. `Accepted` به‌معنی تأیید پرداخت، تغییر Project State، بستن ریسک یا ایجاد Action نیست. هر تبدیل آینده به Action باید command مستقل، Permission مستقل و تأیید انسانی جدا داشته باشد.

## Safe failures

کدهای `ai.provider.not_configured`, `ai.provider.unavailable`, `ai.provider.timeout`, `ai.provider.refusal`, `ai.provider.incomplete`, `ai.provider.invalid_response`, `ai.context.no_snapshot`, `ai.context.stale_snapshot`, `insight.citation.unsupported` حالت‌های صریح‌اند. متن خام پاسخ یا خطای Provider در API کاربر منتشر نمی‌شود.
