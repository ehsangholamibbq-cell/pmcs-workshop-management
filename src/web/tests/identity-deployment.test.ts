import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const repositoryRoot = new URL("../../../", import.meta.url);

test("production identity image does not embed the development realm", async () => {
  const dockerfile = await readFile(new URL("deploy/keycloak/Dockerfile", repositoryRoot), "utf8");
  const compose = await readFile(new URL("docker-compose.yml", repositoryRoot), "utf8");

  assert.doesNotMatch(dockerfile, /pmcs-realm\.json/u);
  assert.doesNotMatch(dockerfile, /--import-realm/u);
  assert.match(compose, /pmcs-realm\.json:\/opt\/keycloak\/data\/import\/pmcs-realm\.json:ro/u);
  assert.match(compose, /--import-realm/u);
});

test("development realm keeps registration closed and strong login boundaries", async () => {
  const json = await readFile(new URL("deploy/keycloak/pmcs-realm.json", repositoryRoot), "utf8");
  const realm = JSON.parse(json) as {
    registrationAllowed: boolean;
    bruteForceProtected: boolean;
    accessTokenLifespan: number;
    clients: Array<{ clientId: string; redirectUris?: string[]; attributes?: Record<string, string> }>;
    users: Array<{ serviceAccountClientId?: string; requiredActions?: string[] }>;
  };
  const webClient = realm.clients.find((client) => client.clientId === "pmcs-web");

  assert.equal(realm.registrationAllowed, false);
  assert.equal(realm.bruteForceProtected, true);
  assert.equal(realm.accessTokenLifespan, 300);
  assert.deepEqual(webClient?.redirectUris, [
    "http://localhost:3000/api/auth/callback/keycloak",
    "http://localhost:3000",
  ]);
  assert.equal(webClient?.attributes?.["pkce.code.challenge.method"], "S256");
  assert.ok(realm.users.filter((user) => !user.serviceAccountClientId).every((user) => user.requiredActions?.includes("CONFIGURE_TOTP")));
});

test("identity administration client uses a service account with limited user-management roles", async () => {
  const json = await readFile(new URL("deploy/keycloak/pmcs-realm.json", repositoryRoot), "utf8");
  const realm = JSON.parse(json) as {
    clients: Array<{ clientId: string; serviceAccountsEnabled?: boolean; standardFlowEnabled?: boolean }>;
    users: Array<{ serviceAccountClientId?: string; clientRoles?: Record<string, string[]> }>;
  };
  const client = realm.clients.find((item) => item.clientId === "pmcs-identity-admin");
  const account = realm.users.find((item) => item.serviceAccountClientId === "pmcs-identity-admin");

  assert.equal(client?.serviceAccountsEnabled, true);
  assert.equal(client?.standardFlowEnabled, false);
  assert.deepEqual(account?.clientRoles?.["realm-management"], ["manage-users", "view-users"]);
});

test("identity administration page is behind the BFF session boundary", async () => {
  const proxy = await readFile(new URL("src/web/proxy.ts", repositoryRoot), "utf8");
  assert.match(proxy, /"\/admin\/users"/u);
});
