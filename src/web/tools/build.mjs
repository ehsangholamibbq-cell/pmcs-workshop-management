import { spawnSync } from "node:child_process";
import { writeReleaseIdentity } from "./release-identity.mjs";

const buildEnvironment = {
  ...process.env,
  PMCS_BUILD_PHASE: "true",
  PMCS_AUTH_ALLOW_INSECURE_HTTP: "true",
  PMCS_WEB_URL: process.env.PMCS_WEB_URL ?? "http://localhost:3000",
  PMCS_AUTH_SECRET:
    process.env.PMCS_AUTH_SECRET ?? "build-only-placeholder-secret-at-least-32-characters",
  PMCS_WEB_AUTH_DATABASE_URL:
    process.env.PMCS_WEB_AUTH_DATABASE_URL ?? "postgresql://build:build@localhost:5432/build",
  PMCS_INTERNAL_API_URL: process.env.PMCS_INTERNAL_API_URL ?? "http://localhost:8080",
  PMCS_OIDC_PUBLIC_ISSUER:
    process.env.PMCS_OIDC_PUBLIC_ISSUER ?? "http://localhost:8081/realms/pmcs",
  PMCS_OIDC_INTERNAL_ISSUER:
    process.env.PMCS_OIDC_INTERNAL_ISSUER ?? "http://localhost:8081/realms/pmcs",
  PMCS_WEB_OIDC_CLIENT_ID: process.env.PMCS_WEB_OIDC_CLIENT_ID ?? "pmcs-web",
  PMCS_WEB_OIDC_CLIENT_SECRET:
    process.env.PMCS_WEB_OIDC_CLIENT_SECRET ?? "build-only-client-secret",
};

writeReleaseIdentity(new URL("../public/release.json", import.meta.url), buildEnvironment);

const result = spawnSync(
  process.execPath,
  ["node_modules/next/dist/bin/next", "build"],
  { env: buildEnvironment, stdio: "inherit" },
);

process.exit(result.status ?? 1);
