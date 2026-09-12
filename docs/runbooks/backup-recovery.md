# Backup and recovery runbook

## هدف و مسئولیت

Backup فقط زمانی معتبر است که checksum، نگهداری خارج از محیط اصلی و Restore Drill موفق داشته باشد. مالک اجرای برنامه، DBA/Platform Owner است و نتیجه هر Drill باید با زمان شروع/پایان، نسخه Backup و نتیجه اعتبارسنجی ثبت شود.

هدف اولیه V1 که باید پیش از Pilot با کسب‌وکار تأیید شود:

- PostgreSQL RPO حداکثر ۱۵ دقیقه با Continuous WAL/PITR؛
- Object Storage RPO حداکثر ۱۵ دقیقه با Versioning و Replication؛
- RTO کل سرویس حداکثر ۴ ساعت؛
- Restore Drill حداقل فصلی و پیش از هر تغییر بزرگ زیرساخت.

اسکریپت Repository یک Base Backup منطقی می‌سازد؛ رسیدن به RPO پانزده‌دقیقه‌ای علاوه بر آن به WAL archiving یا سرویس Managed PostgreSQL با PITR نیاز دارد.

این سیاست برای سه پایگاه جدا اجرا می‌شود: داده اصلی PMCS، داده هویتی Keycloak و پایگاه نشست BFF. Keycloak شامل حساب، عامل دوم و Recovery state است و بازیابی آن باید هم‌زمان با Secretها و Encryption keyهای مربوط انجام شود. پایگاه BFF شامل Token رمز‌شده است؛ در رخداد امنیتی ترجیح پیش‌فرض، بازیابی‌نکردن Sessionهای قدیمی و اجبار ورود مجدد همه کاربران است.

## PostgreSQL base backup

هر دو متغیر باید صریح باشند و مسیر Backup باید absolute و غیر root باشد:

```bash
export PMCS_BACKUP_CONNECTION_STRING="<read-only-backup-connection>"
export PMCS_BACKUP_DIRECTORY="/srv/pmcs-backups/postgres"
./ops/backup/postgres-backup.sh
```

خروجی custom-format با `--no-owner --no-privileges`، checksum SHA-256 و permission فایل `0600` ساخته می‌شود. خروجی و checksum را به مقصد immutable/secondary منتقل و retention را در زیرساخت اعمال کنید. Connection string را در job log چاپ نکنید.

برای Keycloak و BFF همین Script را با Connection string و مقصد جدا اجرا کنید؛ نام فایل/مسیر و دسترسی هر پایگاه نباید با دیگری مشترک باشد. Backup پایگاه BFF به‌دلیل داشتن Refresh Token رمز‌شده، همان سطح محرمانگی Secret Store را نیاز دارد.

## Restore drill ایزوله

Restore فقط روی نام جدید با الگوی `pmcs_restore_drill_[a-z0-9_]+` مجاز است. Script دیتابیس موجود را حذف یا overwrite نمی‌کند و پس از آزمون نیز آن را برای بررسی نگه می‌دارد.

```bash
export PMCS_RESTORE_ADMIN_CONNECTION_STRING="<maintenance-database-connection>"
export PMCS_RESTORE_TARGET_DATABASE="pmcs_restore_drill_2026q3"
export PMCS_RESTORE_TARGET_CONNECTION_STRING="<connection-to-pmcs_restore_drill_2026q3>"
export PMCS_RESTORE_BACKUP_FILE="/srv/pmcs-backups/postgres/pmcs-....dump"
./ops/backup/postgres-restore-drill.sh
```

پس از موفقیت Script:

1. شمار migrationها و وجود جداول Foundation/Projects را ثبت کنید؛
2. با credential فقط‌خواندنی، شمار پروژه‌ها، آخرین Audit، Snapshot و Evidence metadata را با منبع مقایسه کنید؛
3. چند فایل Evidence را از نسخه بازیابی‌شده Object Storage با SHA-256 دیتابیس تطبیق دهید؛
4. RPO/RTO واقعی را ثبت و انحراف را Incident کنید؛
5. حذف دیتابیس Drill فقط پس از sign-off و با رویه DBA خارج از این Script انجام شود.

برای Drill هویت، Realm، User ID، attribute سازمان، Credential metadata و عامل دوم را بررسی کنید؛ رمز یا Token را در گزارش Drill ثبت نکنید. Sessionهای BFF بازیابی‌شده تا زمانی که تصمیم صریح Incident Commander برای حفظ آن‌ها وجود ندارد باید revoke شوند.

## Object Storage

Bucket Production از قبل و private ساخته می‌شود؛ API اجازه ساخت خودکار آن را ندارد. در Provider باید Versioning، encryption at rest، lifecycle/retention، replication به failure domain دوم و audit access فعال باشد. Public ACL ممنوع است. Backup دیتابیس بدون Object Storage هم‌نسخه، Evidence کامل را بازیابی نمی‌کند؛ شناسه نسخه/نقطه بازیابی هر دو باید در پرونده Drill ثبت شود.

## ترتیب بازیابی Incident

1. write traffic را متوقف و زمان recovery point را تعیین کنید؛
2. PostgreSQL را در target جدید تا recovery point بازیابی کنید؛
3. Object Storage را به نقطه سازگار یا نزدیک‌تر بازیابی کنید؛
4. Migration drift، Tenant isolation، Audit/Outbox و sample SHA-256 را بررسی کنید؛
5. Keycloak را با hostname/issuer قطعی بالا بیاورید و discovery/JWKS را کنترل کنید؛
6. پایگاه BFF را بازیابی یا خالی راه‌اندازی کنید؛ حالت خالی همه کاربران را به ورود مجدد وادار می‌کند؛
7. ابتدا API را بدون Worker بالا بیاورید و `/health/ready` را کنترل کنید؛
8. Web/BFF را بالا بیاورید و Login، Refresh، Logout و حساب Suspended را Smoke کنید؛
9. Workerها را فعال، backlog و retryها را پایش و سپس traffic را باز کنید؛
10. هیچ Insight AI را به‌عنوان حقیقت بازیابی‌شده یا جایگزین Fact استفاده نکنید.
