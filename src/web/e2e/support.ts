import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export const tenantId = "11111111-1111-1111-1111-111111111111";
export const userId = "22222222-2222-2222-2222-222222222222";
export const projectId = "33333333-3333-3333-3333-333333333333";
export const projectPath = `/projects/${projectId}`;

export interface StoredOperation {
  readonly operationId: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly status: string;
  readonly description?: string;
}

export async function loginThroughOidc(page: Page, destination = "/portfolio"): Promise<void> {
  await page.goto(`/login?returnTo=${encodeURIComponent(destination)}`);
  await expect(page).toHaveURL(/\/login(?:\?|$)/u);
  await page.getByRole("button", { name: "ورود امن" }).click();
  await expect(page).toHaveURL(/localhost:8081\/realms\/pmcs\/protocol\/openid-connect\/auth/u);

  await page.locator("#username").fill(requiredEnvironment("PMCS_E2E_USER_EMAIL"));
  await page.locator("#password").fill(requiredEnvironment("PMCS_E2E_USER_PASSWORD"));
  await page.locator("#kc-login").click();

  await expect(page).toHaveURL(new RegExp(`${escapeRegularExpression(destination)}(?:[?#]|$)`, "u"));
  await expect(page.getByRole("heading", { name: destination === "/portfolio"
    ? "مرکز فرمان سبد پروژه‌ها"
    : "مرکز فرمان پروژه" })).toBeVisible();
}

export async function readStoredOperations(page: Page): Promise<readonly StoredOperation[]> {
  return page.evaluate(async ({ expectedTenantId, expectedUserId }) => {
    const databaseName = `pmcs-field-v3-${expectedTenantId}-${expectedUserId}`;
    return new Promise<StoredOperation[]>((resolve, reject) => {
      const request = indexedDB.open(databaseName);
      request.onerror = () => reject(request.error ?? new Error("IndexedDB open failed."));
      request.onsuccess = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains("operations")) {
          database.close();
          resolve([]);
          return;
        }
        const transaction = database.transaction("operations", "readonly");
        const all = transaction.objectStore("operations").getAll();
        all.onerror = () => reject(all.error ?? new Error("IndexedDB read failed."));
        all.onsuccess = () => {
          const operations = (all.result as Array<{
            operationId: string;
            tenantId: string;
            userId: string;
            projectId: string;
            status: string;
            payload?: { description?: string };
          }>).map((operation) => ({
            operationId: operation.operationId,
            tenantId: operation.tenantId,
            userId: operation.userId,
            projectId: operation.projectId,
            status: operation.status,
            description: operation.payload?.description,
          }));
          database.close();
          resolve(operations);
        };
      };
    });
  }, { expectedTenantId: tenantId, expectedUserId: userId });
}

export async function hasOfflineLease(page: Page): Promise<boolean> {
  return page.evaluate(async ({ expectedTenantId, expectedUserId, expectedProjectId }) => {
    const databaseName = `pmcs-field-v3-${expectedTenantId}-${expectedUserId}`;
    return new Promise<boolean>((resolve, reject) => {
      const request = indexedDB.open(databaseName);
      request.onerror = () => reject(request.error ?? new Error("IndexedDB open failed."));
      request.onsuccess = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains("sync-metadata")) {
          database.close();
          resolve(false);
          return;
        }
        const transaction = database.transaction("sync-metadata", "readonly");
        const manifest = transaction.objectStore("sync-metadata").get(`manifest:${expectedProjectId}`);
        manifest.onerror = () => reject(manifest.error ?? new Error("Sync manifest read failed."));
        manifest.onsuccess = () => {
          const value = manifest.result as { leaseExpiresAt?: string; allowedOperations?: string[] } | undefined;
          database.close();
          resolve(Boolean(
            value?.leaseExpiresAt &&
            Date.parse(value.leaseExpiresAt) > Date.now() &&
            value.allowedOperations?.includes("CaptureDailyReportFact"),
          ));
        };
      };
    });
  }, { expectedTenantId: tenantId, expectedUserId: userId, expectedProjectId: projectId });
}

export async function waitForProjectReady(page: Page): Promise<void> {
  await page.goto(projectPath);
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await expect.poll(() => hasOfflineLease(page), { timeout: 30_000 }).toBe(true);
  await expect.poll(async () => page.locator("#fact-location option").count(), { timeout: 30_000 })
    .toBeGreaterThan(1);
}

function requiredEnvironment(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is required for UI/E2E verification.`);
  return value;
}

function escapeRegularExpression(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/gu, "\\$&");
}
