# ADR 0022 — Release provenance and fail-closed Pilot gate

- Status: Accepted
- Date: 2026-09-12

## Context

سبز بودن Build به‌تنهایی ثابت نمی‌کند همان Web و API تأییدشده Deploy شده‌اند. Tag قابل‌تغییر، Evidence قدیمی، گزارش متعلق به Commit دیگر یا تأیید یک نفر در چند نقش می‌تواند Candidate ظاهراً معتبر ولی غیرقابل‌ممیزی بسازد. محیط واقعی و آزمون دستگاه نیز نباید با موفقیت تست‌های Repository یکی گرفته شوند.

## Decision

- Commit کامل، نسخه و زمان Build در خود Artifactهای API و Web قرار می‌گیرد.
- API هویت را از `/api/v1/release` و Web فایل ثابت Build‌شده را از `/release.json` منتشر می‌کند.
- Production API با Commit توسعه، Hash کوتاه، نسخه نامعتبر یا زمان غیر UTC شروع نمی‌شود.
- Candidate فقط با Hash مستقل `api-image`، `web-image` و `source-bundle` معتبر است و استقرار باید بر اساس Digest انجام شود، نه Tag قابل‌تغییر.
- هشت Gate سیاستی باید دقیقاً یک‌بار، با وضعیت `passed`، Commit یکسان، گزارش SHA-256دار و تاریخ انقضای معتبر ثبت شوند.
- سه تأیید Product، Security و Operations توسط سه هویت متفاوت و پس از تولید همه Evidenceها لازم است.
- Candidate، Evidence منقضی، فیلد ناشناخته، Gate مفقود یا Commit متفاوت را fail-closed رد می‌کند.
- Smoke محیط پس از Deploy، هویت Web و API را با Candidate یکسان تطبیق می‌دهد تا استقرار ترکیبی Frontend/Backend پذیرفته نشود.

## Consequences

- Repository می‌تواند قرارداد Release را تست کند، اما بدون Evidence واقعی PostgreSQL/Object Storage، محیط و دستگاه، Pilot آماده اعلام نمی‌شود.
- Rebuild همان Commit یک Artifact جدید است و Hash جدید نیاز دارد؛ تأیید قبلی به Artifact تازه منتقل نمی‌شود.
- Rollback فقط به Candidate قبلیِ معتبر و Artifactهای immutable آن انجام می‌شود.
