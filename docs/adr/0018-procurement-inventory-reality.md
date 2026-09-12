# ADR 0018 — تفکیک واقعیت خرید، دریافت، موجودی و مصرف

- Status: Accepted
- Date: 2026-09-11

## Context

تأیید درخواست خرید، صدور سفارش، رسیدن محموله، پذیرش بازرسی، ورود به انبار، تحویل به اکیپ و مصرف، رویدادهای متفاوت‌اند. یکی‌گرفتن این رویدادها موجودی و تعهد غیرواقعی می‌سازد. از طرف دیگر، نبود WBS یا بودجه اولیه نباید ثبت نیاز واقعی کارگاه را متوقف کند و پذیرش فنی Submittal نیز نمی‌تواند جای دریافت و بازرسی کالا را بگیرد.

## Decision

- `Commercial` مالک Purchase Request و Purchase Order موجود باقی می‌ماند و Supply Reality همین زنجیره را بدون ساخت سفارش موازی ادامه می‌دهد.
- درخواست مقداری فقط با `SupplyItem` نسخه‌دار، مقدار، واحد و محل تحویل معتبر است؛ WBS، Work Item و Budget Line پیوندهای اختیاری‌اند.
- `SupplyItem` واحد پایه و تاریخچه افزایشی Conversion را نگه می‌دارد؛ هر تراکنش نسخه Conversion استفاده‌شده را ثبت می‌کند.
- `GoodsReceipt` فقط دریافت فیزیکی است. نتیجه بازرسی و `PostStock` فرمان‌های مستقل با Revision و Permission مستقل‌اند.
- اضافه‌تحویل فقط با دلیل صریح و Permission جدا ثبت می‌شود و عامل تأیید در Receipt باقی می‌ماند.
- فقط مقدار `Accepted` و فقط با محل فعالِ قابل مصرف، رخداد مثبت دفتر موجودی می‌سازد. مردودی و قرنطینه موجودی قابل مصرف نیستند.
- موجودی از جمع `InventoryLedgerEntry`ها محاسبه می‌شود. Ledger در API و پایگاه‌داده append-only است؛ Update/Delete با Trigger رد می‌شود.
- Issue یک رخداد خروج موجودی و آغاز Custody است؛ Consumption، Return و Waste فقط با Reconciliation افزایشی ثبت می‌شوند. پرت نیازمند علت و Evidence است.
- Transfer دو Entry متوازن با Transaction ID مشترک می‌سازد. Issue، Transfer و Adjustment تحت تراکنش Serializable و کنترل موجودی منفی اجرا می‌شوند.
- Physical Count مقدار دفتر را بازنویسی نمی‌کند؛ اختلاف پیشنهادی، Review مستقل و سپس رخداد Adjustment ایجاد می‌کند.
- `ServiceAcceptance` برای خدمت و اجاره تجهیز مستقل است و هیچ رخداد موجودی ایجاد نمی‌کند.
- همه Queryها با Tenant/Project و همه Commandها علاوه بر آن با Actor، Permission، Idempotency، Audit و Outbox محدود می‌شوند.

## Consequences

- «دریافت‌شده»، «پذیرفته‌شده»، «موجود»، «تحویل‌شده» و «مصرف‌شده» قابل Drill-down و قابل تفکیک باقی می‌مانند.
- سفارش‌های قدیمی که قلم و مقدار عملیاتی ندارند، مبنای دریافت کالا یا پذیرش خدمت قرار نمی‌گیرند.
- نقش ثبت‌کننده Receipt نمی‌تواند صرفاً با همان مجوز نتیجه بازرسی یا اصلاح موجودی را تصویب کند.
- اجرای واقعی Migration، رفتار Serialization Failure و Trigger دفتر موجودی باید روی PostgreSQL Integration کنترل شود.
- RFQ/Quote/Evaluation، Revision سفارش، Expediting، Reservation، Return-to-vendor، Invoice three-way match و Draft آفلاین زنجیره تأمین Sliceهای بعدی‌اند و از این تصمیم استنتاج نمی‌شوند.
