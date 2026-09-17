#!/usr/bin/env node

import { randomUUID } from "node:crypto";

const keycloakUrl = readEnvironment("PMCS_IDENTITY_TEST_URL", "http://localhost:8081").replace(/\/$/u, "");
const realm = readEnvironment("PMCS_IDENTITY_TEST_REALM", "pmcs");
const clientId = readEnvironment("PMCS_IDENTITY_TEST_CLIENT_ID", "pmcs-identity-admin");
const clientSecret = requiredEnvironment("PMCS_IDENTITY_TEST_CLIENT_SECRET");
const realmUrl = `${keycloakUrl}/realms/${encodeURIComponent(realm)}`;

const discoveryResponse = await fetch(`${realmUrl}/.well-known/openid-configuration`);
await requireSuccess(discoveryResponse, "OIDC discovery");
const discovery = await discoveryResponse.json();
if (discovery.issuer !== realmUrl) throw new Error(`Unexpected OIDC issuer: ${discovery.issuer}`);
for (const field of ["authorization_endpoint", "token_endpoint", "jwks_uri"]) {
  if (typeof discovery[field] !== "string" || !discovery[field]) throw new Error(`OIDC discovery is missing ${field}.`);
}

const tokenResponse = await fetch(`${realmUrl}/protocol/openid-connect/token`, {
  method: "POST",
  headers: { "content-type": "application/x-www-form-urlencoded" },
  body: new URLSearchParams({ grant_type: "client_credentials", client_id: clientId, client_secret: clientSecret }),
});
await requireSuccess(tokenResponse, "client credentials");
const { access_token: accessToken } = await tokenResponse.json();
if (typeof accessToken !== "string" || !accessToken) throw new Error("Identity administration token is missing.");

const adminUsersUrl = `${keycloakUrl}/admin/realms/${encodeURIComponent(realm)}/users`;
const authorization = { authorization: `Bearer ${accessToken}` };
const usersResponse = await fetch(`${adminUsersUrl}?max=1`, { headers: authorization });
await requireSuccess(usersResponse, "administration users probe");

const unique = randomUUID();
const email = `ci-${unique}@pmcs.invalid`;
const createResponse = await fetch(adminUsersUrl, {
  method: "POST",
  headers: { ...authorization, "content-type": "application/json" },
  body: JSON.stringify({ username: email, email, enabled: false }),
});
if (createResponse.status !== 201) await requireSuccess(createResponse, "administration user creation");
const location = createResponse.headers.get("location");
const createdUserId = location?.split("/").filter(Boolean).at(-1);
if (!createdUserId) throw new Error("Identity administration did not return a user Location header.");

try {
  const deleteResponse = await fetch(`${adminUsersUrl}/${encodeURIComponent(createdUserId)}`, {
    method: "DELETE",
    headers: authorization,
  });
  if (deleteResponse.status !== 204) await requireSuccess(deleteResponse, "administration user deletion");
} catch (error) {
  throw new Error(`The isolated identity fixture could not be removed: ${error instanceof Error ? error.message : String(error)}`);
}

console.log(JSON.stringify({
  status: "passed",
  stage: "identity-container-verification",
  realm,
  discovery: true,
  administrationRoundTrip: true,
}));

async function requireSuccess(response, operation) {
  if (response.ok) return;
  const body = await response.text();
  throw new Error(`${operation} returned HTTP ${response.status}: ${body.slice(0, 500)}`);
}

function requiredEnvironment(name) {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is required.`);
  return value;
}

function readEnvironment(name, fallback) {
  return process.env[name]?.trim() || fallback;
}
