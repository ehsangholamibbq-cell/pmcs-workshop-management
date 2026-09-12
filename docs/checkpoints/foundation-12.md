# Foundation checkpoint 12 — measurement and progress core

- Date: 2026-09-11
- Status: Implemented; live PostgreSQL migration and browser UAT remain environment gates
- Baseline: Checkpoint 11.1 (`8599117`)

## Scope delivered

- bounded context مستقل Planning با Measurement Item فعال/غیرفعال، کد و واحد immutable، هدف اختیاری و اصلاح Revision-controlled؛
- API فهرست/ساخت/اصلاح/غیرفعال‌سازی قلم و Read API دفتر پیشرفت؛
- اتصال اختیاری `measurementItemId` به Fact پیشرفت مستقیم و Payload آفلاین نسخه ۱؛
- اعتبارسنجی دوباره Tenant/Project، فعال‌بودن قلم و تطابق واحد در API و مسیر Sync؛
- منبع قراردادی FieldOperations برای خواندن Factهای `Submitted` و `Approved` بدون دسترسی Planning به Persistence ماژول دیگر؛
- نمایش جداگانه مقدار موقت و تأییدشده، نگهداری Factهای بدون اتصال و عدم حذف تاریخچه قلم غیرفعال؛
- رابط فارسی کاتالوگ/دفتر پیشرفت و انتخاب قلم در ثبت واقعیت آفلاین؛
- Permissionهای مستقل خواندن دفتر، خواندن قلم و مدیریت قلم.

## Semantic safeguards

- `PlanningMode.None` وضعیت معتبر است و مانع ثبت Actual نمی‌شود.
- هدف قلم اختیاری است و نبود آن صفر تلقی نمی‌شود.
- بدون مقدار تأییدشده، درصد قلم `null` است؛ مقدار موقت به درصد رسمی وارد نمی‌شود.
- تا نبود وزن‌دهی رسمی، `officialOverallPhysicalPercent = null` است.
- تا نبود خط مبنای رسمی، `scheduleVariancePercent` و `forecastCompletionDate` مقدار `null` دارند.
- WBS، بودجه اولیه، HSE و Quality همچنان مستقل و اختیاری‌اند.

## Verification

- Build ماژول Planning و Build کامل C# با Warning-as-error موفق؛
- ۱۰۴ تست C# شامل تفکیک موقت/تأییدشده، حالت بدون برنامه و نبود داده در برابر صفر موفق؛
- ۵۸ تست Frontend، ESLint، ممیزی فارسی و Build تولیدی Next.js موفق؛
- Architecture Guard روی ۲۲۰ فایل ماژول و `git diff --check` موفق؛
- Reference موقت Compile برای JWT فقط خارج Repository استفاده و PackageReference رسمی پیش از ثبت نسخه بازگردانده شد.

## Remaining environment gates

- اجرای Migrationهای `planning` و `field_operations` روی PostgreSQL واقعی؛
- آزمون End-to-End ساخت قلم، ثبت آفلاین، Sync، Submit/Approve و مشاهده دو ستون دفتر؛
- آزمون هم‌زمانی کد یکتا و Revision در PostgreSQL؛
- ادامه Planning V1 برای Milestone، WBS Baseline، External Schedule و وزن‌دهی رسمی، بدون تغییر معنای این Slice.
