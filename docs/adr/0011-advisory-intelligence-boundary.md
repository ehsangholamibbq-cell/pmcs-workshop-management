# ADR 0011 — Advisory Intelligence Boundary

- Status: Accepted
- Date: 2026-09-10

## Context

PMCS باید داده ساختاریافته Project State، مالی، تجاری و اقدامات را به تحلیل مدیریتی تبدیل کند؛ اما مدل زبانی موتور محاسبات قطعی، مرجع حقیقت، تأییدکننده سند یا تصمیم‌گیر سازمانی نیست. داده‌های قابل استفاده نیز برای هر کاربر متفاوت‌اند. نبود WBS، بودجه مبنا یا HSE ممکن است انتخاب معتبر پیکربندی باشد و نباید به ریسک یا عملکرد بد تعبیر شود.

## Decision

- bounded context مستقل `Intelligence` فقط Advisory Insight تولید می‌کند و هیچ write path به Fact، Metric، Project State، سند مالی، قرارداد یا اقدام رسمی ندارد.
- هر درخواست ابتدا با `Idempotency-Key` در durable queue ثبت می‌شود. Worker دارای lease، retry محدود و safe failure code است؛ قطع سرویس مدل به وضعیت رسمی پروژه صدمه نمی‌زند.
- Context از آخرین Project State Snapshot رسمی و به‌روز ساخته می‌شود. Financial، Commercial و Action فقط در صورت مجوز جداگانه درخواست‌کننده اضافه می‌شوند و Permission درست پیش از پردازش دوباره بررسی می‌شود.
- Insight دامنه داده استفاده‌شده را ذخیره می‌کند. کاربری که مجوز یکی از ابعاد Context را ندارد، آن Insight را در Read API نمی‌بیند.
- درخواست OpenAI Responses API با Structured Outputs سخت‌گیرانه، JSON Schema نسخه‌دار و `store=false` ارسال می‌شود. نام مدل از Configuration می‌آید و در کد ثابت نیست.
- Evidence referenceهای خروجی باید عضو citation manifest همان Context باشند؛ ارجاع ناشناخته باعث عدم انتشار خروجی می‌شود. Facts، assumptions و data gaps جدا نگه داشته می‌شوند.
- Prompt، policy، model، provider response id، context hash، snapshot id، زمان تولید و انقضا ذخیره می‌شوند.
- خروجی ابتدا `NeedsReview` است. پذیرش یا کنارگذاشتن با Permission، base revision، Audit و Outbox ثبت می‌شود؛ پذیرش فقط Review Status را تغییر می‌دهد و هیچ اقدام رسمی نمی‌سازد.
- Refusal، incomplete response، schema failure و provider failure حالت‌های صریح‌اند و متن خام خطای سرویس خارجی به کاربر یا Log داده‌ای منتقل نمی‌شود.

## Consequences

- تحلیل قابل ردیابی و قابل ابطال است، ولی جای حقیقت محاسباتی را نمی‌گیرد.
- Permission leakage هم در retrieval و هم در readback کنترل می‌شود.
- نبود تنظیم OpenAI به‌صورت capability unavailable نمایش داده می‌شود و داده نمونه یا پاسخ ساختگی ایجاد نمی‌شود.
- ساخت Action از پیشنهاد، تحلیل Portfolio و retrieval از اسناد آزاد به Vertical Sliceهای بعدی و Permissionهای مستقل نیاز دارد.
