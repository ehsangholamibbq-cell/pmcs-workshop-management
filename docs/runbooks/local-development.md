# Local development runbook

## Start

```bash
cp .env.example .env
docker compose up --build
```

برنامه وب روی `http://localhost:3000` و صفحه ورود Keycloak روی `http://localhost:8081` است. کنسول مدیریتی Keycloak در `http://localhost:8081/admin` با credentialهای Development فایل `.env` در دسترس است.

User نمونه `demo@pmcs.local` با رمز `PMCS_DEMO_USER_PASSWORD` فقط برای Development ساخته می‌شود. ورود نخست تغییر رمز و فعال‌سازی رمز یک‌بارمصرف (OTP) را الزام می‌کند. Secret یا رمز واقعی را در Repository قرار ندهید.

## Reset development data

این عملیات مخرب است و فقط برای داده محلی Development اجرا می‌شود:

```bash
docker compose down
docker volume rm pmcs_pmcs-postgres pmcs_pmcs-minio pmcs_pmcs-keycloak-postgres pmcs_pmcs-web-auth-postgres
docker compose up --build
```

حذف Volumeها تمام داده محلی برنامه، هویت، نشست و فایل‌های نمونه را غیرقابل‌بازیابی پاک می‌کند.

## Health checks

```bash
curl http://localhost:8080/health
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
curl http://localhost:8080/api/v1/foundation
curl http://localhost:8081/realms/pmcs/.well-known/openid-configuration
```

MinIO console روی `http://localhost:9001` و S3 API روی `http://localhost:9000` در دسترس است. Bucket خصوصی `pmcs-dev` در اولین Upload توسط Adapter ساخته می‌شود.

## Evidence smoke test

ابتدا یک Daily Report/Fact را Sync کنید. سپس از Field PWA تصویر یا PDF حداکثر ۲۵ MiB انتخاب کنید. Operation queue باید پیش از attachment queue خالی شود و metadata فایل پس از تأیید Object Storage به `Uploaded` برسد. Bucket را public نکنید؛ دریافت فایل فقط از endpoint مجوزدار API انجام می‌شود.

## Advisory intelligence configuration

بدون secret و نام مدل، API و Worker سالم اجرا می‌شوند اما قابلیت تولید تحلیل در UI غیرفعال است و هیچ پاسخ ساختگی ساخته نمی‌شود. برای محیط Integration این مقادیر را از secret store تزریق کنید:

```bash
export OPENAI_API_KEY="<integration-secret>"
export OPENAI_MODEL="<approved-model-id>"
```

پیش از live smoke، Project State رسمی و به‌روز بسازید. سپس یک درخواست از UI ثبت و وضعیت `Pending → Processing → Succeeded`، citationها، Review و Audit/Outbox را بررسی کنید. کلید یا payload Context/Output را در Log چاپ نکنید. live test هزینه و ارسال داده به Provider دارد و فقط در محیط تأییدشده اجرا شود.

## Migration failure

API در صورت شکست migration نباید آماده (`ready`) شود. Log خطای migration را بررسی کنید و migration applied را دستی حذف یا ویرایش نکنید.

## خطای ورود

- issuer در Token باید دقیقاً `http://localhost:8081/realms/pmcs` باشد.
- API metadata را داخل شبکه از `keycloak:8080` می‌گیرد؛ این جداسازی فقط Development است.
- Client secret در Keycloak و Web باید یکسان باشد.
- User باید هم در Keycloak با `sub/tenant_id` صحیح و هم در IdentityAccess با وضعیت Active وجود داشته باشد.
- پس از تغییر فایل Realm، Volume Keycloak موجود خودکار overwrite نمی‌شود؛ فقط برای داده Development از Reset بالا استفاده کنید.
