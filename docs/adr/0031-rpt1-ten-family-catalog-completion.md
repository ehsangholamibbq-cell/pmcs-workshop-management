# ADR 0031 — حفظ و تکمیل کاتالوگ ده‌گانه RPT1

- شناسه تصمیم: `PMCS-RPT1-CATALOG-DECISION-001`
- وضعیت: Accepted
- تاریخ: ۱۴۰۵/۰۶/۲۸ (۲۰۲۶-۰۹-۱۹)
- Parent checkpoint: `PMCS-V1.1-RPT1-S06-MS06-C1`
- تصمیم‌گیر: Product owner؛ انتخاب صریح «حفظ هر ۱۰ خانواده»
- تصمیم جایگزین‌شده: ابهام باز میان تکمیل ۹ خانواده یا کاهش Scope نسخه‌دار

## Context

Roadmap مؤثر RPT1 از ابتدا ده خانواده گزارش استاندارد را داخل Scope قرار داده است. Runtime و Evidence
فعلی فقط خانواده اول، یعنی `daily-report-certified/1.0.0`، را پیاده و با XLSX/PDF Golden، visual،
performance، permission، snapshot و audit qualify کرده‌اند. زیرساخت مشترک Reporting به‌تنهایی به‌معنی
پیاده‌شدن نه خانواده دیگر نیست و RPT1 نمی‌تواند با ادعای کاتالوگ کامل بسته شود.

Canonical Reference و Checkpoint `S06-MS06` این اختلاف را عمداً به یک تصمیم صریح مالک محصول موکول
کردند و استنتاج آن از code یا Conversation را ممنوع دانستند.

## Decision

### ۱. Scope ده‌گانه بدون کاهش حفظ می‌شود

هر ده خانوادهٔ استاندارد زیر داخل Gate خروج `V1.1-RPT1` باقی می‌مانند. نه خانوادهٔ باقی‌مانده به
V1.2 یا Stage دیگری منتقل نمی‌شوند و RPT1 تا Qualification مستقل همه آن‌ها بسته نخواهد شد.

| شناسه خانواده | خانواده مصوب | وضعیت در زمان تصمیم |
| --- | --- | --- |
| `RPT1-F01` | گزارش روزانه رسمی و زنجیره اصلاحات | Qualified؛ `daily-report-certified/1.0.0` |
| `RPT1-F02` | گزارش هفتگی و ماهانه پروژه | Required؛ Not Implemented |
| `RPT1-F03` | گزارش مدیریتی / Executive Project State | Required؛ Not Implemented |
| `RPT1-F04` | پیشرفت، Planned/Actual/Variance و S-Curve با Baseline معتبر | Required؛ Not Implemented |
| `RPT1-F05` | مالی، Cash Position، تعهدات، Aging و بودجه در صورت پیکربندی | Required؛ Not Implemented |
| `RPT1-F06` | قرارداد، اصلاحیه، خرید و تأمین | Required؛ Not Implemented |
| `RPT1-F07` | دفتر فنی شامل Document/RFI/Submittal/Transmittal | Required؛ Not Implemented |
| `RPT1-F08` | Quality و HSE با رعایت Classification | Required؛ Not Implemented |
| `RPT1-F09` | Issue، Risk، Decision، Escalation و Action | Required؛ Not Implemented |
| `RPT1-F10` | Portfolio Summary با تفکیک دسترسی و ارز و بدون تبدیل پنهان | Required؛ Not Implemented |

### ۲. اجرا فقط به‌صورت Micro-Slice مستقل

- هر خانواده DoR، semantic/source contract، permission/classification، وضعیت‌های
  `NoData/NotConfigured/InsufficientData`، Template Version و Golden مخصوص خود را پیش از Done شدن
  دریافت می‌کند.
- هر خانواده جداگانه `Implement → Test → Fix → Retest → Verify → Checkpoint → Atomic Commit`
  می‌شود؛ اجرای یک‌جای نه خانواده یا اعلام Done گروهی ممنوع است.
- زیرساخت مشترک موجود reuse می‌شود، اما تعمیم speculative یا اتصال مستقیم به Persistence ماژول دیگر
  ممنوع می‌ماند.
- Definition/Template خانواده‌ای که Runtime معتبر ندارد صرفاً برای پرکردن Catalog seed نمی‌شود.
- ترتیب شروع مطابق کاتالوگ است؛ Micro-Slice بعدی `RPT1-F02`، گزارش هفتگی و ماهانه پروژه است. هر تغییر
  ترتیب فقط با dependency ثبت‌شده در Checkpoint مجاز است.

### ۳. Gate خروج RPT1

RPT1 فقط زمانی قابل بسته‌شدن است که برای هر ده خانواده حداقل این Evidence ثبت شده باشد:

1. قرارداد معنایی و source lineage نسخه‌دار؛
2. permission، tenant/project isolation و classification fail-closed؛
3. snapshot/as-of، determinism، audit و idempotency؛
4. خروجی‌های مصوب همان خانواده با Golden و parse مستقل؛
5. visual/RTL/Jalali و performance/size budget متناسب؛
6. Full CI سبز و Checkpoint قابل Resume.

وجود Foundation مشترک، Qualification خانواده اول یا نمایش placeholder در UI هیچ خانواده دیگری را
Done نمی‌کند. `RPT1-F01` نیز دوباره طراحی نمی‌شود و Evidence معتبر MS05/MS06 آن حفظ می‌شود.

### ۴. مرز این تصمیم

- هیچ API، Migration، Renderer، feature flag یا Production setting در این ADR تغییر نمی‌کند.
- Baseline قفل‌شده V1، معماری Reporting و ترتیب کلان Roadmap تغییر نمی‌کنند.
- UI اختصاصی Reporting همچنان طبق Roadmap در UX2 اجرا می‌شود؛ این ADR مالکیت آن Gate را تعیین نمی‌کند.
- شناسه Runtime و Template Version خانواده‌های F02 تا F10 فقط در DoR همان خانواده قطعی می‌شود.

## Alternatives Rejected

| گزینه | دلیل رد |
| --- | --- |
| محدودکردن RPT1 به Foundation و گزارش روزانه | انتخاب صریح مالک محصول حفظ Scope مصوب است |
| انتقال ضمنی ۹ خانواده به V1.2 | تغییر بدون Change Record و مغایر کاتالوگ مؤثر |
| اعلام Done براساس زیرساخت مشترک | فاقد semantic contract، renderer و Golden خانواده‌ای |
| پیاده‌سازی هم‌زمان ۹ خانواده | غیرقابل Resume، پرریسک و ناسازگار با روش Micro-Step |
| seed کردن placeholder برای نمایش کاتالوگ | ایجاد ادعای قابلیت بدون Runtime و Evidence |

## Consequences

- اختلاف Scope رسماً حل می‌شود، اما نه خانواده همچنان کار باز و قابل‌اندازه‌گیری RPT1 هستند.
- زمان بسته‌شدن RPT1 به تکمیل واقعی F02 تا F10 وابسته است؛ Scope برای کوتاه‌کردن زمان کاهش نمی‌یابد.
- Micro-Step بعدی فقط DoR و قرارداد معنایی `RPT1-F02` را تثبیت می‌کند و پیش از آن هیچ Runtime جدیدی
  Done ادعا نمی‌شود.
- هر تصمیم آینده برای کاهش یا انتقال خانواده‌ها باید این ADR را با Change Record نسخه‌دار و تأیید صریح
  مالک محصول supersede کند.
