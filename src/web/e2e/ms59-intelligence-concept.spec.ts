import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms59/index.html");
const output = resolve(root, "src/web/artifacts/ms59-intelligence-concept");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS59 hypothetical insight never becomes a fact or action", async ({ page }) => {
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
    for (const [key, title, insight, reviewable] of [
      ["empty", "تحلیلی موجود نیست", false, false],
      ["loading", "در حال بررسی نمونه", false, false],
      ["proposal", "پیشنهاد آزمایشی", true, true],
      ["uncertain", "عدم قطعیت بالا", true, true],
      ["stale", "منبع کهنه", true, false],
      ["permission", "دسترسی مجاز نیست", false, false],
      ["offline", "اتصال برقرار نیست", false, false],
      ["error", "تحلیل کامل نشد", false, false],
      ["conflict", "نسخهٔ منبع تغییر کرده است", false, false],
    ] as const) {
      await page.locator("#state").selectOption(key);
      await expect(page.locator("body")).toHaveAttribute("data-state", key);
      await expect(page.locator("#status-title")).toHaveText(title);
      if (insight) await expect(page.locator("#insight")).toBeVisible();
      else await expect(page.locator("#insight")).toBeHidden();
      if (reviewable) await expect(page.locator("#review")).toBeEnabled();
      else await expect(page.locator("#review")).toBeDisabled();
      await expect(page.locator("#draft")).toBeDisabled();
      await expect(page.locator("#action")).toBeDisabled();
      if (key === "loading") await expect(page.locator("#skeleton")).toBeVisible();
      else await expect(page.locator("#skeleton")).toBeHidden();
      if (key === "permission") await expect(page.getByText("دو رکورد ساختگی در پروژهٔ نمونه")).toBeHidden();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px ${key}`).toBe(true);
      if (width === 1280 || (width === 390 && ["proposal", "stale", "permission"].includes(key)) ||
          (width === 320 && ["uncertain", "offline", "error"].includes(key))) {
        await capture(`intelligence-${width}-${key}.png`, width);
      }
    }
    await page.locator("#state").selectOption("proposal");
    await page.locator("#review").click();
    await expect(page.locator("#review-dialog")).toBeVisible();
    await expect(page.locator("#close")).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(page.locator("#review-dialog")).toBeHidden();
    await expect(page.locator("#review")).toBeFocused();
    await page.locator("#review").click();
    await page.locator("#close").click();
    await expect(page.locator("#review-result")).toContainText("هیچ داده یا اقدامی ثبت نشد");
    await page.locator("#state").selectOption("conflict");
    await expect(page.locator("#review-result")).not.toContainText("مرور محلی پایان یافت");
  }
  expect(requests).toEqual([]);
  expect(new Set(files.map(file => file.name)).size).toBe(files.length);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS59", prototypeSha256: digest(readFileSync(prototype)), files,
  }, null, 2) + "\n");
});
