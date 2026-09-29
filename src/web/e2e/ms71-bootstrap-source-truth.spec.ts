import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms71-bootstrap-source-truth");

test("bootstrap wizard never calls an unavailable source or member directory empty", async ({ page }) => {
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

  let releaseProjects: () => void = () => {};
  const projectGate = new Promise<void>(resolve => { releaseProjects = resolve; });
  let projectRequests = 0;
  let memberRequests = 0;
  let createRequests = 0;
  await page.route("**/api/pmcs/api/v1/projects", async route => {
    projectRequests += 1;
    if (projectRequests === 1) {
      await projectGate;
      return route.fulfill({ status: 503, contentType: "application/problem+json",
        body: JSON.stringify({ status: 503, title: "Sensitive upstream error" }) });
    }
    return route.continue();
  });
  await page.route("**/api/pmcs/api/v1/identity/directory", async route => {
    memberRequests += 1;
    if (memberRequests === 1) await projectGate;
    if (memberRequests <= 2) return route.fulfill({ status: 503, contentType: "application/problem+json",
      body: JSON.stringify({ status: 503, title: "Sensitive upstream error" }) });
    return route.continue();
  });
  await page.route("**/api/pmcs/api/v1/project-bootstraps", async route => {
    createRequests += 1;
    await route.fulfill({ status: 500 });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/project-bootstraps");
  const message = page.locator(".bootstrap-message");
  const source = page.getByRole("combobox", { name: "پروژه مبدأ" });
  await expect(message).toHaveAttribute("role", "status");
  await expect(source).toBeDisabled();
  await capture("bootstrap-390-loading.png", 390, 844, ".bootstrap-message");

  releaseProjects();
  await expect(message).toHaveAttribute("role", "alert");
  await expect(message).toContainText("در حال حاضر مشکلی در سرور رخ داده است");
  await expect(message).not.toContainText("Sensitive upstream error");
  await expect(source).toBeDisabled();
  await expect(source).toContainText("فهرست پروژه‌ها در دسترس نیست");
  await capture("bootstrap-390-project-error.png", 390, 844, ".bootstrap-message");

  await page.getByRole("button", { name: "تلاش دوباره برای دریافت داده‌ها" }).click();
  await expect(source).toBeEnabled();
  await expect(message).toHaveAttribute("role", "alert");
  await expect(message).toContainText("فهرست اعضا در دسترس نیست");
  await page.setViewportSize({ width: 320, height: 720 });
  await page.getByRole("navigation", { name: "مراحل ساخت پروژه" }).getByRole("button", { name: /اعضا/u }).click();
  await expect(page.getByText("فهرست اعضا در دسترس نیست؛ پیش‌نمایش با دستهٔ اعضا مجاز نیست.")).toBeVisible();
  const preview = page.getByRole("button", { name: "ایجاد مقصد پیش‌نویس و نمایش پیش‌نمایش" });
  await expect(preview).toBeDisabled();
  await capture("bootstrap-320-members-error.png", 320, 720, ".bootstrap-member-list");

  await page.getByRole("button", { name: "تلاش دوباره برای دریافت داده‌ها" }).click();
  await expect(message).toHaveAttribute("role", "status");
  await expect(preview).toBeEnabled();
  await expect(page.getByText("فهرست اعضا در دسترس نیست؛ پیش‌نمایش با دستهٔ اعضا مجاز نیست.")).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture("bootstrap-320-ready.png", 320, 720, ".bootstrap-member-list");
  expect(createRequests).toBe(0);
  expect(projectRequests).toBe(3);
  expect(memberRequests).toBe(3);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/project-bootstraps", owner: "VX-G4 bootstrap source/member truth", files,
  }, null, 2) + "\n");
});
