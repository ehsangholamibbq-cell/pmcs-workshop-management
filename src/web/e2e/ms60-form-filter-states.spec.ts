import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms60/index.html");
const output = resolve(root, "src/web/artifacts/ms60-form-filter-states");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS60 form and filter states preserve truthful dates, access and results", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const requests: string[] = [];
  page.on("request", request => { if (/^https?:/u.test(request.url())) requests.push(request.url()); });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number) {
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    const imageWidth = bytes.readUInt32BE(16), imageHeight = bytes.readUInt32BE(20);
    expect(imageWidth).toBe(width);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length, width: imageWidth, height: imageHeight });
  }
  for (const [width, height] of [[1280, 800], [390, 844], [320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(prototype).href);
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    for (const [key, title, rows, enabled] of [
      ["ready", "آمادهٔ بررسی", true, true],
      ["invalid", "تاریخ نامعتبر", false, true],
      ["range", "بازهٔ ناسازگار", false, true],
      ["loading", "در حال دریافت نمونه", false, false],
      ["success", "فیلتر نمایشی اعمال شد", true, true],
      ["empty", "نتیجه‌ای نیست", false, true],
      ["error", "دریافت کامل نشد", false, true],
      ["offline", "اتصال برقرار نیست", false, false],
      ["permission", "دسترسی مجاز نیست", false, false],
      ["stale", "نتیجهٔ کهنه", false, true],
    ] as const) {
      await page.locator("#state").selectOption(key);
      await expect(page.locator("body")).toHaveAttribute("data-state", key);
      await expect(page.locator("#status-title")).toHaveText(title);
      if (rows) await expect(page.locator("#table-wrap")).toBeVisible();
      else await expect(page.locator("#table-wrap")).toBeHidden();
      if (enabled) await expect(page.locator("#apply")).toBeEnabled();
      else await expect(page.locator("#apply")).toBeDisabled();
      if (key === "loading") await expect(page.locator("#skeleton")).toBeVisible();
      else await expect(page.locator("#skeleton")).toBeHidden();
      if (key === "invalid" || key === "range") {
        const field = page.locator(key === "invalid" ? "#start" : "#end");
        const error = page.locator(key === "invalid" ? "#start-error" : "#end-error");
        await expect(field).toHaveAttribute("aria-invalid", "true");
        await expect(error).toBeVisible();
        await page.locator("#apply").click();
        await expect(field).toBeFocused();
        await expect(page.locator("body")).toHaveAttribute("data-state", key);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px ${key}`).toBe(true);
      if (width === 1280 || (width === 390 && ["invalid", "success", "permission"].includes(key)) ||
          (width === 320 && ["range", "empty", "offline"].includes(key))) {
        await capture(`form-${width}-${key}.png`, width);
      }
    }
    await page.locator("#state").selectOption("ready");
    await page.locator("#kind").selectOption("done");
    await page.locator("#apply").click();
    await expect(page.locator("body")).toHaveAttribute("data-state", "empty");
    await expect(page.locator("#table-wrap")).toBeHidden();
    await page.locator("#clear").click();
    await expect(page.locator("#start")).toBeFocused();
    await expect(page.locator("#start")).toHaveValue("۱۴۰۵/۰۷/۰۱");
  }
  expect(requests).toEqual([]);
  expect(new Set(files.map(file => file.name)).size).toBe(files.length);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS60", prototypeSha256: digest(readFileSync(prototype)), files,
  }, null, 2) + "\n");
});
