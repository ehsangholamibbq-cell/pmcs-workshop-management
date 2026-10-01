import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms58/index.html");
const output = resolve(root, "src/web/artifacts/ms58-reporting-print");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS58 print remains a bounded review and never grants official output access", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const requests: string[] = [];
  page.on("request", request => { if (/^https?:/u.test(request.url())) requests.push(request.url()); });
  const files: Array<{ name: string; sha256: string; bytes: number; width?: number; height?: number }> = [];
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
    for (const [key, title, sourceVisible] of [
      ["empty", "بدون تصویر رسمی", true], ["requested", "درخواست‌شده", true],
      ["processing", "در حال تهیه", true], ["ready", "آمادهٔ نمایشی", true],
      ["rejected", "ردشده", true], ["expired", "منقضی", true],
      ["permission", "دسترسی مجاز نیست", false], ["offline", "اتصال برقرار نیست", true],
      ["error", "دریافت کامل نشد", true],
    ] as const) {
      await page.locator("#state").selectOption(key);
      await expect(page.locator("body")).toHaveAttribute("data-state", key);
      await expect(page.locator("#status-title")).toHaveText(title);
      await expect(page.locator("#sheet-title")).toHaveText(title);
      if (sourceVisible) await expect(page.locator("#official-facts")).toBeVisible();
      else await expect(page.locator("#official-facts")).toBeHidden();
      await expect(page.locator("#request")).toBeDisabled();
      await expect(page.locator("#download")).toBeDisabled();
      await expect(page.locator("#sheet")).toContainText("گزارش رسمی امضاشده یا خروجی مرکز گزارش‌ها نیست");
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px ${key}`).toBe(true);
      if (width === 1280 || (width === 390 && ["ready", "permission"].includes(key)) ||
          (width === 320 && ["empty", "offline"].includes(key))) {
        await capture(`report-${width}-${key}.png`, width);
      }
    }
  }
  await page.setViewportSize({ width: 1280, height: 800 });
  for (const [state, size, landscape] of [
    ["empty", "A4", false], ["empty", "A4", true],
    ["empty", "A3", false], ["empty", "A3", true],
    ["ready", "A4", false],
  ] as const) {
    await page.locator("#state").selectOption(state);
    await page.emulateMedia({ media: "print", reducedMotion: "reduce" });
    await expect(page.locator(".screen:visible")).toHaveCount(0);
    await expect(page.locator("#sheet")).toBeVisible();
    await expect(page.locator("#sheet button, #sheet select, #sheet input")).toHaveCount(0);
    const bytes = await page.pdf({ format: size, landscape, printBackground: true, preferCSSPageSize: false });
    expect(bytes.subarray(0, 4).toString()).toBe("%PDF");
    const name = `report-print-${state}-${size.toLowerCase()}-${landscape ? "landscape" : "portrait"}.pdf`;
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length });
    await page.emulateMedia({ media: "screen" });
  }
  expect(requests).toEqual([]);
  expect(new Set(files.map(file => file.name)).size).toBe(files.length);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS58", prototypeSha256: digest(readFileSync(prototype)), files,
  }, null, 2) + "\n");
});
