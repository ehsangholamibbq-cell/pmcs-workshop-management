export interface AuthEnvironment {
  readonly webUrl: string;
  readonly authSecret: string;
  readonly databaseUrl: string;
  readonly internalApiUrl: string;
  readonly publicIssuer: string;
  readonly internalIssuer: string;
  readonly clientId: string;
  readonly clientSecret: string;
  readonly loginTenantId: string;
  readonly secureCookies: boolean;
  readonly isBuild: boolean;
}

const buildPhase = "phase-production-build";

export function readAuthEnvironment(
  environment: NodeJS.ProcessEnv = process.env,
): AuthEnvironment {
  const isBuild = environment.NEXT_PHASE === buildPhase || environment.PMCS_BUILD_PHASE === "true";
  const allowInsecureHttp = environment.PMCS_AUTH_ALLOW_INSECURE_HTTP === "true";
  const webUrl = readUrl(environment, "PMCS_WEB_URL", isBuild, "http://localhost:3000");
  const publicIssuer = readUrl(
    environment,
    "PMCS_OIDC_PUBLIC_ISSUER",
    isBuild,
    "http://localhost:8081/realms/pmcs",
  );
  const internalIssuer = readUrl(
    environment,
    "PMCS_OIDC_INTERNAL_ISSUER",
    isBuild,
    "http://localhost:8081/realms/pmcs",
  );
  const internalApiUrl = readUrl(
    environment,
    "PMCS_INTERNAL_API_URL",
    isBuild,
    "http://localhost:8080",
  );

  if (!allowInsecureHttp && [webUrl, publicIssuer, internalIssuer, internalApiUrl]
    .some((value) => new URL(value).protocol !== "https:")) {
    throw new Error("نشانی‌های احراز هویت و سرویس داخلی باید در محیط عملیاتی از HTTPS استفاده کنند.");
  }
  if (allowInsecureHttp &&
      (!isLoopback(new URL(webUrl).hostname) ||
        !isLoopback(new URL(publicIssuer).hostname) ||
        !isDevelopmentHost(new URL(internalIssuer).hostname) ||
        !isDevelopmentHost(new URL(internalApiUrl).hostname))) {
    throw new Error("HTTP ناامن فقط برای محیط محلی و شبکه داخلی Compose مجاز است.");
  }

  const authSecret = required(
    environment,
    "PMCS_AUTH_SECRET",
    isBuild,
    "build-only-placeholder-secret-at-least-32-characters",
  );
  if (authSecret.length < 32) {
    throw new Error("PMCS_AUTH_SECRET باید حداقل ۳۲ نویسه داشته باشد.");
  }

  const databaseUrl = required(
    environment,
    "PMCS_WEB_AUTH_DATABASE_URL",
    isBuild,
    "postgresql://build:build@localhost:5432/build",
  );
  if (!allowInsecureHttp) {
    validateProductionDatabaseUrl(databaseUrl);
    rejectDevelopmentSecret(authSecret, "PMCS_AUTH_SECRET");
  }

  const clientSecret = required(
    environment,
    "PMCS_WEB_OIDC_CLIENT_SECRET",
    isBuild,
    "build-only-client-secret",
  );
  if (!allowInsecureHttp) {
    if (clientSecret.length < 24) {
      throw new Error("PMCS_WEB_OIDC_CLIENT_SECRET باید حداقل ۲۴ نویسه داشته باشد.");
    }
    rejectDevelopmentSecret(clientSecret, "PMCS_WEB_OIDC_CLIENT_SECRET");
  }

  const loginTenantId = required(
    environment,
    "PMCS_LOGIN_TENANT_ID",
    isBuild,
    "11111111-1111-1111-1111-111111111111",
  );
  if (!isUuid(loginTenantId)) {
    throw new Error("PMCS_LOGIN_TENANT_ID باید شناسه UUID معتبر سازمان باشد.");
  }

  return {
    webUrl,
    authSecret,
    databaseUrl,
    internalApiUrl,
    publicIssuer: publicIssuer.replace(/\/$/u, ""),
    internalIssuer: internalIssuer.replace(/\/$/u, ""),
    clientId: required(environment, "PMCS_WEB_OIDC_CLIENT_ID", isBuild, "pmcs-web"),
    clientSecret,
    loginTenantId: loginTenantId.toLowerCase(),
    secureCookies: new URL(webUrl).protocol === "https:",
    isBuild,
  };
}

function isUuid(value: string): boolean {
  return /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(value);
}

function validateProductionDatabaseUrl(value: string): void {
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error("PMCS_WEB_AUTH_DATABASE_URL باید یک رشته اتصال معتبر PostgreSQL باشد.");
  }
  const sslMode = url.searchParams.get("sslmode")?.toLowerCase();
  if ((url.protocol !== "postgresql:" && url.protocol !== "postgres:") ||
      !["require", "verify-ca", "verify-full"].includes(sslMode ?? "")) {
    throw new Error("پایگاه نشست عملیاتی باید PostgreSQL باشد و TLS را در sslmode الزامی کند.");
  }
}

function rejectDevelopmentSecret(value: string, key: string): void {
  if (["dev_only", "change_me", "build-only"].some((marker) => value.toLowerCase().includes(marker))) {
    throw new Error(`${key} نباید مقدار نمونه یا توسعه‌ای داشته باشد.`);
  }
}

function isLoopback(hostname: string): boolean {
  return hostname === "localhost" || hostname === "127.0.0.1" || hostname === "::1";
}

function isDevelopmentHost(hostname: string): boolean {
  return isLoopback(hostname) || (!hostname.includes(".") && !hostname.includes(":"));
}

function required(
  environment: NodeJS.ProcessEnv,
  key: string,
  isBuild: boolean,
  buildFallback: string,
): string {
  const value = environment[key]?.trim();
  if (value) return value;
  if (isBuild) return buildFallback;
  throw new Error(`${key} برای اجرای برنامه وب الزامی است.`);
}

function readUrl(
  environment: NodeJS.ProcessEnv,
  key: string,
  isBuild: boolean,
  buildFallback: string,
): string {
  const value = required(environment, key, isBuild, buildFallback);
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error(`${key} باید یک نشانی مطلق معتبر باشد.`);
  }

  if (url.protocol !== "http:" && url.protocol !== "https:") {
    throw new Error(`${key} فقط می‌تواند از قرارداد HTTP یا HTTPS استفاده کند.`);
  }
  return value.replace(/\/$/u, "");
}
