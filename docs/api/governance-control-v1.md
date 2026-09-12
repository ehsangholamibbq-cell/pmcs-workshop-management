# Governance Control API V1

Base path:

```text
/api/v1/projects/{projectId}/governance
```

همه Commandها به Actor معتبر، Permission پروژه و `Idempotency-Key` نیاز دارند. همه تغییر وضعیت‌ها `baseRevision` می‌خواهند و در تعارض بدون بازنویسی پاسخ 409 می‌دهند. رکورد غیرعمومی بدون مجوز حساس حتی با شناسه مستقیم بازگردانده نمی‌شود.

## State and configuration

| Method | Path | Permission | قاعده |
| --- | --- | --- | --- |
| GET | `/` | `governance.read` | وضعیت، شمارنده‌ها، مهلت‌ها، افراد فعال قابل تخصیص و فقط رکوردهای قابل مشاهده |
| POST | `/settings/risk-matrices` | `governance.configure` | نسخه جدید ماتریس پنج‌درپنج؛ سوابق قبلی ثابت می‌مانند |
| POST | `/settings/sla-rules` | `governance.configure` | نسخه جدید قاعده توافق سطح خدمت برای نوع رکورد و شدت اختیاری |

`setupState` تا فعال‌شدن حداقل یک ماتریس و یک قاعده برای هرکدام از Issue، Risk و Decision Request برابر `SetupRequired` است. شدت فقط برای قاعده Issue معتبر است؛ API نوع پشتیبانی‌نشده نمی‌پذیرد. این وضعیت هیچ نتیجه‌ای درباره سلامت پروژه نمی‌دهد. `outlook.state` در نبود مهلت قابل محاسبه `InsufficientData` است. `assignablePeople` افراد فعال همان پروژه و قابلیت `canDecide` را برای انتخاب صریح مالک و مرجع تصمیم برمی‌گرداند.

## Issues

| Method | Path | Permission | اثر |
| --- | --- | --- | --- |
| POST | `/issues` | `issues.create` | مسئله رسمی با واقعیت، مالک، هدف و Evidence |
| POST | `/issues/{id}/transition` | `issues.manage` | حرکت محدود ماشین حالت |

بستن علاوه بر `issues.manage` به `issues.verify-close` و Evidence مستقل نیاز دارد. کاربری که `Resolved` را ثبت کرده است اجازه ثبت `Closed` همان چرخه را ندارد؛ پاسخ API نیز `resolvedBy/resolvedAt` و `closedBy/closedAt` را برای ممیزی برمی‌گرداند. مسیر معمول `Open → UnderAssessment → ResponseInProgress → Resolved → Closed` است. رفع و بستن یک عملیات نیستند.

## Risks

| Method | Path | Permission | اثر |
| --- | --- | --- | --- |
| POST | `/risks` | `risks.create` | ثبت ریسک پیشنهادی بدون امتیاز |
| POST | `/risks/{id}/assess` | `risks.assess` | ارزیابی ذاتی با ماتریس نسخه‌دار و پاسخ انسانی |
| POST | `/risks/{id}/activate` | `risks.manage` | فعال‌سازی ریسک ارزیابی‌شده |
| POST | `/risks/{id}/review` | `risks.review` | ثبت امتیاز باقیمانده و تاریخ بازبینی بعدی با همان ماتریس pinned |
| POST | `/risks/{id}/materialize` | `risks.manage` + `issues.create` | ساخت اتمیک Issue پیوندخورده؛ Risk بازنویسی نمی‌شود |
| POST | `/risks/{id}/close` | `risks.manage` | بستن/انقضا با دلیل و Evidence |
| POST | `/risks/{id}/reopen` | `risks.manage` | بازگشایی با دلیل |

فرمول نسخه اول `probability-x-maximum-impact-v1` است: بیشترین اثر میان زمان، هزینه، کیفیت، ایمنی، قرارداد و عملیات در احتمال ضرب می‌شود. آستانه‌ها متعلق به نسخه ماتریس پروژه‌اند. نبود بودجه مانع ارزیابی کیفی هزینه نیست، اما هیچ مبلغ یا درصد هزینه‌ای اختراع نمی‌شود.

## Decision requests and decisions

| Method | Path | Permission | اثر |
| --- | --- | --- | --- |
| POST | `/decision-requests` | `decision-requests.create` | پیش‌نویس دارای Facts/Assumptions/Predictions/Options جدا |
| POST | `/decision-requests/{id}/submit` | `decision-requests.submit` | ارسال با حداقل دو گزینه |
| POST | `/decision-requests/{id}/begin` | `decisions.decide` + مرجع صریح | آغاز بررسی رسمی |
| POST | `/decision-requests/{id}/request-information` | `decisions.decide` + مرجع صریح | بازگشت مستدل برای اطلاعات بیشتر |
| POST | `/decision-requests/{id}/decisions` | `decisions.decide` + مرجع صریح | ثبت Decision immutable از گزینه رسمی |
| POST | `/decision-requests/{id}/implement` | `decisions.implement` | آغاز صریح اجرای تصمیم |
| POST | `/decisions/{id}/supersede` | `decisions.decide` + مرجع صریح | تصمیم جایگزین و زنجیره نسخه‌ها |
| POST | `/decisions/{id}/effect-review` | `decisions.review-effect` | بازبینی اثر با Evidence؛ فقط تصمیم جاری |

مرجع درخواست باید عضو فعال همان پروژه و دارای `decisions.decide` باشد. `VerbalRecordedLater` فقط کانال ثبت است و زمان واقعی تصمیم را جدا از زمان ثبت نگه می‌دارد. رابط رسمی این جریان را آفلاین صف نمی‌کند.

## Deadlines and escalation

| Method | Path | Permission | اثر |
| --- | --- | --- | --- |
| POST | `/alerts/evaluate` | `governance.escalate` | ارزیابی قطعی مهلت‌ها و ایجاد/تازه‌سازی رشته deduplicated |
| POST | `/escalations/{id}/acknowledge` | `escalations.acknowledge` | ثبت دریافت؛ بدون تغییر منبع |

قاعده مهلت به نسخه‌ای که هنگام ایجاد رکورد قابل اعمال بوده متصل می‌شود. `ElapsedHours` از زمان ایجاد و `ProjectWorkingDays` فقط از الگوی هفته کاری و منطقه زمانی پروژه محاسبه می‌شود. Reminder نزدیک سررسید رشته Escalation ایجاد نمی‌کند؛ اعلام هشدار فقط پس از overdue/escalation threshold یا ریسک بحرانی رخ می‌دهد. ارزیابی‌های تکراری در یک ساعت Notification تازه نمی‌سازند و همان Thread را نگه می‌دارند.

گیرنده از نسخه قاعده pinned انتخاب می‌شود؛ اگر گیرنده قاعده در دسترس نباشد، مدیر پروژه و سپس مالک Issue/Risk یا Authority درخواست تصمیم مسیر جایگزین هستند. این fallback فقط مسیریابی هشدار است و مالک یا Authority منبع را تغییر نمی‌دهد.

## Current boundaries

- WBS، Budget، Quality و HSE پیش‌شرط دفتر عمومی ریسک و تصمیم نیستند.
- اطلاعات محرمانه HSE و حساس تجاری فقط با Permission صریح قابل خواندن/نوشتن‌اند.
- چشم‌انداز فعلی deterministic است؛ مدل آماری یا Monte Carlo در V1 اجرا نمی‌شود.
- فایل Evidence در ماژول Evidence خصوصی می‌ماند و این API فقط reference قابل ردیابی ثبت می‌کند.
- AI هیچ Command این API را فراخوانی یا به‌عنوان تصمیم انسانی اجرا نمی‌کند.
