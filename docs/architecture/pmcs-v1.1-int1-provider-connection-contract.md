# INT1 — قرارداد مسیر اتصال Provider

- شناسه: `PMCS-V1.1-INT1-PROVIDER-001`
- وضعیت: مسیر Probe و compatibility fixture؛ این سند ادعای اتصال زنده یا Gateway کامل ندارد.
- Parent: `PMCS-V1.1-INT1-DOR-001` و ADR 0032

سه آداپتور مستقل `OpenAI`، `GoogleGemini` و `AnthropicClaude` از تنظیمات `OpenAI:*`،
`Gemini:*` و `Anthropic:*` یا متغیر محیطی هم‌نام با حروف بزرگ استفاده می‌کنند. Model ID
پیکربندی است و کلید فقط در Header درخواست خارجی قرار می‌گیرد. API هیچ کلید یا پاسخ خام
Provider را برنمی‌گرداند. Probe فقط متن ثابت بی‌ارتباط با دادهٔ PMCS می‌فرستد و خروجی
بستهٔ `{"ok":true}`، پایان کامل پاسخ و سقف ۳۲ KiB را بررسی می‌کند.

`Unavailable` برای نبود کلید/مدل، `Unverified` برای تنظیم بدون آزمون، `Available` فقط
پس از پاسخ معتبر همان آزمون، و `Failed` برای خطای واقعی برگردانده می‌شود. این وضعیت
موقتی Probe هنوز فعال‌سازی Model Catalog یا مجوز اجرای Run نیست. فهرست وضعیت از مسیر
`GET /api/v1/intelligence/admin/providers` فقط با Grant مستقل خواندن Catalog و Probe
از مسیر `POST /api/v1/intelligence/admin/providers/{provider}/probe` فقط با Grant مستقل
مدیریت Provider مجاز است. Probe محدودیت نرخ دارد و هیچ تغییر داخلی PMCS انجام نمی‌دهد.

Compatibility suite محلی با Handler جعلی، شکل درخواست، مقصد، مصرف، نبود Credential و
پاسخ ناقص هر سه آداپتور را می‌سنجد. آزمون اتصال زنده با کلیدهای پیکربندی‌شده در محیط
ایزوله و Grant مدیریتیِ صریح باید جدا ثبت شود؛ نبود کلید نتیجهٔ موفقیت نیست. قابلیت
tool calling، Profile، selection و fallback هنوز در MSهای بعدی INT1 بازند.

مراجع API هنگام طراحی: [OpenAI Responses](https://platform.openai.com/docs/api-reference/responses)،
[Gemini generateContent](https://ai.google.dev/api/generate-content)،
[Claude Messages](https://platform.claude.com/docs/en/api/messages/create) و
[Claude structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs).
