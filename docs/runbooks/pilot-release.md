# Pilot release runbook

## 1. ساخت Candidate قابل‌اثبات

Build هر دو Artifact باید با سه مقدار یکسان انجام شود:

```bash
export PMCS_RELEASE_COMMIT="<full-lowercase-40-character-commit>"
export PMCS_RELEASE_VERSION="1.21.0-rc.1"
export PMCS_RELEASE_BUILT_AT="<ISO-8601-UTC>"
export PMCS_RELEASE_REQUIRED=true
docker compose build api web
```

Imageها با Digest immutable و Source bundle با SHA-256 ثبت می‌شوند. Tag مانند `latest` مدرک Release نیست.

## 2. Evidenceهای اجباری

فهرست مرجع در `release/pilot-gates.json` است:

1. Repository validation؛
2. Backend tests؛
3. Web check؛
4. PostgreSQL integration؛
5. Object Storage upload/download roundtrip؛
6. Backup/Restore drill؛
7. Environment smoke؛
8. Device UAT.

گزارش Web check باید `audit:calendar` را نیز پاس کند. Device UAT باید روی همه مرورگرها و دستگاه‌های هدف، ورود دستی و انتخاب Date Picker شمسی، نوروز، اسفند کبیسه، ارقام فارسی و نمایش زمان تهران را کنترل کند. نمایش تاریخ میلادی یا ISO خام در هر مسیر کاربر، Gate را مردود می‌کند.

هر گزارش باید Commit، زمان اجرا، زمان انقضا، URI پایدار و SHA-256 داشته باشد. گزارش یک Commit برای Commit دیگر قابل‌استفاده نیست. Backup/Restore محیط واقعی باید PostgreSQL و Object Storage هم‌نسخه را پوشش دهد؛ Roundtrip MinIO در CI جایگزین این Drill نیست.

## 3. تأیید مستقل

پس از آخرین Evidence، سه نفر متفاوت در نقش‌های `product`، `security` و `operations` همان Commit را تأیید می‌کنند. Candidate نهایی پس از تأییدها ساخته می‌شود و بیش از ۱۴ روز اعتبار ندارد.

```bash
node tools/validate-pilot-release.mjs --candidate /absolute/path/to/pilot-candidate.json
```

Schema ساختاری در `release/pilot-candidate.schema.json` و قواعد زمانی/بین‌فیلدی در Validator اعمال می‌شوند.

## 4. Deploy و Smoke

API و Web فقط با Digestهای ثبت‌شده Deploy می‌شوند. سپس:

```bash
export PMCS_CANDIDATE_FILE="/absolute/path/to/pilot-candidate.json"
export PMCS_API_URL="https://api.example.com"
export PMCS_WEB_URL="https://pmcs.example.com"
./tools/release-smoke.sh
```

Smoke باید Commit، نسخه و زمان Build هر دو Artifact را با Candidate تطبیق دهد. شکست هر تطبیق، Release را متوقف می‌کند.

## 5. Rollback

- Rollback فقط به Digestهای Candidate قبلی که هنوز معتبر و نگهداری‌شده است انجام می‌شود.
- Migration ناسازگار با Rollback نیازمند برنامه Forward-fix یا Restore Drill مصوب پیش از Deploy است.
- گزارش شکست، Correlation ID، Candidate و Digestهای واقعی حفظ می‌شوند؛ Tagها بازنویسی نمی‌شوند.
