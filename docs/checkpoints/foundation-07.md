# Foundation checkpoint 07 — Portfolio Command Center V1

- Date: 2026-09-10
- Status: Implemented and locally validated
- Product source: PMCS Product & System Blueprint V1.9

## Outcome

مدیرعامل اکنون تمام پروژه‌های مجاز را در یک مرکز فرمان فارسی می‌بیند. این View وضعیت عملیاتی، مالی و تجاری را مستقل نگه می‌دارد، نبود داده را صریح نشان می‌دهد، مبالغ را فقط در ارز همسان جمع می‌کند و استثناها را همراه مسئول و سررسید به تصمیم مدیریتی وصل می‌کند. هیچ داده نمونه جدیدی تزریق نشده است.

## Implemented

- endpoint نسخه‌دار `GET /api/v1/portfolio/command-center` با قرارداد `portfolio-command-center-v1`؛
- Permission مستقل `portfolio.read` و اعمال مجدد scope برای Project State، Finance، Commercial و Actions؛
- Registry پروژه‌های مجاز با lifecycle، مدل قرارداد، Planning mode و مدیر/مدیران پروژه؛
- Project card با Operational/Freshness/Coverage، Financial/Data Quality، Commercial/Approval و Action summary مستقل؛
- نبود Snapshot به‌صورت `NoData` و وجود Fact تأییدشده بدون Snapshot به‌صورت `needsCalculation`؛
- تشخیص Snapshot قدیمی از Revision پروژه و تغییر جدیدتر Daily Report تأییدشده؛
- حفظ صریح حالت‌های اختیاری WBS، Budget و HSE بدون صفر یا رنگ سبز مصنوعی؛
- Exposure مالی/تعهد فقط به تفکیک ارز، بدون FX یا جمع کل چندارزی؛
- حداکثر ۵۰ Action Exception بین‌پروژه‌ای با Owner، Due Date، Priority، Status و Revision؛
- رابط فارسی و RTL در `/portfolio` با جست‌وجو، فیلتر lifecycle/attention، مرتب‌سازی و drill-down به `/projects/{projectId}`؛
- مسیر Project Command Center واقعی با project id انتخاب‌شده و cache مستقل برای هر پروژه؛
- Architecture guard برای Permission، ممنوعیت Composite Health و الزام aggregation براساس currency؛
- تست‌های Domain برای empty/no-data/currency isolation و تست‌های Web برای قرارداد API، sort و فیلتر.

## Verification evidence

- .NET solution build: ۰ warning و ۰ error؛
- Domain tests: ۵۷/۵۷ passed؛
- Frontend unit/API contract tests: ۳۳/۳۳ passed؛
- Persian UI audit: passed؛
- ESLint و TypeScript: passed؛
- Next.js production build: passed و routeهای `/portfolio` و `/projects/[projectId]` تولید شدند؛
- Next.js standalone smoke: صفحه اصلی، Portfolio، جزئیات پروژه، Manifest و Service Worker همگی HTTP 200؛
- Repository architecture validation و `git diff --check`: passed.

## Boundaries and open gates

- migration و queryهای واقعی PostgreSQL در این محیط اجرا نشده‌اند، چون Docker/PostgreSQL runtime نصب نیست؛ این مورد Gate تست Integration است.
- Portfolio V1 یک Read Model آنلاین و on-demand است؛ Snapshot تاریخی Portfolio و استفاده آفلاین Staff خارج از این Slice است.
- نرخ تبدیل ارز، Trend سبد، Export، Saved View و مقایسه Baseline زمانی هنوز پیاده نشده‌اند.
- Approval exception در این Slice فقط تعریف فعلی Commercial State را پوشش می‌دهد.
- نام مدیر از Membership فعال با role code `ProjectManager` خوانده می‌شود؛ Governance role پیشرفته‌تر در Slice Identity آینده تکمیل می‌شود.

## Next checkpoint

Vertical Slice «Project Intelligence Advisory V1»: تولید تحلیل مدیریتی cited از Fact/Metric/State ساختاریافته، کنترل Permission در retrieval، ثبت prompt/model/version، جلوگیری از تغییر حقیقت رسمی توسط AI و evaluation برای unsupported claim و permission leakage.
