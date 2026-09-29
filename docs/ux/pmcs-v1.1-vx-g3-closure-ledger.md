# PMCS V1.1 — فهرست بسته‌شدن VX-G3

- شناسه: `PMCS-UX-VX-G3-CLOSURE-001`، نسخهٔ Candidate `0.2.0` در UX2-MS56.
- مرجع: `PMCS-RM-VISUAL-001`، `PMCS-DS-001`، `PMCS-UX-COMPONENT-STATES-001` و بستهٔ مرور `docs/ux/review/ms53/`.
- وضعیت: `VX-G3 System Ready` باز؛ این فهرست به‌تنهایی پذیرش Gate نیست.

شرط Gate، قرارداد Token/Component و Prototype تمام وضعیت‌های مرتبط است.
شاهد UI فعال برای شناخت شکاف و مهاجرت بعدی استفاده می‌شود؛ `D` در ماتریس
MS46 هم به معنی Qualification همهٔ مصرف‌کنندگان نیست. هر ردیف زیر با
بررسی رفتار در مرورگر، Artifact منبع‌دار و ثبت محدودهٔ تصمیم بسته می‌شود.

| خانوادهٔ قرارداد | شواهد موجود | شکاف قابل‌اقدام پیش از تصمیم G3 | مقصد |
| --- | --- | --- | --- |
| Token، قلم، برند و Motion | `PMCS-DS-001`؛ وزیرمتن 2.0.0 و Goldenهای Run 425/426؛ MS44/54 | ممیزی سازگاری semantic tokens، کنتراست و reduced motion در نمونه‌های قراردادی؛ معیارهای بصری باقی‌ماندهٔ مالک | G3 contract؛ آزمون همهٔ مصرف‌کنندگان در G5 |
| Action، Field، Status، Feedback و Table | MS47 نه وضعیت، MS44 ده سناریو، MS51 ردیف موبایل؛ Browser/Artifactهای 419/433 | ثبت حالت‌های مرتبط برای هر خانواده در ماتریس، به‌ویژه فرم/تاریخ/فیلتر و بیان Success/Error؛ تطبیق نام/رفتار با قرارداد | برش‌های Foundation G3؛ مهاجرت فعال G4 |
| Shell، Navigation، Dialog و Overlay | MS49 و MS51 با Focus، Escape و وضعیت‌های بسته؛ MS52/54 ترکیب Login/Shell/Chart | پوشش معنایی مسیرهای نمونه و بازبینی چهار معیار MS53؛ Navigation موبایل UI فعال هنوز قرارداد جایگزین ندارد | G3 نمونه؛ شش Shell فعال در G4 |
| Avatar، برش تصویر و حریم خصوصی | MS44 فقط fallback ایستا؛ MS55 نمونهٔ مستقل هشت وضعیت و برش محلی با Browser/Artifact | آزمون نسخه/مجوز/امنیت فایل و پیاده‌سازی نهایی در پروفایل فعال، خارج از ادعای نمونه | G3 نمونهٔ خانواده؛ مهاجرت/Qualification در G4/G5 |
| Wizard تکثیر، انتخاب، Conflict و Confirmation | MS36 Preview مسدود UI فعال و MS44 سناریوی نمایشی؛ MS56 نمونهٔ تعاملی انتخاب/تعارض/مسدود/تأیید محلی با Run 445 و Artifact `11020397707` معتبر | آزمون Revision/Permission، ساخت و اجرای واقعی در Wizard فعال و Qualification مستقل؛ نمونه هیچ عملیاتی انجام نمی‌دهد | G3 نمونهٔ خانواده پس از CI؛ سپس G4/G5 |
| Attachment، Evidence و Collaboration | قرارداد فایل و تبدیل‌های MS16–31، Chat گروه پروژه و Conflict MS35؛ MS44 نمای محدود | نمونهٔ دیداری مشترک پیش‌نمایش/قرنطینه/Released/رد، مجوز و منشأ؛ پیام خصوصی/تماس خارج از دامنه | برش مستقل G3؛ سپس G4/G5 |
| Reporting، Print و OutputAccess | MS43 برگهٔ محدود A4؛ MS44/52 نمای گزارش، Chart و PDF نمونه؛ Goldens رسمی فونت | الگوی Print A4/A3، عمودی/افقی و تفاوت Snapshot رسمی از Draft؛ وضعیت درخواست/آماده/رد/منقضی و مجوز دانلود | G3 contract/prototype؛ صلاحیت خروجی در G5 |
| Executive Intelligence | MS44 سناریوی AI با برچسب پیشنهاد آزمایشی و بدون Fact | Prototype Workspace با منشأ، تازگی، عدم قطعیت، مجوز و مرز اقدام انسانی؛ Agent به Chat ساده تبدیل نشود | برش مستقل G3؛ سپس G4/G5 |

## تصمیم مالک و ترتیب Gate

مالک جهت کلی بصری را با پیگیری تراکم موبایل و متن فنی فارسی پذیرفت؛ MS54
هر دو را در نمونهٔ مستقل اصلاح کرد. این پاسخ، تأیید جداگانهٔ چهار معیار
هویت/خوانایی، حقیقت State، تعامل و چاپ نیست. برای بستن G3 باید شکاف‌های
مرتبط بالا با شاهد نمونه و Contract تعیین تکلیف شوند و تصمیم صریح مالک روی
بستهٔ به‌روز ثبت شود. پس از آن مهاجرت مرحله‌ای G4، Qualification G5، و
سپس INT1/QA1 در ترتیب Roadmap می‌آیند. Feature Flagهای Collaboration،
Reporting/OutputAccess/Worker در defaults خاموش و `PdfLicense=Unconfigured`
می‌مانند.
