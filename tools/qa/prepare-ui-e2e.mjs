#!/usr/bin/env node

const keycloakUrl = readEnvironment("PMCS_E2E_KEYCLOAK_URL", "http://localhost:8081").replace(/\/$/u, "");
const realm = readEnvironment("PMCS_E2E_REALM", "pmcs");
const clientId = readEnvironment("PMCS_E2E_ADMIN_CLIENT_ID", "pmcs-identity-admin");
const clientSecret = requiredEnvironment("PMCS_E2E_ADMIN_CLIENT_SECRET");
const userId = readEnvironment("PMCS_E2E_USER_ID", "22222222-2222-2222-2222-222222222222");
const userPassword = requiredEnvironment("PMCS_E2E_USER_PASSWORD");

const tokenResponse = await fetch(`${keycloakUrl}/realms/${encodeURIComponent(realm)}/protocol/openid-connect/token`, {
  method: "POST",
  headers: { "content-type": "application/x-www-form-urlencoded" },
  body: new URLSearchParams({
    grant_type: "client_credentials",
    client_id: clientId,
    client_secret: clientSecret,
  }),
});
await requireSuccess(tokenResponse, "client credentials");
const { access_token: accessToken } = await tokenResponse.json();
if (typeof accessToken !== "string" || !accessToken) {
  throw new Error("Keycloak did not return an administration access token.");
}

const userUrl = `${keycloakUrl}/admin/realms/${encodeURIComponent(realm)}/users/${encodeURIComponent(userId)}`;
const headers = { authorization: `Bearer ${accessToken}`, "content-type": "application/json" };
const userResponse = await fetch(userUrl, { headers });
await requireSuccess(userResponse, "read E2E user");
const user = await userResponse.json();

const updateResponse = await fetch(userUrl, {
  method: "PUT",
  headers,
  body: JSON.stringify({ ...user, enabled: true, emailVerified: true, requiredActions: [] }),
});
await requireSuccess(updateResponse, "clear isolated E2E required actions");

const passwordResponse = await fetch(`${userUrl}/reset-password`, {
  method: "PUT",
  headers,
  body: JSON.stringify({ type: "password", value: userPassword, temporary: false }),
});
await requireSuccess(passwordResponse, "set isolated E2E password");

const bruteForceResponse = await fetch(
  `${keycloakUrl}/admin/realms/${encodeURIComponent(realm)}/attack-detection/brute-force/users/${encodeURIComponent(userId)}`,
  { method: "DELETE", headers },
);
if (!bruteForceResponse.ok && bruteForceResponse.status !== 404) {
  await requireSuccess(bruteForceResponse, "clear isolated E2E brute-force state");
}

console.log(JSON.stringify({
  prepared: true,
  realm,
  userId,
  requiredActions: [],
  passwordMode: "permanent-isolated-fixture",
}));

async function requireSuccess(response, operation) {
  if (response.ok) return;
  const body = await response.text();
  throw new Error(`Keycloak ${operation} failed with HTTP ${response.status}: ${body.slice(0, 500)}`);
}

function requiredEnvironment(name) {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is required.`);
  return value;
}

function readEnvironment(name, fallback) {
  return process.env[name]?.trim() || fallback;
}
