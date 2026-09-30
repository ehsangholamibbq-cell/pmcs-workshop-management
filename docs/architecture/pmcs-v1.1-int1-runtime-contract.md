# INT1 — قرارداد Runtime مرجع و شواهد باز

- Parent: `PMCS-V1.1-INT1-DOR-001` و ADR 0032.
- وضعیت: پیاده‌سازی در جریان؛ این سند checkpoint خروج INT1 نیست.
- Feature flag: `Intelligence:INT1ReferenceEnabled=false` به‌صورت پیش‌فرض. روشن‌کردن در استقرار تنها پس از Gate مستقل و credential معتبر مجاز است.

## سطح محدود

`POST /api/v1/projects/{projectId}/intelligence/reference-runs` با `RequestId` یکتا، پرسش متنی محدود و `Idempotency-Key`، فقط یک فراخوانی ابزار خواندنی ثبت‌شده و یک پاسخ ساختاریافته انجام می‌دهد. `GET .../{runId}` فقط metadata درخواست همان کاربر را نشان می‌دهد؛ پاسخ متنی و payload ابزار در DB، Outbox، idempotency receipt و Audit ذخیره نمی‌شوند. تکرار درخواست با کلید یکسان فقط metadata برمی‌گرداند و پاسخ گذرا را دوباره تولید نمی‌کند. این سطح، Chat/Agent عمومی یا Action نوشتنی نیست.

هر Run یک Session مستقل یک‌نوبتی با شناسه و انقضای ده‌دقیقه‌ای دارد. هیچ تاریخچهٔ گفتگو برای نوبت بعد نگهداری یا بازاستفاده نمی‌شود؛ RequestId همان شناسهٔ Run و SessionId مجزا است. Context فقط در حافظهٔ همان درخواست و در محدودهٔ timeout Profile حضور دارد.

قبل از Run، `insights.generate`، وجود پروژه، انتخاب Profile نسخه‌دار، scope، طبقه‌بندی `Confidential`، قابلیت‌های `StructuredOutput | ToolCalling`، Provider فعال‌شدهٔ مستقل، مدل فعال و تأییدشده، پیکربندی adapter و نرخ مصرف بررسی می‌شوند. Tool Registry در هر فراخوانی `insights.generate` و Permission منبع manifest را دوباره می‌سنجد و فقط به Application Contract ماژول مالک می‌رود. خروجی‌های Reporting فقط metadata هستند؛ Collaboration حداکثر هشت پیام، هر متن تا ۵۰۰ نویسه، بدون فایل/ضمیمه برمی‌گرداند. آداپتور هیچ SQL، URL ابزار داخلی یا secret در اختیار مدل نمی‌گذارد.

سطح مدیریت `GET /api/v1/intelligence/admin/profiles/preview` تصمیم مدل، محدودهٔ پروژه، مجوز اجرا، پیکربندی adapter و هزینهٔ سقف را بدون Secret نشان می‌دهد. Preview خود دسترسی اجرای مدل یا ابزار نمی‌دهد. Latency از timestampهای Run مشتق و همراه usage/cost و علت خطا در metadata/Audit ثبت می‌شود.

سه آداپتور OpenAI، Google Gemini و Anthropic Claude تصمیم native function call را به شناسهٔ manifest نگاشت می‌کنند. ورودی ابزار با `projectId` درخواست و schema بسته مقایسه می‌شود. پاسخ نهایی باید یک `answer` محدود و تنها citation همان ابزار داشته باشد؛ پاسخ ناقص، ابزار ناشناخته، تغییر ابزار در fallback و مصرف بالاتر از سقف رد می‌شوند. Fallback فقط برای timeout، unavailable و invalid response به مدل تأییدشدهٔ همان Profile با محدودیت برابر انجام می‌شود.

نرخ‌های `input/output microunits per token` سقف محافظه‌کارانه‌ای هستند که مدیر ارشد هنگام ثبت نسخهٔ مدل اعلام می‌کند؛ هزینهٔ ثبت‌شده برآورد محاسبه‌شده از usage Provider با این نرخ است و صورتحساب Provider نیست. مدل نسخهٔ قدیمی با نرخ صفر برای انتخاب/Run بسته می‌ماند تا نسخهٔ قیمت‌گذاری‌شده منتشر و تأیید شود. قبل از شروع، مصرف بدبینانهٔ سقف token با بودجه سنجیده می‌شود و usage نهایی دوباره بررسی می‌شود.

## Gateهای باز

- CI کامل یک SHA واحد، آزمون‌های منفی cross-tenant/project و سناریوی end-to-end Run با DB و Provider ساختگی.
- تست اتصال واقعی سه Provider با credential پیکربندی‌شده و ثبت `Available/Unavailable`؛ credential در مخزن/CI عمومی نگهداری نمی‌شود.
- بررسی مستقل timeout/cancellation، بازیابی Runهای `Running` رهاشده، انقضای Session و اعتبارسنجی عملیاتی migration/restore.
- Audit و telemetry metadata-only و بررسی مستقل هزینه، fallback و عدم نشت payload در log.

در lineage هر Run، مدل و نسخهٔ اولیه همراه Provider و نسخهٔ فعال‌سازی اولیه ثبت می‌شود؛ اگر fallback رخ دهد، مدل/Provider و نسخهٔ مقصد جداگانه در فیلدهای انتخاب نهایی و دلیل خطا ثبت می‌شوند. نسخهٔ Provider پیش از هر ارسال به Provider دوباره کنترل می‌شود تا disable/re-activate میان دو گام، Run را fail-closed کند.
