# PMCS V1.1 — Development Baseline

- شناسه: `PMCS-GOV-V1.1-BASELINE-001`
- وضعیت: `Architecture Approved`
- تاریخ ثبت: ۱۴۰۵/۰۶/۲۷ (۲۰۲۶-۰۹-۱۸)
- شاخه توسعه: `v1.1-development`

## ۱. مراجع قطعی

| مرجع | مقدار | معنی |
| --- | --- | --- |
| Parent Product Baseline | `PMCS V1` | محصول Qualified/Final/Locked |
| Parent Runtime Commit | `26bf222d44634562ca7f3fc0931f3f8b79ca04a1` | آخرین Commit دارای Runtime قفل‌شده |
| Repository Start Commit | `0389b52cbd3385bdcc9f0e2a94411800389ae2fc` | Commit قفل و Evidence که شاخه V1.1 دقیقاً از آن ساخته می‌شود |
| Repository Start Tree | `c0f3f7dfc0295dbd02a20192ad8b56217db579db` | Tree شروع شاخه |
| Qualification Evidence | Run 69 / `35255343431` | Qualification هفت Suite روی Runtime Commit |
| Lock Evidence | Run 70 / `35256341709` | بازآزمایی سبز Commit قفل Repository |

`Repository Start Commit` یک Commit جلوتر از Runtime Parent است، اما فقط Release evidence، مستندات و Repository validation را تغییر داده و Runtime محصول را تغییر نداده است. بنابراین:

- مبنای Regression و Migration داده: `26bf222d...`؛
- مبنای شاخه و تاریخچه Repository: `0389b52c...`؛
- هیچ‌کدام با عبارت مبهم «آخرین main» جایگزین نمی‌شوند.

## ۲. وضعیت خط توسعه

| مورد | وضعیت |
| --- | --- |
| Product line | `PMCS V1.1` |
| SemVer target | `1.1.0` |
| State | `Development | UX1/EXT1/DOC1/IAM1 Closed | PRJ1 Active` |
| Product runtime implementation | در جریان روی شاخه V1.1؛ IAM1 تا source commit `86f9f4e` تأیید شده است |
| Database migration | ۴۰ Migration؛ Restore Drill در Run 88 پاس |
| V1 maintenance line | مستقل و بدون Feature جدید |
| Visual direction | `مدیریت ممتاز` — Approved |

Governance source commit `d4ac64ea818c7e48476b650b84dafe31bf1872a4` در Run 71 (`35275795712`) با هفت Suite و Qualification Report سبز تأیید شد. این Approval فقط G0 را می‌بندد و هیچ Runtime/Migration جدیدی را جزو Baseline قفل‌شده V1 نمی‌کند.

IAM1 با Candidate source commit `86f9f4efd086e4e67823a930fcb3ef1b7249b71f` و Run 88 (`35345558791`) در هر هشت Job تأیید شد. این Evidence فقط Checkpoint توسعه IAM1 را می‌بندد؛ PMCS V1.1 هنوز Feature Complete، Qualified، Final یا Locked نیست و مرحله فعال `V1.1-PRJ1` است.

## ۳. قرارداد شاخه و ادغام

- شاخه `v1.1-development` فقط از SHA دقیق بالا ایجاد می‌شود؛
- قابلیت‌ها در شاخه‌های کوتاه‌عمر از Integration Baseline همین خط ساخته می‌شوند؛
- هیچ Commit قابلیت V1.1 مستقیماً روی Locked Runtime یا Maintenance line ثبت نمی‌شود؛
- Integration فقط با PR، expected head SHA و CI یکسان انجام می‌شود؛
- Rebase/force-push روی Commitهای Evidence، Checkpoint یا Baseline ممنوع است؛
- Main تا زمان Qualification نسخه V1.1 مرجع V1 قفل‌شده باقی می‌ماند؛
- Merge نهایی V1.1 فقط بعد از `Qualified → Final → Baseline Locked` مجاز است.

## ۴. Gateهای قبل از Runtime

اولین تغییر Runtime فقط پس از تکمیل هم‌زمان موارد زیر مجاز است:

1. ثبت Scope و Non-Scope؛
2. پذیرش ADRهای 0027 و 0028؛
3. Permission Catalog اولیه؛
4. API/Event/Migration/Offline strategy؛
5. Risk Register و Rollback policy؛
6. Test/Qualification Contract؛
7. `VX-G2 Direction Approved`؛
8. Checkpoint `V1.1-G0 Governance Approved` با CI سبز.

Art Direction تصویب شده است، اما `VX-G1 Audit Complete` و `VX-G3 System Ready` هنوز برای مهاجرت UI باز هستند. بستن G0 اجازه آغاز Foundationهای غیرنمایشی را می‌دهد؛ مهاجرت رابط تولیدی بدون Gateهای Visual مربوط مجاز نیست.

## ۵. قاعده بازگشت

- Rollback خط توسعه با بازگشت Branch/Deployment به `0389b52c...` انجام می‌شود؛
- Rollback Runtime به Artifact متناظر `26bf222d...` متکی است؛
- Migration جدید باید forward-only و سازگار با rollback Runtime باشد؛
- هیچ rollback مجاز نیست دادهٔ جدید را حذف کند؛
- هر Candidate باید Upgrade از Locked Baseline و Restore Drill مستقل را اجرا کند.
