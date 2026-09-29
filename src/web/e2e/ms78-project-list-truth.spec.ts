import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms78-project-list-truth");

test("the project list hides old cards during refresh and distinguishes a failed read from empty", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number) {
    await page.locator(".project-list-section").scrollIntoViewIfNeeded();
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let reads = 0;
  let releaseRefresh: () => void = () => {};
  const refreshGate = new Promise<void>(resolve => { releaseRefresh = resolve; });
  await page.route("**/api/pmcs/api/v1/projects", async route => {
    reads += 1;
    if (reads === 2) {
      await refreshGate;
      return route.fulfill({ status: 503, contentType: "application/problem+json",
        body: JSON.stringify({ status: 503, title: "Sensitive upstream error" }) });
    }
    return route.continue();
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  const main = page.locator(".project-landing");
  await expect(main).toHaveAttribute("data-project-read-state", "current");
  const cards = page.locator(".project-card");
  expect(await cards.count()).toBeGreaterThan(0);
  await capture("projects-390-current.png", 390, 844);

  await page.getByRole("button", { name: "تازه‌سازی" }).click();
  await expect(main).toHaveAttribute("data-project-read-state", "loading");
  await expect(cards).toHaveCount(0);
  await expect(page.getByRole("button", { name: "ایجاد پیش‌نویس پروژه" })).toBeDisabled();
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("projects-320-refreshing.png", 320, 720);

  releaseRefresh();
  await expect(main).toHaveAttribute("data-project-read-state", "error");
  await expect(cards).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "فهرست پروژه‌ها در دسترس نیست" })).toBeVisible();
  await expect(page.locator(".empty-project-state.error")).toHaveCSS("border-top-color", "rgb(170, 55, 64)");
  await expect(page.getByRole("heading", { name: "پروژه‌ای برای نمایش وجود ندارد" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "ایجاد پیش‌نویس پروژه" })).toBeDisabled();
  await expect(main).not.toContainText("Sensitive upstream error");
  await capture("projects-320-read-error.png", 320, 720);

  await page.getByRole("button", { name: "تازه‌سازی" }).click();
  await expect(main).toHaveAttribute("data-project-read-state", "current");
  expect(await cards.count()).toBeGreaterThan(0);
  await capture("projects-320-restored.png", 320, 720);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/", owner: "VX-G4 project list refresh truth", files,
  }, null, 2) + "\n");
});
