# قرارداد برنامه‌ریزی و پیشرفت نسخه ۱

این Slice یک دفتر اندازه‌گیری مستقل فراهم می‌کند و وجود WBS یا برنامه زمان‌بندی را پیش‌شرط ثبت پیشرفت نمی‌داند. همه مسیرها زیر `/api/v1/projects/{projectId}/planning` هستند.

## مسیرها

| روش | مسیر | مجوز | نتیجه |
| --- | --- | --- | --- |
| `GET` | `/measurement-items` | `planning.measurement-items.read` | فهرست قلم‌های فعال و غیرفعال پروژه |
| `POST` | `/measurement-items` | `planning.measurement-items.manage` | ساخت قلم مستقل با هدف اختیاری |
| `PUT` | `/measurement-items/{itemId}` | `planning.measurement-items.manage` | اصلاح عنوان، هدف و یادداشت با Revision |
| `POST` | `/measurement-items/{itemId}/deactivate` | `planning.measurement-items.manage` | توقف استفاده جدید بدون حذف تاریخچه |
| `GET` | `/progress` | `planning.progress.read` | دفتر تجمیعی پیشرفت موقت و تأییدشده |
| `GET` | `/baselines` | `planning.baselines.read` | تاریخچه نسخه‌های مبنای پیشرفت و برنامه |
| `POST/PUT` | `/baselines[/{baselineId}]` | `planning.baselines.capture` | ساخت یا اصلاح Draft/Returned با Revision |
| `POST` | `/baselines/{baselineId}/submit` | `planning.baselines.submit` | ارسال نسخه برای بررسی |
| `POST` | `/baselines/{baselineId}/approve` | `planning.baselines.review` | تصویب نسخه و Supersede نسخه مصوب قبلی |
| `POST` | `/baselines/{baselineId}/return` | `planning.baselines.review` | بازگشت نسخه همراه دلیل |
| `GET` | `/milestone-updates` | `planning.milestones.read` | تاریخچه وضعیت نقاط عطف |
| `POST/PUT` | `/milestone-updates[/{updateId}]` | `planning.milestones.capture` | ثبت یا اصلاح درصد دستی همراه مدرک |
| `POST` | `/milestone-updates/{updateId}/submit` | `planning.milestones.submit` | ارسال وضعیت نقطه عطف برای بررسی |
| `POST` | `/milestone-updates/{updateId}/approve` | `planning.milestones.review` | تأیید انسانی و ورود به محاسبه رسمی |
| `POST` | `/milestone-updates/{updateId}/return` | `planning.milestones.review` | بازگشت وضعیت همراه دلیل |

تغییر حالت برنامه‌ریزی در مالک Project و از مسیر `PUT /api/v1/projects/{projectId}/planning-mode` با `projects.configure` انجام می‌شود. تغییر حالت، نسخه مبنا یا Fact تاریخی را حذف یا بازنویسی نمی‌کند؛ اگر نسخه مصوب قبلی با حالت جدید سازگار نباشد، `ModeMismatch` برگردانده و از محاسبه کنار گذاشته می‌شود.

Commandها به `Idempotency-Key` نیاز دارند و Aggregate، Audit، Outbox و رسید Idempotency را در یک Transaction می‌نویسند. کد و واحد قلم پس از ایجاد تغییر نمی‌کنند تا Factهای تاریخی معنای خود را از دست ندهند؛ غیرفعال‌سازی نیز رکورد یا ارتباط قدیمی را حذف نمی‌کند.

## معنای داده

- `Submitted` با عنوان مقدار موقت و `Approved` با عنوان مقدار تأییدشده جمع می‌شود؛ این دو با هم ادغام نمی‌شوند.
- فقط Fact با Kind برابر `WorkProgress` وارد دفتر می‌شود.
- Fact بدون قلم اندازه‌گیری در شمارنده مستقل نگه داشته می‌شود و به‌زور تخصیص نمی‌یابد.
- `targetQuantity` اختیاری است. درصد همان قلم فقط وقتی هدف و حداقل یک مقدار تأییدشده وجود دارند محاسبه می‌شود.
- مجموع درصد کل فقط از نسخه مصوبی ساخته می‌شود که وزن همه ردیف‌های پیشرفت آن دقیقاً ۱۰۰٪ باشد؛ میانگین ساده درصد قلم‌ها مبنای رسمی نیست.
- اگر حتی یک ردیف وزن‌دار Actual رسمی قابل محاسبه نداشته باشد، `officialOverallPhysicalPercent = null` و وضعیت `IncompleteActualData` است؛ نبود Actual صفر فرض نمی‌شود.
- در `SimpleWorkList` نسخه `MeasurementWeights` فقط درصد کل را فعال می‌کند و شاخص زمان‌بندی همچنان `NotConfigured` است.
- در `Milestones/WbsBaseline/ExternalSchedule`، Planned V1 از تاریخ مصوب هر ردیف و تقویم کاری اختیاری پروژه ساخته می‌شود. Milestone در موعد ۰/۱۰۰ برنامه‌ای و Activity به‌صورت توزیع خطی شفاف محاسبه می‌شود.
- `scheduleVariancePercent = official - planned` فقط وقتی هر دو مقدار رسمی باشند. `forecastCompletionDate` تا افزوده‌شدن موتور Forecast معتبر همچنان `null` باقی می‌ماند.
- درصد دستی فقط برای Milestone مجاز است و بدون `evidenceReference`، Submit و Approval انسانی وارد محاسبه نمی‌شود.

## انواع مبنا

| حالت پروژه | نوع نسخه مجاز | شرط اصلی |
| --- | --- | --- |
| `None` | هیچ مبنای رسمی | Actual مستقل همچنان ثبت می‌شود |
| `SimpleWorkList` | `MeasurementWeights` | قلم مقدارمحور فعال، Target معلوم و جمع وزن ۱۰۰٪ |
| `Milestones` | `MilestonePlan` | تاریخ هر Milestone، وزن ۱۰۰٪ و درصد دستی بازبینی‌شده |
| `WbsBaseline` | `WbsBaseline` | حداقل یک Summary، Activity/Milestone وزن‌دار و سلسله‌مراتب بدون Cycle |
| `ExternalSchedule` | `ExternalSchedule` | Source System/Reference و External ID برای هر ردیف |

## مرز ماژول

Planning واقعیت‌های موقت/تأییدشده را فقط از قرارداد عمومی FieldOperations می‌خواند و به Persistence آن دسترسی ندارد. FieldOperations نیز قلم را از قرارداد `IMeasurementItemDirectory` اعتبارسنجی می‌کند و دیتابیس Planning را نمی‌شناسد. اتصال در سطح شناسه پایدار و بدون Foreign Key بین Schemaها نگه داشته می‌شود.
