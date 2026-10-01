# ADR 0033 — تفکیک تحویل Foundation نسخهٔ 1.1 از اتصال زندهٔ Agent

- وضعیت: Accepted / تصمیم مالک محصول، ۲۰۲۶-۱۰-۰۱
- Change record: `V1.1-INT1-LIVE-GATE-DEFERRAL`
- Parent: ADR 0032، `PMCS-V1.1-INT1-DOR-001` و `PMCS-V1.1-INT1-QC1`
- اثر: زمان‌بندی Gate اتصال زنده و پیش‌نیاز QA1؛ معیارهای فنی ADR 0032 پابرجا هستند.

## زمینه و تصمیم

کد Foundation، آزمون‌های قراردادی سه آداپتور و سناریوی متصل با Provider ساختگی در
نسخهٔ 1.1 آماده‌اند. مالک محصول هنوز API key و Model ID سه Provider را نساخته است و
ممکن است آن‌ها را پس از قفل نسخهٔ 1.1 فراهم کند. نبود این شواهد نباید با موفقیت اتصال
واقعی اشتباه شود؛ در عین حال نباید انتشار قابلیت‌های مستقل V1.1 را نامحدود معطل کند.

1. خروجی `V1.1-INT1` فقط **Foundation غیرفعال برای انتشار** است: Gateway و سه آداپتور،
   انتخاب و Profile نسخه‌دار، مرز Permission و Tool، Run مرجع، Audit و مهاجرت‌ها با
   آزمون‌های قراردادی، منفی، DB ایزوله و CI همان Source پذیرفته می‌شوند. این یک
   Checkpoint محدود است و `AGENT-S1` را Qualified نهایی اعلام نمی‌کند.
2. در Baseline نسخهٔ 1.1، `Intelligence:INT1ReferenceEnabled` خاموش است؛ QA1 باید
   غیرفعال‌بودن مسیر Reference Run، default-deny مجوزها، نبود Provider/Model فعال و
   Secret در تنظیمات انتشار، و عدم اثر مسیر جدید بر Advisory تاریخی V1 را بررسی کند.
   روشن‌کردن INT1 یا اعطای Grant راه‌انداز در Pilot/Production نسخهٔ 1.1 مجاز نیست.
3. `AGENT-S1-LIVE` نخستین Gate در فاز Intelligence پس از قفل Baseline نسخهٔ 1.1 است؛
   Stage تازه‌ای به برنامهٔ هفت‌مرحله‌ای اضافه نمی‌کند. در محیط امن با Credential
   پیکربندی‌شده، اتصال واقعی و هر دو قابلیت structured output و tool calling برای
   OpenAI/GPT، Google/Gemini و Anthropic/Claude ثبت می‌شود. `allAvailable=false` یا
   نبود Credential این Gate را می‌بندد؛ کد خروج صفرِ Probe بدون Credential شاهد قبولی
   نیست. سپس تطبیق کامل ADR 0032، ریسک‌ها و Checkpoint نهایی Stage 1 انجام می‌شود.
4. `AGENT-S2` و فعال‌سازی INT1 در Pilot/Production تنها پس از `AGENT-S1-LIVE` با
   Evidence مستقل مجازند. QA1 نسخهٔ 1.1 پس از Checkpoint Foundation محدود می‌تواند
   آغاز شود و فقط نسخهٔ فاقد Runtime فعال Agent را Qualified می‌کند. قفل V1.1،
   Stage 1 را کامل یا Agent را قابل استفاده اعلام نمی‌کند.

## Evidence و ریسک باقی‌مانده

- Source `65ec8f1814caf4f240688972f0ad7857cd134cb5` در Run 674
  (`36795510420`) و Candidate مستندات در Run 675 (`36796873924`) هر کدام هشت Job
  سبز دارند. این شواهد فقط آزمون قراردادی و fixture محلی را اثبات می‌کنند.
- `R-AI-03` برای اتصال واقعی تا `AGENT-S1-LIVE` باز می‌ماند؛ Release نسخهٔ 1.1
  ریسکِ قابلیت خاموش را با آزمون عدم فعال‌سازی و نبود Secret کنترل می‌کند. در صورت
  شکست هر آزمون فعال‌نبودن، QA1 و Release fail-closed می‌شوند.
- هر تغییر در Adapter، مدل/endpoint یا Policy پس از این Candidate به Source و آزمون
  دوباره نیاز دارد. هیچ کلید یا پاسخ خام Provider در مخزن یا Evidence عمومی ثبت نمی‌شود.

این ADR بند زمان‌بندی آزمون زندهٔ ADR 0032 و بند ترتیب QA1 در DoR را برای نسخهٔ 1.1
اصلاح می‌کند. سایر قواعد Permission، داده، fallback و قرارداد فنی ADR 0032 تغییر ندارند.
