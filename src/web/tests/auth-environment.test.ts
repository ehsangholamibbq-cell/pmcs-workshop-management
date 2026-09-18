import assert from "node:assert/strict";
import test from "node:test";
import { readAuthEnvironment } from "../lib/auth-environment.ts";

const valid = {
  NODE_ENV: "production",
  PMCS_WEB_URL: "https://pmcs.example.com",
  PMCS_AUTH_SECRET: "a-production-secret-with-more-than-32-characters",
  PMCS_WEB_AUTH_DATABASE_URL: "postgresql://user:secret@database:5432/web_auth?sslmode=verify-full",
  PMCS_INTERNAL_API_URL: "https://api.internal.example.com",
  PMCS_OIDC_PUBLIC_ISSUER: "https://identity.example.com/realms/pmcs",
  PMCS_OIDC_INTERNAL_ISSUER: "https://identity.internal.example.com/realms/pmcs",
  PMCS_WEB_OIDC_CLIENT_ID: "pmcs-web",
  PMCS_WEB_OIDC_CLIENT_SECRET: "production-client-secret-at-least-24",
  PMCS_LOGIN_TENANT_ID: "11111111-1111-1111-1111-111111111111",
} satisfies NodeJS.ProcessEnv;

test("production authentication environment accepts explicit HTTPS boundaries", () => {
  const result = readAuthEnvironment(valid);

  assert.equal(result.secureCookies, true);
  assert.equal(result.clientId, "pmcs-web");
  assert.equal(result.publicIssuer, "https://identity.example.com/realms/pmcs");
  assert.equal(result.loginTenantId, "11111111-1111-1111-1111-111111111111");
});

test("login presentation tenant must be an explicit UUID", () => {
  assert.throws(
    () => readAuthEnvironment({ ...valid, PMCS_LOGIN_TENANT_ID: "current-tenant" }),
    /UUID/u,
  );
});

test("insecure identity endpoints require an explicit development switch", () => {
  assert.throws(
    () => readAuthEnvironment({ ...valid, PMCS_WEB_URL: "http://localhost:3000" }),
    /HTTPS/u,
  );
});

test("authentication secret shorter than 32 characters is rejected", () => {
  assert.throws(
    () => readAuthEnvironment({ ...valid, PMCS_AUTH_SECRET: "too-short" }),
    /۳۲/u,
  );
});

test("development HTTP switch cannot be used with a public host", () => {
  assert.throws(
    () => readAuthEnvironment({
      ...valid,
      PMCS_AUTH_ALLOW_INSECURE_HTTP: "true",
      PMCS_WEB_URL: "http://pmcs.example.com",
      PMCS_OIDC_PUBLIC_ISSUER: "http://identity.example.com/realms/pmcs",
      PMCS_OIDC_INTERNAL_ISSUER: "http://keycloak:8080/realms/pmcs",
      PMCS_INTERNAL_API_URL: "http://api:8080",
    }),
    /محلی/u,
  );
});
