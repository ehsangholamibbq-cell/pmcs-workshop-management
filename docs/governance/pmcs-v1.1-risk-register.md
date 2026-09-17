# PMCS V1.1 — Risk Register

- شناسه: `PMCS-GOV-V1.1-RISK-001`
- نسخه: `1.0.0`
- وضعیت: Active از G0 تا Baseline Lock

| ID | ریسک | احتمال/اثر | کنترل الزامی | مالک Gate | Evidence خروج |
| --- | --- | --- | --- | --- | --- |
| `R-GOV-01` | توسعه از Snapshot یا Commit اشتباه | متوسط/بحرانی | SHA دقیق Parent و Repository Start، PR با expected head | Architecture | Baseline manifest و compare evidence |
| `R-GOV-02` | تزریق Feature جدید به V1 Locked | کم/بحرانی | شاخه مستقل V1.1، no-runtime guard، Full Regression | Release | Git ancestry و runtime diff |
| `R-GOV-03` | Main محافظت‌نشده و Push ناخواسته | متوسط/زیاد | منع direct push در Policy، PR-only V1.1، SHA check | Release | PR history و merge evidence |
| `R-ARCH-01` | Coupling مستقیم Persistence ماژول‌ها | متوسط/بحرانی | Manifest، Application Contract و architecture test | Architecture | dependency guard |
| `R-SEC-01` | کد یا Asset ناسالم در Login | متوسط/بحرانی | Schema بسته، CSP، validation/quarantine و rollback | Security | negative upload و CSP tests |
| `R-SEC-02` | نشت تصویر یا مشخصات عضو | متوسط/زیاد | private object storage، field permission و tenant isolation | Security | privacy matrix |
| `R-PRJ-01` | انتقال Role باعث privilege escalation شود | متوسط/بحرانی | Preview، re-evaluation در مقصد و Permission مستقل | Projects/Security | cross-role negative tests |
| `R-PRJ-02` | دادهٔ عملیاتی در Duplicate پروژه کپی شود | متوسط/بحرانی | allowlist Contributor و non-copy contract | Projects/QA | source-target database assertions |
| `R-DOC-01` | Malware یا فایل جعلی منتشر شود | متوسط/بحرانی | signature، scanner adapter، quarantine و release state | Documents/Security | malicious fixture tests |
| `R-COL-01` | Chat منبع حقیقت رسمی تلقی شود | متوسط/زیاد | Convert command، confirmation و lineage | Collaboration | mutation boundary tests |
| `R-RPT-01` | گزارش زیبا ولی عدد نادرست باشد | متوسط/بحرانی | semantic model، as-of، hash و golden data | Reporting/QA | deterministic replay tests |
| `R-AI-01` | Agent Permission یا Business Rule را دور بزند | متوسط/بحرانی | Tool Registry، per-call evaluation و no-SQL tests | Intelligence/Security | negative tool suite |
| `R-UX-01` | Motion/Asset باعث افت سرعت یا خستگی شود | متوسط/متوسط | budget، lazy asset، non-blocking login و reduced-motion | UX/QA | performance/visual evidence |
| `R-OFF-01` | قابلیت جدید در Offline duplicate/conflict بسازد | متوسط/زیاد | stable client ID، idempotency و conflict state | Feature owner | reconnect/concurrency tests |
| `R-MIG-01` | Migration برگشت Runtime را مختل کند | کم/بحرانی | expand/migrate/verify/contract و restore drill | Data/Release | upgrade + rollback rehearsal |
| `R-SCOPE-01` | V1.1 به بستهٔ بزرگ غیرقابل‌بستن تبدیل شود | زیاد/زیاد | Checkpoint مستقل، Non-Scope و WIP limit | Product | roadmap review در هر Gate |

ریسک بدون Owner، Gate و Evidence قابل پذیرش نیست. تغییر احتمال/اثر یا پذیرش Risk باید در Checkpoint بعدی نسخه‌دار ثبت شود.

