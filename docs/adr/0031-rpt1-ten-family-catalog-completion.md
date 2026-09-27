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

### وضعیت اجرای تصمیم در Safe Checkpoint جاری

این ستون تاریخی «وضعیت در زمان تصمیم» را بازنویسی نمی‌کند. تا Safe Checkpoint
`PMCS-V1.1-RPT1-S07-MS21-C1`، خانواده‌های `RPT1-F01` تا `RPT1-F06` قرارداد معنایی، Runtime،
Renderer/Golden، Catalog/API/Worker و Qualification End-to-End مستقل دارند. `RPT1-F07` تا
`RPT1-F10` همچنان `Required / Not Implemented` هستند؛ بنابراین Gate خروج RPT1 طبق همین ADR باز است.
این Snapshot تاریخی MS21 باقی می‌ماند. در Checkpoint `PMCS-V1.1-RPT1-S07-MS22-C1`
DoR و قرارداد مستقل `PMCS-RPT1-F07-SEMANTIC-001 v1.0.0` با Evidence سبز Run 205
برای Document/RFI/Submittal/Transmittal بسته شد. F07 اکنون
`Contract Ready / Runtime Not Implemented` است؛ F08 تا F10 هنوز
`Required / Not Implemented` و Gate خروج RPT1 بازند. گام بعد فقط Runtime Core محدود F07 است.

در Checkpoint `PMCS-V1.1-RPT1-S07-MS23-C1`، Runtime Core محدود F07 با Run 211
(`36296626010`) و تمام هشت Job سبز شد. چهار دفتر در Application Contract نسخه‌دار قرار گرفتند؛
RFI/Submittal با history میانی ناقص صریحاً `InsufficientData` هستند. F07 هنوز Renderer/Golden،
historical producer کامل و Catalog/API/Worker ندارد و End-to-End یا Qualified نیست. F08 تا F10
بازند؛ تصمیم ده‌گانه و Gate خروج این ADR تغییر نکرده است. گام بعد فقط Renderer/Golden F07 است.

در Checkpoint `PMCS-V1.1-RPT1-S07-MS24-C1`، PDF/XLSX Renderer محدود F07 و Goldenهای
binary/visual با Run 215 (`36301524177`) و تمام هشت Job سبز شدند. RFI/Submittal legacy با
history ناقص همچنان `InsufficientData` و count نامعلوم‌اند. Historical transition producer
و Catalog/API/Worker متصل باقی مانده‌اند؛ F07 End-to-End یا Qualified نیست و Gate خروج ده‌گانه
باز است. گام بعد `S07-MS25` فقط producer تاریخی در مالک TechnicalOffice است.

در Checkpoint `PMCS-V1.1-RPT1-S07-MS25-C1`، producer تاریخچهٔ transitionهای RFI/Submittal
برای رکوردهای تازه با Migration 49 و بدون backfill قدیمی افزوده شد. Run 219 هشت Job را سبز
کرد؛ F07 هنوز Catalog/API/Worker و Qualification متصل ندارد. Scope ده‌گانه و defaultهای خاموش
تغییر نکرده‌اند؛ MS26 گام اتصال مستقل F07 است.

در Checkpoint `PMCS-V1.1-RPT1-S07-MS26-C1`، Definition/Template و مسیر
Catalog/API/Worker F07 با Migration 50، هر دو مجوز Technical read، Source مالک و
Rendererهای پین‌شده متصل شد. Run 222 (`36305583760`) هر هشت Job و آزمون متصل F07
`17/17` را سبز کرد؛ نقش فاقد `technical.confidential.read` دسترسی ندارد و defaultهای
Production خاموش‌اند. بنابراین F01 تا F07 End-to-End checkpointed و F08 تا F10 بازند؛
Gate خروج ده‌گانه همچنان باز و گام بعد DoR/قرارداد معنایی مستقل F08 است.

در `PMCS-V1.1-RPT1-S07-MS27-C1`، قرارداد معنایی مستقل F08 با Run 224 و هشت Job سبز شد.
این Checkpoint صرفاً DoR/Source/Classifications/Golden Matrix را می‌بندد؛ Runtime، Renderer
و wiring F08 هنوز بازند. F01–F07 متصل، F09/F10 باز، Gate ده‌گانه و defaults خاموش ثابت‌اند.

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
