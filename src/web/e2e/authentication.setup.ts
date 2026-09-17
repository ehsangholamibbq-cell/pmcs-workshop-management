import { test as setup, expect } from "@playwright/test";
import { authenticationState } from "../playwright.config";
import { loginThroughOidc, tenantId, userId } from "./support";

setup("OIDC login establishes the scoped BFF session", async ({ page }) => {
  await loginThroughOidc(page);

  const sessionResponse = await page.request.get("/api/pmcs/api/v1/session");
  expect(sessionResponse.status()).toBe(200);
  expect(sessionResponse.headers()["cache-control"]).toMatch(/no-store/u);
  const session = await sessionResponse.json() as {
    tenantId: string;
    userId: string;
    authentication: string;
  };
  expect(session).toMatchObject({
    tenantId,
    userId,
    authentication: "oidc-access-token",
  });

  await page.context().storageState({ path: authenticationState, indexedDB: true });
});
