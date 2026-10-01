import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms64-portfolio-navigation");

test("active Portfolio navigation discloses permitted links without horizontal scroll", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];

  async function capture(name: string, width: number) {
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.evaluate(() => document.fonts.ready);
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height: bytes.readUInt32BE(20) });
  }

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/portfolio");
  await expect(page.getByRole("heading", { name: "مرکز فرمان سبد پروژه‌ها" })).toBeVisible();
  const sidebar = page.locator(".disclosure-sidebar");
  const desktop = sidebar.locator(".sidebar-desktop-navigation");
  const mobile = sidebar.locator(".sidebar-mobile-navigation nav");
  await expect(desktop).toBeVisible();
  await expect(mobile).toBeHidden();
  await expect(desktop.getByRole("link", { name: "سبد پروژه‌ها" })).toHaveAttribute("aria-current", "page");
  const permitted = await desktop.getByRole("link").allTextContents();

  for (const [width, height] of [[820, 1180], [390, 844], [320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await expect(desktop).toBeHidden();
    const toggle = sidebar.getByRole("button", { name: "باز کردن فهرست بخش‌ها" });
    await expect(toggle).toBeVisible();
    await expect(toggle).toHaveAttribute("aria-expanded", "false");
    await expect(mobile).toBeHidden();
    expect(await sidebar.evaluate(element => element.scrollWidth <= element.clientWidth), `${width}px sidebar`).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px document`).toBe(true);
    await capture(`portfolio-${width}-closed.png`, width);

    await toggle.focus();
    await page.keyboard.press("Enter");
    const close = sidebar.getByRole("button", { name: "بستن فهرست بخش‌ها" });
    await expect(close).toHaveAttribute("aria-expanded", "true");
    await expect(mobile).toBeVisible();
    expect(await mobile.getByRole("link").allTextContents()).toEqual(permitted);
    await page.keyboard.press("Tab");
    await expect(mobile.getByRole("link", { name: "سبد پروژه‌ها" })).toBeFocused();
    await capture(`portfolio-${width}-open.png`, width);
    const bounds = await mobile.boundingBox();
    expect(bounds?.x ?? -1).toBeGreaterThanOrEqual(0);
    expect((bounds?.x ?? width) + (bounds?.width ?? width)).toBeLessThanOrEqual(width + 1);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px open document`).toBe(true);
    await page.keyboard.press("Escape");
    await expect(mobile).toBeHidden();
    await expect(toggle).toBeFocused();
    await expect(toggle).toHaveAttribute("aria-expanded", "false");
  }

  const toggle = sidebar.getByRole("button", { name: "باز کردن فهرست بخش‌ها" });
  await toggle.click();
  await mobile.getByRole("link", { name: "اقدامات کلیدی" }).click();
  await expect(page).toHaveURL(/\/portfolio#exceptions$/u);
  await expect(mobile).toBeHidden();
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  expect(new Set(files.map(file => file.name)).size).toBe(6);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/portfolio", owner: "VX-G4 wave 1", viewports: [820, 390, 320], files,
  }, null, 2) + "\n");
});
