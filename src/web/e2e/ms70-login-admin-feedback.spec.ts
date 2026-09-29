import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms70-login-admin-feedback");

test("login presentation administration keeps list and draft states honest", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];

  async function capture(name: string, width: number, height: number, selector: string) {
    await page.locator(selector).scrollIntoViewIfNeeded();
    await page.evaluate(() => document.fonts.ready);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let releaseLoad: () => void = () => {};
  const loadGate = new Promise<void>(resolve => { releaseLoad = resolve; });
  let releaseSave: () => void = () => {};
  const saveGate = new Promise<void>(resolve => { releaseSave = resolve; });
  let listRequests = 0;
  await page.route("**/api/pmcs/api/v1/identity/login-experiences", async route => {
    if (route.request().method() === "GET") {
      listRequests += 1;
      if (listRequests === 1) {
        await loadGate;
        return route.fulfill({ status: 503, contentType: "application/problem+json",
          body: JSON.stringify({ status: 503, code: "service.unavailable", title: "Sensitive upstream error" }) });
      }
      return route.continue();
    }
    await saveGate;
    return route.fulfill({ status: 503, contentType: "application/problem+json",
      body: JSON.stringify({ status: 503, code: "service.unavailable", title: "Sensitive upstream error" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/login-experience");
  const panel = page.locator(".login-version-panel");
  const feedback = panel.locator(".login-admin-feedback");
  await expect(panel).toHaveAttribute("aria-busy", "true");
  await expect(feedback).toHaveAttribute("role", "status");
  await expect(feedback).toContainText("در حال دریافت نسخه‌ها…");
  await expect(panel.locator(".empty-state")).toHaveCount(0);
  await capture("login-admin-390-loading.png", 390, 844, ".login-version-panel");

  releaseLoad();
  await expect(panel).toHaveAttribute("aria-busy", "false");
  await expect(feedback).toHaveAttribute("role", "alert");
  await expect(feedback).toContainText("در حال حاضر مشکلی در سرور رخ داده است");
  await expect(feedback).not.toContainText("Sensitive upstream error");
  await expect(panel.locator(".empty-state")).toHaveCount(0);
  await capture("login-admin-390-list-error.png", 390, 844, ".login-version-panel");

  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت نسخه‌ها" }).click();
  await expect(panel).toHaveAttribute("aria-busy", "false");
  await expect(panel.getByRole("button", { name: "تلاش دوباره برای دریافت نسخه‌ها" })).toHaveCount(0);
  await expect(feedback).toHaveCount(0);

  await page.setViewportSize({ width: 320, height: 720 });
  const form = page.locator(".login-admin-grid > form");
  const headline = form.getByLabel("تیتر اصلی");
  await headline.fill("نسخه آزمون بازخورد");
  await form.getByRole("button", { name: "ساخت نسخه پیش‌نویس" }).click();
  await expect(form).toHaveAttribute("aria-busy", "true");
  await expect(headline).toBeDisabled();
  await expect(form.getByRole("combobox", { name: "شدت حرکت" })).toBeDisabled();
  await expect(feedback).toHaveAttribute("role", "status");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture("login-admin-320-saving.png", 320, 720, ".login-admin-grid > form");

  releaseSave();
  await expect(form).toHaveAttribute("aria-busy", "false");
  await expect(headline).toBeEnabled();
  await expect(headline).toHaveValue("نسخه آزمون بازخورد");
  await expect(feedback).toHaveAttribute("role", "alert");
  await expect(feedback).toContainText("در حال حاضر مشکلی در سرور رخ داده است");
  await capture("login-admin-320-draft-error.png", 320, 720, ".login-version-panel");

  expect(listRequests).toBeGreaterThanOrEqual(2);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/admin/login-experience", owner: "VX-G4 login administration feedback", files,
  }, null, 2) + "\n");
});
