import { expect, test, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { auditSurface, evidence } from "./vx-g5-support";

test("Login design publishes and rolls back through the real UI while failed custom assets fall back safely", async ({ page, browser }, testInfo) => {
  test.setTimeout(240_000);
  const report = evidence(testInfo, "login-rollback");
  const initial = await page.request.get("/api/login-experience");
  expect(initial.ok()).toBe(true);
  const original = await initial.json() as { version: number; headline: string };
  expect(original.version).toBeGreaterThan(0);
  const headline = `بررسی مستقل طراحی ورود ${testInfo.project.name}`;
  const context = await browser.newContext({ storageState: { cookies: [], origins: [] },
    locale: "fa-IR", timezoneId: "Asia/Tehran", viewport: { width: 320, height: 900 }, reducedMotion: "reduce" });
  const guest = await context.newPage();
  let changed = false;
  async function restore(admin: Page) {
    await admin.goto("/admin/login-experience");
    const version = admin.locator(".login-version-list article").filter({
      has: admin.locator("strong", { hasText: new RegExp(`^نسخه ${original.version.toLocaleString("fa-IR")}$`, "u") }),
    });
    await version.getByRole("button", { name: "بازگشت به این نسخه", exact: true }).click();
    await expect(version.locator('[data-status="Published"]')).toBeVisible();
    await expect.poll(async () => (await (await admin.request.get("/api/login-experience")).json()).version).toBe(original.version);
    changed = false;
  }
  try {
    await page.goto("/admin/login-experience");
    await expect(page.getByRole("heading", { name: "مدیریت ظاهر صفحه ورود", exact: true })).toBeVisible();
    await page.getByLabel("تیتر اصلی").fill(headline);
    const png = readFileSync("public/brand/bbq-official-symbol.png");
    await page.getByLabel("لوگو اختیاری").setInputFiles({ name: "g5-logo.png", mimeType: "image/png", buffer: png });
    await page.getByLabel("تصویر زمینه اختیاری").setInputFiles({ name: "g5-hero.png", mimeType: "image/png", buffer: png });
    await page.getByRole("button", { name: "ساخت نسخه پیش‌نویس", exact: true }).click();
    const candidate = page.locator(".login-version-list article").filter({ hasText: headline });
    await expect(candidate).toContainText("پیش‌نویس");
    await candidate.getByRole("button", { name: "انتشار", exact: true }).click();
    changed = true;
    await expect(candidate.locator('[data-status="Published"]')).toBeVisible();
    const descriptor = await (await page.request.get("/api/login-experience")).json();
    expect(descriptor.headline).toBe(headline);
    expect(descriptor.logoUrl).toBeTruthy(); expect(descriptor.heroUrl).toBeTruthy();
    await guest.route("**/api/login-experience/assets/**", route => route.abort("failed"));
    await guest.goto("/login");
    await expect(guest.getByRole("heading", { name: headline, exact: true })).toBeVisible();
    await expect(guest.getByRole("button", { name: "ورود امن", exact: true })).toBeEnabled();
    const official = guest.getByRole("img", { name: "نشان رسمی بتن بسپار قزوین", exact: true });
    await expect(official).toBeVisible();
    await expect.poll(() => official.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
    await expect(guest.locator(".login-hero-media img")).toHaveCount(0);
    await auditSurface(guest, report, "published-custom-assets-failed-320");
    await report.capture(guest, "published-custom-assets-failed-320.png");
    await restore(page);
    await guest.reload();
    await expect(guest.getByRole("heading", { name: original.headline, exact: true })).toBeVisible();
    await expect(guest.getByRole("button", { name: "ورود امن", exact: true })).toBeEnabled();
    await report.capture(guest, "rolled-back-320.png");
  } finally {
    await context.close();
    if (changed) await restore(page);
  }
});
