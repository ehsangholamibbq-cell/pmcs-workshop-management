import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectPath } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms66-shell-navigation");
const cases = [
  { id: "identity", path: "/admin/users", heading: "کاربران، دعوت‌ها و عضویت پروژه", label: "بخش‌های مدیریت هویت" },
  { id: "portfolio-reports", path: "/portfolio/reports", heading: "گزارش‌های سبد در دسترس نیستند", label: "بخش‌های گزارش سبد" },
  { id: "project-chat", path: `${projectPath}/collaboration`, heading: "گفت‌وگو در این پروژه در دسترس نیست", label: "بخش‌های گفت‌وگوی پروژه" },
  { id: "project-reports", path: `${projectPath}/reports`, heading: "گزارش‌گیری در این پروژه در دسترس نیست", label: "بخش‌های گزارش پروژه" },
] as const;

test("remaining active Shells disclose only their existing links without mobile overflow", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];

  async function capture(name: string, width: number, height: number) {
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.evaluate(() => document.fonts.ready);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  for (const route of cases) {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(route.path);
    await expect(page.getByRole("heading", { name: route.heading })).toBeVisible();
    const sidebar = page.locator(".disclosure-sidebar");
    const desktop = sidebar.getByRole("navigation", { name: route.label });
    await expect(desktop).toBeVisible();
    const permitted = await desktop.getByRole("link").allTextContents();
    const active = await desktop.locator(".nav-item.active").textContent();

    for (const [width, height] of [[390, 844], [320, 720]] as const) {
      await page.setViewportSize({ width, height });
      await expect(desktop).toBeHidden();
      const toggle = sidebar.getByRole("button", { name: "باز کردن فهرست بخش‌ها" });
      const mobile = sidebar.locator(".sidebar-mobile-navigation nav");
      await expect(toggle).toHaveAttribute("aria-expanded", "false");
      await expect(mobile).toBeHidden();
      expect(await sidebar.evaluate(element => element.scrollWidth <= element.clientWidth), `${route.id} ${width}px sidebar`).toBe(true);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${route.id} ${width}px document`).toBe(true);
      await capture(`${route.id}-${width}-closed.png`, width, height);

      await toggle.focus();
      await page.keyboard.press("Enter");
      await expect(sidebar.getByRole("button", { name: "بستن فهرست بخش‌ها" })).toHaveAttribute("aria-expanded", "true");
      await expect(mobile).toBeVisible();
      expect(await mobile.getByRole("link").allTextContents()).toEqual(permitted);
      expect(await mobile.locator(".nav-item.active").textContent()).toBe(active);
      await page.keyboard.press("Tab");
      await expect(mobile.getByRole("link").first()).toBeFocused();
      await capture(`${route.id}-${width}-open.png`, width, height);
      const bounds = await mobile.boundingBox();
      expect(bounds?.x ?? -1).toBeGreaterThanOrEqual(0);
      expect((bounds?.x ?? width) + (bounds?.width ?? width)).toBeLessThanOrEqual(width + 1);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${route.id} ${width}px open document`).toBe(true);
      await page.keyboard.press("Escape");
      await expect(mobile).toBeHidden();
      await expect(toggle).toBeFocused();
    }
  }

  expect(new Set(files.map(file => file.name)).size).toBe(16);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    owner: "VX-G4 wave 3", routes: cases.map(({ id, path }) => ({ id, path })),
    viewports: [390, 320], files,
  }, null, 2) + "\n");
});
