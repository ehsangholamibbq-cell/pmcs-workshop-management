import { chromium, test, expect, type BrowserContext } from "@playwright/test";
import {
  loginThroughOidc,
  projectId,
  projectPath,
  readStoredOperations,
  tenantId,
  userId,
  waitForProjectReady,
} from "./support";

test("offline queue survives a browser restart and reconnects exactly once", async ({}, testInfo) => {
  const profileDirectory = testInfo.outputPath("persistent-browser-profile");
  const baseURL = process.env.PMCS_E2E_BASE_URL ?? "http://localhost:3000";
  const description = `واقعیت آفلاین آزمون مرورگر ${Date.now()}`;
  let context: BrowserContext | undefined;

  try {
    context = await chromium.launchPersistentContext(profileDirectory, {
      baseURL,
      headless: true,
      locale: "fa-IR",
      timezoneId: "Asia/Tehran",
      serviceWorkers: "allow",
    });
    let page = context.pages()[0] ?? await context.newPage();
    await loginThroughOidc(page);
    await waitForProjectReady(page);

    await page.evaluate(async () => {
      await navigator.serviceWorker.ready;
      if (navigator.serviceWorker.controller) return;
      await new Promise<void>((resolve) => {
        navigator.serviceWorker.addEventListener("controllerchange", () => resolve(), { once: true });
      });
    });

    await context.setOffline(true);
    await expect(page.getByText("آفلاین", { exact: true })).toBeVisible();

    await page.locator("#fact-location").selectOption({ index: 1 });
    await page.getByRole("button", { name: "ذخیره پیش‌نویس آفلاین" }).click();
    await expect(page.locator(".capture-form output")).toContainText("شرح واقعیت الزامی است");

    await page.locator("#fact-category").fill("فعالیت آزمون مرورگر");
    await page.locator("#fact-quantity").fill("1");
    await page.locator("#fact-unit").fill("متر");
    await page.locator("#fact-description").fill(description);
    await page.getByRole("button", { name: "ذخیره پیش‌نویس آفلاین" }).click();
    await expect(page.locator(".capture-form output")).toContainText("روی این دستگاه ذخیره شد");
    await expect(page.locator(".sync-pill")).toContainText("۱ عملیات");

    await expect.poll(async () => {
      const operations = await readStoredOperations(page);
      return operations.filter((operation) => operation.description === description && operation.status === "queued").length;
    }).toBe(1);

    await context.close();
    context = undefined;

    context = await chromium.launchPersistentContext(profileDirectory, {
      baseURL,
      headless: true,
      locale: "fa-IR",
      timezoneId: "Asia/Tehran",
      serviceWorkers: "allow",
      offline: true,
    });
    page = context.pages()[0] ?? await context.newPage();
    await page.goto(projectPath);
    await expect(page.getByRole("heading", { name: "اتصال به سامانه برقرار نیست" })).toBeVisible();

    const afterRestart = await readStoredOperations(page);
    expect(afterRestart).toContainEqual(expect.objectContaining({
      tenantId,
      userId,
      projectId,
      status: "queued",
      description,
    }));

    await context.setOffline(false);
    await page.reload();
    await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
    await expect(page.locator(".sync-pill")).toContainText("۰ عملیات", { timeout: 45_000 });
    await expect.poll(async () => {
      const operations = await readStoredOperations(page);
      return operations.filter((operation) => operation.description === description && operation.status === "synced").length;
    }, { timeout: 45_000 }).toBe(1);
  } finally {
    await context?.close();
  }
});
